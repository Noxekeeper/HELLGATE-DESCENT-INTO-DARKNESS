using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using NoREroMod.Systems.Spawn;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.UndeadSkeletonsModCustom;

internal enum UndeadSkeletonVariant
{
    BigMerman,
    Bigoni,
    Pilgrim,
    Vagrant
}

/// <summary>
/// Undead skeleton reskins: vanilla combat prefab + disk-loaded battle skeleton, no H-scene.
/// Layout: sources/HellGate_sources/Enemies/UndeadSkeletons/&lt;SpawnKey&gt;/&lt;SpawnKey&gt;.{json,atlas,png}
/// </summary>
internal static class UndeadSkeletonsLoader
{
    private sealed class VariantDefinition
    {
        internal readonly string SpawnKey;
        internal readonly string BaseRegistryKey;

        internal VariantDefinition(string spawnKey, string baseRegistryKey)
        {
            SpawnKey = spawnKey;
            BaseRegistryKey = baseRegistryKey;
        }
    }

    private static readonly Dictionary<UndeadSkeletonVariant, VariantDefinition> Definitions =
        new Dictionary<UndeadSkeletonVariant, VariantDefinition>
        {
            { UndeadSkeletonVariant.BigMerman, new VariantDefinition("BigMermanUndead", "BigMerman") },
            { UndeadSkeletonVariant.Bigoni, new VariantDefinition("BigoniUndead", "Bigoni") },
            { UndeadSkeletonVariant.Pilgrim, new VariantDefinition("PilgrimUndead", "Pilgrim") },
            { UndeadSkeletonVariant.Vagrant, new VariantDefinition("VagrantUndead", "Vagrant") }
        };

    internal static readonly UndeadSkeletonVariant[] AllVariants =
    {
        UndeadSkeletonVariant.BigMerman,
        UndeadSkeletonVariant.Bigoni,
        UndeadSkeletonVariant.Pilgrim,
        UndeadSkeletonVariant.Vagrant
    };

    private static readonly Dictionary<UndeadSkeletonVariant, SkeletonDataAsset> SkeletonCache =
        new Dictionary<UndeadSkeletonVariant, SkeletonDataAsset>();

    private static readonly HashSet<UndeadSkeletonVariant> InitFailed = new HashSet<UndeadSkeletonVariant>();

    internal static readonly Dictionary<SkeletonDataAsset, UndeadSkeletonsAssetData> CustomAssets =
        new Dictionary<SkeletonDataAsset, UndeadSkeletonsAssetData>();

    internal sealed class UndeadSkeletonsAssetData
    {
        public readonly SkeletonData SkeletonData;
        public readonly AnimationStateData StateData;

        public UndeadSkeletonsAssetData(SkeletonData skeletonData)
        {
            SkeletonData = skeletonData;
            StateData = new AnimationStateData(skeletonData);
        }
    }

    internal static string GetSpawnKey(UndeadSkeletonVariant variant)
    {
        return Definitions[variant].SpawnKey;
    }

    internal static string GetBaseDescription(UndeadSkeletonVariant variant)
    {
        return Definitions[variant].BaseRegistryKey;
    }

    internal static bool TryGetVariant(string spawnKey, out UndeadSkeletonVariant variant)
    {
        foreach (KeyValuePair<UndeadSkeletonVariant, VariantDefinition> kv in Definitions)
        {
            if (string.Equals(kv.Value.SpawnKey, spawnKey, StringComparison.OrdinalIgnoreCase))
            {
                variant = kv.Key;
                return true;
            }
        }

        variant = default;
        return false;
    }

    /// <summary>
    /// Renames the clone to the spawn key, tags it as a no-H undead skeleton and swaps the battle skeleton.
    /// erodata keeps its vanilla skeleton; it is never activated because H entry is blocked.
    /// </summary>
    internal static void ApplySkeleton(GameObject spawned, UndeadSkeletonVariant variant)
    {
        if (spawned == null)
            return;

        spawned.name = Definitions[variant].SpawnKey;
        UndeadSkeletonsRuntime.Register(spawned, variant);
        ApplyVisual(spawned, variant);
    }

    /// <summary>Battle skeleton swap only (authoring preview clones).</summary>
    internal static void ApplyVisual(GameObject spawned, UndeadSkeletonVariant variant)
    {
        if (spawned == null)
            return;

        VariantDefinition definition = Definitions[variant];
        EnemyPrefabRegistry.Initialize();
        GameObject templatePrefab = EnemyPrefabRegistry.GetPrefab(definition.BaseRegistryKey);
        if (templatePrefab == null)
            Plugin.Log?.LogWarning("[UndeadSkeletonsLoader] Template prefab not found: " + definition.BaseRegistryKey);

        SkeletonDataAsset skeleton = GetSkeleton(variant, definition, templatePrefab);
        if (skeleton == null)
        {
            Plugin.Log?.LogWarning(
                "[UndeadSkeletonsLoader] Assets NOT loaded for " + definition.SpawnKey
                + ". Check " + Path.Combine(GetBasePath() ?? "<game root>", definition.SpawnKey));
            return;
        }

        SkeletonAnimation battleSpine = spawned.GetComponent<SkeletonAnimation>();
        if (battleSpine == null)
        {
            Plugin.Log?.LogWarning("[UndeadSkeletonsLoader] Battle SkeletonAnimation not found on " + spawned.name);
            return;
        }

        battleSpine.skeletonDataAsset = skeleton;
        battleSpine.Initialize(true);
    }

