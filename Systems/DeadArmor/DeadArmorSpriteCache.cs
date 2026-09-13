using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NoREroMod.Systems.DeadArmor;

/// <summary>Caches physical / magic DeadArmor PNG sequences (w1.png …).</summary>
internal static class DeadArmorSpriteCache
{
    internal const float PixelsPerUnit = 100f;
    private static readonly Vector2 CenterPivot = new Vector2(0.5f, 0.5f);
    private const string LogTag = "DeadArmor";

    private static Sprite[] _physicalFrames;
    private static Sprite[] _magicFrames;
    private static bool _physicalTried;
    private static bool _magicTried;

    internal static Sprite[] GetPhysicalFrames()
    {
        if (!_physicalTried)
        {
            _physicalTried = true;
            _physicalFrames = LoadSequentialWFrames(DeadArmorPaths.ResolvePhysicalClipDirectory());
        }

        return _physicalFrames;
    }

    internal static Sprite[] GetMagicFrames()
    {
        if (!_magicTried)
        {
            _magicTried = true;
            _magicFrames = LoadSequentialWFrames(DeadArmorPaths.ResolveMagicClipDirectory());
        }

        return _magicFrames;
    }

    internal static void Preload()
    {
        GetPhysicalFrames();
        GetMagicFrames();
    }

    private static Sprite[] LoadSequentialWFrames(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            Plugin.Log?.LogWarning("[" + LogTag + "] Clip folder missing: " + directory);
            return null;
        }

        var frames = new List<Sprite>(16);
        for (int i = 1; i <= 64; i++)
        {
            string filePath = Path.Combine(directory, "w" + i + ".png");
            if (!File.Exists(filePath))
                break;

            Sprite sprite = LoadSpriteFromFile(filePath);
            if (sprite != null)
                frames.Add(sprite);
        }

        if (frames.Count == 0)
        {
            Plugin.Log?.LogWarning("[" + LogTag + "] No w*.png frames in: " + directory);
            return null;
        }

        DeadArmorConfig.LogDebug("[" + LogTag + "] Loaded " + frames.Count + " frames from " + directory);
        return frames.ToArray();
    }

    private static Sprite LoadSpriteFromFile(string filePath)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            if (bytes == null || bytes.Length == 0)
                return null;

            if (!IsPng(bytes))
            {
                Plugin.Log?.LogWarning("[" + LogTag + "] Not a PNG: " + filePath);
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.name = Path.GetFileNameWithoutExtension(filePath);
            if (!tex.LoadImage(bytes, false))
            {
                Object.Destroy(tex);
                Plugin.Log?.LogWarning("[" + LogTag + "] LoadImage failed: " + filePath);
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
            Plugin.Log?.LogWarning("[" + LogTag + "] Failed to load " + filePath + ": " + ex.Message);
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
