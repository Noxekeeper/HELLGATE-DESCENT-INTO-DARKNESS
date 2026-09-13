using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace NoREroMod.Systems.UI;

/// <summary>
/// NoRSceneLoader Awake would sync-load forestlvl + start prefab harvest before first paint.
/// We defer both until Early Boot is visible.
///
/// CRITICAL: stock LoadPrefabs uses LoadSceneAsync + allowSceneActivation=false, which never
/// finishes and queues ~11 stuck loads — that deadlocks HellGate splash SceneManager.LoadScene
/// (UI stuck at 0/14 RanchEro2). We never call stock LoadPrefabs; we harvest with activation=true.
/// </summary>
internal static class ForestLoaderDeferral
{
    private const string PluginTypeName = "NoRSceneLoader.NoRSceneLoaderPlugin";
    private static readonly Harmony Harmony = new Harmony("NoREroMod.HellGate.ForestDefer");

    private static bool _installed;
    private static bool _patched;
    private static bool _defer = true;
    private static object? _pluginInstance;
    private static Type? _pluginType;
    private static MethodInfo? _loadAssetBundles;
    private static MethodInfo? _loadPrefabs;
    private static MethodInfo? _loadPrefabsInScene;
    private static MethodInfo? _loadEnemiesInScene;
    private static FieldInfo? _scenesLoadedField;
    private static FieldInfo? _spawnenemyEnemyField;
    private static bool _bundlesRan;
    private static bool _prefabsRan;
    private static bool _prefabHandlerHooked;

    internal static bool IsReady => _pluginInstance != null;

    internal static void Install()
    {
        if (_installed)
            return;
        _installed = true;
        _defer = true;

        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < loaded.Length; i++)
            TryPatchAssembly(loaded[i]);

