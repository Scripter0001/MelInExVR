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

public static class MelInExShared
{
    public const string MelonLoaderURL = "https://github.com/LavaGang/MelonLoader/releases/latest/download/MelonLoader.x64.zip";
    public const string BepInExURL = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip";
    
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
        string mlPath = Path.GetFullPath("./");
        // Load Dependencies
        Assembly.LoadFrom(Path.Combine(mlPath, "MelonLoader/net35/MonoMod.ILHelpers.dll"));
        Assembly.LoadFrom(Path.Combine(mlPath, "MelonLoader/net35/MonoMod.Backports.dll"));
        Assembly.LoadFrom(Path.Combine(mlPath, "MelonLoader/net35/bHapticsLib.dll"));
        Assembly.LoadFrom(Path.Combine(mlPath, "MelonLoader/net35/AssetRipper.Primitives.dll"));
        Assembly.LoadFrom(Path.Combine(mlPath, "MelonLoader/net35/Tomlet.dll"));
        Assembly.LoadFrom(Path.Combine(mlPath, "MelonLoader/net35/AssetsTools.NET.dll"));
        // Load ML
        Assembly.LoadFrom(Path.Combine(mlPath, "MelonLoader/net35/MelonLoader.dll"));
        // Setup Config
        Type loaderConfig = AccessTools.TypeByName("LoaderConfig");
        Type coreConfig = AccessTools.TypeByName("CoreConfig");
        object CurrentConfig = AccessTools.Property(loaderConfig, "Current").GetValue(null);
        object CurrentLoaderConfig = AccessTools.Property(loaderConfig, "Loader").GetValue(CurrentConfig);
        AccessTools.Property(coreConfig, "BaseDirectory").SetValue(CurrentLoaderConfig, mlPath);
        // Setup Mods Directories
        Type melonHandler = AccessTools.TypeByName("MelonHandler");
        AccessTools.Method(melonHandler, "Setup").Invoke(null, null);
        // create dummy loggers
        Type bootstrapInterop = AccessTools.TypeByName("BootstrapInterop");
        Type bootstrapLibrary = AccessTools.TypeByName("BootstrapLibrary");
        Type logError = AccessTools.TypeByName("LogErrorFn");
        Type logMelonInfo = AccessTools.TypeByName("LogMelonInfoFn");
        Type logMsg = AccessTools.TypeByName("LogMsgFn");
        object library = Activator.CreateInstance(bootstrapLibrary);
        AccessTools.Property(bootstrapInterop, "Library").SetValue(null, library);

        Delegate logErrorDelegate =
            DelegateHandler.CreateForwarder(typeof(BepInExMelonLogger), logError, "LogError");
        
        Delegate logMsgDelegate =
            DelegateHandler.CreateForwarder(typeof(BepInExMelonLogger), logMsg, "LogMsg");
        
        Delegate logMelonDelegate =
            DelegateHandler.CreateForwarder(typeof(BepInExMelonLogger), logMelonInfo, "LogMelonInfo");
        
