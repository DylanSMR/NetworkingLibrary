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
    /// Whether the RPC absolutely has to reach the target or things could go very wrong
    /// </summary>
    public bool m_Important = false;

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
    public bool m_RequestAuthority;

    public NetworkSpawnRPC(int index, bool requestAuthority = false, int id = -1) : base(id, NetworkRPCType.RPC_LIB_SPAWN)
    {
        this.m_PrefabIndex = index;
        this.m_RequestAuthority = requestAuthority;
    }
}

[Serializable]
public class NetworkDestroyRPC : NetworkRPC
{ 
    public NetworkDestroyRPC(int netId) : base(netId, NetworkRPCType.RPC_LIB_DESTROY)
    {

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

    public NetworkTransformRPC(Transform transform, int id) : base(id, NetworkRPCType.RPC_LIB_TRANSFORM)
    {
        m_Position = MathHelpers.RoundVector3(transform.position);
        m_Rotation = MathHelpers.RoundVector3(transform.eulerAngles);
        m_Scale = MathHelpers.RoundVector3(transform.localScale);
    }
}

[Serializable]
public class NetworkPlayerRPC : NetworkRPC
{
    public Color m_Color;
    public float m_Health;

    public NetworkPlayerRPC(Color color, float health, int id) : base(id, NetworkRPCType.RPC_CUSTOM_PLAYER)
    {
        m_Color = color;
        m_Health = health;
    }  
}

[Serializable]
public class NetworkPlayerConnectRPC : NetworkRPC
{
    public NetworkPlayer m_Player;

    public NetworkPlayerConnectRPC(NetworkPlayer player, int id) : base(id, NetworkRPCType.RPC_LIB_CONNECTED)
    {
        m_Player = player;
    }
}

[Serializable]
public class NetworkPlayerDisconnctRPC : NetworkRPC
{
    public NetworkPlayer m_Player;
    public NetworkDisconnectType m_DisconnectType;
    public string m_Reason;

    public NetworkPlayerDisconnctRPC(NetworkPlayer player, NetworkDisconnectType type, string reason, int id) : base(id, NetworkRPCType.RPC_LIB_DISCONNECTED)
    {
        m_Player = player;
        m_DisconnectType = type;
        m_Reason = reason;
    }
}

public enum NetworkDisconnectType
{ 
    Kick,
    Ban,
    Request,
    LostConnection
}


public enum NetworkRPCType
{
    RPC_LIB_INVALID,
    RPC_LIB_SPAWN,
    RPC_LIB_DESTROY,
    RPC_LIB_OBJECT_NETAUTH,
    RPC_LIB_TRANSFORM,
    RPC_LIB_CONNECTED,
    RPC_LIB_DISCONNECTED,

    RPC_CUSTOM_PLAYER
}