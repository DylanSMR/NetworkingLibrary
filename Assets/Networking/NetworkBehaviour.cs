using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(NetworkIdentity))]
public class NetworkBehaviour : MonoBehaviour
{
    public bool m_HasAuthority;

    public bool m_IsLocal;
    public bool m_IsServer;

    /// <summary>
    /// Triggers when a RPC command is sent over the network
    /// </summary>
    /// <param name="rpc"></param>
    public virtual void OnRPCCommand(string content)
    {
        NetworkRPC rpc = NetworkRPC.FromString(content);

        switch(rpc.m_Type)
        {
            case NetworkRPCType.RPC_OBJECT_AUTHORIZATION:
                {
                    NetworkAuthorizationRPC authorizationRPC = NetworkRPC.Parse<NetworkAuthorizationRPC>(content);
                    m_HasAuthority = authorizationRPC.m_LocalAuthSet;
                    m_IsLocal = authorizationRPC.m_LocalSet;
                    m_IsServer = authorizationRPC.m_ServerSet;
                } break;
        }
    }
}
