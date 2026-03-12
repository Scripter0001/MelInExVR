using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace MelInEx;

public class MelonLoaderNativeShims
{
    // most of this is from https://github.com/Smethan/BepInEx.MelonLoader.Loader/blob/master/Shared/BootstrapShim.cs
    // without it I don't think I'd ever get this working
    public static ManualLogSource MelonLoaderLogger = Logger.CreateLogSource("MelonLoader");
    private static ResolveEventHandler _monoResolveHandler;
    private static Func<string, Assembly> _monoSearchDirectoryScan;
    private static IntPtr _monoRuntimeHandle;
    
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

    public static void GetLoaderConfig(ref object config)
    {
        // Set BaseDirectory so all MelonEnvironment paths work
        object coreConfig = AccessTools.Property(config.GetType(), "Loader").GetValue(config);
        AccessTools.Property(coreConfig.GetType(), "BaseDirectory").SetValue(coreConfig, MelInExShared.MlPath);
        // we don't want to set the title since BIE already does
        object consoleConfig = AccessTools.Property(config.GetType(), "Console").GetValue(config);
        AccessTools.Property(consoleConfig.GetType(), "DontSetTitle").SetValue(consoleConfig, true);
    }

    // rest of this is from MelonLoader.Loader
    
    public static IntPtr MonoGetRuntimeHandle()
    {
        if (_monoRuntimeHandle != IntPtr.Zero)
            return _monoRuntimeHandle;

        foreach (var candidate in EnumerateMonoLibraryCandidates())
        {
            var handle = LoadMonoLibrary(candidate);
            if (handle != IntPtr.Zero)
            {
                _monoRuntimeHandle = handle;
                break;
            }
        }

        if (_monoRuntimeHandle == IntPtr.Zero)
            Debug.Log("Failed to locate the Mono runtime library. Mono-based titles may not function correctly.");

        return _monoRuntimeHandle;
    }
    
    private static IntPtr LoadMonoLibrary(string path)
    {
        try
        {
            // Use reflection to call MelonLoader.NativeLibrary.AgnosticLoadLibrary
            var melonAssembly = MelInExShared.ml;
            var nativeLibraryType = melonAssembly.GetType("MelonLoader.NativeLibrary");
            var loadMethod = nativeLibraryType?.GetMethod("AgnosticLoadLibrary", BindingFlags.Static | BindingFlags.Public);
            if (loadMethod != null)
            {
                var result = loadMethod.Invoke(null, new object[] { path });
                return result != null ? (IntPtr)result : IntPtr.Zero;
            }
            return IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static IEnumerable<string> EnumerateMonoLibraryCandidates()
    {
        string[] names;
        var platform = Environment.OSVersion.Platform;

        if (platform == PlatformID.Win32NT)
        {
            names = new[] { "mono-2.0-bdwgc.dll", "mono-2.0-sgen.dll", "mono.dll" };
        }
        else if (platform == PlatformID.MacOSX)
        {
            names = new[] { "libmonobdwgc-2.0.dylib", "libmono-2.0.dylib", "libmono.0.dylib" };
        }
        else
        {
            names = new[] { "libmonobdwgc-2.0.so", "libmono-2.0.so", "libmono.so" };
        }

        foreach (var name in names)
            yield return name;

        var gameRoot = Paths.GameRootPath;
        if (string.IsNullOrEmpty(gameRoot))
            yield break;

        var dataDirectory = Path.Combine(gameRoot, $"{Paths.ProcessName}_Data");
        var candidates = new[]
        {
            Path.Combine(Path.Combine(dataDirectory, "MonoBleedingEdge"), "EmbedRuntime"),
            Path.Combine(dataDirectory, "MonoBleedingEdge"),
            Path.Combine(dataDirectory, "Mono")
        };

        foreach (var directory in candidates)
        {
            foreach (var name in names)
                yield return Path.Combine(directory, name);
        }
    }

    public static void MonoInstallHooks()
    {
        if (_monoResolveHandler != null)
            return;

        _monoSearchDirectoryScan ??= CreateMonoSearchDirectoryDelegate();
        
        _monoResolveHandler = (_, args) =>
        {
            try
            {
                var requestedName = new AssemblyName(args.Name).Name;
                if (string.IsNullOrEmpty(requestedName))
                    return null;

                // Prefer MelonLoader's own search directory logic when available.
                var assembly = _monoSearchDirectoryScan?.Invoke(requestedName);
                if (assembly != null)
                    return assembly;

                return ResolveFromKnownDirectories(requestedName);
            }
            catch (Exception ex)
            {
                Debug.Log($"Mono assembly resolve failed: {ex.Message}");
                return null;
            }
        };

        AppDomain.CurrentDomain.AssemblyResolve += _monoResolveHandler;
    }
    
    private static Assembly ResolveFromKnownDirectories(string assemblyName)
    {
        Type environment = AccessTools.TypeByName("MelonLoader.Utils.MelonEnvironment");
        string plugins = (string)AccessTools.Property(environment, "PluginsDirectory").GetValue(null);
        string mods = (string)AccessTools.Property(environment, "ModsDirectory").GetValue(null);
        string userLibs = (string)AccessTools.Property(environment, "UserLibsDirectory").GetValue(null);
        var searchPaths = new List<string>
        {
            MelInExShared.MlPath,
            Path.Combine(MelInExShared.MlPath, "MelonLoader", "net35"),
            Path.Combine(MelInExShared.MlPath, "MelonLoader", "net6"),
            plugins,
            mods,
            userLibs
        };

        foreach (var directory in searchPaths.Distinct())
        {
            if (!Directory.Exists(directory))
                continue;

            var candidate = Path.Combine(directory, assemblyName + ".dll");
            if (!File.Exists(candidate))
                continue;

            try
            {
                return Assembly.LoadFrom(candidate);
            }
            catch (Exception ex)
            {
                Debug.Log($"Failed to load {candidate}: {ex.Message}");
            }
        }

        return null;
    }
    
    private static Func<string, Assembly> CreateMonoSearchDirectoryDelegate()
    {
        try
        {
            var searchManagerType = MelInExShared.ml.GetType("MelonLoader.MonoInternals.ResolveInternals.SearchDirectoryManager");
            var scanMethod = searchManagerType?.GetMethod("Scan", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (scanMethod == null)
                return null;

            return requestedName => scanMethod.Invoke(null, new object[] { requestedName }) as Assembly;
        }
        catch (Exception ex)
        {
            Debug.Log($"Failed to bind SearchDirectoryManager.Scan: {ex.Message}");
            return null;
        }
    }
}