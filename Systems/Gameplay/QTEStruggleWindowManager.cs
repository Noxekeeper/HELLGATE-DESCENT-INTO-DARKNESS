using HarmonyLib;

namespace NoREroMod;

/// <summary>
/// QTEStruggleWindowManager - Wrapper over StruggleSystem for bug fixes
/// 
/// GOAL: Fix bugs in StruggleSystem.isValidStruggleWindow() for levels -1 and 10
/// 
/// BUGS IN ORIGINAL:
/// - Level -1: timers are not reset (condition <= -9999f never triggers)
/// - Level 10: timers are not reset on level change
/// 
/// SOLUTION: Use existing system NoREroMod, but fix window check
/// </summary>
public static class QTEStruggleWindowManager {
    
    /// <summary>
    /// Checks whether the struggle window is open
    /// Fixes bugs for levels -1 and 10
    /// </summary>
    /// <returns>true if the window is open, false if locked</returns>
    public static bool IsWindowOpen() {
        try {
            // Use reflection to access struggleLevel and struggleTimer
            var struggleLevelField = AccessTools.Field(typeof(StruggleSystem), "struggleLevel");
            var struggleTimerField = AccessTools.Field(typeof(StruggleSystem), "struggleTimer");
            
            if (struggleLevelField != null) {
                int struggleLevel = (int)struggleLevelField.GetValue(null);
                
                // Bug fix: for level -1 the window is always open
                // (in the original, timers do not work correctly due to the <= -9999f condition)
                if (struggleLevel == -1) {
                    return true; // Window always open
                }
                
                // Bug fix: for level 10 the window is always locked
                // (in the original, struggleTimer = 9999 and you must wait until it decreases to 0)
                // We check the level directly so the window closes immediately
                if (struggleLevel == 10) {
                    // Additionally: force-set struggleTimer to a large value,
                    // to guarantee the window stays closed
                    if (struggleTimerField != null) {
                        float currentTimer = (float)struggleTimerField.GetValue(null);
                        // If the timer is not yet set to a large value, set it
                        if (currentTimer < 9999f) {
                            struggleTimerField.SetValue(null, 9999f);
                        }
                    }
                    return false; // Window always locked
                }
            }
            
            // For other levels (0, 1, 2, 9) — use original logic
            // (it works correctly for periodic windows)
            return StruggleSystem.isValidStruggleWindow();
        } catch (System.Exception ex) {
            // If something goes wrong, use original logic
            Plugin.Log?.LogError($"[QTE Window Manager] Error in IsWindowOpen: {ex.Message}");
            return StruggleSystem.isValidStruggleWindow();
        }
    }
    
    /// <summary>
    /// Checks whether to penalize clicks outside the window
    /// Uses original logic (it works correctly)
    /// </summary>
    /// <returns>true if a penalty can be applied</returns>
    public static bool IsPunishableWindow() {
        try {
            // Use original logic (it works correctly)
            return StruggleSystem.isPunishableStruggleWindow();
        } catch (System.Exception ex) {
            Plugin.Log?.LogError($"[QTE Window Manager] Error in IsPunishableWindow: {ex.Message}");
            return false; // Safe default value
        }
    }
}
