using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Spine;
using Spine.Unity;
using UnityEngine;
using NoREroMod.Systems.Spawn;

namespace NoREroMod.Patches.Enemy.WolfModCustom;

/// <summary>
/// Загружает Spine-ассеты Wolf (WolfE for боя, Wolf for H-scenesы) from папки ресурсов
/// via прямой Spine API, обходя TextAsset (Unity 5.6 несовместим).
/// </summary>
internal static class WolfSkeletonLoader
{
    private static SkeletonDataAsset _wolfESkeleton;
    private static SkeletonDataAsset _wolfSkeleton;
    private static bool _initFailed;

    /// <summary>
    /// Хранит предзагруженные SkeletonData и AnimationStateData for кастомных ассетов.
    /// Harmony патч возвращает their instead of вызова оригинального GetSkeletonData/GetAnimationStateData.
    /// </summary>
    internal static readonly Dictionary<SkeletonDataAsset, WolfAssetData> CustomAssets = new Dictionary<SkeletonDataAsset, WolfAssetData>();

    internal sealed class WolfAssetData
    {
        public readonly SkeletonData SkeletonData;
        public readonly AnimationStateData StateData;

        public WolfAssetData(SkeletonData skeletonData)
        {
            SkeletonData = skeletonData;
            StateData = new AnimationStateData(skeletonData);
        }
    }

