using System.Collections.Generic;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions;

/// <summary>
/// Lightweight relation event runtime:
/// trigger on "Avoided" (dash evade), then track non-aggressive proximity window.
/// </summary>
internal static class FactionDeescalationRuntime
{
    private static bool _active;
    private static int _activeFactionId = FactionIds.Neutral;
    private static float _startTime;
    private static float _nextTickAt;
    private static string _pendingUiResult;
    private static bool _hasPendingUiResult;

    internal static void NotifyPlayerAvoidedEnemyAttack(playercon player)
    {
        if (!EnemyFactionsConfig.Enable || !EnemyFactionsConfig.EnableDeescalationRollEvent)
            return;
        if (_active)
            return;
        if (player == null || player.transform == null)
            return;
        if (player.eroflag || player.erodown != 0)
            return;

        if (!TryResolveNearestHostileFaction(player.transform.position, Mathf.Max(0.5f, EnemyFactionsConfig.DeescalationRadius), out int factionId))
            return;

        _active = true;
        _activeFactionId = factionId;
        _startTime = Time.time;
        _nextTickAt = _startTime;

        if (EnemyFactionsConfig.DebugLogging)
            Plugin.Log?.LogInfo("[Reputation] Deescalation event started for faction=" + factionId);
    }

    internal static void Process(playercon player)
    {
        if (!_active)
            return;
        if (!EnemyFactionsConfig.Enable || !EnemyFactionsConfig.EnableDeescalationRollEvent)
        {
            Reset();
            return;
        }
        if (player == null || player.transform == null || player.eroflag || player.erodown != 0)
        {
            Reset();
            return;
        }

        float now = Time.time;
        if (now < _nextTickAt)
            return;
        _nextTickAt = now + Mathf.Max(0.05f, EnemyFactionsConfig.DeescalationTickSeconds);

        float elapsed = now - _startTime;
        bool playerAggressiveNow = player.Attacknow || player.magicnow;
        if (playerAggressiveNow)
        {
            if (elapsed >= Mathf.Max(0f, EnemyFactionsConfig.DeescalationLateAttackPenaltyStartSeconds))
            {
                int repForPenalty = MercyReputationFactionId(_activeFactionId);
                PlayerFactionReputation.ModifyScore(repForPenalty, EnemyFactionsConfig.DeescalationLateAttackPenaltyDelta);
                QueueUiResult("Mercy broken: -5%");
                if (EnemyFactionsConfig.DebugLogging)
                    Plugin.Log?.LogInfo("[Reputation] Deescalation late-attack penalty faction=" + repForPenalty + " delta=" + EnemyFactionsConfig.DeescalationLateAttackPenaltyDelta.ToString("0.##"));
            }
            Reset();
            return;
        }

        float radius = Mathf.Max(0.5f, EnemyFactionsConfig.DeescalationRadius);
        int enemyCountInRadius = CountHostileEnemiesFromFactionInRadius(player.transform.position, _activeFactionId, radius);
        float duration = Mathf.Max(0.1f, EnemyFactionsConfig.DeescalationDurationSeconds);

        if (elapsed < duration)
        {
            if (enemyCountInRadius <= 0)
            {
                QueueUiResult("Mercy lost: out of range");
                if (EnemyFactionsConfig.DebugLogging)
                    Plugin.Log?.LogInfo("[Reputation] Deescalation cancelled (left radius) faction=" + _activeFactionId);
                Reset();
                return;
            }

            return;
        }

        int repFaction = MercyReputationFactionId(_activeFactionId);
        PlayerFactionReputation.ModifyScore(repFaction, EnemyFactionsConfig.DeescalationRewardRelationDelta);
        QueueUiResult("Mercy: +5%");
        if (EnemyFactionsConfig.DebugLogging)
            Plugin.Log?.LogInfo("[Reputation] Deescalation success (full window) faction=" + repFaction + " delta=" + EnemyFactionsConfig.DeescalationRewardRelationDelta.ToString("0.##"));

        Reset();
    }

    /// <summary>
    /// Reputation HUD uses a single "Bandits" row for the whole bandit family; Mercy should move that score.
    /// </summary>
    private static int MercyReputationFactionId(int runtimeFactionId)
    {
        if (FactionIds.IsPassiveNonCombat(runtimeFactionId))
            return FactionIds.Neutral;
        if (FactionIds.IsBanditFamily(runtimeFactionId))
            return FactionIds.Bandits;
        return runtimeFactionId;
    }

    internal static bool IsEventActive => _active;

    internal static float GetProgress01()
    {
        if (!_active)
            return 0f;
        float duration = Mathf.Max(0.1f, EnemyFactionsConfig.DeescalationDurationSeconds);
        float elapsed = Mathf.Max(0f, Time.time - _startTime);
        return Mathf.Clamp01(elapsed / duration);
    }

    internal static bool IsLatePenaltyWindow()
    {
        if (!_active)
            return false;
        float elapsed = Mathf.Max(0f, Time.time - _startTime);
        return elapsed >= Mathf.Max(0f, EnemyFactionsConfig.DeescalationLateAttackPenaltyStartSeconds);
    }

    internal static bool TryConsumeUiResult(out string result)
    {
        result = null;
        if (!_hasPendingUiResult || string.IsNullOrEmpty(_pendingUiResult))
            return false;
        result = _pendingUiResult;
        _pendingUiResult = null;
        _hasPendingUiResult = false;
        return true;
    }

    private static void Reset()
    {
        _active = false;
        _activeFactionId = FactionIds.Neutral;
        _startTime = 0f;
        _nextTickAt = 0f;
    }

    private static void QueueUiResult(string result)
    {
        if (string.IsNullOrEmpty(result))
            return;
        _pendingUiResult = result;
        _hasPendingUiResult = true;
    }

    private static bool TryResolveNearestHostileFaction(Vector3 playerPos, float radius, out int factionId)
    {
        factionId = FactionIds.Neutral;
        float bestSq = radius * radius;
        bool found = false;
        foreach (KeyValuePair<int, EnemyDate> kv in EnemyFactionRuntime.EnumerateEnemies())
        {
            EnemyDate enemy = kv.Value;
            if (enemy == null || enemy.gameObject == null || enemy.Hp <= 0f)
                continue;

            int candidateFaction = EnemyFactionRuntime.GetFaction(enemy.gameObject);
            if (FactionIds.IsPassiveNonCombat(candidateFaction))
                continue;
            if (PlayerFactionReputation.GetScore(candidateFaction) >= FactionReputationBehavior.GetPeaceReputationThreshold())
                continue;

            Vector3 pos = enemy.transform.position;
            float dx = pos.x - playerPos.x;
            float dy = pos.y - playerPos.y;
            float sq = dx * dx + dy * dy;
            if (sq > bestSq)
                continue;

            bestSq = sq;
            factionId = candidateFaction;
            found = true;
        }
        return found;
    }

    private static int CountHostileEnemiesFromFactionInRadius(Vector3 playerPos, int factionId, float radius)
    {
        int count = 0;
        float radiusSq = radius * radius;
        foreach (KeyValuePair<int, EnemyDate> kv in EnemyFactionRuntime.EnumerateEnemies())
        {
            EnemyDate enemy = kv.Value;
            if (enemy == null || enemy.gameObject == null || enemy.Hp <= 0f)
                continue;
            if (EnemyFactionRuntime.GetFaction(enemy.gameObject) != factionId)
                continue;

            Vector3 pos = enemy.transform.position;
            float dx = pos.x - playerPos.x;
            float dy = pos.y - playerPos.y;
            if (dx * dx + dy * dy <= radiusSq)
                count++;
        }
        return count;
    }
}
