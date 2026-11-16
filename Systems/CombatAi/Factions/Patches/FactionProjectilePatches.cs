using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions.Patches;

/// <summary>
/// Makes enemy-fired projectiles (Arrow, fallBullet, LightMagic, SlashDamage, NomalMoveMagic)
/// able to damage OTHER enemies from hostile factions. Vanilla projectiles only target the
/// player — they pass through enemy hurtboxes. We tag each projectile with its EnemyDate
/// owner on spawn, then intercept OnTriggerEnter2D (and for SlashDamage AOE pillars, a short
/// proximity pulse) to convert EnemyCol contacts into real faction damage.
/// LightMagic: Sisterknight / CrawlingSisterKnight. SlashDamage: RequiemKnight light pillars
/// (also Touzoku / Gob / etc.). NomalMoveMagic: DemonRequiemKnight shadow bolt.
/// </summary>
internal static class FactionProjectileHelper
{
    public static void TagOwner(GameObject projectile, Vector3 pos)
    {
        TagOwner(projectile, pos, 4f);
    }

    public static void TagOwner(GameObject projectile, Vector3 pos, float maxRadius)
    {
        if (projectile == null)
            return;

        FactionProjectileOwner owner = projectile.GetComponent<FactionProjectileOwner>();
        if (owner == null)
            owner = projectile.AddComponent<FactionProjectileOwner>();
        if (owner.AttackerInstanceId != 0)
            return;

        EnemyDate nearest = FindNearestActiveAttacker(pos, Mathf.Max(0.5f, maxRadius));
        if (nearest != null)
        {
            owner.AttackerInstanceId = nearest.gameObject.GetInstanceID();
            owner.SpawnTime = Time.time;
        }
    }

    public static bool TryApplyHit(GameObject projectile, Collider2D col, float projectileDamage)
    {
        return TryApplyHit(projectile, col, projectileDamage, destroyOnHit: true);
    }

    public static bool TryApplyHit(GameObject projectile, Collider2D col, float projectileDamage, bool destroyOnHit)
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

        if (!owner.TryMarkHit(defender.gameObject.GetInstanceID()))
            return false;

