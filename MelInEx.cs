using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using MelInEx;
using MelInEx.Interop;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(MelOnEx), MelInExInfo.Name, MelInExInfo.Version, MelInExInfo.Author)]
[assembly: MelonOptionalDependencies("BepInEx")]

namespace MelInEx;

// This should NEVER use any particular class from either loader
public static class MelInExShared
{
    public const string MelonLoaderURL = "https://github.com/LavaGang/MelonLoader/releases/latest/download/MelonLoader.x64.zip";
    public const string BepInExURL = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip";
    public static readonly string MlPath = Path.GetFullPath("./");
    public static Assembly ml;
    
    public static void InstallModLoader(string URL, string folderToLoad)
    {
        new Material(new Material(Shader.Find("UI/Default")));
        // Don't Install if it's already installed
        if (Directory.Exists("./" + folderToLoad))
            return;
        if (!Directory.Exists("./Temp"))
            Directory.CreateDirectory("./Temp");
        Stream modLoader = Stream.Synchronized(WebRequest.Create(URL).GetResponse().GetResponseStream());
        ZipArchive zip = new ZipArchive(modLoader);
        modLoader.Dispose();
        zip.ExtractToDirectory("./Temp/ModLoader");
        Directory.Move("./Temp/ModLoader/" + folderToLoad, "./" + folderToLoad);
        Directory.Delete("./Temp", true);
    }

    public static string GetProductName()
    {
        foreach (string directory in Directory.GetDirectories(Path.GetFullPath("./")))
        {
            if (directory.EndsWith("_Data"))
            {
                return directory.Replace(Path.GetFullPath("./"), string.Empty).Replace("_Data", string.Empty);
            }
        }
        throw new DirectoryNotFoundException("Could not find data directory, is this a Unity Mono game?");
    }
}

// BepInEx
[BepInPlugin(MelInExInfo.GUID, MelInExInfo.Name, MelInExInfo.Version)]
public class BelInEx : BaseUnityPlugin
{
    private void Awake()
    {
        MelInExShared.InstallModLoader(MelInExShared.MelonLoaderURL, "MelonLoader");
        
        // Load dependencies
        Assembly.LoadFrom(Path.Combine(MelInExShared.MlPath, "MelonLoader/net35/MonoMod.ILHelpers.dll"));
        Assembly.LoadFrom(Path.Combine(MelInExShared.MlPath, "MelonLoader/net35/MonoMod.Backports.dll"));
        Assembly.LoadFrom(Path.Combine(MelInExShared.MlPath, "MelonLoader/net35/bHapticsLib.dll"));
        Assembly.LoadFrom(Path.Combine(MelInExShared.MlPath, "MelonLoader/net35/AssetRipper.Primitives.dll"));
        Assembly.LoadFrom(Path.Combine(MelInExShared.MlPath, "MelonLoader/net35/Tomlet.dll"));
        Assembly.LoadFrom(Path.Combine(MelInExShared.MlPath, "MelonLoader/net35/AssetsTools.NET.dll"));
        // Load ML (assembly is used EVERYWHERE in the shims so it's assigned to the shared class)
        MelInExShared.ml = Assembly.LoadFrom(Path.Combine(MelInExShared.MlPath, "MelonLoader/net35/MelonLoader.dll"));
        
        // initialize native shims
        Type bootstrapLibrary = AccessTools.TypeByName("BootstrapLibrary");
        
        // get delegate types
        Type logError = AccessTools.TypeByName("LogErrorFn");
        Type logMelonInfo = AccessTools.TypeByName("LogMelonInfoFn");
        Type logMsg = AccessTools.TypeByName("LogMsgFn");
        Type loaderConfig = AccessTools.TypeByName("GetLoaderConfigFn");
        Type installHooks = AccessTools.TypeByName("ActionFn");
        Type loadMono = AccessTools.TypeByName("PtrRetFn");

        // Create forwarders for these delegates
        Type nativeFunctions = typeof(MelonLoaderNativeShims);
        
        Delegate logErrorDelegate =
            DelegateHandler.CreateForwarder(nativeFunctions, logError, "LogError");
        Delegate logMsgDelegate =
            DelegateHandler.CreateForwarder(nativeFunctions, logMsg, "LogMsg");
        Delegate logMelonDelegate =
            DelegateHandler.CreateForwarder(nativeFunctions, logMelonInfo, "LogMelonInfo");
        Delegate loaderConfigDelegate =
            DelegateHandler.CreateForwarder(nativeFunctions, loaderConfig, "GetLoaderConfig");
        Delegate installHooksDelegate =
            DelegateHandler.CreateForwarder(nativeFunctions, installHooks, "MonoInstallHooks");
        Delegate loadMonoDelegate =
            DelegateHandler.CreateForwarder(nativeFunctions, loadMono, "MonoGetRuntimeHandle");
        
        // create and assign new library object
        object library = Activator.CreateInstance(bootstrapLibrary);
        
        AccessTools.Property(AccessTools.TypeByName("BootstrapInterop"), "Library").SetValue(null, library);

        // assign shims to library
        AccessTools.Property(bootstrapLibrary, "LogError").SetValue(library, logErrorDelegate);
        AccessTools.Property(bootstrapLibrary, "LogMelonInfo").SetValue(library, logMelonDelegate);
        AccessTools.Property(bootstrapLibrary, "LogMsg").SetValue(library, logMsgDelegate);
        AccessTools.Property(bootstrapLibrary, "GetLoaderConfig").SetValue(library, loaderConfigDelegate);
        AccessTools.Property(bootstrapLibrary, "MonoInstallHooks").SetValue(library, installHooksDelegate);
        AccessTools.Property(bootstrapLibrary, "MonoGetRuntimeHandle").SetValue(library, loadMonoDelegate);

        // initialize MelonLoader
        Type core = AccessTools.TypeByName("MelonLoader.Core");
        
        AccessTools.Method(core, "Initialize").Invoke(null, null);
        AccessTools.Method(core, "Start").Invoke(null, null);
    }
}

