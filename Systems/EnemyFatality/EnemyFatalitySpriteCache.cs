using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>Caches numbered PNG frames (1.png …) per resolved clip directory.</summary>
internal static class EnemyFatalitySpriteCache
{
    internal const float PixelsPerUnit = 100f;
    private static readonly Vector2 CenterPivot = new Vector2(0.5f, 0.5f);

    private static readonly Dictionary<string, Sprite[]> Cache =
        new Dictionary<string, Sprite[]>(System.StringComparer.OrdinalIgnoreCase);

    internal static Sprite[] GetFramesForRelative(string clipRelative, string logTag)
    {
        if (string.IsNullOrEmpty(clipRelative))
            return null;

        string directory = EnemyFatalityPaths.ResolveRelativeDirectory(clipRelative);
        if (string.IsNullOrEmpty(directory))
            return null;

        if (Cache.TryGetValue(directory, out Sprite[] cached) && cached != null && cached.Length > 0)
            return cached;

        Sprite[] frames = LoadSequentialFrames(directory, logTag);
        if (frames != null && frames.Length > 0)
            Cache[directory] = frames;
        return frames;
    }

    internal static void Preload(IEnemyFatalityProfile profile)
    {
        if (profile == null)
            return;

        string[] relatives = EnemyFatalityClipPicker.GetPreloadRelatives(profile);
        for (int i = 0; i < relatives.Length; i++)
            GetFramesForRelative(relatives[i], profile.Id);
    }

    private static Sprite[] LoadSequentialFrames(string directory, string logTag)
    {
        string tag = string.IsNullOrEmpty(logTag) ? "EnemyFatality" : logTag;

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            Plugin.Log?.LogWarning("[" + tag + "] Clip folder missing: " + directory);
            return null;
        }

        var frames = new List<Sprite>(24);
        for (int i = 1; i <= 128; i++)
        {
            string filePath = Path.Combine(directory, i + ".png");
            if (!File.Exists(filePath))
                break;

            Sprite sprite = LoadSpriteFromFile(filePath, tag);
            if (sprite != null)
                frames.Add(sprite);
        }

        if (frames.Count == 0)
        {
            Plugin.Log?.LogWarning("[" + tag + "] No numbered PNG frames in: " + directory);
            return null;
        }

        EnemyFatalityConfig.LogDebug(
            "[" + tag + "] Loaded " + frames.Count + " frames from " + directory);
        return frames.ToArray();
    }

    private static Sprite LoadSpriteFromFile(string filePath, string logTag)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            if (bytes == null || bytes.Length == 0)
                return null;

            if (!IsPng(bytes))
            {
                Plugin.Log?.LogWarning("[" + logTag + "] Not a PNG: " + filePath);
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.name = Path.GetFileNameWithoutExtension(filePath);
            if (!tex.LoadImage(bytes, false))
            {
                Object.Destroy(tex);
                Plugin.Log?.LogWarning("[" + logTag + "] LoadImage failed: " + filePath);
                return null;
            }

            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                CenterPivot,
                PixelsPerUnit);
            sprite.name = tex.name;
            return sprite;
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[" + logTag + "] Failed to load " + filePath + ": " + ex.Message);
            return null;
        }
    }

    private static bool IsPng(byte[] bytes)
    {
        return bytes.Length >= 8 &&
               bytes[0] == 0x89 &&
               bytes[1] == (byte)'P' &&
               bytes[2] == (byte)'N' &&
               bytes[3] == (byte)'G';
    }
}
