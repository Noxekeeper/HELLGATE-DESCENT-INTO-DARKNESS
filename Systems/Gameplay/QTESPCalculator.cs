using UnityEngine;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Fear;

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
/// Fear/Morale (after MB): ±2.5% SP per ±10 Morale (linear). −10 → ×0.975, +10 → ×1.025.
/// Applies to Struggle clicks, normal QTE, SimpleQTE/Free Struggle, yellow buttons.
/// </summary>
public static class QTESPCalculator
{
    // Base SP gain values for QTE letters (normal WASD arrows)
    // Values from config (with fallback to defaults)
    private static float BASE_SP_GAIN => Plugin.qteSPGainBase?.Value ?? 0.05f;
    private static float MIN_SP_GAIN => Plugin.qteSPGainMin?.Value ?? 0.02f;

    // Base SP gain values for click (mouse / E key) — from QTE config
    private static float CLICK_BASE_SP_GAIN => Plugin.qteClickSPGainBase?.Value ?? 0.015f;
    private static float CLICK_MIN_SP_GAIN => Plugin.qteClickSPGainMin?.Value ?? 0.005f;

    // Base values for blinking letters (bonus)
    // Blinking (yellow) letters give a bonus: 15% → 10% with MB influence
    private const float BASE_BLINKING_SP_GAIN = 0.15f; // 15% on 0% MB
    private const float MIN_BLINKING_SP_GAIN = 0.10f; // 10% at 100% MB

    // Base values for yellow up/down buttons (QTE 3.0)
    // 5% at 0% MB -> linear to 2.5% at 100% MB (base, until x10 combo)
    private const float BASE_YELLOW_BUTTON_SP_GAIN = 0.05f; // 5% on 0% MB
    private const float MIN_YELLOW_BUTTON_SP_GAIN = 0.025f; // 2.5% at 100% MB

    // Base values for yellow buttons after x10 combo
    // 10% at 0% MB -> linear to 5% at 100% MB (after x10 combo)
    private const float BASE_YELLOW_BUTTON_COMBO_SP_GAIN = 0.10f; // 10% on 0% MB
    private const float MIN_YELLOW_BUTTON_COMBO_SP_GAIN = 0.05f; // 5% at 100% MB

    /// <summary>
    /// Gets the current MindBroken percent (0.0 - 1.0)
    /// </summary>
    private static float GetMindBrokenPercent()
    {
        if (MindBrokenSystem.Enabled)
            return Mathf.Clamp01(MindBrokenSystem.Percent);
        return 0f;
    }

    /// <summary>
    /// Linear Morale mult for Struggle / QTE / SimpleQTE SP gains.
    /// Default: ±2.5% per ±10 Morale (−100 → ×0.75, 0 → ×1, +100 → ×1.25).
    /// </summary>
    public static float ApplyMoraleStruggleMult(float spGain)
    {
        if (!FearSystem.Enabled || !(FearConfig.EnableCombatEffects?.Value ?? true))
            return spGain;

        float perTen = FearConfig.StruggleSpMultPerTenMorale?.Value ?? 0.025f;
        if (Mathf.Abs(perTen) < 0.0001f)
            return spGain;

        float mult = 1f + (FearSystem.Morale / 10f) * perTen;
        if (mult < 0.05f)
            mult = 0.05f;
        return spGain * mult;
    }

    /// <summary>
    /// Calculates SP gain for a normal letter/click considering MindBroken
    /// Linear interpolation: 1.6% → 0.2%
    /// Used for normal mouse clicks and normal QTE presses
    /// </summary>
    public static float CalculateSPGain()
    {
        float mbPercent = GetMindBrokenPercent();
        return CalculateSPGain(mbPercent);
    }

    /// <summary>
    /// Calculates SP gain for a click (mouse / E) considering MindBroken
    /// Linear interpolation: 1.5% → 0.5%
    /// </summary>
    public static float CalculateSPGainClick()
    {
        float mbPercent = GetMindBrokenPercent();
        return CalculateSPGainClick(mbPercent);
    }

    /// <summary>
    /// Calculates SP gain for a normal letter considering MindBroken
    /// Linear interpolation: 1.6% → 0.2%
    /// </summary>
    public static float CalculateSPGain(float mindBrokenPercent)
    {
        return ApplyMoraleStruggleMult(Mathf.Lerp(BASE_SP_GAIN, MIN_SP_GAIN, mindBrokenPercent));
    }

    /// <summary>
    /// Calculates SP gain for a click considering MindBroken
    /// Linear interpolation: 1.5% → 0.5%
    /// </summary>
    public static float CalculateSPGainClick(float mindBrokenPercent)
    {
        return ApplyMoraleStruggleMult(Mathf.Lerp(CLICK_BASE_SP_GAIN, CLICK_MIN_SP_GAIN, mindBrokenPercent));
    }

    /// <summary>
    /// Calculates SP gain for a blinking letter considering MindBroken
    /// Linear interpolation: 15% → 10%
    /// </summary>
    public static float CalculateSPGainBlinking()
    {
        float mbPercent = GetMindBrokenPercent();
        return CalculateSPGainBlinking(mbPercent);
    }

    /// <summary>
    /// Calculates SP gain for a blinking (yellow) letter considering MindBroken
    /// Linear interpolation: 15% → 10%
    /// </summary>
    public static float CalculateSPGainBlinking(float mindBrokenPercent)
    {
        return ApplyMoraleStruggleMult(Mathf.Lerp(BASE_BLINKING_SP_GAIN, MIN_BLINKING_SP_GAIN, mindBrokenPercent));
    }

    /// <summary>
    /// Calculates SP gain for a yellow up/down button (QTE 3.0)
    /// Linear interpolation: 5% → 2.5% (base, until x10 combo)
    /// </summary>
    public static float CalculateYellowButtonSPGain()
    {
        float mbPercent = GetMindBrokenPercent();
        return CalculateYellowButtonSPGain(mbPercent);
    }

    /// <summary>
    /// Calculates SP gain for a yellow button considering MindBroken
    /// Linear interpolation: 5% → 2.5%
    /// </summary>
    public static float CalculateYellowButtonSPGain(float mindBrokenPercent)
    {
        return ApplyMoraleStruggleMult(Mathf.Lerp(BASE_YELLOW_BUTTON_SP_GAIN, MIN_YELLOW_BUTTON_SP_GAIN, mindBrokenPercent));
    }

    /// <summary>
    /// Calculates SP gain for a yellow button after x10 combo (QTE 3.0)
    /// Linear interpolation: 10% → 5% (after x10 combo)
    /// </summary>
    public static float CalculateYellowButtonComboSPGain()
    {
        float mbPercent = GetMindBrokenPercent();
        return CalculateYellowButtonComboSPGain(mbPercent);
    }

    /// <summary>
    /// Calculates SP gain for a yellow button after x10 combo considering MindBroken
    /// Linear interpolation: 10% → 5%
    /// </summary>
    public static float CalculateYellowButtonComboSPGain(float mindBrokenPercent)
    {
        return ApplyMoraleStruggleMult(
            Mathf.Lerp(BASE_YELLOW_BUTTON_COMBO_SP_GAIN, MIN_YELLOW_BUTTON_COMBO_SP_GAIN, mindBrokenPercent));
    }
}
