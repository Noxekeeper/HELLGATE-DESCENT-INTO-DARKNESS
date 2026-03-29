using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using NoREroMod;
using NoREroMod.Systems.GrabSystem;
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
            if (attacker == null) return true;
            if (___playerstatus == null) { GrabViaAttackContext.Reset(); return true; }

            // Hard guard: if player already entered H/grab state, do not run another grab path.
            if (__instance.eroflag || __instance.erodown != 0)
            {
                GrabViaAttackContext.Reset();
                return true;
            }

            var jpName = Traverse.Create(attacker).Field("JPname").GetValue() as string ?? "";
            bool isElite = jpName.Contains("<SUPER>");

            if (!GrabChanceCalculator.ShouldTriggerGrab(__instance, kickbackkind, isElite))
            {
                GrabViaAttackContext.Reset();
                return true;
            }

            InvokeEliteGrabPlayer(attacker, ___playerstatus);
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
            if (attacker == null) return true;
            if (___playerstatus == null) { GrabViaAttackContext.Reset(); return true; }

            // Hard guard: if player already entered H/grab state, do not run another grab path.
            if (__instance.eroflag || __instance.erodown != 0)
            {
                GrabViaAttackContext.Reset();
                return true;
            }

            var jpName = Traverse.Create(attacker).Field("JPname").GetValue() as string ?? "";
            bool isElite = jpName.Contains("<SUPER>");

            if (!GrabChanceCalculator.ShouldTriggerGrab(__instance, kickbackkind, isElite))
            {
                GrabViaAttackContext.Reset();
                return true;
            }

            InvokeEliteGrabPlayer(attacker, ___playerstatus);
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
