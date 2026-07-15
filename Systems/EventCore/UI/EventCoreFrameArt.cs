using System;
using System.IO;
using System.Text;
using NoREroMod;
using NoREroMod.Systems.EventCore.Core;
using UnityEngine;

namespace NoREroMod.Systems.EventCore.UI;

/// <summary>
/// Loads the EventCore modal frame PNG.
/// The preferred location is the shared EventCore content root, with compatibility probes for the legacy
/// source-art folders resolved relative to the game executable.
/// </summary>
internal static class EventCoreFrameArt
{
    private static readonly string[] FrameFileCandidates =
    {
        "EventCore Text_Frame.png",
        "EventCore_Text_Frame.png",
        "Text_Frame.png",
    };

    private static Texture2D _texture;
    private static bool _loadFinished;
    private static bool _loggedSuccess;
    private static bool _loggedFailure;

    /// <summary>
    /// Returns the PNG texture used by the modal frame overlay.
    /// </summary>
    internal static Texture2D TryGetFrameTexture()
    {
        EnsureLoaded();
        return _texture;
    }

    private static void EnsureLoaded()
    {
        if (_loadFinished)
            return;
        _loadFinished = true;

        if (!ResolveFramePath(out string path, out string diag))
        {
            if (!_loggedFailure)
            {
                _loggedFailure = true;
                Plugin.Log?.LogWarning("[EventCore] Modal frame PNG was not found.\n" + diag);
            }

            return;
        }

        _texture = LoadTextureFromFile(path);
        if (_texture == null)
        {
            if (!_loggedFailure)
            {
                _loggedFailure = true;
                Plugin.Log?.LogWarning("[EventCore] Modal frame PNG exists but failed to decode: " + path);
            }

            return;
        }

        if (!_loggedSuccess)
        {
            _loggedSuccess = true;
            Plugin.Log?.LogInfo("[EventCore] Modal frame loaded: " + path + " (" + _texture.width + "x" + _texture.height + ")");
        }
    }

    private static bool ResolveFramePath(out string foundPath, out string diagnostic)
    {
        foundPath = null;
        diagnostic = string.Empty;
        var sb = new StringBuilder();

        try
        {
            string contentRoot = EventCorePaths.JsonRoot;
            if (!string.IsNullOrEmpty(contentRoot))
            {
                AppendProbeHeader(sb, "EventCore content root", contentRoot);
                if (TryFindInDirectory(contentRoot, out foundPath))
                    return true;
            }
            else
            {
                sb.AppendLine("EventCore content root:");
                sb.AppendLine("  <uninitialized>");
            }

            string gameRoot = Application.dataPath;
            if (!string.IsNullOrEmpty(gameRoot) && gameRoot.EndsWith("_Data", StringComparison.Ordinal))
                gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

            string legacyRelative = Path.Combine(Path.Combine("sources", "HellGate_sources"), "EventCore");
            string legacyRoot = string.IsNullOrEmpty(gameRoot)
                ? string.Empty
                : Path.GetFullPath(Path.Combine(gameRoot, legacyRelative));
            string legacyParentRoot = string.IsNullOrEmpty(gameRoot)
                ? string.Empty
                : Path.GetFullPath(Path.Combine(Path.Combine(gameRoot, ".."), legacyRelative));

            if (!string.IsNullOrEmpty(legacyRoot))
            {
                AppendProbeHeader(sb, "Legacy source-art root", legacyRoot);
                if (TryFindInDirectory(legacyRoot, out foundPath))
                    return true;
            }

            if (!string.IsNullOrEmpty(legacyParentRoot))
            {
                AppendProbeHeader(sb, "Legacy source-art parent root", legacyParentRoot);
                if (TryFindInDirectory(legacyParentRoot, out foundPath))
                    return true;
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine("Search error: " + ex.Message);
        }

        diagnostic = sb.ToString();
        return false;
    }

    private static void AppendProbeHeader(StringBuilder sb, string label, string dir)
    {
        sb.AppendLine(label + ":");
        sb.AppendLine("  " + dir);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            sb.AppendLine("  Directory does not exist.");
            return;
        }

        sb.AppendLine("  Checked candidate files:");
        for (int i = 0; i < FrameFileCandidates.Length; i++)
            sb.AppendLine("    " + Path.Combine(dir, FrameFileCandidates[i]));
    }

    private static bool TryFindInDirectory(string dir, out string path)
    {
        path = null;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            return false;

        foreach (string name in FrameFileCandidates)
        {
            string p = Path.Combine(dir, name);
            if (File.Exists(p))
            {
                path = p;
                return true;
            }
        }

        try
        {
            string[] pngs = Directory.GetFiles(dir, "*.png");
            if (pngs == null || pngs.Length == 0)
                return false;

            if (pngs.Length == 1)
            {
                path = pngs[0];
                return true;
            }

            for (int i = 0; i < pngs.Length; i++)
            {
                string file = Path.GetFileName(pngs[i]);
                if (!string.IsNullOrEmpty(file) &&
                    file.IndexOf("frame", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    path = pngs[i];
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static Texture2D LoadTextureFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return null;
            byte[] data = File.ReadAllBytes(filePath);
            if (data == null || data.Length == 0)
                return null;

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(data, false))
                return null;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();
            return tex;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[EventCore] EventCoreFrameArt texture load failed: " + ex.Message);
            return null;
        }
    }
}
