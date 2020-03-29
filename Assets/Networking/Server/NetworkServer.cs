using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class NetworkServer : MonoBehaviour
{
    // Networking Fields
    private UdpClient m_Client;
    public static NetworkServer Instance;
    private NetworkFrame m_LastFrame;

    // Server Fields
    private string m_Password;
    /// <summary>
    /// A list of users who are authorized and can use the authorized frames
    /// </summary>
    private List<string> m_AuthorizedUsers;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning("[NetworkServer] A new network server was created, yet one already exists.");
            return; // We want to use the already existing network server
        }
        Instance = this;

        m_AuthorizedUsers = new List<string>();
    }

    public void Host(string address, int port, string password)
    {
        m_Password = password;
        if (NetworkManager.Instance.m_ConnectionType == NetworkManager.ENetworkConnectionType.Proxy)
        {
            m_Client = new UdpClient();
            m_Client.Connect(address, port);
            Debug.Log($"[Network Server] Connecting to proxy at {address}:{port}");
            // Attempt connect to proxy
        }
        else
        {
            m_Client = new UdpClient(port); // Host server
            Debug.Log($"[Network Server] Hosting server on port {port}");
            _ = OnReceiveFrame();
        }
    }

    private async Task OnReceiveFrame()
    {
        UdpReceiveResult result = await m_Client.ReceiveAsync();
        NetworkFrame frame = NetworkFrame.ReadFromBytes(result.Buffer);
        m_LastFrame = frame;

        if (NetworkManager.Instance.m_ConnectionType == NetworkManager.ENetworkConnectionType.Server) // A proxy will automatically handle this for us
        {
            frame.m_SenderAddress = $"{result.RemoteEndPoint.Address}:{result.RemoteEndPoint.Port}"; // This is the user who sent it
            frame.m_TargetAddress = "0.0.0.0:0000"; // For now this is recognized as sever
        }

        switch(frame.m_Type)
        {
            case NetworkFrame.NetworkFrameType.Handshake:
                {
                    if (!IsUserAuthorized(frame.m_SenderId)) // Must be authorized to use this frame
                        break;
                    NetworkHandshakeFrame handshake = NetworkFrame.Parse<NetworkHandshakeFrame>(result.Buffer);
                    Debug.Log($"[NetworkServer] Received handshake info: " + handshake.m_DisplayName);

                    NetworkSpawnRPC spawnRpc = new NetworkSpawnRPC(-1, -1, true);
                    OnRPCCommand(spawnRpc.ToString());
                } break;
            case NetworkFrame.NetworkFrameType.Authentication:
                {
                    NetworkAuthenticationFrame authenticationFrame = NetworkFrame.Parse<NetworkAuthenticationFrame>(result.Buffer);
                    if(authenticationFrame.m_Password != m_Password && m_Password != "")
                    {
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.IncorrectPassword;
                        authenticationFrame.Configure(frame);
                        SendFrame(authenticationFrame);
                    } else if (NetworkManager.Instance.m_ConnectionType == NetworkManager.ENetworkConnectionType.Proxy && !m_Client.Client.Connected)
                    {
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.Error;
                        authenticationFrame.m_Message = "server_error_proxy";
                        authenticationFrame.Configure(frame);
                        SendFrame(authenticationFrame);
                    }
                    else if (IsBanned(frame.m_SenderId).Item1) // Store this in a variable or cache later?
                    {
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.Banned;
                        authenticationFrame.m_Message = IsBanned(frame.m_SenderId).Item2;
                        authenticationFrame.Configure(frame);
                        SendFrame(authenticationFrame);
                    }
                    else if (NetworkManager.Instance.GetPlayerCount() == NetworkManager.Instance.m_MaxPlayers && NetworkManager.Instance.m_MaxPlayers > 0)
                    {
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.LobbyFull;
                        authenticationFrame.Configure(frame);
                        SendFrame(authenticationFrame);
                    }
                    else 
                    {
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.Connected;
                        authenticationFrame.Configure(frame);
                        m_AuthorizedUsers.Add(frame.m_SenderId);
                        SendFrame(authenticationFrame);
                        UpdatePlayer(frame);
                    }
                } break;
            case NetworkFrame.NetworkFrameType.Ping:
                {
                    if (!IsUserAuthorized(frame.m_SenderId)) // Must be authorized to use this frame
                        break;

                    NetworkFrame pingFrame = new NetworkFrame(NetworkFrame.NetworkFrameType.Ping, frame.m_SenderId, "server");
                    pingFrame.Configure(frame);
                    SendFrame(pingFrame);
                } break;
            case NetworkFrame.NetworkFrameType.RPC:
                {
                    if (!IsUserAuthorized(frame.m_SenderId)) // Must be authorized to use this frame
                        break;

                    NetworkRPCFrame rpcFrame = NetworkFrame.Parse<NetworkRPCFrame>(result.Buffer);
                    OnRPCCommand(rpcFrame.m_RPC);
                } break;
        }

        _ = OnReceiveFrame();
    }

    /// <summary>
    /// Updates a player to the most recent entity list
    /// </summary>
    private void UpdatePlayer(NetworkFrame refFrame)
    {
        if (NetworkManager.Instance.m_NetworkType == NetworkManager.ENetworkType.Mixed)
            return;

        foreach(var pair in NetworkManager.Instance.GetNetworkedObjects())
        {
            GameObject obj = pair.Value;
            if (obj == null)
                continue;
            NetworkObjectServerInfo info = obj.GetComponent<NetworkObjectServerInfo>();
            NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();

            NetworkSpawnRPC spawnRPC = new NetworkSpawnRPC(info.m_PrefabIndex, identity.m_NetworkId);
            SendRPC(spawnRPC, refFrame);
        }
    }

    private void OnRPCCommand(string command)
    {
        NetworkRPC rpc = NetworkRPC.FromString(command);

        switch (rpc.m_Type)
        {
            case NetworkRPCType.RPC_SPAWN:
                {
                    NetworkSpawnRPC spawnRPC = NetworkRPC.Parse<NetworkSpawnRPC>(command);

                    GameObject spawnPrefab = NetworkManager.Instance.GetObjectByIndex(spawnRPC.m_PrefabIndex);
                    GameObject prefab = Instantiate(spawnPrefab);
                    NetworkBehaviour networkBehaviour = prefab.GetComponent<NetworkBehaviour>();
                    NetworkObjectServerInfo serverInfo = prefab.AddComponent<NetworkObjectServerInfo>();
                    serverInfo.m_PrefabIndex = spawnRPC.m_PrefabIndex;

                    int id = UnityEngine.Random.Range(0, int.MaxValue);
                    prefab.GetComponent<NetworkIdentity>().m_NetworkId = id;
                    networkBehaviour.m_IsServer = true;
                    networkBehaviour.m_HasAuthority = false;
                    networkBehaviour.m_IsClient = false;
                    NetworkManager.Instance.AddObject(id, prefab);
                    if (NetworkManager.Instance.m_NetworkType == NetworkManager.ENetworkType.Mixed)
                    {
                        networkBehaviour.m_IsClient = true;
                    }
                    else
                    {
                        spawnRPC.m_NetworkIndex = id;
                        SendRPC(spawnRPC);
                    }

                    if(spawnRPC.m_RequestAuthority)
                    {
                        NetworkAuthorizationRPC authRPC = new NetworkAuthorizationRPC(
                            true,
                            (NetworkManager.Instance.m_NetworkType == NetworkManager.ENetworkType.Mixed ? true : false),
                            true, 
                            id
                        );
                        SendRPC(authRPC);
                    }
                }
                break;
        }
    }

    private void SendRPC(NetworkRPC rpc, NetworkFrame networkFrame = null)
    {
        NetworkRPCFrame frame;
        if (networkFrame == null)
        {
            frame = new NetworkRPCFrame(rpc.ToString(), m_LastFrame.m_SenderId);
            frame.Configure(m_LastFrame);
        }
        else
        {
            frame = new NetworkRPCFrame(rpc.ToString(), networkFrame.m_SenderId);
            frame.Configure(networkFrame);
        }

        SendFrame(frame);
    }

    /// <summary>
    /// Checks whether a user is authorized to send messages to the server
    /// </summary>
    /// <param name="Id">The Id of the user</param>
    /// <returns>True/False depending on if the user is authorized</returns>
    private bool IsUserAuthorized(string Id)
    {
        return m_AuthorizedUsers.Contains(Id);
    }

    /// <summary>
    /// Checks whether a user is banned or not
    /// TODO: Implement
    /// </summary>
    /// <param name="Id">The UserId of the user</param>
    /// <returns>A Tuple containing whether or not the user is banned, and if banned the reason why</returns>
    private Tuple<bool, string> IsBanned(string Id)
    {
        return new Tuple<bool, string>(false, "To be implemented");
    }

    private void SendFrame(NetworkFrame frame, IPEndPoint endpoint = null)
    {
        Debug.Log("Sending Frame to client: " + JsonUtility.ToJson(frame));

        byte[] bytes = frame.ToBytes();
        if(endpoint == null)
            m_Client.Send(bytes, bytes.Length, frame.GetTargetEndpoint());
        else
            m_Client.Send(bytes, bytes.Length, endpoint);
    }
}