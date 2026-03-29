using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Spine;
using Spine.Unity;
using UnityEngine;
using NoREroMod.Systems.Spawn;

namespace NoREroMod.Patches.Enemy.DoreiModCustom;

/// <summary>
/// Loads DoreiFapping skeleton (IDLE = fapping) from DoreiFapping.
/// Used to replace Dorei idle during H-scene wait.
/// </summary>
internal static class DoreiSkeletonLoader
{
    private static SkeletonDataAsset _doreiFappingSkeleton;
    private static bool _initFailed;

    internal static bool HasIdleAnimation(SkeletonDataAsset asset)
    {
        if (asset == null || !CustomAssets.TryGetValue(asset, out var data)) return false;
        return data.SkeletonData.FindAnimation("IDLE") != null;
    }

    internal static readonly Dictionary<SkeletonDataAsset, DoreiAssetData> CustomAssets = new Dictionary<SkeletonDataAsset, DoreiAssetData>();

    internal sealed class DoreiAssetData
    {
        public readonly SkeletonData SkeletonData;
        public readonly AnimationStateData StateData;

        public DoreiAssetData(SkeletonData skeletonData)
        {
            SkeletonData = skeletonData;
            StateData = new AnimationStateData(skeletonData);
        }
    }

    private static string GetBasePath(string configPath, string defaultSubfolder)
    {
        try
        {
            string gameRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(gameRoot)) return null;
            if (!string.IsNullOrEmpty(configPath))
            {
                string fullPath = Path.IsPathRooted(configPath) ? configPath : Path.Combine(gameRoot, configPath);
                return fullPath;
            }
            return Path.Combine(Path.Combine(gameRoot, "sources"), Path.Combine("HellGate_sources", defaultSubfolder));
        }
        catch
        {
            return null;
        }
    }

    public static SkeletonDataAsset GetDoreiFappingSkeleton(GameObject doreiPrefabTemplate)
    {
        if (_doreiFappingSkeleton != null) return _doreiFappingSkeleton;
        if (_initFailed) return null;

        string basePath = GetBasePath(Plugin.doreiFappingAssetsPath?.Value?.Trim(), "DoreiFapping");
        if (string.IsNullOrEmpty(basePath))
        {
            Plugin.Log?.LogWarning("[DoreiSkeletonLoader] Game root path not found");
            _initFailed = true;
            return null;
        }

        string jsonPath = Path.Combine(basePath, "DOREI_spine.json");
        string atlasPath = Path.Combine(basePath, "DOREI_spine.atlas");
        string pngPath = Path.Combine(basePath, "DOREI_spine.png");

        _doreiFappingSkeleton = LoadViaSpineApi(jsonPath, atlasPath, pngPath, "DoreiFapping", doreiPrefabTemplate);
        if (_doreiFappingSkeleton == null) _initFailed = true;
        return _doreiFappingSkeleton;
    }

    private static SkeletonDataAsset LoadViaSpineApi(string jsonPath, string atlasPath, string pngPath, string name, GameObject materialTemplate)
    {
        if (!File.Exists(jsonPath))
        {
            Plugin.Log?.LogWarning($"[DoreiSkeletonLoader] JSON not found: {jsonPath}");
            return null;
        }
        if (!File.Exists(atlasPath))
        {
            Plugin.Log?.LogWarning($"[DoreiSkeletonLoader] Atlas not found: {atlasPath}");
            return null;
        }
        if (!File.Exists(pngPath))
        {
            Plugin.Log?.LogWarning($"[DoreiSkeletonLoader] PNG not found: {pngPath}");
            return null;
        }

        try
        {
            string jsonText = File.ReadAllText(jsonPath);
            string atlasText = File.ReadAllText(atlasPath);
            string imagesDir = Path.GetDirectoryName(pngPath);

            Material materialTemplateMat = CreateMaterialTemplate(materialTemplate, name, pngPath);
            var textureLoader = new DoreiTextureLoader(imagesDir, materialTemplateMat, name);
            var atlas = new Atlas(new StringReader(atlasText), imagesDir, textureLoader);
            atlas.FlipV();

            var skeletonJson = new SkeletonJson(atlas) { Scale = 0.008f };
            SkeletonData skeletonData = skeletonJson.ReadSkeletonData(new StringReader(jsonText));
            if (skeletonData == null)
            {
                Plugin.Log?.LogWarning($"[DoreiSkeletonLoader] Failed to parse skeleton: {name}");
                return null;
            }

            SkeletonDataAsset asset = ScriptableObject.CreateInstance<SkeletonDataAsset>();
            asset.name = name + "_skeleton";
            asset.scale = 0.008f;
            CustomAssets[asset] = new DoreiAssetData(skeletonData);

            UnityEngine.Object.DontDestroyOnLoad(asset);

            Plugin.Log?.LogInfo($"[DoreiSkeletonLoader] Loaded {name} (IDLE=fapping) successfully");
            return asset;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[DoreiSkeletonLoader] Error loading {name}: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    private static Material CreateMaterialTemplate(GameObject template, string name, string pngPath)
    {
        if (template != null)
        {
            var spine = template.GetComponent<SkeletonAnimation>();
            if (spine?.skeletonDataAsset?.atlasAssets != null && spine.skeletonDataAsset.atlasAssets.Length > 0 &&
                spine.skeletonDataAsset.atlasAssets[0].materials != null &&
                spine.skeletonDataAsset.atlasAssets[0].materials.Length > 0)
            {
                Material src = spine.skeletonDataAsset.atlasAssets[0].materials[0];
                if (src != null && src.shader != null)
                    return src;
            }
        }
        return null;
    }
}

/// <summary>
/// Harmony patch for SkeletonDataAsset — returns preloaded data for DoreiFapping.
/// </summary>
[HarmonyPatch(typeof(SkeletonDataAsset))]
internal static class DoreiSkeletonDataAssetPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("GetSkeletonData", new Type[] { typeof(bool) })]
    static bool GetSkeletonData_Prefix(SkeletonDataAsset __instance, ref SkeletonData __result)
    {
        if (DoreiSkeletonLoader.CustomAssets.TryGetValue(__instance, out var data))
        {
            __result = data.SkeletonData;
            return false;
        }
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch("GetAnimationStateData")]
    static bool GetAnimationStateData_Prefix(SkeletonDataAsset __instance, ref AnimationStateData __result)
    {
        if (DoreiSkeletonLoader.CustomAssets.TryGetValue(__instance, out var data))
        {
            __result = data.StateData;
            return false;
        }
        return true;
    }
}
