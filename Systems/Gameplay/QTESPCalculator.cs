using UnityEngine;
using NoREroMod.Patches.UI.MindBroken;

namespace NoREroMod;

/// <summary>
/// QTESPCalculator - SP gain calculation for the QTE system
/// 
/// NORMAL CLICKS/PRESSES: linear interpolation from 1.6% to 0.2% based on MindBroken
/// - 0% MB: 1.6% SP per click/press
/// - 50% MB: 0.9% SP per click/press
/// - 100% MB: 0.2% SP per click/press
/// 
/// BLINKING (YELLOW) LETTERS: linear interpolation from 15% to 10% (unchanged)
/// - 0% MB: 15% SP per press (bonus)
/// - 50% MB: 12.5% SP per press
/// - 100% MB: 10% SP per press (bonus)
/// 
/// Note: linear interpolation is used for a smooth decrease
/// of 0.5% per every 10% MB (or 0.25% per every 5% MB)
/// </summary>
public static class QTESPCalculator {
    
    // Base SP gain values for QTE letters (normal WASD arrows)
    // Values from config (with fallback to defaults)
    private static float BASE_SP_GAIN => Plugin.qteSPGainBase?.Value ?? 0.05f;
    private static float MIN_SP_GAIN => Plugin.qteSPGainMin?.Value ?? 0.02f;

    // Base SP gain values for click (mouse / E key) — from QTE config
    private static float CLICK_BASE_SP_GAIN => Plugin.qteClickSPGainBase?.Value ?? 0.015f;
    private static float CLICK_MIN_SP_GAIN => Plugin.qteClickSPGainMin?.Value ?? 0.005f;
    
    // Base values for blinking letters (bonus)
    // Blinking (yellow) letters give a bonus: 15% → 10% with MB influence
    private const float BASE_BLINKING_SP_GAIN = 0.15f;  // 15% on 0% MB
    private const float MIN_BLINKING_SP_GAIN = 0.10f;   // 10% at 100% MB
    
    // Base values for yellow up/down buttons (QTE 3.0)
    // 5% at 0% MB -> linear to 2.5% at 100% MB (base, until x10 combo)
    private const float BASE_YELLOW_BUTTON_SP_GAIN = 0.05f;  // 5% on 0% MB
    private const float MIN_YELLOW_BUTTON_SP_GAIN = 0.025f;  // 2.5% at 100% MB
    
    // Base values for yellow buttons after x10 combo
    // 10% at 0% MB -> linear to 5% at 100% MB (after x10 combo)
    private const float BASE_YELLOW_BUTTON_COMBO_SP_GAIN = 0.10f;  // 10% on 0% MB
    private const float MIN_YELLOW_BUTTON_COMBO_SP_GAIN = 0.05f;   // 5% at 100% MB
    
    /// <summary>
    /// Gets the current MindBroken percent (0.0 - 1.0)
    /// </summary>
    private static float GetMindBrokenPercent() {
        if (MindBrokenSystem.Enabled) {
            return Mathf.Clamp01(MindBrokenSystem.Percent);
        }
        return 0f;
    }
    
    /// <summary>
    /// Calculates SP gain for a normal letter/click considering MindBroken
    /// Linear interpolation: 1.6% → 0.2%
    /// Used for normal mouse clicks and normal QTE presses
    /// </summary>
    /// <returns>SP gain (0.002 - 0.016)</returns>
    public static float CalculateSPGain() {
        float mbPercent = GetMindBrokenPercent();
        return CalculateSPGain(mbPercent);
    }

    /// <summary>
    /// Calculates SP gain for a click (mouse / E) considering MindBroken
    /// Linear interpolation: 1.5% → 0.5%
    /// </summary>
    public static float CalculateSPGainClick() {
        float mbPercent = GetMindBrokenPercent();
        return CalculateSPGainClick(mbPercent);
    }
    
    /// <summary>
    /// Calculates SP gain for a normal letter considering MindBroken
    /// Linear interpolation: 1.6% → 0.2%
    /// </summary>
    /// <param name="mindBrokenPercent">MindBroken percent (0.0 - 1.0)</param>
    /// <returns>SP gain (0.002 - 0.016)</returns>
    public static float CalculateSPGain(float mindBrokenPercent) {
        // Linear interpolation: 1.6% → 0.2%
        // This automatically decreases by 0.14% per every 10% MB
        return Mathf.Lerp(BASE_SP_GAIN, MIN_SP_GAIN, mindBrokenPercent);
        // 0% MB: 0.016 (1.6%)
        // 10% MB: 0.0146 (1.46%) — decrease of 0.14%
        // 50% MB: 0.009 (0.9%) — decrease of 0.7%
        // 100% MB: 0.002 (0.2%) — decrease of 1.4%
    }

