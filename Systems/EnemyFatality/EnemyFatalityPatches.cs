using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>
/// Shared Harmony entry: arms matching enemy hits, applies HP/chance/guard/airborne gates,
/// triggers playback, blocks further hits / grabs during the session, cleans up on Death_flag.
/// </summary>
internal static class EnemyFatalityPatches
{
    private static bool _applied;
    private static bool _collisionGrabPatched;

    private static bool _hitArmed;
    private static IEnemyFatalityProfile _armedProfile;
    private static EnemyDate _armedEnemy;
    private static bool _wasGuarding;
    private static int _erodownBefore;
    private static float _hpBefore;
    private static float _maxHpBefore;

    public static void Apply(Harmony harmony)
    {
        if (_applied || harmony == null)
            return;

        if (!EnemyFatalityRegistry.AnyModuleEnabled())
        {
            EnemyFatalityConfig.LogDebug("[EnemyFatality] Patches skipped — no profiles enabled");
            return;
        }

        try
        {
            MethodInfo ondmg = AccessTools.Method(typeof(EnemyDate), "OndamageSend");
            if (ondmg != null)
            {
                harmony.Patch(
                    ondmg,
                    prefix: new HarmonyMethod(typeof(EnemyFatalityPatches), nameof(OndamageSend_Prefix)),
                    postfix: new HarmonyMethod(typeof(EnemyFatalityPatches), nameof(OndamageSend_Postfix)));
            }

            MethodInfo dmgImp = AccessTools.Method(typeof(playercon), "fun_damage_Improvement");
            if (dmgImp != null)
            {
                harmony.Patch(
                    dmgImp,
                    prefix: new HarmonyMethod(typeof(EnemyFatalityPatches), nameof(DamageImprovement_Prefix)),
                    postfix: new HarmonyMethod(typeof(EnemyFatalityPatches), nameof(Damage_Postfix)));
            }

            // Magic projectiles call fun_damage (not fun_damage_Improvement).
            MethodInfo dmg = AccessTools.Method(typeof(playercon), "fun_damage");
            if (dmg != null)
            {
                harmony.Patch(
                    dmg,
                    prefix: new HarmonyMethod(typeof(EnemyFatalityPatches), nameof(DamageFun_Prefix)),
                    postfix: new HarmonyMethod(typeof(EnemyFatalityPatches), nameof(Damage_Postfix)));
            }

            MethodInfo deathFlag = AccessTools.Method(typeof(playercon), "Death_flag");
            if (deathFlag != null)
            {
                harmony.Patch(
                    deathFlag,
                    postfix: new HarmonyMethod(typeof(EnemyFatalityPatches), nameof(DeathFlag_Postfix)));
            }

            ApplyCollisionGrabBlock(harmony);
            EnemyFatalityProjectilePatches.Apply(harmony);
            EnemyFatalityVanillaDeathMute.Apply(harmony);

            _applied = true;
            EnemyFatalityConfig.LogDebug("[EnemyFatality] Patches applied");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[EnemyFatality] Apply failed: " + ex.Message);
        }
    }

    internal static void ArmHit(IEnemyFatalityProfile profile, EnemyDate enemy)
    {
        _hitArmed = profile != null;
        _armedProfile = profile;
        _armedEnemy = enemy;
    }

    internal static void ClearHitArm()
    {
        _hitArmed = false;
        _armedProfile = null;
        _armedEnemy = null;
    }

    /// <summary>
    /// Reflective prefix on NoREroMod <c>EnemyDatePatch.CanEliteGrabPlayer</c>
    /// so collision grabs cannot start on the hidden body during a fatality clip.
    /// </summary>
    private static void ApplyCollisionGrabBlock(Harmony harmony)
    {
        if (_collisionGrabPatched || harmony == null)
            return;

        try
        {
            Type patchType = typeof(StruggleSystem).Assembly.GetType("NoREroMod.EnemyDatePatch");
            if (patchType == null)
            {
                Plugin.Log?.LogWarning("[EnemyFatality] EnemyDatePatch not found; grab block skipped");
                return;
            }

            MethodInfo prefix = typeof(EnemyFatalityPatches).GetMethod(
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
                    Plugin.Log?.LogWarning("[EnemyFatality] CanEliteGrabPlayer patch failed: " + ex.Message);
                }
            }

            if (patched > 0)
            {
                _collisionGrabPatched = true;
                EnemyFatalityConfig.LogDebug(
                    "[EnemyFatality] Patched " + patched + " CanEliteGrabPlayer overload(s)");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[EnemyFatality] ApplyCollisionGrabBlock failed: " + ex.Message);
        }
    }

    private static bool CanEliteGrabPlayer_Prefix(ref bool __result)
    {
        if (!EnemyFatalitySession.IsActive)
            return true;

        __result = false;
        return false;
    }

    private static bool OndamageSend_Prefix(EnemyDate __instance, string tag)
    {
        ClearHitArm();

        // Block further player damage from any enemy while the fatality session is active.
        if (EnemyFatalitySession.IsActive)
            return tag != "playerDAMAGEcol";

        if (tag != "playerDAMAGEcol")
            return true;

        IEnemyFatalityProfile profile = EnemyFatalityRegistry.FindEnabledMatch(__instance);
        if (profile == null)
            return true;

        ArmHit(profile, __instance);
        EnemyFatalityConfig.LogDebug("[" + profile.Id + "] Hit armed");
        return true;
    }

    private static void OndamageSend_Postfix()
    {
        ClearHitArm();
    }

    private static void DamageImprovement_Prefix(playercon __instance, bool Noguard)
    {
        CaptureDamageState(__instance, Noguard);
    }

    private static void DamageFun_Prefix(playercon __instance)
    {
        CaptureDamageState(__instance, Noguard: false);
    }

    private static void CaptureDamageState(playercon __instance, bool Noguard)
    {
        _wasGuarding = false;
        _erodownBefore = 0;
        _hpBefore = 0f;
        _maxHpBefore = 1f;

        if (!_hitArmed || __instance == null)
            return;

        _wasGuarding = __instance.guard && !Noguard;
        _erodownBefore = __instance.erodown;

        PlayerStatus status = Traverse.Create(__instance).Field("playerstatus").GetValue<PlayerStatus>();
        if (status != null)
        {
            _hpBefore = status.Hp;
            float max = status.AllMaxHP();
            _maxHpBefore = max > 0.01f ? max : 1f;
        }
    }

    private static void Damage_Postfix(playercon __instance)
    {
        if (!_hitArmed || __instance == null || _armedProfile == null)
            return;

        IEnemyFatalityProfile profile = _armedProfile;
        EnemyDate enemy = _armedEnemy;

        if (_wasGuarding && __instance.erodown == _erodownBefore)
        {
            EnemyFatalityConfig.LogDebug("[" + profile.Id + "] Guard blocked fatality");
            return;
        }

        if (EnemyFatalitySession.IsActive)
            return;

        float ratio = _hpBefore / _maxHpBefore;
        string clipRel = EnemyFatalityClipPicker.PickRelative(profile, ratio);
        if (string.IsNullOrEmpty(clipRel))
            return;

        EnemyFatalityPlayback.Trigger(__instance, enemy, profile, clipRel);
    }

    private static void DeathFlag_Postfix(playercon __instance)
    {
        if (!EnemyFatalitySession.IsActive && !EnemyFatalityClipPlayer.HasActiveClip)
            return;

        EnemyFatalityClipPlayer.ForceCleanupForRespawn(__instance);
        EnemyFatalityConfig.LogDebug("[EnemyFatality] Death_flag cleanup");
    }
}
