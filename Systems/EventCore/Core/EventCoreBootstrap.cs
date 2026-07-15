using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;
using NoREroMod;
using NoREroMod.Systems.EventCore.Content;
using NoREroMod.Systems.EventCore.UI;

namespace NoREroMod.Systems.EventCore.Core;

/// <summary>
/// Wires EventCore into the plugin lifecycle. Canvas-only UI; handlers extend by handler id later.
/// </summary>
internal static class EventCoreBootstrap
{
    private const float SceneReloadDebounceSeconds = 1f;

    private static bool _installed;
    private static float _nextReloadAllowedUnscaled = -9999f;

    internal static void Install(BaseUnityPlugin plugin)
    {
        if (_installed)
            return;

        _installed = true;
        EventCorePaths.Initialize();
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (plugin.gameObject.GetComponent<EventCoreModalDriver>() == null)
            plugin.gameObject.AddComponent<EventCoreModalDriver>();

        if (Plugin.eventCoreEnable.Value)
            EventCoreRuntime.Initialize();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EventCoreRuntime.ShutdownSession();

        if (!Plugin.eventCoreEnable.Value)
            return;

        if (ShouldSkipEventCoreDiskReload(scene.name, mode))
            return;

        float t = Time.unscaledTime;
        if (t < _nextReloadAllowedUnscaled)
            return;
        _nextReloadAllowedUnscaled = t + SceneReloadDebounceSeconds;

        EventCoreDefinitionRegistry.ReloadFromDisk();
        EventCoreStringRegistry.ReloadFromDisk();
    }

    /// <summary>
    /// NoRSceneLoader loads many additive scenes at boot to harvest enemy prefabs; each used to
    /// trigger a full JSON parse and caused multi-second hitches. Real level transitions use Single mode.
    /// </summary>
    private static bool ShouldSkipEventCoreDiskReload(string sceneName, LoadSceneMode mode)
    {
        if (string.IsNullOrEmpty(sceneName))
            return true;
        if (string.Equals(sceneName, "Gametitle", System.StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(sceneName, "Common", System.StringComparison.OrdinalIgnoreCase))
            return true;
        if (mode == LoadSceneMode.Additive)
            return true;
        return false;
    }
}
