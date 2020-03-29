using System;

/// <summary>
/// A frame that is used to send basic information with the server, and get some basic info back
/// </summary>
[Serializable]
public class NetworkHandshakeFrame : NetworkFrame
{
    public string m_DisplayName;

    /// <summary>
    /// Creates a new NetworkHandshakeFrame
    /// </summary>
    /// <param name="m_DisplayName">The display name of the local player*</param>
    public NetworkHandshakeFrame(string m_DisplayName) 
        : base(NetworkFrameType.Handshake, 
            NetworkClient.Instance.GetUniqueIndentifier())
    {
        this.m_DisplayName = m_DisplayName;
    }
}
