using System;
using System.Collections.Generic;
using System.Globalization;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.CombatAi.Factions.Patches;
using NoREroMod.Systems.Pregnancy.Patches;
using NoREroMod.Systems.Spawn;
using UnityEngine;
using Random = UnityEngine.Random;

namespace NoREroMod.Systems.Pregnancy.ShelterAttack;

/// <summary>Sequential spawn at fixed ParishChurch points with per-point cooldown.</summary>
internal static class ShelterAttackSpawnScheduler
{
    internal static readonly Vector2[] SpawnPoints =
    {
        new Vector2(-175.11f, -47.54f),
        new Vector2(-170.42f, -47.33f),
        new Vector2(-165.65f, -47.35f),
        new Vector2(-159.78f, -47.32f),
        new Vector2(-151.83f, -47.31f)
    };

    private static readonly List<string> _pendingEnemies = new List<string>();
    private static readonly float[] _pointCooldownUntil = new float[SpawnPoints.Length];

    private static bool _waveQueueReady;
    private static float _nextSpawnAttemptAt;
    private static string _failingHeadEnemy;
    private static int _failingHeadAttempts;

    private const int MaxSpawnAttemptsPerEntry = 5;

    internal static void Reset()
    {
        _pendingEnemies.Clear();
        _waveQueueReady = false;
        _nextSpawnAttemptAt = 0f;
        _failingHeadEnemy = null;
        _failingHeadAttempts = 0;
        for (int i = 0; i < _pointCooldownUntil.Length; i++)
            _pointCooldownUntil[i] = 0f;
    }

    internal static void ResetForAssault()
    {
        Reset();
        ShelterAttackState.CurrentWave = 0;
        ShelterAttackState.SpawnIndexInWave = 0;
    }

    internal static void PrepareCurrentWaveQueue()
    {
        _pendingEnemies.Clear();
        _pendingEnemies.AddRange(
            ShelterAttackWaves.BuildSpawnQueue(ShelterAttackState.AttackingFaction, ShelterAttackState.CurrentWave));

        ShelterAttackWaves.FilterUnspawnableEntries(_pendingEnemies, ShelterAttackState.CurrentWave);

        if (ShelterAttackState.SpawnIndexInWave > 0 && ShelterAttackState.SpawnIndexInWave < _pendingEnemies.Count)
            _pendingEnemies.RemoveRange(0, ShelterAttackState.SpawnIndexInWave);

        _waveQueueReady = true;
        _nextSpawnAttemptAt = Time.unscaledTime;
        _failingHeadEnemy = null;
        _failingHeadAttempts = 0;

        if (_pendingEnemies.Count == 0)
        {
            Plugin.Log?.LogError(
                $"[Pregnancy.ShelterAttack] Wave {ShelterAttackState.CurrentWave + 1} has no spawnable enemies — check Shelter event wave JSON.");
        }

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
        {
            Plugin.Log?.LogInfo(
                $"[Pregnancy.ShelterAttack] Wave {ShelterAttackState.CurrentWave + 1}/{ShelterAttackState.TotalWaves} queued: " +
                $"{_pendingEnemies.Count} enemy spawn(s) remaining (faction={ShelterAttackState.AttackingFaction}).");
        }
    }

    internal static bool HasPendingSpawns => _pendingEnemies.Count > 0;

