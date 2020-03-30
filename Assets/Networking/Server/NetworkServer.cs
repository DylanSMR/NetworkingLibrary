using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static NetworkServerSettings;

public class NetworkServer : MonoBehaviour
{
    // Networking Fields
    private UdpClient m_Client;
    public static NetworkServer Instance;
    private int m_FrameCount = 0;

    // Server Fields
    private string m_Password;
    public NetworkServerStatus m_Status = NetworkServerStatus.Connecting;
    /// <summary>
    /// A list of users who are authorized and can use the authorized frames
    /// </summary>
    private List<string> m_AuthorizedUsers;

    public Dictionary<string, IPEndPoint> m_IPMap;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning("[Server] A new network server was created, yet one already exists.");
            return; // We want to use the already existing network server
        }
        Instance = this;

        m_AuthorizedUsers = new List<string>();
        m_IPMap = new Dictionary<string, IPEndPoint>();
        m_Heartbeats = new Dictionary<string, int>();
        m_ImportantFrames = new Dictionary<ImportantFrameHolder, float>();
    }

    public Dictionary<string, int> m_Heartbeats;

    private IEnumerator HeartbeatWorker()
    {
        while(m_Status == NetworkServerStatus.Connected)
        {
            foreach(var player in NetworkManager.Instance.GetPlayers())
            {
                if(!m_Heartbeats.ContainsKey(player.m_Id))
                {
                    m_Heartbeats.Add(player.m_Id, 0);
                    continue;
                }
                if(m_Heartbeats[player.m_Id] + 1 == 3)
                {
                    ELogger.Log($"Player {player.m_Name} lost connection to server", ELogger.LogType.Server);
                    KickPlayer(player, "Lost Connection", true);
                    continue;
                }

                m_Heartbeats[player.m_Id] += 1;

                NetworkFrame heartFrame = new NetworkFrame(NetworkFrame.NetworkFrameType.Heartbeat, "server");
                SendFrame(heartFrame, player);
            }

            yield return new WaitForSeconds(1);
        }
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
        if (NetworkManager.Instance.m_Settings.m_ConnectionType == ENetworkConnectionType.Proxy)
        {
            m_Client = new UdpClient();
            m_Client.Connect(address, port);
            ELogger.Log($"Connecting to proxy at {address}:{port}", ELogger.LogType.Server);
            StartCoroutine(ProxyConnectWorker(address, port));
        }
        else
        {
            m_Client = new UdpClient(port); // Host server
            OnServerStarted(address, port);
        }
    }

    private IEnumerator ProxyConnectWorker(string address, int port)
    {
        OnServerStarted(address, port);
        return null;
    }

    private Dictionary<ImportantFrameHolder, float> m_ImportantFrames;
    private class ImportantFrameHolder
    {
        public NetworkFrame m_Frame;
        public NetworkPlayer m_Player;
        public int m_Tries = 0;
    }

    private IEnumerator ImportantFrameWorker()
    {
        while (m_Status == NetworkServerStatus.Connected)
        {
            if (m_ImportantFrames.Count > 0)
            {
                Dictionary<ImportantFrameHolder, float> newPairs = new Dictionary<ImportantFrameHolder, float>();
                foreach (var importantPair in m_ImportantFrames.ToDictionary(entry => entry.Key, entry => entry.Value))
                {
                    NetworkFrame frame = importantPair.Key.m_Frame;
                    NetworkPlayer player = importantPair.Key.m_Player;
                    if(importantPair.Key.m_Tries > 4) // They have had 4 chances to ack this frame, lets just kick them and assume they cant hear us
                    {
                        KickPlayer(player, "Lost Connection", true);
                        continue;
                    }
                    if (importantPair.Value + 2.0f > Time.time)
                    {
                        // This frame has expired, we assume its invalid and send it agai
                        importantPair.Key.m_Tries++;
                        newPairs.Add(importantPair.Key, Time.time + 2f);
                        SendFrame(frame, player);
                        continue;
                    }
                }
                m_ImportantFrames = newPairs;
            }

            yield return new WaitForSeconds(1);
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
        NetworkPlayer player = NetworkManager.Instance.GetPlayer(frame.m_SenderId);
        if(frame.m_Important && player != null)
        {
            NetworkFrame importantFrame = new NetworkFrame(NetworkFrame.NetworkFrameType.Acknowledged, "server");
            importantFrame.m_FrameId = frame.m_FrameId;
            SendFrame(importantFrame, player);
        }

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
                    OnRPCCommand(player, spawnRpc.ToString());
                    UpdatePlayer(player);
                } break;
            case NetworkFrame.NetworkFrameType.Authentication: // TODO: Clean this? Its terrible lol
                {
                    NetworkAuthenticationFrame authenticationFrame = NetworkFrame.Parse<NetworkAuthenticationFrame>(result.Buffer);
                    NetworkPlayer tempPlayer = new NetworkPlayer()
                    {
                        m_Id = frame.m_SenderId
                    };
                    if(!m_IPMap.ContainsKey(tempPlayer.m_Id))
                        m_IPMap.Add(frame.m_SenderId, result.RemoteEndPoint);

                    if (authenticationFrame.m_Password != m_Password && m_Password != "")
                    {
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.IncorrectPassword;
                        SendFrame(authenticationFrame, tempPlayer);
                    } else if (NetworkManager.Instance.m_Settings.m_ConnectionType == ENetworkConnectionType.Proxy && !m_Client.Client.Connected)
                    {
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.Error;
                        authenticationFrame.m_Message = "server_error_proxy";
                        SendFrame(authenticationFrame, tempPlayer);
                    }
                    else if (IsBanned(frame.m_SenderId).Item1) // Store this in a variable or cache later?
                    {
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.Banned;
                        authenticationFrame.m_Message = IsBanned(frame.m_SenderId).Item2;
                        SendFrame(authenticationFrame, tempPlayer);
                    }
                    else if (NetworkManager.Instance.GetPlayerCount() == NetworkManager.Instance.m_Settings.m_MaxPlayers &&
                        NetworkManager.Instance.m_Settings.m_MaxPlayers > 0)
                    {
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.LobbyFull;
                        SendFrame(authenticationFrame, tempPlayer);
                    }
                    else 
                    {
                        m_AuthorizedUsers.Add(frame.m_SenderId);

                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.Connected;
                        SendFrame(authenticationFrame, tempPlayer);
                    }
                } break;
            case NetworkFrame.NetworkFrameType.Ping:
                {
                    if (!IsUserAuthorized(frame.m_SenderId)) // Must be authorized to use this frame
                        break;

                    NetworkFrame pingFrame = new NetworkFrame(NetworkFrame.NetworkFrameType.Ping, frame.m_SenderId, "server");
                    SendFrame(pingFrame, player);
                } break;
            case NetworkFrame.NetworkFrameType.RPC:
                {
                    if (!IsUserAuthorized(frame.m_SenderId)) // Must be authorized to use this frame
                        break;

                    NetworkRPCFrame rpcFrame = NetworkFrame.Parse<NetworkRPCFrame>(result.Buffer);
                    OnRPCCommand(player,rpcFrame.m_RPC);
                } break;
            case NetworkFrame.NetworkFrameType.Heartbeat:
                {
                    if(m_Heartbeats.ContainsKey(frame.m_SenderId))
                        m_Heartbeats[frame.m_SenderId]++;
                    else
                        m_Heartbeats.Add(frame.m_SenderId, 0);
                } break;
            case NetworkFrame.NetworkFrameType.Acknowledged:
                {
                    Dictionary<ImportantFrameHolder, float> importanteFrames = new Dictionary<ImportantFrameHolder, float>();
                    foreach(var importanteFrame in m_ImportantFrames)
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
    public virtual void OnRPCCommand(NetworkPlayer player, string command)
    {
        NetworkRPC rpc = NetworkRPC.FromString(command);

        switch (rpc.m_Type)
        {
            case NetworkRPCType.RPC_LIB_SPAWN: // TODO: Clean this up if at all possible?
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
                        NetworkPlayerConnectRPC connectRPC = new NetworkPlayerConnectRPC(player, -1);
                        SendRPCAll(connectRPC);
                    }

                    networkBehaviour.m_Spawner = player.m_NetworkId;

                    if (spawnRPC.m_RequestAuthority)
                        serverInfo.AddAuthority(player);

                    if (IsPlayerServer(player))
                    {
                        // Sending to ourselves

                        NetworkAuthorizationRPC authRPC = new NetworkAuthorizationRPC(
                            true,
                            (NetworkManager.Instance.m_Settings.m_NetworkType == ENetworkType.Mixed) ? true : false, // We might be server because we can mixed host
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
            case NetworkRPCType.RPC_LIB_TRANSFORM:
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
            case NetworkRPCType.RPC_LIB_DISCONNECTED:
                {

                } break;
        }
    }

    public void KickPlayer(NetworkPlayer player, string reason, bool deleteObjects = false)
    {
        if(player == null)
            return;

        GameObject obj = NetworkManager.Instance.GetNetworkedObject(player.m_NetworkId);
        if(obj == null)
            return;

        NetworkBehaviour networkBehaviour = obj.GetComponent<NetworkBehaviour>();
        if(networkBehaviour == null)
            return;

        NetworkPlayerDisconnctRPC disconnectRPC = new NetworkPlayerDisconnctRPC(player, NetworkDisconnectType.Kick, reason, player.m_NetworkId);
        SendRPC(disconnectRPC, player);

        if(deleteObjects)
        {
            List<GameObject> objects = player.GetOwnedObjects();
            foreach(var networkObject in objects)
            {
                DestroyNetworkedObject(networkObject.GetComponent<NetworkIdentity>().m_NetworkId);
            }
        }

        OnPlayerDisconnected(player, NetworkDisconnectType.Kick, reason);
        NetworkManager.Instance.RemovePlayer(player.m_Id);
    }

    private void OnApplicationQuit()
    {
        OnServerStopped(NetworkServerStopType.Gracefully, "Application Quit");
    }

    public void DestroyNetworkedObject(int id)
    {
        NetworkDestroyRPC rpc = new NetworkDestroyRPC(id);
        OnRPCCommand(null, rpc.ToString());
    }

    public void UpdateRPC(GameObject obj, NetworkRPC rpc)
    {
        NetworkObjectServerInfo info = obj.GetComponent<NetworkObjectServerInfo>();
        if (info != null)
        {
            info.UpdateRPC(rpc);
        }
    }

    public bool PlayerHasAuthority(GameObject obj, NetworkPlayer player)
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
        => NetworkManager.Instance.GetPlayers().ForEach(player => SendRPC(rpc, player));

    /// <summary>
    /// Constructs a NetworkFrame and sends it from a NetworkRPC
    /// </summary>
    /// <param name="rpc">The RPC being sent over the network</param>
    private void SendRPC(NetworkRPC rpc, NetworkPlayer player)
    {
        NetworkRPCFrame frame = new NetworkRPCFrame(rpc.ToString(), player.m_Id);
        frame.m_Important = rpc.m_Important;

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
        frame.m_FrameId = m_FrameCount + 1;
        if (frame.m_Important)
        {
            m_ImportantFrames.Add(new ImportantFrameHolder()
            {
                m_Frame = frame,
                m_Player = player
            }, Time.time);
        }

        m_FrameCount++;
        byte[] bytes = frame.ToBytes();
        m_Client.Send(bytes, bytes.Length, m_IPMap[player.m_Id]);
    }

    public enum NetworkServerStatus
    {
        Connected,
        Connecting,
        Stopped,
        Error
    }

    public enum NetworkServerStopType
    {
        Gracefully,
        Errored,
        Unknown
    }

    public virtual void OnServerStarted(string address, int port)
    {
        ELogger.Log($"Server started on: {address}:{port}", ELogger.LogType.Server);
        m_Status = NetworkServerStatus.Connected;
        StartCoroutine(ImportantFrameWorker());
        StartCoroutine(HeartbeatWorker());
        _ = OnReceiveFrame();
    }

    public virtual void OnServerStopped(NetworkServerStopType type, string reason)
    {
        m_Status = NetworkServerStatus.Stopped;
        ELogger.Log($"Server stopped for reason '{reason}' with type {type}", ELogger.LogType.Server);

        foreach(NetworkPlayer player in NetworkManager.Instance.GetPlayers())
        {
            KickPlayer(player, "Server Stopping", true);
        }

        if(m_Client != null)
        {
            m_Client.Close();
            m_Client.Dispose();
        }
    }
    
    public virtual void OnServerError(string error)
    {
        m_Status = NetworkServerStatus.Error;
        ELogger.Log($"Server errored with error '{error}'", ELogger.LogType.Server);
        OnServerStopped(NetworkServerStopType.Errored, error);
    }

    public virtual void OnPlayerConnected(NetworkPlayer player)
    {
        ELogger.Log($"A player has connected to the server: {player.m_Name}|{player.m_Id}", ELogger.LogType.Server);
    }

    public virtual void OnPlayerDisconnected(NetworkPlayer player, NetworkDisconnectType type, string reason)
    {
        ELogger.Log($"A player has disconnected from the server: {player.m_Name}|{type}|{reason}", ELogger.LogType.Server);
    }
}