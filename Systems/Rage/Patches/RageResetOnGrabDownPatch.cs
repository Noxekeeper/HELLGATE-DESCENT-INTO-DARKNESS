using System;
using HarmonyLib;
using NoREroMod;
using NoREroMod.Systems.Rage;
using NoREroMod.Patches.UI.MindBroken;

namespace NoREroMod.Systems.Rage.Patches;

/// <summary>
/// MindBroken penalty when Rage is interrupted by grab or knockdown.
/// Penalties run only from <see cref="Process"/> (PlayerConUpdateDispatcher) so each edge fires once.
/// H-scene start often sets eroflag and erodown together (or one frame apart): we do not stack knockdown
/// on top of grab when <c>eroflag</c> is already true — that was an extra +2% on the same interruption.
/// </summary>
internal static class RageResetOnGrabDownPatch
{
    private static bool _lastEroflagState = false;
    private static int _lastErodownState = 0;

    private static float PenaltyGrab => Plugin.rageResetHCPenaltyGrab?.Value ?? 0.05f;
    private static float PenaltyKnockdown => Plugin.rageResetHCPenaltyKnockdown?.Value ?? 0.02f;

    /// <summary>Instant slow-mo reset when game enters ERO via ImmediatelyERO (same frame as vanilla).</summary>
    [HarmonyPatch(typeof(playercon), "ImmediatelyERO")]
    [HarmonyPostfix]
    private static void ImmediatelyERO_Postfix(playercon __instance)
    {
        try
        {
            if (__instance.eroflag)
                TimeSlowMoSystem.Reset();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RAGE RESET] Error in ImmediatelyERO patch: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>Invoked by PlayerConUpdateDispatcher — sole place for grab/knockdown MB penalties.</summary>
    internal static void Process(playercon __instance)
    {
        try
        {
            if (!RageSystem.Enabled) return;
            if (__instance == null) return;

            bool currentEroflag = __instance.eroflag;
            int currentErodown = __instance.erodown;

            if (!_lastEroflagState && currentEroflag)
            {
                TimeSlowMoSystem.Reset();
                bool wasActive = RageSystem.IsActive;
                bool wasOutburstFury = RageSystem.IsOutburstFury;
                if (wasActive && MindBrokenSystem.Enabled)
                {
                    string reason = wasOutburstFury ? "outburst_fury_interrupt_grab" : "rage_interrupt_grab";
                    MindBrokenSystem.AddPercent(PenaltyGrab, reason);
                }
            }

            // Knockdown-only penalty: not while eroflag is true (grab/H — handled by grab edge or same event).
            if (_lastErodownState == 0 && currentErodown != 0 && !currentEroflag)
            {
                bool wasActive = RageSystem.IsActive;
                bool wasOutburstFury = RageSystem.IsOutburstFury;

                if (wasActive && MindBrokenSystem.Enabled)
                {
                    string reason = wasOutburstFury ? "outburst_fury_interrupt_down" : "rage_interrupt_down";
                    MindBrokenSystem.AddPercent(PenaltyKnockdown, reason);
                }
            }

            _lastEroflagState = currentEroflag;
            _lastErodownState = currentErodown;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RAGE RESET] Error in Update patch: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
