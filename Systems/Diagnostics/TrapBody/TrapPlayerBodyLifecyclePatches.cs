using System;
using HarmonyLib;
using NoREroMod.Patches.Player;
using NoREroMod.Systems.Cache;
using UnityEngine;

namespace NoREroMod.Systems.Diagnostics.TrapBody;

/// <summary>
/// Event hooks for trap body diagnostics. No gameplay changes.
/// </summary>
internal static class TrapPlayerBodyLifecyclePatches
{
    [HarmonyPatch(typeof(Ivy_monster), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void Ivy_OnGrab(Ivy_monster __instance, Collider2D col)
    {
        if (!TrapPlayerBodyDiagnosticsConfig.Enable || __instance == null || !__instance.eroflag)
            return;

        playercon player = __instance.com_player;
        TrapPlayerBodyMonitor.LogEvent(
            "Ivy_monster.OnTriggerEnter2D",
            player,
            "grabSnapToRoot");
    }

    [HarmonyPatch(typeof(StruggleSystem), nameof(StruggleSystem.startGrabInvul))]
    [HarmonyPrefix]
    private static void StruggleInvul_Prefix()
    {
        if (!TrapPlayerBodyDiagnosticsConfig.Enable)
            return;

        playercon player = UnifiedPlayerCacheManager.GetPlayer();
        TrapPlayerBodyMonitor.LogEventWithOptionalStack(
            "StruggleSystem.startGrabInvul",
            player,
            "beforeRestore",
            TrapPlayerBodyDiagnosticsConfig.LogStackTraceOnStruggleInvul);
    }

    [HarmonyPatch(typeof(PlayerCombatControlRecovery), nameof(PlayerCombatControlRecovery.RestoreAfterStruggleEscape))]
    [HarmonyPrefix]
    private static void RestoreAfterStruggleEscape_Prefix()
    {
        if (!TrapPlayerBodyDiagnosticsConfig.Enable)
            return;

        playercon player = UnifiedPlayerCacheManager.GetPlayer();
        bool simBefore = player != null && player.rigi2d != null && player.rigi2d.simulated;
        TrapPlayerBodyMonitor.LogEventWithOptionalStack(
            "PlayerCombatControlRecovery.RestoreAfterStruggleEscape",
            player,
            "simBefore=" + simBefore,
            TrapPlayerBodyDiagnosticsConfig.LogStackTraceOnSimulatedEnable);
    }

    [HarmonyPatch(typeof(Trapdata), "ero_camera_1")]
    [HarmonyPostfix]
    private static void Trap_EroCamera1(Trapdata __instance)
    {
        if (!TrapPlayerBodyDiagnosticsConfig.Enable || __instance == null)
            return;
        if (!__instance.eroflag && (__instance.erodata == null || !__instance.erodata.activeSelf))
            return;

        TrapPlayerBodyMonitor.LogEvent(
            "Trapdata.ero_camera_1",
            __instance.com_player,
            "type=" + __instance.GetType().Name);
    }
}
