using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace NoREroMod.Systems.Diagnostics.TrapBody;

/// <summary>
/// Config for trap H-scene player-body / camera diagnostics.
/// File: <c>BepInEx/plugins/HellGateJson/Diagnostics/TrapPlayerBodyDiagnostics.json</c>.
/// Hot-reloaded every 2 seconds.
/// </summary>
internal static class TrapPlayerBodyDiagnosticsConfig
{
    private const float ReloadIntervalSec = 2f;
    private static TrapPlayerBodyDiagnosticsSettings _cached;
    private static float _lastLoadTime = -999f;

    public static bool Enable => Get().Enable;
    public static float HeartbeatSec => Mathf.Max(0.05f, Get().HeartbeatSec);
    public static float YDropWarnThreshold => Mathf.Max(0.01f, Get().YDropWarnThreshold);
    public static bool LogStackTraceOnSimulatedEnable => Get().LogStackTraceOnSimulatedEnable;
    public static bool LogStackTraceOnStruggleInvul => Get().LogStackTraceOnStruggleInvul;
    public static bool WatchAllTraps => Get().WatchAllTraps;
    public static int MaxLogsPerSession => Mathf.Max(0, Get().MaxLogsPerSession);

    public static TrapPlayerBodyDiagnosticsSettings Get()
    {
        if (_cached != null && Time.realtimeSinceStartup - _lastLoadTime < ReloadIntervalSec)
            return _cached;

        _cached = LoadFromFile();
        _lastLoadTime = Time.realtimeSinceStartup;
        return _cached;
    }

    private static TrapPlayerBodyDiagnosticsSettings LoadFromFile()
    {
        string path = GetConfigPath();
        if (!File.Exists(path))
            return TrapPlayerBodyDiagnosticsSettings.Default();

        try
        {
            string json = File.ReadAllText(path);
            if (!string.IsNullOrEmpty(json) && json[0] == '\uFEFF')
                json = json.TrimStart('\uFEFF');

            TrapPlayerBodyDiagnosticsSettings loaded =
                JsonUtility.FromJson<TrapPlayerBodyDiagnosticsSettings>(json);
            return loaded ?? TrapPlayerBodyDiagnosticsSettings.Default();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[TrapBodyDiag] Failed to load TrapPlayerBodyDiagnostics.json: " + ex.Message);
            return TrapPlayerBodyDiagnosticsSettings.Default();
        }
    }

    public static string GetConfigPath()
    {
        string dir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "Diagnostics");
        return Path.Combine(dir, "TrapPlayerBodyDiagnostics.json");
    }

    public static string GetLogFilePath()
    {
        string dir = Path.Combine(Paths.BepInExRootPath, "LogOutput");
        return Path.Combine(dir, "HellGate_TrapPlayerBodyDiag.log");
    }
}

[Serializable]
public class TrapPlayerBodyDiagnosticsSettings
{
    public bool Enable = false;
    public float HeartbeatSec = 0.25f;
    public float YDropWarnThreshold = 0.05f;
    public bool LogStackTraceOnSimulatedEnable = true;
    public bool LogStackTraceOnStruggleInvul = true;
    public bool WatchAllTraps = true;
    public int MaxLogsPerSession = 2000;

    public static TrapPlayerBodyDiagnosticsSettings Default() => new TrapPlayerBodyDiagnosticsSettings();
}
