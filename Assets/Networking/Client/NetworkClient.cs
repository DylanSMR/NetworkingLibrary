using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug; // Hmmm

public class NetworkClient : NetworkConnection
{
    // Networking Fields
    public static NetworkClient Instance;

    public string m_DefaultName = "Default";
    public int randomNumber = 0;

    // Ping Fields
    public float m_LastPing { get; private set; } = -1;
    private Stopwatch m_Stopwatch; // Maybe there is a more useful way? Time.time possibly

    private void Awake()
    {
        if(Instance != null)
        {
            Debug.LogWarning("[Client] A new network client was created, yet one already exists.");
            return; // We want to use the already existing network client
        }
        Instance = this;
        randomNumber = UnityEngine.Random.Range(0, 100);
    }
    
    /// <summary>
    /// Returns a unique identifier only used to identify network packets
    /// TODO: Make this unique as in only one client can have it
    /// </summary>
    /// <returns>The unqiue identifier for this user</returns>
    public string GetUniqueIndentifier()
        => SystemInfo.deviceUniqueIdentifier + randomNumber.ToString();

    private void OnApplicationQuit()
        => Disconnect("Quitting");

    public void Disconnect(string reason)
    {
        NetworkPlayerDisconnctRPC disconnect = new NetworkPlayerDisconnctRPC(
            NetworkManager.Instance.GetPlayer(GetUniqueIndentifier()), NetworkDisconnectType.Request, reason, -1
        );
        SendRPC(disconnect);
        Shutdown(NetworkShutdownType.Gracefully, reason);
    }

    public int m_ServerHeartbeats = 0;

    private IEnumerator HeartbeatWorker()
    {
        while (GetNetworkState() == NetworkState.Established)
        {
            if (m_ServerHeartbeats == 2)
            {
                Disconnect("Lost connection to host");
                continue;
            }

            m_ServerHeartbeats++;
            NetworkFrame heartFrame = new NetworkFrame(NetworkFrame.NetworkFrameType.Heartbeat, GetUniqueIndentifier(), "server");
            QueueFrame(heartFrame);

            yield return new WaitForSeconds(2.5f);
        }
    }

    public void Clean()
    {
        if (NetworkManager.Instance.IsMixed())
            return;

        ELogger.Log("Cleaning up client", ELogger.LogType.Client);
        if(NetworkManager.Instance.IsClient())
            NetworkManager.Instance.Clean();

        Destroy(this);
    }

    /// <summary>
    /// Used to send the initial handshake to the server
    /// </summary>
    private void SendHandshake()
    {
        NetworkHandshakeFrame handshake = new NetworkHandshakeFrame(m_DefaultName);
        QueueFrame(handshake);
    }

    /// <summary>
    /// Sends a ping request to the server to calculate our ping
    /// </summary>
    private void SendPing()
    {
        NetworkFrame pingFrame = new NetworkFrame(NetworkFrame.NetworkFrameType.Ping, GetUniqueIndentifier(), "server");
        if (GetNetworkState() == NetworkState.Established)
        {
            m_Stopwatch = new Stopwatch();
            m_Stopwatch.Start();
        }
        QueueFrame(pingFrame);
    }

