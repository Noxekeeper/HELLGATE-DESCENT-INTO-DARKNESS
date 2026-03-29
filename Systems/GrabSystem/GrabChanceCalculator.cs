using System;
using System.Reflection;
using UnityEngine;
using NoREroMod;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Rage;

namespace NoREroMod.Systems.GrabSystem;

/// <summary>
/// Calculates grab probability when a melee attack hits the player.
/// Ranged attacks never trigger grab. Block reduces but does not eliminate grab.
/// MindBroken increases grab chance; Rage reduces it.
/// </summary>
internal static class GrabChanceCalculator
{
    private static FieldInfo _playerStatusField;
    private static FieldInfo _hpField;
    private static MethodInfo _allMaxHpMethod;
    private static FieldInfo _badstatusValField;

    /// <summary>
    /// Returns approximate melee grab chance for UI (normal melee, not blocking, non-knockdown).
    /// Includes MindBroken, low HP, Pleasure and Rage modifiers.
    /// </summary>
    internal static float GetApproxMeleeGrabChanceForUI()
    {
        try
        {
            var player = UnityEngine.Object.FindObjectOfType<playercon>();
            if (player == null)
            {
                Plugin.Log?.LogInfo("[GrabChanceUI] playercon not found for UI, returning 0");
                return 0f;
            }

            // UI baseline: approximate melee grab chance without block or knockdown.
            // Defaults are intentionally conservative; real logic uses slightly lower base.
            float grabChance = Plugin.grabChanceMelee?.Value ?? 0.3f;

            // MindBroken
            float mindBrokenPercent = MindBrokenSystem.Percent * 100f;
            float bonusPer10 = Plugin.grabChanceMindBrokenBonusPer10Percent?.Value ?? 0.05f;
            grabChance += (mindBrokenPercent / 10f) * bonusPer10;

            // Low HP bonus (same logic as in ShouldTriggerGrab)
            ApplyLowHpBonus(player, ref grabChance);

            // Pleasure (BadstatusVal[0]) bonus
            grabChance += GetPleasureBonus(player);

            // Rage
            float ragePercent = RageSystem.Percent;
            float rageReductionPerPercent = Plugin.grabChanceRageReductionPerPercent?.Value ?? 0.005f;
            float rageMultiplier = 1f - (ragePercent * rageReductionPerPercent);
            grabChance *= Mathf.Max(0f, rageMultiplier);

            return Mathf.Clamp01(grabChance);
        }
        catch
        {
            return 0f;
        }
    }