        Plugin.Log?.LogInfo("[BOOT] NoRSceneLoader Forest deferral armed (bundle/prefab load after Early Boot paints).");
    }

    private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
    {
        try { TryPatchAssembly(args.LoadedAssembly); }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BOOT] Forest defer patch on load failed: {ex.Message}");
        }
    }

    private static void TryPatchAssembly(Assembly asm)
    {
        if (asm == null || _patched)
            return;

        string name = asm.GetName().Name ?? "";
        if (!string.Equals(name, "NoRSceneLoader", StringComparison.OrdinalIgnoreCase))
            return;

        Type? type = asm.GetType(PluginTypeName, false) ?? AccessTools.TypeByName(PluginTypeName);
        if (type == null)
        {
            Plugin.Log?.LogWarning("[BOOT] NoRSceneLoaderPlugin type not found for deferral.");
            return;
        }

        _patched = true;
        _pluginType = type;
        _loadAssetBundles = AccessTools.Method(type, "LoadAssetBundles");
        _loadPrefabs = AccessTools.Method(type, "LoadPrefabs");
        _loadPrefabsInScene = AccessTools.Method(type, "LoadPrefabsInScene");
        _loadEnemiesInScene = AccessTools.Method(type, "LoadEnemiesInScene");
        _scenesLoadedField = AccessTools.Field(type, "scenesLoaded");
        _spawnenemyEnemyField = AccessTools.Field(typeof(Spawnenemy), "enemy");
        MethodInfo? awake = AccessTools.Method(type, "Awake");

        // Bundles: skip only while deferred (Awake).
        if (_loadAssetBundles != null)
        {
            Harmony.Patch(
                _loadAssetBundles,
                prefix: new HarmonyMethod(typeof(ForestLoaderDeferral), nameof(Prefix_SkipBundlesIfDeferred)));
        }

        // Prefabs: NEVER run stock — allowSceneActivation=false deadlocks later LoadScene.
        if (_loadPrefabs != null)
        {
            Harmony.Patch(
                _loadPrefabs,
                prefix: new HarmonyMethod(typeof(ForestLoaderDeferral), nameof(Prefix_SkipStockLoadPrefabs)));
        }

        if (_loadEnemiesInScene != null)
        {
            Harmony.Patch(
                _loadEnemiesInScene,
                postfix: new HarmonyMethod(typeof(ForestLoaderDeferral), nameof(Postfix_PushEnemyNames)));
        }

        if (awake != null)
        {
            Harmony.Patch(
                awake,
                postfix: new HarmonyMethod(typeof(ForestLoaderDeferral), nameof(Postfix_CaptureInstance)));
        }

        Plugin.Log?.LogInfo("[BOOT] NoRSceneLoader: bundles deferred; stock LoadPrefabs permanently redirected (activation fix).");
    }

    private static bool Prefix_SkipBundlesIfDeferred() => !_defer;

    private static bool Prefix_SkipStockLoadPrefabs() => false;

    private static void Postfix_CaptureInstance(object __instance) => _pluginInstance = __instance;

    /// <summary>
    /// After Early Boot paints: sync forestlvl bundle, then start fixed additive scene harvest.
    /// </summary>
    internal static void RunDeferredLoads(Action<string, float>? status)
    {
        if (_pluginInstance == null)
        {
            status?.Invoke("Forest loader not present", 0.12f);
            Plugin.Log?.LogInfo("[BOOT] NoRSceneLoader instance missing — skip deferred Forest loads.");
            ForestLoaderBootProgress.MarkFinished();
            return;
        }

        _defer = false;

        try
        {
            if (!_bundlesRan && _loadAssetBundles != null)
            {
                status?.Invoke("forestlvl", 0.06f);
                float t0 = Time.realtimeSinceStartup;
                _loadAssetBundles.Invoke(_pluginInstance, null);
                _bundlesRan = true;
                Plugin.Log?.LogInfo(
                    $"[BOOT TIMING] NoRSceneLoader.LoadAssetBundles (deferred): {(Time.realtimeSinceStartup - t0) * 1000f:F0}ms");
            }

            if (!_prefabsRan)
            {
                status?.Invoke("Forest scenes", 0.1f);
                StartFixedPrefabHarvest();
                _prefabsRan = true;
                Plugin.Log?.LogInfo("[BOOT] NoRSceneLoader prefab harvest started (LoadSceneAsync allowSceneActivation=true).");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BOOT] Deferred NoRSceneLoader loads failed: {ex.Message}");
            ForestLoaderBootProgress.MarkFinished();
        }
    }

    private static void StartFixedPrefabHarvest()
    {
        if (_pluginInstance == null)
            return;

        MonoBehaviour? mb = _pluginInstance as MonoBehaviour;
        if (mb == null)
        {
            Plugin.Log?.LogWarning("[BOOT] NoRSceneLoader instance is not a MonoBehaviour.");
            ForestLoaderBootProgress.MarkFinished();
            return;
        }

        HookPrefabSceneHandler();

        List<string> scenes = ReadPrefabSceneNames();
        if (scenes.Count == 0)
        {
            Plugin.Log?.LogWarning("[BOOT] NoRSceneLoader scenesLoaded empty — nothing to harvest.");
            ForestLoaderBootProgress.MarkFinished();
            return;
        }

        for (int i = 0; i < scenes.Count; i++)
            mb.StartCoroutine(FixedLoadScene(scenes[i]));
    }

    private static void HookPrefabSceneHandler()
    {
        if (_prefabHandlerHooked || _pluginInstance == null || _loadPrefabsInScene == null)
            return;

        try
        {
            var handler = (UnityAction<Scene, LoadSceneMode>)Delegate.CreateDelegate(
                typeof(UnityAction<Scene, LoadSceneMode>),
                _pluginInstance,
                _loadPrefabsInScene);
            SceneManager.sceneLoaded += handler;
            _prefabHandlerHooked = true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BOOT] Could not hook LoadPrefabsInScene: {ex.Message}");
        }
    }

    private static List<string> ReadPrefabSceneNames()
    {
        var list = new List<string>();
        if (_scenesLoadedField == null || _pluginInstance == null)
            return list;

        try
        {
            object? dictObj = _scenesLoadedField.GetValue(_pluginInstance);
            if (dictObj is IDictionary dict)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    if (entry.Key is string key && !string.IsNullOrEmpty(key))
                        list.Add(key);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BOOT] Could not read scenesLoaded: {ex.Message}");
        }

        return list;
    }

    private static IEnumerator FixedLoadScene(string sceneName)
    {
        if (HellGateSplashScreen.IsEarlyBootActive)
            HellGateSplashScreen.PushEarlyBootItem(sceneName);

        AsyncOperation? op = null;
        try
        {
            op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[BOOT] Forest LoadSceneAsync({sceneName}) failed: {ex.Message}");
            yield break;
        }

        if (op == null)
            yield break;

        // Stock NoRSceneLoader left this false → never isDone → deadlocks later sync loads.
        op.allowSceneActivation = true;
        while (!op.isDone)
            yield return null;
    }

    /// <summary>Stream real enemy prefab names into Early Boot ticker while NoRSceneLoader harvests.</summary>
    private static void Postfix_PushEnemyNames(Scene scene, LoadSceneMode mode)
    {
        if (!HellGateSplashScreen.IsEarlyBootActive)
            return;
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.name))
            return;
        if (string.Equals(scene.name, "Gametitle", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            if (_spawnenemyEnemyField == null)
                _spawnenemyEnemyField = AccessTools.Field(typeof(Spawnenemy), "enemy");

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                if (roots[r] == null)
                    continue;

                Spawnenemy[] points = roots[r].GetComponentsInChildren<Spawnenemy>(true);
                for (int i = 0; i < points.Length; i++)
                {
                    if (points[i] == null || _spawnenemyEnemyField == null)
                        continue;

                    GameObject? enemy = _spawnenemyEnemyField.GetValue(points[i]) as GameObject;
                    if (enemy != null && !string.IsNullOrEmpty(enemy.name))
                        HellGateSplashScreen.PushEarlyBootItem(enemy.name);
                }
            }
        }
        catch
        {
            // Ticker only — never break harvest.
        }
    }
}
