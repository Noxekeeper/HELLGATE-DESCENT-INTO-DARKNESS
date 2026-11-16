using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Spawn;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.DemonTentacleModCustom;

/// <summary>
/// DemonTentacle: AngelStatue combat prefab + disk-loaded battle skeleton (same animations/events as angel_Statue).
/// Layout: sources/HellGate_sources/Enemies/Demon_tentacle/Demon_tentacle.{json,atlas,png}
/// </summary>
internal static class DemonTentacleLoader
{
    internal const string SpawnKey = "DemonTentacle";
    private const string BaseRegistryKey = "AngelStatue";
    private const string AssetBaseName = "Demon_tentacle";
    private const string DefaultFaction = "demons";

    private static SkeletonDataAsset _skeleton;
    private static bool _initFailed;
    private static FieldInfo _mySpineField;

    internal static readonly Dictionary<SkeletonDataAsset, DemonTentacleAssetData> CustomAssets =
        new Dictionary<SkeletonDataAsset, DemonTentacleAssetData>();

    internal sealed class DemonTentacleAssetData
    {
        public readonly SkeletonData SkeletonData;
        public readonly AnimationStateData StateData;

        public DemonTentacleAssetData(SkeletonData skeletonData)
        {
            SkeletonData = skeletonData;
            StateData = new AnimationStateData(skeletonData);
        }
    }

    /// <summary>Renames the clone to the spawn key, tags it as DemonTentacle and swaps the battle skeleton.</summary>
    internal static void ApplySkeleton(GameObject spawned)
    {
        if (spawned == null)
            return;

        spawned.name = SpawnKey;
        DemonTentacleRuntime.Register(spawned);
        ApplyDefaultFaction(spawned);
        ApplyVisual(spawned);
    }

    /// <summary>AngelStatue resolves to Church by type name; DemonTentacle defaults to Demons unless the spawn line set |faction=.</summary>
    private static void ApplyDefaultFaction(GameObject spawned)
    {
        if (spawned.GetComponent<SpawnFactionOverride>() != null)
            return;

        spawned.AddComponent<SpawnFactionOverride>().FactionIdRaw = DefaultFaction;
    }

    /// <summary>Battle skeleton swap only (authoring preview clones).</summary>
    internal static void ApplyVisual(GameObject spawned)
    {
        if (spawned == null)
            return;

        EnemyPrefabRegistry.Initialize();
        GameObject templatePrefab = EnemyPrefabRegistry.GetPrefab(BaseRegistryKey);
        if (templatePrefab == null)
            Plugin.Log?.LogWarning("[DemonTentacleLoader] Template prefab not found: " + BaseRegistryKey);

        SkeletonDataAsset skeleton = GetSkeleton(templatePrefab);
        if (skeleton == null)
        {
            Plugin.Log?.LogWarning(
                "[DemonTentacleLoader] Assets NOT loaded. Check " + (GetBasePath() ?? "<game root>"));
            return;
        }

        SkeletonAnimation battleSpine = ResolveBattleSpine(spawned);
        if (battleSpine == null)
        {
            Plugin.Log?.LogWarning("[DemonTentacleLoader] Battle SkeletonAnimation not found on " + spawned.name);
            return;
        }

        battleSpine.skeletonDataAsset = skeleton;
        battleSpine.Initialize(true);
    }

    /// <summary>AngelStatue drives animation through its serialized mySpine, which is not guaranteed to sit on the root.</summary>
    internal static SkeletonAnimation ResolveBattleSpine(GameObject go)
    {
        if (go == null)
            return null;

        AngelStatue angel = go.GetComponent<AngelStatue>() ?? go.GetComponentInChildren<AngelStatue>(true);
        if (angel != null)
        {
            _mySpineField ??= AccessTools.Field(typeof(AngelStatue), "mySpine");
            if (_mySpineField?.GetValue(angel) is SkeletonAnimation fromField && fromField != null)
                return fromField;
        }

        return go.GetComponent<SkeletonAnimation>() ?? go.GetComponentInChildren<SkeletonAnimation>(true);
    }

