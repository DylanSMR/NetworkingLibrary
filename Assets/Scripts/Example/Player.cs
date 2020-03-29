using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : NetworkBehaviour
{
    private void Update()
    {
        if(m_IsClient)
        {
            if(m_HasAuthority)
            {
                if(Input.GetKeyDown(KeyCode.Space))
                {
                    transform.position += new Vector3(0, 0.25f, 0);
                    NetworkTransformRPC rpc = new NetworkTransformRPC(transform, GetComponent<NetworkIdentity>().m_NetworkId);

                    NetworkClient.Instance.SendRPC(rpc);
                }
                // We are client, and we have authority! Yay :)
            } else
            {
                // We are client, but we dont have authority. Maybe we "guess" the position here to save server resources?
            }
        } 

        if(m_IsServer)
        {
            // We are server, we can do anything we want here. This should only be called on server
            // Unless some is cheatsydoodling, then it doesnt matter cause they dont have authority according to the server
            // Pff, nerds.

            // Do server stuff here though, check position, make sure not flying, no infinite ammo, etc
        }
    }

    /// <summary>
    /// An optional way to receive any extra RPC commands.
    /// Can be used to update things like health, active gun, animation (maybe done by library later), etc
    /// </summary>
    /// <param name="content">The content of the rpc we received. See example below to understand more</param>
    public override void OnRPCCommand(string content)
    {
        NetworkRPC rpc = NetworkRPC.FromString(content);
        base.OnRPCCommand(content);

        switch (rpc.m_Type)
        {
            case NetworkRPCType.RPC_CUSTOM_TRANSFORM:
                {
                    NetworkTransformRPC transformRPC = NetworkRPC.Parse<NetworkTransformRPC>(content);
                    transform.position = transformRPC.m_Position;
                    transform.eulerAngles = transformRPC.m_Rotation;
                    transform.localScale = transformRPC.m_Scale;
                } break;
        }
    }
}
