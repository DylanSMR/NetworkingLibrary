using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Creates a networked frame to send to the server (m_TargetId will be filled in automatically)
    /// </summary>
    /// <param name="type">The type of frame sent to the server</param>
    /// <param name="m_SenderId">The ID of the client who is sending this frame</param>
    public NetworkFrame(NetworkFrameType type, string m_SenderId)
    {
        this.m_SenderId = m_SenderId;
    }

    /// <summary>
    /// Creates a networked frame to sent to a specific client identified by m_TargetId (m_SenderId will be filled in automatically)
    /// </summary>
    /// <param name="type">The type of frame sent to the client</param>
    /// <param name="m_TargetId">The ID of the client who is receiving this frame</param>
    /// <param name="m_SenderId">The ID of the server, generally should be left to default</param>
    public NetworkFrame(NetworkFrameType type, string m_TargetId, string m_SenderId = "server")
    {
        this.m_SenderId = m_SenderId;
    }

    public byte[] ToBytes()
    {
        string content = JsonUtility.ToJson(this);
        return Encoding.ASCII.GetBytes(content);
    }

    public static T Parse<T>(byte[] bytes)
    {
        string content = Encoding.ASCII.GetString(bytes);
        T frame = JsonUtility.FromJson<T>(content);
        return frame;
    }

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
        RPC
    }
}
