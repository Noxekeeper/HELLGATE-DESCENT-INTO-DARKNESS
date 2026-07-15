using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace NoREroMod.Systems.Diagnostics.Kinoko;

/// <summary>
/// Config for Kinoko / MushroomERO H-scene event diagnostics.
/// File: <c>HellGateJson/Diagnostics/KinokoMushroomEroDiagnostics.json</c>.
/// Hot-reloaded every 2 seconds.
/// </summary>
internal static class KinokoMushroomEroDiagnosticsConfig
{
    private const float ReloadIntervalSec = 2f;
    private static KinokoMushroomEroDiagnosticsSettings _cached;
    private static float _lastLoadTime = -999f;

    public static bool Enable => Get().Enable;
    public static float HeartbeatSec => Mathf.Max(0.1f, Get().HeartbeatSec);
    public static float StuckWarnSec => Mathf.Max(0.5f, Get().StuckWarnSec);
    public static bool LogAllEvents => Get().LogAllEvents;
    public static bool LogInterestingOnly => Get().LogInterestingOnly;
    public static int MaxLogsPerSession => Mathf.Max(0, Get().MaxLogsPerSession);

    public static KinokoMushroomEroDiagnosticsSettings Get()
    {
        if (_cached != null && Time.realtimeSinceStartup - _lastLoadTime < ReloadIntervalSec)
            return _cached;

        _cached = LoadFromFile();
        _lastLoadTime = Time.realtimeSinceStartup;
        return _cached;
    }

    private static KinokoMushroomEroDiagnosticsSettings LoadFromFile()
    {
        string path = GetConfigPath();
        if (!File.Exists(path))
            return KinokoMushroomEroDiagnosticsSettings.Default();

        try
        {
            string json = File.ReadAllText(path);
            if (!string.IsNullOrEmpty(json) && json[0] == '\uFEFF')
                json = json.TrimStart('\uFEFF');

            KinokoMushroomEroDiagnosticsSettings loaded =
                JsonUtility.FromJson<KinokoMushroomEroDiagnosticsSettings>(json);
            return loaded ?? KinokoMushroomEroDiagnosticsSettings.Default();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[KinokoEroDiag] Failed to load KinokoMushroomEroDiagnostics.json: " + ex.Message);
            return KinokoMushroomEroDiagnosticsSettings.Default();
        }
    }

    public static string GetConfigPath()
    {
        string dir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "Diagnostics");
        return Path.Combine(dir, "KinokoMushroomEroDiagnostics.json");
    }

    public static string GetLogFilePath()
    {
        string dir = Path.Combine(Paths.BepInExRootPath, "LogOutput");
        return Path.Combine(dir, "HellGate_KinokoMushroomEroDiag.log");
    }

    public static bool IsInterestingEventOrAnim(string eventName, string animName)
    {
        string e = eventName ?? string.Empty;
        string a = animName ?? string.Empty;
        return e.IndexOf("START", StringComparison.OrdinalIgnoreCase) >= 0
            || e.IndexOf("ERO", StringComparison.OrdinalIgnoreCase) >= 0
            || a.IndexOf("START", StringComparison.OrdinalIgnoreCase) >= 0
            || a.IndexOf("ERO", StringComparison.OrdinalIgnoreCase) >= 0
            || string.Equals(e, "SE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e, "SE1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e, "SE5", StringComparison.OrdinalIgnoreCase);
    }
}

[Serializable]
public class KinokoMushroomEroDiagnosticsSettings
{
    public bool Enable = false;
    public float HeartbeatSec = 0.5f;
    public float StuckWarnSec = 1.5f;
    public bool LogAllEvents = true;
    public bool LogInterestingOnly = false;
    public int MaxLogsPerSession = 3000;

    public static KinokoMushroomEroDiagnosticsSettings Default() => new KinokoMushroomEroDiagnosticsSettings();
}
