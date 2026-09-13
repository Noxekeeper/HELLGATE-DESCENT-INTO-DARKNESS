using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using NoREroMod.Systems.Spawn;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.DemonGorotukiModCustom;

/// <summary>
/// Loads Demon_gorotuki Spine assets (battle + ERO) from disk and swaps them onto a Gorotuki clone.
/// Default layout:
///   sources/HellGate_sources/Gorutoki/Gorutoki/Demon_gorotuki.{json,atlas,png}
///   sources/HellGate_sources/Gorutoki/Gorutoki ERO/Demon_gorotuki_ERO.{json,atlas,png}
/// </summary>
internal static class DemonGorotukiSkeletonLoader
{
    internal const string SpawnKey = "Demon_gorotuki";
    private const string BattleBaseName = "Demon_gorotuki";
    private const string EroBaseName = "Demon_gorotuki_ERO";
    private const string BattleFolderName = "Gorutoki";
    private const string EroFolderName = "Gorutoki ERO";

    private static SkeletonDataAsset _battleSkeleton;
    private static SkeletonDataAsset _eroSkeleton;
    private static bool _battleInitFailed;
    private static bool _eroInitFailed;

    internal static readonly Dictionary<SkeletonDataAsset, DemonGorotukiAssetData> CustomAssets =
        new Dictionary<SkeletonDataAsset, DemonGorotukiAssetData>();

    internal sealed class DemonGorotukiAssetData
    {
        public readonly SkeletonData SkeletonData;
        public readonly AnimationStateData StateData;

        public DemonGorotukiAssetData(SkeletonData skeletonData)
        {
            SkeletonData = skeletonData;
            StateData = new AnimationStateData(skeletonData);
        }
    }

    public static void ApplySkeletons(GameObject spawned)
    {
        if (spawned == null)
            return;

        EnemyPrefabRegistry.Initialize();
        GameObject templatePrefab = EnemyPrefabRegistry.GetPrefab("Gorotuki");
        if (templatePrefab == null)
            Plugin.Log?.LogWarning("[DemonGorotukiSkeletonLoader] Gorotuki prefab not found for material template");

        SkeletonDataAsset battleSkeleton = GetBattleSkeleton(templatePrefab);
        SkeletonDataAsset eroSkeleton = GetEroSkeleton(templatePrefab);
        if (battleSkeleton == null || eroSkeleton == null)
        {
            Plugin.Log?.LogWarning(
                "[DemonGorotukiSkeletonLoader] Assets NOT loaded. Expected under sources/HellGate_sources/Gorutoki/ "
                + $"({BattleFolderName}/{BattleBaseName}.* and {EroFolderName}/{EroBaseName}.*). Skipping skeleton swap.");
            return;
        }

        SkeletonAnimation battleSpine = spawned.GetComponent<SkeletonAnimation>();
        if (battleSpine != null)
        {
            battleSpine.skeletonDataAsset = battleSkeleton;
            battleSpine.Initialize(true);
        }
        else
        {
            Plugin.Log?.LogWarning("[DemonGorotukiSkeletonLoader] Battle SkeletonAnimation not found on " + spawned.name);
        }

        var erodataField = typeof(EnemyDate).GetField(
            "erodata",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (erodataField == null)
            return;

        var enemyDate = spawned.GetComponent<EnemyDate>();
        if (enemyDate == null)
            return;

        var erodata = erodataField.GetValue(enemyDate) as GameObject;
        if (erodata == null)
            return;

        SkeletonAnimation eroSpine = erodata.GetComponent<SkeletonAnimation>();
        if (eroSpine == null)
            return;

        eroSpine.skeletonDataAsset = eroSkeleton;
        eroSpine.Initialize(true);
    }

    private static SkeletonDataAsset GetBattleSkeleton(GameObject materialTemplate)
    {
        if (_battleSkeleton != null)
            return _battleSkeleton;
        if (_battleInitFailed)
            return null;

        string folder = GetSideFolderPath(BattleFolderName);
        if (string.IsNullOrEmpty(folder))
        {
            _battleInitFailed = true;
            return null;
        }

        _battleSkeleton = LoadSideSkeleton(folder, BattleBaseName, materialTemplate);
        if (_battleSkeleton == null)
            _battleInitFailed = true;
        return _battleSkeleton;
    }

    private static SkeletonDataAsset GetEroSkeleton(GameObject materialTemplate)
    {
        if (_eroSkeleton != null)
            return _eroSkeleton;
        if (_eroInitFailed)
            return null;

        string folder = GetSideFolderPath(EroFolderName);
        if (string.IsNullOrEmpty(folder))
        {
            _eroInitFailed = true;
            return null;
        }

        _eroSkeleton = LoadSideSkeleton(folder, EroBaseName, materialTemplate);
        if (_eroSkeleton == null)
            _eroInitFailed = true;
        return _eroSkeleton;
    }

    private static SkeletonDataAsset LoadSideSkeleton(string folder, string baseName, GameObject materialTemplate)
    {
        string jsonPath = Path.Combine(folder, baseName + ".json");
        string atlasPath = Path.Combine(folder, baseName + ".atlas");
        string pngPath = ResolvePngPath(atlasPath, folder, baseName);
        return LoadViaSpineApi(jsonPath, atlasPath, pngPath, baseName, materialTemplate);
    }

    private static string ResolvePngPath(string atlasPath, string folder, string baseName)
    {
        string preferred = Path.Combine(folder, baseName + ".png");
        if (File.Exists(preferred))
            return preferred;

        if (!File.Exists(atlasPath))
            return preferred;

        foreach (string line in File.ReadAllLines(atlasPath))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || !trimmed.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                continue;
            if (trimmed.IndexOf(':') >= 0)
                continue;

            string candidate = Path.Combine(folder, trimmed);
            if (File.Exists(candidate))
                return candidate;
        }

        return preferred;
    }

