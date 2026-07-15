using System;
using System.Collections.Generic;
using System.IO;
using NoREroMod;
using UnityEngine;

namespace NoREroMod.Systems.EventCore.UI;

/// <summary>Loads ordered PNG frames from an expression subfolder (same pattern as HUD portrait mod).</summary>
internal static class EventCorePortraitClipLoader
{
    private static readonly Sprite[] Empty = new Sprite[0];
    private static readonly Dictionary<string, Sprite[]> Cache = new Dictionary<string, Sprite[]>(StringComparer.OrdinalIgnoreCase);

    internal static Sprite[] GetFrames(string characterRoot, string expressionFolder)
    {
        if (string.IsNullOrEmpty(characterRoot) || string.IsNullOrEmpty(expressionFolder))
            return Empty;

        string cacheKey = characterRoot + "|" + expressionFolder;
        if (Cache.TryGetValue(cacheKey, out Sprite[] cached))
            return cached;

        string folder = ResolveExpressionFolder(characterRoot, expressionFolder);
        if (string.IsNullOrEmpty(folder))
        {
            Cache[cacheKey] = Empty;
            return Empty;
        }

        string[] files = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly);
        if (files == null || files.Length == 0)
        {
            Plugin.Log?.LogWarning($"[EventCore] Portrait folder empty: {folder}");
            Cache[cacheKey] = Empty;
            return Empty;
        }

        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        var list = new List<Sprite>(files.Length);
        for (int i = 0; i < files.Length; i++)
        {
            Sprite sprite = LoadSpriteFromFile(files[i]);
            if (sprite != null)
                list.Add(sprite);
        }

        Cache[cacheKey] = list.ToArray();
        return Cache[cacheKey];
    }

    internal static void ClearCache()
    {
        foreach (var kv in Cache)
        {
            if (kv.Value == null)
                continue;
            for (int i = 0; i < kv.Value.Length; i++)
            {
                if (kv.Value[i] == null)
                    continue;
                if (kv.Value[i].texture != null)
                    UnityEngine.Object.Destroy(kv.Value[i].texture);
                UnityEngine.Object.Destroy(kv.Value[i]);
            }
        }

        Cache.Clear();
    }

    private static string ResolveExpressionFolder(string characterRoot, string expressionFolder)
    {
        string direct = Path.Combine(characterRoot, expressionFolder.Trim());
        if (Directory.Exists(direct))
            return direct;

        if (!Directory.Exists(characterRoot))
            return null;

        string[] dirs = Directory.GetDirectories(characterRoot);
        for (int i = 0; i < dirs.Length; i++)
        {
            if (string.Equals(Path.GetFileName(dirs[i]), expressionFolder.Trim(), StringComparison.OrdinalIgnoreCase))
                return dirs[i];
        }

        return null;
    }

    private static Sprite LoadSpriteFromFile(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }

            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply(false, true);

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = Path.GetFileNameWithoutExtension(path);
            return sprite;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[EventCore] Portrait load failed ({path}): {ex.Message}");
            return null;
        }
    }
}
