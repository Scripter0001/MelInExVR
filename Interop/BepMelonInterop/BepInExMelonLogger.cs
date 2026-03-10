using System;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace MelInEx;

public class BepInExMelonLogger
{
    public static void LogMsg(IntPtr msgColor, string msg, int msgLength, IntPtr sectionColor, string section,
        int sectionLength, string strippedMSg, int strippedMsgLength)
    {
        Logger.CreateLogSource(section).LogMessage(msg);
    }
    
    public static void LogError(string msg, int msgLength, string section, int sectionLength, bool warning)
    {
        Logger.CreateLogSource(section).LogError(msg);
    }

    public static void LogMelonInfo(IntPtr nameColor, string name, int nameLength, string info, int infoLength)
    {
        Debug.Log("LogMelonInfo not implemented.");
    }
}