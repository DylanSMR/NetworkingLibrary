using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using static NetworkServerSettings;
using Debug = UnityEngine.Debug; // Hmmm

/// <summary>
/// A class that handles all networking between the server and the us (The Client)
/// </summary>
public class NetworkClient : MonoBehaviour
{
    // Networking Fields
    private UdpClient m_Client;
    public static NetworkClient Instance;
    private int m_FrameCount;

    private Coroutine m_Coroutine_CTS;
    private Coroutine m_Coroutine_HB;
    private Coroutine m_Coroutine_PING;
    private Coroutine m_Coroutine_IMPT;

    private string m_DefaultName = "Default";

    /// <summary>
    /// A integer that represents how many times we have tried to connect to the server/proxy
    /// </summary>
    public int m_ConnectionTries { get; private set; } = 0;
    /// <summary>
    /// The status of the network client
    /// </summary>
    public NetworkClientStatus m_Status = NetworkClientStatus.Disconnected;

    private int randomNumber = 0;

    private Dictionary<ImportantFrameHolder, float> m_ImportantFrames;

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
        m_ImportantFrames = new Dictionary<ImportantFrameHolder, float>();
        randomNumber = UnityEngine.Random.Range(0, 100);
    }
    
    /// <summary>
    /// Returns a unique identifier only used to identify network packets
    /// TODO: Make this unique as in only one client can have it
    /// </summary>
    /// <returns>The unqiue identifier for this user</returns>
    public string GetUniqueIndentifier()
    {
        return SystemInfo.deviceUniqueIdentifier + randomNumber.ToString();
    }

    /// <summary>
    /// Connects to a game server
    /// </summary>
    /// <param name="address">The address of the game server/proxy</param>
    /// <param name="port">The port of the game server/proxy</param>
    /// <param name="password">The password used to connect to the server</param>
    public void Connect(string address, int port, string password = "")
    {
        m_Status = NetworkClientStatus.Connecting;
        m_Coroutine_CTS = StartCoroutine(ConnectToServer(address, port, password));
    }

    private void OnApplicationQuit()
    {
        m_Client.Close();
        m_Client.Dispose();
    }

    public int m_ServerHeartbeats = 0;

    private IEnumerator HeartbeatWorker()
    {
        while (m_Status == NetworkClientStatus.Connected)
        {
            foreach (var player in NetworkManager.Instance.GetPlayers())
            {
                if (m_ServerHeartbeats + 1 == 3)
                {
                    ELogger.Log($"Lost connection to server", ELogger.LogType.Server);
                    Clean();
                    continue;
                }

                m_ServerHeartbeats++;
                NetworkFrame heartFrame = new NetworkFrame(NetworkFrame.NetworkFrameType.Heartbeat, GetUniqueIndentifier());
                SendFrame(heartFrame);
            }

            yield return new WaitForSeconds(1);
        }
    }

    public void Clean()
    {
        if (NetworkManager.Instance.IsMixed())
            return;

        ELogger.Log("Cleaning up client", ELogger.LogType.Client);

        NetworkManager.Instance.Clean();
        if (m_Coroutine_CTS != null)
            StopCoroutine(m_Coroutine_CTS);
        if (m_Coroutine_HB != null)
            StopCoroutine(m_Coroutine_HB);
        if (m_Coroutine_PING != null)
            StopCoroutine(m_Coroutine_PING);
        if (m_Coroutine_IMPT != null)
            StopCoroutine(m_Coroutine_IMPT);

        if(m_Client != null)
        {
            m_Client.Close();
            m_Client.Dispose();
        }

        Destroy(this);
    }

    /// <summary>
    /// Runs a loop to ensure connection to the game server. 
    /// After a few tries it will eventually fail and log an error
    /// TODO: Call a function or something if it fails to connect
    /// </summary>
    /// <param name="address">The address of the game server/proxy</param>
    /// <param name="port">The port of the game server/proxy</param>
    /// <param name="password">The password of the game server/proxy</param>
    private IEnumerator ConnectToServer(string address, int port, string password) // Rework this entire function
    {
        for(; ;)
        {
            if(m_ConnectionTries == 5)
            {
                Debug.LogError("[Client] Failed to connect to the server, ran out of tries.");
                m_Status = NetworkClientStatus.Error;
                break;
            }

            if(m_Client != null && m_Client.Client != null && m_Client.Client.Connected && m_Status != NetworkClientStatus.Connected)
            {
                NetworkAuthenticationFrame authFrame = new NetworkAuthenticationFrame(password);
                SendFrame(authFrame); // If we get a auth frame back, we are good! Unless we got the password wrong :/
                yield return new WaitForSeconds(2f);
            }
            // Keep trying to connect until we either give up or eventually connect
            if(m_Status == NetworkClientStatus.Connected)
                break;
            if (m_Status == NetworkClientStatus.Error || m_Status == NetworkClientStatus.Unknown)
                break;

            m_ConnectionTries++;
            ELogger.Log($"Attempting to connect to server [{address}:{port}][{m_ConnectionTries}/5]", ELogger.LogType.Client);
            if (m_Client != null)
            {
                m_Client.Close();
                m_Client.Dispose();
            }

            m_Client = new UdpClient();
            m_Client.Connect(address, port); // Try and connect to the server
            _ = OnReceiveFrame();
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
        NetworkHandshakeFrame handshake = new NetworkHandshakeFrame(m_DefaultName);
        SendFrame(handshake);
    }

    private class ImportantFrameHolder
    {
        public NetworkFrame m_Frame;
        public int m_Tries = 0;
    }
    private IEnumerator ImportantFrameWorker()
    {
        while(m_Status == NetworkClientStatus.Connected)
        {
            if (m_ImportantFrames.Count > 0)
            {
                Dictionary<ImportantFrameHolder, float> newPairs = new Dictionary<ImportantFrameHolder, float>();
                foreach (var importantPair in m_ImportantFrames.ToDictionary(entry => entry.Key, entry => entry.Value))
                {
                    NetworkFrame frame = importantPair.Key.m_Frame;
                    if (importantPair.Key.m_Tries > 4) 
                    {
                        // Cry? Idk what to do if server doesnt ack our frame, maybe just disconnected and cry
                        continue;
                    }
                    if (importantPair.Value + 2.0f > Time.time)
                    {
                        // This frame has expired, we assume its invalid and send it again
                        importantPair.Key.m_Tries++;
                        newPairs.Add(importantPair.Key, Time.time + 2f);
                        SendFrame(frame);
                        continue;
                    }
                }
                m_ImportantFrames = newPairs;
            }

            yield return new WaitForSeconds(1);

            if (m_ImportantFrames.Count > 0)
            {
                Dictionary<ImportantFrameHolder, float> newPairs = new Dictionary<ImportantFrameHolder, float>();
                foreach (var importantPair in m_ImportantFrames)
                {
                    if(importantPair.Value + 2.0f > Time.time)
                    {
                        // This frame has expired, we assume its invalid and send it again
                        ELogger.Log("The server has missed one of our frames: ", ELogger.LogType.Client);
                        SendFrame(importantPair.Key.m_Frame);
                        continue;
                    }
                    newPairs.Add(importantPair.Key, importantPair.Value);
                }
                m_ImportantFrames = newPairs;
            }

            yield return new WaitForSeconds(1);
        }
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
        if (frame.m_Important)
        {
            NetworkFrame importantFrame = new NetworkFrame(NetworkFrame.NetworkFrameType.Acknowledged, GetUniqueIndentifier());
            importantFrame.m_FrameId = frame.m_FrameId;
            SendFrame(importantFrame);
        }

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
                    NetworkAuthenticationFrame authenticationFrame = NetworkFrame.Parse<NetworkAuthenticationFrame>(result.Buffer);
                    if(authenticationFrame.m_Response == NetworkAuthenticationFrame.NetworkAuthenticationResponse.Connected)
                    {
                        m_Status = NetworkClientStatus.Connected;
                        ELogger.Log("Connected to server.", ELogger.LogType.Client);

                        SendHandshake();
                        m_Coroutine_PING = StartCoroutine(PingChecker());
                        m_Coroutine_IMPT = StartCoroutine(ImportantFrameWorker());
                        m_Coroutine_HB = StartCoroutine(HeartbeatWorker());
                    } else
                    {
                        m_Status = NetworkClientStatus.Error;
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
                    NetworkRPCFrame rpcFrame = NetworkFrame.Parse<NetworkRPCFrame>(result.Buffer);
                    OnRPCCommand(rpcFrame.m_RPC);
                } break;
            case NetworkFrame.NetworkFrameType.Heartbeat:
                {
                    m_ServerHeartbeats = 0;
                } break;
            case NetworkFrame.NetworkFrameType.Acknowledged:
                {
                    Dictionary<ImportantFrameHolder, float> importanteFrames = new Dictionary<ImportantFrameHolder, float>();
                    foreach (var importanteFrame in m_ImportantFrames)
                    {
                        if (importanteFrame.Key.m_Frame.m_FrameId == frame.m_FrameId)
                            continue;

                        importanteFrames.Add(importanteFrame.Key, importanteFrame.Value);
                    }
                    m_ImportantFrames = importanteFrames;
                } break;
        }

        _ = OnReceiveFrame();
    }

    private IEnumerator PingChecker()
    {
        SendPing();
        yield return new WaitForSeconds(2.5f);
        StartCoroutine(PingChecker());
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.P))
        {
            SendPing();
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
                        networkBehaviour.OnAuthorityChanged(authorizationRPC.m_LocalAuthSet);
                        gameObject.GetComponent<NetworkIdentity>().m_NetworkId = authorizationRPC.m_NetworkId;
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
        NetworkRPCFrame networkRPCFrame = new NetworkRPCFrame(rpc.ToString(), GetUniqueIndentifier());
        networkRPCFrame.m_Important = rpc.m_Important;
        SendFrame(networkRPCFrame);
    }

    /// <summary>
    /// Sends a frame to the server
    /// </summary>
    /// <param name="frame">The frame being sent to the server</param>
    private void SendFrame(NetworkFrame frame)
    {
        frame.m_SenderId = GetUniqueIndentifier();
        frame.m_TargetId = "server";
        if(frame.m_FrameId == -1)
            frame.m_FrameId = m_FrameCount + 1;

        if(frame.m_Important)
        {
            m_ImportantFrames.Add(new ImportantFrameHolder()
            {
                m_Frame = frame
            }, Time.time);
        }

        m_FrameCount++;
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