// MelonLoader
public class MelOnEx : MelonPlugin
{
    public override void OnApplicationEarlyStart()
    {
        MelInExShared.InstallModLoader(MelInExShared.BepInExURL, "BepInEx");
        
        string gameName = MelInExShared.GetProductName();
        Environment.SetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH", Path.GetFullPath("./BepInEx/core/BepInEx.Preloader.dll"));
        Environment.SetEnvironmentVariable("DOORSTOP_MANAGED_FOLDER_DIR", Path.GetFullPath("./" + gameName + "_Data/Managed"));
        Environment.SetEnvironmentVariable("DOORSTOP_PROCESS_PATH", Path.GetFullPath("./" + gameName + ".exe"));
        Environment.SetEnvironmentVariable("DOORSTOP_DLL_SEARCH_DIRS", Path.GetFullPath("./" + gameName + "_Data/Managed"));
        // Load Dependencies
        string biePath = Path.GetFullPath("./BepInEx");
        Assembly.LoadFrom(Path.Combine(biePath, "core/0Harmony.dll"));
        Assembly.LoadFrom(Path.Combine(biePath, "core/HarmonyXInterop.dll"));
        // Load BepInEx
        Assembly.LoadFrom(Path.Combine(biePath, "core/BepInEx.Preloader.dll"));
        Assembly.LoadFrom(Path.Combine(biePath, "core/BepInEx.dll"));
        // Hack fix that I REALLY shouldn't do, but it works so whatever
        AccessTools.Field(AccessTools.TypeByName("MonoMod.Utils.PlatformHelper"), "_currentLocked").SetValue(null, false);
        AccessTools.Method(AccessTools.TypeByName("Doorstop.Entrypoint"), "Start").Invoke(null, null);
        // We're already past the chainloader initialization stage so manually call the chainloader
        AccessTools.Method(AccessTools.TypeByName("Chainloader"), "Initialize").Invoke(null, [Environment.GetEnvironmentVariable("DOORSTOP_PROCESS_PATH"), false, null]);
        AccessTools.Method(AccessTools.TypeByName("Chainloader"), "Start").Invoke(null, null);
    }
}