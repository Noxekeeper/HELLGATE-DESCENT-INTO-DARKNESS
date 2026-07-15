using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace NoREroMod.Systems.Diagnostics.Kinoko;

/// <summary>
/// Writes to BepInEx log (tagged) and <c>LogOutput/HellGate_KinokoMushroomEroDiag.log</c>.
/// </summary>
internal static class KinokoMushroomEroDiagLog
{
    private const string Tag = "[KinokoEroDiag]";
    private static readonly object FileLock = new object();
    private static int _logsThisSession;
    private static bool _fileHeaderWritten;
    private static bool _capWarned;

    public static void ResetSession()
    {
        _logsThisSession = 0;
        _capWarned = false;
    }

    public static void Info(string message) => Write(message, asWarning: false);

    public static void Warn(string message) => Write(message, asWarning: true);

    private static void Write(string message, bool asWarning)
    {
        if (!KinokoMushroomEroDiagnosticsConfig.Enable)
            return;

        if (_logsThisSession >= KinokoMushroomEroDiagnosticsConfig.MaxLogsPerSession)
        {
            if (!_capWarned)
            {
                _capWarned = true;
                string cap = Tag + " log cap reached (" + KinokoMushroomEroDiagnosticsConfig.MaxLogsPerSession + ")";
                Plugin.Log?.LogWarning(cap);
                AppendFile(cap);
            }
            return;
        }

        _logsThisSession++;
        string line = Tag + " " + message;
        if (asWarning)
            Plugin.Log?.LogWarning(line);
        else
            Plugin.Log?.LogInfo(line);
        AppendFile(line);
    }

    private static void AppendFile(string line)
    {
        try
        {
            string path = KinokoMushroomEroDiagnosticsConfig.GetLogFilePath();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            lock (FileLock)
            {
                if (!_fileHeaderWritten)
                {
                    File.AppendAllText(path,
                        "===== KinokoMushroomEroDiag session " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
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
