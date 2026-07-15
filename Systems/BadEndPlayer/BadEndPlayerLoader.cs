using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using NoREroMod;

namespace NoREroMod.Systems.BadEndPlayer;

/// <summary>
/// Loads BadEnd Player manifest and images from sources/HellGate_sources/BadEndPlayer_Proto.
/// </summary>
internal static class BadEndPlayerLoader
{
    /// <summary>Legacy single-pack folder name used by the original prototype.</summary>
    private const string ProtoFolderName = "BadEndPlayer_Proto";
    /// <summary>Root folder for multi-pack setup: [game root]/sources/HellGate_sources/BadEndPlayer/[PackName]</summary>
    private const string MultiPackRootFolderName = "BadEndPlayer";

    /// <summary>
    /// Currently selected pack name (subfolder of BadEndPlayer). When null, falls back to legacy BadEndPlayer_Proto.
    /// </summary>
    private static string _currentPackName;

    /// <summary>
    /// Randomly select a BadEnd pack folder under sources/HellGate_sources/BadEndPlayer, if any exist.
    /// If the multi-pack root or subfolders are missing, falls back to legacy BadEndPlayer_Proto.
    /// </summary>
    public static void SelectRandomPackIfAny()
    {
        try
        {
            string basePath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string root = Path.Combine(Path.Combine(basePath, "sources"), "HellGate_sources");
            string multiRoot = Path.Combine(root, MultiPackRootFolderName);
            if (!Directory.Exists(multiRoot))
            {
                _currentPackName = null;
                return;
            }

            string[] packDirs = Directory.GetDirectories(multiRoot);
            if (packDirs == null || packDirs.Length == 0)
            {
                _currentPackName = null;
                return;
            }

            int idx = UnityEngine.Random.Range(0, packDirs.Length);
            _currentPackName = Path.GetFileName(packDirs[idx]);
            Plugin.Log?.LogInfo($"[BadEndPlayer] Selected pack: {_currentPackName}");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BadEndPlayer] SelectRandomPackIfAny failed: {ex.Message}");
            _currentPackName = null;
        }
    }

    /// <summary>
    /// Returns full path to the content folder:
    /// - Multi-pack: [game root]/sources/HellGate_sources/BadEndPlayer/[SelectedPack]
    /// - Legacy:     [game root]/sources/HellGate_sources/BadEndPlayer_Proto
    /// </summary>
    public static string GetProtoFolderPath()
    {
        try
        {
            string basePath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string root = Path.Combine(Path.Combine(basePath, "sources"), "HellGate_sources");

            // Multi-pack path if a pack has been selected.
            if (!string.IsNullOrEmpty(_currentPackName))
            {
                string multiPath = Path.Combine(Path.Combine(root, MultiPackRootFolderName), _currentPackName);
                return multiPath;
            }

            // Legacy single-folder path.
            string legacyPath = Path.Combine(root, ProtoFolderName);
            return legacyPath;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[BadEndPlayer] GetProtoFolderPath: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Load manifest.json from the proto folder. Returns null if file missing or invalid.
    /// </summary>
    public static BadEndPlayerManifest LoadManifest()
    {
        string folder = GetProtoFolderPath();
        if (string.IsNullOrEmpty(folder))
        {
            Plugin.Log?.LogWarning("[BadEndPlayer] Proto folder path is empty.");
            return null;
        }
        if (!Directory.Exists(folder))
        {
            Plugin.Log?.LogWarning("[BadEndPlayer] Proto folder not found: " + folder);
            return null;
        }

        string manifestPath = Path.Combine(folder, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            Plugin.Log?.LogWarning($"[BadEndPlayer] manifest.json not found: {manifestPath}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(manifestPath, System.Text.Encoding.UTF8);
            var manifest = JsonUtility.FromJson<BadEndPlayerManifest>(json);
            if (manifest == null) return null;
            // Unity JsonUtility often fails to fill root-level array; try wrapper first
            if (manifest.scenesWrapper != null && manifest.scenesWrapper.scenes != null && manifest.scenesWrapper.scenes.Length > 0)
                manifest.scenes = manifest.scenesWrapper.scenes;
            // Fallback: try parsing root "scenes" only (some Unity versions)
            if ((manifest.scenes == null || manifest.scenes.Length == 0))
            {
                var fallback = JsonUtility.FromJson<BadEndPlayerManifestScenesOnly>(json);
                if (fallback?.scenes != null && fallback.scenes.Length > 0)
                    manifest.scenes = fallback.scenes;
            }
            if (manifest.scenes == null || manifest.scenes.Length == 0)
            {
                var manualScenes = ParseScenesManually(json);
                if (manualScenes != null && manualScenes.Count > 0)
                    manifest.scenes = manualScenes.ToArray();
            }
            if (manifest.scenes == null || manifest.scenes.Length == 0)
            {
                Plugin.Log?.LogWarning("[BadEndPlayer] manifest has no scenes.");
                return null;
            }
            // JsonUtility often leaves long "text" empty; fill from manual parse
            bool anyEmptyText = false;
            for (int i = 0; i < manifest.scenes.Length; i++)
            {
                if (string.IsNullOrEmpty(manifest.scenes[i].text)) { anyEmptyText = true; break; }
            }
            if (anyEmptyText)
            {
                var manualScenes = ParseScenesManually(json);
                if (manualScenes != null)
                {
                    for (int i = 0; i < manifest.scenes.Length && i < manualScenes.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(manualScenes[i].text))
                            manifest.scenes[i].text = manualScenes[i].text;
                    }
                }
            }
            return manifest;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BadEndPlayer] JsonUtility failed (e.g. unescaped newlines in text): " + ex.Message);
            try
            {
                string path = Path.Combine(GetProtoFolderPath(), "manifest.json");
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                var manualScenes = ParseScenesManually(json);
                if (manualScenes != null && manualScenes.Count > 0)
                {
                    var manifest = new BadEndPlayerManifest();
                    manifest.scenes = manualScenes.ToArray();
                    ParseManifestFieldsFromJson(json, manifest);
                    return manifest;
                }
            }
            catch (Exception ex2)
            {
                Plugin.Log?.LogError($"[BadEndPlayer] LoadManifest fallback: {ex2.Message}");
            }
            return null;
        }
    }

    private static void ParseManifestFieldsFromJson(string json, BadEndPlayerManifest manifest)
    {
        if (string.IsNullOrEmpty(json) || manifest == null) return;
        var m = Regex.Match(json, "\"diaryTitle\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        if (m.Success) manifest.diaryTitle = UnescapeJsonString(m.Groups[1].Value);
        m = Regex.Match(json, "\"diaryIntro\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        if (m.Success) manifest.diaryIntro = UnescapeJsonString(m.Groups[1].Value);
        m = Regex.Match(json, "\"backgroundAudio\"\\s*:\\s*\"([^\"]*)\"");
        if (m.Success) manifest.backgroundAudio = m.Groups[1].Value;
        m = Regex.Match(json, "\"autoPlayDelay\"\\s*:\\s*([\\d.]+)");
        if (m.Success) float.TryParse(m.Groups[1].Value, out manifest.autoPlayDelay);
    }

    /// <summary>
    /// Load image as Texture2D from proto folder. File name is relative to proto folder (e.g. scene_01.png).
    /// </summary>
    public static Texture2D LoadImage(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return null;

        string folder = GetProtoFolderPath();
        string fullPath = Path.Combine(folder, fileName);
        if (!File.Exists(fullPath))
        {
            Plugin.Log?.LogWarning($"[BadEndPlayer] Image not found: {fullPath}");
            return null;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(fullPath);
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            return tex;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[BadEndPlayer] LoadImage {fileName}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Manual parse of "scenes" array from JSON when JsonUtility fails (Unity 5.6). Supports both "scenes":[...] and "scenesWrapper":{"scenes":[...]}
    /// </summary>
    private static List<BadEndPlayerScene> ParseScenesManually(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        List<BadEndPlayerScene> list = new List<BadEndPlayerScene>();

        // Find "scenes":[ (root or inside scenesWrapper)
        int arrStart = json.IndexOf("\"scenes\"", StringComparison.OrdinalIgnoreCase);
        if (arrStart < 0) return null;
        arrStart = json.IndexOf('[', arrStart);
        if (arrStart < 0) return null;
        int depth = 1;
        int arrayEnd = arrStart + 1;
        for (; arrayEnd < json.Length && depth > 0; arrayEnd++)
        {
            char c = json[arrayEnd];
            if (c == '[' || c == '{') depth++;
            else if (c == ']' || c == '}') depth--;
        }
        if (depth != 0) return null;
        string arrayContent = json.Substring(arrStart + 1, arrayEnd - arrStart - 2);

        // Split into object strings by matching { }
        int i = 0;
        while (i < arrayContent.Length)
        {
            int obStart = arrayContent.IndexOf('{', i);
            if (obStart < 0) break;
            depth = 1;
            int obEnd = obStart + 1;
            for (; obEnd < arrayContent.Length && depth > 0; obEnd++)
            {
                char c = arrayContent[obEnd];
                if (c == '{') depth++;
                else if (c == '}') depth--;
            }
            if (depth != 0) break;
            string block = arrayContent.Substring(obStart, obEnd - obStart);
            var scene = ParseOneSceneBlock(block);
            if (scene != null) list.Add(scene);
            i = obEnd;
        }

        return list.Count > 0 ? list : null;
    }

    /// <summary>Unescape JSON string: \\n -> newline, \\" -> ", \\\\ -> \</summary>
    private static string UnescapeJsonString(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        return raw.Replace("\\\\", "\u0001").Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\u0001", "\\");
    }

    private static BadEndPlayerScene ParseOneSceneBlock(string block)
    {
        if (string.IsNullOrEmpty(block)) return null;
        var scene = new BadEndPlayerScene();
        var idMatch = Regex.Match(block, "\"id\"\\s*:\\s*(\\d+)");
        if (idMatch.Success) int.TryParse(idMatch.Groups[1].Value, out scene.id);
        var fileMatch = Regex.Match(block, "\"file\"\\s*:\\s*\"([^\"]*)\"");
        if (fileMatch.Success) scene.file = fileMatch.Groups[1].Value;
        var durMatch = Regex.Match(block, "\"duration\"\\s*:\\s*([\\d.]+)");
        if (durMatch.Success) float.TryParse(durMatch.Groups[1].Value, out scene.duration);
        // Match "text":"...", allowing \" and \\ inside the string so JSON escape doesn't cut text
        var textMatch = Regex.Match(block, "\"text\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        if (textMatch.Success) scene.text = UnescapeJsonString(textMatch.Groups[1].Value);
        return string.IsNullOrEmpty(scene.file) ? null : scene;
    }

    /// <summary>
    /// Load audio from Proto folder (WWW). Tries fileName as-is, then same name with .wav if needed.
    /// </summary>
    public static IEnumerator LoadAudioClip(string fileName, Action<AudioClip> onLoaded)
    {
        if (string.IsNullOrEmpty(fileName)) { onLoaded(null); yield break; }
        string folder = GetProtoFolderPath();
        AudioClip clip = null;
        string[] toTry = new string[] { fileName, Path.GetFileNameWithoutExtension(fileName) + ".wav" };
        foreach (string name in toTry)
        {
            if (string.IsNullOrEmpty(name)) continue;
            string fullPath = Path.Combine(folder, name);
            if (!File.Exists(fullPath)) continue;
            string pathForUrl = fullPath.Replace('\\', '/').Replace(" ", "%20").Replace("#", "%23").Replace("&", "%26");
            string url = "file:///" + pathForUrl;
            WWW www = new WWW(url);
            yield return www;
            if (!string.IsNullOrEmpty(www.error))
            {
                Plugin.Log?.LogWarning("[BadEndPlayer] LoadAudio error for " + name + ": " + www.error);
                continue;
            }
            clip = www.GetAudioClip(false, name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? false : true);
            if (clip != null)
                break;
        }
        if (clip == null)
            Plugin.Log?.LogWarning("[BadEndPlayer] No audio loaded. Put .mp3 or .wav in BadEndPlayer_Proto and set backgroundAudio in manifest.");
        onLoaded(clip);
    }

    /// <summary>
    /// Returns true if file is video by extension (.mp4, .webm, .mov). Proto currently uses images only.
    /// </summary>
    public static bool IsVideoFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return false;
        string ext = Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";
        return ext == ".mp4" || ext == ".webm" || ext == ".mov";
    }
}
