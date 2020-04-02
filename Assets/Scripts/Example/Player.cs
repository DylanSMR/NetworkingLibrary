using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// For movement: https://github.com/jiankaiwang/FirstPersonController/tree/master/Assets/scripts
public class Player : NetworkBehaviour
{
    private NetworkIdentity m_Identity;
    private Camera m_Camera;

    public float m_Health = 100f;
    public Color m_Color;
    public bool m_Movement = true;

    public float speed = 100.0f;
    private float translation;
    private float straffe;

    private void Start()
    {
        m_Identity = GetComponent<NetworkIdentity>();
        m_Camera = GetComponentInChildren<Camera>();
        m_Camera.gameObject.SetActive(false);
        m_Color = GetComponentInChildren<Renderer>().material.GetColor("_BaseColor");
    }

    public void UpdateColor(Color color)
    {
        m_Color = color;
        GetComponentInChildren<Renderer>().material.SetColor("_BaseColor", color);
    }

    public override void OnAuthorityChanged(bool status)
    {
        base.OnAuthorityChanged(status);
        if (!m_HasAuthority)
            return;

        if(status)
        {
            m_Camera.gameObject.SetActive(true);

            StartCoroutine(SendTransformUpdate());
        }
    }

    private void OnGUI()
    {
        if(m_HasAuthority)
        {
            GUI.Label(new Rect(100, 100, 100, 100), $"Ping: {NetworkClient.Instance.m_LastPing}ms");
            GUI.Label(new Rect(100, 120, 100, 100), $"Health: {m_Health}");
        }
    }

    public void OnFireGun()
    {
        NetworkSpawnRPC spawnRPC = new NetworkSpawnRPC(0);
        NetworkClient.Instance.SendRPC(spawnRPC);
    }

    private void Update()
    {
        if(m_IsClient)
        {
            if (m_HasAuthority)
            {
                if(m_Movement)
                {
                    translation = Input.GetAxis("Vertical") * speed * Time.deltaTime;
                    straffe = Input.GetAxis("Horizontal") * speed * Time.deltaTime;
                    transform.Translate(straffe, 0, translation);
                }

                if (Input.GetKeyDown(KeyCode.F))
                {
                    m_Color = new Color(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
                    NetworkPlayerRPC rpc = new NetworkPlayerRPC(m_Color, -1, m_Identity.m_NetworkId); // Health is -1 as it doesnt matter we we set it, only what the server sets
                    NetworkClient.Instance.SendRPC(rpc);
                }

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    m_Movement = !m_Movement;
                }
            }
        } 
    }

    private IEnumerator SendTransformUpdate()
    {
        while(true)
        {
            NetworkTransformRPC rpc = new NetworkTransformRPC(transform, m_Identity.m_NetworkId);
            NetworkClient.Instance.SendRPC(rpc);

            yield return new WaitForSeconds(60 / 1000);
        }
    }

    /// <summary>
    /// An optional way to receive any extra RPC commands.
    /// Can be used to update things like health, active gun, animation (maybe done by library later), etc
    /// Note, all calls are sent from the server never another client. So things like health can be checked via the server and not here
    /// </summary>
    /// <param name="content">The content of the rpc we received. See example below to understand more</param>
    public override void OnRPCCommand(string content)
    {
        base.OnRPCCommand(content);
        NetworkRPC rpc = NetworkRPC.FromString(content);

        switch (rpc.m_Type)
        {
            case NetworkRPCType.RPC_CUSTOM_PLAYER:
                {
                    NetworkPlayerRPC playerRPC = NetworkRPC.Parse<NetworkPlayerRPC>(content);
                    UpdateColor(playerRPC.m_Color);
                    m_Health = playerRPC.m_Health;
                } break;
        }
    }
}
