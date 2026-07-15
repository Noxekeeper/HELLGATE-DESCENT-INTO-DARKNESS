using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using NoREroMod.Systems.Spawn;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.HellishTouzokuModCustom;

internal enum HellishTouzokuVariant
{
    Boss,
    Axe,
    Sword
}

/// <summary>
/// Loads Hellish Touzoku Spine assets from disk for Boss / Axe / Sword variants.
/// </summary>
internal static class HellishTouzokuSkeletonLoader
{
    private sealed class VariantDefinition
    {
        internal readonly string Subfolder;
        internal readonly string EnemyBaseName;
        internal readonly string EroBaseName;
        internal readonly string MaterialTemplateRegistryKey;

        internal VariantDefinition(string subfolder, string enemyBaseName, string eroBaseName, string materialTemplateRegistryKey)
        {
            Subfolder = subfolder;
            EnemyBaseName = enemyBaseName;
            EroBaseName = eroBaseName;
            MaterialTemplateRegistryKey = materialTemplateRegistryKey;
        }
    }

    private static readonly Dictionary<HellishTouzokuVariant, VariantDefinition> Definitions =
        new Dictionary<HellishTouzokuVariant, VariantDefinition>
        {
            {
                HellishTouzokuVariant.Boss,
                new VariantDefinition("HelllishTouzokuBoSS", "HellishTouzokuBoss", "HellishTouzokuBossERO", "BossTouzoku")
            },
            {
                HellishTouzokuVariant.Axe,
                new VariantDefinition("HelllishTouzokuAxe", "HelllishTouzokuAxe", "HelllishTouzokuAxeERO", "TouzokuAxe")
            },
            {
                HellishTouzokuVariant.Sword,
                new VariantDefinition("HelllishTouzokuSword", "HelllishTouzokuSword", "HelllishTouzokuSwordEro", "TouzokuNormal")
            }
        };

    private static readonly Dictionary<HellishTouzokuVariant, SkeletonDataAsset> BattleSkeletonCache =
        new Dictionary<HellishTouzokuVariant, SkeletonDataAsset>();

    private static readonly Dictionary<HellishTouzokuVariant, SkeletonDataAsset> EroSkeletonCache =
        new Dictionary<HellishTouzokuVariant, SkeletonDataAsset>();

    private static readonly HashSet<HellishTouzokuVariant> BattleInitFailed = new HashSet<HellishTouzokuVariant>();
    private static readonly HashSet<HellishTouzokuVariant> EroInitFailed = new HashSet<HellishTouzokuVariant>();

    internal static readonly Dictionary<SkeletonDataAsset, HellishTouzokuAssetData> CustomAssets =
        new Dictionary<SkeletonDataAsset, HellishTouzokuAssetData>();

    internal sealed class HellishTouzokuAssetData
    {
        public readonly SkeletonData SkeletonData;
        public readonly AnimationStateData StateData;

        public HellishTouzokuAssetData(SkeletonData skeletonData)
        {
            SkeletonData = skeletonData;
            StateData = new AnimationStateData(skeletonData);
        }
    }

