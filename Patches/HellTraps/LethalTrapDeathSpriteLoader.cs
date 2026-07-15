using System.IO;
using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Shared PNG frame loading for lethal trap death overlays (Unity LoadImage only).</summary>
internal static class LethalTrapDeathSpriteLoader
{
    internal const float PixelsPerUnit = 100f;
    internal static readonly Vector2 FeetPivot = new Vector2(0.5f, 0f);

    internal static Sprite LoadSpriteFromFile(string filePath, string logTag)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            if (bytes == null || bytes.Length == 0)
                return null;

            if (IsGif(bytes))
            {
                Plugin.Log?.LogWarning(
                    "[" + logTag + "] "
                    + Path.GetFileName(filePath)
                    + " is GIF89a but named .png — Unity cannot load it. Run dev/tools/ConvertWebSpikeGifFrames.ps1 on WebSpike_Death.");
                return null;
            }

            if (!IsPng(bytes))
            {
                Plugin.Log?.LogWarning(
                    "[" + logTag + "] Not a PNG file: " + filePath);
                return null;
            }

            string texName = Path.GetFileNameWithoutExtension(filePath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.name = texName;
            if (!tex.LoadImage(bytes, false))
            {
                Object.Destroy(tex);
                Plugin.Log?.LogWarning("[" + logTag + "] LoadImage failed: " + filePath);
                return null;
            }

            if (tex.width < 32 || tex.height < 32)
            {
                Plugin.Log?.LogWarning(
                    "[" + logTag + "] Frame too small ("
                    + tex.width
                    + "x"
                    + tex.height
                    + "): "
                    + filePath);
                Object.Destroy(tex);
                return null;
            }

            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                FeetPivot,
                PixelsPerUnit);
            sprite.name = texName;
            return sprite;
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[" + logTag + "] Failed to load " + filePath + ": " + ex.Message);
            return null;
        }
    }

    private static bool IsGif(byte[] bytes)
    {
        return bytes.Length >= 6 &&
               bytes[0] == (byte)'G' &&
               bytes[1] == (byte)'I' &&
               bytes[2] == (byte)'F';
    }

    private static bool IsPng(byte[] bytes)
    {
        return bytes.Length >= 8 &&
               bytes[0] == 0x89 &&
               bytes[1] == (byte)'P' &&
               bytes[2] == (byte)'N' &&
               bytes[3] == (byte)'G';
    }

    internal static string DescribeSpriteWorldSize(Sprite frame)
    {
        if (frame == null)
            return "null";

        Texture2D tex = frame.texture;
        string texSize = tex != null ? tex.width + "x" + tex.height + "px" : "?x?";
        Vector2 bounds = frame.bounds.size;
        return texSize + " @ PPU " + PixelsPerUnit.ToString("0.##")
            + " → " + bounds.x.ToString("0.##") + "x" + bounds.y.ToString("0.##") + " world";
    }

    private static Sprite _emptyBonePlaceholderSprite;

    internal static Sprite GetEmptyBonePlaceholderSprite()
    {
        if (_emptyBonePlaceholderSprite != null)
            return _emptyBonePlaceholderSprite;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.name = "HellGateLethalDeathEmptyBoneFrame";
        tex.SetPixel(0, 0, Color.clear);
        tex.Apply(false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;

        _emptyBonePlaceholderSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, 1f, 1f),
            FeetPivot,
            PixelsPerUnit);
        _emptyBonePlaceholderSprite.name = "EmptyBoneFrame";
        return _emptyBonePlaceholderSprite;
    }
}
