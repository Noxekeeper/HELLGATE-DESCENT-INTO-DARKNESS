using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Spine;
using Spine.Unity;
using UnityEngine;
using NoREroMod.Systems.Spawn;

namespace NoREroMod.Patches.Enemy.SlaveBigAxeMeatArmor;

/// <summary>
/// Disk-loads <c>s_DOREIBIG_Aradia_armor</c> (json/atlas/png) for MeatArmor walk,
/// using the same SkeletonDataAsset hook pattern as Wolf custom enemies.
/// </summary>
internal static class SlaveBigAxeMeatArmorSkeletonLoader
{
    private const string AssetFileBase = "s_DOREIBIG_Aradia_armor";

    private static SkeletonDataAsset _armorSkeleton;
    private static bool _initFailed;

    internal static readonly Dictionary<SkeletonDataAsset, MeatArmorAssetData> CustomAssets =
        new Dictionary<SkeletonDataAsset, MeatArmorAssetData>();

    internal sealed class MeatArmorAssetData
    {
        public readonly SkeletonData SkeletonData;
        public readonly AnimationStateData StateData;

        public MeatArmorAssetData(SkeletonData skeletonData)
        {
            SkeletonData = skeletonData;
            StateData = new AnimationStateData(skeletonData);
        }
    }

    private static string GetAssetsBasePath()
    {
        try
        {
            string gameRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(gameRoot))
            {
                return null;
            }

            string customPath = Plugin.slaveBigAxeMeatArmorAssetsPath?.Value?.Trim();
            if (!string.IsNullOrEmpty(customPath))
            {
                return Path.IsPathRooted(customPath)
                    ? customPath
                    : Path.Combine(gameRoot, customPath);
            }

            return Path.Combine(Path.Combine(gameRoot, "sources"),
                Path.Combine("HellGate_sources", "SlaveBigAxeSource"));
        }
        catch
        {
            return null;
        }
    }

    public static SkeletonDataAsset GetArmorSkeleton(GameObject materialTemplate)
    {
        if (_armorSkeleton != null)
        {
            return _armorSkeleton;
        }

        if (_initFailed)
        {
            return null;
        }

        string basePath = GetAssetsBasePath();
        if (string.IsNullOrEmpty(basePath))
        {
            Plugin.Log?.LogWarning("[SlaveBigAxeMeatArmor] Game root / assets path not found");
            _initFailed = true;
            return null;
        }

        string jsonPath = Path.Combine(basePath, AssetFileBase + ".json");
        string atlasPath = Path.Combine(basePath, AssetFileBase + ".atlas");
        string pngPath = Path.Combine(basePath, AssetFileBase + ".png");

        _armorSkeleton = LoadViaSpineApi(jsonPath, atlasPath, pngPath, "SlaveBigAxeMeatArmor", materialTemplate);
        if (_armorSkeleton == null)
        {
            _initFailed = true;
        }

        return _armorSkeleton;
    }

    private static SkeletonDataAsset LoadViaSpineApi(
        string jsonPath,
        string atlasPath,
        string pngPath,
        string name,
        GameObject materialTemplate)
    {
        if (!File.Exists(jsonPath))
        {
            Plugin.Log?.LogWarning($"[SlaveBigAxeMeatArmor] JSON not found: {jsonPath}");
            return null;
        }

        if (!File.Exists(atlasPath))
        {
            Plugin.Log?.LogWarning($"[SlaveBigAxeMeatArmor] Atlas not found: {atlasPath}");
            return null;
        }

        if (!File.Exists(pngPath))
        {
            Plugin.Log?.LogWarning($"[SlaveBigAxeMeatArmor] PNG not found: {pngPath}");
            return null;
        }

        try
        {
            string jsonText = File.ReadAllText(jsonPath);
            string atlasText = File.ReadAllText(atlasPath);
            string imagesDir = Path.GetDirectoryName(pngPath);

            Material materialTemplateMat = CreateMaterialTemplate(materialTemplate);
            var textureLoader = new SlaveBigAxeMeatArmorTextureLoader(imagesDir, materialTemplateMat, name);
            var atlas = new Atlas(new StringReader(atlasText), imagesDir, textureLoader);
            atlas.FlipV();

            var skeletonJson = new SkeletonJson(atlas) { Scale = 0.01f };
            SkeletonData skeletonData = skeletonJson.ReadSkeletonData(new StringReader(jsonText));
            if (skeletonData == null)
            {
                Plugin.Log?.LogWarning($"[SlaveBigAxeMeatArmor] Failed to parse skeleton: {name}");
                return null;
            }

            SkeletonDataAsset asset = ScriptableObject.CreateInstance<SkeletonDataAsset>();
            asset.name = name + "_skeleton";
            asset.scale = 0.01f;
            CustomAssets[asset] = new MeatArmorAssetData(skeletonData);
            UnityEngine.Object.DontDestroyOnLoad(asset);

            Plugin.Log?.LogInfo($"[SlaveBigAxeMeatArmor] Loaded {name} from disk");
            return asset;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[SlaveBigAxeMeatArmor] Error loading {name}: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    private static Material CreateMaterialTemplate(GameObject template)
    {
        if (template == null)
        {
            return null;
        }

        var spine = template.GetComponent<SkeletonAnimation>()
                    ?? template.GetComponentInChildren<SkeletonAnimation>(true);
        if (spine?.skeletonDataAsset?.atlasAssets == null || spine.skeletonDataAsset.atlasAssets.Length == 0)
        {
            return null;
        }

        var mats = spine.skeletonDataAsset.atlasAssets[0].materials;
        if (mats == null || mats.Length == 0)
        {
            return null;
        }

        Material src = mats[0];
        return src != null && src.shader != null ? src : null;
    }

    /// <summary>Finds a SlaveBigAxe instance/prefab to copy Spine material settings from.</summary>
    public static GameObject ResolveMaterialTemplate()
    {
        try
        {
            EnemyPrefabRegistry.Initialize();
            GameObject prefab = EnemyPrefabRegistry.GetPrefab("SlaveBigAxe");
            if (prefab != null)
            {
                return prefab;
            }
        }
        catch
        {
        }

        try
        {
            SlaveBigAxe[] axes = Resources.FindObjectsOfTypeAll<SlaveBigAxe>();
            if (axes != null && axes.Length > 0 && axes[0] != null)
            {
                return axes[0].gameObject;
            }
        }
        catch
        {
        }

        return null;
    }

    /// <summary>Drops the cached disk asset so the next request reloads from disk.</summary>
    public static void ResetCache()
    {
        _armorSkeleton = null;
        _initFailed = false;
        CustomAssets.Clear();
    }
}

/// <summary>Serves disk-built SkeletonData / AnimationStateData for MeatArmor SkeletonDataAssets.</summary>
[HarmonyPatch(typeof(SkeletonDataAsset))]
internal static class SlaveBigAxeMeatArmorSkeletonDataAssetPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("GetSkeletonData", new Type[] { typeof(bool) })]
    private static bool GetSkeletonData_Prefix(SkeletonDataAsset __instance, ref SkeletonData __result)
    {
        if (SlaveBigAxeMeatArmorSkeletonLoader.CustomAssets.TryGetValue(__instance, out var data))
        {
            __result = data.SkeletonData;
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch("GetAnimationStateData")]
    private static bool GetAnimationStateData_Prefix(SkeletonDataAsset __instance, ref AnimationStateData __result)
    {
        if (SlaveBigAxeMeatArmorSkeletonLoader.CustomAssets.TryGetValue(__instance, out var data))
        {
            __result = data.StateData;
            return false;
        }

        return true;
    }
}
