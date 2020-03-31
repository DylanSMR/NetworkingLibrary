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
    public long x = 0;

    // Server Fields
    private string m_Password;
    public NetworkServerStatus m_Status = NetworkServerStatus.Connecting;
    /// <summary>
    /// A list of users who are authorized and can use the authorized frames
    /// </summary>
    private List<string> m_AuthorizedUsers;

    private Coroutine m_Coroutine_IMPF;
    private Coroutine m_Coroutine_HB;
    private Coroutine m_Coroutine_CTP;

    public Dictionary<string, IPEndPoint> m_IPMap;

    private void OnGUI()
    {
        GUI.Label(new Rect(200, 100, 500, 500), $"Read: {x}");
    }

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
                    RemovePlayer(player, "Lost Connection", NetworkDisconnectType.LostConnection);
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
        m_Client = new UdpClient(port); // Host server
        OnServerStarted(address, port);
    }

    public void HostAsProxy(string address, int port, string password)
    {
        m_Password = password;
        m_Client = new UdpClient();
        m_Client.Connect(address, port);
        ELogger.Log($"Connecting to proxy at {address}:{port}", ELogger.LogType.Server);
        m_Coroutine_CTP = StartCoroutine(ProxyConnectWorker(address, port));
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
                        RemovePlayer(player, "Lost Connection", NetworkDisconnectType.LostConnection, true);
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
        x++;

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
                    }
                    else if (IsBanned(frame.m_SenderId).Item1) // Store this in a variable or cache later?
                    {
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.Banned;
                        authenticationFrame.m_Message = IsBanned(frame.m_SenderId).Item2;
                    }
                    else if (NetworkManager.Instance.GetPlayerCount() == NetworkManager.Instance.m_Settings.m_MaxPlayers && NetworkManager.Instance.m_Settings.m_MaxPlayers > 0)
                    {
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.LobbyFull;
                    }
                    else 
                    {
                        m_AuthorizedUsers.Add(frame.m_SenderId);
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.Connected;
                    }
                    SendFrame(authenticationFrame, tempPlayer);
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

    public void Shutdown(string reason)
    {
        OnServerStopped(NetworkServerStopType.Gracefully, reason);
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
                    true, false, false, identity.m_NetworkId
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

                    int id = AddObjectToNetwork(prefab);
                    spawnRPC.m_NetworkId = id;
                    networkBehaviour.m_Spawner = (spawnRPC.m_PrefabIndex == -1) ? id : player.m_NetworkId;
                    serverInfo.m_PrefabIndex = spawnRPC.m_PrefabIndex;
                    SendRPC(spawnRPC, player);

                    if (spawnRPC.m_PrefabIndex == -1) 
                    {
                        player.m_NetworkId = id;
                        NetworkPlayerConnectRPC connectRPC = new NetworkPlayerConnectRPC(player, -1);
                        SendRPCAll(connectRPC);
                    }
                    if (spawnRPC.m_RequestAuthority)
                        serverInfo.AddAuthority(player);

                    NetworkAuthorizationRPC authRPC = new NetworkAuthorizationRPC(true, IsPlayerServer(player), spawnRPC.m_RequestAuthority, id);
                    SendRPC(authRPC, player);
                }
                break;
            case NetworkRPCType.RPC_LIB_DESTROY:
                {
                    GameObject obj = NetworkManager.Instance.GetNetworkedObject(rpc.m_NetworkId);
                    if (PlayerHasAuthority(obj, player))
                    {
                        OnNetworkObjectDestroyed(obj, player);
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
                    NetworkPlayerDisconnctRPC disconnctRPC = NetworkRPC.Parse<NetworkPlayerDisconnctRPC>(command);
                    if (disconnctRPC.m_Player.m_Id == player.m_Id)
                    {
                        // Disconnection checks?
                        RemovePlayer(player, disconnctRPC.m_Reason, disconnctRPC.m_DisconnectType, true);
                    }
                    else
                    {
                        // Authoratative admin checks later : TODO
                        RemovePlayer(player, $"Kicked by: {player.m_Name}", NetworkDisconnectType.Kick, true);
                    }
                } break;
        }
    }
    private int AddObjectToNetwork(GameObject obj)
    {
        NetworkBehaviour networkBehaviour = obj.GetComponent<NetworkBehaviour>();

        int id = UnityEngine.Random.Range(0, int.MaxValue);
        obj.GetComponent<NetworkIdentity>().m_NetworkId = id;
        networkBehaviour.m_IsServer = true;
        networkBehaviour.m_HasAuthority = false;
        networkBehaviour.m_IsClient = false;
        networkBehaviour.m_Spawner = -1; // Server
        NetworkManager.Instance.AddObject(id, obj);

        return id;
    }
    
    public void Clean()
    {
        ELogger.Log($"Cleaning up", ELogger.LogType.Server);

        if (m_Client != null)
        {
            m_Client.Close();
            m_Client.Dispose();
        }

        if (m_Coroutine_CTP != null)
            StopCoroutine(m_Coroutine_CTP);
        if (m_Coroutine_HB != null)
            StopCoroutine(m_Coroutine_HB);
        if (m_Coroutine_IMPF != null)
            StopCoroutine(m_Coroutine_IMPF);

        if (NetworkManager.Instance.IsServer() || NetworkManager.Instance.IsMixed())
            NetworkManager.Instance.Clean();

        m_Status = NetworkServerStatus.Stopped;
        Destroy(this);
    }

    public void RemovePlayer(NetworkPlayer player, string reason, NetworkDisconnectType type = NetworkDisconnectType.Kick, bool deleteObjects = false)
    {
        if(player == null)
            return;

        GameObject obj = NetworkManager.Instance.GetNetworkedObject(player.m_NetworkId);
        if(obj == null)
            return;

        NetworkBehaviour networkBehaviour = obj.GetComponent<NetworkBehaviour>();
        if(networkBehaviour == null)
            return;

        NetworkPlayerDisconnctRPC disconnectRPC = new NetworkPlayerDisconnctRPC(player, type, reason, player.m_NetworkId);
        SendRPC(disconnectRPC, player);

        List<GameObject> objects = player.GetOwnedObjects();
        if (deleteObjects)
        {
            foreach(var networkObject in objects)
                DestroyNetworkedObject(networkObject.GetComponent<NetworkIdentity>().m_NetworkId);
        } else
        {
            foreach (var networkObject in objects)
                networkObject.GetComponent<NetworkObjectServerInfo>().RemoveAuthority(player);
        }

        OnPlayerDisconnected(player, type, reason);
        NetworkManager.Instance.RemovePlayer(player.m_Id);
        if (m_AuthorizedUsers.Contains(player.m_Id))
            m_AuthorizedUsers.Remove(player.m_Id);
        if (m_IPMap.ContainsKey(player.m_Id))
            m_IPMap.Remove(player.m_Id);
        if (m_Heartbeats.ContainsKey(player.m_Id))
            m_Heartbeats.Remove(player.m_Id);
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
        Error,
        Stopping
    }

    public enum NetworkServerStopType
    {
        Gracefully,
        Errored,
        Unknown
    }

    /// <summary>
    /// Initializes any networked objects that were already in the scene
    /// </summary>
    private void InitializeNetworkedObjects()
    {
        NetworkBehaviour[] networkObjects = FindObjectsOfType<NetworkBehaviour>();

        foreach(NetworkBehaviour behaviour in networkObjects)
        {
            NetworkIdentity identity = behaviour.GetComponent<NetworkIdentity>();
            if (identity == null)
                continue;
            if (NetworkManager.Instance.GetNetworkedObject(identity.m_NetworkId) != null)
                continue;

            AddObjectToNetwork(behaviour.gameObject);
            NetworkObjectServerInfo serverInfo = behaviour.gameObject.AddComponent<NetworkObjectServerInfo>();
            serverInfo.m_PrefabIndex = -2;
            // Lets "spawn" the object on our side real fast

            ELogger.Log($"Spawning networked object: {behaviour.gameObject.name}" , ELogger.LogType.Server);
        }
    }

    public virtual void OnServerStarted(string address, int port)
    {
        ELogger.Log($"Server started on: {address}:{port}", ELogger.LogType.Server);
        m_Status = NetworkServerStatus.Connected;
        m_Coroutine_IMPF = StartCoroutine(ImportantFrameWorker());
        m_Coroutine_HB = StartCoroutine(HeartbeatWorker());
        InitializeNetworkedObjects();
        _ = OnReceiveFrame();
    }

    /// <summary>
    /// Called when an object is created on the network
    /// </summary>
    /// <param name="gameObject">The (server) game object that was created</param>
    /// <param name="player">The client who requested the object, can be null if it is server</param>
    public virtual void OnNetworkObjectCreated(GameObject gameObject, NetworkPlayer player)
    {

    }

    /// <summary>
    /// Called right after the object is destroyed on the server, but before it is destroyed on the clients
    /// </summary>
    /// <param name="gameObject">The object that was destroyed</param>
    /// <param name="player">The player who requested the object be destroyed, can be null</param>
    public void OnNetworkObjectDestroyed(GameObject gameObject, NetworkPlayer player)
    {

    }

    /// <summary>
    /// Called when the server is stopped, either by code or by Application.Quit()
    /// </summary>
    /// <param name="type">The type of server stop that happened</param>
    /// <param name="reason">The reason the server was stopped</param>
    public virtual void OnServerStopped(NetworkServerStopType type, string reason)
    {
        if (m_Status == NetworkServerStatus.Stopping)
            return;

        m_Status = NetworkServerStatus.Stopping;
        ELogger.Log($"Server stopped for reason '{reason}' with type {type}", ELogger.LogType.Server);

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.m_IsPlaying = false;

            foreach (NetworkPlayer player in NetworkManager.Instance.GetPlayers())
            {
                RemovePlayer(player, "Server Stopping", NetworkDisconnectType.ServerStopped, true);
            }
        }

        Clean();
    }
    
    /// <summary>
    /// Called when the server errors for any reason
    /// </summary>
    /// <param name="error">The error that has occured</param>
    public virtual void OnServerError(string error)
    {
        m_Status = NetworkServerStatus.Error;
        ELogger.Log($"Server errored with error '{error}'", ELogger.LogType.Server);
        OnServerStopped(NetworkServerStopType.Errored, error);
    }

    /// <summary>
    /// Called when a player connects to the server
    /// </summary>
    /// <param name="player">The player that has connected</param>
    public virtual void OnPlayerConnected(NetworkPlayer player)
    {
        ELogger.Log($"A player has connected to the server: {player.m_Name}|{player.m_Id}", ELogger.LogType.Server);
    }

    /// <summary>
    /// Called when a player has disconnected from the server
    /// </summary>
    /// <param name="player">The player that was disconnected</param>
    /// <param name="type">The type of disconnection it was</param>
    /// <param name="reason">The reason the player was disconnected</param>
    public virtual void OnPlayerDisconnected(NetworkPlayer player, NetworkDisconnectType type, string reason)
    {
        ELogger.Log($"A player has disconnected from the server: {player.m_Name}|{type}|{reason}", ELogger.LogType.Server);
    }
}