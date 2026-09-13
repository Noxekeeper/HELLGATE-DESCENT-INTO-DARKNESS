using System;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>
/// Tags magic projectiles with their owner at spawn, and arms HeavyCritical
/// profiles on <c>playerDAMAGEcol</c> hits (whitelist projectile types only).
/// </summary>
internal static class EnemyFatalityProjectilePatches
{
    private const float OwnerSearchRadius = 28f;

    private static bool _applied;

    internal static void Apply(Harmony harmony)
    {
        if (_applied || harmony == null)
            return;

        try
        {
            PatchSpawnTag(harmony, typeof(LightMagic), "Magicset", nameof(LightMagic_Magicset_Postfix));
            PatchSpawnTag(harmony, typeof(Fireball), "SetSlantingball_Constant", nameof(Fireball_SetSlanting_Postfix));
            PatchSpawnTag(harmony, typeof(WaterBall), "SetTrapball_Constant", nameof(WaterBall_SetTrap_Postfix));
            PatchSpawnTag(harmony, typeof(BoundMoveMagic), "Magicset", nameof(BoundMoveMagic_Magicset_Postfix));
            PatchSpawnTag(harmony, typeof(TargetRotationEnemy), "SET", nameof(TargetRotationEnemy_SET_Postfix));
            PatchSpawnTag(harmony, typeof(HomingMissileConst), "STARTSet", nameof(HomingMissile_STARTSet_Postfix));

            PatchHit(harmony, typeof(LightMagic));
            PatchHit(harmony, typeof(Fireball));
            PatchHit(harmony, typeof(WaterBall));
            PatchHit(harmony, typeof(BoundMoveMagic));
            PatchHit(harmony, typeof(TargetRotationEnemy));
            PatchHit(harmony, typeof(HomingMissileConst));

            _applied = true;
            EnemyFatalityConfig.LogDebug("[EnemyFatality] Projectile patches applied");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[EnemyFatality] Projectile patches failed: " + ex.Message);
        }
    }

    private static void PatchSpawnTag(Harmony harmony, Type type, string methodName, string postfixName)
    {
        var method = AccessTools.Method(type, methodName);
        var postfix = AccessTools.Method(typeof(EnemyFatalityProjectilePatches), postfixName);
        if (method == null || postfix == null)
        {
            Plugin.Log?.LogWarning("[EnemyFatality] Missing spawn patch target: " + type.Name + "." + methodName);
            return;
        }

        harmony.Patch(method, postfix: new HarmonyMethod(postfix));
    }

    private static void PatchHit(Harmony harmony, Type type)
    {
        var method = AccessTools.Method(type, "OnTriggerEnter2D", new[] { typeof(Collider2D) });
        if (method == null)
        {
            Plugin.Log?.LogWarning("[EnemyFatality] Missing OnTriggerEnter2D: " + type.Name);
            return;
        }

        harmony.Patch(
            method,
            prefix: new HarmonyMethod(typeof(EnemyFatalityProjectilePatches), nameof(Projectile_OnTrigger_Prefix)),
            postfix: new HarmonyMethod(typeof(EnemyFatalityProjectilePatches), nameof(Projectile_OnTrigger_Postfix)));
    }

    // --- Spawn tagging ---

    private static void LightMagic_Magicset_Postfix(LightMagic __instance)
    {
        TagNearest(__instance, "LightMagic", typeof(Sisterknight), typeof(CrawlingSisterKnight));
    }

    private static void Fireball_SetSlanting_Postfix(Fireball __instance)
    {
        TagNearest(__instance, "Fireball", typeof(InquisitionRED));
    }

    private static void WaterBall_SetTrap_Postfix(WaterBall __instance)
    {
        TagNearest(__instance, "WaterBall", typeof(Snailshell), typeof(SkeltonOoze));
    }

    private static void BoundMoveMagic_Magicset_Postfix(BoundMoveMagic __instance)
    {
        TagNearest(__instance, "BoundMoveMagic", typeof(SkeltonOoze));
    }

    private static void TargetRotationEnemy_SET_Postfix(TargetRotationEnemy __instance)
    {
        // Orbs are often parented under the ooze; prefer parent, else nearest.
        if (__instance == null)
            return;

        EnemyDate parent = __instance.GetComponentInParent<EnemyDate>();
        if (parent is SkeltonOoze)
        {
            EnsureTag(__instance.gameObject, parent, "TargetRotationEnemy");
            return;
        }

        TagNearest(__instance, "TargetRotationEnemy", typeof(SkeltonOoze));
    }

    private static void HomingMissile_STARTSet_Postfix(HomingMissileConst __instance)
    {
        TagNearest(__instance, "HomingMissileConst", typeof(Pilgrim));
    }

    private static void TagNearest(Component projectile, string kind, params Type[] ownerTypes)
    {
        if (projectile == null)
            return;

        EnemyDate owner = FindNearestOwner(projectile.transform.position, ownerTypes);
        if (owner == null)
        {
            EnemyFatalityConfig.LogDebug(
                "[EnemyFatality] No owner near " + kind + " at " + projectile.transform.position);
            return;
        }

        EnsureTag(projectile.gameObject, owner, kind);
    }

