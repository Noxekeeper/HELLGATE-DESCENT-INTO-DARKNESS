using System;
using System.Collections.Generic;
using NoREroMod;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.Dialogue;
using NoREroMod.Systems.EventCore.Core;
using NoREroMod.Systems.Spawn;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace NoREroMod.Systems.EventCore.EventTrap;

/// <summary>
/// Non-modal coordinate-zone suspicion + knockdown ambush (JSON under EventCore language folders).
/// Separate from <see cref="Core.EventCoreRuntime"/> modal flow.
/// </summary>
internal sealed class EventTrapEncounterDriver : MonoBehaviour
{
    private const float SceneReloadDebounceSeconds = 0.5f;

    /// <summary>Shared across all EventTrap anchors so overlapping zones do not spam danger lines.</summary>
    private static float _globalPhraseCooldownUntil = -9999f;

    private static bool _loggedEventTrapDisabled;

    private EventTrapRegistryFile _registry;
    private List<EventTrapLoadedEncounter> _encounters = new List<EventTrapLoadedEncounter>();
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
            Plugin.Log?.LogError($"[EventTrap] ReloadFromDisk exception: {ex}");
        }
    }

    internal void ResetRuntimeState()
    {
        if (_encounters == null)
            return;

        for (int i = 0; i < _encounters.Count; i++)
        {
            EventTrapLoadedEncounter ev = _encounters[i];
            if (ev == null)
                continue;

            ev.InsideSuspicionZone = false;
            ev.LastThoughtTime = -9999f;
            ev.PrevPlayerKnockdown = false;
            ev.LifetimeAmbushSpawned = false;
            ev.PendingAmbushSpawnAt = -1f;
            ev.AmbushSpawnSuccessCount = 0;
            ev.AmbushDepleted = false;
            ev.PrevDistanceToAnchor = float.MaxValue;
        }

        _accum = 0f;
    }

    private void ReloadFromDiskCore()
    {
        _encounters.Clear();
        _registry = null;
        _accum = 0f;

        if (Plugin.eventTrapEncountersEnable == null || !Plugin.eventTrapEncountersEnable.Value)
        {
            if (!_loggedEventTrapDisabled)
            {
                _loggedEventTrapDisabled = true;
                Plugin.Log?.LogInfo(
                    "[EventTrap] Off: set [EventCore] EventTrapEncountersEnable = true in BepInEx/config/NoREroMod_HellGate.cfg (and event_trap_registry.json enabled, or legacy ambient_spike_registry.json).");
            }

            return;
        }

        if (!EventTrapEncounterLoader.TryLoadAll(out _registry, out _encounters))
        {
            Plugin.Log?.LogWarning(
                "[EventTrap] Load failed (see loader messages above): EventCore JSON root, event_trap_registry.json, or encounter packs under HellGateJson/EventCore/.");
            return;
        }

        if (_registry == null || !_registry.enabled)
        {
            Plugin.Log?.LogInfo(
                $"[EventTrap] Registry present but disabled (enabled={(_registry != null && _registry.enabled).ToString()}); encounters inactive.");
            return;
        }

        _stepSeconds = Mathf.Clamp(_registry.checkIntervalSeconds, 0.05f, 2f);
        LogLoadSummaryIfChanged();
        if (_encounters.Count == 0)
        {
            Plugin.Log?.LogWarning(
                "[EventTrap] Registry enabled but 0 encounters loaded — check EventCore/_shared/<folder>/config.json + per-language phrases, or legacy EventCore/<Lang>/<folder>/ (config + phrases).");
        }
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

    private static bool SceneFilterAllows(EventTrapRegistryEntry reg, string fragScene, string unityActive)
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

    /// <summary>
    /// Combat / damage knockdown (erodown) without H-scene flags. Matches design doc: not grab/ero states.
    /// </summary>
    private static bool IsEventTrapKnockdown(playercon pc)
    {
        if (pc == null || pc._Death)
            return false;
        if (pc.eroflag || pc._eroflag2)
            return false;
        return pc.erodown != 0;
    }

    /// <summary>
    /// Skip full EventTrap reload on title/menu scenes — they trigger many <see cref="SceneManager.sceneLoaded"/> events
    /// and <see cref="EventTrapEncounterLoader"/> can rescan all HellGate spawn files (heavy I/O).
    /// </summary>
    private static bool ShouldSkipEventTrapDiskReload(string sceneName)
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

    private void LogLoadSummaryIfChanged()
    {
        string summary = $"{_encounters.Count}|{_stepSeconds:F2}";
        if (string.Equals(summary, _lastLoggedSummary, StringComparison.Ordinal))
            return;

        _lastLoggedSummary = summary;
        Plugin.Log?.LogInfo(
            $"[EventTrap] Loaded {_encounters.Count} encounter(s). step={_stepSeconds:F2}s.");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (ShouldSkipEventTrapDiskReload(scene.name))
            return;

        // Splash disk-cache and zone additive loads do not change the global anchor registry.
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
            EventTrapLoadedEncounter ev = _encounters[i];
            if (ev == null || ev.Registry == null || ev.Config == null)
                continue;

            if (!SceneFilterAllows(ev.Registry, fragScene, unityActive))
                continue;

            if (ev.AmbushDepleted)
                continue;

            float d = Vector2.Distance(p, ev.Anchor);
            float enterR = ev.SuspicionEnterR;

            if (d > ev.AmbushZoneR)
                ev.PendingAmbushSpawnAt = -1f;

            bool down = IsEventTrapKnockdown(pc);

            bool wasInside = ev.InsideSuspicionZone;
            ev.InsideSuspicionZone = d <= enterR;

            bool globalPhraseReady = Time.time >= _globalPhraseCooldownUntil;
            bool perAnchorReady = Time.time - ev.LastThoughtTime >= ev.ThoughtCooldown;
            bool cooldownReady = globalPhraseReady && perAnchorReady;
            bool onlyOnEnter = ev.Config.suspicionThoughtOnlyOnEnteringZone;
            if (ev.InsideSuspicionZone && cooldownReady)
            {
                if (!onlyOnEnter || !wasInside)
                {
                    if (TryShowSuspicionThought(ev, playerObj))
                    {
                        for (int j = 0; j < _encounters.Count; j++)
                        {
                            if (_encounters[j] != null)
                                _encounters[j].LastThoughtTime = Time.time;
                        }
                    }
                }
            }

            string typeTrim = ev.Config.spawnEnemyType != null ? ev.Config.spawnEnemyType.Trim() : string.Empty;
            bool canSpawnAmbush = ev.UsesPlayerRelativeAmbushSpawn || !string.IsNullOrEmpty(typeTrim);
            int spawnCap = ev.Config.maxAmbushSpawns;
            bool underSpawnCap = spawnCap <= 0 || ev.AmbushSpawnSuccessCount < spawnCap;
            bool allowAmbush = underSpawnCap && (!ev.Config.ambushOnce || !ev.LifetimeAmbushSpawned);

            if (ev.PendingAmbushSpawnAt > 0f && Time.time >= ev.PendingAmbushSpawnAt)
            {
                ev.PendingAmbushSpawnAt = -1f;
                float dSpawn = Vector2.Distance(p, ev.Anchor);
                if (allowAmbush &&
                    dSpawn <= ev.AmbushZoneR &&
                    canSpawnAmbush)
                {
                    int n = TrySpawnAmbush(ev, p);
                    if (n > 0)
                    {
                        if (ev.Config.ambushOnce)
                            ev.LifetimeAmbushSpawned = true;
                        RegisterAmbushSpawnSuccess(ev);
                    }
                    else if (n == 0)
                    {
                        Plugin.Log?.LogWarning(
                            $"[EventTrap] Delayed ambush spawned 0 enemies (type='{typeTrim}'). Check EnemyPrefabRegistry / prefab name.");
                    }
                }
                else if (dSpawn > ev.AmbushZoneR)
                {
                    Plugin.Log?.LogInfo("[EventTrap] Ambush timer fired but player left anchor zone — spawn skipped.");
                }
            }

            bool edgeIntoKo = down && !ev.PrevPlayerKnockdown;
            bool enteredAmbushWhileDown =
                down &&
                ev.PrevDistanceToAnchor > ev.AmbushZoneR &&
                d <= ev.AmbushZoneR;
            bool ambushTrigger = (edgeIntoKo || enteredAmbushWhileDown) && d <= ev.AmbushZoneR;

            ev.PrevPlayerKnockdown = down;
            ev.PrevDistanceToAnchor = d;

            if (allowAmbush &&
                ambushTrigger &&
                canSpawnAmbush)
            {
                float delaySec = ev.Config.ambushSpawnDelaySeconds;
                if (delaySec > 0.05f)
                {
                    if (ev.PendingAmbushSpawnAt <= 0f)
                    {
                        ev.PendingAmbushSpawnAt = Time.time + delaySec;
                        Plugin.Log?.LogInfo(
                            $"[EventTrap] Ambush scheduled in {delaySec:F1}s (player at {p.x:F1},{p.y:F1}).");
                    }
                }
                else
                {
                    int n = TrySpawnAmbush(ev, p);
                    if (n > 0)
                    {
                        if (ev.Config.ambushOnce)
                            ev.LifetimeAmbushSpawned = true;
                        RegisterAmbushSpawnSuccess(ev);
                    }
                }
            }
        }
    }

    private static void RegisterAmbushSpawnSuccess(EventTrapLoadedEncounter ev)
    {
        if (ev == null)
            return;

        ev.AmbushSpawnSuccessCount++;
        int cap = ev.Config != null ? ev.Config.maxAmbushSpawns : 0;
        if (cap > 0 && ev.AmbushSpawnSuccessCount >= cap)
        {
            ev.AmbushDepleted = true;
            ev.PendingAmbushSpawnAt = -1f;
            string folder = ev.Registry != null ? ev.Registry.folder : "?";
            Plugin.Log?.LogInfo(
                $"[EventTrap] Encounter '{folder}' reached maxAmbushSpawns={cap}; trigger disabled for this session.");
        }
    }

    private static bool TryShowSuspicionThought(EventTrapLoadedEncounter ev, GameObject playerObj)
    {
        if (ev.PhraseLines == null || ev.PhraseLines.Length == 0)
            return false;

        if (Time.time < _globalPhraseCooldownUntil)
            return false;

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
            Plugin.Log?.LogWarning("[EventTrap] DialogueDisplay not ready; suspicion line skipped.");
            return false;
        }

        string line = ev.PhraseLines[Random.Range(0, ev.PhraseLines.Length)];
        float dur = Mathf.Clamp(ev.Config.thoughtDurationSeconds, 0.5f, 30f);
        float vOff = ev.Config.thoughtVerticalOffsetPx;
        float phraseCooldown = ev.ThoughtCooldown;

        DialogueStyle style = DialogueDisplay.BuildAradiaThoughtStyle(vOff, 0f, true);
        display.ShowAradiaThought(playerObj, line, "hair1", style, dur, disableBoneFallbacks: true);
        ev.LastThoughtTime = Time.time;
        _globalPhraseCooldownUntil = Time.time + phraseCooldown;
        return true;
    }

    private static bool TryBuildTwinSidePack(
        EventTrapLoadedEncounter ev,
        Vector2 playerPlanar,
        string enemyType,
        string factionRaw,
        out SpawnConfigExecutor.RuntimeSpawnPoint[] pack)
    {
        float ox = ev.Config.ambushSideOffset > 0.01f ? ev.Config.ambushSideOffset : 1f;
        pack = new[]
        {
            new SpawnConfigExecutor.RuntimeSpawnPoint
            {
                Center = playerPlanar + new Vector2(-ox, 0f),
                EnemyType = enemyType,
                FactionIdRaw = factionRaw,
                EventCoreEventId = null,
                Count = 1
            },
            new SpawnConfigExecutor.RuntimeSpawnPoint
            {
                Center = playerPlanar + new Vector2(ox, 0f),
                EnemyType = enemyType,
                FactionIdRaw = factionRaw,
                EventCoreEventId = null,
                Count = 1
            }
        };
        return true;
    }

    private static bool TryBuildFlankPack(
        EventTrapLoadedEncounter ev,
        Vector2 playerPlanar,
        string enemyType,
        string factionRaw,
        out SpawnConfigExecutor.RuntimeSpawnPoint[] pack)
    {
        pack = new SpawnConfigExecutor.RuntimeSpawnPoint[0];
        EventTrapConfigFile c = ev.Config;
        int maxSide = c.flankAmbushPerSideMax;
        if (maxSide < 2)
            return false;

        int minSide = c.flankAmbushPerSideMin;
        if (minSide < 1)
            minSide = 2;
        if (minSide > maxSide)
            minSide = maxSide;

        int dMin = c.flankDistanceMin > 0 ? c.flankDistanceMin : 2;
        int dMax = c.flankDistanceMax > 0 ? c.flankDistanceMax : Mathf.Max(dMin, 4);
        if (dMax < dMin)
            dMax = dMin;

        int leftN = Random.Range(minSide, maxSide + 1);
        int rightN = Random.Range(minSide, maxSide + 1);

        var list = new List<SpawnConfigExecutor.RuntimeSpawnPoint>(leftN + rightN);

        for (int i = 0; i < leftN; i++)
        {
            float dist = Random.Range(dMin, dMax + 1);
            float yOff = leftN <= 1 ? 0f : (i - (leftN - 1) * 0.5f) * 0.35f;
            list.Add(new SpawnConfigExecutor.RuntimeSpawnPoint
            {
                Center = playerPlanar + new Vector2(-dist, yOff),
                EnemyType = enemyType,
                FactionIdRaw = factionRaw,
                EventCoreEventId = null,
                Count = 1
            });
        }

        for (int i = 0; i < rightN; i++)
        {
            float dist = Random.Range(dMin, dMax + 1);
            float yOff = rightN <= 1 ? 0f : (i - (rightN - 1) * 0.5f) * 0.35f;
            list.Add(new SpawnConfigExecutor.RuntimeSpawnPoint
            {
                Center = playerPlanar + new Vector2(dist, yOff),
                EnemyType = enemyType,
                FactionIdRaw = factionRaw,
                EventCoreEventId = null,
                Count = 1
            });
        }

        pack = list.ToArray();
        return pack.Length > 0;
    }

    private static int TrySpawnAmbush(EventTrapLoadedEncounter ev, Vector2 playerPlanar)
    {
        if (ev.UsesPlayerRelativeAmbushSpawn)
            return TrySpawnPlayerRelativeAmbush(ev, playerPlanar);

        string type = ev.Config.spawnEnemyType != null ? ev.Config.spawnEnemyType.Trim() : string.Empty;
        if (string.IsNullOrEmpty(type))
            return 0;

        string faction = ev.Config.spawnFactionIdRaw != null ? ev.Config.spawnFactionIdRaw.Trim() : string.Empty;

        EnemyPrefabRegistry.Initialize();

        if (TryBuildFlankPack(ev, playerPlanar, type, faction, out SpawnConfigExecutor.RuntimeSpawnPoint[] flank))
        {
            int n = SpawnConfigExecutor.SpawnRuntimePack(flank, "[EventTrap]");
            if (n > 0)
            {
                Plugin.Log?.LogInfo(
                    $"[EventTrap] Flank ambush '{type}' x{n} near player ({playerPlanar.x:F1},{playerPlanar.y:F1}).");
            }

            return n;
        }

        TryBuildTwinSidePack(ev, playerPlanar, type, faction, out SpawnConfigExecutor.RuntimeSpawnPoint[] twin);
        int pair = SpawnConfigExecutor.SpawnRuntimePack(twin, "[EventTrap]");
        if (pair > 0)
        {
            Plugin.Log?.LogInfo(
                $"[EventTrap] Twin ambush '{type}' x{pair} at ±offset near player ({playerPlanar.x:F1},{playerPlanar.y:F1}).");
        }

        return pair;
    }

    private static int TrySpawnPlayerRelativeAmbush(EventTrapLoadedEncounter ev, Vector2 playerPlanar)
    {
        if (ev.EnemyTypes == null || ev.EnemyTypes.Length == 0)
            return 0;
        if (ev.HorizontalDistances == null || ev.HorizontalDistances.Length == 0)
            return 0;

        int minC = Mathf.Max(1, ev.Config.spawnCountMin);
        int maxC = Mathf.Max(minC, ev.Config.spawnCountMax);
        int count = Random.Range(minC, maxC + 1);

        string faction = ev.Config.spawnFactionIdRaw != null ? ev.Config.spawnFactionIdRaw.Trim() : string.Empty;
        bool rightOnly = ev.Config.spawnRightOnly;

        EnemyPrefabRegistry.Initialize();

        var pack = new List<SpawnConfigExecutor.RuntimeSpawnPoint>(count);
        for (int i = 0; i < count; i++)
        {
            string enemyType = ev.EnemyTypes[Random.Range(0, ev.EnemyTypes.Length)];
            int dist = ev.HorizontalDistances[Random.Range(0, ev.HorizontalDistances.Length)];
            float sign = rightOnly ? 1f : (Random.value < 0.5f ? -1f : 1f);
            Vector2 pos = playerPlanar + new Vector2(sign * dist, 0f);

            pack.Add(new SpawnConfigExecutor.RuntimeSpawnPoint
            {
                Center = pos,
                EnemyType = enemyType,
                FactionIdRaw = string.IsNullOrEmpty(faction) ? null : faction,
                EventCoreEventId = null,
                Count = 1
            });
        }

        int spawned = SpawnConfigExecutor.SpawnRuntimePack(pack.ToArray(), "[EventTrap]");
        if (spawned > 0)
        {
            Plugin.Log?.LogInfo(
                $"[EventTrap] Player-relative ambush x{spawned} near ({playerPlanar.x:F1},{playerPlanar.y:F1}) [anchor '{ev.LogLabel}'].");
        }

        return spawned;
    }
}
