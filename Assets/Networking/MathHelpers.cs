using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class MathHelpers
{
    public static float RoundFloat(float value, int digits)
    {
        float mult = Mathf.Pow(10.0f, (float)digits);
        return Mathf.Round(value * mult) / mult;
    }

    public static Vector3 RoundVector3(Vector3 vector, int digits = 5)
    {
        return new Vector3(
            RoundFloat(vector.x, digits),
            RoundFloat(vector.y, digits),
            RoundFloat(vector.z, digits)
        );
    }
}
