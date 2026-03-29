using UnityEngine;
using NoREroMod.Patches.UI.MindBroken;

namespace NoREroMod;

/// <summary>
/// QTESPCalculator - Расчет SP gain for QTE системы
/// 
/// ОБЫЧНЫЕ КЛИКИ/НАЖАТИЯ: Линейная интерполяция from 1.6% until 0.2% depending on MindBroken
/// - 0% MB: 1.6% SP за клик/press
/// - 50% MB: 0.9% SP за клик/press
/// - 100% MB: 0.2% SP за клик/press
/// 
/// МОРГАЮЩИЕ (ЖЕЛТЫЕ) БУКВЫ: Линейная интерполяция from 15% until 10% (without changes)
/// - 0% MB: 15% SP за press (бонус)
/// - 50% MB: 12.5% SP за press
/// - 100% MB: 10% SP за press (бонус)
/// 
/// Примечание: Используется линейная интерполяция, that дает плавное уменьшение
/// on 0.5% за every 10% MB (or 0.25% за каждые 5% MB)
/// </summary>
public static class QTESPCalculator {
    
    // Базовые values SP gain for QTE-букin (обычные стрелки WASD)
    // Значения from config (with fallback on values by умолчанию)
    private static float BASE_SP_GAIN => Plugin.qteSPGainBase?.Value ?? 0.05f;
    private static float MIN_SP_GAIN => Plugin.qteSPGainMin?.Value ?? 0.02f;

    // Базовые values SP gain for клика (мышь / key E) — from config QTE
    private static float CLICK_BASE_SP_GAIN => Plugin.qteClickSPGainBase?.Value ?? 0.015f;
    private static float CLICK_MIN_SP_GAIN => Plugin.qteClickSPGainMin?.Value ?? 0.005f;
    
    // Базовые values for моргающtheir букin (бонус)
    // Моргающие (желтые) буквы дают бонус: 15% → 10% with MB влиянием
    private const float BASE_BLINKING_SP_GAIN = 0.15f;  // 15% on 0% MB
    private const float MIN_BLINKING_SP_GAIN = 0.10f;   // 10% at 100% MB
    
    // Базовые values for желтых кнопок up/down (QTE 3.0)
    // 5% on 0% MB -> линейbut until 2.5% at 100% MB (базовый, until x10 комбо)
    private const float BASE_YELLOW_BUTTON_SP_GAIN = 0.05f;  // 5% on 0% MB
    private const float MIN_YELLOW_BUTTON_SP_GAIN = 0.025f;  // 2.5% at 100% MB
    
    // Базовые values for желтых кнопок after x10 комбо
    // 10% on 0% MB -> линейbut until 5% at 100% MB (after x10 комбо)
    private const float BASE_YELLOW_BUTTON_COMBO_SP_GAIN = 0.10f;  // 10% on 0% MB
    private const float MIN_YELLOW_BUTTON_COMBO_SP_GAIN = 0.05f;   // 5% at 100% MB
    
    /// <summary>
    /// Получает текущий процент MindBroken (0.0 - 1.0)
    /// </summary>
    private static float GetMindBrokenPercent() {
        if (MindBrokenSystem.Enabled) {
            return Mathf.Clamp01(MindBrokenSystem.Percent);
        }
        return 0f;
    }
    
    /// <summary>
    /// Рассчитывает SP gain for обычной буквы/клика considering MindBroken
    /// Линейная интерполяция: 1.6% → 0.2%
    /// Используется for обычных кликоin мыши и обычных QTE нажатий
    /// </summary>
    /// <returns>SP gain (0.002 - 0.016)</returns>
    public static float CalculateSPGain() {
        float mbPercent = GetMindBrokenPercent();
        return CalculateSPGain(mbPercent);
    }

    /// <summary>
    /// Рассчитывает SP gain for клика (мышь / E) considering MindBroken
    /// Линейная интерполяция: 1.5% → 0.5%
    /// </summary>
    public static float CalculateSPGainClick() {
        float mbPercent = GetMindBrokenPercent();
        return CalculateSPGainClick(mbPercent);
    }
    
    /// <summary>
    /// Рассчитывает SP gain for обычной буквы considering MindBroken
    /// Линейная интерполяция: 1.6% → 0.2%
    /// </summary>
    /// <param name="mindBrokenPercent">Процент MindBroken (0.0 - 1.0)</param>
    /// <returns>SP gain (0.002 - 0.016)</returns>
    public static float CalculateSPGain(float mindBrokenPercent) {
        // Линейная интерполяция: 1.6% → 0.2%
        // This automatically дает уменьшение on 0.14% за every 10% MB
        return Mathf.Lerp(BASE_SP_GAIN, MIN_SP_GAIN, mindBrokenPercent);
        // 0% MB: 0.016 (1.6%)
        // 10% MB: 0.0146 (1.46%) - уменьшение on 0.14%
        // 50% MB: 0.009 (0.9%) - уменьшение on 0.7%
        // 100% MB: 0.002 (0.2%) - уменьшение on 1.4%
    }

