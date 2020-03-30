using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gun : MonoBehaviour
{
    private Player m_Player;

    private void Start()
    {
        m_Player = GetComponentInParent<Player>();
    }

    void Update()
    {
        if(m_Player.m_IsClient)
        {
            if(m_Player.m_HasAuthority)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    m_Player.OnFireGun();
                }
            }
        }
    }
}
