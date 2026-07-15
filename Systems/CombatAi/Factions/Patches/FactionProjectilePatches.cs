using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions.Patches;

/// <summary>
/// Makes enemy-fired projectiles (Arrow, fallBullet, LightMagic) able to damage OTHER
/// enemies from hostile factions. Vanilla projectiles only target the player — they pass
/// through enemy hurtboxes. We tag each projectile with its EnemyDate owner on
/// spawn, then intercept its OnTriggerEnter2D to convert EnemyCol contacts with
/// hostile factions into real damage with proper pop-up numbers.
/// LightMagic is the Sisterknight / CrawlingSisterKnight magic bolt.
/// </summary>
internal static class FactionProjectileHelper
{
    public static void TagOwner(GameObject projectile, Vector3 pos)
    {
        if (projectile == null)
            return;

        FactionProjectileOwner owner = projectile.GetComponent<FactionProjectileOwner>();
        if (owner == null)
            owner = projectile.AddComponent<FactionProjectileOwner>();
        if (owner.AttackerInstanceId != 0)
            return;

        EnemyDate nearest = FindNearestActiveAttacker(pos, 4f);
        if (nearest != null)
        {
            owner.AttackerInstanceId = nearest.gameObject.GetInstanceID();
            owner.SpawnTime = Time.time;
        }
    }

    public static bool TryApplyHit(GameObject projectile, Collider2D col, float projectileDamage)
    {
        if (projectile == null || col == null || col.gameObject == null)
            return true;

        if (col.gameObject.tag != "EnemyCol")
            return true; // Only intercept collisions with enemy hurtboxes.

        FactionProjectileOwner owner = projectile.GetComponent<FactionProjectileOwner>();
        if (owner == null || owner.AttackerInstanceId == 0)
            return true;

        EnemyDate attacker;
        if (!EnemyFactionRuntime.TryGetEnemyByInstanceId(owner.AttackerInstanceId, out attacker) || attacker == null)
            return true;

        EnemyDate defender = col.GetComponentInParent<EnemyDate>();
        if (defender == null && col.transform != null && col.transform.parent != null)
            defender = col.transform.parent.GetComponent<EnemyDate>();
        if (defender == null || defender == attacker)
            return true;

        if (!EnemyFactionRuntime.AreHostile(attacker.gameObject, defender.gameObject))
            return true;

        float damage = projectileDamage > 0f ? projectileDamage : (attacker.enmATK > 0f ? attacker.enmATK : 0f);
        EnemyFactionRuntime.ApplyProjectileFactionDamage(attacker, defender, damage, "projectile");
        UnityEngine.Object.Destroy(projectile);
        return false;
    }

    private static EnemyDate FindNearestActiveAttacker(Vector3 pos, float maxRadius)
    {
        float bestSq = maxRadius * maxRadius;
        EnemyDate best = null;
        EnemyDate bestFallback = null;
        float bestFallbackSq = maxRadius * maxRadius;

        foreach (KeyValuePair<int, EnemyDate> kvp in EnemyFactionRuntime.EnumerateEnemies())
        {
            EnemyDate e = kvp.Value;
            if (e == null || e.gameObject == null || e.Hp <= 0f)
                continue;

            float dx = e.transform.position.x - pos.x;
            float dy = e.transform.position.y - pos.y;
            float sq = dx * dx + dy * dy;

            if (e.enmATKnow && sq < bestSq)
            {
                bestSq = sq;
                best = e;
            }
            else if (sq < bestFallbackSq)
            {
                bestFallbackSq = sq;
                bestFallback = e;
            }
        }

        return best ?? bestFallback;
    }
}

[HarmonyPatch(typeof(Arrow), "ArrowAttack")]
internal static class FactionArrowOwnerPatch
{
    [HarmonyPostfix]
    private static void Postfix(Arrow __instance)
    {
        if (__instance == null || __instance.gameObject == null)
            return;
        if (!EnemyFactionsConfig.Enable)
            return;
        FactionProjectileHelper.TagOwner(__instance.gameObject, __instance.transform.position);
    }
}

[HarmonyPatch(typeof(fallBullet), "AttackSet")]
internal static class FactionFallBulletOwnerPatch
{
    [HarmonyPostfix]
    private static void Postfix(fallBullet __instance)
    {
        if (__instance == null || __instance.gameObject == null)
            return;
        if (!EnemyFactionsConfig.Enable)
            return;
        FactionProjectileHelper.TagOwner(__instance.gameObject, __instance.transform.position);
    }
}

[HarmonyPatch(typeof(Arrow), "OnTriggerEnter2D")]
internal static class FactionArrowHitPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Arrow __instance, Collider2D col)
    {
        if (!EnemyFactionsConfig.Enable || __instance == null)
            return true;

        float damage = 0f;
        try
        {
            var field = AccessTools.Field(typeof(Arrow), "enmATK");
            if (field != null)
                damage = (float)field.GetValue(__instance);
        }
        catch { }

        return FactionProjectileHelper.TryApplyHit(__instance.gameObject, col, damage);
    }
}

[HarmonyPatch(typeof(fallBullet), "OnTriggerEnter2D")]
internal static class FactionFallBulletHitPatch
{
    [HarmonyPrefix]
    private static bool Prefix(fallBullet __instance, Collider2D col)
    {
        if (!EnemyFactionsConfig.Enable || __instance == null)
            return true;

        float damage = 0f;
        try
        {
            var field = AccessTools.Field(typeof(fallBullet), "enmATK");
            if (field != null)
                damage = (float)field.GetValue(__instance);
        }
        catch { }

        return FactionProjectileHelper.TryApplyHit(__instance.gameObject, col, damage);
    }
}

[HarmonyPatch(typeof(LightMagic), "Magicset")]
internal static class FactionLightMagicOwnerPatch
{
    [HarmonyPostfix]
    private static void Postfix(LightMagic __instance)
    {
        if (__instance == null || __instance.gameObject == null)
            return;
        if (!EnemyFactionsConfig.Enable)
            return;
        FactionProjectileHelper.TagOwner(__instance.gameObject, __instance.transform.position);
    }
}

[HarmonyPatch(typeof(LightMagic), "OnTriggerEnter2D")]
internal static class FactionLightMagicHitPatch
{
    [HarmonyPrefix]
    private static bool Prefix(LightMagic __instance, Collider2D col)
    {
        if (!EnemyFactionsConfig.Enable || __instance == null)
            return true;

        float damage = 0f;
        try
        {
            var field = AccessTools.Field(typeof(LightMagic), "enmATK");
            if (field != null)
                damage = (float)field.GetValue(__instance);
        }
        catch { }

        return FactionProjectileHelper.TryApplyHit(__instance.gameObject, col, damage);
    }
}
