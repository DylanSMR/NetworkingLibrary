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
    public NetworkRPC(int id, NetworkRPCType type = NetworkRPCType.RPC_LIB_INVALID)
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

    public NetworkSpawnRPC(int index, int netId, bool requestAuthority = false, int id = -1) : base(id, NetworkRPCType.RPC_LIB_SPAWN)
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

    public NetworkAuthorizationRPC(bool local, bool server, bool localauth, int id) : base(id, NetworkRPCType.RPC_LIB_OBJECT_NETAUTH)
    {
        m_LocalSet = local;
        m_ServerSet = server;
        m_LocalAuthSet = localauth;
    }
}

/// <summary>
/// An RPC used to update a objects transform to other players
/// A better way might be to use velocity, and have the server send transform updates?
/// </summary>
[Serializable]
public class NetworkTransformRPC : NetworkRPC
{
    public Vector3 m_Position;
    public Vector3 m_Rotation;
    public Vector3 m_Scale;

    public NetworkTransformRPC(Transform transform, int id) : base(id, NetworkRPCType.RPC_CUSTOM_TRANSFORM)
    {
        m_Position = transform.position;
        m_Rotation = transform.eulerAngles;
        m_Scale = transform.localScale;
    }
}

public enum NetworkRPCType
{
    RPC_LIB_INVALID,
    RPC_LIB_SPAWN,
    RPC_LIB_OBJECT_NETAUTH,

    RPC_CUSTOM_TRANSFORM
}