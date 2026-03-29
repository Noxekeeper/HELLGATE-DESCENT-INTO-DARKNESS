using HarmonyLib;

namespace NoREroMod;

/// <summary>
/// QTEStruggleWindowManager - Wrapper over StruggleSystem for bug fixes
/// 
/// GOAL: Fix bugs in StruggleSystem.isValidStruggleWindow() for levels -1 и 10
/// 
/// BUGS IN ORIGINAL:
/// - Уровень -1: timers are not reset (условие <= -9999f not срабатывает)
/// - Уровень 10: timers are not reset on change уровня
/// 
/// SOLUTION: Use existing system NoREroMod, but fix window check
/// </summary>
public static class QTEStruggleWindowManager {
    
    /// <summary>
    /// Проверяет, открыто ли window борьбы
    /// Fixes bugs for levels -1 и 10
    /// </summary>
    /// <returns>true if window открыто, false if locked</returns>
    public static bool IsWindowOpen() {
        try {
            // Use reflection for access to struggleLevel и struggleTimer
            var struggleLevelField = AccessTools.Field(typeof(StruggleSystem), "struggleLevel");
            var struggleTimerField = AccessTools.Field(typeof(StruggleSystem), "struggleTimer");
            
            if (struggleLevelField != null) {
                int struggleLevel = (int)struggleLevelField.GetValue(null);
                
                // Исправляем баг: for уровня -1 window always открыто
                // (in оригинале таймеры not работают correctly due to условия <= -9999f)
                if (struggleLevel == -1) {
                    return true; // Окbut always открыто
                }
                
                // Исправляем баг: for уровня 10 window always заперто
                // (in оригинале struggleTimer = 9999, need to ждать, пока it decreases until 0)
                // Но мы проверяем уровень directly, so that window закрывалось immediately
                if (struggleLevel == 10) {
                    // Дополнительно: force устанавливаем struggleTimer in большое значение,
                    // so that гарантировать, that window закрыто
                    if (struggleTimerField != null) {
                        float currentTimer = (float)struggleTimerField.GetValue(null);
                        // If таймер еще not set on большое значение, устанавливаем его
                        if (currentTimer < 9999f) {
                            struggleTimerField.SetValue(null, 9999f);
                        }
                    }
                    return false; // Окbut always заперто
                }
            }
            
            // For остальных уровней (0, 1, 2, 9) - используем original logic
            // (it works correctly for периодическtheir окон)
            return StruggleSystem.isValidStruggleWindow();
        } catch (System.Exception ex) {
            // If что-то пошло not так, используем original logic
            Plugin.Log?.LogError($"[QTE Window Manager] Error in IsWindowOpen: {ex.Message}");
            return StruggleSystem.isValidStruggleWindow();
        }
    }
    
    /// <summary>
    /// Проверяет, whether to penalize for clicks outside window
    /// Использует original logic (it works correctly)
    /// </summary>
    /// <returns>true if can штрафовать</returns>
    public static bool IsPunishableWindow() {
        try {
            // Use original logic (it works correctly)
            return StruggleSystem.isPunishableStruggleWindow();
        } catch (System.Exception ex) {
            Plugin.Log?.LogError($"[QTE Window Manager] Error in IsPunishableWindow: {ex.Message}");
            return false; // Безопасное значение by умолчанию
        }
    }
}
