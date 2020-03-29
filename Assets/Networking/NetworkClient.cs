using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

using Debug = UnityEngine.Debug; // Hmmm

/// <summary>
/// A class that handles all networking between the server and the us (The Client)
/// </summary>
public class NetworkClient : MonoBehaviour
{
    // Networking Fields
    private UdpClient m_Client;
    private string m_Address;
    public static NetworkClient Instance;
    /// <summary>
    /// A integer that represents how many times we have tried to connect to the server/proxy
    /// </summary>
    public int m_ConnectionTries { get; private set; } = 0;
    /// <summary>
    /// The status of the network client
    /// </summary>
    public NetworkClientStatus m_Status = NetworkClientStatus.Disconnected;

    // Ping Fields
    public float m_LastPing { get; private set; } = -1;
    private Stopwatch m_Stopwatch; // Maybe there is a more useful way? Time.time possibly

    private void Awake()
    {
        if(Instance != null)
        {
            Debug.LogWarning("[NetworkClient] A new network client was created, yet one already exists.");
            return; // We want to use the already existing network client
        }
        Instance = this;
    }

    /// <summary>
    /// Returns a unique identifier only used to identify network packets
    /// TODO: Make this unique as in only one client can have it
    /// </summary>
    /// <returns>The unqiue identifier for this user</returns>
    public string GetUniqueIndentifier()
    {
        return SystemInfo.deviceUniqueIdentifier;
    }

    /// <summary>
    /// Connects to a game server
    /// </summary>
    /// <param name="address">The address of the game server/proxy</param>
    /// <param name="port">The port of the game server/proxy</param>
    /// <param name="password">The password used to connect to the server</param>
    public void Connect(string address, int port, string password = "")
    {
        m_Client = new UdpClient();

        m_Status = NetworkClientStatus.Connecting;
        StartCoroutine(ConnectToServer(address, port, password));
        OnReceiveFrame();
    }

    /// <summary>
    /// Runs a loop to ensure connection to the game server. 
    /// After a few tries it will eventually fail and log an error
    /// TODO: Call a function or something if it fails to connect
    /// </summary>
    /// <param name="address">The address of the game server/proxy</param>
    /// <param name="port">The port of the game server/proxy</param>
    /// <param name="password">The password of the game server/proxy</param>
    private IEnumerator ConnectToServer(string address, int port, string password)
    {
        for(; ;)
        {
            if(m_ConnectionTries == 5)
            {
                Debug.LogError("[NetworkClient] Failed to connect to the server, ran out of tries.");
                m_Status = NetworkClientStatus.Error;
                break;
            }

            // Keep trying to connect until we either give up or eventually connect
            if(m_Status == NetworkClientStatus.Connected)
                break;
            if (m_Status == NetworkClientStatus.Error || m_Status == NetworkClientStatus.Unknown)
                break;

            m_ConnectionTries++;
            Debug.Log($"[NetworkClient] Attempting to connect to server [{address}:{port}][{m_ConnectionTries}/5]");
            m_Client.Connect(address, port); // Try and connect to the server
            try
            {
                NetworkAuthenticationFrame authFrame = new NetworkAuthenticationFrame(password);
                SendFrame(authFrame); // If we get a auth frame back, we are good! Unless we got the password wrong :/
            }
            catch (Exception e) {
                Debug.LogError(e);
            }; // Ignore any errors, stupid errors

            yield return new WaitForSeconds(2.5f); // Every 2 and a half seconds we are going to try and connect again! Yay :/
        }
    }

    /// <summary>
    /// Used to send the initial handshake to the server
    /// </summary>
    private void SendHandshake()
    {
        NetworkHandshakeFrame handshake = new NetworkHandshakeFrame("DylanSMR");
        SendFrame(handshake);
    }

    /// <summary>
    /// Sends a ping request to the server to calculate our ping
    /// </summary>
    private void SendPing()
    {
        NetworkFrame pingFrame = new NetworkFrame(NetworkFrame.NetworkFrameType.Ping, GetUniqueIndentifier());
        if (m_Status == NetworkClientStatus.Connected)
        {
            m_Stopwatch = new Stopwatch();
            m_Stopwatch.Start();
        }   
        SendFrame(pingFrame);
    }

