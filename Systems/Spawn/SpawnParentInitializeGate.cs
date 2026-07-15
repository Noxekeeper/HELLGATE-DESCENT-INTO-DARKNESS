using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// <see cref="SpawnParent.Initialize"/> (Invoke 0.5s after Common Single reload) destroys every
/// <c>Enemy</c>. HellGate runs a single zone pack after that wipe — no second "stabilize" pass.
/// </summary>
internal static class SpawnParentInitializeGate
{
    private static string _pendingZoneAfterLoad = string.Empty;
    private static float _blockControllerUntilUnscaled;
    private static int _refreshToken;

    /// <summary>Delay after Initialize so late vanilla camera/_NotSpwan cleanup has finished.</summary>
    private const float PostInitializeDelaySeconds = 0.1f;

    internal static bool HasPendingZone => !string.IsNullOrEmpty(_pendingZoneAfterLoad);

    internal static bool ShouldBlockControllerRefresh =>
        Time.unscaledTime < _blockControllerUntilUnscaled || HasPendingZone;

    internal static void Install(Harmony harmony)
    {
        if (harmony == null)
            return;

        try
        {
            MethodInfo load = AccessTools.Method(typeof(PlayerStatus), nameof(PlayerStatus.LoadSceneAndWait));
            if (load != null)
            {
                harmony.Patch(
                    load,
                    prefix: new HarmonyMethod(typeof(SpawnParentInitializeGate), nameof(LoadSceneAndWaitPrefix)));
            }

            MethodInfo init = AccessTools.Method(typeof(SpawnParent), "Initialize");
            if (init != null)
            {
                harmony.Patch(
                    init,
                    postfix: new HarmonyMethod(typeof(SpawnParentInitializeGate), nameof(AfterSpawnParentInitialize)));
                Plugin.Log?.LogInfo("[LOCATION SPAWN] SpawnParent.Initialize gate installed.");
            }
            else
            {
                Plugin.Log?.LogWarning("[LOCATION SPAWN] SpawnParent.Initialize not found — gate not installed.");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[LOCATION SPAWN] Initialize gate install failed: {ex.Message}");
        }
    }

    private static void LoadSceneAndWaitPrefix(string a, string b)
    {
        _blockControllerUntilUnscaled = Time.unscaledTime + 4f;
        HellGateLocationSpawnRefresh.CancelInFlightRefresh("LoadSceneAndWait");

        if (!string.IsNullOrEmpty(b)
            && !HellGateLocationSpawnRefresh.ShouldIgnoreSceneName(b))
        {
            ArmPendingZone(b);
        }
    }

    internal static void ArmPendingZone(string targetZone)
    {
        _pendingZoneAfterLoad = targetZone ?? string.Empty;
        _blockControllerUntilUnscaled = Mathf.Max(_blockControllerUntilUnscaled, Time.unscaledTime + 4f);
    }

    private static void AfterSpawnParentInitialize()
    {
        // Abort any pack that started before vanilla Enemy wipe (batched spawn race).
        HellGateLocationSpawnRefresh.CancelInFlightRefresh("SpawnParent.Initialize");
        HellGateLocationSpawnRefresh.InvalidateDuplicateCooldown();

        string zone = _pendingZoneAfterLoad;
        if (string.IsNullOrEmpty(zone))
            zone = HellGateLocationSpawnRefresh.GetActiveGameplayZone();

        if (string.IsNullOrEmpty(zone)
            || HellGateLocationSpawnRefresh.ShouldIgnoreSceneName(zone))
            return;

        if (Plugin.Instance == null)
            return;

        int token = ++_refreshToken;
        Plugin.Instance.StartCoroutine(DeferredRefreshAfterInitialize(token, zone));
    }

    private static IEnumerator DeferredRefreshAfterInitialize(int token, string zone)
    {
        // One frame for Destroy(Enemy) from Initialize, then wait out late vanilla settle.
        yield return null;
        yield return new WaitForSecondsRealtime(PostInitializeDelaySeconds);

        if (token != _refreshToken)
            yield break;

        _pendingZoneAfterLoad = string.Empty;
        _blockControllerUntilUnscaled = Mathf.Max(_blockControllerUntilUnscaled, Time.unscaledTime + 1f);

        Plugin.Log?.LogInfo($"[LOCATION SPAWN] Post-Initialize refresh for zone=\"{zone}\".");
        HellGateLocationSpawnRefresh.InvalidateDuplicateCooldown();
        HellGateLocationSpawnRefresh.OnGameplaySceneLoadCompleted(zone, forceTakeover: true);
    }

    /// <summary>
    /// SceneLoad wrapper: arm zone, wait for Initialize-driven refresh, fallback if Initialize missed.
    /// </summary>
    internal static IEnumerator FallbackRefreshIfStillPending(string zone, float delaySeconds = 2f)
    {
        ArmPendingZone(zone);

        float start = Time.unscaledTime;
        while (Time.unscaledTime - start < delaySeconds)
        {
            if (string.IsNullOrEmpty(_pendingZoneAfterLoad))
                yield break;
            yield return null;
        }

        if (string.IsNullOrEmpty(_pendingZoneAfterLoad))
            yield break;

        Plugin.Log?.LogWarning(
            "[LOCATION SPAWN] SpawnParent.Initialize not seen — fallback refresh after delay.");

        string zoneCapture = _pendingZoneAfterLoad;
        _pendingZoneAfterLoad = string.Empty;
        int token = ++_refreshToken;
        if (Plugin.Instance != null)
            Plugin.Instance.StartCoroutine(DeferredRefreshAfterInitialize(token, zoneCapture));
        else
            HellGateLocationSpawnRefresh.OnGameplaySceneLoadCompleted(zoneCapture, forceTakeover: true);
    }
}
