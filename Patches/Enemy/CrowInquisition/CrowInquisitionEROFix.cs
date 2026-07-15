using HarmonyLib;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod;

/// <summary>
/// Fix for CrowInquisition ERO phase to restore original animation cycles
/// Overrides Hellachaz time slowdown and auto-skip without removing base patches
/// Returns to original NoREroMod v0.11.5 behavior
/// </summary>
internal class CrowInquisitionEROFix
{
    /// <summary>
    /// No-op method for compatibility
    /// </summary>
    public static void UnpatchHellachaz()
    {
        // Method kept for compatibility but does nothing
        // We use runtime overrides instead of unpatching
    }

    /// <summary>
    /// Override Hellachaz behavior by resetting modifications in real-time
    /// Runs with very high priority to block time slowdown
    /// Sets dynamic struggle levels based on animation phases
    /// </summary>
    [HarmonyPatch(typeof(CrowInquisitionERO), "OnEvent")]
    [HarmonyPrefix]
    [HarmonyPriority(800)]
    static bool OverrideHellachazBehavior(CrowInquisitionERO __instance, SkeletonAnimation ___myspine, Spine.Event e, int ___se_count, ref int ___count)
    {
        try
        {
            string eventName = e?.ToString() ?? "";
            string animName = ___myspine?.AnimationName ?? "";

            // Handle animation transition events (START, IKISTART, IKI, ERO, etc.)
            // These events fire when animation switches
            if (eventName == "START")
            {
                // START animation begins - open window at start so player can try to escape
                StruggleSystem.setStruggleLevel(-1);
            }
            else if (eventName == "START2" || eventName == "START3" || eventName == "START4")
            {
                // Other START phases - close struggle window during buildup
                StruggleSystem.setStruggleLevel(10);
            }
            else if (eventName == "IKISTART")
            {
                // Transition to IKISTART - open struggle window
                StruggleSystem.setStruggleLevel(-1);
            }
            else if (eventName == "IKI" || eventName == "IKI2")
            {
                // IKI phase - window open for struggle
                StruggleSystem.setStruggleLevel(-1);
            }
            else if (eventName == "ERO" || eventName == "ERO2" || eventName == "EROFIN" || 
                     eventName == "2EROSTART" || eventName == "2ERO" || eventName == "2EROFIN" || eventName == "2EROFIN2")
            {
                // ERO phases - close window during action
                StruggleSystem.setStruggleLevel(10);
            }
            else if (eventName == "END" || eventName == "END2")
            {
                // Final phases - allow struggle
                StruggleSystem.setStruggleLevel(-1);
            }
            // Handle SE events within animations for fine-tuned control
            else if (eventName == "SE")
            {
                // START animation phase - dynamic window control
                if (animName == "START")
                {
                    if (___se_count == 1 || ___se_count == 2)
                    {
                        // Early stage - window open so player can try to escape
                        StruggleSystem.setStruggleLevel(-1);
                    }
                    else if (___se_count == 3 || ___se_count == 4 || ___se_count == 5)
                    {
                        // Initial stage - keep window open so player can struggle
                        // But block Hellachaz time slowdown
                        StruggleSystem.setStruggleLevel(-1);
                        return false;
                    }
                    else if (___se_count == 6 || ___se_count == 7)
                    {
                        // Near end of START phase - keep window closed
                        StruggleSystem.setStruggleLevel(10);
                    }
                }
                // IKISTART phase - adjust struggle level before IKI transition
                else if (animName == "IKISTART")
                {
                    if (___se_count == 6)
                    {
                        // Before IKI phase - normal struggle difficulty
                        StruggleSystem.setStruggleLevel(2);
                    }
                }
            }

            // Let base NoREroMod patches run for other phases
            return true;
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[CROW FIX] Error: {ex.Message}");
            return true;
        }
    }

    /// <summary>
    /// Cleanup after Hellachaz patches
    /// Stop coroutines and reset time scale
    /// </summary>
    [HarmonyPatch(typeof(CrowInquisitionERO), "OnEvent")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void CleanupAfterHellachaz(CrowInquisitionERO __instance)
    {
        try
        {
            // Reset time to normal only if TimeSlowMo is not active
            if (Time.timeScale != 1f && Time.timeScale != 0f && !NoREroMod.Systems.Rage.TimeSlowMoSystem.IsActive)
            {
                Time.timeScale = 1f;
            }

            // Stop any coroutines that skip animations
            if (__instance != null)
            {
                __instance.StopAllCoroutines();
            }
        }
        catch
        {
            // Silent fail
        }
    }

    /// <summary>
    /// Reset time when H-scene starts
    /// </summary>
    [HarmonyPatch(typeof(CrowInquisition), "eroanime")]
    [HarmonyPrefix]
    static void EnsureNormalTime()
    {
        // Only reset time if TimeSlowMo is not active
        if (!NoREroMod.Systems.Rage.TimeSlowMoSystem.IsActive)
        {
            Time.timeScale = 1f;
        }
    }
}



