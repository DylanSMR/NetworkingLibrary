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

    // Server Fields
    private string m_Password;
    /// <summary>
    /// A list of users who are authorized and can use the authorized frames
    /// </summary>
    private List<string> m_AuthorizedUsers;

    public Dictionary<string, IPEndPoint> m_IPMap;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning("[NetworkServer] A new network server was created, yet one already exists.");
            return; // We want to use the already existing network server
        }
        Instance = this;

        m_AuthorizedUsers = new List<string>();
        m_IPMap = new Dictionary<string, IPEndPoint>();
    }

    /// <summary>
    /// Attempts to host a networked server
    /// </summary>
    /// <param name="address">The address of the proxy/server</param>
    /// <param name="port">The port of the proxy/server</param>
    /// <param name="password">The password required to enter the server</param>
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

    /// <summary>
    /// Called when the server receives a networked frame
    /// </summary>
    /// <returns></returns>
    private async Task OnReceiveFrame()
    {
        UdpReceiveResult result = await m_Client.ReceiveAsync();
        NetworkFrame frame = NetworkFrame.ReadFromBytes(result.Buffer);

        if (NetworkManager.Instance.m_ConnectionType == NetworkManager.ENetworkConnectionType.Server) // A proxy will automatically handle this for us
        {
            frame.m_SenderAddress = $"{result.RemoteEndPoint.Address}:{result.RemoteEndPoint.Port}"; // This is the user who sent it
            frame.m_TargetAddress = "0.0.0.0:0000"; // For now this is recognized as sever
        }
        NetworkPlayer player = NetworkManager.Instance.GetPlayer(frame.m_SenderId);

        switch(frame.m_Type)
        {
            case NetworkFrame.NetworkFrameType.Handshake:
                {
                    if (!IsUserAuthorized(frame.m_SenderId)) // Must be authorized to use this frame
                        break;
                    NetworkHandshakeFrame handshake = NetworkFrame.Parse<NetworkHandshakeFrame>(result.Buffer);

                    NetworkManager.Instance.AddPlayer(handshake.m_SenderId, new NetworkPlayer()
                    {
                        m_Id = handshake.m_SenderId,
                        m_Name = handshake.m_DisplayName
                    });
                    player = NetworkManager.Instance.GetPlayer(frame.m_SenderId);

                    NetworkSpawnRPC spawnRpc = new NetworkSpawnRPC(-1, true);
                    OnRPCCommand(spawnRpc.ToString(), player);
                    UpdatePlayer(player);
                } break;
            case NetworkFrame.NetworkFrameType.Authentication:
                {
                    NetworkAuthenticationFrame authenticationFrame = NetworkFrame.Parse<NetworkAuthenticationFrame>(result.Buffer);
                    Debug.Log("[NetworkServer] Received auth frame: " + JsonUtility.ToJson(authenticationFrame));
                    if(authenticationFrame.m_Password != m_Password && m_Password != "")
                    {
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.IncorrectPassword;
                        authenticationFrame.ConfigureForServer(player);
                        SendFrame(authenticationFrame, player);
                    } else if (NetworkManager.Instance.m_ConnectionType == NetworkManager.ENetworkConnectionType.Proxy && !m_Client.Client.Connected)
                    {
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.Error;
                        authenticationFrame.m_Message = "server_error_proxy";
                        authenticationFrame.ConfigureForServer(player);
                        SendFrame(authenticationFrame, player);
                    }
                    else if (IsBanned(frame.m_SenderId).Item1) // Store this in a variable or cache later?
                    {
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.Banned;
                        authenticationFrame.m_Message = IsBanned(frame.m_SenderId).Item2;
                        authenticationFrame.ConfigureForServer(player);
                        SendFrame(authenticationFrame, player);
                    }
                    else if (NetworkManager.Instance.GetPlayerCount() == NetworkManager.Instance.m_MaxPlayers && NetworkManager.Instance.m_MaxPlayers > 0)
                    {
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.LobbyFull;
                        authenticationFrame.ConfigureForServer(player);
                        SendFrame(authenticationFrame, player);
                    }
                    else 
                    {
                        NetworkPlayer tempPlayer = new NetworkPlayer()
                        {
                            m_Id = frame.m_SenderId
                        };
                        m_AuthorizedUsers.Add(frame.m_SenderId);
                        m_IPMap.Add(frame.m_SenderId, frame.GetSenderEndpoint());

                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.Connected;
                        authenticationFrame.ConfigureForServer(tempPlayer);

                        Debug.Log("[NEtworkServer] Snding auth frame");
                        SendFrame(authenticationFrame, tempPlayer);
                    }
                } break;
            case NetworkFrame.NetworkFrameType.Ping:
                {
                    if (!IsUserAuthorized(frame.m_SenderId)) // Must be authorized to use this frame
                        break;

                    NetworkFrame pingFrame = new NetworkFrame(NetworkFrame.NetworkFrameType.Ping, frame.m_SenderId, "server");
                    pingFrame.ConfigureForServer(player);
                    SendFrame(pingFrame, player);
                } break;
            case NetworkFrame.NetworkFrameType.RPC:
                {
                    if (!IsUserAuthorized(frame.m_SenderId)) // Must be authorized to use this frame
                        break;

                    NetworkRPCFrame rpcFrame = NetworkFrame.Parse<NetworkRPCFrame>(result.Buffer);
                    OnRPCCommand(rpcFrame.m_RPC, player);
                } break;
        }

        _ = OnReceiveFrame();
    }

    /// <summary>
    /// Determines if the player is on the same instance as the server 
    /// </summary>
    /// <param name="player"></param>
    /// <returns></returns>
    public bool IsPlayerServer(NetworkPlayer player)
    {
        if (NetworkClient.Instance != null && NetworkClient.Instance.GetUniqueIndentifier() == player.m_Id)
            return true;

        return false;
    }

    /// <summary>
    /// Updates a player to the most recent entity list
    /// </summary>
    private void UpdatePlayer(NetworkPlayer player)
    {
        if (IsPlayerServer(player))
            return;

        foreach(var pair in NetworkManager.Instance.GetNetworkedObjects())
        {
            GameObject obj = pair.Value;
            if (obj == null)
                continue;
            NetworkObjectServerInfo info = obj.GetComponent<NetworkObjectServerInfo>();

            NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();

            NetworkSpawnRPC spawnRPC = new NetworkSpawnRPC(info.m_PrefabIndex, false, identity.m_NetworkId);
            SendRPC(spawnRPC, player); // Spawn it

            if (!info.HasAuthority(player) && !IsPlayerServer(player))
            {
                NetworkAuthorizationRPC authRPC = new NetworkAuthorizationRPC(
                    true,
                    false,
                    false,
                    identity.m_NetworkId
                );
                SendRPC(authRPC, player);
            }

            foreach (var type in info.m_RPCTypes) // Update any extra RPC's the object may have
            {
                NetworkRPC rpc = info.GetRPC(type);
                if (rpc != null)
                    SendRPC(rpc, player);
            }
        }
    }

    /// <summary>
    /// Triggers an RPC command on the server
    /// </summary>
    /// <param name="command">The raw json command</param>
    private void OnRPCCommand(string command, NetworkPlayer player)
    {
        NetworkRPC rpc = NetworkRPC.FromString(command);

        switch (rpc.m_Type)
        {
            case NetworkRPCType.RPC_LIB_SPAWN:
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
                    spawnRPC.m_NetworkId = id;
                    SendRPC(spawnRPC, player);

                    if (spawnRPC.m_PrefabIndex == -1)
                    {
                        player.m_NetworkId = id;
                        Debug.Log("Setting Network ID: " + player.m_NetworkId);
                    }

                    networkBehaviour.m_Spawner = player.m_NetworkId;

                    if (spawnRPC.m_RequestAuthority)
                        serverInfo.AddAuthority(player);

                    if (IsPlayerServer(player))
                    {
                        // Sending to ourselves

                        NetworkAuthorizationRPC authRPC = new NetworkAuthorizationRPC(
                            true,
                            (NetworkManager.Instance.m_NetworkType == NetworkManager.ENetworkType.Mixed) ? true : false, // We might be server because we can mixed host
                            spawnRPC.m_RequestAuthority,
                            id
                        );
                        SendRPC(authRPC, player);
                    } else
                    {
                        // Sending to another client

                        NetworkAuthorizationRPC authRPC = new NetworkAuthorizationRPC(
                            true,
                            false, // Other clients are definitely not server
                            spawnRPC.m_RequestAuthority,
                            id
                        );
                        SendRPC(authRPC, player);
                    }
                }
                break;
            case NetworkRPCType.RPC_LIB_DESTROY:
                {
                    GameObject obj = NetworkManager.Instance.GetNetworkedObject(rpc.m_NetworkId);
                    if (PlayerHasAuthority(obj, player))
                    {
                        Destroy(obj);
                        SendRPCAll(rpc);
                    }
                } break;
            case NetworkRPCType.RPC_CUSTOM_TRANSFORM:
                {
                    NetworkTransformRPC transformRPC = NetworkRPC.Parse<NetworkTransformRPC>(command);

                    GameObject obj = NetworkManager.Instance.GetNetworkedObject(transformRPC.m_NetworkId);
                    if(PlayerHasAuthority(obj, player))
                    {
                        obj.transform.position = transformRPC.m_Position;
                        obj.transform.eulerAngles = transformRPC.m_Rotation;
                        obj.transform.localScale = transformRPC.m_Scale;

                        UpdateRPC(obj, transformRPC);
                        SendRPCAll(transformRPC); // Probably should add a exclude to this so we dont send it to ourselves? Idk
                    }
                } break;
            case NetworkRPCType.RPC_CUSTOM_PLAYER:
                {
                    NetworkPlayerRPC playerRPC = NetworkRPC.Parse<NetworkPlayerRPC>(command);

                    GameObject obj = NetworkManager.Instance.GetNetworkedObject(playerRPC.m_NetworkId);
                    if (PlayerHasAuthority(obj, player))
                    {
                        if(playerRPC.m_Health != -1)
                        {
                            Debug.Log($"[NetworkServer] {playerRPC.m_NetworkId} is trying to edit their health to {playerRPC.m_Health}!");
                            break;
                        }

                        obj.GetComponent<Player>().UpdateColor(playerRPC.m_Color);
                        UpdateRPC(obj, playerRPC);
                        SendRPCAll(playerRPC); // Probably should add a exclude to this so we dont send it to ourselves? Idk
                    }
                } break;
        }
    }

    private void OnApplicationQuit()
    {
        m_Client.Close();
        m_Client.Dispose();
    }

    public void DestroyObject(int id)
    {
        NetworkDestroyRPC rpc = new NetworkDestroyRPC(id);
        OnRPCCommand(rpc.ToString(), null);
    }

    private void UpdateRPC(GameObject obj, NetworkRPC rpc)
    {
        NetworkObjectServerInfo info = obj.GetComponent<NetworkObjectServerInfo>();
        if (info != null)
        {
            info.UpdateRPC(rpc);
        }
    }

    private bool PlayerHasAuthority(GameObject obj, NetworkPlayer player)
    {
        if (obj != null)
        {
            NetworkObjectServerInfo info = obj.GetComponent<NetworkObjectServerInfo>();
            if (info != null)
            {
                return info.HasAuthority(player);
            }
        }

        return false;
    }

    public void SendRPCAll(NetworkRPC rpc)
    {
        foreach(var pair in NetworkManager.Instance.GetPlayers())
        {
            NetworkPlayer player = pair.Value;
            SendRPC(rpc, player);
        }
    }

    /// <summary>
    /// Constructs a NetworkFrame and sends it from a NetworkRPC
    /// </summary>
    /// <param name="rpc">The RPC being sent over the network</param>
    private void SendRPC(NetworkRPC rpc, NetworkPlayer player)
    {
        NetworkRPCFrame frame = new NetworkRPCFrame(rpc.ToString(), player.m_Id);
        frame.ConfigureForServer(player);

        SendFrame(frame, player);
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

    /// <summary>
    /// Sends a frame to the network
    /// </summary>
    /// <param name="frame">The frame being sent</param>
    /// <param name="endpoint">The endpoint of the user who is receiving this frame</param>
    private void SendFrame(NetworkFrame frame, NetworkPlayer player)
    {
        byte[] bytes = frame.ToBytes();
        m_Client.Send(bytes, bytes.Length, m_IPMap[player.m_Id]);
    }
}