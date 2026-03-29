using System;
using System.IO;
using UnityEngine;
using Spine;

namespace NoREroMod.Patches.Enemy.WolfModCustom;

/// <summary>
/// TextureLoader for Spine Atlas — загружает PNG with диска, создаёт Material, присваивает page.rendererObject.
/// Обходит необходимость in TextAsset (Unity 5.6 not позволяет their создавать via reflection).
/// </summary>
internal sealed class WolfTextureLoader : TextureLoader
{
    private readonly string _imagesDir;
    private readonly Material _materialTemplate;
    private readonly string _name;

    public WolfTextureLoader(string imagesDir, Material materialTemplate, string name)
    {
        _imagesDir = imagesDir ?? "";
        _materialTemplate = materialTemplate;
        _name = name ?? "Wolf";
    }

    public void Load(AtlasPage page, string path)
    {
        string fullPath = path;
        if (!Path.IsPathRooted(path) && !string.IsNullOrEmpty(_imagesDir))
        {
            fullPath = Path.Combine(_imagesDir, Path.GetFileName(path));
        }

        if (!File.Exists(fullPath))
        {
            Plugin.Log?.LogError($"[WolfTextureLoader] PNG not found: {fullPath}");
            return;
        }

        byte[] pngBytes = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2);
        if (!texture.LoadImage(pngBytes))
        {
            Plugin.Log?.LogError($"[WolfTextureLoader] Failed to load image: {fullPath}");
            return;
        }
        texture.name = _name + "_tex";
        texture.filterMode = FilterMode.Bilinear;

        Material material = _materialTemplate != null && _materialTemplate.shader != null
            ? new Material(_materialTemplate) { mainTexture = texture, name = _name + "_mat" }
            : new Material(Shader.Find("Spine/Straight Alpha") ?? Shader.Find("Spine/Skeleton") ?? Shader.Find("Sprites/Default"))
            {
                mainTexture = texture,
                name = _name + "_mat"
            };

        page.rendererObject = material;
        if (page.width == 0 || page.height == 0)
        {
            page.width = texture.width;
            page.height = texture.height;
        }
    }

    public void Unload(object texture)
    {
        // Material/Texture managed by Unity
    }
}
