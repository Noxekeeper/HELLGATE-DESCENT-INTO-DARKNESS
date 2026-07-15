using System;
using System.Reflection;
using HarmonyLib;
using NoREroMod.Patches.Player;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Vanilla door/ladder transitions (<see cref="SceneMove"/>, <see cref="SceneMoveSubmit"/>,
/// <see cref="SceneMoveMainEv"/>) update <see cref="StaticMng.Idea_Nowscene"/>; HellGate clears
/// spawn cooldown and wipes enemies here. Full pack refresh runs after LoadSceneAndWait via
/// <see cref="SceneLoadSpawnRefreshPatch"/> (safety-net: <see cref="LocationTransitionSpawnController"/>).
/// </summary>
internal static class SceneMoveTransitionSpawnPatch
{
    internal static void PrepareWalkTransition(string targetScene, string sourceLabel)
    {
        // Bar / EV additive scenes use internal SceneMove; do not overwrite _re_Scenename or respawn packs.
        if (VanillaCutsceneSceneGuard.IsAdditiveEvSceneActive())
            return;

        if (HellGateLocationSpawnRefresh.ShouldIgnoreSceneName(targetScene))
            return;

        string fromZone = HellGateLocationSpawnRefresh.GetActiveGameplayZone();
        if (string.IsNullOrEmpty(fromZone))
            fromZone = HellGateLocationSpawnRefresh.GetReSceneName();

        // Only wipe + clear cooldown on a real cross-zone walk. Same-zone SceneMove must not
        // destroy enemies without a guaranteed refresh (controller only arms on Idea_Nowscene change).
        if (!HellGateLocationSpawnRefresh.NotifyCrossZoneWalkTransition(fromZone, targetScene))
            return;

        HellGateLocationSpawnRefresh.ForceZoneSpawnReset($"walk transition {fromZone} -> {targetScene}");
    }

    private static string ReadSceneName(object instance, Type moverType)
    {
        if (instance == null || moverType == null)
            return string.Empty;

        FieldInfo field = moverType.GetField("SceneName", BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(instance) as string ?? string.Empty;
    }

    [HarmonyPatch(typeof(SceneMove), nameof(SceneMove.SceneMOVE))]
    internal static class SceneMoveTransitionPatch
    {
        [HarmonyPrefix]
        private static void BeforeSceneMove(SceneMove __instance)
        {
            try
            {
                PrepareWalkTransition(ReadSceneName(__instance, typeof(SceneMove)), "SceneMove");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[LOCATION SPAWN] SceneMove prefix failed: " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(SceneMoveSubmit), nameof(SceneMoveSubmit.SceneMOVE))]
    internal static class SceneMoveSubmitTransitionPatch
    {
        [HarmonyPrefix]
        private static void BeforeSceneMoveSubmit(SceneMoveSubmit __instance)
        {
            try
            {
                PrepareWalkTransition(ReadSceneName(__instance, typeof(SceneMoveSubmit)), "SceneMoveSubmit");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[LOCATION SPAWN] SceneMoveSubmit prefix failed: " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(SceneMoveMainEv), nameof(SceneMoveMainEv.SceneMOVE))]
    internal static class SceneMoveMainEvTransitionPatch
    {
        [HarmonyPrefix]
        private static void BeforeSceneMoveMainEv(SceneMoveMainEv __instance)
        {
            try
            {
                PrepareWalkTransition(ReadSceneName(__instance, typeof(SceneMoveMainEv)), "SceneMoveMainEv");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[LOCATION SPAWN] SceneMoveMainEv prefix failed: " + ex.Message);
            }
        }
    }
}
