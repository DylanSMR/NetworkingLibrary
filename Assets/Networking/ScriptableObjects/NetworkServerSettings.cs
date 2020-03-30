using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Settings", menuName = "Networking/Server Settings", order = 1)]
public class NetworkServerSettings : ScriptableObject
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
    [Tooltip("The available prefabs for networking")]
    public List<GameObject> m_NetworkPrefabs;
    [Tooltip("The prefab that will be spawned when a player connects")]
    public GameObject m_PlayerPrefab;

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