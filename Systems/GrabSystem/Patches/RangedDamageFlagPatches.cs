using HarmonyLib;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.GrabSystem;
using UnityEngine;

namespace NoREroMod.Systems.GrabSystem.Patches;

/// <summary>
/// Flags ranged damage components so GrabViaAttackPatch can skip them, and attributes
/// the projectile owner for Fear (per-enemy projectileFear).
/// </summary>
internal static class RangedDamageFlagPatches
{
    private static bool IsPlayerDamageCollision(Collider2D col)
    {
        return col != null && col.gameObject != null && col.gameObject.CompareTag("playerDAMAGEcol");
    }

    private static void MarkRanged(Collider2D col, Component projectile)
    {
        if (!IsPlayerDamageCollision(col))
            return;
        EnemyDate owner = ResolveProjectileOwner(projectile);
        GrabViaAttackContext.MarkNextDamageAsRanged(owner);
    }

    private static EnemyDate ResolveProjectileOwner(Component projectile)
    {
        if (projectile == null || projectile.gameObject == null)
            return null;

        FactionProjectileOwner tagged = projectile.GetComponent<FactionProjectileOwner>();
        if (tagged != null && tagged.AttackerInstanceId != 0 &&
            EnemyFactionRuntime.TryGetEnemyByInstanceId(tagged.AttackerInstanceId, out EnemyDate taggedOwner) &&
            taggedOwner != null)
        {
            return taggedOwner;
        }

        return FindNearestEnemy(projectile.transform.position, 14f);
    }

    private static EnemyDate FindNearestEnemy(Vector3 pos, float maxRadius)
    {
        float bestSq = maxRadius * maxRadius;
        EnemyDate best = null;
        try
        {
            foreach (var kvp in EnemyFactionRuntime.EnumerateEnemies())
            {
                EnemyDate e = kvp.Value;
                if (e == null || e.gameObject == null || e.Hp <= 0f)
                    continue;
                float dx = e.transform.position.x - pos.x;
                float dy = e.transform.position.y - pos.y;
                float sq = dx * dx + dy * dy;
                if (sq >= bestSq)
                    continue;
                bestSq = sq;
                best = e;
            }
        }
        catch { }
        return best;
    }

    [HarmonyPatch(typeof(ThrowObj), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void ThrowObj_Prefix(ThrowObj __instance, Collider2D col) => MarkRanged(col, __instance);

    [HarmonyPatch(typeof(ThrowObj), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void ThrowObj_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(ShotBullet), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void ShotBullet_Prefix(ShotBullet __instance, Collider2D col) => MarkRanged(col, __instance);

    [HarmonyPatch(typeof(ShotBullet), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void ShotBullet_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(Arrow), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void Arrow_Prefix(Arrow __instance, Collider2D col) => MarkRanged(col, __instance);

    [HarmonyPatch(typeof(Arrow), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void Arrow_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(WaterBall), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void WaterBall_Prefix(WaterBall __instance, Collider2D col) => MarkRanged(col, __instance);

    [HarmonyPatch(typeof(WaterBall), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void WaterBall_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(Fireball), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void Fireball_Prefix(Fireball __instance, Collider2D col) => MarkRanged(col, __instance);

    [HarmonyPatch(typeof(Fireball), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void Fireball_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(NomalMoveMagic), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void NomalMoveMagic_Prefix(NomalMoveMagic __instance, Collider2D col) => MarkRanged(col, __instance);

    [HarmonyPatch(typeof(NomalMoveMagic), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void NomalMoveMagic_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(HomingMissileConst), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void HomingMissileConst_Prefix(HomingMissileConst __instance, Collider2D col) => MarkRanged(col, __instance);

    [HarmonyPatch(typeof(HomingMissileConst), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void HomingMissileConst_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(SetupFireball), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void SetupFireball_Prefix(SetupFireball __instance, Collider2D col) => MarkRanged(col, __instance);

    [HarmonyPatch(typeof(SetupFireball), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void SetupFireball_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(fallBulletdamage), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void FallBulletdamage_Prefix(fallBulletdamage __instance, Collider2D col) => MarkRanged(col, __instance);

    [HarmonyPatch(typeof(fallBulletdamage), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void FallBulletdamage_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(fallBullet), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void FallBullet_Prefix(fallBullet __instance, Collider2D col) => MarkRanged(col, __instance);

    [HarmonyPatch(typeof(fallBullet), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void FallBullet_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(LightMagic), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void LightMagic_Prefix(LightMagic __instance, Collider2D col) => MarkRanged(col, __instance);

    [HarmonyPatch(typeof(LightMagic), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void LightMagic_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(SlashDamage), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void SlashDamage_Prefix(SlashDamage __instance, Collider2D col) => MarkRanged(col, __instance);

    [HarmonyPatch(typeof(SlashDamage), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void SlashDamage_Postfix() => GrabViaAttackContext.Reset();
}
