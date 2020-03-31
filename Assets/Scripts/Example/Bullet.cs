using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class Bullet : NetworkBehaviour
{
    private Rigidbody m_Body;

    public float m_Speed = 10f;
    private float m_KillAt = 0f;

    private NetworkIdentity m_Identity;

    private void Start()
    {
        m_Identity = GetComponent<NetworkIdentity>();
        m_Body = GetComponent<Rigidbody>();

        if (m_IsServer)
        {
            GameObject obj = NetworkManager.Instance.GetNetworkedObject(m_Spawner);
            if (obj == null)
            {
                NetworkServer.Instance.DestroyNetworkedObject(m_Identity.m_NetworkId);
                return;
            }

            Gun gun = obj.GetComponentInChildren<Gun>();
            transform.position = gun.transform.position + (gun.transform.forward / 2.5f);
            transform.rotation = gun.transform.rotation;
            m_KillAt = Time.time + 3f;

            NetworkTransformRPC rpc = new NetworkTransformRPC(transform, m_Identity.m_NetworkId);
            NetworkServer.Instance.SendRPCAll(rpc);
        }
    }

    private void Update()
    {
        if (m_IsServer)
        {
            if (Time.time > m_KillAt)
            {
                NetworkServer.Instance.DestroyNetworkedObject(m_Identity.m_NetworkId);
                return;
            }
        }

        // No matter who it is do this, we dont bother updating transform since the server handles collisions
        // So if a client moves this bullet only they will see it. This should save on server perf
        m_Body.velocity += transform.forward * Time.deltaTime * m_Speed;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (m_IsServer)
        {
            // If a client calls this their game will probably just crash since NetworkServer.Instance isnt a thing

            GameObject collided = collision.gameObject;
            Player player = collided.GetComponentInParent<Player>();
            if (player == null)
            {
                NetworkServer.Instance.DestroyNetworkedObject(m_Identity.m_NetworkId);
                return;
            }

            player.m_Health -= 25;

            NetworkPlayerRPC rpc = new NetworkPlayerRPC(player.m_Color, player.m_Health, m_Identity.m_NetworkId);
            rpc.m_Important = true;
            NetworkServer.Instance.SendRPCAll(rpc);
            NetworkServer.Instance.DestroyNetworkedObject(m_Identity.m_NetworkId);
        }

        if(m_IsClient)
        {
            // Spawn hit marker, bullet effect, etc
        }
    }
}
