using System;
using DarkTonic.MasterAudio;
using HarmonyLib;
using NoREroMod.Patches.Player;
using NoREroMod.Systems.Cache;
using Spine.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Patches.Trap;

/// <summary>
/// Unified struggle / give-up / Rage-QTE cleanup for all vanilla <see cref="Trapdata"/> H-traps
/// (Rosewarm, TrapMachine, WallHip, Ivy_monster, BlackOozetrap, PictureEroNon, AngelStatue_Trap, …).
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

        // Invul MUST be armed before rigi2d.simulated=true — restoring physics re-fires
        // OnTriggerEnter while the player still overlaps the trap.
        // Do not call startGrabInvul if already active: that would reset the timer and drop the +2s extension.
        if (!StruggleSystem.isGrabInvul())
            StruggleSystem.startGrabInvul();

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

    internal static bool IsPlayerInActiveTrapHScene(playercon player)
    {
        if (player == null || !player.eroflag)
            return false;

        foreach (Trapdata trap in Object.FindObjectsOfType<Trapdata>())
        {
            if (trap == null || !IsTrapInActiveHScene(trap))
                continue;
            if (trap.com_player == player || trap.eroflag)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Black ooze trap H is active (Type A needs knockdown; TypeB grabs standing).
    /// Used to skip max-SP force-escape that softlocks these grabs.
    /// </summary>
    internal static bool IsBlackOozeTrapHSceneActive()
    {
        foreach (BlackOozetrap trap in Object.FindObjectsOfType<BlackOozetrap>())
        {
            if (trap != null && trap.eroflag)
                return true;
        }

        foreach (BlackOozeTrapTypeB trap in Object.FindObjectsOfType<BlackOozeTrapTypeB>())
        {
            if (trap != null && trap.eroflag)
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
            ResetOneShotTrapFlags(trap);
            ResetAngelStatueGrabTimer(trap);

            try
            {
                trap.CancelInvoke("fun_DisableWhenOneTarget_reset");
            }
            catch
            {
            }

            try
            {
                trap.CancelInvoke("flagcount");
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

            // AngelStatue_Trap is a one-shot grab object: vanilla Destroy on escape.
            // Leaving it alive lets OnTriggerStay re-arm H after 1.5s while player is still DOWN.
            if (trap is AngelStatue_Trap)
            {
                Object.Destroy(trap.gameObject);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[TrapdataEscape] Failed to abort trap H-scene on "
                + trap.GetType().Name + ": " + ex.Message);
            return false;
        }

        return true;
    }

    private static void ResetAngelStatueGrabTimer(Trapdata trap)
    {
        if (!(trap is AngelStatue_Trap))
            return;

        try
        {
            var traverse = Traverse.Create(trap);
            if (traverse.Field("ErostartCount").FieldExists())
                traverse.Field("ErostartCount").SetValue(0f);
            if (traverse.Field("colEroflag").FieldExists())
                traverse.Field("colEroflag").SetValue(false);
            if (traverse.Field("Eroendcount").FieldExists())
                traverse.Field("Eroendcount").SetValue(999f);
        }
        catch
        {
        }
    }

    /// <summary>
    /// <see cref="BlackOozeTrapTypeB"/> is reusable (not one-shot). Vanilla sets
    /// <c>trapflag</c> on grab and clears it via <c>flagcount</c> ~0.3s after H ends.
    /// After struggle abort we keep <c>trapflag</c> true briefly (anti instant re-grab),
    /// then clear it on an unscaled timer so the trap can fire again.
    /// </summary>
    private static void ResetOneShotTrapFlags(Trapdata trap)
    {
        if (trap == null)
            return;

        try
        {
            if (trap is BlackOozeTrapTypeB typeB)
            {
                var traverse = Traverse.Create(typeB);
                if (!traverse.Field("trapflag").FieldExists())
                    return;

                traverse.Field("trapflag").SetValue(true);
                try
                {
                    typeB.CancelInvoke("flagcount");
                }
                catch
                {
                }

                BlackOozeTypeBRearmGate gate = typeB.GetComponent<BlackOozeTypeBRearmGate>();
                if (gate == null)
                    gate = typeB.gameObject.AddComponent<BlackOozeTypeBRearmGate>();
                // Cover grab invul (+2s HellGate) with a little margin; unscaled so pause/slow-mo cannot stick the flag.
                gate.Arm(2.75f);
                return;
            }

            var tr = Traverse.Create(trap);
            if (tr.Field("trapflag").FieldExists())
                tr.Field("trapflag").SetValue(false);
        }
        catch
        {
        }
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

    internal static void ClearPlayerHSceneFlags(playercon player)
    {
        if (player == null)
            return;

        player.eroflag = false;
        player._eroflag2 = false;

        if (!player._Death && player.rigi2d != null)
            player.rigi2d.simulated = true;

        if (!player._Death)
        {
            try
            {
                MeshRenderer[] renderers = player.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                        renderers[i].enabled = true;
                }
            }
            catch
            {
            }
        }
    }
}

/// <summary>
/// At full SP, NoREroMod's fun_nowdamage prefix is skipped; vanilla still needs downup &gt;= 2.
/// Same gap that softlocked Succubus / Suraimu — AngelStatue_Trap hits it via Trapdata.
/// <para>
/// Do <b>not</b> force-escape <see cref="BlackOozetrap"/> / <see cref="BlackOozeTrapTypeB"/>:
/// TypeB starts the grab with <c>ImmediatelyERO()</c> (erodown=1) while the player often has
/// full SP. Forcing escape here aborts the grab on the same frame → simulated flicker / softlock.
/// QTE already skips BlackOoze for the same reason.
/// </para>
/// </summary>
[HarmonyPatch(typeof(playercon), "fun_nowdamage")]
internal static class TrapdataStruggleMaxSpEscapePostfix
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void ForceEscapeWhenSpFull(playercon __instance, PlayerStatus ___playerstatus)
    {
        if (__instance == null || ___playerstatus == null)
            return;
        if (PlayerEroContextUtility.ShouldBlockEnemyStruggleAutomation(__instance))
            return;
        if (__instance.erodown == 0 || __instance._easyESC || !___playerstatus._SOUSA)
            return;
        if (___playerstatus.Sp < ___playerstatus.AllMaxSP())
            return;
        if (!TrapdataHSceneEscapePatch.IsPlayerInActiveTrapHScene(__instance)
            && !TrapdataHSceneEscapePatch.IsAnyTrapHSceneVisualActive())
            return;
        // Black ooze traps: struggle is QTE/vanilla-driven; max-SP force escape softlocks the grab.
        if (TrapdataHSceneEscapePatch.IsBlackOozeTrapHSceneActive())
            return;

        __instance.erodown = 0;
        __instance.nowdamage = false;
        __instance.tough = __instance.maxtough;

        try
        {
            Traverse.Create(__instance).Field("damecount").SetValue(0f);
            Traverse.Create(__instance).Field("downup").SetValue(0);
        }
        catch
        {
        }

        StruggleSystem.startGrabInvul();
        TrapdataHSceneEscapePatch.AbortActiveTrapHScenesOnPlayerEscape(__instance, requireErodownClear: true);
    }
}

[HarmonyPatch(typeof(StruggleSystem), nameof(StruggleSystem.startGrabInvul))]
internal static class TrapdataHSceneEscapeStrugglePatch
{
    [HarmonyPostfix]
    private static void OnStruggleEscapeCleanup()
    {
        try
        {
            playercon player = UnifiedPlayerCacheManager.GetPlayer();
            TrapdataHSceneEscapePatch.AbortActiveTrapHScenesOnPlayerEscape(
                player,
                requireErodownClear: false);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[TrapdataEscape] Struggle cleanup failed: " + ex.Message);
        }
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
            // Invul is armed inside Abort when a trap H was active; keep an explicit call for
            // the no-visual edge case so re-enter stays gated.
            if (!StruggleSystem.isGrabInvul())
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

/// <summary>
/// Trapdata grabs that ignore <see cref="StruggleSystem.isGrabInvul"/> (EnemyDate respects it).
/// After escape, <c>rigi2d.simulated</c> restore re-fires trigger while still overlapping.
/// Separate patch types — multi-target HarmonyPatch on one method was unreliable here.
/// </summary>
[HarmonyPatch(typeof(AngelStatue_Trap), "OnTriggerStay2D")]
internal static class AngelStatueTrapGrabInvulPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool BlockWhileGrabInvul()
    {
        return !StruggleSystem.isGrabInvul();
    }
}

[HarmonyPatch(typeof(BlackOozetrap), "OnTriggerEnter2D")]
internal static class BlackOozetrapGrabInvulPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool BlockWhileGrabInvul()
    {
        return !StruggleSystem.isGrabInvul();
    }
}

[HarmonyPatch(typeof(BlackOozeTrapTypeB), "OnTriggerEnter2D")]
internal static class BlackOozeTrapTypeBGrabInvulPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool BlockWhileGrabInvul()
    {
        // Only invul — trapflag rearm is owned by vanilla + BlackOozeTypeBRearmGate.
        // Duplicating a trapflag block here made failed rearms look like a permanent one-shot.
        return !StruggleSystem.isGrabInvul();
    }
}
