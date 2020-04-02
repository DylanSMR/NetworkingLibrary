using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class NetworkServer : NetworkConnection
{
    // Networking Fields
    public static NetworkServer Instance;

    private Dictionary<string, int> m_Heartbeats;
    private List<string> m_AuthorizedUsers;

    private string m_Password;

    public void SetPassword(string password)
        => m_Password = password;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning("[Server] A new network server was created, yet one already exists.");
            return; // We want to use the already existing network server
        }
        Instance = this;

        m_AuthorizedUsers = new List<string>();
        m_Heartbeats = new Dictionary<string, int>();
    } 

    private IEnumerator HeartbeatWorker()
    {
        while(GetNetworkState() == NetworkState.Established)
        {
            foreach (var player in NetworkManager.Instance.GetPlayers())
            {
                if(!m_Heartbeats.ContainsKey(player.m_Id))
                {
                    m_Heartbeats.Add(player.m_Id, 0);
                    continue;
                }
                if(m_Heartbeats[player.m_Id] == 2)
                {
                    RemovePlayer(player, "Heartbeat lost", NetworkDisconnectType.LostConnection);
                    continue;
                }
                m_Heartbeats[player.m_Id] += 1;

                NetworkFrame heartFrame = new NetworkFrame(NetworkFrame.NetworkFrameType.Heartbeat, "server", player.m_Id);
                QueueFrame(heartFrame);
            }

            yield return new WaitForSeconds(2.5f);
        }
    }

    public override void OnReceiveData(NetworkFrame frame, IPEndPoint point, byte[] bytes)
    {
        base.OnReceiveData(frame, point, bytes);
        NetworkPlayer player = NetworkManager.Instance.GetPlayer(frame.m_SenderId);

        if (!IsUserAuthorized(frame.m_SenderId) && frame.m_Type != NetworkFrame.NetworkFrameType.Authentication)
            return;

        switch (frame.m_Type)
        {
            case NetworkFrame.NetworkFrameType.Handshake:
                {
                    NetworkHandshakeFrame handshake = NetworkFrame.Parse<NetworkHandshakeFrame>(bytes);
                    player = NetworkManager.Instance.AddPlayer(handshake.m_SenderId, new NetworkPlayer()
                    {
                        m_Id = handshake.m_SenderId,
                        m_Name = handshake.m_DisplayName
                    });

                    NetworkSpawnRPC spawnRpc = new NetworkSpawnRPC(-1, true);
                    OnRPCCommand(player, spawnRpc.ToString());
                    UpdatePlayer(player);
                } break;
            case NetworkFrame.NetworkFrameType.Authentication: // TODO: Clean this? Its terrible lol
                {
                    NetworkAuthenticationFrame authenticationFrame = NetworkFrame.Parse<NetworkAuthenticationFrame>(bytes);

                    if (m_Password != "" && m_Password != null && authenticationFrame.m_Password != m_Password)
                    {
                        Debug.Log(m_Password);
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.IncorrectPassword;
                    }
                    else if (NetworkManager.Instance.GetPlayerCount() == NetworkManager.Instance.m_Settings.m_MaxPlayers && NetworkManager.Instance.m_Settings.m_MaxPlayers > 0)
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.LobbyFull;
                    else
                    {
                        Debug.Log("bop");
                        m_AuthorizedUsers.Add(frame.m_SenderId);
                        authenticationFrame.m_Response = NetworkAuthenticationFrame.NetworkAuthenticationResponse.Connected;
                    }
                    authenticationFrame.m_TargetId = authenticationFrame.m_SenderId;
                    authenticationFrame.m_SenderId = "server";
                    QueueFrame(authenticationFrame);
                } break;
            case NetworkFrame.NetworkFrameType.Ping:
                {
                    NetworkFrame pingFrame = new NetworkFrame(NetworkFrame.NetworkFrameType.Ping, "server", frame.m_SenderId);
                    QueueFrame(pingFrame);
                } break;
            case NetworkFrame.NetworkFrameType.RPC:
                {
                    NetworkRPCFrame rpcFrame = NetworkFrame.Parse<NetworkRPCFrame>(bytes);
                    OnRPCCommand(player,rpcFrame.m_RPC);
                } break;
            case NetworkFrame.NetworkFrameType.Heartbeat:
                {
                    if(m_Heartbeats.ContainsKey(frame.m_SenderId))
                        m_Heartbeats[frame.m_SenderId] = 0;
                    else
                        m_Heartbeats.Add(frame.m_SenderId, 0);
                } break;
        }
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
                        OnPlayerConnected(player);
                    }
                    if (spawnRPC.m_RequestAuthority)
                        serverInfo.AddAuthority(player);

                    NetworkAuthorizationRPC authRPC = new NetworkAuthorizationRPC(true, IsPlayerServer(player), spawnRPC.m_RequestAuthority, id);
                    SendRPC(authRPC, player);
                    OnNetworkObjectCreated(prefab, player);
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
                        // Do any checks on the data here, such as being out of bounds, etc

                        if(!IsPlayerServer(player)) // We dont need to update info we already have
                        {
                            obj.transform.position = transformRPC.m_Position;
                            obj.transform.eulerAngles = transformRPC.m_Rotation;
                            obj.transform.localScale = transformRPC.m_Scale;
                        }

                        UpdateRPC(obj, transformRPC);
                        SendRPCAll(transformRPC, new List<string>() { player.m_Id }); // Probably should add a exclude to this so we dont send it to ourselves? Idk
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
                        // This is literally terrible, this would just kick the player who is trying to kick. 
                        // Make a proper "kick" rpc or add some property into the disconnect rpc
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

    public void Destroy()
    {
        ELogger.Log($"Cleaning up", ELogger.LogType.Server);

        StopAllCoroutines();
        CancelInvoke();

        NetworkManager.Instance.Clean();
        Destroy(this);
    }

    public void RemovePlayer(NetworkPlayer player, string reason, NetworkDisconnectType type = NetworkDisconnectType.Kick, bool deleteObjects = false)
    {
        List<GameObject> objects = player.GetOwnedObjects();
        if (deleteObjects)
            objects.ForEach(x => DestroyNetworkedObject(x.GetComponent<NetworkIdentity>().m_NetworkId));
        else
            objects.ForEach(x => x.GetComponent<NetworkObjectServerInfo>().RemoveAuthority(player));

        NetworkManager.Instance.RemovePlayer(player.m_Id);
        if (m_AuthorizedUsers.Contains(player.m_Id))
            m_AuthorizedUsers.Remove(player.m_Id);
        if (m_Heartbeats.ContainsKey(player.m_Id))
            m_Heartbeats.Remove(player.m_Id);

        NetworkPlayerDisconnctRPC disconnectRPC = new NetworkPlayerDisconnctRPC(player, type, reason, player.m_NetworkId);
        SendRPC(disconnectRPC, player); // TODO: Look into how this works

        OnPlayerDisconnected(player, type, reason);
    }

    private void OnApplicationQuit()
    {
        Shutdown(NetworkShutdownType.Gracefully, "OnApplicationQuit");
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
        => SendRPCAll(rpc, null);
    public void SendRPCAll(NetworkRPC rpc, List<string> players)
    {
        foreach(var player in NetworkManager.Instance.GetPlayers())
        {
            if (players != null && players.Contains(player.m_Id))
                continue;
            SendRPC(rpc, player);
        }
    }

    /// <summary>
    /// Constructs a NetworkFrame and sends it from a NetworkRPC
    /// </summary>
    /// <param name="rpc">The RPC being sent over the network</param>
    private void SendRPC(NetworkRPC rpc, NetworkPlayer player)
    {
        NetworkRPCFrame frame = new NetworkRPCFrame(rpc.ToString(), "server", player.m_Id);
        frame.m_Important = rpc.m_Important;

        QueueFrame(frame);
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

            ELogger.Log($"Spawning pre-existing networked object: {behaviour.gameObject.name}" , ELogger.LogType.Server);
        }
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

    /// <summary>
    /// Called when an object is created on the network
    /// </summary>
    /// <param name="gameObject">The (server) game object that was created</param>
    /// <param name="player">The client who requested the object, can be null if it is server</param>
    public virtual void OnNetworkObjectCreated(GameObject gameObject, NetworkPlayer player) { }

    /// <summary>
    /// Called right after the object is destroyed on the server, but before it is destroyed on the clients
    /// </summary>
    /// <param name="gameObject">The object that was destroyed</param>
    /// <param name="player">The player who requested the object be destroyed, can be null</param>
    public void OnNetworkObjectDestroyed(GameObject gameObject, NetworkPlayer player) { }

    public override void OnConnectionEstablished()
    {
        base.OnConnectionEstablished();

        StartCoroutine(HeartbeatWorker());
        InitializeNetworkedObjects();
    }

    public override void OnConnectionShutdown(NetworkShutdownType type, string reason)
    {
        base.OnConnectionShutdown(type, reason);
        ELogger.Log($"Server stopped for reason '{reason}' with type {type}", ELogger.LogType.Server);

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.m_IsPlaying = false;

            foreach (NetworkPlayer player in NetworkManager.Instance.GetPlayers())
                RemovePlayer(player, "Server Stopping", NetworkDisconnectType.ServerStopped, true);
        }

        Destroy();
    }

    public override void OnConnectionError(NetworkError error)
    {
        base.OnConnectionError(error);

        ELogger.Log($"Server errored with error '{error}'", ELogger.LogType.Server);
    }
}