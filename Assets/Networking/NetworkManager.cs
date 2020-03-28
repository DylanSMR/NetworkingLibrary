using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    [Header("Server Settings")]
    public string m_ServerAddress;
    public int m_ServerPort;
    public string m_ServerPassword;

    [Header("Proxy Settings")]
    public string m_ProxyAddress;
    public int m_ProxyPort;

    [Header("Network Settings")]
    public int m_MaxPlayers;
    public ENetworkConnectionType m_ConnectionType;
    public ENetworkType m_NetworkType;

    public NetworkManager Instance;
    private NetworkServer m_Server;
    private NetworkClient m_Client;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (m_NetworkType == ENetworkType.Server || m_NetworkType == ENetworkType.Mixed)
            m_Server = gameObject.AddComponent<NetworkServer>(); // Create our server
        if (m_NetworkType == ENetworkType.Client || m_NetworkType == ENetworkType.Mixed)
            m_Client = gameObject.AddComponent<NetworkClient>();
    }

    private void Host()
    {
        // Start hosting the server, do any pre-init we need to do
    }

    private void Connect()
       => Connect(m_ServerAddress, m_ServerPort);

    private void Connect(string serverAddress, int serverPort)
    {

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
