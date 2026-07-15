using System;
using System.Collections.Generic;
using NoREroMod;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.Dialogue;
using NoREroMod.Systems.Spawn;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace NoREroMod.Systems.EventCore.Reinforcement;

/// <summary>
/// Knockdown-triggered reinforcement spawns near the player when inside a radius of a world anchor.
/// Optional suspicion lines via <c>phrasesFromEventFolder</c> (e.g. EventTrap packs).
/// </summary>
internal sealed class ReinforcementEncounterDriver : MonoBehaviour
{
    private const float SceneReloadDebounceSeconds = 0.5f;

    private static bool _loggedDisabled;

    private ReinforcementRegistryFile _registry;
    private List<ReinforcementLoadedEncounter> _encounters = new List<ReinforcementLoadedEncounter>();
    private float _accum;
    private float _stepSeconds = 0.25f;
    private float _nextReloadAllowedUnscaled = -9999f;
    private string _lastLoggedSummary = string.Empty;

    /// <param name="rediscoverFromSpawnFiles">When false and encounters are already loaded, only resets runtime state (no disk scan).</param>
    internal void ReloadFromDisk(bool rediscoverFromSpawnFiles = true)
    {
        try
        {
            if (!rediscoverFromSpawnFiles && _encounters != null && _encounters.Count > 0)
            {
                ResetRuntimeState();
                return;
            }

            ReloadFromDiskCore();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Reinforcement] ReloadFromDisk exception: {ex}");
        }
    }

    internal void ResetRuntimeState()
    {
        if (_encounters == null)
            return;

        for (int i = 0; i < _encounters.Count; i++)
        {
            ReinforcementLoadedEncounter ev = _encounters[i];
            if (ev == null)
                continue;

            ev.InsideSuspicionZone = false;
            ev.LastThoughtTime = -9999f;
            ev.PrevPlayerKnockdown = false;
            ev.PrevDistanceToAnchor = float.MaxValue;
            ev.PendingSpawnAt = -1f;
            ev.SpawnSuccessCount = 0;
            ev.Depleted = false;
        }

        _accum = 0f;
    }

    private void ReloadFromDiskCore()
    {
        _encounters.Clear();
        _registry = null;
        _accum = 0f;

        if (Plugin.reinforcementEncountersEnable == null || !Plugin.reinforcementEncountersEnable.Value)
        {
            if (!_loggedDisabled)
            {
                _loggedDisabled = true;
                Plugin.Log?.LogInfo(
                    "[Reinforcement] Off: set [EventCore] ReinforcementEncountersEnable = true in BepInEx/config/NoREroMod_HellGate.cfg and enable reinforcement_registry.json.");
            }

            return;
        }

        if (!ReinforcementEncounterLoader.TryLoadAll(out _registry, out _encounters))
        {
            Plugin.Log?.LogWarning("[Reinforcement] Load failed — see loader messages above.");
            return;
        }

        if (_registry == null || !_registry.enabled)
        {
            Plugin.Log?.LogInfo(
                $"[Reinforcement] Registry present but disabled (enabled={(_registry != null && _registry.enabled).ToString()}).");
            return;
        }

        _stepSeconds = Mathf.Clamp(_registry.checkIntervalSeconds, 0.05f, 2f);
        LogLoadSummaryIfChanged();
    }

    private void LogLoadSummaryIfChanged()
    {
        string summary = $"{_encounters.Count}|{_stepSeconds:F2}";
        if (string.Equals(summary, _lastLoggedSummary, StringComparison.Ordinal))
            return;

        _lastLoggedSummary = summary;
        Plugin.Log?.LogInfo(
            $"[Reinforcement] Loaded {_encounters.Count} encounter(s). step={_stepSeconds:F2}s.");
    }

    private static string TryGetFragReSceneName()
    {
        try
        {
            game_fragmng frag = UnifiedGameControllerCacheManager.GetGameFragMng();
            if (frag != null && !string.IsNullOrEmpty(frag._re_Scenename))
                return frag._re_Scenename.Trim();
        }
        catch
        {
        }

        return string.Empty;
    }

    private static bool IsKnockdown(playercon pc)
    {
        if (pc == null || pc._Death)
            return false;
        if (pc.eroflag || pc._eroflag2)
            return false;
        return pc.erodown != 0;
    }

    private static bool SceneFilterAllows(ReinforcementRegistryEntry reg, string fragScene, string unityActive)
    {
        if (reg == null)
            return false;
        string filter = reg.sceneNameContains != null ? reg.sceneNameContains.Trim() : string.Empty;
        if (filter.Length == 0)
            return true;

        string[] tokens = filter.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return true;

        for (int i = 0; i < tokens.Length; i++)
        {
            string t = tokens[i].Trim();
            if (t.Length == 0)
                continue;
            if (!string.IsNullOrEmpty(fragScene) &&
                fragScene.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (!string.IsNullOrEmpty(unityActive) &&
                unityActive.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static bool ShouldSkipDiskReload(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return true;
        if (string.Equals(sceneName, "Gametitle", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (ShouldSkipDiskReload(scene.name))
            return;

        if (mode == LoadSceneMode.Additive)
            return;

        float t = Time.unscaledTime;
        if (t < _nextReloadAllowedUnscaled)
            return;
        _nextReloadAllowedUnscaled = t + SceneReloadDebounceSeconds;

        ReloadFromDisk();
    }

    private void Update()
    {
        if (_registry == null || !_registry.enabled || _encounters == null || _encounters.Count == 0)
            return;

        _accum += Time.deltaTime;
        if (_accum < _stepSeconds)
            return;

        _accum = 0f;
        Tick();
    }

    private void Tick()
    {
        GameObject playerObj = UnifiedPlayerCacheManager.GetPlayerObject();
        playercon pc = UnifiedPlayerCacheManager.GetPlayer();
        if (playerObj == null || pc == null)
            return;

        Vector2 p = new Vector2(playerObj.transform.position.x, playerObj.transform.position.y);
        string unityActive = SceneManager.GetActiveScene().name ?? string.Empty;
        string fragScene = TryGetFragReSceneName();

        for (int i = 0; i < _encounters.Count; i++)
        {
            ReinforcementLoadedEncounter ev = _encounters[i];
            if (ev == null || ev.Registry == null || ev.Config == null)
                continue;

            if (!SceneFilterAllows(ev.Registry, fragScene, unityActive))
                continue;

            if (ev.Depleted)
                continue;

            float d = Vector2.Distance(p, ev.Anchor);
            float triggerR = ev.TriggerR;
            float enterR = ev.SuspicionEnterR;

            if (d > triggerR)
                ev.PendingSpawnAt = -1f;

            bool wasInside = ev.InsideSuspicionZone;
            ev.InsideSuspicionZone = d <= enterR;
            bool cooldownReady = Time.time - ev.LastThoughtTime >= ev.ThoughtCooldown;
            bool onlyOnEnter = ev.Config.suspicionThoughtOnlyOnEnteringZone;
            if (ev.PhraseLines != null && ev.PhraseLines.Length > 0 && ev.InsideSuspicionZone && cooldownReady)
            {
                if (!onlyOnEnter || !wasInside)
                    TryShowSuspicionThought(ev, playerObj);
            }

            bool down = IsKnockdown(pc);
            bool edgeIntoKo = down && !ev.PrevPlayerKnockdown;
            bool enteredWhileDown =
                down &&
                ev.PrevDistanceToAnchor > triggerR &&
                d <= triggerR;
            bool trigger = (edgeIntoKo || enteredWhileDown) && d <= triggerR;

            ev.PrevPlayerKnockdown = down;
            ev.PrevDistanceToAnchor = d;

            int cap = ev.Config.maxKnockdownSpawns;
            bool underCap = cap <= 0 || ev.SpawnSuccessCount < cap;

            if (ev.PendingSpawnAt > 0f && Time.time >= ev.PendingSpawnAt)
            {
                ev.PendingSpawnAt = -1f;
                float dSpawn = Vector2.Distance(p, ev.Anchor);
                if (underCap && dSpawn <= triggerR)
                {
                    int n = TrySpawnWave(ev, p);
                    if (n > 0)
                        RegisterSpawnSuccess(ev, cap);
                    else
                        LogKnockdownSpawnFailed(ev, p);
                }

                continue;
            }

            if (underCap && trigger)
            {
                float delay = ev.Config.spawnDelaySeconds;
                if (delay > 0.05f)
                {
                    if (ev.PendingSpawnAt <= 0f)
                        ev.PendingSpawnAt = Time.time + delay;
                }
                else
                {
                    int n = TrySpawnWave(ev, p);
                    if (n > 0)
                        RegisterSpawnSuccess(ev, cap);
                    else
                        LogKnockdownSpawnFailed(ev, p);
                }
            }
        }
    }

    private static void LogKnockdownSpawnFailed(ReinforcementLoadedEncounter ev, Vector2 playerPlanar)
    {
        if (ev == null)
            return;
        Plugin.Log?.LogWarning(
            $"[Reinforcement] Knockdown trigger at anchor '{ev.LogLabel}' spawned 0 enemies (player {playerPlanar.x:F1},{playerPlanar.y:F1}). Check EnemyPrefabRegistry / types / scene filter.");
    }

    private static void RegisterSpawnSuccess(ReinforcementLoadedEncounter ev, int cap)
    {
        if (ev == null)
            return;
        ev.SpawnSuccessCount++;
        if (cap > 0 && ev.SpawnSuccessCount >= cap)
        {
            ev.Depleted = true;
            ev.PendingSpawnAt = -1f;
            Plugin.Log?.LogInfo($"[Reinforcement] Anchor '{ev.LogLabel}' reached maxKnockdownSpawns={cap}; disabled for this session.");
        }
    }

    private static void TryShowSuspicionThought(ReinforcementLoadedEncounter ev, GameObject playerObj)
    {
        if (ev.PhraseLines == null || ev.PhraseLines.Length == 0)
            return;

        try
        {
            if (!DialogueFramework.IsInitialized)
                DialogueFramework.Initialize();
        }
        catch
        {
        }

        DialogueDisplay display = DialogueFramework.GetDisplay();
        if (display == null)
        {
            Plugin.Log?.LogWarning("[Reinforcement] DialogueDisplay not ready; suspicion line skipped.");
            return;
        }

        string line = ev.PhraseLines[Random.Range(0, ev.PhraseLines.Length)];
        float dur = Mathf.Clamp(ev.Config.thoughtDurationSeconds, 0.5f, 30f);
        float vOff = ev.Config.thoughtVerticalOffsetPx;

        DialogueStyle style = DialogueDisplay.BuildAradiaThoughtStyle(vOff, 0f, true);
        display.ShowAradiaThought(playerObj, line, "hair1", style, dur, disableBoneFallbacks: true);
        ev.LastThoughtTime = Time.time;
    }

    private static int TrySpawnWave(ReinforcementLoadedEncounter ev, Vector2 playerPlanar)
    {
        if (ev.EnemyTypes == null || ev.EnemyTypes.Length == 0)
            return 0;
        if (ev.HorizontalDistances == null || ev.HorizontalDistances.Length == 0)
            return 0;

        int minC = Mathf.Max(1, ev.Config.spawnCountMin);
        int maxC = Mathf.Max(minC, ev.Config.spawnCountMax);
        int count = Random.Range(minC, maxC + 1);

        string faction = ev.Config.spawnFactionIdRaw != null ? ev.Config.spawnFactionIdRaw.Trim() : string.Empty;
        float jitter = Mathf.Abs(ev.Config.verticalJitter);
        bool rightOnly = ev.Config.spawnRightOnly;

        EnemyPrefabRegistry.Initialize();

        var pack = new List<SpawnConfigExecutor.RuntimeSpawnPoint>(count);
        for (int i = 0; i < count; i++)
        {
            string enemyType = ev.EnemyTypes[Random.Range(0, ev.EnemyTypes.Length)];
            int dist = ev.HorizontalDistances[Random.Range(0, ev.HorizontalDistances.Length)];
            float sign = rightOnly ? 1f : (Random.value < 0.5f ? -1f : 1f);
            float yOff = jitter > 0.0001f ? Random.Range(-jitter, jitter) : 0f;
            Vector2 pos = playerPlanar + new Vector2(sign * dist, yOff);

            pack.Add(new SpawnConfigExecutor.RuntimeSpawnPoint
            {
                Center = pos,
                EnemyType = enemyType,
                FactionIdRaw = string.IsNullOrEmpty(faction) ? null : faction,
                EventCoreEventId = null,
                Count = 1
            });
        }

        int spawned = SpawnConfigExecutor.SpawnRuntimePack(pack.ToArray(), "[Reinforcement]");
        if (spawned > 0)
        {
            Plugin.Log?.LogInfo(
                $"[Reinforcement] Spawned {spawned} unit(s) near player ({playerPlanar.x:F1},{playerPlanar.y:F1}) [anchor '{ev.LogLabel}'].");
        }

        return spawned;
    }
}
