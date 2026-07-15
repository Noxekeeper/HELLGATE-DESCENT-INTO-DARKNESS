using System;
using DarkTonic.MasterAudio;
using HarmonyLib;
using NoREroMod.Patches.Player;
using Spine.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Patches.Trap;

/// <summary>
/// Unified struggle / give-up / Rage-QTE cleanup for all vanilla <see cref="Trapdata"/> H-traps
/// (Rosewarm, TrapMachine, WallHip, Ivy_monster, BlackOozetrap, PictureEroNon, …).
/// </summary>
internal static class TrapdataHSceneEscapePatch
{
    internal static void AbortActiveTrapHScenesOnPlayerEscape(playercon player, bool requireErodownClear)
    {
        if (player == null)
            return;

        if (requireErodownClear && player.erodown != 0)
            return;

        if (!IsAnyTrapHSceneVisualActive())
            return;

        bool abortedAny = false;
        foreach (Trapdata trap in Object.FindObjectsOfType<Trapdata>())
        {
            if (trap != null && TryAbortSingleTrap(trap, player))
                abortedAny = true;
        }

        if (!abortedAny)
            return;

        ClearPlayerHSceneFlags(player);
        PlayerCombatControlRecovery.RestoreAfterStruggleEscape();
    }

    internal static bool IsAnyTrapHSceneVisualActive()
    {
        foreach (Trapdata trap in Object.FindObjectsOfType<Trapdata>())
        {
            if (trap != null && IsTrapInActiveHScene(trap))
                return true;
        }

        return false;
    }

    internal static bool IsTrapInActiveHScene(Trapdata trap)
    {
        if (trap == null)
            return false;

        if (trap.eroflag)
            return true;

        GameObject eroData = trap.erodata;
        return eroData != null && eroData.activeSelf;
    }

    private static bool TryAbortSingleTrap(Trapdata trap, playercon player)
    {
        if (trap == null || !IsTrapInActiveHScene(trap))
            return false;

        try
        {
            try
            {
                MasterAudio.StopBus("EroVoice");
            }
            catch
            {
            }

            GameObject eroData = trap.erodata;
            if (eroData != null)
            {
                SkeletonAnimation eroSpine = eroData.GetComponent<SkeletonAnimation>();
                eroSpine?.AnimationState?.ClearTracks();
                if (eroSpine != null)
                    eroSpine.enabled = false;
                eroData.SetActive(false);
            }

            trap.eroflag = false;
            RestoreTrapVisuals(trap);

            try
            {
                trap.CancelInvoke("fun_DisableWhenOneTarget_reset");
            }
            catch
            {
            }

            try
            {
                trap.ero_camerareset();
                trap.fun_DisableWhenOneTarget_reset();
            }
            catch
            {
            }

            if (player != null)
                player.eroflag = false;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[TrapdataEscape] Failed to abort trap H-scene on "
                + trap.GetType().Name + ": " + ex.Message);
            return false;
        }

        return true;
    }

    private static void RestoreTrapVisuals(Trapdata trap)
    {
        GameObject eroRoot = trap.erodata;

        MeshRenderer meshRenderer = trap.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.enabled = true;

        SpriteRenderer spriteRenderer = trap.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            spriteRenderer.enabled = true;

        MeshRenderer[] childMeshes = trap.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < childMeshes.Length; i++)
        {
            MeshRenderer renderer = childMeshes[i];
            if (renderer == null || (eroRoot != null && renderer.transform.IsChildOf(eroRoot.transform)))
                continue;
            renderer.enabled = true;
        }

        SpriteRenderer[] childSprites = trap.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < childSprites.Length; i++)
        {
            SpriteRenderer renderer = childSprites[i];
            if (renderer == null || (eroRoot != null && renderer.transform.IsChildOf(eroRoot.transform)))
                continue;
            renderer.enabled = true;
        }
    }

    private static void ClearPlayerHSceneFlags(playercon player)
    {
        if (player == null)
            return;

        player.eroflag = false;
        player._eroflag2 = false;

        if (!player._Death && player.rigi2d != null)
            player.rigi2d.simulated = true;
    }
}

[HarmonyPatch(typeof(playercon), "fun_nowdamage")]
internal static class TrapdataHSceneEscapeFunNowDamagePatch
{
    private static int _playerErodownBeforeFunNowdamage;

    [HarmonyPrefix]
    private static void BeforeFunNowdamage(playercon __instance)
    {
        _playerErodownBeforeFunNowdamage = __instance != null ? __instance.erodown : 0;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void AfterFunNowdamage(playercon __instance)
    {
        try
        {
            if (__instance == null
                || _playerErodownBeforeFunNowdamage == 0
                || __instance.erodown != 0)
            {
                return;
            }

            TrapdataHSceneEscapePatch.AbortActiveTrapHScenesOnPlayerEscape(__instance, requireErodownClear: true);
            StruggleSystem.startGrabInvul();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[TrapdataEscape] fun_nowdamage cleanup failed: " + ex.Message);
        }
    }
}

[HarmonyPatch(typeof(playercon), nameof(playercon.ImmediatelyERO))]
internal static class TrapdataHSceneEscapeGiveUpPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void OnGiveUpCleanup(playercon __instance)
    {
        try
        {
            TrapdataHSceneEscapePatch.AbortActiveTrapHScenesOnPlayerEscape(__instance, requireErodownClear: false);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[TrapdataEscape] GiveUp cleanup failed: " + ex.Message);
        }
    }
}