    internal static void Tick()
    {
        if (!_waveQueueReady)
            PrepareCurrentWaveQueue();

        if (!HideoutSceneUtility.IsParishHideoutActive())
            return;

        if (_pendingEnemies.Count == 0)
            return;

        if (Time.unscaledTime < _nextSpawnAttemptAt)
            return;

        if (!TryPickSpawnPoint(out int pointIndex))
        {
            _nextSpawnAttemptAt = Time.unscaledTime + 0.25f;
            return;
        }

        string enemyType = _pendingEnemies[0];
        _pendingEnemies.RemoveAt(0);

        if (!TrySpawnEnemy(enemyType, SpawnPoints[pointIndex]))
        {
            if (string.Equals(_failingHeadEnemy, enemyType, StringComparison.OrdinalIgnoreCase))
                _failingHeadAttempts++;
            else
            {
                _failingHeadEnemy = enemyType;
                _failingHeadAttempts = 1;
            }

            if (_failingHeadAttempts >= MaxSpawnAttemptsPerEntry)
            {
                Plugin.Log?.LogError(
                    $"[Pregnancy.ShelterAttack] Dropping unspawnable '{enemyType}' from wave {ShelterAttackState.CurrentWave + 1} after {_failingHeadAttempts} failed attempts.");
                _failingHeadEnemy = null;
                _failingHeadAttempts = 0;
                ShelterAttackState.SpawnIndexInWave++;
                ShelterAttackSlotStore.MarkDirty();
                _nextSpawnAttemptAt = Time.unscaledTime + 0.15f;
                return;
            }

            _pendingEnemies.Insert(0, enemyType);
            _nextSpawnAttemptAt = Time.unscaledTime + 1f;
            return;
        }

        _failingHeadEnemy = null;
        _failingHeadAttempts = 0;

        ShelterAttackState.SpawnIndexInWave++;
        ShelterAttackSlotStore.MarkDirty();

        float cooldownMin = PregnancyConfig.ShelterAttackSpawnCooldownMin?.Value ?? 4f;
        float cooldownMax = PregnancyConfig.ShelterAttackSpawnCooldownMax?.Value ?? 8f;
        if (cooldownMax < cooldownMin)
            cooldownMax = cooldownMin;

        float pointCooldown = Random.Range(cooldownMin, cooldownMax);
        _pointCooldownUntil[pointIndex] = Time.unscaledTime + pointCooldown;
        _nextSpawnAttemptAt = Time.unscaledTime + 0.15f;
    }

    private static bool TryPickSpawnPoint(out int pointIndex)
    {
        pointIndex = -1;
        float now = Time.unscaledTime;
        var ready = new List<int>();

        for (int i = 0; i < SpawnPoints.Length; i++)
        {
            if (now >= _pointCooldownUntil[i])
                ready.Add(i);
        }

        if (ready.Count == 0)
            return false;

        pointIndex = ready[Random.Range(0, ready.Count)];
        return true;
    }

    private static bool TrySpawnEnemy(string enemyType, Vector2 position)
    {
        int attackingFaction = ShelterAttackState.AttackingFaction;
        string factionRaw = attackingFaction.ToString(CultureInfo.InvariantCulture);
        GameObject spawned = SpawnConfigExecutor.TrySpawnRuntimeEnemy(
            enemyType,
            position,
            factionRaw,
            markHostileToPlayer: true);

        if (spawned == null)
        {
            Plugin.Log?.LogWarning($"[Pregnancy.ShelterAttack] Failed to spawn '{enemyType}' at ({position.x:F2},{position.y:F2}).");
            return false;
        }

        ApplyShelterAttackFaction(spawned, attackingFaction);

        ShelterAttackEnemyMarker marker = spawned.GetComponent<ShelterAttackEnemyMarker>();
        if (marker == null)
            marker = spawned.AddComponent<ShelterAttackEnemyMarker>();
        ShelterAttackTracker.Register(marker);

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
        {
            Plugin.Log?.LogInfo(
                $"[Pregnancy.ShelterAttack] Spawned {enemyType} at ({position.x:F2},{position.y:F2}) wave={ShelterAttackState.CurrentWave + 1}, faction={attackingFaction}.");
        }

        return true;
    }

    /// <summary>
    /// Prefabs activate during <see cref="Object.Instantiate"/> before <see cref="SpawnFactionOverride"/>
    /// is wired — re-apply the assault faction so Cocoonman and other roster defaults do not win.
    /// </summary>
    private static void ApplyShelterAttackFaction(GameObject spawned, int attackingFaction)
    {
        if (spawned == null || attackingFaction == 0)
            return;

        string factionRaw = attackingFaction.ToString(CultureInfo.InvariantCulture);
        SpawnFactionOverride overrideComponent = spawned.GetComponent<SpawnFactionOverride>();
        if (overrideComponent == null)
            overrideComponent = spawned.AddComponent<SpawnFactionOverride>();
        overrideComponent.FactionIdRaw = factionRaw;

        EnemyDate enemy = spawned.GetComponent<EnemyDate>();
        if (enemy == null)
            enemy = spawned.GetComponentInChildren<EnemyDate>();
        if (enemy == null)
            return;

        EnemyFactionRuntime.SetFaction(enemy.gameObject, attackingFaction);
        EnemyFactionRuntime.RegisterEnemy(enemy);
        EnemyDateFactionColorBootstrapPatch.ApplyFactionMarker(enemy);
    }
}