    /// <summary>
    /// An async function to receive bytes (frames) from the game server
    /// </summary>
    private async Task OnReceiveFrame()
    {
        UdpReceiveResult result = await m_Client.ReceiveAsync();
        NetworkFrame frame = NetworkFrame.ReadFromBytes(result.Buffer);

        switch (frame.m_Type)
        {
            case NetworkFrame.NetworkFrameType.Handshake:
                {
                    
                } break;
            case NetworkFrame.NetworkFrameType.Ping: 
                {
                    if(m_Stopwatch != null)
                    {
                        m_Stopwatch.Stop();
                        m_LastPing = m_Stopwatch.ElapsedMilliseconds;
                        m_Stopwatch = null;
                        Debug.Log($"[NetworkClient] Calculated Ping | {m_LastPing}ms");
                    }
                } break;
            case NetworkFrame.NetworkFrameType.Authentication:
                {
                    NetworkAuthenticationFrame authenticationFrame = NetworkFrame.Parse<NetworkAuthenticationFrame>(result.Buffer);
                    if(authenticationFrame.m_Response == NetworkAuthenticationFrame.NetworkAuthenticationResponse.Connected)
                    {
                        m_Status = NetworkClientStatus.Connected;
                        m_Address = authenticationFrame.m_TargetAddress;
                        Debug.Log("[NetworkClient] Connected to server.");

                        SendPing();
                        SendHandshake();
                    } else
                    {
                        m_Status = NetworkClientStatus.Error;
                        if(authenticationFrame.m_Response == NetworkAuthenticationFrame.NetworkAuthenticationResponse.Banned)
                        {
                            Debug.LogWarning($"[NetworkClient] Failed to connect to server. We have been banned from that server: {authenticationFrame.m_Message}!");
                        } else if (authenticationFrame.m_Response == NetworkAuthenticationFrame.NetworkAuthenticationResponse.LobbyFull)
                        {
                            Debug.LogWarning($"[NetworkClient] Failed to connect to server. That server is full!");
                        } else if (authenticationFrame.m_Response == NetworkAuthenticationFrame.NetworkAuthenticationResponse.IncorrectPassword)
                        {
                            Debug.LogWarning($"[NetworkClient] Failed to connect to server. The password was incorrect!");
                        } else if (authenticationFrame.m_Response == NetworkAuthenticationFrame.NetworkAuthenticationResponse.Error)
                        {
                            Debug.LogWarning($"[NetworkClient] Failed to connect to server. We have received an error: {authenticationFrame.m_Message}");
                        } else
                        {
                            Debug.LogWarning($"[NetworkClient] Failed to connect to server. Unknown reason: {authenticationFrame.m_Message}");
                        }
                    }
                } break;
            case NetworkFrame.NetworkFrameType.RPC:
                {
                    NetworkRPCFrame rpcFrame = NetworkFrame.Parse<NetworkRPCFrame>(result.Buffer);
                    OnRPCCommand(rpcFrame.m_RPC);
                } break;
        }

        OnReceiveFrame();
    }

    /// <summary>
    /// Send a RPC command to a specified network id
    /// </summary>
    /// <param name="rpc">The RPC being sent throughout the network</param>
    public void OnRPCCommand(string content)
    {
        NetworkRPC rpc = NetworkRPC.FromString(content);
        switch(rpc.m_Type) // If a type is handled here its because it needs to be here, or the networking wont function properly
        {
            case NetworkRPCType.RPC_SPAWN: 
                {
                    NetworkSpawnRPC spawnRPC = NetworkRPC.Parse<NetworkSpawnRPC>(content);

                    GameObject spawnPrefab = NetworkManager.Instance.GetObjectByIndex(spawnRPC.m_PrefabIndex);
                    GameObject prefab = Instantiate(spawnPrefab);
                    NetworkBehaviour networkBehaviour = prefab.GetComponent<NetworkBehaviour>();

                    networkBehaviour.m_IsServer = false;
                    networkBehaviour.m_HasAuthority = false;
                    networkBehaviour.m_IsLocal = true;
                    NetworkManager.Instance.AddObject(spawnRPC.m_NetworkIndex, prefab);
                } break;
            case NetworkRPCType.RPC_OBJECT_AUTHORIZATION:
                {
                    NetworkAuthorizationRPC authorizationRPC = NetworkRPC.Parse<NetworkAuthorizationRPC>(content);
                    GameObject gameObject = NetworkManager.Instance.GetNetworkedObject(authorizationRPC.m_NetworkId);
                    if(gameObject != null)
                    {
                        NetworkBehaviour networkBehaviour = gameObject.GetComponent<NetworkBehaviour>();
                        networkBehaviour.m_IsLocal = authorizationRPC.m_LocalSet;
                        networkBehaviour.m_HasAuthority = authorizationRPC.m_LocalAuthSet;
                        networkBehaviour.m_IsServer = authorizationRPC.m_ServerSet;
                    }
                } break;
            default: // This can be handled on behaviour/object
                {                  
                    // Get object it is targeted towards
                    // Call target.OnRPCCommand(content)
                } break;
        }
    }

    private void SendFrame(NetworkFrame frame)
    {
        //Debug.Log("[NetworkClient] Sending frame: " + JsonUtility.ToJson(frame));

        byte[] bytes = frame.ToBytes();
        m_Client.Send(bytes, bytes.Length);
    }

    /// <summary>
    /// A enum that represents the status of the client
    /// </summary>
    public enum NetworkClientStatus
    {
        Connecting,
        Connected,
        Disconnected,
        Error,
        Unknown
    }
}