    private static string GetWolfModBasePath()
    {
        try
        {
            string gameRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(gameRoot)) return null;
            string customPath = Plugin.wolfModAssetsPath?.Value?.Trim();
            if (!string.IsNullOrEmpty(customPath))
            {
                string fullPath = Path.IsPathRooted(customPath) ? customPath : Path.Combine(gameRoot, customPath);
                return fullPath;
            }
            return Path.Combine(Path.Combine(gameRoot, "sources"), Path.Combine("HellGate_sources", "Wolf Mod Spine"));
        }
        catch
        {
            return null;
        }
    }

    public static SkeletonDataAsset GetWolfESkeleton(GameObject mummyDogPrefabTemplate)
    {
        if (_wolfESkeleton != null) return _wolfESkeleton;
        if (_initFailed) return null;

        string basePath = GetWolfModBasePath();
        if (string.IsNullOrEmpty(basePath))
        {
            Plugin.Log?.LogWarning("[WolfSkeletonLoader] Game root path not found");
            _initFailed = true;
            return null;
        }

        string enemyPath = Path.Combine(basePath, "Enemy");
        string jsonPath = Path.Combine(enemyPath, "WolfE.json");
        string atlasPath = Path.Combine(enemyPath, "WolfE.atlas");
        string pngPath = Path.Combine(enemyPath, "WolfE.png");

        _wolfESkeleton = LoadViaSpineApi(jsonPath, atlasPath, pngPath, "WolfE", mummyDogPrefabTemplate);
        if (_wolfESkeleton == null) _initFailed = true;
        return _wolfESkeleton;
    }

    public static SkeletonDataAsset GetWolfSkeleton(GameObject mummyDogPrefabTemplate)
    {
        if (_wolfSkeleton != null) return _wolfSkeleton;
        if (_initFailed) return null;

        string basePath = GetWolfModBasePath();
        if (string.IsNullOrEmpty(basePath))
        {
            Plugin.Log?.LogWarning("[WolfSkeletonLoader] Game root path not found");
            _initFailed = true;
            return null;
        }

        string eroPath = Path.Combine(basePath, "ERO");
        string jsonPath = Path.Combine(eroPath, "Wolf.json");
        string atlasPath = Path.Combine(eroPath, "Wolf.atlas");
        string pngPath = Path.Combine(eroPath, "Wolf.png");

        _wolfSkeleton = LoadViaSpineApi(jsonPath, atlasPath, pngPath, "Wolf", mummyDogPrefabTemplate);
        if (_wolfSkeleton == null) _initFailed = true;
        return _wolfSkeleton;
    }

    /// <summary>
    /// Загрузка через Spine API (Atlas + SkeletonJson) without TextAsset.
    /// </summary>
    private static SkeletonDataAsset LoadViaSpineApi(string jsonPath, string atlasPath, string pngPath, string name, GameObject materialTemplate)
    {
        if (!File.Exists(jsonPath))
        {
            Plugin.Log?.LogWarning($"[WolfSkeletonLoader] JSON not found: {jsonPath}");
            return null;
        }
        if (!File.Exists(atlasPath))
        {
            Plugin.Log?.LogWarning($"[WolfSkeletonLoader] Atlas not found: {atlasPath}");
            return null;
        }
        if (!File.Exists(pngPath))
        {
            Plugin.Log?.LogWarning($"[WolfSkeletonLoader] PNG not found: {pngPath}");
            return null;
        }

        try
        {
            string jsonText = File.ReadAllText(jsonPath);
            string atlasText = File.ReadAllText(atlasPath);
            string imagesDir = Path.GetDirectoryName(pngPath);

            Material materialTemplateMat = CreateMaterialTemplate(materialTemplate, name, pngPath);
            var textureLoader = new WolfTextureLoader(imagesDir, materialTemplateMat, name);
            var atlas = new Atlas(new StringReader(atlasText), imagesDir, textureLoader);
            atlas.FlipV(); // Критичbut for Unity: атлаwith Spine использует другую system UV by V; without FlipV — спрайты "франкенштейнятся"

            var skeletonJson = new SkeletonJson(atlas) { Scale = 0.01f };
            SkeletonData skeletonData = skeletonJson.ReadSkeletonData(new StringReader(jsonText));
            if (skeletonData == null)
            {
                Plugin.Log?.LogWarning($"[WolfSkeletonLoader] Failed to parse skeleton: {name}");
                return null;
            }

            var stateData = new AnimationStateData(skeletonData);

            SkeletonDataAsset asset = ScriptableObject.CreateInstance<SkeletonDataAsset>();
            asset.name = name + "_skeleton";
            asset.scale = 0.01f;
            CustomAssets[asset] = new WolfAssetData(skeletonData);

            UnityEngine.Object.DontDestroyOnLoad(asset);

            return asset;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[WolfSkeletonLoader] Error loading {name}: {ex.Message}\n{ex.StackTrace}");
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

    public static void ApplyWolfSkeletons(GameObject spawned)
    {
        if (spawned == null) return;

        string basePath = GetWolfModBasePath();

        EnemyPrefabRegistry.Initialize();
        GameObject mummyDogPrefab = EnemyPrefabRegistry.GetPrefab("MummyDog");
        if (mummyDogPrefab == null)
            Plugin.Log?.LogWarning("[WolfSkeletonLoader] MummyDog prefab not found for material template");

        var wolfESkeleton = GetWolfESkeleton(mummyDogPrefab);
        var wolfSkeleton = GetWolfSkeleton(mummyDogPrefab);
        if (wolfESkeleton == null || wolfSkeleton == null)
        {
            Plugin.Log?.LogWarning("[WolfSkeletonLoader] Wolf assets NOT loaded! Add WolfE.png and Wolf.png to Enemy/ and ERO/ folders. Skipping skeleton swap.");
            return;
        }

        // Plugin.Log?.LogInfo("[WolfSkeletonLoader] Wolf skeletons applied successfully"); // Disabled for cleaner logs

        var mummyDog = spawned.GetComponent<MummyDog>();
        if (mummyDog == null)
        {
            Plugin.Log?.LogWarning("[WolfSkeletonLoader] MummyDog component not found");
            return;
        }

        var battleSpine = spawned.GetComponent<SkeletonAnimation>();
        if (battleSpine != null)
        {
            battleSpine.skeletonDataAsset = wolfESkeleton;
            battleSpine.Initialize(true);
        }

        var erodataField = typeof(EnemyDate).GetField("erodata", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (erodataField != null)
        {
            var erodata = erodataField.GetValue(mummyDog) as GameObject;
            if (erodata != null)
            {
                var eroSpine = erodata.GetComponent<SkeletonAnimation>();
                if (eroSpine != null)
                {
                    eroSpine.skeletonDataAsset = wolfSkeleton;
                    eroSpine.Initialize(true);
                }
            }
        }
    }
}

/// <summary>
/// Harmony-патч for SkeletonDataAsset.GetSkeletonData и GetAnimationStateData.
/// Возвращает предзагруженные данные for Wolf-ассетов, so that обойти отсутствие skeletonJSON.
/// </summary>
[HarmonyPatch(typeof(SkeletonDataAsset))]
internal static class WolfSkeletonDataAssetPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("GetSkeletonData", new Type[] { typeof(bool) })]
    static bool GetSkeletonData_Prefix(SkeletonDataAsset __instance, ref SkeletonData __result)
    {
        if (WolfSkeletonLoader.CustomAssets.TryGetValue(__instance, out var data))
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
        if (WolfSkeletonLoader.CustomAssets.TryGetValue(__instance, out var data))
        {
            __result = data.StateData;
            return false;
        }
        return true;
    }
}
