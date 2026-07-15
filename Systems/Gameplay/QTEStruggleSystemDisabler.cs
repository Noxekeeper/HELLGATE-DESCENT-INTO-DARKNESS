using System;
using HarmonyLib;
using UnityEngine;
using NoREroMod.Patches.Player;

namespace NoREroMod;

/// <summary>
/// Intercepts NoREroMod struggle SP gain on <c>playercon.fun_nowdamage</c>.
///
/// NoREroMod can grant up to 60% SP per click and prime escape in its Prefix.
/// HellGate must rewrite that gain to <see cref="QTESPCalculator.CalculateSPGainClick"/>
/// AFTER NoREroMod runs but BEFORE vanilla processes <c>key_submit</c>/<c>downup</c>.
/// A Postfix is too late: escape already happened when the inflated gain crossed 100%.
/// </summary>
class QTEStruggleSystemDisabler
{
    private static float spBeforeCalculation = 0f;
    private static bool originalKeySubmit = false;
    private static bool originalKeyAtk = false;
    private static bool originalKeyItem = false;

    private static System.Reflection.FieldInfo inPraymaidenStruggleField = null;

    private static bool GetInPraymaidenStruggle()
    {
        try
        {
            if (inPraymaidenStruggleField == null)
            {
                var playerConPatchType = HellGateTypeResolver.Resolve("NoREroMod.PlayerConPatch");
                if (playerConPatchType != null)
                    inPraymaidenStruggleField = AccessTools.Field(playerConPatchType, "inPraymaidenStruggle");
            }

            if (inPraymaidenStruggleField != null)
                return (bool)inPraymaidenStruggleField.GetValue(null);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[QTE Disabler] Error getting inPraymaidenStruggle: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Snapshot SP and input before NoREroMod mutates them.
    /// </summary>
    [HarmonyPatch(typeof(playercon), "fun_nowdamage")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    static void SaveSPBeforeCalculation(
        PlayerStatus ___playerstatus,
        bool ___key_submit,
        bool ___key_atk,
        bool ___key_item)
    {
        if (___playerstatus == null)
            return;

        spBeforeCalculation = ___playerstatus.Sp;
        originalKeySubmit = ___key_submit;
        originalKeyAtk = ___key_atk;
        originalKeyItem = ___key_item;
    }

    /// <summary>
    /// Replace NoREroMod struggle gain with HellGate click gain, and undo false escape priming
    /// when corrected SP is still below max. Runs after NoREroMod Prefix, before vanilla body.
    /// </summary>
    [HarmonyPatch(typeof(playercon), "fun_nowdamage")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    static void OverrideStruggleSPCalculation(
        playercon __instance,
        PlayerStatus ___playerstatus,
        ref bool ___key_submit,
        ref bool ___key_atk,
        ref int ___downup)
    {
        try
        {
            if (__instance == null || ___playerstatus == null)
                return;

            // Potion escape: NoREroMod fills SP to max via item — do not rewrite.
            if (originalKeyItem && (Plugin.allowStrugglePotion?.Value ?? false))
                return;

            bool birthRecovery = BirthRecoveryStruggleState.IsActive
                && PlayerEroContextUtility.IsActivePregnancyBirth(__instance);

            if (__instance.erodown == 0
                || !___playerstatus._SOUSA
                || (__instance._easyESC && !birthRecovery))
            {
                return;
            }

            bool isStruggling = originalKeySubmit || originalKeyAtk;
            if (!isStruggling)
                return;

            bool inPraymaidenStruggle = GetInPraymaidenStruggle();
            if (!birthRecovery && !__instance.eroflag && !inPraymaidenStruggle)
                return;

            if (birthRecovery)
                BirthRecoveryStruggleState.NotifyStruggleInput();

            bool windowOpen = QTEStruggleWindowManager.IsWindowOpen() || birthRecovery;
            if (!windowOpen)
                return;

            float maxSp = ___playerstatus.AllMaxSP();
            float originalSPGain = ___playerstatus.Sp - spBeforeCalculation;

            // Only rewrite positive NoREroMod struggle gains (including jumps that hit/exceed max).
            if (originalSPGain <= 0f)
                return;

            float spGain = QTESPCalculator.CalculateSPGainClick();
            float ourSPGainAbsolute = maxSp * spGain;
            float newSP = Mathf.Min(maxSp, spBeforeCalculation + ourSPGainAbsolute);
            ___playerstatus.Sp = newSP;

            if (newSP < maxSp)
            {
                // Undo NoREroMod escape priming from an inflated single-click fill.
                ___key_submit = false;
                ___key_atk = false;
            }
            else
            {
                // Legitimate full SP after HellGate-sized gain — allow vanilla stand-up.
                ___key_submit = true;
                ___downup = 1;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[QTE Disabler] Error in OverrideStruggleSPCalculation: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