    /// <summary>
    /// Calculates SP gain for a click considering MindBroken
    /// Linear interpolation: 1.5% → 0.5%
    /// </summary>
    /// <param name="mindBrokenPercent">MindBroken percent (0.0 - 1.0)</param>
    /// <returns>SP gain (0.005 - 0.015)</returns>
    public static float CalculateSPGainClick(float mindBrokenPercent) {
        // Linear interpolation: 1.5% → 0.5%
        return Mathf.Lerp(CLICK_BASE_SP_GAIN, CLICK_MIN_SP_GAIN, mindBrokenPercent);
        // 0% MB: 0.015 (1.5%)
        // 50% MB: 0.010 (1.0%)
        // 100% MB: 0.005 (0.5%)
    }
    
    /// <summary>
    /// Calculates SP gain for a blinking letter considering MindBroken
    /// Linear interpolation: 15% → 10%
    /// </summary>
    /// <returns>SP gain (0.10 - 0.15)</returns>
    public static float CalculateSPGainBlinking() {
        float mbPercent = GetMindBrokenPercent();
        return CalculateSPGainBlinking(mbPercent);
    }
    
    /// <summary>
    /// Calculates SP gain for a blinking (yellow) letter considering MindBroken
    /// Linear interpolation: 15% → 10%
    /// Blinking letters give a bonus compared to normal ones
    /// </summary>
    /// <param name="mindBrokenPercent">MindBroken percent (0.0 - 1.0)</param>
    /// <returns>SP gain (0.10 - 0.15)</returns>
    public static float CalculateSPGainBlinking(float mindBrokenPercent) {
        // Linear interpolation: 15% → 10%
        return Mathf.Lerp(BASE_BLINKING_SP_GAIN, MIN_BLINKING_SP_GAIN, mindBrokenPercent);
        // 0% MB: 0.15 (15%)
        // 10% MB: 0.145 (14.5%)
        // 50% MB: 0.125 (12.5%)
        // 100% MB: 0.10 (10%)
    }
    
    /// <summary>
    /// Calculates SP gain for a yellow up/down button (QTE 3.0)
    /// Linear interpolation: 5% → 2.5% (base, until x10 combo)
    /// </summary>
    /// <returns>SP gain (0.025 - 0.05)</returns>
    public static float CalculateYellowButtonSPGain() {
        float mbPercent = GetMindBrokenPercent();
        return CalculateYellowButtonSPGain(mbPercent);
    }
    
    /// <summary>
    /// Calculates SP gain for a yellow button considering MindBroken
    /// Linear interpolation: 5% → 2.5%
    /// </summary>
    /// <param name="mindBrokenPercent">MindBroken percent (0.0 - 1.0)</param>
    /// <returns>SP gain (0.025 - 0.05)</returns>
    public static float CalculateYellowButtonSPGain(float mindBrokenPercent) {
        return Mathf.Lerp(BASE_YELLOW_BUTTON_SP_GAIN, MIN_YELLOW_BUTTON_SP_GAIN, mindBrokenPercent);
        // 0% MB: 0.05 (5%)
        // 50% MB: 0.0375 (3.75%)
        // 100% MB: 0.025 (2.5%)
    }
    
    /// <summary>
    /// Calculates SP gain for a yellow button after x10 combo (QTE 3.0)
    /// Linear interpolation: 10% → 5% (after x10 combo)
    /// </summary>
    /// <returns>SP gain (0.05 - 0.10)</returns>
    public static float CalculateYellowButtonComboSPGain() {
        float mbPercent = GetMindBrokenPercent();
        return CalculateYellowButtonComboSPGain(mbPercent);
    }
    
    /// <summary>
    /// Calculates SP gain for a yellow button after x10 combo considering MindBroken
    /// Linear interpolation: 10% → 5%
    /// </summary>
    /// <param name="mindBrokenPercent">MindBroken percent (0.0 - 1.0)</param>
    /// <returns>SP gain (0.05 - 0.10)</returns>
    public static float CalculateYellowButtonComboSPGain(float mindBrokenPercent) {
        return Mathf.Lerp(BASE_YELLOW_BUTTON_COMBO_SP_GAIN, MIN_YELLOW_BUTTON_COMBO_SP_GAIN, mindBrokenPercent);
        // 0% MB: 0.10 (10%)
        // 50% MB: 0.075 (7.5%)
        // 100% MB: 0.05 (5%)
    }
}
