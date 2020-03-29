using System;
using System.Net;
using System.Text;
using UnityEngine;

/// <summary>
/// The NetworkFrame is a method used to communicate across multiple devices in a network
/// Frames are generally extended on to create bigger frames, allowing for more precise update logic
/// </summary>
[Serializable]
public class NetworkFrame 
{
    /// <summary>
    /// The type of request that this frame represents
    /// </summary>
    public NetworkFrameType m_Type;
    /// <summary>
    /// The unique identifier of the target this represents (for now HWID, but later a generated ID such as steams)
    /// </summary>
    public string m_TargetId;
    /// <summary>
    /// The unique identifier of whoever is sending this frame
    /// </summary>
    public string m_SenderId;
    /// <summary>
    /// The IP address of the target
    /// </summary>
    public string m_TargetAddress;
    /// <summary>
    /// The IP address of the sender
    /// </summary>
    public string m_SenderAddress;

    /// <summary>
    /// Configure the frame for the server to send to a client
    /// </summary>
    /// <param name="frame">The frame that is going to be configured</param>
    public void Configure(NetworkFrame frame)
    {
        m_TargetAddress = frame.m_SenderAddress;
        m_SenderAddress = "0.0.0.0:0000";
    }

    public IPEndPoint GetTargetEndpoint()
    {
        if (m_TargetAddress == "")
            return null;

        string[] split = m_TargetAddress.Split(':');
        if (split.Length != 2)
            return null;

        return new IPEndPoint(IPAddress.Parse(split[0]), int.Parse(split[1]));
    }

    public IPEndPoint GetSenderEndpoint()
    {
        if (m_TargetAddress == "")
            return null;

        string[] split = m_SenderAddress.Split(':');
        if (split.Length != 2)
            return null;

        return new IPEndPoint(IPAddress.Parse(split[0]), int.Parse(split[1]));
    }

    /// <summary>
    /// Creates a networked frame to send to the server (m_TargetId will be filled in automatically)
    /// </summary>
    /// <param name="type">The type of frame sent to the server</param>
    /// <param name="m_SenderId">The ID of the client who is sending this frame</param>
    public NetworkFrame(NetworkFrameType type, string m_SenderId)
    {
        this.m_SenderId = m_SenderId;
        this.m_Type = type;
    }

    /// <summary>
    /// Creates a networked frame to sent to a specific client identified by m_TargetId (m_SenderId will be filled in automatically)
    /// </summary>
    /// <param name="type">The type of frame sent to the client</param>
    /// <param name="m_TargetId">The ID of the client who is receiving this frame</param>
    /// <param name="m_SenderId">The ID of the server, generally should be left to default</param>
    public NetworkFrame(NetworkFrameType type, string m_TargetId, string m_SenderId = "server")
    {
        this.m_Type = type;
        this.m_TargetId = m_TargetId;
        this.m_SenderId = m_SenderId; 
    }
    
    /// <summary>
    /// Turns the network frame into a array of bytes
    /// </summary>
    /// <returns>An array of bytes representing the network frame</returns>
    public byte[] ToBytes()
    {
        string content = JsonUtility.ToJson(this);
        return Encoding.ASCII.GetBytes(content);
    }

    /// <summary>
    /// Turns an array of bytes into a generic type
    /// </summary>
    /// <typeparam name="T">The generic type you are wanting to parse into</typeparam>
    /// <param name="bytes">The array of bytes containing that generic type</param>
    /// <returns>The generic type filled with the specified data</returns>
    /// <exception cref="Exception">Thrown if the bytes did not contain the generic type</exception>
    public static T Parse<T>(byte[] bytes)
    {
        string content = Encoding.ASCII.GetString(bytes);
        T frame = JsonUtility.FromJson<T>(content);
        if (frame == null)
            throw new Exception($"Error reading frame from bytes, {content} is not a valid {typeof(T)}!");
        return frame;
    }

    /// <summary>
    /// Reads a network frame from an array of bytes
    /// </summary>
    /// <param name="bytes">An array of bytes containing a network frame</param>
    /// <returns>A network frames</returns>
    /// <exception cref="Exception">Thrown if the bytes did not contain a frame</exception>
    public static NetworkFrame ReadFromBytes(byte[] bytes)
    {
        string content = Encoding.ASCII.GetString(bytes);
        NetworkFrame frame = JsonUtility.FromJson<NetworkFrame>(content);
        if (frame == null)
            throw new Exception($"Error reading frame from bytes, {content} is not a valid NetworkFrame!");

        return frame;
    }

    /// <summary>
    /// An enum that represents what time of frame is being sent over the network
    /// </summary>
    public enum NetworkFrameType
    {
        Handshake,
        Ping,
        Authentication,
        RPC
    }
}
