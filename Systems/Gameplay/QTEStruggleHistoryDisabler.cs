using System;
using HarmonyLib;

namespace NoREroMod;

/// <summary>
/// QTEStruggleHistoryDisabler - disables UpdateStruggleHistory to prevent system escalation
/// 
/// GOAL: Block UpdateStruggleHistory() calls that update enemyStrugglePriors and playerEasyStruggles
/// </summary>
class QTEStruggleHistoryDisabler {
    
    /// <summary>
    /// Patch that disables UpdateStruggleHistory — this prevents system escalation
    /// Use AccessTools to obtain the type via reflection
    /// </summary>
    [HarmonyPatch]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    static bool DisableUpdateStruggleHistory() {
        // Block the UpdateStruggleHistory call so the system does not escalate
        // Plugin.Log?.LogInfo("[QTE History Disabler] UpdateStruggleHistory blocked - escalation disabled");
        return false; // Skip the original method
    }
    
    /// <summary>
    /// TargetMethod for the UpdateStruggleHistory patch
    /// Use reflection to obtain the method from PlayerConPatch
    /// </summary>
    static System.Reflection.MethodBase TargetMethod() {
        try {
            var playerConPatchType = HellGateTypeResolver.Resolve("NoREroMod.PlayerConPatch");
            if (playerConPatchType != null) {
                var method = AccessTools.Method(playerConPatchType, "UpdateStruggleHistory");
                if (method != null) {
                    // Plugin.Log?.LogInfo("[QTE History Disabler] Found UpdateStruggleHistory method, patching...");
                    return method;
                } else {
                    // Plugin.Log?.LogError("[QTE History Disabler] UpdateStruggleHistory method not found!");
                }
            } else {
                // Plugin.Log?.LogError("[QTE History Disabler] PlayerConPatch type not found!");
            }
        } catch (Exception ex) {
            Plugin.Log?.LogError($"[QTE History Disabler] Error finding UpdateStruggleHistory: {ex.Message}\n{ex.StackTrace}");
        }
        return null;
    }
}