    private static SkeletonDataAsset GetSkeleton(GameObject template)
    {
        if (_skeleton != null)
            return _skeleton;
        if (_initFailed)
            return null;

        string folder = GetBasePath();
        if (string.IsNullOrEmpty(folder))
        {
            Plugin.Log?.LogWarning("[DemonTentacleLoader] Game root path not found");
            _initFailed = true;
            return null;
        }

        string jsonPath = Path.Combine(folder, AssetBaseName + ".json");
        string atlasPath = Path.Combine(folder, AssetBaseName + ".atlas");
        string pngPath = ResolvePngPath(atlasPath, folder, AssetBaseName);

        SkeletonAnimation templateSpine = ResolveBattleSpine(template);
        _skeleton = LoadViaSpineApi(jsonPath, atlasPath, pngPath, templateSpine);
        if (_skeleton == null)
            _initFailed = true;
        return _skeleton;
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

            string customPath = DemonTentacleConfig.AssetsPath?.Value?.Trim();
            if (!string.IsNullOrEmpty(customPath))
            {
                return Path.IsPathRooted(customPath)
                    ? customPath
                    : Path.Combine(gameRoot, customPath);
            }

            return Path.Combine(
                Path.Combine(gameRoot, "sources"),
                Path.Combine("HellGate_sources", Path.Combine("Enemies", AssetBaseName)));
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
        SkeletonAnimation templateSpine)
    {
        if (!File.Exists(jsonPath))
        {
            Plugin.Log?.LogWarning("[DemonTentacleLoader] JSON not found: " + jsonPath);
            return null;
        }

        if (!File.Exists(atlasPath))
        {
            Plugin.Log?.LogWarning("[DemonTentacleLoader] Atlas not found: " + atlasPath);
            return null;
        }

        if (string.IsNullOrEmpty(pngPath) || !File.Exists(pngPath))
        {
            Plugin.Log?.LogWarning("[DemonTentacleLoader] PNG not found for: " + atlasPath);
            return null;
        }

        try
        {
            float scale = ResolveTemplateScale(templateSpine) * DemonTentacleConfig.GetScaleMultiplier();
            string jsonText = SpineJsonLegacyConverter.ConvertIfNeeded(File.ReadAllText(jsonPath), AssetBaseName);
            string atlasText = File.ReadAllText(atlasPath);
            string imagesDir = Path.GetDirectoryName(pngPath);

            var textureLoader = new DemonTentacleTextureLoader(imagesDir, CreateMaterialTemplate(templateSpine), AssetBaseName);
            var atlas = new Atlas(new StringReader(atlasText), imagesDir, textureLoader);
            atlas.FlipV();

            var skeletonJson = new SkeletonJson(atlas) { Scale = scale };
            SkeletonData skeletonData = skeletonJson.ReadSkeletonData(new StringReader(jsonText));
            if (skeletonData == null)
            {
                Plugin.Log?.LogWarning("[DemonTentacleLoader] Failed to parse skeleton: " + AssetBaseName);
                return null;
            }

            var asset = ScriptableObject.CreateInstance<SkeletonDataAsset>();
            asset.name = AssetBaseName + "_skeleton";
            asset.scale = scale;
            CustomAssets[asset] = new DemonTentacleAssetData(skeletonData);
            UnityEngine.Object.DontDestroyOnLoad(asset);
            Plugin.Log?.LogInfo("[DemonTentacleLoader] Loaded " + AssetBaseName + " (spine " + skeletonData.Version + ", scale " + scale + ")");
            return asset;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[DemonTentacleLoader] Error loading " + AssetBaseName + ": " + ex.Message);
            return null;
        }
    }

    /// <summary>Vanilla battle skeletons are imported at 0.008, not the 0.01 disk-loader default.</summary>
    private static float ResolveTemplateScale(SkeletonAnimation templateSpine)
    {
        float scale = templateSpine?.skeletonDataAsset != null ? templateSpine.skeletonDataAsset.scale : 0f;
        return scale > 0f ? scale : 0.01f;
    }

    private static Material CreateMaterialTemplate(SkeletonAnimation templateSpine)
    {
        if (templateSpine?.skeletonDataAsset?.atlasAssets == null || templateSpine.skeletonDataAsset.atlasAssets.Length == 0)
            return null;

        if (templateSpine.skeletonDataAsset.atlasAssets[0].materials == null ||
            templateSpine.skeletonDataAsset.atlasAssets[0].materials.Length == 0)
            return null;

        Material src = templateSpine.skeletonDataAsset.atlasAssets[0].materials[0];
        return src != null && src.shader != null ? src : null;
    }
}

[HarmonyPatch(typeof(SkeletonDataAsset))]
internal static class DemonTentacleSkeletonDataAssetPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("GetSkeletonData", new[] { typeof(bool) })]
    private static bool GetSkeletonData_Prefix(SkeletonDataAsset __instance, ref SkeletonData __result)
    {
        if (DemonTentacleLoader.CustomAssets.TryGetValue(__instance, out DemonTentacleLoader.DemonTentacleAssetData data))
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
        if (DemonTentacleLoader.CustomAssets.TryGetValue(__instance, out DemonTentacleLoader.DemonTentacleAssetData data))
        {
            __result = data.StateData;
            return false;
        }

        return true;
    }
}
