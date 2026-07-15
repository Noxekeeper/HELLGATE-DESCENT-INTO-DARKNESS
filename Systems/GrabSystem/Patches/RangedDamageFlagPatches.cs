using HarmonyLib;
using UnityEngine;
using NoREroMod.Systems.GrabSystem;

namespace NoREroMod.Systems.GrabSystem.Patches;

/// <summary>
/// Flags ranged damage components so GrabViaAttackPatch can skip them.
/// Each Prefix marks the context as ranged; Postfix resets it.
/// </summary>
internal static class RangedDamageFlagPatches
{
    private static bool IsPlayerDamageCollision(Collider2D col)
    {
        return col != null && col.gameObject != null && col.gameObject.CompareTag("playerDAMAGEcol");
    }

    [HarmonyPatch(typeof(ThrowObj), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void ThrowObj_Prefix(Collider2D col)
    {
        if (IsPlayerDamageCollision(col))
            GrabViaAttackContext.MarkNextDamageAsRanged();
    }

    [HarmonyPatch(typeof(ThrowObj), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void ThrowObj_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(ShotBullet), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void ShotBullet_Prefix(Collider2D col)
    {
        if (IsPlayerDamageCollision(col))
            GrabViaAttackContext.MarkNextDamageAsRanged();
    }

    [HarmonyPatch(typeof(ShotBullet), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void ShotBullet_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(Arrow), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void Arrow_Prefix(Collider2D col)
    {
        if (IsPlayerDamageCollision(col))
            GrabViaAttackContext.MarkNextDamageAsRanged();
    }

    [HarmonyPatch(typeof(Arrow), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void Arrow_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(WaterBall), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void WaterBall_Prefix(Collider2D col)
    {
        if (IsPlayerDamageCollision(col))
            GrabViaAttackContext.MarkNextDamageAsRanged();
    }

    [HarmonyPatch(typeof(WaterBall), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void WaterBall_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(Fireball), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void Fireball_Prefix(Collider2D col)
    {
        if (IsPlayerDamageCollision(col))
            GrabViaAttackContext.MarkNextDamageAsRanged();
    }

    [HarmonyPatch(typeof(Fireball), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void Fireball_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(NomalMoveMagic), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void NomalMoveMagic_Prefix(Collider2D col)
    {
        if (IsPlayerDamageCollision(col))
            GrabViaAttackContext.MarkNextDamageAsRanged();
    }

    [HarmonyPatch(typeof(NomalMoveMagic), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void NomalMoveMagic_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(HomingMissileConst), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void HomingMissileConst_Prefix(Collider2D col)
    {
        if (IsPlayerDamageCollision(col))
            GrabViaAttackContext.MarkNextDamageAsRanged();
    }

    [HarmonyPatch(typeof(HomingMissileConst), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void HomingMissileConst_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(SetupFireball), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void SetupFireball_Prefix(Collider2D col)
    {
        if (IsPlayerDamageCollision(col))
            GrabViaAttackContext.MarkNextDamageAsRanged();
    }

    [HarmonyPatch(typeof(SetupFireball), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void SetupFireball_Postfix() => GrabViaAttackContext.Reset();

    [HarmonyPatch(typeof(fallBulletdamage), "OnTriggerEnter2D")]
    [HarmonyPrefix]
    private static void FallBulletdamage_Prefix(Collider2D col)
    {
        if (IsPlayerDamageCollision(col))
            GrabViaAttackContext.MarkNextDamageAsRanged();
    }

    [HarmonyPatch(typeof(fallBulletdamage), "OnTriggerEnter2D")]
    [HarmonyPostfix]
    private static void FallBulletdamage_Postfix() => GrabViaAttackContext.Reset();
}
