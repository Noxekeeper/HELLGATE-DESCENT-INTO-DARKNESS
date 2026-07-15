using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>
/// Physical restrictions for the third trimester: block all dash actions, shorten
/// jumps, and optionally slow ground movement. These are purely mechanical; the
/// faction-specific modifiers are handled by <see cref="FactionTrimesterModifierPatch"/>.
/// </summary>
internal static class TrimesterPhysicsPatch
{
    /// <summary>Blocks dodge / double-tap dash / air-step from starting in III trimester.</summary>
    [HarmonyPatch(typeof(playercon), "stepcall_fun")]
    internal static class BlockDashInThirdTrimesterPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (!PregnancyConfig.IsEnabled)
                return true;
            if (!PregnancyConfig.BlockDashInThirdTrimester?.Value ?? false)
                return true;
            if (!WitchPregnancyState.IsThirdTrimester)
                return true;

            return false;
        }
    }

    /// <summary>Shortens jump impulse in III trimester without permanently altering vspeed.</summary>
    /// <summary>
    /// Universal movement speed debuff during trimesters. Applies from II trimester onward.
    /// </summary>
    [HarmonyPatch(typeof(playercon), "Move_fun")]
    internal static class TrimesterMoveSpeedPatch
    {
        [HarmonyPostfix]
        private static void Postfix(playercon __instance)
        {
            if (!PregnancyConfig.IsEnabled || __instance == null)
                return;
            if (!WitchPregnancyState.IsActive)
                return;

            float multiplier = TrimesterDebuffs.MoveSpeedMultiplier;
            if (Mathf.Approximately(multiplier, 1f))
                return;

            Rigidbody2D rb = __instance.rigi2d;
            if (rb == null)
                return;

            rb.velocity = new Vector2(rb.velocity.x * multiplier, rb.velocity.y);
        }
    }

    [HarmonyPatch(typeof(playercon), "Jump_fun")]
    internal static class ThirdTrimesterJumpHeightPatch
    {
        private static float _originalVspeed;

        [HarmonyPrefix]
        private static void Prefix(playercon __instance)
        {
            if (!PregnancyConfig.IsEnabled || __instance == null)
                return;
            if (!WitchPregnancyState.IsThirdTrimester)
                return;

            float multiplier = PregnancyConfig.ThirdTrimesterJumpMultiplier?.Value ?? 1f;
            if (multiplier == 1f)
                return;

            _originalVspeed = __instance.vspeed;
            __instance.vspeed = _originalVspeed * multiplier;
        }

        [HarmonyPostfix]
        private static void Postfix(playercon __instance)
        {
            if (!PregnancyConfig.IsEnabled || __instance == null)
                return;
            if (!WitchPregnancyState.IsThirdTrimester)
                return;

            __instance.vspeed = _originalVspeed;
        }
    }

}
