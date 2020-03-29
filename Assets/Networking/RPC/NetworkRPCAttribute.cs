using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class NetworkRPCAttribute : Attribute
{
    public string identifier;

    public NetworkRPCAttribute(string id)
    {
        this.identifier = id;
    }
}
