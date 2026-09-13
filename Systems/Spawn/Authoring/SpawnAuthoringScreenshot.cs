using System;
using System.Collections;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// F11 authoring screenshot (F12): hides editor chrome for one frame, saves PNG under game root.
/// </summary>
internal static class SpawnAuthoringScreenshot
{
    private static bool capturing;
    private static bool busy;

    /// <summary>True while chrome/UI should stay hidden for a clean capture.</summary>
    internal static bool SuppressChrome => capturing;

    internal static bool TryBeginCapture(MonoBehaviour host, out string status)
    {
        status = null;
        if (host == null)
        {
            status = SpawnAuthoringLoc.T("status.noHost");
            return false;
        }

        if (busy)
        {
            status = SpawnAuthoringLoc.T("status.screenshotBusy");
            return false;
        }

        busy = true;
        host.StartCoroutine(CaptureRoutine(host));
        status = null;
        return true;
    }

    private static IEnumerator CaptureRoutine(MonoBehaviour host)
    {
        capturing = true;
        global::NoREroMod.SpawnPointAnalyzer.SetModeBannerVisible(false);

        // Wait for a frame where OnGUI skips chrome, then grab pixels at end of frame.
        yield return null;
        yield return new WaitForEndOfFrame();

        string path = null;
        string error = null;
        try
        {
            path = BuildOutputPath();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            int w = Screen.width;
            int h = Screen.height;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Plugin.Log?.LogWarning("[SPAWN AUTHORING] Screenshot failed: " + ex.Message);
        }

        capturing = false;
        if (global::NoREroMod.SpawnPointAnalyzer.IsRecordingModeActive)
            global::NoREroMod.SpawnPointAnalyzer.SetModeBannerVisible(true);
        busy = false;

        var overlay = host as SpawnAuthoringOverlayHost;
        if (overlay == null)
            yield break;

        if (!string.IsNullOrEmpty(error))
        {
            overlay.NotifyScreenshotResult(false, error);
            yield break;
        }

        string shown = ToRelativeDisplay(path);
        overlay.NotifyScreenshotResult(true, shown);
        Plugin.Log?.LogInfo("[SPAWN AUTHORING] Screenshot: " + shown);
    }

    private static string BuildOutputPath()
    {
        // Game root / HellGateScreenshots/
        string root = Paths.GameRootPath;
        if (string.IsNullOrEmpty(root))
            root = Application.dataPath;

        string dir = Path.Combine(root, "HellGateScreenshots");
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string loc = SanitizeFileToken(ResolveLocationHint());
        string name = "spawn_" + loc + "_" + stamp + ".png";
        return Path.Combine(dir, name);
    }

    /// <summary>UI/log path relative to game root when possible.</summary>
    private static string ToRelativeDisplay(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
            return "shot.png";

        try
        {
            string gameRoot = Paths.GameRootPath;
            if (string.IsNullOrEmpty(gameRoot))
                gameRoot = Application.dataPath;

            if (!string.IsNullOrEmpty(gameRoot))
            {
                string full = Path.GetFullPath(absolutePath);
                string root = Path.GetFullPath(gameRoot);
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    string rel = full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    return rel.Replace('\\', '/');
                }
            }
        }
        catch
        {
            // fall through
        }

        return Path.GetFileName(absolutePath);
    }

    private static string ResolveLocationHint()
    {
        string pack = SpawnAuthoringPackWriter.GetActivePackFileName();
        if (!string.IsNullOrEmpty(pack))
        {
            string noExt = Path.GetFileNameWithoutExtension(pack);
            if (!string.IsNullOrEmpty(noExt))
                return noExt;
        }

        if (!string.IsNullOrEmpty(SpawnAuthoringState.LastClickPackHint))
            return SpawnAuthoringState.LastClickPackHint;

        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        return string.IsNullOrEmpty(scene) ? "map" : scene;
    }

    private static string SanitizeFileToken(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "map";

        var sb = new StringBuilder(raw.Length);
        foreach (char c in raw)
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                sb.Append(c);
            else if (c == ' ' || c == '.' || c == ',')
                sb.Append('_');
        }

        string s = sb.ToString().Trim('_');
        if (s.Length > 48)
            s = s.Substring(0, 48);
        return string.IsNullOrEmpty(s) ? "map" : s;
    }
}