    public override void OnReceiveData(NetworkFrame frame, IPEndPoint point, byte[] bytes)
    {
        base.OnReceiveData(frame, point, bytes);

        switch (frame.m_Type)
        {
            case NetworkFrame.NetworkFrameType.Ping: 
                {
                    if(m_Stopwatch != null)
                    {
                        m_Stopwatch.Stop();
                        m_LastPing = m_Stopwatch.ElapsedMilliseconds;
                        m_Stopwatch = null;
                    }
                } break;
            case NetworkFrame.NetworkFrameType.Authentication:
                {
                    NetworkAuthenticationFrame authenticationFrame = NetworkFrame.Parse<NetworkAuthenticationFrame>(bytes);
                    if(authenticationFrame.m_Response == NetworkAuthenticationFrame.NetworkAuthenticationResponse.Connected)
                    {
                        SendHandshake();
                        StartCoroutine(PingChecker());
                        StartCoroutine(HeartbeatWorker());
                    } else
                    {
                        if(authenticationFrame.m_Response == NetworkAuthenticationFrame.NetworkAuthenticationResponse.Banned)
                        {
                            Debug.LogWarning($"[Client] Failed to connect to server. We have been banned from that server: {authenticationFrame.m_Message}!");
                        } else if (authenticationFrame.m_Response == NetworkAuthenticationFrame.NetworkAuthenticationResponse.LobbyFull)
                        {
                            Debug.LogWarning($"[Client] Failed to connect to server. That server is full!");
                        } else if (authenticationFrame.m_Response == NetworkAuthenticationFrame.NetworkAuthenticationResponse.IncorrectPassword)
                        {
                            Debug.LogWarning($"[Client] Failed to connect to server. The password was incorrect!");
                        } else if (authenticationFrame.m_Response == NetworkAuthenticationFrame.NetworkAuthenticationResponse.Error)
                        {
                            Debug.LogWarning($"[Client] Failed to connect to server. We have received an error: {authenticationFrame.m_Message}");
                        } else
                        {
                            Debug.LogWarning($"[Client] Failed to connect to server. Unknown reason: {authenticationFrame.m_Message}");
                        }
                        Clean();
                    }
                } break;
            case NetworkFrame.NetworkFrameType.RPC:
                {
                    NetworkRPCFrame rpcFrame = NetworkFrame.Parse<NetworkRPCFrame>(bytes);
                    OnRPCCommand(rpcFrame.m_RPC);
                } break;
            case NetworkFrame.NetworkFrameType.Heartbeat:
                {
                    m_ServerHeartbeats = 0;
                } break;
        }
    }

