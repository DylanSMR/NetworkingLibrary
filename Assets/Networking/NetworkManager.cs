using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    [Header("Server Settings")]
    [Tooltip("The address of the server that is going to be connected to")]
    public string m_ServerAddress = "127.0.0.1";
    [Tooltip("The port of the server that is going to be connected to")]
    public int m_ServerPort = 58120;
    [Tooltip("The password of the server that is going to be connected to")]
    public string m_ServerPassword = "!kappa3!";

    [Header("Proxy Settings")]
    [Tooltip("The address of the proxy that is going to be connected to")]
    public string m_ProxyAddress;
    [Tooltip("The port of the proxy that is going to be connected to")]
    public int m_ProxyPort;
    [Tooltip("The password of the proxy that is going to be connected to")]
    public string m_ProxyPassword;

    [Header("Network Settings")]
    [Tooltip("How many players can be connected to the server at one time")]
    public int m_MaxPlayers;
    [Tooltip("How the server is being hosted (being connected via proxy or by itself)")]
    public ENetworkConnectionType m_ConnectionType;
    [Tooltip("Is the unity process going to be a client, a server, or both")]
    public ENetworkType m_NetworkType;

    /// <summary>
    /// A static instance to the always active NetworkManager object
    /// </summary>
    public static NetworkManager Instance;
    private NetworkServer m_Server;
    private NetworkClient m_Client;

    private void Awake()
    {
        if(Instance != null)
        {
            Debug.LogWarning("[NetworkManager] A new network manager was created, yet one already exists.");
            return; // We want to use the already existing network manager
        }
        Instance = this;
        DontDestroyOnLoad(this);
    }

    private void Start()
    {
        if (m_NetworkType == ENetworkType.Server || m_NetworkType == ENetworkType.Mixed)
            m_Server = gameObject.AddComponent<NetworkServer>(); // Create our server
        if (m_NetworkType == ENetworkType.Client || m_NetworkType == ENetworkType.Mixed)
            m_Client = gameObject.AddComponent<NetworkClient>();

        Connect();
    }

    /// <summary>
    /// Starts hosting a server with the editor specified address, port and password
    /// </summary>
    public void Host()
        => Host(m_ServerAddress, m_ServerPort, m_ServerPassword);

    /// <summary>
    /// Starts hosting a server given a address, port, and password to host it with
    /// </summary>
    /// <param name="address">The address the server will be hosting on</param>
    /// <param name="port">The port the server will be hosting on</param>
    /// <param name="password">The password used to connect to the server. Leave blank if no password is required</param>
    public void Host(string address, int port, string password = "")
    {

    }

    /// <summary>
    /// Connect to a server with the predefined address and port.
    /// </summary>
    /// <param name="password">The password required for the server. Leave blank if no password is required</param>
    public void Connect(string password = "")
       => Connect(m_ServerAddress, m_ServerPort, password);

    /// <summary>
    /// Connect to a server with a custom address and port (proxy/server)
    /// </summary>
    /// <param name="serverAddress">The IP address of the server we are going to connect to</param>
    /// <param name="serverPort">The port of the server we are going to connect to</param>
    /// <param name="password">The password required for the server. Leave blank if no password is required</param>
    public void Connect(string serverAddress, int serverPort, string password = "")
    {
        m_Client.Connect(serverAddress, serverPort, password);
    }

    public enum ENetworkType
    {
        Server,
        Client,
        Mixed
    }

    public enum ENetworkConnectionType
    {
        Server,
        Proxy
    }
}
