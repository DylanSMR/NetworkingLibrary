using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class NetworkConnection : MonoBehaviour
{
    private Thread m_ReceiveThread;
    private Thread m_SendThread;
    private UdpClient m_Client;

    private NetworkState m_NetworkState = NetworkState.Connecting;

    private bool m_IsServer = false;
    private bool m_Running = false;

    private int m_Tries = 0;
    private int m_SentFrames = 0;
    private int m_ReceivedFrames = 0;
    private int m_ProcessedFrames = 0;

    private Queue<DataChunk> m_ProcessBuffer;
    private Queue<SendChunk> m_SendBuffer;
    private Dictionary<string, IPEndPoint> m_Endpoints;

    private void OnGUI()
    {
        if (m_Client == null)
            return;

        if (NetworkManager.Instance.IsMixed())
        {
            if(m_IsServer)
            {
                GUI.color = Color.red;
                GUI.Label(new Rect(50, 280, 500, 500), "Server Statistics");
                GUI.Label(new Rect(20, 300, 500, 500), $"Network State: {GetNetworkState()}");
                GUI.Label(new Rect(20, 320, 500, 500), $"Network Running: {m_Running}");
                GUI.Label(new Rect(20, 340, 500, 500), $"Connection Tries: {m_Tries}");
                GUI.Label(new Rect(20, 360, 500, 500), $"Sent Frames: {GetSentFrames()}");
                GUI.Label(new Rect(20, 380, 500, 500), $"Received Frames: {GetReceivedFrames()}");
                GUI.Label(new Rect(20, 400, 500, 500), $"Processed Frames: {GetProcesedFrames()}");
                GUI.Label(new Rect(20, 420, 500, 500), $"Process Buffer: {GetProcessBufferCount()}");
                GUI.Label(new Rect(20, 440, 500, 500), $"Send Buffer: {GetSendBufferCount()}");
                GUI.Label(new Rect(20, 460, 500, 500), $"Client Connected: {m_Client.Client.Connected}");
                GUI.Label(new Rect(20, 480, 500, 500), $"Client Availability: {m_Client.Available}");
                return;
            }
        }

        GUI.color = Color.blue;
        GUI.Label(new Rect(50, 20, 500, 500), "Client Statistics");
        GUI.Label(new Rect(20, 40, 500, 500), $"Network State: {GetNetworkState()}");
        GUI.Label(new Rect(20, 60, 500, 500), $"Network Running: {m_Running}");
        GUI.Label(new Rect(20, 80, 500, 500), $"Connection Tries: {m_Tries}");
        GUI.Label(new Rect(20, 100, 500, 500), $"Sent Frames: {GetSentFrames()}");
        GUI.Label(new Rect(20, 120, 500, 500), $"Received Frames: {GetReceivedFrames()}");
        GUI.Label(new Rect(20, 140, 500, 500), $"Processed Frames: {GetProcesedFrames()}");
        GUI.Label(new Rect(20, 160, 500, 500), $"Process Buffer: {GetProcessBufferCount()}");
        GUI.Label(new Rect(20, 180, 500, 500), $"Send Buffer: {GetSendBufferCount()}");
        GUI.Label(new Rect(20, 200, 500, 500), $"Client Connected: {m_Client.Client.Connected}");
        GUI.Label(new Rect(20, 220, 500, 500), $"Client Availability: {m_Client.Available}");
    }

    public void Create(int port)
        => Initialize("server", false, false, port);

    public void Connect(string id, string address, int port)
        => Initialize(id, true, false, port, address);

    public void ConnectAsServer(string address, int port)
        => Initialize("server", false, true, port, address);

    public void Shutdown(NetworkShutdownType type, string reason)
    {
        OnConnectionShutdown(type, reason);
        Destroy();
    }

    // TODO: Clean this function up, its not pretty 
    private void Initialize(string id, bool client = false, bool proxy = false, int port = 0, string address = "")
    {
        if (id == "server")
            m_IsServer = true;

        m_ProcessBuffer = new Queue<DataChunk>();
        m_SendBuffer = new Queue<SendChunk>();
        m_Endpoints = new Dictionary<string, IPEndPoint>();

        m_ReceiveThread = new Thread(new ThreadStart(ReceiveWorker));
        m_SendThread = new Thread(new ThreadStart(SendWorker));

        m_Running = true;

        if (client || proxy)
        {
            m_Client = new UdpClient();
            ELogger.Log($"Connecting to proxy/server at {address}:{port}", ELogger.LogType.Normal);
            m_Client.Connect(address, port);
            StartCoroutine(TryConnect(id));
        }
        else
        {
            try
            {
                ELogger.Log($"Establishing server on port {port}", ELogger.LogType.Server);
                m_Client = new UdpClient(port);
                m_NetworkState = NetworkState.Established;
                OnConnectionEstablished();
            }
            catch (SocketException)
            {
                OnConnectionError(NetworkError.CrowdedPort);
                return;
            }
        }
        m_ReceiveThread.Start();
        m_SendThread.Start();
        InvokeRepeating("ProcessWorker", 0, 0.001f);
    }

    /// <summary>
    /// A function that attempts to connect to a server if given the ip and port
    /// </summary>
    private IEnumerator TryConnect(string senderId)
    {
        while (m_NetworkState != NetworkState.Established && m_Tries != 5)
        {
            ELogger.Log($"Attempting to establish connection: [{m_Tries + 1}/5]", ELogger.LogType.Client);
            QueueFrame(new NetworkAuthenticationFrame("", senderId, "proxy"));

            m_Tries++;
            yield return new WaitForSeconds(2.5f);
        }

        if(m_NetworkState == NetworkState.Established)
        {
            ELogger.Log("Network connection established", ELogger.LogType.Client);
            OnConnectionEstablished();
        } else
        {
            OnConnectionError(NetworkError.ConnectionFailed);
        }
    }

    /// <summary>
    /// The worker in charge of receiving data and pushing it into a queue to be processed
    /// </summary>
    private void ReceiveWorker()
    {
        while(m_Running)
        {
            IPEndPoint remote = new IPEndPoint(0, 0);
            byte[] buffer = m_Client.Receive(ref remote);

            DataChunk chunk = new DataChunk(buffer, remote);
            m_ProcessBuffer.Enqueue(chunk);
            m_ReceivedFrames++;
        }

        Thread.CurrentThread.Abort();
    }

    /// <summary>
    /// The worker in charge of processing data and pushing it to other objects
    /// </summary>
    private void ProcessWorker()
    {
        if (m_ProcessBuffer.Count == 0)
            return;

        DataChunk chunk = m_ProcessBuffer.Dequeue();
        m_ProcessedFrames++;
        OnReceiveData(NetworkFrame.ReadFromBytes(chunk.m_Bytes), chunk.m_Endpoint, chunk.m_Bytes);
    }

    /// <summary>
    /// The worker in charge of sending data across the network
    /// </summary>
    private void SendWorker()
    {
        while(m_Running)
        {
            if (m_SendBuffer.Count == 0)
                continue;

            SendChunk chunk = m_SendBuffer.Dequeue();
            byte[] bytes = chunk.m_Data.ToBytes();

            if (chunk.m_Data.m_TargetId == "server" || chunk.m_Data.m_TargetId == "proxy")
            {
                m_Client.Send(bytes, bytes.Length);
            } else
            {
                if (!m_Endpoints.TryGetValue(chunk.m_Data.m_TargetId, out IPEndPoint point))
                    return;
                m_Client.Send(bytes, bytes.Length, point);
            }
            m_SentFrames++;
        }

        Thread.CurrentThread.Abort();
    }

    /// <summary>
    /// Returns how many chunks are waiting to be processed
    /// </summary>
    /// <returns></returns>
    public int GetProcessBufferCount()
        => m_ProcessBuffer.Count;

    /// <summary>
    /// Returns how many chunks of data are currently sitting in the send buffer
    /// </summary>
    /// <returns></returns>
    public int GetSendBufferCount()
        => m_SendBuffer.Count;

    /// <summary>
    /// Returns the amount of frames that have been sent over the connection
    /// </summary>
    public int GetSentFrames()
        => m_SentFrames;

    /// <summary>
    /// Returns the amount of frames that have been processed over the connection
    /// </summary>
    public int GetProcesedFrames()
        => m_ProcessedFrames;

    /// <summary>
    /// Returns the amount of frames that have been received over the connection
    /// </summary>
    public int GetReceivedFrames()
        => m_ReceivedFrames;

    public NetworkState GetNetworkState()
        => m_NetworkState;

    /// <summary>
    /// Queues up a frame to be sent across the network
    /// </summary>
    /// <param name="frame">The frame of data that is going to be sent</param>
    /// <param name="target">The target of whom this is going to, if left null then to the connected client</param>
    public void QueueFrame(NetworkFrame frame)
    {
        SendChunk chunk = new SendChunk(frame);
        m_SendBuffer.Enqueue(chunk);
    }

    /// <summary>
    /// Called when data is processed and sent across the client/server
    /// </summary>
    /// <param name="frame">The frame that was processed and sent to us</param>
    /// <param name="point">The ip address that sent this to us</param>
    /// <param name="bytes">The raw bytes of the frame, used to parse into bigger frames</param>
    public virtual void OnReceiveData(NetworkFrame frame, IPEndPoint point, byte[] bytes)
    {
        if (!m_Endpoints.ContainsKey(frame.m_SenderId))
            m_Endpoints.Add(frame.m_SenderId, point);

        if(m_NetworkState == NetworkState.Connecting)
            if (frame.m_SenderId == "proxy")
                m_NetworkState = NetworkState.EstablishedProxy;
            else if (frame.m_SenderId == "server")
                m_NetworkState = NetworkState.Established;
    }

    /// <summary>
    /// Called when the connection is being shutdown, right before anything happens
    /// </summary>
    public virtual void OnConnectionShutdown(NetworkShutdownType type, string reason)
    {

    }

    /// <summary>
    /// Called when either the server has been hosted, or a connection was established to a server
    /// </summary>
    public virtual void OnConnectionEstablished()
    {

    }

    /// <summary>
    /// Called when there is an error connecting to the server or hosting a server
    /// </summary>
    /// <param name="error">The error that was thrown</param>
    public virtual void OnConnectionError(NetworkError error)
    {
        if (error == NetworkError.ConnectionFailed)
            Debug.LogError("The connection to the server failed to establish");
        else if (error == NetworkError.CrowdedPort)
            Debug.LogError("The provided port is being used by another process");

        OnConnectionShutdown(NetworkShutdownType.Error, error.ToString());
        Destroy();
    }

    private class DataChunk
    {
        public byte[] m_Bytes;
        public IPEndPoint m_Endpoint;

        public DataChunk(byte[] bytes, IPEndPoint point)
        {
            m_Bytes = bytes;
            m_Endpoint = point;
        }
    }

    private class SendChunk
    {
        public NetworkFrame m_Data;

        public SendChunk(NetworkFrame frame)
            => m_Data = frame;
    }

    public enum NetworkError
    {
        ConnectionFailed,
        CrowdedPort
    }

    public enum NetworkShutdownType
    {
        Gracefully,
        Error
    }

    public enum NetworkState
    {
        Connecting,
        Established,
        EstablishedProxy,
        Errored
    }

    private void Destroy()
    {
        m_Running = false;
        CancelInvoke();
        StopAllCoroutines();
    }
}