    /// <summary>
    /// Рассчитывает SP gain for клика considering MindBroken
    /// Линейная интерполяция: 1.5% → 0.5%
    /// </summary>
    /// <param name="mindBrokenPercent">Процент MindBroken (0.0 - 1.0)</param>
    /// <returns>SP gain (0.005 - 0.015)</returns>
    public static float CalculateSPGainClick(float mindBrokenPercent) {
        // Линейная интерполяция: 1.5% → 0.5%
        return Mathf.Lerp(CLICK_BASE_SP_GAIN, CLICK_MIN_SP_GAIN, mindBrokenPercent);
        // 0% MB: 0.015 (1.5%)
        // 50% MB: 0.010 (1.0%)
        // 100% MB: 0.005 (0.5%)
    }
    
    /// <summary>
    /// Рассчитывает SP gain for моргающей буквы considering MindBroken
    /// Линейная интерполяция: 15% → 10%
    /// </summary>
    /// <returns>SP gain (0.10 - 0.15)</returns>
    public static float CalculateSPGainBlinking() {
        float mbPercent = GetMindBrokenPercent();
        return CalculateSPGainBlinking(mbPercent);
    }
    
    /// <summary>
    /// Рассчитывает SP gain for моргающей (желтой) буквы considering MindBroken
    /// Линейная интерполяция: 15% → 10%
    /// Моргающие буквы дают бонуwith by сравнению with обычными
    /// </summary>
    /// <param name="mindBrokenPercent">Процент MindBroken (0.0 - 1.0)</param>
    /// <returns>SP gain (0.10 - 0.15)</returns>
    public static float CalculateSPGainBlinking(float mindBrokenPercent) {
        // Линейная интерполяция: 15% → 10%
        return Mathf.Lerp(BASE_BLINKING_SP_GAIN, MIN_BLINKING_SP_GAIN, mindBrokenPercent);
        // 0% MB: 0.15 (15%)
        // 10% MB: 0.145 (14.5%)
        // 50% MB: 0.125 (12.5%)
        // 100% MB: 0.10 (10%)
    }
    
    /// <summary>
    /// Рассчитывает SP gain for желтой кнопки up/down (QTE 3.0)
    /// Линейная интерполяция: 5% → 2.5% (базовый, until x10 комбо)
    /// </summary>
    /// <returns>SP gain (0.025 - 0.05)</returns>
    public static float CalculateYellowButtonSPGain() {
        float mbPercent = GetMindBrokenPercent();
        return CalculateYellowButtonSPGain(mbPercent);
    }
    
    /// <summary>
    /// Рассчитывает SP gain for желтой кнопки considering MindBroken
    /// Линейная интерполяция: 5% → 2.5%
    /// </summary>
    /// <param name="mindBrokenPercent">Процент MindBroken (0.0 - 1.0)</param>
    /// <returns>SP gain (0.025 - 0.05)</returns>
    public static float CalculateYellowButtonSPGain(float mindBrokenPercent) {
        return Mathf.Lerp(BASE_YELLOW_BUTTON_SP_GAIN, MIN_YELLOW_BUTTON_SP_GAIN, mindBrokenPercent);
        // 0% MB: 0.05 (5%)
        // 50% MB: 0.0375 (3.75%)
        // 100% MB: 0.025 (2.5%)
    }
    
    /// <summary>
    /// Рассчитывает SP gain for желтой кнопки after x10 комбо (QTE 3.0)
    /// Линейная интерполяция: 10% → 5% (after x10 комбо)
    /// </summary>
    /// <returns>SP gain (0.05 - 0.10)</returns>
    public static float CalculateYellowButtonComboSPGain() {
        float mbPercent = GetMindBrokenPercent();
        return CalculateYellowButtonComboSPGain(mbPercent);
    }
    
    /// <summary>
    /// Рассчитывает SP gain for желтой кнопки after x10 комбо considering MindBroken
    /// Линейная интерполяция: 10% → 5%
    /// </summary>
    /// <param name="mindBrokenPercent">Процент MindBroken (0.0 - 1.0)</param>
    /// <returns>SP gain (0.05 - 0.10)</returns>
    public static float CalculateYellowButtonComboSPGain(float mindBrokenPercent) {
        return Mathf.Lerp(BASE_YELLOW_BUTTON_COMBO_SP_GAIN, MIN_YELLOW_BUTTON_COMBO_SP_GAIN, mindBrokenPercent);
        // 0% MB: 0.10 (10%)
        // 50% MB: 0.075 (7.5%)
        // 100% MB: 0.05 (5%)
    }
}
