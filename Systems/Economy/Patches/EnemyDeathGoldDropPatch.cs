using System;
using System.Collections.Generic;
using HarmonyLib;
using NoREroMod.Systems.CombatAi.Factions;
using UnityEngine;

namespace NoREroMod.Systems.Economy.Patches;

/// <summary>
/// Universal "enemy died → maybe drop gold" hook.
///
/// We mirror <see cref="NoREroMod.Systems.Rage.Patches.RageUniversalKillTrackerPatch"/>:
/// patching <see cref="EnemyDate"/>'s four damage methods (Weapon / DPSWeapon /
/// Magic / DPSMagic) and reacting when <c>Hp &lt;= 0</c> at postfix time. This
/// catches every enemy class without per-class patches and fires immediately at
/// kill time, instead of waiting for the eventual <c>Destroy(gameObject)</c> that
/// runs only after the death animation finishes.
///
/// Idempotency uses <c>gameObject.GetInstanceID()</c> in a <see cref="HashSet{Int32}"/>;
/// we never remove ids on destroy because Unity reuses instance ids (same
/// approach the biscord reward path takes).
/// </summary>
[HarmonyPatch]
internal static class EnemyDeathGoldDropPatch
{
    private static readonly HashSet<int> RewardedInstanceIds = new HashSet<int>();

    [HarmonyPatch(typeof(EnemyDate), "WeaponDamage")]
    [HarmonyPostfix]
    private static void WeaponDamage_Postfix(EnemyDate __instance) => TryAward(__instance);

    [HarmonyPatch(typeof(EnemyDate), "DPSWeaponDamage")]
    [HarmonyPostfix]
    private static void DPSWeaponDamage_Postfix(EnemyDate __instance) => TryAward(__instance);

    [HarmonyPatch(typeof(EnemyDate), "MagicDamage")]
    [HarmonyPostfix]
    private static void MagicDamage_Postfix(EnemyDate __instance) => TryAward(__instance);

    [HarmonyPatch(typeof(EnemyDate), "DPSMagicDamage")]
    [HarmonyPostfix]
    private static void DPSMagicDamage_Postfix(EnemyDate __instance) => TryAward(__instance);

    [HarmonyPatch(typeof(EnemyDate), "StabDamage")]
    [HarmonyPostfix]
    private static void StabDamage_Postfix(EnemyDate __instance) => TryAward(__instance);

    private static void TryAward(EnemyDate enemy)
    {
        if (enemy == null) return;
        if (!EconomicConfig.Enable) return;

        try
        {
            if (enemy.Hp > 0f) return;

            int id = enemy.gameObject != null ? enemy.gameObject.GetInstanceID() : 0;
            if (id == 0) return;
            if (RewardedInstanceIds.Contains(id)) return;

            GoldRule rule = GoldDropTable.Resolve(enemy);
            if (rule == null)
            {
                if (EconomicConfig.DebugLogging)
                {
                    int fid = EnemyFactionRuntime.GetFaction(enemy.gameObject);
                    GoldDropTableConfig cfg = GoldDropTable.Get();
                    int factionRulesCount = cfg != null && cfg.FactionRules != null ? cfg.FactionRules.Length : -1;
                    int overridesCount = cfg != null && cfg.EnemyOverrides != null ? cfg.EnemyOverrides.Length : -1;
                    Plugin.Log?.LogInfo($"[GoldDrop:trace] no rule for {enemy.gameObject.name}/{enemy.GetType().Name} faction={fid} factionRules.Length={factionRulesCount} overrides.Length={overridesCount}");
                }
                return;
            }

            // Mark BEFORE rolling so a second damage method on the same kill frame does not double-pay.
            RewardedInstanceIds.Add(id);

            float roll01 = UnityEngine.Random.value;
            if (roll01 > rule.Chance)
            {
                if (EconomicConfig.DebugLogging)
                    Plugin.Log?.LogInfo($"[GoldDrop:trace] roll failed for {enemy.gameObject.name}: rolled={roll01:0.###} > chance={rule.Chance:0.###}");
                return;
            }

            int rolled = rule.Roll();
            if (rolled <= 0) return;

            // Difficulty scaling.
            int difficulty = ResolveGameDifficulty();
            float multiplier = 1f;
            GoldDropTableSettings settings = GoldDropTable.Get().Settings;
            if (settings != null && settings.ApplyDifficultyScaling)
                multiplier = EconomicConfig.DifficultyMultipliers.Resolve(difficulty);

            // Phase 2 hook (FactionTreasury, Rage, MindBroken …). No-op until subscribers exist.
            int factionId = EnemyFactionRuntime.GetFaction(enemy.gameObject);
            multiplier *= DropMultiplierBus.Compute(enemy, factionId);

            int amount = Mathf.RoundToInt(rolled * multiplier);
            int floor = settings != null ? settings.MinAmountFloor : 1;
            if (amount < floor) amount = floor;
            if (amount <= 0) return;

            Vector3 p = enemy.transform.position;
            GoldDropAwarder.TrySpawnDrop(new Vector2(p.x, p.y + EconomicConfig.DropSpawnYOffset), amount);

            if (EconomicConfig.DebugLogging)
                Plugin.Log?.LogInfo($"[GoldDrop] {enemy.GetType().Name}/{enemy.gameObject.name}: rolled={rolled} mult={multiplier:0.##} -> {amount}");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[GoldDrop] TryAward threw: " + ex.Message);
        }
    }

    private static int ResolveGameDifficulty()
    {
        try { return StaticMng.GameDifficulty; } catch { return 1; }
    }

    /// <summary>Cleared on stage reset / scene change so a re-spawned enemy with a recycled instance id can reward again.</summary>
    internal static void ClearRewardedSet()
    {
        RewardedInstanceIds.Clear();
    }
}
