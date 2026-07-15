using System;
using UnityEngine;

namespace NoREroMod.Systems.Rage;

/// <summary>
/// Combo system for Rage: base gain per hit (scaled) + flat milestone bonuses every 10th hit (+1%, +2%, +3%... on the bar).
/// Timeout from config.
/// </summary>
internal static class RageComboSystem
{
    private static int _comboCount = 0;
    private static float _lastHitTime = 0f;
    private static float COMBO_TIMEOUT => Plugin.rageComboTimeout?.Value ?? 2f;
    private static float BASE_RAGE_GAIN => Plugin.rageComboBaseGain?.Value ?? 0.5f;
    // Global balance: reduce all hit/combo Rage gain 3x (even if cfg values are high).
    private const float HIT_RAGE_SCALE = 1f / 3f;
    private static float GAIN_MULTIPLIER => Mathf.Max(0f, Plugin.rageComboGainMultiplier?.Value ?? 1f) * HIT_RAGE_SCALE;

    internal static event Action<int>? OnComboChanged;
    internal static event Action? OnComboReset;

    internal static int ComboCount => _comboCount;

    internal static void RegisterHit()
    {
        if (!RageSystem.Enabled) return;

        float currentTime = Time.unscaledTime;

        if (_lastHitTime > 0f && currentTime - _lastHitTime > COMBO_TIMEOUT)
            ResetCombo();

        _comboCount++;
        _lastHitTime = currentTime;

        float baseGain = BASE_RAGE_GAIN * GAIN_MULTIPLIER;
        float milestoneGain = GetMilestoneRagePercentFlat(_comboCount);
        float total = baseGain + milestoneGain;
        string reason = milestoneGain > 0f
            ? $"hit_combo_{_comboCount}_milestone_{milestoneGain:F1}"
            : $"hit_combo_{_comboCount}";
        RageSystem.AddRage(total, reason);
        OnComboChanged?.Invoke(_comboCount);
    }

    /// <summary>
    /// Every 10th hit (10, 20, 30...): flat % on the rage bar — +1, +2, +3, +4...
    /// Not scaled by ComboGainMultiplier or HIT_RAGE_SCALE.
    /// </summary>
    private static float GetMilestoneRagePercentFlat(int comboCount)
    {
        if (comboCount < 10 || comboCount % 10 != 0)
            return 0f;
        int n = comboCount / 10;
        return 1f + (n - 1);
    }

    internal static void ResetCombo(string reason = "timeout")
    {
        if (_comboCount == 0) return;

        _comboCount = 0;
        _lastHitTime = 0f;
        OnComboReset?.Invoke();
        OnComboChanged?.Invoke(0);
    }

    internal static void Update()
    {
        if (!RageSystem.Enabled) return;
        if (_comboCount == 0) return;

        float currentTime = Time.unscaledTime;
        if (_lastHitTime > 0f && currentTime - _lastHitTime > COMBO_TIMEOUT)
            ResetCombo("timeout");
    }

    internal static void ResetState()
    {
        _comboCount = 0;
        _lastHitTime = 0f;
        OnComboReset?.Invoke();
        OnComboChanged?.Invoke(0);
    }
}
