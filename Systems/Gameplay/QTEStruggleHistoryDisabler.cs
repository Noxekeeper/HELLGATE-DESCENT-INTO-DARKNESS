using System;
using HarmonyLib;

namespace NoREroMod;

/// <summary>
/// QTEStruggleHistoryDisabler - Отключение UpdateStruggleHistory for предотвращения усложнения системы
/// 
/// GOAL: Блокировать вызовы UpdateStruggleHistory(), которые обновляют enemyStrugglePriors и playerEasyStruggles
/// </summary>
class QTEStruggleHistoryDisabler {
    
    /// <summary>
    /// Patch for отключения UpdateStruggleHistory - this предотвращает усложнение системы
    /// Use AccessTools for получения типа via reflection
    /// </summary>
    [HarmonyPatch]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    static bool DisableUpdateStruggleHistory() {
        // Блокируем call UpdateStruggleHistory, so that система not усложнялась
        // Plugin.Log?.LogInfo("[QTE History Disabler] UpdateStruggleHistory blocked - система усложнения отключена");
        return false; // Skip оригинальный метод
    }
    
    /// <summary>
    /// TargetMethod for патча UpdateStruggleHistory
    /// Use reflection for получения метода from PlayerConPatch
    /// </summary>
    static System.Reflection.MethodBase TargetMethod() {
        try {
            var playerConPatchType = AccessTools.TypeByName("NoREroMod.PlayerConPatch");
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
