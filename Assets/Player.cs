﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : NetworkBehaviour
{
    public override void OnRPCCommand(string content)
    {
        NetworkRPC rpc = NetworkRPC.FromString(content);
        base.OnRPCCommand(content);

        // Handle player based rpc stuff here
    }
}