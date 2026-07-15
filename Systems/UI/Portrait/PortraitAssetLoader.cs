using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NoREroMod.Systems.UI.Portrait;

internal static class PortraitAssetLoader
{
    private static readonly Sprite[] _emptySprites = new Sprite[0];
    private static readonly Dictionary<string, Sprite[]> Cache = new Dictionary<string, Sprite[]>(StringComparer.OrdinalIgnoreCase);

    /// <summary>State key to on-disk folder names; first existing path wins (supports spaced/underscored variants).</summary>
    private static readonly Dictionary<string, string[]> FolderAliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        { "Normal", new[] { "Normal" } },
        { "NakedNormal", new[] { "NakedNormal", "Naked Normal", "Naked_Normal" } },
        { "Sex", new[] { "Sex" } },
        { "Rage", new[] { "Rage" } },
        { "NakedRage", new[] { "NakedRage", "Naked Rage", "Naked_Rage" } },
        { "Brainwash", new[] { "Brainwash" } },
    };

    internal static string ResolveRootDirectory()
    {
        string rel = Plugin.portraitModAssetsPath.Value;
        if (string.IsNullOrEmpty(rel))
            rel = Path.Combine(Path.Combine("sources", "HellGate_sources"), "Portrait_mod");

        string gameRoot = Application.dataPath;
        if (gameRoot.EndsWith("_Data", StringComparison.Ordinal))
            gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

        rel = rel.Replace('/', Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar);

        string pluginSide = Path.Combine(Path.Combine(Path.Combine(Path.Combine(gameRoot, "BepInEx"), "plugins"), "NoR_HellGate"), rel);
        string[] candidates = new string[]
        {
            Path.Combine(gameRoot, rel),
            Path.Combine(Path.GetFullPath(Path.Combine(gameRoot, "..")), rel),
            pluginSide,
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            string full = Path.GetFullPath(candidates[i]);
            if (Directory.Exists(full))
                return full;
        }

        // No candidate exists; return canonical default path for diagnostics.
        return Path.GetFullPath(Path.Combine(gameRoot, rel));
    }

    internal static string ResolveStateFolderPath(string root, string stateKey)
    {
        if (!FolderAliases.TryGetValue(stateKey, out string[] aliases))
            aliases = new[] { stateKey };

        for (int i = 0; i < aliases.Length; i++)
        {
            string combined = Path.Combine(root, aliases[i]);
            if (Directory.Exists(combined))
                return combined;
        }

        return null;
    }

    internal static Sprite[] GetOrLoadSprites(string root, string stateKey)
    {
        string cacheKey = stateKey + "|" + root;
        if (Cache.TryGetValue(cacheKey, out Sprite[] cached))
            return cached;

        string folder = ResolveStateFolderPath(root, stateKey);
        if (string.IsNullOrEmpty(folder))
        {
            Cache[cacheKey] = _emptySprites;
            return Cache[cacheKey];
        }

        string[] files = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly);
        if (files == null || files.Length == 0)
        {
            Cache[cacheKey] = _emptySprites;
            return Cache[cacheKey];
        }

        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        var list = new List<Sprite>(files.Length);
        for (int i = 0; i < files.Length; i++)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(files[i]);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.name = Path.GetFileNameWithoutExtension(files[i]);
                if (!tex.LoadImage(bytes))
                {
                    UnityEngine.Object.Destroy(tex);
                    continue;
                }

                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.Apply(false, true);

                // pixelsPerUnit 100: Image.SetNativeSize uses rect size / PPU; PPU 1 inflates layout units for large textures.
                var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = tex.name;
                list.Add(sprite);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[PortraitMod] Failed to load {files[i]}: {ex.Message}");
            }
        }

        Cache[cacheKey] = list.ToArray();
        return Cache[cacheKey];
    }

    internal static void ClearCacheAndDestroySprites()
    {
        foreach (var kv in Cache)
        {
            if (kv.Value == null) continue;
            for (int i = 0; i < kv.Value.Length; i++)
            {
                if (kv.Value[i] == null) continue;
                if (kv.Value[i].texture != null)
                    UnityEngine.Object.Destroy(kv.Value[i].texture);
                UnityEngine.Object.Destroy(kv.Value[i]);
            }
        }

        Cache.Clear();
    }
}
