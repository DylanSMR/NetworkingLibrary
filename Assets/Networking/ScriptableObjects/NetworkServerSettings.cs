using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Settings", menuName = "Networking/Server Settings", order = 1)]
public class NetworkServerSettings : ScriptableObject
{
    [Header("Server Settings")]
    [Tooltip("The default address of the server")]
    public string m_ServerAddress = "127.0.0.1";
    [Tooltip("The default port of the server")]
    public int m_ServerPort = 58120;
    [Tooltip("The default password of the server")]
    public string m_ServerPassword = "!kappa3!";

    [Header("Network Settings")]
    [Tooltip("How many players can be connected to the server at one time")]
    public int m_MaxPlayers;
    [Tooltip("The available prefabs for networking")]
    public List<GameObject> m_NetworkPrefabs;
    [Tooltip("The prefab that will be spawned when a player connects")]
    public GameObject m_PlayerPrefab;
}