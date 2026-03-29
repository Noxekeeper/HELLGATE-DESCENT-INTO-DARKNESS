using System;
using HarmonyLib;
using UnityEngine;
using Com.LuisPedroFonseca.ProCamera2D;

namespace NoREroMod;

/// <summary>
/// QTEStruggleSystemDisabler - Отключение ненужных систем NoREroMod for QTE
/// 
/// GOAL: Перехватить calculation SP in playercon.fun_nowdamage и убрать зависимости от:
/// - enemyStrugglePriors (история борьбы)
/// - playerEasyStruggles (легкие побеги)
/// - HP enemy
/// - struggleMultiplier
/// 
/// IMPORTANT: Эthe same патч must be applied AFTER патча NoREroMod, so that intercept its calculation SP
/// </summary>
class QTEStruggleSystemDisabler {
    
    // Сохраняем SP until вызова original method
    private static float spBeforeCalculation = 0f;
    
    // Сохраняем исходные values keys ДО патча NoREroMod
    private static bool originalKeySubmit = false;
    private static bool originalKeyAtk = false;
    
    // Кэш for access to PlayerConPatch.inPraymaidenStruggle via reflection
    private static System.Reflection.FieldInfo inPraymaidenStruggleField = null;
    
    /// <summary>
    /// Gets value PlayerConPatch.inPraymaidenStruggle via reflection
    /// </summary>
    private static bool GetInPraymaidenStruggle() {
        try {
            if (inPraymaidenStruggleField == null) {
                // Get тип PlayerConPatch via reflection
                var playerConPatchType = AccessTools.TypeByName("NoREroMod.PlayerConPatch");
                if (playerConPatchType != null) {
                    inPraymaidenStruggleField = AccessTools.Field(playerConPatchType, "inPraymaidenStruggle");
                }
            }
            
            if (inPraymaidenStruggleField != null) {
                return (bool)inPraymaidenStruggleField.GetValue(null);
            }
        } catch (Exception ex) {
            Plugin.Log?.LogError($"[QTE Disabler] Error getting inPraymaidenStruggle: {ex.Message}");
        }
        return false;
    }
    
    /// <summary>
    /// Prefix: Сохраняем SP until calculation
    /// </summary>
    [HarmonyPatch(typeof(playercon), "fun_nowdamage")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)] // Выполняется ПЕРЕД патчем NoREroMod
    static void SaveSPBeforeCalculation(
        PlayerStatus ___playerstatus,
        bool ___key_submit,
        bool ___key_atk) {
        if (___playerstatus != null) {
            spBeforeCalculation = ___playerstatus.Sp;
            // Сохраняем исходные values keys ДО того, as патч NoREroMod their обнулит
            originalKeySubmit = ___key_submit;
            originalKeyAtk = ___key_atk;
        }
    }
    
    
    /// <summary>
    /// Postfix: Перезаписываем SP gain after calculation NoREroMod
    /// Убираем зависимости from HP, priors, easyStruggles, multiplier
    /// Выполняется with НИЗКИМ приоритетом, so that быть ПОСЛЕ патча NoREroMod
    /// </summary>
    [HarmonyPatch(typeof(playercon), "fun_nowdamage")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)] // Выполняется ПОСЛЕ патча NoREroMod
    static void OverrideStruggleSPCalculation(
        playercon __instance,
        PlayerStatus ___playerstatus,
        ref bool ___key_submit,
        ref bool ___key_atk,
        ref bool ___key_item,
        ref int ___downup,
        ProCamera2DShake ___shake) {
        
        try {
            // ВРЕМЕННОЕ ЛОГИРОВАНИЕ: проверяем, is called ли патч вообще
            // Plugin.Log?.LogInfo($"[QTE Disabler] Postfix called: erodown={__instance.erodown}, _easyESC={__instance._easyESC}, _SOUSA={___playerstatus._SOUSA}, SP={___playerstatus.Sp:F1}");
            
            // Check if that игрок in H-сцене
            if (__instance.erodown != 0 && 
                !__instance._easyESC && 
                ___playerstatus._SOUSA) {
                
                // Use СОХРАНЕННЫЕ исходные values keys, а not текущие
                // (потому that патч NoREroMod already обнулил ___key_submit и ___key_atk)
                bool isStruggling = (originalKeySubmit || originalKeyAtk);
                
                // Get inPraymaidenStruggle via reflection
                bool inPraymaidenStruggle = GetInPraymaidenStruggle();
                
                // If игрок борется in H-сцене
                if (isStruggling && 
                    (__instance.eroflag || inPraymaidenStruggle)) {
                    
                    bool windowOpen = QTEStruggleWindowManager.IsWindowOpen();
                    
                    // Debug logging (отключено)
                    // Plugin.Log?.LogInfo($"[QTE Disabler] Struggle detected: erodown={__instance.erodown}, eroflag={__instance.eroflag}, inPraymaiden={inPraymaidenStruggle}, windowOpen={windowOpen}, SP before={spBeforeCalculation:F1}, SP now={___playerstatus.Sp:F1}, maxSP={___playerstatus.AllMaxSP():F1}");
                    
                    // If window открыто и SP < MaxSP
                    if (windowOpen && ___playerstatus.Sp < ___playerstatus.AllMaxSP()) {
                        // Вычисляем, сколько SP добавил оригинальный метод (NoREroMod)
                        float originalSPGain = ___playerstatus.Sp - spBeforeCalculation;
                        
                        // If оригинальный метод добавил SP (значит window было открыто)
                        if (originalSPGain > 0f) {
                            // Use калькулятор клика (мышь / E): 1.5% → 0.5% with MB влиянием
                            float spGain = QTESPCalculator.CalculateSPGainClick();
                            
                            // Get максимальный SP
                            float maxSp = ___playerstatus.AllMaxSP();
                            
                            // Перезаписываем SP: убираем оригинальный gain, добавляем наш
                            float ourSPGainAbsolute = maxSp * spGain;
                            float newSP = Mathf.Min(maxSp, spBeforeCalculation + ourSPGainAbsolute);
                            
                            // Debug logging (отключено)
                            // Plugin.Log?.LogInfo($"[QTE Disabler] SP override: original gain +{originalSPGain:F2} ({originalSPGain/maxSp*100:F1}%), our gain +{ourSPGainAbsolute:F2} ({spGain*100:F1}%), SP: {spBeforeCalculation:F1} -> {newSP:F1}/{maxSp:F1}");
                            
                            ___playerstatus.Sp = newSP;
                        } else {
                            // Debug logging (отключено)
                            // Plugin.Log?.LogInfo($"[QTE Disabler] No SP gain from original method (originalSPGain={originalSPGain:F2})");
                        }
                    }
                }
            }
        } catch (Exception ex) {
            Plugin.Log?.LogError($"[QTE Disabler] Error in OverrideStruggleSPCalculation: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