    internal static void ApplySkeletons(GameObject spawned, HellishTouzokuVariant variant)
    {
        if (spawned == null)
            return;

        ApplySpawnScale(spawned);

        if (!Definitions.TryGetValue(variant, out VariantDefinition definition))
        {
            Plugin.Log?.LogWarning("[HellishTouzokuSkeletonLoader] Unknown variant: " + variant);
            return;
        }

        EnemyPrefabRegistry.Initialize();
        GameObject templatePrefab = EnemyPrefabRegistry.GetPrefab(definition.MaterialTemplateRegistryKey);
        if (templatePrefab == null)
            Plugin.Log?.LogWarning("[HellishTouzokuSkeletonLoader] Template prefab not found: " + definition.MaterialTemplateRegistryKey);

        SkeletonDataAsset battleSkeleton = GetBattleSkeleton(variant, definition, templatePrefab);
        SkeletonDataAsset eroSkeleton = GetEroSkeleton(variant, definition, templatePrefab);
        if (battleSkeleton == null || eroSkeleton == null)
        {
            Plugin.Log?.LogWarning(
                "[HellishTouzokuSkeletonLoader] Assets NOT loaded for "
                + variant
                + ". Check sources/HellGate_sources/Hellish Touzoku Spine/"
                + definition.Subfolder);
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
            Plugin.Log?.LogWarning("[HellishTouzokuSkeletonLoader] Battle SkeletonAnimation not found on " + spawned.name);
        }

        var erodataField = typeof(EnemyDate).GetField(
            "erodata",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (erodataField == null)
            return;

        var erodata = erodataField.GetValue(spawned.GetComponent<EnemyDate>()) as GameObject;
        if (erodata == null)
            return;

        SkeletonAnimation eroSpine = erodata.GetComponent<SkeletonAnimation>();
        if (eroSpine == null)
            return;

        eroSpine.skeletonDataAsset = eroSkeleton;
        eroSpine.Initialize(true);
    }

    internal static void ApplySpawnScale(GameObject spawned)
    {
        if (spawned == null)
            return;

        float multiplier = Plugin.hellishTouzokuScaleMultiplier?.Value ?? 0.8f;
        if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
            return;

        spawned.transform.localScale *= multiplier;
    }

    private static SkeletonDataAsset GetBattleSkeleton(
        HellishTouzokuVariant variant,
        VariantDefinition definition,
        GameObject materialTemplate)
    {
        if (BattleSkeletonCache.TryGetValue(variant, out SkeletonDataAsset cached))
            return cached;

        if (BattleInitFailed.Contains(variant))
            return null;

        SkeletonDataAsset loaded = LoadSideSkeleton(definition, "Enemy", definition.EnemyBaseName, materialTemplate);
        if (loaded == null)
        {
            BattleInitFailed.Add(variant);
            return null;
        }

        BattleSkeletonCache[variant] = loaded;
        return loaded;
    }

    private static SkeletonDataAsset GetEroSkeleton(
        HellishTouzokuVariant variant,
        VariantDefinition definition,
        GameObject materialTemplate)
    {
        if (EroSkeletonCache.TryGetValue(variant, out SkeletonDataAsset cached))
            return cached;

        if (EroInitFailed.Contains(variant))
            return null;

        SkeletonDataAsset loaded = LoadSideSkeleton(definition, "ERO", definition.EroBaseName, materialTemplate);
        if (loaded == null)
        {
            EroInitFailed.Add(variant);
            return null;
        }

        EroSkeletonCache[variant] = loaded;
        return loaded;
    }

    private static SkeletonDataAsset LoadSideSkeleton(
        VariantDefinition definition,
        string sideFolder,
        string baseName,
        GameObject materialTemplate)
    {
        string variantRoot = GetVariantRootPath(definition.Subfolder);
        if (string.IsNullOrEmpty(variantRoot))
            return null;

        string sidePath = Path.Combine(variantRoot, sideFolder);
        string jsonPath = Path.Combine(sidePath, baseName + ".json");
        string atlasPath = Path.Combine(sidePath, baseName + ".atlas");
        string pngPath = ResolvePngPath(atlasPath, sidePath);

        return LoadViaSpineApi(jsonPath, atlasPath, pngPath, baseName, materialTemplate);
    }

    private static string ResolvePngPath(string atlasPath, string sidePath)
    {
        if (!File.Exists(atlasPath))
            return null;

        foreach (string line in File.ReadAllLines(atlasPath))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.EndsWith(".png", StringComparison.OrdinalIgnoreCase) == false)
                continue;

            if (trimmed.IndexOf(':') >= 0)
                continue;

            string candidate = Path.Combine(sidePath, trimmed);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string GetVariantRootPath(string subfolder)
    {
        string basePath = GetHellishTouzokuBasePath();
        if (string.IsNullOrEmpty(basePath))
        {
            Plugin.Log?.LogWarning("[HellishTouzokuSkeletonLoader] Game root path not found");
            return null;
        }

        return Path.Combine(basePath, subfolder);
    }

    private static string GetHellishTouzokuBasePath()
    {
        try
        {
            string gameRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(gameRoot))
                return null;

            string customPath = Plugin.hellishTouzokuAssetsPath?.Value?.Trim();
            if (!string.IsNullOrEmpty(customPath))
            {
                return Path.IsPathRooted(customPath)
                    ? customPath
                    : Path.Combine(gameRoot, customPath);
            }

            return Path.Combine(
                Path.Combine(gameRoot, "sources"),
                Path.Combine("HellGate_sources", "Hellish Touzoku Spine"));
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
            Plugin.Log?.LogWarning("[HellishTouzokuSkeletonLoader] JSON not found: " + jsonPath);
            return null;
        }

        if (!File.Exists(atlasPath))
        {
            Plugin.Log?.LogWarning("[HellishTouzokuSkeletonLoader] Atlas not found: " + atlasPath);
            return null;
        }

        if (string.IsNullOrEmpty(pngPath) || !File.Exists(pngPath))
        {
            Plugin.Log?.LogWarning("[HellishTouzokuSkeletonLoader] PNG not found for: " + atlasPath);
            return null;
        }

        try
        {
            string jsonText = File.ReadAllText(jsonPath);
            string atlasText = File.ReadAllText(atlasPath);
            string imagesDir = Path.GetDirectoryName(pngPath);

            Material materialTemplateMat = CreateMaterialTemplate(materialTemplate);
            var textureLoader = new HellishTouzokuTextureLoader(imagesDir, materialTemplateMat, name);
            var atlas = new Atlas(new StringReader(atlasText), imagesDir, textureLoader);
            atlas.FlipV();

            var skeletonJson = new SkeletonJson(atlas) { Scale = 0.01f };
            SkeletonData skeletonData = skeletonJson.ReadSkeletonData(new StringReader(jsonText));
            if (skeletonData == null)
            {
                Plugin.Log?.LogWarning("[HellishTouzokuSkeletonLoader] Failed to parse skeleton: " + name);
                return null;
            }

            var asset = ScriptableObject.CreateInstance<SkeletonDataAsset>();
            asset.name = name + "_skeleton";
            asset.scale = 0.01f;
            CustomAssets[asset] = new HellishTouzokuAssetData(skeletonData);
            UnityEngine.Object.DontDestroyOnLoad(asset);
            return asset;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[HellishTouzokuSkeletonLoader] Error loading " + name + ": " + ex.Message);
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
internal static class HellishTouzokuSkeletonDataAssetPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("GetSkeletonData", new[] { typeof(bool) })]
    private static bool GetSkeletonData_Prefix(SkeletonDataAsset __instance, ref SkeletonData __result)
    {
        if (HellishTouzokuSkeletonLoader.CustomAssets.TryGetValue(__instance, out HellishTouzokuSkeletonLoader.HellishTouzokuAssetData data))
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
        if (HellishTouzokuSkeletonLoader.CustomAssets.TryGetValue(__instance, out HellishTouzokuSkeletonLoader.HellishTouzokuAssetData data))
        {
            __result = data.StateData;
            return false;
        }

        return true;
    }
}
