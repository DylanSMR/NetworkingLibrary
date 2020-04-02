using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkManagerUI : MonoBehaviour
{
    public string m_Address;
    private string m_StringPort;
    public string m_Password;

    private void Start()
    {
        if(NetworkManager.Instance.m_Settings != null)
        {
            m_Address = NetworkManager.Instance.m_Settings.m_ServerAddress;
            m_StringPort = NetworkManager.Instance.m_Settings.m_ServerPort.ToString();
            m_Password = NetworkManager.Instance.m_Settings.m_ServerPassword;
        }
    }

    private void OnGUI()
    {
        if (NetworkManager.Instance.m_IsPlaying)
            return;

        GUI.Label(new Rect(10, 10, 200, 200), "Server Address");
        m_Address = GUI.TextField(new Rect(10, 30, 125, 25), m_Address);
        GUI.Label(new Rect(10, 55, 200, 200), "Server Port");
        m_StringPort = GUI.TextField(new Rect(10, 75, 125, 25), m_StringPort);
        GUI.Label(new Rect(10, 105, 200, 200), "Server Password");
        m_Password = GUI.TextField(new Rect(10, 125, 125, 25), m_Password);

        if(GUI.Button(new Rect(10, 160, 125, 25), "Host Server"))
        {
            int port;
            if(int.TryParse(m_StringPort, out port))
                NetworkManager.Instance.Host(port);
        }

        if (GUI.Button(new Rect(10, 200, 125, 25), "Host With Proxy"))
        {
            int port;
            if (int.TryParse(m_StringPort, out port))
                NetworkManager.Instance.HostWithProxy(m_Address, port, m_Password);
        }

        if (GUI.Button(new Rect(10, 240, 125, 25), "Host/Connect (Server)"))
        {
            int port;
            if (int.TryParse(m_StringPort, out port))
            {
                NetworkManager.Instance.HostMixed(m_Address, port, m_Password, false);
            }
        }

        if (GUI.Button(new Rect(10, 280, 125, 25), "Connect To Server"))
        {
            int port;
            if (int.TryParse(m_StringPort, out port))
            {
                NetworkManager.Instance.Connect(m_Address, port, m_Password);
            }
        }

        if (GUI.Button(new Rect(10, 320, 125, 25), "Host/Connect (Proxy)"))
        {
            int port;
            if (int.TryParse(m_StringPort, out port))
            {
                NetworkManager.Instance.HostMixed(m_Address, port, m_Password, true);
            }
        }
    }
}
