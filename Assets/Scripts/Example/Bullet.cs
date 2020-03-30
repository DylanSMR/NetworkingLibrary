using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class Bullet : NetworkBehaviour
{
    private Player m_player;
    private Rigidbody m_Body;
    public float m_Speed = 10f;
    public float m_Lifetime = 3f;
    private float m_Started = 0f;

    private void Start()
    {
        if (!m_IsServer)
            return;

        GameObject obj = NetworkManager.Instance.GetNetworkedObject(m_Spawner);
        if (obj == null)
        {
            return;
        }
        m_player = obj.GetComponent<Player>();
        if (m_player == null)
        {
            NetworkServer.Instance.DestroyObject(GetComponent<NetworkIdentity>().m_NetworkId);
            return;
        }

        m_Body = GetComponent<Rigidbody>();
        if (m_Body == null)
        {
            NetworkServer.Instance.DestroyObject(GetComponent<NetworkIdentity>().m_NetworkId);
            return;
        }

        Gun gun = obj.GetComponentInChildren<Gun>();
        transform.position = (gun.transform.position + ( gun.transform.forward / 2.5f ));
        transform.rotation = gun.transform.rotation;
        m_Started = Time.time + m_Lifetime;
        UpdateTransform();
    }

    private void Update()
    {
        if (!m_IsServer)
            return;

        if(Time.time > m_Started)
        {
            NetworkServer.Instance.DestroyObject(GetComponent<NetworkIdentity>().m_NetworkId);
            return;
        }

        m_Body.velocity += transform.forward * Time.deltaTime * m_Speed;
    }

    private IEnumerator UpdateTransform()
    {
        while(true)
        {
            NetworkTransformRPC rpc = new NetworkTransformRPC(transform, GetComponent<NetworkIdentity>().m_NetworkId);
            NetworkServer.Instance.SendRPCAll(rpc);

            yield return new WaitForSeconds(60 / 1000);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!m_IsServer)
            return;

        if (NetworkServer.Instance == null) // If the check above didnt work, if we dont have a network server quit
            return;

        GameObject collided = collision.gameObject;
        if (collision == null)
        {
            NetworkServer.Instance.DestroyObject(GetComponent<NetworkIdentity>().m_NetworkId);
            return;
        }

        Player player;
        if ((player = collided.GetComponentInParent<Player>()) == null)
        {
            NetworkServer.Instance.DestroyObject(GetComponent<NetworkIdentity>().m_NetworkId);
            return;
        }
        player.m_Health -= 25;

        NetworkPlayerRPC rpc = new NetworkPlayerRPC(player.m_Color, player.m_Health, player.GetComponent<NetworkIdentity>().m_NetworkId); ; // Health is -1 as it doesnt matter we we set it, only what the server sets
        NetworkServer.Instance.SendRPCAll(rpc);
        NetworkServer.Instance.DestroyObject(GetComponent<NetworkIdentity>().m_NetworkId);
    }
}
