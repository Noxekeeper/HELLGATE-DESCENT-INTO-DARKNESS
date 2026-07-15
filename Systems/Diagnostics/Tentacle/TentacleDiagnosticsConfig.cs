using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace NoREroMod.Systems.Diagnostics.Tentacle;

/// <summary>
/// Standalone config for the tentacle H-scene diagnostics module.
/// File: <c>BepInEx/plugins/HellGateJson/Diagnostics/TentacleDiagnostics.json</c>.
///
/// The module is OFF by default. Flip <see cref="Enable"/> to <c>true</c> when you need
/// to investigate a soft-lock / invisible-scene reproduction. Hot-reloaded every 2 seconds
/// so you can toggle it without restarting the game.
/// </summary>
internal static class TentacleDiagnosticsConfig
{
    private const float ReloadIntervalSec = 2f;
    private static TentacleDiagnosticsSettings _cached;
    private static float _lastLoadTime = -999f;

    public static bool Enable => Get().Enable;
    public static float HeartbeatSec => Mathf.Max(0.05f, Get().HeartbeatSec);
    public static bool LogStackTraceOnErodataDeactivate => Get().LogStackTraceOnErodataDeactivate;
    public static bool LogStackTraceOnDestroyDuringHScene => Get().LogStackTraceOnDestroyDuringHScene;
    public static int MaxLogsPerSession => Mathf.Max(0, Get().MaxLogsPerSession);

    public static TentacleDiagnosticsSettings Get()
    {
        if (_cached != null && Time.realtimeSinceStartup - _lastLoadTime < ReloadIntervalSec)
            return _cached;

        _cached = LoadFromFile();
        _lastLoadTime = Time.realtimeSinceStartup;
        return _cached;
    }

    private static TentacleDiagnosticsSettings LoadFromFile()
    {
        string path = GetConfigPath();
        if (!File.Exists(path))
            return TentacleDiagnosticsSettings.Default();

        try
        {
            string json = File.ReadAllText(path);
            if (!string.IsNullOrEmpty(json) && json[0] == '\uFEFF')
                json = json.TrimStart('\uFEFF');

            TentacleDiagnosticsSettings loaded = JsonUtility.FromJson<TentacleDiagnosticsSettings>(json);
            return loaded ?? TentacleDiagnosticsSettings.Default();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[TentacleDiag] Failed to load TentacleDiagnostics.json: " + ex.Message);
            return TentacleDiagnosticsSettings.Default();
        }
    }

    public static string GetConfigPath()
    {
        string dir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "Diagnostics");
        return Path.Combine(dir, "TentacleDiagnostics.json");
    }
}

[Serializable]
public class TentacleDiagnosticsSettings
{
    public bool Enable = false;
    public float HeartbeatSec = 0.5f;
    public bool LogStackTraceOnErodataDeactivate = true;
    public bool LogStackTraceOnDestroyDuringHScene = true;
    public int MaxLogsPerSession = 500;

    public static TentacleDiagnosticsSettings Default() => new TentacleDiagnosticsSettings();
}
