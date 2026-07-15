using System;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.Diagnostics.Tentacle;

/// <summary>
/// Lifecycle Harmony postfixes that the polling monitor cannot reliably observe by itself:
/// <list type="bullet">
///   <item>H-scene start triggers (so we have a clean "scene began" timestamp).</item>
///   <item>Actor destruction events (so we know if the GameObject is being torn down while
///         the H-scene flag is still set — the prime soft-lock candidate).</item>
/// </list>
/// All postfixes are no-ops when <see cref="TentacleDiagnosticsConfig.Enable"/> is false.
/// </summary>
internal static class TentacleDiagnosticsLifecyclePatches
{
    private const string TAG = "[TentacleDiag]";

    [HarmonyPatch(typeof(global::Tentacle), "OnTriggerStay2D")]
    [HarmonyPostfix]
    private static void Tentacle_OnTriggerStay2D_Postfix(global::Tentacle __instance, Collider2D collision)
    {
        if (!TentacleDiagnosticsConfig.Enable) return;
        if (__instance == null || !__instance.eroflag) return;
        if (collision == null || collision.gameObject == null) return;
        if (!string.Equals(collision.gameObject.tag, "playerDAMAGEcol")) return;

        // Only log on the *frame* the H-scene was just kicked off — best signal we have.
        TentacleHSceneSnapshot snap = TentacleHSceneReflection.CaptureFromTentacle(__instance);
        if (snap.ActorEroflag && snap.PlayerEroflag)
            Plugin.Log?.LogInfo(TAG + " Tentacle H-scene START " + snap);
    }

    [HarmonyPatch(typeof(global::Tentacle), "OnDestroy")]
    [HarmonyPostfix]
    private static void Tentacle_OnDestroy_Postfix(global::Tentacle __instance)
    {
        if (!TentacleDiagnosticsConfig.Enable) return;
        if (__instance == null) return;

        // Prime smoking gun: if the actor is being destroyed while eroflag is still true,
        // the player is about to be soft-locked because erodata (a child of this GameObject)
        // dies with it and no eroanime() will ever fire to clean up com_player.eroflag.
        if (__instance.eroflag)
        {
            string trace = TentacleDiagnosticsConfig.LogStackTraceOnDestroyDuringHScene
                ? "\n" + Environment.StackTrace
                : string.Empty;
            TentacleHSceneSnapshot snap = TentacleHSceneReflection.CaptureFromTentacle(__instance);
            Plugin.Log?.LogWarning(TAG + " !! Tentacle.OnDestroy DURING active H-scene !! " + snap + trace);
        }
    }

    [HarmonyPatch(typeof(global::Trap_TentacleIronmaiden), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void Trap_OnTriggerEnter2D_Postfix(global::Trap_TentacleIronmaiden __instance, Collider2D col)
    {
        if (!TentacleDiagnosticsConfig.Enable) return;
        if (__instance == null || !__instance.eroflag) return;
        if (col == null || col.gameObject == null) return;
        if (!string.Equals(col.gameObject.tag, "playerDAMAGEcol")) return;

        TentacleHSceneSnapshot snap = TentacleHSceneReflection.CaptureFromTrap(__instance);
        if (snap.ActorEroflag && snap.PlayerEroflag)
            Plugin.Log?.LogInfo(TAG + " Trap_TentacleIronmaiden H-scene START " + snap);
    }

    // Trap_TentacleIronmaiden does NOT define its own OnDestroy (no override on Trapdata
    // either), so we detect trap destruction through the polling monitor: an instance that
    // was in the previous-frame snapshot dict but is missing from FindObjectsOfType this
    // frame has been destroyed.
}
