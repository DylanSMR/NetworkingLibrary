using System;
using System.Net;
using System.Text;
using UnityEngine;

[Serializable]
public class NetworkFrame 
{
    public NetworkFrameType m_Type;
    public int m_FrameId = -1;
    public string m_TargetId;
    public string m_SenderId;
    public bool m_Important;

    public NetworkFrame(NetworkFrameType m_Type, string m_SenderId, string m_TargetId)
    {
        this.m_Type = m_Type;
        this.m_TargetId = m_TargetId;
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
        if (frame == null)
            throw new Exception($"Error reading frame from bytes, {content} is not a valid {typeof(T)}!");
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

    public enum NetworkFrameType
    {
        Handshake,
        Ping,
        Authentication,
        RPC,
        Heartbeat,
        Acknowledged
    }
}
