using System.Collections;
using System.Reflection;
using HarmonyLib;
using NoREroMod.Patches.Enemy.DemonTentacleModCustom;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Patches;

/// <summary>
/// DemonTentacle combat AI on top of vanilla AngelStatue: faster attack timer, projectile bursts,
/// wall blocking (vanilla flies through level geometry) and enrage at low HP.
/// Config: HellGateJson/CombatAi/CombatAi.json (DemonTentacle*). No-op for other AngelStatue instances.
/// </summary>
internal static class DemonTentacleCombatAi
{
    /// <summary>Vanilla AngelStatue.fun_Idle attack range (horizontal).</summary>
    private const float VanillaAttackRange = 8f;
    /// <summary>Vanilla AngelStatue.FixedUpdate homing range (horizontal).</summary>
    private const float VanillaHomingRange = 17f;

    private static readonly Color EnrageTint = new Color(1f, 0.6f, 0.6f, 1f);

    private static readonly FieldInfo CountField = AccessTools.Field(typeof(AngelStatue), "count");
    private static readonly FieldInfo MoveStopField = AccessTools.Field(typeof(AngelStatue), "movestop");
    private static readonly FieldInfo MoveDistanceField = AccessTools.Field(typeof(EnemyDate), "Movedistance");
    private static readonly FieldInfo RigidbodyField = AccessTools.Field(typeof(EnemyDate), "rigi2D");

    private static int _wallMask = -1;

    private static bool IsActive(AngelStatue enemy, out DemonTentacleMarker marker)
    {
        marker = null;
        if (!CombatAiConfig.Enable || !CombatAiConfig.Get().DemonTentacleEnable)
            return false;

        marker = DemonTentacleRuntime.GetMarker(enemy);
        return marker != null;
    }

    private static bool IsEnraged(EnemyDate enemy)
    {
        float fraction = CombatAiConfig.Get().DemonTentacleEnrageHpFraction;
        return fraction > 0f && enemy.Hp > 0f && enemy.MaxHp > 0f && enemy.Hp <= enemy.MaxHp * fraction;
    }

    private static float GetMoveDistance(EnemyDate enemy)
    {
        return MoveDistanceField?.GetValue(enemy) is float distance ? distance : 10f;
    }

    private static bool IsMoveStopped(AngelStatue enemy)
    {
        return MoveStopField?.GetValue(enemy) is bool stopped && stopped;
    }

    /// <summary>Speeds up the vanilla attack timer; same range / eroflag gate as AngelStatue.fun_Idle.</summary>
    internal static void AfterIdle(AngelStatue enemy)
    {
        if (!IsActive(enemy, out DemonTentacleMarker marker))
            return;

        CombatAiConfig.CombatAiSettings cfg = CombatAiConfig.Get();
        bool enraged = IsEnraged(enemy);
        marker.SetBaseColor(enraged ? EnrageTint : Color.white);

        if (CountField == null || IsMoveStopped(enemy))
            return;
        if (enemy.com_player == null || enemy.com_player.eroflag)
            return;

        if (Mathf.Abs(enemy.distance) >= VanillaAttackRange || Mathf.Abs(enemy.distance_y) >= GetMoveDistance(enemy))
            return;

        float mult = cfg.DemonTentacleAttackRateMultiplier;
        if (enraged)
            mult *= cfg.DemonTentacleEnrageAttackRateMultiplier;
        if (mult <= 1f)
            return;

        float count = (float)CountField.GetValue(enemy);
        CountField.SetValue(enemy, count + (mult - 1f) * Time.deltaTime);
    }

    /// <summary>Called by DemonTentacleRuntime after the first projectile of an attack.</summary>
    internal static void OnShotFired(AngelStatue enemy, DemonTentacleMarker marker)
    {
        if (!IsActive(enemy, out _))
            return;

        CombatAiConfig.CombatAiSettings cfg = CombatAiConfig.Get();
        int extraShots = cfg.DemonTentacleShotsPerAttack - 1;
        if (IsEnraged(enemy))
            extraShots += cfg.DemonTentacleEnrageExtraShots;
        if (extraShots <= 0)
            return;

        marker.StartCoroutine(FireBurst(enemy, marker, extraShots, Mathf.Max(0.05f, cfg.DemonTentacleBurstInterval)));
    }

    private static IEnumerator FireBurst(AngelStatue enemy, DemonTentacleMarker marker, int shots, float interval)
    {
        for (int i = 0; i < shots; i++)
        {
            yield return new WaitForSeconds(interval);
            if (enemy == null || enemy.Hp <= 0f || enemy.com_player == null || enemy.com_player.eroflag)
                yield break;
            DemonTentacleRuntime.TrySpawnProjectile(enemy, marker);
        }
    }

    /// <summary>Runs after vanilla FixedUpdate has set the homing velocity.</summary>
    internal static void AfterFixedUpdate(AngelStatue enemy)
    {
        if (!IsActive(enemy, out _) || IsMoveStopped(enemy))
            return;

        if (!(RigidbodyField?.GetValue(enemy) is Rigidbody2D body))
            return;

        CombatAiConfig.CombatAiSettings cfg = CombatAiConfig.Get();
        Vector2 velocity = body.velocity;

        // Vanilla re-sets the homing velocity only inside this range; outside it scaling would compound every tick.
        bool homing = Mathf.Abs(enemy.distance) < VanillaHomingRange && Mathf.Abs(enemy.distance_y) < GetMoveDistance(enemy);
        if (homing && IsEnraged(enemy) && cfg.DemonTentacleEnrageMoveSpeedMultiplier > 0f)
            velocity *= cfg.DemonTentacleEnrageMoveSpeedMultiplier;

        if (cfg.DemonTentacleBlockWalls)
            velocity = BlockWalls(body.position, velocity, Mathf.Max(0.05f, cfg.DemonTentacleWallProbeRadius));

        body.velocity = velocity;
    }

    /// <summary>Removes the velocity component pointing into Ground / EnemyGround, so the enemy slides along walls.</summary>
    private static Vector2 BlockWalls(Vector2 position, Vector2 velocity, float radius)
    {
        float speed = velocity.magnitude;
        if (speed < 0.0001f)
            return velocity;

        if (_wallMask == -1)
            _wallMask = LayerMask.GetMask("Ground", "EnemyGround");
        if (_wallMask == 0)
            return velocity;

        Vector2 direction = velocity / speed;
        float probe = speed * Time.fixedDeltaTime + radius;
        RaycastHit2D hit = Physics2D.CircleCast(position, radius, direction, probe, _wallMask);
        if (hit.collider == null)
            return velocity;

        // Starting inside a collider reports distance 0 and an unreliable normal; push away from the contact point instead.
        Vector2 normal = hit.distance > 0f ? hit.normal : (position - hit.point).normalized;
        if (normal.sqrMagnitude < 0.0001f)
            return velocity;

        float into = Vector2.Dot(velocity, normal);
        return into < 0f ? velocity - into * normal : velocity;
    }
}

[HarmonyPatch]
internal static class DemonTentacleCombatAiPatch
{
    [HarmonyPatch(typeof(AngelStatue), "fun_Idle")]
    [HarmonyPostfix]
    private static void AngelStatue_fun_Idle(AngelStatue __instance) => DemonTentacleCombatAi.AfterIdle(__instance);

    [HarmonyPatch(typeof(AngelStatue), "FixedUpdate")]
    [HarmonyPostfix]
    private static void AngelStatue_FixedUpdate(AngelStatue __instance) => DemonTentacleCombatAi.AfterFixedUpdate(__instance);
}
