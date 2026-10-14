using System;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace NoREroMod.Systems.Diagnostics.AudioLeak;

/// <summary>
/// Config for leaked / looping audio diagnostics.
/// File: <c>BepInEx/plugins/HellGateJson/Diagnostics/AudioLeakDiagnostics.json</c>.
/// Hot-reloaded every 2 seconds.
/// </summary>
internal static class AudioLeakDiagnosticsConfig
{
    private const float ReloadIntervalSec = 2f;
    private static AudioLeakDiagnosticsSettings _cached;
    private static float _lastLoadTime = -999f;

    public static bool Enable => Get().Enable;
    public static float PollIntervalSec => Mathf.Max(0.1f, Get().PollIntervalSec);
    public static float RepeatSummarySec => Mathf.Max(1f, Get().RepeatSummarySec);
    public static int CallerDepth => Mathf.Clamp(Get().CallerDepth, 1, 8);
    public static int MaxLogsPerSession => Mathf.Max(0, Get().MaxLogsPerSession);

    public static AudioLeakDiagnosticsSettings Get()
    {
        if (_cached != null && Time.realtimeSinceStartup - _lastLoadTime < ReloadIntervalSec)
            return _cached;

        _cached = LoadFromFile();
        _lastLoadTime = Time.realtimeSinceStartup;
        return _cached;
    }

    private static AudioLeakDiagnosticsSettings LoadFromFile()
    {
        string path = GetConfigPath();
        if (!File.Exists(path))
            return AudioLeakDiagnosticsSettings.Default();

        try
        {
            string json = File.ReadAllText(path);
            if (!string.IsNullOrEmpty(json) && json[0] == '\uFEFF')
                json = json.TrimStart('\uFEFF');

            AudioLeakDiagnosticsSettings loaded = JsonUtility.FromJson<AudioLeakDiagnosticsSettings>(json);
            return loaded ?? AudioLeakDiagnosticsSettings.Default();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[AudioLeakDiag] Failed to load AudioLeakDiagnostics.json: " + ex.Message);
            return AudioLeakDiagnosticsSettings.Default();
        }
    }

    public static string GetConfigPath()
    {
        string dir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "Diagnostics");
        return Path.Combine(dir, "AudioLeakDiagnostics.json");
    }

    public static string GetLogFilePath()
    {
        string dir = Path.Combine(Paths.BepInExRootPath, "LogOutput");
        return Path.Combine(dir, "HellGate_AudioLeakDiag.log");
    }
}

[Serializable]
public class AudioLeakDiagnosticsSettings
{
    public bool Enable = false;
    public float PollIntervalSec = 0.5f;
    public float RepeatSummarySec = 5f;
    public int CallerDepth = 3;
    public int MaxLogsPerSession = 5000;

    public static AudioLeakDiagnosticsSettings Default() => new AudioLeakDiagnosticsSettings();
}

/// <summary>
/// Writes to a dedicated file only (<c>BepInEx/LogOutput/HellGate_AudioLeakDiag.log</c>) —
/// MasterAudio spam must not flood LogOutput.log.
/// </summary>
internal static class AudioLeakDiagLog
{
    private static readonly object FileLock = new object();
    private static int _logsThisSession;
    private static bool _fileHeaderWritten;
    private static bool _capWarned;

    public static void ResetSession()
    {
        _logsThisSession = 0;
        _capWarned = false;
    }

    public static void Write(string message)
    {
        if (!AudioLeakDiagnosticsConfig.Enable)
            return;

        if (_logsThisSession >= AudioLeakDiagnosticsConfig.MaxLogsPerSession)
        {
            if (!_capWarned)
            {
                _capWarned = true;
                AppendFile("log cap reached (" + AudioLeakDiagnosticsConfig.MaxLogsPerSession + ")");
            }
            return;
        }

        _logsThisSession++;
        AppendFile(message);
    }

    private static void AppendFile(string line)
    {
        try
        {
            string path = AudioLeakDiagnosticsConfig.GetLogFilePath();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            lock (FileLock)
            {
                if (!_fileHeaderWritten)
                {
                    File.AppendAllText(path,
                        "===== AudioLeakDiag session " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                        " =====" + Environment.NewLine,
                        Encoding.UTF8);
                    _fileHeaderWritten = true;
                }

                File.AppendAllText(
                    path,
                    DateTime.Now.ToString("HH:mm:ss.fff") + " t=" + Time.unscaledTime.ToString("0.000") +
                    " " + line + Environment.NewLine,
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Never break gameplay for diagnostics I/O.
        }
    }
}
