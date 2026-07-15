using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Loads numbered PNG frames for the lethal cocoon trap death overlay.</summary>
internal static class LethalCocoonTrapAssetLoader
{
    private static readonly Sprite[] EmptySprites = new Sprite[0];
    private static Sprite[] _cachedFrames;
    private static string _cachedDirectory = string.Empty;

    internal static Sprite[] GetDeathFrames()
    {
        string directory = LethalCocoonTrapPaths.ResolveDeathClipDirectory(
            Plugin.lethalCocoonTrapDeathClipPath.Value);

        if (_cachedFrames != null &&
            string.Equals(_cachedDirectory, directory, StringComparison.OrdinalIgnoreCase))
        {
            return _cachedFrames;
        }

        ClearCache();
        _cachedDirectory = directory;

        if (!Directory.Exists(directory))
        {
            Plugin.Log?.LogWarning(
                "[LethalCocoonTrap] Death clip folder not found: " + directory);
            _cachedFrames = EmptySprites;
            return _cachedFrames;
        }

        string[] files = Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly);
        if (files == null || files.Length == 0)
        {
            Plugin.Log?.LogWarning(
                "[LethalCocoonTrap] No PNG frames in death clip folder: " + directory);
            _cachedFrames = EmptySprites;
            return _cachedFrames;
        }

        Array.Sort(files, CompareFramePaths);

        var list = new List<Sprite>(files.Length);
        for (int i = 0; i < files.Length; i++)
        {
            Sprite sprite = LoadSpriteFromFile(files[i]);
            if (sprite != null)
                list.Add(sprite);
        }

        _cachedFrames = list.ToArray();
        if (_cachedFrames.Length > 0)
        {
            string sizeWarning = BuildVariableFrameSizeWarning(_cachedFrames);
            Plugin.Log?.LogInfo(
                "[LethalCocoonTrap] Loaded "
                + _cachedFrames.Length
                + " death frame(s) from: "
                + directory
                + " — frame1 "
                + LethalTrapDeathSpriteLoader.DescribeSpriteWorldSize(_cachedFrames[0])
                + sizeWarning);
        }
        else if (files.Length > 0)
        {
            byte[] head = File.ReadAllBytes(files[0]);
            if (head != null && head.Length >= 3 && head[0] == (byte)'G' && head[1] == (byte)'I')
            {
                Plugin.Log?.LogError(
                    "[LethalCocoonTrap] WebSpike_Death frames are GIF renamed as .png. "
                    + "Run: powershell -ExecutionPolicy Bypass -File \"REZERVNIE COPY\\HELLGATE for Git\\dev\\tools\\ConvertWebSpikeGifFrames.ps1\"");
            }
        }

        return _cachedFrames;
    }

    internal static string GetCachedDirectory()
    {
        return _cachedDirectory ?? string.Empty;
    }

    internal static void ClearCache()
    {
        if (_cachedFrames == null)
            return;

        for (int i = 0; i < _cachedFrames.Length; i++)
        {
            Sprite sprite = _cachedFrames[i];
            if (sprite == null)
                continue;

            if (sprite.texture != null)
                UnityEngine.Object.Destroy(sprite.texture);
            UnityEngine.Object.Destroy(sprite);
        }

        _cachedFrames = null;
        _cachedDirectory = string.Empty;
    }

    private static int CompareFramePaths(string a, string b)
    {
        int na = ExtractFrameNumber(a);
        int nb = ExtractFrameNumber(b);
        if (na != nb)
            return na.CompareTo(nb);
        return string.Compare(
            Path.GetFileName(a),
            Path.GetFileName(b),
            StringComparison.OrdinalIgnoreCase);
    }

    private static int ExtractFrameNumber(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        Match match = Regex.Match(name, @"(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int value))
            return value;
        return int.MaxValue;
    }

    private static Sprite LoadSpriteFromFile(string filePath) =>
        LethalTrapDeathSpriteLoader.LoadSpriteFromFile(filePath, "LethalCocoonTrap");

    private static string BuildVariableFrameSizeWarning(Sprite[] frames)
    {
        if (frames == null || frames.Length < 2 || frames[0] == null)
            return string.Empty;

        Vector2 first = frames[0].bounds.size;
        float maxHeight = first.y;
        float minHeight = first.y;

        for (int i = 1; i < frames.Length; i++)
        {
            Sprite frame = frames[i];
            if (frame == null)
                continue;

            float h = frame.bounds.size.y;
            if (h > maxHeight)
                maxHeight = h;
            if (h < minHeight)
                minHeight = h;
        }

        if (maxHeight <= minHeight * 1.05f)
            return string.Empty;

        return " — variable frame heights ("
            + minHeight.ToString("0.##")
            + "–"
            + maxHeight.ToString("0.##")
            + " world); clip runner normalizes to frame 1.";
    }
}
