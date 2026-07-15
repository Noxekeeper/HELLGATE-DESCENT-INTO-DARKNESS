using System;
using NoREroMod.Patches.Player;
using NoREroMod.Systems.Pregnancy.ShelterAttack;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Safety-net for <c>Idea_Nowscene</c> changes that do not go through <c>LoadSceneAndWait</c>.
/// Primary refresh is armed by <see cref="SceneLoadSpawnRefreshPatch"/> and runs after
/// <see cref="SpawnParent.Initialize"/>.
/// </summary>
internal sealed class LocationTransitionSpawnController : MonoBehaviour
{
    private const float DebounceSeconds = 0.85f;

    private string lastObservedScene = string.Empty;
    private string pendingScene = string.Empty;
    private float pendingSinceUnscaled;
    private string lastRefreshedScene = string.Empty;

    private void Update()
    {
        if (VanillaCutsceneSceneGuard.IsAdditiveEvSceneActive())
            return;

        string scene = HellGateLocationSpawnRefresh.GetActiveGameplayZone();
        if (HellGateLocationSpawnRefresh.ShouldIgnoreSceneName(scene))
            return;

        if (!string.Equals(scene, lastObservedScene, StringComparison.Ordinal))
        {
            if (!string.IsNullOrEmpty(lastObservedScene))
                lastRefreshedScene = string.Empty;

            lastObservedScene = scene;
            pendingScene = scene;
            pendingSinceUnscaled = Time.unscaledTime;
        }

        if (string.IsNullOrEmpty(pendingScene))
            return;

        if (!string.Equals(scene, pendingScene, StringComparison.Ordinal))
        {
            pendingScene = scene;
            pendingSinceUnscaled = Time.unscaledTime;
            return;
        }

        if (Time.unscaledTime - pendingSinceUnscaled < DebounceSeconds)
            return;

        if (!HellGateLocationSpawnRefresh.IsTargetZoneSceneLoaded(pendingScene))
            return;

        // LoadScene + SpawnParent.Initialize own the first refresh; do not race them.
        if (SpawnParentInitializeGate.ShouldBlockControllerRefresh)
            return;

        if (HellGateLocationSpawnRefresh.IsZoneRefreshPendingOrRecent(pendingScene)
            || HellGateLocationSpawnRefresh.WasZoneRefreshedRecently(pendingScene))
        {
            lastRefreshedScene = pendingScene;
            pendingScene = string.Empty;
            return;
        }

        if (string.Equals(pendingScene, lastRefreshedScene, StringComparison.Ordinal))
        {
            pendingScene = string.Empty;
            return;
        }

        string target = pendingScene;
        ShelterAttackDriver.OnSceneChanged(target);

        if (ShelterAttackSceneGuard.ShouldBlockParishZoneRefresh(target))
        {
            lastRefreshedScene = target;
            pendingScene = string.Empty;
            return;
        }

        if (HellGateLocationSpawnRefresh.RefreshOnLocationTransition(target))
        {
            lastRefreshedScene = target;
            pendingScene = string.Empty;
            return;
        }

        if (HellGateLocationSpawnRefresh.IsZoneRefreshPendingOrRecent(target))
            return;

        pendingSinceUnscaled = Time.unscaledTime;
    }
}
