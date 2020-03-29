using System;

/// <summary>
/// A frame that is used to send basic information with the server, and get some basic info back
/// </summary>
[Serializable]
public class NetworkRPCFrame : NetworkFrame
{
    public string m_RPC;

    /// <summary>
    /// Creates a new NetworkHandshakeFrame
    /// </summary>
    /// <param name="rpc">The rpc to be sent over the network</param>
    public NetworkRPCFrame(string rpc) 
        : base(NetworkFrameType.RPC, NetworkClient.Instance.GetUniqueIndentifier())
    {
        this.m_RPC = rpc;
    }
}
