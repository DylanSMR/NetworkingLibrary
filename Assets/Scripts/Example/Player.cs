using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// For movement: https://github.com/jiankaiwang/FirstPersonController/tree/master/Assets/scripts
public class Player : NetworkBehaviour
{
    private NetworkIdentity m_Identity;
    private Camera camera;

    public float speed = 100.0f;
    private float translation;
    private float straffe;

    private void Start()
    {
        m_Identity = GetComponent<NetworkIdentity>();
        camera = GetComponentInChildren<Camera>();
        camera.gameObject.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
    }

    public override void OnAuthorityChanged(bool status)
    {
        base.OnAuthorityChanged(status);

        if(status)
            camera.gameObject.SetActive(true);
    }

    private void OnGUI()
    {
        if(m_HasAuthority)
        {
            GUI.Label(new Rect(100, 100, 100, 100), $"Ping: {NetworkClient.Instance.m_LastPing}ms");
        }
    }

    private void Update()
    {
        if(m_IsClient)
        {
            if (m_HasAuthority)
            {
                translation = Input.GetAxis("Vertical") * speed * Time.deltaTime;
                straffe = Input.GetAxis("Horizontal") * speed * Time.deltaTime;
                transform.Translate(straffe, 0, translation);

                if (Input.GetKeyDown(KeyCode.F))
                {
                    Color c = new Color(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
                    NetworkPlayerRPC rpc = new NetworkPlayerRPC(c, m_Identity.m_NetworkId);
                    NetworkClient.Instance.SendRPC(rpc);
                }

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    Cursor.lockState = CursorLockMode.None;
                }
            }
        } 
    }

    private void FixedUpdate()
    {
        if (!m_HasAuthority)
            return;

        NetworkTransformRPC rpc = new NetworkTransformRPC(transform, m_Identity.m_NetworkId);
        NetworkClient.Instance.SendRPC(rpc);
    }

    /// <summary>
    /// An optional way to receive any extra RPC commands.
    /// Can be used to update things like health, active gun, animation (maybe done by library later), etc
    /// </summary>
    /// <param name="content">The content of the rpc we received. See example below to understand more</param>
    public override void OnRPCCommand(string content)
    {
        NetworkRPC rpc = NetworkRPC.FromString(content);
        base.OnRPCCommand(content);

        switch (rpc.m_Type)
        {
            case NetworkRPCType.RPC_CUSTOM_TRANSFORM:
                {
                    NetworkTransformRPC transformRPC = NetworkRPC.Parse<NetworkTransformRPC>(content);
                    transform.position = transformRPC.m_Position;
                    transform.eulerAngles = transformRPC.m_Rotation;
                    transform.localScale = transformRPC.m_Scale;
                } break;
            case NetworkRPCType.RPC_CUSTOM_PLAYER:
                {
                    NetworkPlayerRPC playerRPC = NetworkRPC.Parse<NetworkPlayerRPC>(content);
                    GetComponentInChildren<Renderer>().material.SetColor("_BaseColor", playerRPC.m_Color);
                } break;
        }
    }
}
