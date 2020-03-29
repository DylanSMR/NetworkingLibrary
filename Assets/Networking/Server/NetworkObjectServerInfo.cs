using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkObjectServerInfo : MonoBehaviour
{
    public int m_PrefabIndex;
    public Dictionary<string, NetworkRPC> m_RPCUpdates;

    private void Update()
    {
        m_RPCUpdates = new Dictionary<string, NetworkRPC>();
    }
}
