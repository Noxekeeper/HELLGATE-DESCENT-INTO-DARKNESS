using System;
using System.Reflection;
using UnityEngine;
using NoREroMod;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.EventCore.Host;
using NoREroMod.Systems.Rage;

namespace NoREroMod.Systems.GrabSystem;

/// <summary>
/// Calculates grab probability when a melee attack hits the player.
/// Ranged attacks never trigger grab. Dash, parry and post-hit i-frames block grab.
/// Block can fully deny grab (default) or use through-block chances from config.
/// MindBroken increases grab chance; Rage reduces it. Modifiers apply only when base chance &gt; 0.
/// </summary>
internal static class GrabChanceCalculator
{
    private static FieldInfo _playerStatusField;
    private static FieldInfo _hpField;
    private static MethodInfo _allMaxHpMethod;
    private static FieldInfo _badstatusValField;

    /// <summary>
    /// Dash, parry, post-hit i-frames, and optional full block immunity.
    /// Does not apply to EventCore consent grab (checked before this in <see cref="ShouldTriggerGrab"/>).
    /// </summary>
    internal static bool IsPlayerDefensivelyImmuneToGrab(playercon player)
    {
        if (player == null)
            return true;

        if (player.stepfrag)
            return true;

        if (player._parrynow)
            return true;

        if (player.mutekitime)
            return true;

        if ((Plugin.grabBlockImmunity?.Value ?? true) && player.guard)
            return true;

        return false;
    }

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
                return 0f;

            if (IsPlayerDefensivelyImmuneToGrab(player))
                return 0f;

            float grabChance = Plugin.grabChanceMelee?.Value ?? 0.3f;
            if (grabChance <= 0f)
                return 0f;

            ApplyGrabModifiers(player, ref grabChance);
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

    private static void ApplyGrabModifiers(playercon player, ref float grabChance)
    {
        if (grabChance <= 0f)
            return;

        float mindBrokenPercent = MindBrokenSystem.Percent * 100f;
        float bonusPer10 = Plugin.grabChanceMindBrokenBonusPer10Percent?.Value ?? 0.02f;
        grabChance += (mindBrokenPercent / 10f) * bonusPer10;

        ApplyLowHpBonus(player, ref grabChance);
        grabChance += GetPleasureBonus(player);

        float ragePercent = RageSystem.Percent;
        float rageReductionPerPercent = Plugin.grabChanceRageReductionPerPercent?.Value ?? 0.005f;
        float rageMultiplier = 1f - (ragePercent * rageReductionPerPercent);
        grabChance *= Mathf.Max(0f, rageMultiplier);
    }

    /// <summary>
    /// Determines whether a grab should trigger for the given hit.
    /// </summary>
    /// <param name="player">Player instance.</param>
    /// <param name="kickbackkind">Knockback type (3, 4, 6 = knockdown/power attack).</param>
    /// <param name="isElite">Whether the attacker has &lt;SUPER&gt; in JPname.</param>
    internal static bool ShouldTriggerGrab(playercon player, int kickbackkind, bool isElite, EnemyDate attacker = null)
    {
        if (!(Plugin.enableGrabViaAttack?.Value ?? true))
            return false;

        if (attacker != null)
        {
            EventCoreHost consentHost = attacker.GetComponent<EventCoreHost>();
            if (consentHost != null && consentHost.IsConsentGrabWindowActive())
                return true;
        }

        if (IsPlayerDefensivelyImmuneToGrab(player))
            return false;

        if (RageSystem.IsGrabKnockdownImmuneWhileRageActive)
            return false;

        if (player.eroflag || player.erodown != 0)
            return false;

        if ((Plugin.enableVengeanceStrikeBlockGrabDuringStab?.Value ?? true) && player._stabnow)
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
            grabChance = Plugin.grabChancePowerAttack?.Value ?? 0.15f;
        }
        else
        {
            grabChance = Plugin.grabChanceMelee?.Value ?? 0.10f;
        }

        if (grabChance <= 0f)
            return false;

        ApplyGrabModifiers(player, ref grabChance);
        grabChance = Mathf.Clamp01(grabChance);
        return UnityEngine.Random.value < grabChance;
    }
}
