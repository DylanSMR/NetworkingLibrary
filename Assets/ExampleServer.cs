using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleServer : NetworkServer
{
    public override void OnPlayerConnected(NetworkPlayer player)
    {
        base.OnPlayerConnected(player);
    }

    public override void OnPlayerDisconnected(NetworkPlayer player, NetworkDisconnectType type, string reason)
    {
        base.OnPlayerDisconnected(player, type, reason);
    }

    public override void OnRPCCommand(NetworkPlayer player, string command)
    {
        base.OnRPCCommand(player, command);
        NetworkRPC rpc = NetworkRPC.FromString(command);

        switch(rpc.m_Type)
        {
            case NetworkRPCType.RPC_CUSTOM_PLAYER:
                {
                    NetworkPlayerRPC playerRPC = NetworkRPC.Parse<NetworkPlayerRPC>(command);

                    GameObject obj = NetworkManager.Instance.GetNetworkedObject(playerRPC.m_NetworkId);
                    if (PlayerHasAuthority(obj, player))
                    {
                        if (playerRPC.m_Health != -1)
                        {
                            ELogger.Log($"{playerRPC.m_NetworkId} is trying to edit their health to {playerRPC.m_Health}!", ELogger.LogType.Server);
                            break;
                        }

                        obj.GetComponent<Player>().UpdateColor(playerRPC.m_Color);
                        UpdateRPC(obj, playerRPC);
                        SendRPCAll(playerRPC); 
                    }
                }
                break;
        }
    }

    public override void OnServerStarted(string address, int port)
    {
        base.OnServerStarted(address, port);
    }

    public override void OnServerError(string error)
    {
        base.OnServerError(error);
    }

    public override void OnServerStopped(NetworkServerStopType type, string reason)
    {
        base.OnServerStopped(type, reason);
    }
}
