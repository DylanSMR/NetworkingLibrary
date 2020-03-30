using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class ELogger
{
    public class Colorizer
    {
        private static string prefix = "<b><color=#{0}>";
        private static string suffix = "</color></b>";
        
        public static string Colorize(string str, Color col)
            => Colorize(str, ColorUtility.ToHtmlStringRGB(col));

        public static string Colorize(string str, string col)
        {
            return $"{string.Format(prefix, col)}{str}{suffix}";
        }
    }

    public static void Log(string message, LogType type, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
    {
        string fileName = Path.GetFileName(sourceFilePath);

        switch(type)
        {
            case LogType.Client:
                {
                    Debug.Log($"[{Colorizer.Colorize("Client", Color.cyan)}][{memberName}:{fileName}:{sourceLineNumber}] {message}");
                } break;
            case LogType.Server:
                {
                    Debug.unityLogger.Log($"[{Colorizer.Colorize("Server", Color.green)}][{memberName}:{fileName}:{sourceLineNumber}] {message}");
                } break;
            case LogType.Normal:
                {
                    Debug.Log($"[{Colorizer.Colorize("Normal", Color.black)}][{memberName}:{sourceFilePath}:{fileName}] {message}");
                }
                break;
        }
    }

    public enum LogType
    {
        Server,
        Client,
        Normal
    }
}