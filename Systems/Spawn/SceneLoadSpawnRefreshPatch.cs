using System.Collections;
using HarmonyLib;
using NoREroMod.Patches.Player;
using NoREroMod.Systems.Pregnancy.ShelterAttack;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Arms a pending zone after <see cref="PlayerStatus.LoadSceneAndWait"/>; actual HellGate spawn
/// is driven by <see cref="SpawnParentInitializeGate"/> after vanilla Enemy wipe.
/// </summary>
internal static class SceneLoadSpawnRefreshPatch
{
    [HarmonyPatch(typeof(PlayerStatus), nameof(PlayerStatus.LoadSceneAndWait))]
    [HarmonyPostfix]
    private static void LoadSceneAndWaitPostfix(ref IEnumerator __result, string a, string b)
    {
        if (__result == null || string.IsNullOrEmpty(b))
            return;

        __result = WrapLoadSceneAndWait(__result, b);
    }

    private static IEnumerator WrapLoadSceneAndWait(IEnumerator inner, string targetScene)
    {
        while (inner.MoveNext())
            yield return inner.Current;

        float sceneWaitStart = Time.unscaledTime;
        while (Time.unscaledTime - sceneWaitStart < 3f)
        {
            if (HellGateLocationSpawnRefresh.IsTargetZoneSceneLoaded(targetScene))
                break;

            Scene byName = SceneManager.GetSceneByName(targetScene);
            if (byName.IsValid() && byName.isLoaded)
                break;

            yield return null;
        }

        if (VanillaEvSceneExitPatch.IsAllowingAltarSceneLoad)
            yield break;

        if (VanillaCutsceneSceneGuard.IsAdditiveEvSceneActive())
            yield break;

        ShelterAttackDriver.OnSceneChanged(targetScene);
        yield return SpawnParentInitializeGate.FallbackRefreshIfStillPending(targetScene, 2f);
    }
}
