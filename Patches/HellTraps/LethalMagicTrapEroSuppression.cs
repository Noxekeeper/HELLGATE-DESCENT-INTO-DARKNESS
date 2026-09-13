using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Spine.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Patches.HellTraps;

/// <summary>
/// During lethal trap custom death: pin the corpse, block grabs, and freeze nearby
/// combat AI (IDLE + Behaviour disable) so enemies do not keep attacking the clip.
/// </summary>
internal static class LethalMagicTrapEroSuppression
{
    private const float EnemyScanIntervalSeconds = 0.15f;

    private static bool _collisionGrabPatched;
    private static bool _enemyDamagePatched;
    private static float _nextEnemyScanUnscaledTime;
    private static FieldInfo _enmAtkNowField;
    private static FieldInfo _rigiField;

    private static readonly Dictionary<Type, EnemyStateAccess> EnemyStateCache =
        new Dictionary<Type, EnemyStateAccess>();

    private static readonly List<EnemyDate> DisabledAi = new List<EnemyDate>(32);

    private sealed class EnemyStateAccess
    {
        internal FieldInfo StateField;
        internal FieldInfo LookField;
        internal FieldInfo SpineField;
        internal object IdleState;
    }

    internal static bool ShouldSuppress =>
        (Plugin.IsLethalMagicTrapActive && LethalMagicTrapDeathContext.IsEroSuppressionActive) ||
        (Plugin.IsLethalCocoonTrapActive && LethalCocoonTrapDeathContext.IsEroSuppressionActive) ||
        (Plugin.IsLethalLightningTrapActive && LethalLightningTrapDeathContext.IsEroSuppressionActive);

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
         LethalCocoonTrapDeathContext.IsCustomDeathActive ||
         LethalLightningTrapDeathContext.IsLethalDamageInFlight ||
         LethalLightningTrapDeathContext.HasPending ||
         LethalLightningTrapDeathContext.HitDealtDamage ||
         LethalLightningTrapDeathContext.IsCustomDeathActive);

    internal static void ApplyPatches(Harmony harmony)
    {
        if (harmony == null)
            return;

        harmony.PatchAll(typeof(LethalMagicTrapEroSuppression));
        ApplyCollisionGrabBlock(harmony);
        ApplyEnemyDamageBlock(harmony);
    }

    internal static void ResetRuntimeState()
    {
        _nextEnemyScanUnscaledTime = 0f;
        RestoreCombatAi();
    }

    /// <summary>
    /// Called when any trap family clears EroSuppression — restore AI only if no
    /// other lethal-trap death session still owns the body.
    /// </summary>
    internal static void OnEroSuppressionDisabled()
    {
        if (ShouldSuppress)
            return;

        RestoreCombatAi();
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

        if (player.eroflag)
            player.eroflag = false;

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

    /// <summary>
    /// Historical name: clears ERO approach and freezes combat AI for the death clip.
    /// </summary>
    internal static void SuppressEnemyEroApproach(bool forceImmediate)
    {
        MaintainEnemyFreeze(forceImmediate);
    }

    internal static void MaintainEnemyFreeze(bool forceImmediate)
    {
        if (!ShouldSuppress)
            return;

        if (!forceImmediate && Time.unscaledTime < _nextEnemyScanUnscaledTime)
            return;

        _nextEnemyScanUnscaledTime = Time.unscaledTime + EnemyScanIntervalSeconds;

        EnemyDate[] enemies = Object.FindObjectsOfType<EnemyDate>();
        for (int i = 0; i < enemies.Length; i++)
            FreezeOrDisableEnemy(enemies[i]);
    }

    internal static void RestoreCombatAi()
    {
        for (int i = 0; i < DisabledAi.Count; i++)
        {
            EnemyDate enemy = DisabledAi[i];
            if (enemy != null)
                enemy.enabled = true;
        }

        DisabledAi.Clear();
    }

    private static void FreezeOrDisableEnemy(EnemyDate enemy)
    {
        if (enemy == null || enemy.eroflag)
            return;

        if (!enemy.enabled)
        {
            if (!DisabledAi.Contains(enemy))
                DisabledAi.Add(enemy);
            return;
        }

        EnemyStateAccess access = ResolveEnemyStateAccess(enemy.GetType());
        if (access == null || access.StateField == null)
            return;

        object currentState = access.StateField.GetValue(enemy);
        string stateName = currentState != null ? currentState.ToString() : string.Empty;

        if (string.Equals(stateName, "DEATH", StringComparison.OrdinalIgnoreCase))
            return;

        if (access.IdleState != null)
            access.StateField.SetValue(enemy, access.IdleState);

        if (access.LookField != null)
            access.LookField.SetValue(enemy, false);

        if (_enmAtkNowField == null)
            _enmAtkNowField = AccessTools.Field(typeof(EnemyDate), "enmATKnow");
        if (_enmAtkNowField != null)
            _enmAtkNowField.SetValue(enemy, false);

        if (_rigiField == null)
            _rigiField = AccessTools.Field(typeof(EnemyDate), "rigi2D");
        if (_rigiField != null)
        {
            Rigidbody2D body = _rigiField.GetValue(enemy) as Rigidbody2D;
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        TryForceIdleSpine(enemy, access);

        enemy.enabled = false;
        if (!DisabledAi.Contains(enemy))
            DisabledAi.Add(enemy);
    }

    private static void TryForceIdleSpine(EnemyDate enemy, EnemyStateAccess access)
    {
        try
        {
            SkeletonAnimation spine = null;
            if (access.SpineField != null)
                spine = access.SpineField.GetValue(enemy) as SkeletonAnimation;

            if (spine == null)
                spine = enemy.GetComponentInChildren<SkeletonAnimation>(true);

            if (spine == null || spine.state == null)
                return;

            if (string.Equals(spine.AnimationName, "IDLE", StringComparison.OrdinalIgnoreCase))
                return;

            spine.state.SetAnimation(0, "IDLE", true);
        }
        catch
        {
            // Spine set is best-effort; missing IDLE clip is acceptable.
        }
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

        FieldInfo look = AccessTools.Field(enemyType, "Look")
            ?? AccessTools.Field(typeof(EnemyDate), "Look");

        FieldInfo spine = AccessTools.Field(enemyType, "myspine")
            ?? AccessTools.Field(typeof(EnemyDate), "myspine");

        object idleState = null;
        try
        {
            idleState = Enum.Parse(stateEnum, "IDLE");
        }
        catch
        {
            idleState = null;
        }

        var access = new EnemyStateAccess
        {
            StateField = stateField,
            LookField = look,
            SpineField = spine,
            IdleState = idleState,
        };

        EnemyStateCache[enemyType] = access;
        return access;
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

    private static void ApplyEnemyDamageBlock(Harmony harmony)
    {
        if (_enemyDamagePatched || harmony == null)
            return;

        try
        {
            MethodInfo ondmg = AccessTools.Method(typeof(EnemyDate), "OndamageSend");
            if (ondmg == null)
                return;

            harmony.Patch(
                ondmg,
                prefix: new HarmonyMethod(
                    typeof(LethalMagicTrapEroSuppression),
                    nameof(EnemyOndamageSend_Prefix)));
            _enemyDamagePatched = true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[LethalMagicTrap] Enemy damage block patch failed: " + ex.Message);
        }
    }

    private static bool CanEliteGrabPlayer_Prefix(ref bool __result)
    {
        if (!ShouldSuppress)
            return true;

        __result = false;
        return false;
    }

    private static bool EnemyOndamageSend_Prefix(string tag)
    {
        if (!ShouldSuppress || tag != "playerDAMAGEcol")
            return true;

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
                !LethalCocoonTrapDeathDisplay.HasActiveClip &&
                !LethalLightningTrapDeathDisplay.HasActiveClip)
            {
                return;
            }

            LethalMagicTrapDeathAudio.MaintainDuringSuppression();
            PinPlayerBody(__instance);
            SuppressEnemyEroApproach(forceImmediate: false);
        }
    }
}
