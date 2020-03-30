using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(NetworkIdentity))]
public class NetworkBehaviour : MonoBehaviour
{
    public bool m_HasAuthority;

    public bool m_IsClient;
    public bool m_IsServer;

    /// <summary>
    /// Who requested to spawn this object?
    /// </summary>
    public int m_Spawner;

    /// <summary>
    /// Triggers when a RPC command is sent over the network
    /// </summary>
    /// <param name="rpc"></param>
    public virtual void OnRPCCommand(string content)
    {
        //NetworkRPC rpc = NetworkRPC.FromString(content);
    }

    public virtual void OnAuthorityChanged(bool status)
    {

    }

    public virtual void OnPlayerJoined(NetworkPlayer m_Player)
    {
        
    }

    public virtual void OnPlayerDisconnected(NetworkPlayer player, NetworkDisconnectType type, string reason)
    {

    }
}