    /// <summary>
    /// Adds bonus for low HP to grabChance.
    /// </summary>
    private static void ApplyLowHpBonus(playercon player, ref float grabChance)
    {
        try
        {
            if (_playerStatusField == null)
            {
                _playerStatusField = typeof(playercon).GetField("playerstatus", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            object playerStatus = _playerStatusField?.GetValue(player);
            if (playerStatus == null)
                return;

            if (_hpField == null || _allMaxHpMethod == null)
            {
                var psType = playerStatus.GetType();
                _hpField = _hpField ?? psType.GetField("Hp", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _allMaxHpMethod = _allMaxHpMethod ?? psType.GetMethod("AllMaxHP", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (_hpField == null || _allMaxHpMethod == null)
                return;

            float hp = Convert.ToSingle(_hpField.GetValue(playerStatus));
            float maxHp = Convert.ToSingle(_allMaxHpMethod.Invoke(playerStatus, null));
            if (maxHp <= 0f)
                return;

        float hpPercent = Mathf.Clamp01(hp / maxHp);
        float lowHpThreshold = 0.10f; // 10%
            if (hpPercent <= lowHpThreshold)
            {
                grabChance += 0.25f;
            }
            else
            {
                float t = (1f - hpPercent) / (1f - lowHpThreshold);
                t = Mathf.Clamp01(t);
                grabChance += 0.25f * t;
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// Returns additional grab chance contribution from Pleasure gauge (BadstatusVal[0]) using config-driven max bonus.
    /// </summary>
    private static float GetPleasureBonus(playercon player)
    {
        try
        {
            if (_playerStatusField == null)
            {
                _playerStatusField = typeof(playercon).GetField("playerstatus", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            object playerStatus = _playerStatusField?.GetValue(player);
            if (playerStatus == null)
                return 0f;

            if (_badstatusValField == null)
            {
                var psType = playerStatus.GetType();
                _badstatusValField = psType.GetField("BadstatusVal", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (_badstatusValField == null)
                return 0f;

            var array = _badstatusValField.GetValue(playerStatus) as float[];
            if (array == null || array.Length == 0)
                return 0f;

            float pleasure = Mathf.Clamp(array[0], 0f, 100f);
            float maxBonus = Plugin.grabChancePleasureBonusMax?.Value ?? 0.20f;

            if (pleasure <= 0f || maxBonus <= 0f)
                return 0f;

            return (pleasure / 100f) * maxBonus;
        }
        catch
        {
            return 0f;
        }
    }

    /// <summary>
    /// Determines whether a grab should trigger for the given hit.
    /// </summary>
    /// <param name="player">Player instance.</param>
    /// <param name="kickbackkind">Knockback type (3, 4, 6 = knockdown/power attack).</param>
    /// <param name="isElite">Whether the attacker has &lt;SUPER&gt; in JPname.</param>
    internal static bool ShouldTriggerGrab(playercon player, int kickbackkind, bool isElite)
    {
        if (!(Plugin.enableGrabViaAttack?.Value ?? true))
            return false;

        // Prevent nested/duplicate grab attempts when player is already in H-scene state.
        if (player == null || player.eroflag || player.erodown != 0)
            return false;

        if (Plugin.grabViaAttackEliteOnly?.Value ?? false)
            if (!isElite)
                return false;

        if (DamageSourceClassifier.IsRanged)
            return false;

        if (StruggleSystem.isGrabInvul())
            return false;

        bool isGuarding = player.guard;
        bool isKnockdownAttack = DamageSourceClassifier.IsPowerAttack(kickbackkind);

        float grabChance;
        if (isGuarding)
        {
            grabChance = isKnockdownAttack
                ? (Plugin.grabChancePowerThroughBlock?.Value ?? 0.1f)
                : (Plugin.grabChanceThroughBlock?.Value ?? 0.05f);
        }
        else if (isKnockdownAttack)
        {
            // Default: 15% base chance for power/knockdown melee
            grabChance = Plugin.grabChancePowerAttack?.Value ?? 0.15f;
        }
        else
        {
            // Default: 10% base chance for normal melee
            grabChance = Plugin.grabChanceMelee?.Value ?? 0.10f;
        }

        // MindBroken: every 10% adds +2% grab chance (configurable)
        float mindBrokenPercent = MindBrokenSystem.Percent * 100f; // 0..100
        float bonusPer10 = Plugin.grabChanceMindBrokenBonusPer10Percent?.Value ?? 0.02f;
        grabChance += (mindBrokenPercent / 10f) * bonusPer10;

        // Low HP bonus: linearly scales up grab chance when player HP is low.
        ApplyLowHpBonus(player, ref grabChance);

        // Pleasure (BadstatusVal[0]) bonus: scaled 0..grabChancePleasureBonusMax
        grabChance += GetPleasureBonus(player);

        // Rage: more Rage = less grab chance (e.g. 0.5% reduction per 1% Rage -> at 100% Rage chance is halved)
        float ragePercent = RageSystem.Percent; // 0..100
        float rageReductionPerPercent = Plugin.grabChanceRageReductionPerPercent?.Value ?? 0.005f;
        float rageMultiplier = 1f - (ragePercent * rageReductionPerPercent);
        grabChance *= Mathf.Max(0f, rageMultiplier);

        grabChance = Mathf.Clamp01(grabChance);
        return UnityEngine.Random.value < grabChance;
    }
}
