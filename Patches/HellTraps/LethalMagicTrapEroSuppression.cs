using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Patches.HellTraps;

/// <summary>
/// During lethal magic trap custom death: no knockback, no erodown, enemies stay out of EROWALK.
/// </summary>
internal static class LethalMagicTrapEroSuppression
{
    private const float EnemyScanIntervalSeconds = 0.2f;

    private static bool _collisionGrabPatched;
    private static float _nextEnemyScanUnscaledTime;
    private static readonly Dictionary<Type, EnemyStateAccess> EnemyStateCache =
        new Dictionary<Type, EnemyStateAccess>();

    private sealed class EnemyStateAccess
    {
        internal FieldInfo StateField;
        internal object IdleState;
        internal object WalkState;
        internal object BlankState;
    }

    internal static bool ShouldSuppress =>
        (Plugin.enableLethalMagicTrap.Value && LethalMagicTrapDeathContext.IsEroSuppressionActive) ||
        (Plugin.enableLethalCocoonTrap.Value && LethalCocoonTrapDeathContext.IsEroSuppressionActive);

    internal static bool ShouldSuppressKnockback =>
        ShouldSuppress &&
        (LethalMagicTrapDeathContext.IsLethalDamageInFlight ||
         LethalMagicTrapDeathContext.HasPending ||
         LethalMagicTrapDeathContext.BulletHitDealtDamage ||
         LethalMagicTrapDeathContext.IsLethalTrapDamageArmed ||
         LethalMagicTrapDeathContext.IsCustomDeathActive ||
         LethalCocoonTrapDeathContext.IsLethalDamageInFlight ||
         LethalCocoonTrapDeathContext.HasPending ||
         LethalCocoonTrapDeathContext.HitDealtDamage ||
         LethalCocoonTrapDeathContext.IsCustomDeathActive);

    internal static void ApplyPatches(Harmony harmony)
    {
        if (harmony == null)
            return;

        harmony.PatchAll(typeof(LethalMagicTrapEroSuppression));
        ApplyCollisionGrabBlock(harmony);
    }

    internal static void ResetRuntimeState()
    {
        _nextEnemyScanUnscaledTime = 0f;
    }

    /// <summary>Per-frame upkeep while the death clip runner is active.</summary>
    internal static void ProcessDuringCustomDeath(playercon player)
    {
        if (!ShouldSuppress || player == null)
            return;

        PinPlayerBody(player);
        SuppressEnemyEroApproach(forceImmediate: false);
        LethalMagicTrapDeathAudio.MaintainDuringSuppression();
    }

    internal static void PinPlayerBody(playercon player)
    {
        if (player == null)
            return;

        Rigidbody2D body = player.rigi2d;
        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        if (player.erodown != 0)
            player.erodown = 0;

        if (player.nowdamage)
            player.nowdamage = false;

        NeutralizeDownAnimationState(player);
    }

    /// <summary>Vanilla Update plays act_down_s every frame while state is DOWN.</summary>
    private static void NeutralizeDownAnimationState(playercon player)
    {
        if (!ShouldSuppress || player == null)
            return;

        string state = player.state;
        if (state != "DOWN" &&
            state != "FEEL" &&
            state != "FEEL2" &&
            state != "FEEL3" &&
            state != "DAMAGE" &&
            state != "DAMAGEAIR" &&
            state != "DAMAGE3" &&
            state != "DAMAGEFALL" &&
            state != "DAMAGEWALL")
        {
            return;
        }

        player.state = "IDLE";
    }

    internal static void SuppressEnemyEroApproach(bool forceImmediate)
    {
        if (!ShouldSuppress)
            return;

        if (!forceImmediate && Time.unscaledTime < _nextEnemyScanUnscaledTime)
            return;

        _nextEnemyScanUnscaledTime = Time.unscaledTime + EnemyScanIntervalSeconds;

        EnemyDate[] enemies = Object.FindObjectsOfType<EnemyDate>();
        for (int i = 0; i < enemies.Length; i++)
            TryClearEroWalkState(enemies[i]);
    }

    private static bool TryClearEroWalkState(EnemyDate enemy)
    {
        if (enemy == null || enemy.eroflag)
            return false;

        EnemyStateAccess access = ResolveEnemyStateAccess(enemy.GetType());
        if (access == null || access.StateField == null)
            return false;

        object currentState = access.StateField.GetValue(enemy);
        if (currentState == null)
            return false;

        string stateName = currentState.ToString();
        if (stateName != "EROWALK" && stateName != "EROIDLE")
            return false;

        object fallback = access.IdleState ?? access.WalkState ?? access.BlankState;
        if (fallback == null)
            return false;

        access.StateField.SetValue(enemy, fallback);
        return true;
    }

