using System.IO;
using Spine;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.UndeadSkeletonsModCustom;

internal sealed class UndeadSkeletonsTextureLoader : TextureLoader
{
    private readonly string _imagesDir;
    private readonly Material _materialTemplate;
    private readonly string _name;

    public UndeadSkeletonsTextureLoader(string imagesDir, Material materialTemplate, string name)
    {
        _imagesDir = imagesDir ?? string.Empty;
        _materialTemplate = materialTemplate;
        _name = name ?? "UndeadSkeleton";
    }

    public void Load(AtlasPage page, string path)
    {
        string fullPath = path;
        if (!Path.IsPathRooted(path) && !string.IsNullOrEmpty(_imagesDir))
            fullPath = Path.Combine(_imagesDir, Path.GetFileName(path));

        if (!File.Exists(fullPath))
        {
            string fallback = ResolveFallbackPng();
            if (fallback == null)
            {
                Plugin.Log?.LogError($"[UndeadSkeletonsTextureLoader] PNG not found: {fullPath}");
                return;
            }

            Plugin.Log?.LogWarning(
                $"[UndeadSkeletonsTextureLoader] Atlas page '{Path.GetFileName(path)}' not found, using {Path.GetFileName(fallback)}");
            fullPath = fallback;
        }

        byte[] pngBytes = File.ReadAllBytes(fullPath);
        var texture = new Texture2D(2, 2);
        if (!texture.LoadImage(pngBytes))
        {
            Plugin.Log?.LogError($"[UndeadSkeletonsTextureLoader] Failed to load image: {fullPath}");
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

    /// <summary>Atlas page names often keep the animator's original png name; accept &lt;Key&gt;.png or the folder's only png.</summary>
    private string ResolveFallbackPng()
    {
        if (string.IsNullOrEmpty(_imagesDir) || !Directory.Exists(_imagesDir))
            return null;

        string byKey = Path.Combine(_imagesDir, _name + ".png");
        if (File.Exists(byKey))
            return byKey;

        string[] pngs = Directory.GetFiles(_imagesDir, "*.png");
        return pngs.Length == 1 ? pngs[0] : null;
    }

    public void Unload(object texture)
    {
    }
}