    private static void EnsureTag(GameObject go, EnemyDate owner, string kind)
    {
        if (go == null || owner == null)
            return;

        var tag = go.GetComponent<EnemyFatalityProjectileOwner>();
        if (tag == null)
            tag = go.AddComponent<EnemyFatalityProjectileOwner>();

        tag.Owner = owner;
        tag.ProjectileKind = kind;
        EnemyFatalityConfig.LogDebug(
            "[EnemyFatality] Tagged " + kind + " ← " + owner.GetType().Name);
    }

    private static EnemyDate FindNearestOwner(Vector3 worldPos, Type[] ownerTypes)
    {
        if (ownerTypes == null || ownerTypes.Length == 0)
            return null;

        float maxSq = OwnerSearchRadius * OwnerSearchRadius;
        float bestSq = maxSq;
        EnemyDate best = null;

        EnemyDate[] all = UnityEngine.Object.FindObjectsOfType<EnemyDate>();
        for (int i = 0; i < all.Length; i++)
        {
            EnemyDate enemy = all[i];
            if (enemy == null || !enemy.isActiveAndEnabled)
                continue;

            Type t = enemy.GetType();
            bool allowed = false;
            for (int j = 0; j < ownerTypes.Length; j++)
            {
                if (ownerTypes[j].IsAssignableFrom(t))
                {
                    allowed = true;
                    break;
                }
            }

            if (!allowed)
                continue;

            float sq = (enemy.transform.position - worldPos).sqrMagnitude;
            if (sq <= bestSq)
            {
                bestSq = sq;
                best = enemy;
            }
        }

        return best;
    }

    // --- Hit arming ---

    private static bool Projectile_OnTrigger_Prefix(MonoBehaviour __instance, Collider2D col)
    {
        if (col == null || col.gameObject == null || col.gameObject.tag != "playerDAMAGEcol")
            return true;

        if (EnemyFatalitySession.IsActive)
            return false;

        if (__instance == null)
            return true;

        if (!IsWhitelistedProjectile(__instance))
            return true;

        var tag = __instance.GetComponent<EnemyFatalityProjectileOwner>();
        EnemyDate owner = tag != null ? tag.Owner : null;
        if (owner == null)
            owner = ResolveOwnerFallback(__instance);

        if (owner == null)
            return true;

        IEnemyFatalityProfile profile = EnemyFatalityRegistry.FindEnabledMagicProjectileMatch(owner);
        if (profile == null)
            return true;

        // While GG is down / standing up, prefer approach-to-H over magic execute.
        // Fatality bone is also unreliable in prone poses — skip the kill entirely.
        if (IsPlayerKnockdownRecovering(col))
        {
            EnemyFatalityConfig.LogDebug(
                "[" + profile.Id + "] Magic hit ignored — knockdown recovering (prefer H)");
            try
            {
                UnityEngine.Object.Destroy(__instance.gameObject);
            }
            catch
            {
            }

            return false;
        }

        EnemyFatalityPatches.ArmHit(profile, owner);
        EnemyFatalityConfig.LogDebug(
            "[" + profile.Id + "] Magic projectile armed ("
            + (tag != null ? tag.ProjectileKind : __instance.GetType().Name) + ")");
        return true;
    }

    private static bool IsPlayerKnockdownRecovering(Collider2D col)
    {
        if (col == null)
            return false;

        playercon pc = col.GetComponentInParent<playercon>();
        if (pc == null)
            return false;

        // Already in H — do not interfere.
        if (pc.eroflag)
            return false;

        return pc.erodown != 0;
    }

    private static void Projectile_OnTrigger_Postfix()
    {
        EnemyFatalityPatches.ClearHitArm();
    }

    private static bool IsWhitelistedProjectile(MonoBehaviour projectile)
    {
        return projectile is LightMagic
            || projectile is Fireball
            || projectile is WaterBall
            || projectile is BoundMoveMagic
            || projectile is TargetRotationEnemy
            || projectile is HomingMissileConst;
    }

    private static EnemyDate ResolveOwnerFallback(MonoBehaviour projectile)
    {
        if (projectile is LightMagic)
            return FindNearestOwner(projectile.transform.position, new[] { typeof(Sisterknight), typeof(CrawlingSisterKnight) });
        if (projectile is Fireball)
            return FindNearestOwner(projectile.transform.position, new[] { typeof(InquisitionRED) });
        if (projectile is WaterBall)
            return FindNearestOwner(projectile.transform.position, new[] { typeof(Snailshell), typeof(SkeltonOoze) });
        if (projectile is BoundMoveMagic || projectile is TargetRotationEnemy)
            return FindNearestOwner(projectile.transform.position, new[] { typeof(SkeltonOoze) });
        if (projectile is HomingMissileConst)
            return FindNearestOwner(projectile.transform.position, new[] { typeof(Pilgrim) });
        return null;
    }
}
