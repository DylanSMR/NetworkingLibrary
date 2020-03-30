using System;
using System.Collections.Generic;
using UnityEngine;
using static NetworkServerSettings;

[Serializable]
public class NetworkPlayer 
{
    public string m_Name;
    public string m_Id;
    public int m_NetworkId;

    public List<GameObject> GetOwnedObjects()
    {
        List<GameObject> list = new List<GameObject>();
        foreach(var networkedPair in NetworkManager.Instance.GetNetworkedObjects())
        {
            if (networkedPair.Value == null)
                continue;
            NetworkBehaviour behaviour = networkedPair.Value.GetComponent<NetworkBehaviour>();
            if (behaviour == null)
                continue;
            if(NetworkManager.Instance.m_Settings.m_NetworkType == ENetworkType.Client)
            {
                if(behaviour.m_HasAuthority)
                {
                    list.Add(networkedPair.Value);
                    continue;
                }    
            } else
            {
                if (NetworkServer.Instance.PlayerHasAuthority(networkedPair.Value, this))
                {
                    list.Add(networkedPair.Value);
                    continue;
                }
            }
        }

        return list;
    }
}
