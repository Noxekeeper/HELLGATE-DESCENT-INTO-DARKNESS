using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using NoREroMod.Systems.Rage;
using AccessTools = HarmonyLib.AccessTools;

namespace NoREroMod.Systems.Rage.Patches;

/// <summary>
/// Patch for increasing damage and restoring SP during active Rage Mode
/// </summary>
internal static class RageInputPatch
{
    // Critical damage multiplier during Rage (configurable)
    private static float RAGE_CRIT_MULTIPLIER => Plugin.rageCritMultiplier?.Value ?? 1.5f;
    
    // SP restoration on attack click (configurable)
    private static float RAGE_SP_GAIN_PERCENT => Plugin.rageSPGainPercent?.Value ?? 0.5f;

    private static bool _lastAttackButtonState = false;

    /// <summary>
    /// Patch for playercon.Getinput(): restores SP on each attack click during Rage.
    /// Disabled while grabbed (eroflag) or knocked down (erodown != 0).
    /// </summary>
    [HarmonyPatch(typeof(playercon), "Getinput")]
    [HarmonyPostfix]
    private static void Getinput_Postfix(playercon __instance, PlayerStatus ___playerstatus)
    {
        try
        {
            if (__instance == null) return;
            
            if (!RageSystem.Enabled || !RageSystem.IsActive)
            {
                return;
            }
            
            if (___playerstatus == null) return;
            
            // Disable SP boost while the player is grabbed or down.
            bool isGrabbed = __instance.eroflag;
            bool isDown = __instance.erodown != 0;
            
            if (isGrabbed || isDown)
            {
                // Keep input edge detection consistent while boost is disabled.
                _lastAttackButtonState = false;
                return;
            }
            
            // Resolve private "player" field via AccessTools.
            var playerField = AccessTools.Field(typeof(playercon), "player");
            if (playerField == null)
            {
                Plugin.Log?.LogError("[RAGE SP] Field 'player' was not found in playercon.");
                return;
            }
            
            var playerObj = playerField.GetValue(__instance);
            if (playerObj == null) return;
            
            // Use a dynamic call for GetButtonDown.
            var getButtonDownMethod = playerObj.GetType().GetMethod("GetButtonDown", new[] { typeof(string) });
            if (getButtonDownMethod == null)
            {
                Plugin.Log?.LogError("[RAGE SP] Method GetButtonDown was not found.");
                return;
            }
            
            bool currentAttackButton = (bool)getButtonDownMethod.Invoke(playerObj, new object[] { "Attack" });
            
            // Trigger only on key-down edge.
            if (currentAttackButton && !_lastAttackButtonState)
            {
                // Restore SP on each attack click when Rage is active.
                float maxSP = ___playerstatus.AllMaxSP();
                float spGain = maxSP * RAGE_SP_GAIN_PERCENT; // +50% of max SP
                
                ___playerstatus.Sp += spGain;
            }
            
            _lastAttackButtonState = currentAttackButton;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RAGE SP] Error in Getinput SP patch: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Patch on PlayerStatus.Critical() - make all attacks critical during Rage
    /// This gives 1.5x damage multiplier for guaranteed critical hits
    /// </summary>
    [HarmonyPatch(typeof(PlayerStatus), "Critical")]
    [HarmonyPostfix]
    private static void Critical_Postfix(PlayerStatus __instance)
    {
        try
        {
            if (!RageSystem.Enabled || !RageSystem.IsActive) return;

            // During Rage, all attacks are critical (1.5x damage)
            // Use reflection to set the private Criticalcheck field
            var criticalCheckField = typeof(PlayerStatus).GetField("Criticalcheck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (criticalCheckField != null)
            {
                criticalCheckField.SetValue(__instance, true);
                // Plugin.Log?.LogInfo("[RAGE CRIT] Forced critical hit during Rage"); // Disabled for performance
            }
            else
            {
                Plugin.Log?.LogError("[RAGE CRIT] Could not find Criticalcheck field");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RAGE CRIT] Error in Critical postfix: {ex.Message}");
        }
    }

    /// <summary>
    /// Patch on EnemyDate.MagicDamage() - add critical hit support for magic during Rage
    /// </summary>
    [HarmonyPatch(typeof(EnemyDate), "MagicDamage")]
    [HarmonyPrefix]
    private static void MagicDamage_Prefix(EnemyDate __instance, ref float[] attribute)
    {
        try
        {
            if (!RageSystem.Enabled || !RageSystem.IsActive) return;
            if (__instance == null || attribute == null) return;

            // During Rage, multiply magic damage by crit multiplier
            for (int i = 0; i < attribute.Length; i++)
            {
                attribute[i] *= RAGE_CRIT_MULTIPLIER;
            }
            // Plugin.Log?.LogInfo("[RAGE MAGIC] Boosted magic damage during Rage"); // Disabled for performance
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RAGE MAGIC] Error in MagicDamage prefix: {ex.Message}");
        }
    }

    /// <summary>
    /// Patch on EnemyDate.DPSMagicDamage() - add critical hit support for DPS magic during Rage
    /// </summary>
    [HarmonyPatch(typeof(EnemyDate), "DPSMagicDamage")]
    [HarmonyPrefix]
    private static void DPSMagicDamage_Prefix(EnemyDate __instance, ref float[] attribute)
    {
        try
        {
            if (!RageSystem.Enabled || !RageSystem.IsActive) return;
            if (__instance == null || attribute == null) return;

            // During Rage, multiply DPS magic damage by crit multiplier
            for (int i = 0; i < attribute.Length; i++)
            {
                attribute[i] *= RAGE_CRIT_MULTIPLIER;
            }
            // Plugin.Log?.LogInfo("[RAGE MAGIC] Boosted DPS magic damage during Rage"); // Disabled for performance
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RAGE MAGIC] Error in DPSMagicDamage prefix: {ex.Message}");
        }
    }

    /// <summary>
    /// Patch for GAmng.Update. Required for SlowMo stability:
    /// GAmng.Update() can override timeScale, so we restore SlowMo in postfix.
    /// RageSystem.Update() runs earlier in frame order.
    /// </summary>
    [HarmonyPatch(typeof(GAmng), "Update")]
    [HarmonyPostfix]
    private static void GAmngUpdate_Postfix()
    {
        try
        {
            if (!RageSystem.Enabled) return;
            TimeSlowMoSystem.Update();
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogError($"[RAGE INPUT] Error in GAmng slow-mo patch: {ex.Message}\n{ex.StackTrace}");
        }
    }
}

