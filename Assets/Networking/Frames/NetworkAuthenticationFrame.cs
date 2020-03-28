using System;

/// <summary>
/// A frame that is used to authenticate with the server
/// </summary>
[Serializable]
public class NetworkAuthenticationFrame : NetworkFrame
{
    /// <summary>
    /// The password used to connect to the server
    /// </summary>
    public string m_Password;
    /// <summary>
    /// The response given by the server
    /// </summary>
    public NetworkAuthenticationResponse m_Response;
    /// <summary>
    /// Any additional messages
    /// </summary>
    public string m_Message;

    /// <summary>
    /// Creates a new NetworkAuthenticationFrame
    /// </summary>
    /// <param name="m_Password">A password used to connect to the game server</param>
    public NetworkAuthenticationFrame(string m_Password, 
        NetworkAuthenticationResponse response = NetworkAuthenticationResponse.Unknown,
        string message = "") 
        : base(NetworkFrameType.Authentication, NetworkClient.Instance.GetUniqueIndentifier())
    {
        this.m_Password = m_Password;
        this.m_Response = response;
        this.m_Message = "";
    }

    /// <summary>
    /// A response given by the server to determine if we have been connected
    /// </summary>
    public enum NetworkAuthenticationResponse
    {
        LobbyFull,
        Banned,
        Error,
        IncorrectPassword,
        Unknown,
        Connected
    }
}
