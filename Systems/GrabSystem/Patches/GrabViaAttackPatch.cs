using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using NoREroMod;
using NoREroMod.Patches.Enemy.BossTouzokuCustom;
using NoREroMod.Systems.GrabSystem;
using NoREroMod.Systems.Pregnancy.Patches;
using NoREroMod.Systems.Rage;
using UnityEngine;

namespace NoREroMod.Systems.GrabSystem.Patches;

/// <summary>
/// Intercepts fun_damage / fun_damage_Improvement via Harmony Prefix.
/// When grab triggers, calls EliteGrabPlayer, transitions the enemy to EROWALK,
/// fires a white flash, and optionally applies slow-motion — skipping the original
/// damage method so no knockdown or HP loss occurs.
/// </summary>
internal static class GrabViaAttackPatch
{
    private static MethodInfo _eliteGrabPlayerMethod;
    private static bool IsBiscodAttacker(EnemyDate attacker)
    {
        if (attacker == null || attacker.gameObject == null) return false;
        if (attacker.gameObject.GetComponent("BiscodMarker") != null) return true;
        string name = attacker.gameObject.name;
        return !string.IsNullOrEmpty(name) &&
               name.IndexOf("biscord", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Goblins need vanilla knockdown → state DOWN → OnTriggerStay → GoblineroStart.
    /// GrabViaAttack (ImmediatelyERO + EROWALK + flash/teleport) skips that intro: combat body
    /// vanishes (eroflag without eroflag2 / no lying pose) until goblinero START begins.
    /// </summary>
    private static bool IsGoblinAttacker(EnemyDate attacker)
    {
        return attacker is goblin;
    }

    /// <summary>Enemies that must keep their vanilla grab-intro pipeline.</summary>
    private static bool ShouldUseVanillaGrabOnly(EnemyDate attacker)
    {
        return IsBiscodAttacker(attacker) || IsGoblinAttacker(attacker);
    }

    private static void EnsureEliteGrabPlayerCached()
    {
        if (_eliteGrabPlayerMethod != null) return;
        var asm = typeof(StruggleSystem).Assembly;
        var type = asm.GetType("NoREroMod.EnemyDatePatch");
        if (type == null) return;
        _eliteGrabPlayerMethod = type.GetMethod("EliteGrabPlayer",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(EnemyDate), typeof(PlayerStatus) },
            null);
    }

    private static void InvokeEliteGrabPlayer(EnemyDate enemy, PlayerStatus pStatus)
    {
        EnsureEliteGrabPlayerCached();
        try
        {
            _eliteGrabPlayerMethod?.Invoke(null, new object[] { enemy, pStatus });
            SetEnemyStateToEROWALK(enemy);
            TriggerGrabFlash();
            StartGrabSlowmoIfEnabled(enemy);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[GrabViaAttack] EliteGrabPlayer failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Broker consent handoff: grab via attack pipeline without knockdown fall or HP loss.
    /// </summary>
    internal static bool TryForceConsentGrab(EnemyDate attacker, PlayerStatus playerStatus)
    {
        if (attacker == null || playerStatus == null)
            return false;

        if (WitchOffspringCombatRules.ShouldBlockOffspringVsPlayer(attacker))
            return false;

        playercon player = attacker.com_player ?? UnityEngine.Object.FindObjectOfType<playercon>();
        if (player == null || player.eroflag || player.erodown != 0)
            return false;

        if (RageSystem.IsGrabKnockdownImmuneWhileRageActive)
            return false;

        if ((Plugin.enableVengeanceStrikeBlockGrabDuringStab?.Value ?? true) && player._stabnow)
            return false;

        if (StruggleSystem.isGrabInvul())
            return false;

        InvokeEliteGrabPlayer(attacker, playerStatus);
        if (attacker is BossTouzoku customBoss)
            BossTouzokuCustomRuntime.OnGrabViaAttackHit(customBoss);
        NoREroMod.Systems.Economy.CombatGoldLossRuntime.TryProcessPlayerHit(player, wasDodged: false);
        return true;
    }

    /// <summary>
    /// Forces the attacker into EROWALK (or EROIDLE for bosses) via reflection,
    /// mirroring what NoREroMod does on collision-based grab.
    /// </summary>
    private static void SetEnemyStateToEROWALK(EnemyDate enemy)
    {
        if (enemy == null) return;
        try
        {
            var t = enemy.GetType();
            var stateField = Traverse.Create(enemy).Field("state");
            var enums = t.GetNestedType("enemystate", BindingFlags.Public | BindingFlags.NonPublic);
            if (enums == null) return;
            object eroValue = null;
            try { eroValue = Enum.Parse(enums, "EROWALK"); } catch { }
            if (eroValue == null) try { eroValue = Enum.Parse(enums, "EROIDLE"); } catch { }
            if (eroValue != null)
                stateField.SetValue(eroValue);
        }
        catch { }
    }

    /// <summary>Triggers a white screen flash via NoREroMod's UImngPatch.WhiteFadeIn (orgasm flash).</summary>
    private static void TriggerGrabFlash()
    {
        try
        {
            var uimngType = typeof(StruggleSystem).Assembly.GetType("NoREroMod.UImngPatch");
            var whiteFadeIn = uimngType?.GetMethod("WhiteFadeIn", BindingFlags.Public | BindingFlags.Static);
            whiteFadeIn?.Invoke(null, null);
        }
        catch { }
    }

    private static void StartGrabSlowmoIfEnabled(EnemyDate attacker)
    {
        // HSceneStartZoomEffect applies StartZoom.Slowmo* when eroflag starts (all grab sources).
        if (Plugin.enableStartZoomEffect?.Value ?? true)
            return;
        if (attacker == null || !(Plugin.grabViaAttackSlowmo?.Value ?? false)) return;
        float scale = Plugin.grabViaAttackSlowmoTimeScale?.Value ?? 0.3f;
        float dur = Plugin.grabViaAttackSlowmoDuration?.Value ?? 2f;
        if (dur <= 0f) return;
        attacker.StartCoroutine(GrabSlowmoCoroutine(scale, dur));
    }

    /// <summary>
    /// Maintains Time.timeScale every frame for the configured duration.
    /// Other systems (H-scene, pause) may overwrite timeScale; this re-applies it
    /// each frame, similar to TimeSlowMoSystem. Respects pause (timeScale == 0).
    /// </summary>
    private static IEnumerator GrabSlowmoCoroutine(float targetScale, float realSeconds)
    {
        float endTime = Time.realtimeSinceStartup + realSeconds;
        while (Time.realtimeSinceStartup < endTime)
        {
            if (Time.timeScale != targetScale && Time.timeScale != 0f)
                Time.timeScale = targetScale;
            yield return null;
        }
        Time.timeScale = 1f;
    }

    [HarmonyPatch(typeof(playercon), "fun_damage")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool FunDamage_Prefix(playercon __instance, PlayerStatus ___playerstatus, float getatk, int kickbackkind)
    {
        try
        {
            var attacker = GrabViaAttackContext.CurrentAttacker;
            if (attacker != null && WitchOffspringCombatRules.ShouldBlockOffspringVsPlayer(attacker))
            {
                GrabViaAttackContext.Reset();
                return false;
            }

            if (attacker == null) return true;
            if (ShouldUseVanillaGrabOnly(attacker))
            {
                GrabViaAttackContext.Reset();
                return true;
            }
            // Arena BossTouzoku uses vanilla downed grab only; field custom bridges via OnGrabViaAttackHit.
            if (attacker is BossTouzoku arenaBoss && !BossTouzokuCustomStats.IsCustom(arenaBoss))
            {
                GrabViaAttackContext.Reset();
                return true;
            }
            if (___playerstatus == null) { GrabViaAttackContext.Reset(); return true; }

            // Hard guard: if player already entered H/grab state, do not run another grab path.
            if (__instance.eroflag || __instance.erodown != 0)
            {
                GrabViaAttackContext.Reset();
                return true;
            }

            if ((Plugin.enableVengeanceStrikeBlockGrabDuringStab?.Value ?? true) && __instance._stabnow)
            {
                GrabViaAttackContext.Reset();
                return true;
            }

            if (GrabChanceCalculator.IsPlayerDefensivelyImmuneToGrab(__instance))
            {
                GrabViaAttackContext.Reset();
                return true;
            }

            if (RageSystem.IsGrabKnockdownImmuneWhileRageActive)
            {
                GrabViaAttackContext.Reset();
                return true;
            }

            var jpName = Traverse.Create(attacker).Field("JPname").GetValue() as string ?? "";
            bool isElite = jpName.Contains("<SUPER>");

            if (!GrabChanceCalculator.ShouldTriggerGrab(__instance, kickbackkind, isElite, attacker))
            {
                GrabViaAttackContext.Reset();
                return true;
            }

            InvokeEliteGrabPlayer(attacker, ___playerstatus);
            if (attacker is BossTouzoku customBoss)
                BossTouzokuCustomRuntime.OnGrabViaAttackHit(customBoss);
            NoREroMod.Systems.Economy.CombatGoldLossRuntime.TryProcessPlayerHit(__instance, wasDodged: false);
            GrabViaAttackContext.Reset();
            return false;
        }
        catch
        {
            GrabViaAttackContext.Reset();
            return true;
        }
    }

    [HarmonyPatch(typeof(playercon), "fun_damage_Improvement")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool FunDamageImprovement_Prefix(playercon __instance, PlayerStatus ___playerstatus, float getatk, int kickbackkind)
    {
        try
        {
            var attacker = GrabViaAttackContext.CurrentAttacker;
            if (attacker != null && WitchOffspringCombatRules.ShouldBlockOffspringVsPlayer(attacker))
            {
                GrabViaAttackContext.Reset();
                return false;
            }

            if (attacker == null) return true;
            if (ShouldUseVanillaGrabOnly(attacker))
            {
                GrabViaAttackContext.Reset();
                return true;
            }
            // Arena BossTouzoku uses vanilla downed grab only; field custom bridges via OnGrabViaAttackHit.
            if (attacker is BossTouzoku arenaBoss && !BossTouzokuCustomStats.IsCustom(arenaBoss))
            {
                GrabViaAttackContext.Reset();
                return true;
            }
            if (___playerstatus == null) { GrabViaAttackContext.Reset(); return true; }

            // Hard guard: if player already entered H/grab state, do not run another grab path.
            if (__instance.eroflag || __instance.erodown != 0)
            {
                GrabViaAttackContext.Reset();
                return true;
            }

            if ((Plugin.enableVengeanceStrikeBlockGrabDuringStab?.Value ?? true) && __instance._stabnow)
            {
                GrabViaAttackContext.Reset();
                return true;
            }

            if (GrabChanceCalculator.IsPlayerDefensivelyImmuneToGrab(__instance))
            {
                GrabViaAttackContext.Reset();
                return true;
            }

            if (RageSystem.IsGrabKnockdownImmuneWhileRageActive)
            {
                GrabViaAttackContext.Reset();
                return true;
            }

            var jpName = Traverse.Create(attacker).Field("JPname").GetValue() as string ?? "";
            bool isElite = jpName.Contains("<SUPER>");

            if (!GrabChanceCalculator.ShouldTriggerGrab(__instance, kickbackkind, isElite, attacker))
            {
                GrabViaAttackContext.Reset();
                return true;
            }

            InvokeEliteGrabPlayer(attacker, ___playerstatus);
            if (attacker is BossTouzoku customBoss)
                BossTouzokuCustomRuntime.OnGrabViaAttackHit(customBoss);
            NoREroMod.Systems.Economy.CombatGoldLossRuntime.TryProcessPlayerHit(__instance, wasDodged: false);
            GrabViaAttackContext.Reset();
            return false;
        }
        catch
        {
            GrabViaAttackContext.Reset();
            return true;
        }
    }
}