        AccessTools.Property(bootstrapLibrary, "LogError").SetValue(library, logErrorDelegate);
        AccessTools.Property(bootstrapLibrary, "LogMelonInfo").SetValue(library, logMelonDelegate);
        AccessTools.Property(bootstrapLibrary, "LogMsg").SetValue(library, logMsgDelegate);
        // recreate core setup since not everything is here.
        // Misc initialization
        AccessTools.Method(AccessTools.TypeByName("MelonLaunchOptions"), "Load").Invoke(null, null);
        AccessTools.Method(AccessTools.TypeByName("MelonUtils"), "SetupWineCheck").Invoke(null, null);
        AccessTools.Method(AccessTools.TypeByName("MelonLoader.Pastel.ConsoleExtensions"), "Disable").Invoke(null, null); // Usually you'd check if environment is wine before running this but BIE doesn't support Pastel anyways.
        AccessTools.Method(AccessTools.TypeByName("UnhandledException"), "Install").Invoke(null, [AppDomain.CurrentDomain]);
        AccessTools.Method(AccessTools.TypeByName("ServerCertificateValidation"), "Install").Invoke(null, null);
        AccessTools.Method(AccessTools.TypeByName("LemonAssertMapping"), "Setup").Invoke(null, null);
        AccessTools.Method(AccessTools.TypeByName("HarmonyLogger"), "Setup").Invoke(null, null);
        AccessTools.Field(AccessTools.TypeByName("MelonLoader.Core"), "HarmonyInstance").SetValue(null, new HarmonyLib.Harmony("MelonLoader"));
        AccessTools.Method(AccessTools.TypeByName("MelonUtils"), "Setup").Invoke(null, [AppDomain.CurrentDomain]);
        // TODO: Hijack BepInEx's assembly events and execute harmony's MelonAssemblyResolver.OnAssemblyLoad
        // Skipping SetDefaultConsoleTitleWithGameName since BIE already did this
        // Eh, I'm sure we don't need MonoLibrary
        AccessTools.Method(AccessTools.TypeByName("DetourContextDisposeFix"), "Install").Invoke(null, null);
        AccessTools.Method(AccessTools.TypeByName("ForcedCultureInfo"), "Install").Invoke(null, null);
        AccessTools.Method(AccessTools.TypeByName("InstancePatchFix"), "Install").Invoke(null, null);
        // This is a windows only mod
        AccessTools.Method(AccessTools.TypeByName("ProcessFix"), "Install").Invoke(null, null);
        AccessTools.Method(AccessTools.TypeByName("PatchShield"), "Install").Invoke(null, null);
        AccessTools.Method(AccessTools.TypeByName("MelonPreferences"), "Load").Invoke(null, null);
        AccessTools.Method(AccessTools.TypeByName("MelonCompatibilityLayer"), "LoadModules").Invoke(null, null);
        // Load plugins n' shit
        Type melonFolderHandler = AccessTools.TypeByName("MelonLoader.Melons.MelonFolderHandler");
        Type melonFolderHandlerScanType = AccessTools.TypeByName("MelonLoader.Melons.MelonFolderHandler+ScanType");
        AccessTools.Method(melonFolderHandler, "ScanForFolders").Invoke(null, null);
        MethodInfo loadMelons = AccessTools.GetDeclaredMethods(melonFolderHandler).First(x => x.Name == "LoadMelons" && x.ReturnType == typeof(void));
        loadMelons.Invoke(null, [Enum.Parse(melonFolderHandlerScanType, "UserLibs")]);
        loadMelons.Invoke(null, [Enum.Parse(melonFolderHandlerScanType, "Plugins")]);
        Type melonEvent = AccessTools.TypeByName("MelonEvent");
        Type melonEvents = AccessTools.TypeByName("MelonEvents");
        MethodInfo melonEventInvoke = AccessTools.Method(melonEvent, "Invoke");
        // Call events
        melonEventInvoke.Invoke(AccessTools.Field(melonEvents, "MelonHarmonyEarlyInit").GetValue(null), null);
        melonEventInvoke.Invoke(AccessTools.Field(melonEvents, "OnPreInitialization").GetValue(null), null);
        
        // We're already in the scene, so do the Start functionality
        melonEventInvoke.Invoke(AccessTools.Field(melonEvents, "OnApplicationEarlyStart").GetValue(null), null);
        melonEventInvoke.Invoke(AccessTools.Field(melonEvents, "OnPreModsLoaded").GetValue(null), null);
        loadMelons.Invoke(null, [Enum.Parse(melonFolderHandlerScanType, "Mods")]);
        melonEventInvoke.Invoke(AccessTools.Field(melonEvents, "OnPreSupportModule").GetValue(null), null);
        AccessTools.Method(AccessTools.TypeByName("SupportModule"), "Setup").Invoke(null, null);
        AccessTools.Method(AccessTools.TypeByName("MelonLoader.Core"), "AddUnityDebugLog").Invoke(null, null);
        melonEventInvoke.Invoke(AccessTools.Field(melonEvents, "MelonHarmonyInit").GetValue(null), null);
        melonEventInvoke.Invoke(AccessTools.Field(melonEvents, "OnApplicationStart").GetValue(null), null);
    }
}

// MelonLoader
public class MelOnEx : MelonPlugin
{
    private Assembly biePreloader;
    
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
        biePreloader = Assembly.LoadFrom(Path.Combine(biePath, "core/BepInEx.Preloader.dll")); // not a dep but needed to execute some functions that are supposed to run before bepinex inits
        Console.WriteLine(biePreloader);
        // Load BepInEx
        Assembly.LoadFrom(Path.Combine(biePath, "core/BepInEx.dll"));
        Assembly.LoadFrom(Path.Combine(biePath, "core/BepInEx.Harmony.dll"));
        AccessTools.Method(AccessTools.TypeByName("EnvVars"), "LoadVars").Invoke(null, null);
        AccessTools.Method(AccessTools.TypeByName("Paths"), "SetExecutablePath").Invoke(null,
        [
            Environment.GetEnvironmentVariable("DOORSTOP_PROCESS_PATH"),
            Path.Combine(Environment.GetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH"), "../", "../"),
            Environment.GetEnvironmentVariable("DOORSTOP_MANAGED_FOLDER_DIR"),
            new[] {Environment.GetEnvironmentVariable("DOORSTOP_DLL_SEARCH_DIRS")}
        ]);
        AccessTools.Method(AccessTools.TypeByName("Preloader"), "Run").Invoke(null, null);
        AccessTools.Method(AccessTools.TypeByName("Chainloader"), "Initialize").Invoke(null, [Environment.GetEnvironmentVariable("DOORSTOP_PROCESS_PATH"), false, null]);
        AccessTools.Method(AccessTools.TypeByName("Chainloader"), "Start").Invoke(null, null);
    }
}