    private IEnumerator PingChecker()
    {
        while(true)
        {
            SendPing();
            yield return new WaitForSeconds(1f);
        }
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
            case NetworkRPCType.RPC_LIB_SPAWN: 
                {
                    if (NetworkManager.Instance.IsMixed())
                        break; // We already have it spawned for us

                    NetworkSpawnRPC spawnRPC = NetworkRPC.Parse<NetworkSpawnRPC>(content);
                    if(NetworkManager.Instance.GetNetworkedObject(spawnRPC.m_NetworkId) != null)
                        break;

                    GameObject spawnPrefab = NetworkManager.Instance.GetObjectByIndex(spawnRPC.m_PrefabIndex);
                    if (spawnPrefab == null)
                        break;

                    GameObject prefab = Instantiate(spawnPrefab);
                    NetworkBehaviour networkBehaviour = prefab.GetComponent<NetworkBehaviour>();

                    networkBehaviour.m_IsServer = false;
                    networkBehaviour.m_HasAuthority = false;
                    networkBehaviour.m_IsClient = true;
                    prefab.GetComponent<NetworkIdentity>().m_NetworkId = spawnRPC.m_NetworkId;
                    NetworkManager.Instance.AddObject(spawnRPC.m_NetworkId, prefab);
                } break;
            case NetworkRPCType.RPC_LIB_OBJECT_NETAUTH:
                {
                    NetworkAuthorizationRPC authorizationRPC = NetworkRPC.Parse<NetworkAuthorizationRPC>(content);
                    GameObject gameObject = NetworkManager.Instance.GetNetworkedObject(authorizationRPC.m_NetworkId);
                    if(gameObject != null)
                    {
                        NetworkBehaviour networkBehaviour = gameObject.GetComponent<NetworkBehaviour>();
                        networkBehaviour.m_IsClient = authorizationRPC.m_LocalSet;
                        networkBehaviour.m_HasAuthority = authorizationRPC.m_LocalAuthSet;
                        networkBehaviour.m_IsServer = authorizationRPC.m_ServerSet;
                        gameObject.GetComponent<NetworkIdentity>().m_NetworkId = authorizationRPC.m_NetworkId;
                        networkBehaviour.OnAuthorityChanged(networkBehaviour.m_HasAuthority);
                    }
                } break;
            case NetworkRPCType.RPC_LIB_DESTROY:
                {
                    GameObject gameObject = NetworkManager.Instance.GetNetworkedObject(rpc.m_NetworkId);
                    if (gameObject != null)
                        Destroy(gameObject);
                } break;
            case NetworkRPCType.RPC_LIB_TRANSFORM:
                {
                    if (NetworkManager.Instance.IsMixed())
                        break; // We are sort of server, so we have most up to date value

                    NetworkTransformRPC transformRPC = NetworkRPC.Parse<NetworkTransformRPC>(content);
                    GameObject obj = NetworkManager.Instance.GetNetworkedObject(rpc.m_NetworkId);
                    if (obj != null)
                    {
                        obj.transform.position = transformRPC.m_Position;
                        obj.transform.eulerAngles = transformRPC.m_Rotation;
                        obj.transform.localScale = transformRPC.m_Scale;
                    }
                } break;
            case NetworkRPCType.RPC_LIB_CONNECTED:
                {
                    NetworkPlayerConnectRPC connectRPC = NetworkRPC.Parse<NetworkPlayerConnectRPC>(content);
                    ELogger.Log($"Player Connected: {connectRPC.m_Player.m_Name}", ELogger.LogType.Client);
                    NetworkManager.Instance.AddPlayer(connectRPC.m_Player.m_Id, connectRPC.m_Player);

                    foreach(var networkPair in NetworkManager.Instance.GetNetworkedObjects())
                    {
                        if(networkPair.Value != null)
                        {
                            NetworkBehaviour networkBehaviour = networkPair.Value.GetComponent<NetworkBehaviour>();
                            if (networkBehaviour != null)
                                networkBehaviour.OnPlayerJoined(connectRPC.m_Player);
                        }
                    }
                } break;
            case NetworkRPCType.RPC_LIB_DISCONNECTED:
                {
                    NetworkPlayerDisconnctRPC disconnectRPC = NetworkRPC.Parse<NetworkPlayerDisconnctRPC>(content);
                    NetworkManager.Instance.RemovePlayer(disconnectRPC.m_Player.m_Id);

                    foreach (var networkPair in NetworkManager.Instance.GetNetworkedObjects())
                    {
                        if (networkPair.Value != null)
                        {
                            NetworkBehaviour networkBehaviour = networkPair.Value.GetComponent<NetworkBehaviour>();
                            if (networkBehaviour != null)
                                networkBehaviour.OnPlayerDisconnected(disconnectRPC.m_Player, disconnectRPC.m_DisconnectType, disconnectRPC.m_Reason);
                        }
                    }

                    if (disconnectRPC.m_Player.m_Id == GetUniqueIndentifier())
                    {
                        ELogger.Log($"We were disconnected for reason '{disconnectRPC.m_Reason}' with type {disconnectRPC.m_Type}|{disconnectRPC.m_DisconnectType}", ELogger.LogType.Client);
                        Clean();
                        UnityEditor.EditorApplication.isPlaying = false;
                    }
                    else
                    {
                        ELogger.Log($"Player Disonnected: {disconnectRPC.m_Player.m_Name}", ELogger.LogType.Client);
                    }
                } break;
            default:
                {
                    GameObject obj = NetworkManager.Instance.GetNetworkedObject(rpc.m_NetworkId);
                    if(obj != null)
                    {
                        NetworkBehaviour behaviour = obj.GetComponent<NetworkBehaviour>();
                        if(behaviour != null)
                        {
                            behaviour.OnRPCCommand(content);
                        }
                    }
                } break;
        }
    }

    /// <summary>
    /// Sends a RPC to the game server
    /// </summary>
    /// <param name="rpc">The RPC being sent</param>
    public void SendRPC(NetworkRPC rpc)
    {
        NetworkRPCFrame networkRPCFrame = new NetworkRPCFrame(rpc.ToString(), GetUniqueIndentifier(), "server")
        {
            m_Important = rpc.m_Important
        };
        QueueFrame(networkRPCFrame);
    }
}
