using System.Collections;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

using Debug = UnityEngine.Debug; // Hmmm

/// <summary>
/// A class that handles all networking between the server and the us (The Client)
/// </summary>
public class NetworkClient : MonoBehaviour
{
    // Networking Fields
    private UdpClient m_Client;
    /// <summary>
    /// A boolean that represents whether the client is connected to the server/proxy
    /// </summary>
    public bool m_Connected { get; private set; } = false;
    /// <summary>
    /// A integer that represents how many times we have tried to connect to the server/proxy
    /// </summary>
    public int m_ConnectionTries { get; private set; } = 0;

    // Ping Fields
    public float m_LastPing { get; private set; } = -1;
    private Stopwatch m_Stopwatch; // Maybe there is a more useful way? Time.time possibly

    /// <summary>
    /// Connects to a game server
    /// </summary>
    /// <param name="address">The address of the game server/proxy</param>
    /// <param name="port">The port of the game server/proxy</param>
    public void Connect(string address, int port)
    {
        m_Client = new UdpClient();

        StartCoroutine(ConnectToServer(address, port));
    }

    /// <summary>
    /// Runs a loop to ensure connection to the game server. 
    /// After a few tries it will eventually fail and log an error
    /// TODO: Call a function or something if it fails to connect
    /// </summary>
    /// <param name="address">The address of the game server/proxy</param>
    /// <param name="port">The port of the game server/proxy</param>
    private IEnumerator ConnectToServer(string address, int port)
    {
        for(; ;)
        {
            if(m_ConnectionTries == 5)
            {
                Debug.LogError("[NetworkClient] Failed to connect to the server.");
                break;
            }

            // Keep trying to connect until we either give up or eventually connect
            if(m_Connected)
            {
                Debug.Log("[NetworkClient] Successfully connected to the server!");
                break;
            }

            m_ConnectionTries++;
            Debug.Log($"[NetworkClient] Attempting to connect to server [{m_ConnectionTries}/5]");
            m_Client.Connect(address, port); // Try and connect to the server
            try
            {
                SendPing(); // Establish some basic communication with the server to know we are connected
            } catch
            {
                // Errors are thrown from generally not connected UdpClients trying to send bytes (frames)
            }

            yield return new WaitForSeconds(2.5f); // Every 2 and a half seconds we are going to try and connect again! Yay :/
        }
    }

    /// <summary>
    /// Used to send the initial handshake to the server
    /// </summary>
    private void SendHandshake()
    {

    }

    /// <summary>
    /// Sends a ping request to the server to calculate our ping
    /// Use 
    /// </summary>
    private void SendPing(bool checkingConnection = false)
    {
        NetworkFrame pingFrame = new NetworkFrame(NetworkFrame.NetworkFrameType.Ping, SystemInfo.deviceUniqueIdentifier);
        if(checkingConnection == false)
        {
            m_Stopwatch = new Stopwatch();
            m_Stopwatch.Start();
        }
        SendFrame(pingFrame);
    }

    /// <summary>
    /// An async function to receive bytes (frames) from the game server
    /// </summary>
    private async Task OnReceiveFrame()
    {
        UdpReceiveResult result = await m_Client.ReceiveAsync();
        NetworkFrame frame = NetworkFrame.ReadFromBytes(result.Buffer);

        switch(frame.m_Type)
        {
            case NetworkFrame.NetworkFrameType.Handshake:
                {
                    
                } break;
            case NetworkFrame.NetworkFrameType.Ping: 
                {
                    m_Connected = true; // Tells us we have definitely received a request from the server
                    if(m_Stopwatch != null)
                    {
                        m_Stopwatch.Stop();
                        m_LastPing = m_Stopwatch.ElapsedMilliseconds;
                        m_Stopwatch = null;
                        Debug.Log($"[NetworkClient] Calculated Ping | {m_LastPing}ms");
                    }
                } break;
            case NetworkFrame.NetworkFrameType.RPC:
                {

                } break;
        }

        OnReceiveFrame();
    }

    private void SendFrame(NetworkFrame frame)
    {
        byte[] bytes = frame.ToBytes();
        m_Client.Send(bytes, bytes.Length);
    }
}