    private static EnemyStateAccess ResolveEnemyStateAccess(Type enemyType)
    {
        if (enemyType == null)
            return null;

        if (EnemyStateCache.TryGetValue(enemyType, out EnemyStateAccess cached))
            return cached;

        FieldInfo stateField = AccessTools.Field(enemyType, "state");
        Type stateEnum = enemyType.GetNestedType("enemystate", BindingFlags.Public | BindingFlags.NonPublic);
        if (stateField == null || stateEnum == null)
            return null;

        var access = new EnemyStateAccess
        {
            StateField = stateField,
            IdleState = ParseState(stateEnum, "IDLE"),
            WalkState = ParseState(stateEnum, "WALK"),
            BlankState = ParseState(stateEnum, "BLANK"),
        };

        EnemyStateCache[enemyType] = access;
        return access;
    }

    private static object ParseState(Type stateEnum, string name)
    {
        try
        {
            return Enum.Parse(stateEnum, name);
        }
        catch
        {
            return null;
        }
    }

    internal static void ApplyCollisionGrabBlock(Harmony harmony)
    {
        if (_collisionGrabPatched || harmony == null)
            return;

        try
        {
            Type patchType = typeof(StruggleSystem).Assembly.GetType("NoREroMod.EnemyDatePatch");
            if (patchType == null)
                return;

            MethodInfo prefix = typeof(LethalMagicTrapEroSuppression).GetMethod(
                nameof(CanEliteGrabPlayer_Prefix),
                BindingFlags.Static | BindingFlags.NonPublic);
            if (prefix == null)
                return;

            int patched = 0;
            foreach (MethodInfo method in patchType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                         .Where(x => x.Name == "CanEliteGrabPlayer"))
            {
                try
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(prefix) { priority = Priority.First });
                    patched++;
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning("[LethalMagicTrap] CanEliteGrabPlayer patch failed: " + ex.Message);
                }
            }

            if (patched > 0)
            {
                _collisionGrabPatched = true;
                Plugin.Log?.LogInfo(
                    "[LethalMagicTrap] Patched "
                    + patched
                    + " CanEliteGrabPlayer overload(s) during custom trap death.");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[LethalMagicTrap] ApplyCollisionGrabBlock failed: " + ex.Message);
        }
    }

    private static bool CanEliteGrabPlayer_Prefix(ref bool __result)
    {
        if (!ShouldSuppress)
            return true;

        __result = false;
        return false;
    }

    [HarmonyPatch(typeof(playercon), "fun_nowdamage_move")]
    internal static class FunNowdamageMovePatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(playercon __instance)
        {
            if (!ShouldSuppressKnockback || __instance == null || !__instance.nowdamage)
                return;

            __instance.ToKickbackkind = 0;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(playercon __instance)
        {
            if (!ShouldSuppressKnockback || __instance == null)
                return;

            if (__instance.erodown != 0 && !__instance.eroflag)
                __instance.erodown = 0;

            PinPlayerBody(__instance);
            SuppressEnemyEroApproach(forceImmediate: true);
        }
    }

    [HarmonyPatch(typeof(playercon), nameof(playercon.fun_damage))]
    internal static class FunDamagePostfixPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(playercon __instance)
        {
            if (!ShouldSuppressKnockback || __instance == null)
                return;

            if (__instance.erodown != 0)
                __instance.erodown = 0;

            PinPlayerBody(__instance);
            SuppressEnemyEroApproach(forceImmediate: true);
        }
    }

    [HarmonyPatch(typeof(playercon), nameof(playercon.fun_damage_Improvement))]
    internal static class FunDamageImprovementPostfixPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(playercon __instance)
        {
            if (!ShouldSuppressKnockback || __instance == null)
                return;

            if (__instance.erodown != 0)
                __instance.erodown = 0;

            PinPlayerBody(__instance);
            SuppressEnemyEroApproach(forceImmediate: true);
        }
    }

    [HarmonyPatch(typeof(playercon), "Update")]
    internal static class PlayerUpdatePatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(playercon __instance)
        {
            if (!ShouldSuppress || __instance == null)
                return;

            NeutralizeDownAnimationState(__instance);
        }

        /// <summary>Fallback upkeep only while a custom death clip is playing (avoids per-frame cost).</summary>
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(playercon __instance)
        {
            if (__instance == null)
                return;

            if (!LethalMagicTrapDeathDisplay.HasActiveClip &&
                !LethalCocoonTrapDeathDisplay.HasActiveClip)
            {
                return;
            }

            LethalMagicTrapDeathAudio.MaintainDuringSuppression();
            PinPlayerBody(__instance);
            SuppressEnemyEroApproach(forceImmediate: false);
        }
    }
}
