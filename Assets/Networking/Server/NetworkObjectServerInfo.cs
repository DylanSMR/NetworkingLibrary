using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkObjectServerInfo : MonoBehaviour
{
    public int m_PrefabIndex;
    public Dictionary<NetworkRPCType, NetworkRPC> m_RPCUpdates = new Dictionary<NetworkRPCType, NetworkRPC>();
    public List<NetworkRPCType> m_RPCTypes = new List<NetworkRPCType>();
    public List<string> m_Authorities = new List<string>();

    /// <summary>
    /// Updates the latest available RPC
    /// </summary>
    /// <param name="rpc">The RPC replacing the most recent</param>
    public void UpdateRPC(NetworkRPC rpc)
    {
        if (m_RPCUpdates.ContainsKey(rpc.m_Type))
            m_RPCUpdates[rpc.m_Type] = rpc;
        else
            m_RPCUpdates.Add(rpc.m_Type, rpc);

        if (!m_RPCTypes.Contains(rpc.m_Type))
            m_RPCTypes.Add(rpc.m_Type);
    }

    public void RemoveAuthority(NetworkPlayer player)
    {
        if (!HasAuthority(player))
            return;

        m_Authorities.Remove(player.m_Id);
    }

    public void AddAuthority(NetworkPlayer player)
    {
        if (m_Authorities.Contains(player.m_Id))
            return;

        m_Authorities.Add(player.m_Id);
    }

    public bool HasAuthority(NetworkPlayer player)
    {
        if (player == null)
            return true; // Its probably server

        return m_Authorities.Contains(player.m_Id);
    }

    public NetworkRPC GetRPC(NetworkRPCType type)
    {
        if (m_RPCUpdates.ContainsKey(type))
            return m_RPCUpdates[type];

        return null;
    }
}
