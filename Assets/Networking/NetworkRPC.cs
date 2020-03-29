using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// The default RPC used to parse more advanced RPC's
/// </summary>
[Serializable]
public class NetworkRPC
{
    /// <summary>
    /// The ID of the object this RPC is going to
    /// If this is set to "rpc_client_call" then it will be ran on the NetworkClient itself
    /// </summary>
    public int m_NetworkId;
    /// <summary>
    /// The type of RPC this object represents
    /// By default set to RPC_INVALID
    /// </summary>
    public NetworkRPCType m_Type;

    /// <summary>
    /// Creates a new default NetworkRPC
    /// </summary>
    /// <param name="id">The network id of the object this rpc is going to</param>
    /// <param name="type">The type of RPC this is</param>
    public NetworkRPC(int id, NetworkRPCType type = NetworkRPCType.RPC_INVALID)
    {
        this.m_NetworkId = id;
        this.m_Type = type;
    }

    public override string ToString()
    {
        return JsonUtility.ToJson(this);
    }

    public static NetworkRPC FromString(string content)
    {
        return Parse<NetworkRPC>(content);
    }

    public static T Parse<T>(string content)
    {
        return JsonUtility.FromJson<T>(content);
    }
}

/// <summary>
/// An RPC used to spawn objects on the network
/// </summary>
[Serializable]
public class NetworkSpawnRPC : NetworkRPC
{
    /// <summary>
    /// The index of the prefab we are trying to spawn
    /// </summary>
    public int m_PrefabIndex;
    public int m_NetworkIndex;
    public bool m_RequestAuthority;

    public NetworkSpawnRPC(int index, int netId, bool requestAuthority = false, int id = -1) : base(id, NetworkRPCType.RPC_SPAWN)
    {
        this.m_PrefabIndex = index;
        m_NetworkIndex = netId;
        this.m_RequestAuthority = requestAuthority;
    }
}

/// <summary>
/// An RPC used to assign authorization to objects on the network
/// </summary>
[Serializable]
public class NetworkAuthorizationRPC : NetworkRPC
{
    public bool m_LocalSet;
    public bool m_ServerSet;
    public bool m_LocalAuthSet;

    public NetworkAuthorizationRPC(bool local, bool server, bool localauth, int id) : base(id, NetworkRPCType.RPC_OBJECT_AUTHORIZATION)
    {
        m_LocalSet = local;
        m_ServerSet = server;
        m_LocalAuthSet = localauth;
    }
}

public enum NetworkRPCType
{
    RPC_INVALID,
    RPC_SPAWN,
    RPC_OBJECT_AUTHORIZATION
}