        float damage = projectileDamage > 0f ? projectileDamage : (attacker.enmATK > 0f ? attacker.enmATK : 0f);
        EnemyFactionRuntime.ApplyProjectileFactionDamage(attacker, defender, damage, "projectile");
        if (destroyOnHit)
            UnityEngine.Object.Destroy(projectile);
        return false;
    }

    /// <summary>
    /// Stationary AOE (Requiem light pillars): damage hostiles near the pillar for a short window.
    /// Physics layers often never fire EnemyCol OnTriggerEnter2D for SlashDamage.
    /// </summary>
    public static void PulseAoeAround(GameObject projectile, float projectileDamage, float horizontalReach, float verticalReach)
    {
        if (projectile == null)
            return;

        FactionProjectileOwner owner = projectile.GetComponent<FactionProjectileOwner>();
        if (owner == null || owner.AttackerInstanceId == 0)
            return;

        EnemyDate attacker;
        if (!EnemyFactionRuntime.TryGetEnemyByInstanceId(owner.AttackerInstanceId, out attacker) || attacker == null)
            return;

        Vector3 pos = projectile.transform.position;
        float hx = Mathf.Max(0.4f, horizontalReach);
        float vy = Mathf.Max(0.4f, verticalReach);

        foreach (KeyValuePair<int, EnemyDate> kvp in EnemyFactionRuntime.EnumerateEnemies())
        {
            EnemyDate defender = kvp.Value;
            if (defender == null || defender == attacker || defender.gameObject == null || defender.Hp <= 0f)
                continue;
            if (!EnemyFactionRuntime.AreHostile(attacker.gameObject, defender.gameObject))
                continue;

            Vector3 dpos = defender.transform.position;
            if (Mathf.Abs(dpos.x - pos.x) > hx)
                continue;
            if (Mathf.Abs(dpos.y - pos.y) > vy)
                continue;

            if (!owner.TryMarkHit(defender.gameObject.GetInstanceID()))
                continue;

            float damage = projectileDamage > 0f ? projectileDamage : (attacker.enmATK > 0f ? attacker.enmATK : 0f);
            EnemyFactionRuntime.ApplyProjectileFactionDamage(attacker, defender, damage, "slashAoe");
        }
    }

    /// <summary>
    /// SlashDamage owner resolve: prefer known pillar casters (RequiemKnight etc.) so a nearby
    /// hostile that is also enmATKnow is not tagged as the attacker.
    /// </summary>
    public static void TagSlashDamageOwner(GameObject projectile, Vector3 pos)
    {
        if (projectile == null)
            return;

        FactionProjectileOwner owner = projectile.GetComponent<FactionProjectileOwner>();
        if (owner == null)
            owner = projectile.AddComponent<FactionProjectileOwner>();
        if (owner.AttackerInstanceId != 0)
            return;

        EnemyDate nearest = FindNearestSlashCaster(pos, 20f) ?? FindNearestActiveAttacker(pos, 20f);
        if (nearest != null)
        {
            owner.AttackerInstanceId = nearest.gameObject.GetInstanceID();
            owner.SpawnTime = Time.time;
        }
    }

    private static readonly string[] SlashCasterTypeNames =
    {
        "RequiemKnight", "DemonRequiemKnight", "TouzokuNormal", "TouzokuAxe",
        "SuccubusSpine", "GobBigAlter", "Snailshell", "TimestopAtk"
    };

    private static bool IsKnownSlashCaster(EnemyDate e)
    {
        if (e == null)
            return false;
        string typeName = e.GetType().Name;
        for (int i = 0; i < SlashCasterTypeNames.Length; i++)
        {
            if (string.Equals(typeName, SlashCasterTypeNames[i], StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static EnemyDate FindNearestSlashCaster(Vector3 pos, float maxRadius)
    {
        float bestAtkSq = maxRadius * maxRadius;
        EnemyDate bestAtk = null;
        float bestSq = maxRadius * maxRadius;
        EnemyDate best = null;

        foreach (KeyValuePair<int, EnemyDate> kvp in EnemyFactionRuntime.EnumerateEnemies())
        {
            EnemyDate e = kvp.Value;
            if (e == null || e.gameObject == null || e.Hp <= 0f || !IsKnownSlashCaster(e))
                continue;

            float dx = e.transform.position.x - pos.x;
            float dy = e.transform.position.y - pos.y;
            float sq = dx * dx + dy * dy;
            if (sq > maxRadius * maxRadius)
                continue;

            if (e.enmATKnow && sq < bestAtkSq)
            {
                bestAtkSq = sq;
                bestAtk = e;
            }
            if (sq < bestSq)
            {
                bestSq = sq;
                best = e;
            }
        }

        return bestAtk ?? best;
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

/// <summary>
/// RequiemKnight / DemonRequiemKnight light pillars (also Touzoku slash waves, etc.).
/// Tag on Atkset; short AOE pulse because EnemyCol often never triggers OnTriggerEnter2D.
/// </summary>
[HarmonyPatch(typeof(SlashDamage), "Atkset")]
internal static class FactionSlashDamageOwnerPatch
{
    [HarmonyPostfix]
    private static void Postfix(SlashDamage __instance)
    {
        if (__instance == null || __instance.gameObject == null)
            return;
        if (!EnemyFactionsConfig.Enable)
            return;

        // Pillars can spawn up to ~12 units from the knight; prefer known SlashDamage casters
        // so a nearby hostile (also enmATKnow) is not mis-tagged as the owner.
        FactionProjectileHelper.TagSlashDamageOwner(__instance.gameObject, __instance.transform.position);

        FactionSlashDamageAoEPulse pulse = __instance.GetComponent<FactionSlashDamageAoEPulse>();
        if (pulse == null)
            pulse = __instance.gameObject.AddComponent<FactionSlashDamageAoEPulse>();
        pulse.Arm(__instance);
    }
}

[HarmonyPatch(typeof(SlashDamage), "OnTriggerEnter2D")]
internal static class FactionSlashDamageHitPatch
{
    [HarmonyPrefix]
    private static bool Prefix(SlashDamage __instance, Collider2D col)
    {
        if (!EnemyFactionsConfig.Enable || __instance == null)
            return true;

        float damage = ReadSlashDamage(__instance);
        // Keep the pillar alive so multiple hostiles can be hit (AOE).
        return FactionProjectileHelper.TryApplyHit(__instance.gameObject, col, damage, destroyOnHit: false);
    }

    internal static float ReadSlashDamage(SlashDamage instance)
    {
        float damage = 0f;
        try
        {
            var field = AccessTools.Field(typeof(SlashDamage), "damage");
            if (field != null)
                damage = (float)field.GetValue(instance);
        }
        catch { }
        return damage;
    }
}

/// <summary>Short-lived proximity pulse while SlashDamage collider is active (~0.15s).</summary>
internal sealed class FactionSlashDamageAoEPulse : MonoBehaviour
{
    private float _endTime;
    private float _damage;
    private bool _armed;

    internal void Arm(SlashDamage slash)
    {
        _damage = FactionSlashDamageHitPatch.ReadSlashDamage(slash);
        // Vanilla SlashDamage enables the box briefly then disables at 0.15s — pulse a bit longer
        // so late FixedUpdates still catch enemies standing on the pillar.
        _endTime = Time.time + 0.35f;
        _armed = true;
    }

    private void FixedUpdate()
    {
        if (!_armed)
            return;
        if (Time.time > _endTime)
        {
            _armed = false;
            return;
        }

        // Pillars spawn at y-3 under the knight; undead roots sit near knight Y — keep vertical generous.
        FactionProjectileHelper.PulseAoeAround(gameObject, _damage, 1.8f, 4.5f);
    }
}

[HarmonyPatch(typeof(NomalMoveMagic), "Magicset")]
internal static class FactionNomalMoveMagicOwnerPatch
{
    [HarmonyPostfix]
    private static void Postfix(NomalMoveMagic __instance)
    {
        if (__instance == null || __instance.gameObject == null)
            return;
        if (!EnemyFactionsConfig.Enable)
            return;
        FactionProjectileHelper.TagOwner(__instance.gameObject, __instance.transform.position, 8f);
    }
}

[HarmonyPatch(typeof(NomalMoveMagic), "OnTriggerEnter2D")]
internal static class FactionNomalMoveMagicHitPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NomalMoveMagic __instance, Collider2D col)
    {
        if (!EnemyFactionsConfig.Enable || __instance == null)
            return true;

        float damage = 0f;
        try
        {
            var field = AccessTools.Field(typeof(NomalMoveMagic), "enmATK");
            if (field != null)
                damage = (float)field.GetValue(__instance);
        }
        catch { }

        return FactionProjectileHelper.TryApplyHit(__instance.gameObject, col, damage, destroyOnHit: true);
    }
}