    private static string GetSideFolderPath(string sideFolder)
    {
        string basePath = GetDemonGorotukiBasePath();
        if (string.IsNullOrEmpty(basePath))
        {
            Plugin.Log?.LogWarning("[DemonGorotukiSkeletonLoader] Game root path not found");
            return null;
        }

        return Path.Combine(basePath, sideFolder);
    }

    private static string GetDemonGorotukiBasePath()
    {
        try
        {
            string gameRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(gameRoot))
                return null;

            string customPath = Plugin.demonGorotukiAssetsPath?.Value?.Trim();
            if (!string.IsNullOrEmpty(customPath))
            {
                return Path.IsPathRooted(customPath)
                    ? customPath
                    : Path.Combine(gameRoot, customPath);
            }

            return Path.Combine(
                Path.Combine(gameRoot, "sources"),
                Path.Combine("HellGate_sources", "Gorutoki"));
        }
        catch
        {
            return null;
        }
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
            Plugin.Log?.LogWarning("[DemonGorotukiSkeletonLoader] JSON not found: " + jsonPath);
            return null;
        }

        if (!File.Exists(atlasPath))
        {
            Plugin.Log?.LogWarning("[DemonGorotukiSkeletonLoader] Atlas not found: " + atlasPath);
            return null;
        }

        if (string.IsNullOrEmpty(pngPath) || !File.Exists(pngPath))
        {
            Plugin.Log?.LogWarning("[DemonGorotukiSkeletonLoader] PNG not found for: " + atlasPath);
            return null;
        }

        try
        {
            string jsonText = File.ReadAllText(jsonPath);
            string atlasText = File.ReadAllText(atlasPath);
            string imagesDir = Path.GetDirectoryName(pngPath);

            Material materialTemplateMat = CreateMaterialTemplate(materialTemplate);
            var textureLoader = new DemonGorotukiTextureLoader(imagesDir, materialTemplateMat, name);
            var atlas = new Atlas(new StringReader(atlasText), imagesDir, textureLoader);
            atlas.FlipV();

            var skeletonJson = new SkeletonJson(atlas) { Scale = 0.01f };
            SkeletonData skeletonData = skeletonJson.ReadSkeletonData(new StringReader(jsonText));
            if (skeletonData == null)
            {
                Plugin.Log?.LogWarning("[DemonGorotukiSkeletonLoader] Failed to parse skeleton: " + name);
                return null;
            }

            var asset = ScriptableObject.CreateInstance<SkeletonDataAsset>();
            asset.name = name + "_skeleton";
            asset.scale = 0.01f;
            CustomAssets[asset] = new DemonGorotukiAssetData(skeletonData);
            UnityEngine.Object.DontDestroyOnLoad(asset);
            return asset;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[DemonGorotukiSkeletonLoader] Error loading " + name + ": " + ex.Message);
            return null;
        }
    }

    private static Material CreateMaterialTemplate(GameObject template)
    {
        if (template == null)
            return null;

        SkeletonAnimation spine = template.GetComponent<SkeletonAnimation>();
        if (spine?.skeletonDataAsset?.atlasAssets == null || spine.skeletonDataAsset.atlasAssets.Length == 0)
            return null;

        if (spine.skeletonDataAsset.atlasAssets[0].materials == null ||
            spine.skeletonDataAsset.atlasAssets[0].materials.Length == 0)
            return null;

        Material src = spine.skeletonDataAsset.atlasAssets[0].materials[0];
        return src != null && src.shader != null ? src : null;
    }
}

[HarmonyPatch(typeof(SkeletonDataAsset))]
internal static class DemonGorotukiSkeletonDataAssetPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("GetSkeletonData", new[] { typeof(bool) })]
    private static bool GetSkeletonData_Prefix(SkeletonDataAsset __instance, ref SkeletonData __result)
    {
        if (DemonGorotukiSkeletonLoader.CustomAssets.TryGetValue(__instance, out DemonGorotukiSkeletonLoader.DemonGorotukiAssetData data))
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
        if (DemonGorotukiSkeletonLoader.CustomAssets.TryGetValue(__instance, out DemonGorotukiSkeletonLoader.DemonGorotukiAssetData data))
        {
            __result = data.StateData;
            return false;
        }

        return true;
    }
}
