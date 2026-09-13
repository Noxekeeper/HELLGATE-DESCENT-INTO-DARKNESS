using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Loads numbered PNG frames for the lethal lightning button trap death overlay.</summary>
internal static class LethalLightningTrapAssetLoader
{
    private static readonly Sprite[] EmptySprites = new Sprite[0];
    private static Sprite[] _cachedFrames;
    private static string _cachedDirectory = string.Empty;

    internal static Sprite[] GetDeathFrames()
    {
        string directory = LethalLightningTrapPaths.ResolveDeathClipDirectory(
            Plugin.lethalLightningTrapDeathClipPath != null
                ? Plugin.lethalLightningTrapDeathClipPath.Value
                : string.Empty);

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
                "[LethalLightningTrap] Death clip folder not found: " + directory);
            _cachedFrames = EmptySprites;
            return _cachedFrames;
        }

        string[] files = Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly);
        if (files == null || files.Length == 0)
        {
            Plugin.Log?.LogWarning(
                "[LethalLightningTrap] No PNG frames in death clip folder: " + directory);
            _cachedFrames = EmptySprites;
            return _cachedFrames;
        }

        Array.Sort(files, CompareFramePaths);

        var list = new List<Sprite>(files.Length);
        for (int i = 0; i < files.Length; i++)
        {
            Sprite sprite = LethalTrapDeathSpriteLoader.LoadSpriteFromFile(files[i], "LethalLightningTrap");
            if (sprite != null)
                list.Add(sprite);
        }

        _cachedFrames = list.ToArray();
        if (_cachedFrames.Length > 0)
        {
            Plugin.Log?.LogInfo(
                "[LethalLightningTrap] Loaded "
                + _cachedFrames.Length
                + " death frame(s) from: "
                + directory
                + " — frame1 "
                + LethalTrapDeathSpriteLoader.DescribeSpriteWorldSize(_cachedFrames[0]));
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
}
