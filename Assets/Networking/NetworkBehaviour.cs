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
}
