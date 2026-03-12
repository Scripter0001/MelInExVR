using System;
using BepInEx.Logging;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace MelInEx;

public class BepInExMelonLogger
{
    public static ManualLogSource MelonLoaderLogger = Logger.CreateLogSource("MelonLoader");
    
    public static void LogMsg(IntPtr msgColor, string msg, int msgLength, IntPtr sectionColor, string section,
        int sectionLength, string strippedMSg, int strippedMsgLength)
    {
        MelonLoaderLogger.LogMessage(msg);
    }
    
    public static void LogError(string msg, int msgLength, string section, int sectionLength, bool warning)
    {
        MelonLoaderLogger.LogError(msg);
    }

    public static void LogMelonInfo(IntPtr nameColor, string name, int nameLength, string info, int infoLength)
    {
        MelonLoaderLogger.LogMessage(name + " " + info);
    }
}