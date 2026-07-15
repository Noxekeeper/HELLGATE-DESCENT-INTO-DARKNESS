using System;
using System.Collections.Generic;
using System.IO;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.RickEnemyModShared;

/// <summary>
/// Disk Spine loader + asset cache for all RickEnemyMod skeletons (shared logo + per-enemy fatality swaps).
/// </summary>
internal static class RickEnemyModSpineLoader
{
    internal static readonly Dictionary<SkeletonDataAsset, RickEnemyModAssetData> CustomAssets =
        new Dictionary<SkeletonDataAsset, RickEnemyModAssetData>();

    internal sealed class RickEnemyModAssetData
    {
        public readonly SkeletonData SkeletonData;
        public readonly AnimationStateData StateData;

        public RickEnemyModAssetData(SkeletonData skeletonData)
        {
            SkeletonData = skeletonData;
            StateData = new AnimationStateData(skeletonData);
        }
    }

    internal static SkeletonDataAsset LoadSkeleton(
        string jsonPath,
        string atlasPath,
        string pngPath,
        string assetName,
        Material materialTemplate)
    {
        if (!File.Exists(jsonPath))
        {
            Plugin.Log?.LogWarning($"[RickEnemyMod] JSON not found: {jsonPath}");
            return null;
        }

        if (!File.Exists(atlasPath))
        {
            Plugin.Log?.LogWarning($"[RickEnemyMod] Atlas not found: {atlasPath}");
            return null;
        }

        if (!File.Exists(pngPath))
        {
            Plugin.Log?.LogWarning($"[RickEnemyMod] PNG not found: {pngPath}");
            return null;
        }

        try
        {
            string jsonText = File.ReadAllText(jsonPath);
            string atlasText = File.ReadAllText(atlasPath);
            string imagesDir = Path.GetDirectoryName(pngPath);

            var textureLoader = new RickEnemyModTextureLoader(imagesDir, materialTemplate, assetName);
            var atlas = new Atlas(new StringReader(atlasText), imagesDir, textureLoader);
            atlas.FlipV();

            var skeletonJson = new SkeletonJson(atlas) { Scale = 0.01f };
            SkeletonData skeletonData = skeletonJson.ReadSkeletonData(new StringReader(jsonText));
            if (skeletonData == null)
            {
                Plugin.Log?.LogWarning($"[RickEnemyMod] Failed to parse skeleton: {assetName}");
                return null;
            }

            var asset = ScriptableObject.CreateInstance<SkeletonDataAsset>();
            asset.name = assetName + "_skeleton";
            asset.scale = 0.01f;
            CustomAssets[asset] = new RickEnemyModAssetData(skeletonData);

            UnityEngine.Object.DontDestroyOnLoad(asset);
            return asset;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RickEnemyMod] Error loading {assetName}: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    internal static Material BorrowSpineMaterial(GameObject template)
    {
        if (template == null)
            return null;

        var slaughterer = template.GetComponent<Slaughterer>();
        if (slaughterer != null)
        {
            var erodataField = typeof(EnemyDate).GetField(
                "erodata",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var erodata = erodataField?.GetValue(slaughterer) as GameObject;
            var eroSpine = erodata?.GetComponent<SkeletonAnimation>();
            Material fromEro = BorrowFromSpine(eroSpine);
            if (fromEro != null)
                return fromEro;
        }

        return BorrowFromSpine(template.GetComponent<SkeletonAnimation>());
    }

    private static Material BorrowFromSpine(SkeletonAnimation spine)
    {
        if (spine?.skeletonDataAsset?.atlasAssets == null ||
            spine.skeletonDataAsset.atlasAssets.Length == 0 ||
            spine.skeletonDataAsset.atlasAssets[0].materials == null ||
            spine.skeletonDataAsset.atlasAssets[0].materials.Length == 0)
            return null;

        Material src = spine.skeletonDataAsset.atlasAssets[0].materials[0];
        return src != null && src.shader != null ? src : null;
    }
}
