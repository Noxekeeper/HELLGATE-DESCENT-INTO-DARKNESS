using System.Collections.Generic;
using NoREroMod.Systems.Rage;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions;

/// <summary>
/// Centralized reputation event rules beyond direct provocation.
/// Handoff flow is intentionally simple:
/// mark handoff -> next H-scene completion grants fixed bonus to taker's faction.
/// </summary>
internal static class FactionReputationDynamics
{
    private static readonly HashSet<int> _processedKillEnemyIds = new HashSet<int>();
    private static int _pendingHandoffBonuses;

    internal static void RegisterHandoffOccurred()
    {
        if (!EnemyFactionsConfig.Enable)
            return;
        if (EnemyFactionsConfig.HandoffReputationDelta <= 0f)
            return;
        _pendingHandoffBonuses++;
        if (EnemyFactionsConfig.DebugLogging)
            Plugin.Log?.LogInfo("[Reputation] handoff marked pendingCount=" + _pendingHandoffBonuses);
    }

    internal static bool TryConsumePendingHandoffBonus(out float bonusDelta)
    {
        bonusDelta = 0f;
        if (_pendingHandoffBonuses <= 0)
            return false;

        _pendingHandoffBonuses--;
        bonusDelta = EnemyFactionsConfig.HandoffReputationDelta;
        return bonusDelta > 0f;
    }

    internal static bool HasPendingHandoffBonus()
    {
        return _pendingHandoffBonuses > 0;
    }

    internal static void TryRegisterPlayerKill(EnemyDate enemy)
    {
        if (!EnemyFactionsConfig.Enable || enemy == null || enemy.gameObject == null)
            return;
        if (enemy.Hp > 0f)
            return;

        int instanceId = enemy.GetInstanceID();
        if (_processedKillEnemyIds.Contains(instanceId))
            return;
        _processedKillEnemyIds.Add(instanceId);

        int factionId = EnemyFactionRuntime.GetFaction(enemy.gameObject);
        if (FactionIds.IsPassiveNonCombat(factionId))
            return;

        bool rageActive = RageSystem.Enabled && RageSystem.IsActive;
        float penalty = rageActive
            ? EnemyFactionsConfig.KillReputationDeltaWhileRage
            : EnemyFactionsConfig.KillReputationDelta;
        if (Mathf.Approximately(penalty, 0f))
            return;
        PlayerFactionReputation.ModifyScore(factionId, penalty);
        if (EnemyFactionsConfig.DebugLogging)
            Plugin.Log?.LogInfo("[Reputation] kill penalty faction=" + factionId + " delta=" + penalty.ToString("0.##") + " rageActive=" + rageActive);
    }

    internal static void ResetEpisodeData()
    {
        _pendingHandoffBonuses = 0;
        _processedKillEnemyIds.Clear();
    }
}