    private static SkeletonDataAsset GetSkeleton(
        UndeadSkeletonVariant variant,
        VariantDefinition definition,
        GameObject materialTemplate)
    {
        if (SkeletonCache.TryGetValue(variant, out SkeletonDataAsset cached))
            return cached;

        if (InitFailed.Contains(variant))
            return null;

        string basePath = GetBasePath();
        if (string.IsNullOrEmpty(basePath))
        {
            Plugin.Log?.LogWarning("[UndeadSkeletonsLoader] Game root path not found");
            InitFailed.Add(variant);
            return null;
        }

        string folder = Path.Combine(basePath, definition.SpawnKey);
        string jsonPath = Path.Combine(folder, definition.SpawnKey + ".json");
        string atlasPath = Path.Combine(folder, definition.SpawnKey + ".atlas");
        string pngPath = ResolvePngPath(atlasPath, folder, definition.SpawnKey);

        SkeletonDataAsset loaded = LoadViaSpineApi(
            jsonPath, atlasPath, pngPath, definition.SpawnKey, materialTemplate, ResolveTemplateScale(materialTemplate));
        if (loaded == null)
        {
            InitFailed.Add(variant);
            return null;
        }

        SkeletonCache[variant] = loaded;
        return loaded;
    }

    private static string ResolvePngPath(string atlasPath, string folder, string baseName)
    {
        string preferred = Path.Combine(folder, baseName + ".png");
        if (File.Exists(preferred) || !File.Exists(atlasPath))
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

    private static string GetBasePath()
    {
        try
        {
            string gameRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(gameRoot))
                return null;

            string customPath = UndeadSkeletonsConfig.AssetsPath?.Value?.Trim();
            if (!string.IsNullOrEmpty(customPath))
            {
                return Path.IsPathRooted(customPath)
                    ? customPath
                    : Path.Combine(gameRoot, customPath);
            }

            return Path.Combine(
                Path.Combine(gameRoot, "sources"),
                Path.Combine("HellGate_sources", Path.Combine("Enemies", "UndeadSkeletons")));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Vanilla battle skeletons are imported at 0.008, not the 0.01 disk-loader default.</summary>
    private static float ResolveTemplateScale(GameObject template)
    {
        SkeletonAnimation spine = template != null ? template.GetComponent<SkeletonAnimation>() : null;
        float scale = spine?.skeletonDataAsset != null ? spine.skeletonDataAsset.scale : 0f;
        return scale > 0f ? scale : 0.01f;
    }

    private static SkeletonDataAsset LoadViaSpineApi(
        string jsonPath,
        string atlasPath,
        string pngPath,
        string name,
        GameObject materialTemplate,
        float scale)
    {
        if (!File.Exists(jsonPath))
        {
            Plugin.Log?.LogWarning("[UndeadSkeletonsLoader] JSON not found: " + jsonPath);
            return null;
        }

        if (!File.Exists(atlasPath))
        {
            Plugin.Log?.LogWarning("[UndeadSkeletonsLoader] Atlas not found: " + atlasPath);
            return null;
        }

        if (string.IsNullOrEmpty(pngPath) || !File.Exists(pngPath))
        {
            Plugin.Log?.LogWarning("[UndeadSkeletonsLoader] PNG not found for: " + atlasPath);
            return null;
        }

        try
        {
            string jsonText = SpineJsonLegacyConverter.ConvertIfNeeded(File.ReadAllText(jsonPath), name);
            string atlasText = File.ReadAllText(atlasPath);
            string imagesDir = Path.GetDirectoryName(pngPath);

            Material materialTemplateMat = CreateMaterialTemplate(materialTemplate);
            var textureLoader = new UndeadSkeletonsTextureLoader(imagesDir, materialTemplateMat, name);
            var atlas = new Atlas(new StringReader(atlasText), imagesDir, textureLoader);
            atlas.FlipV();

            var skeletonJson = new SkeletonJson(atlas) { Scale = scale };
            SkeletonData skeletonData = skeletonJson.ReadSkeletonData(new StringReader(jsonText));
            if (skeletonData == null)
            {
                Plugin.Log?.LogWarning("[UndeadSkeletonsLoader] Failed to parse skeleton: " + name);
                return null;
            }

            var asset = ScriptableObject.CreateInstance<SkeletonDataAsset>();
            asset.name = name + "_skeleton";
            asset.scale = scale;
            CustomAssets[asset] = new UndeadSkeletonsAssetData(skeletonData);
            UnityEngine.Object.DontDestroyOnLoad(asset);
            Plugin.Log?.LogInfo("[UndeadSkeletonsLoader] Loaded " + name + " (spine " + skeletonData.Version + ", scale " + scale + ")");
            return asset;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[UndeadSkeletonsLoader] Error loading " + name + ": " + ex.Message);
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
internal static class UndeadSkeletonsSkeletonDataAssetPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("GetSkeletonData", new[] { typeof(bool) })]
    private static bool GetSkeletonData_Prefix(SkeletonDataAsset __instance, ref SkeletonData __result)
    {
        if (UndeadSkeletonsLoader.CustomAssets.TryGetValue(__instance, out UndeadSkeletonsLoader.UndeadSkeletonsAssetData data))
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
        if (UndeadSkeletonsLoader.CustomAssets.TryGetValue(__instance, out UndeadSkeletonsLoader.UndeadSkeletonsAssetData data))
        {
            __result = data.StateData;
            return false;
        }

        return true;
    }
}
