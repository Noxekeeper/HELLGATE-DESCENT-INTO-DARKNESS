using System;
using System.Collections;
using NoREroMod.Systems.EventCore.EventTrap;
using NoREroMod.Systems.EventCore.Reinforcement;
using NoREroMod.Systems.Pregnancy.Patches;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Sole HellGate zone spawn refresher: altar soft-respawn, location transitions, F11 hot reload.
/// Soft path = wipe enemies + managed → <c>fun_SpawnRE</c> → Execute pack for <see cref="GetActiveGameplayZone"/>.
/// Does not heal, clear BAD status, or rewrite <c>_re_Scenename</c> on walk.
/// </summary>
internal static class HellGateLocationSpawnRefresh
{
    private static string _lastRefreshedReScene = string.Empty;
    private static string _lastRefreshedLevelScene = string.Empty;
    private static string _lastSuccessfulRefreshZone = string.Empty;
    private static float _lastSuccessfulRefreshAt;
    private static bool _zoneRefreshInFlight;
    private static int _refreshEpoch;

    internal static bool IsZoneRefreshInFlight => _zoneRefreshInFlight;

    internal static bool IsRefreshEpochCurrent(int epoch) => epoch == _refreshEpoch;

    private const float DuplicateRefreshCooldownSeconds = 5f;

    /// <summary>
    /// Abort a batched pack mid-flight (e.g. before SpawnParent.Initialize Enemy wipe).
    /// </summary>
    internal static void CancelInFlightRefresh(string reason = null)
    {
        _refreshEpoch++;
        if (_zoneRefreshInFlight)
        {
            _zoneRefreshInFlight = false;
            if (!string.IsNullOrEmpty(reason))
                Plugin.Log?.LogInfo($"[LOCATION SPAWN] Cancelled in-flight refresh ({reason}).");
        }
    }

    internal static string GetReSceneName()
    {
        try
        {
            var fragMng = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetGameFragMng();
            if (fragMng != null && !string.IsNullOrEmpty(fragMng._re_Scenename))
                return fragMng._re_Scenename;
        }
        catch
        {
            // fall through
        }

        try
        {
            return SceneManager.GetActiveScene().name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Physical zone from walk transitions (<see cref="StaticMng.Idea_Nowscene"/>), else save checkpoint zone.
    /// </summary>
    internal static string GetActiveGameplayZone()
    {
        try
        {
            if (!string.IsNullOrEmpty(StaticMng.Idea_Nowscene))
                return StaticMng.Idea_Nowscene;
        }
        catch
        {
            // fall through
        }

        return GetReSceneName();
    }

    internal static void SyncCheckpointSceneName(string targetScene)
    {
        if (string.IsNullOrEmpty(targetScene))
            return;

        try
        {
            var fragMng = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetGameFragMng();
            if (fragMng != null)
                fragMng._re_Scenename = targetScene;
        }
        catch
        {
        }

        try
        {
            StaticMng.Re_Scenename = targetScene;
        }
        catch
        {
        }
    }

    internal static bool ShouldIgnoreSceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return true;

        if (sceneName.Equals("Gametitle", StringComparison.OrdinalIgnoreCase))
            return true;

        if (sceneName.Equals("Common", StringComparison.OrdinalIgnoreCase))
            return true;

        if (sceneName.Equals("EvInsomniaB1", StringComparison.OrdinalIgnoreCase))
            return true;
        if (sceneName.Equals("EVInsomniaBar", StringComparison.OrdinalIgnoreCase))
            return true;
        if (sceneName.Equals("EVInsomniaBarSP", StringComparison.OrdinalIgnoreCase))
            return true;
        if (sceneName.Equals("GoInsomnia", StringComparison.OrdinalIgnoreCase))
            return true;
        if (NoREroMod.Patches.Player.VanillaCutsceneSceneGuard.IsAltarExitInProgress())
            return true;

        return false;
    }

    /// <summary>
    /// Immediate wipe on zone change (door prefix). Full pack runs after load completes.
    /// </summary>
    internal static void QuickCleanupOnZoneChange(string fromScene, string toScene)
    {
        if (ShouldIgnoreSceneName(toScene))
            return;

        try
        {
            DestroyAllEnemies();
            SpawnConfigExecutor.CleanupManagedSpawns();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[LOCATION SPAWN] Immediate cleanup failed: {ex.Message}");
        }
    }

    /// <summary>True when the additive gameplay scene for <paramref name="zoneName"/> is loaded.</summary>
    internal static bool IsTargetZoneSceneLoaded(string zoneName)
    {
        if (string.IsNullOrEmpty(zoneName))
            return false;

        try
        {
            Scene scene = SceneManager.GetSceneByName(zoneName);
            if (scene.IsValid())
                return scene.isLoaded;
        }
        catch
        {
            // fall through
        }

        string loadedLevel = GetLoadedGameplayLevelScene();
        return !string.IsNullOrEmpty(loadedLevel)
            && !ShouldIgnoreSceneName(loadedLevel)
            && string.Equals(loadedLevel, zoneName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Called after <see cref="PlayerStatus.LoadSceneAndWait"/> finishes (fast travel, doors, etc.).
    /// Primary walk/teleport refresh entry.
    /// </summary>
    /// <param name="forceTakeover">
    /// When true (Post-Initialize), cancel any early/batched refresh so this pack owns the zone.
    /// </param>
    internal static void OnGameplaySceneLoadCompleted(string targetScene, bool forceTakeover = false)
    {
        if (ShouldIgnoreSceneName(targetScene))
            return;

        if (forceTakeover)
            CancelInFlightRefresh("post-init takeover");
        else if (SpawnParentInitializeGate.ShouldBlockControllerRefresh)
            return;

        if (ShouldSkipDuplicateRefresh(targetScene))
            return;

        if (!TryBeginZoneRefresh(targetScene, out int epoch))
            return;

        if (Plugin.Instance != null)
        {
            Plugin.Instance.StartCoroutine(RefreshOnLocationTransitionRoutine(targetScene, epoch));
            return;
        }

        try
        {
            RefreshOnLocationTransitionCore(targetScene);
        }
        finally
        {
            ReleaseZoneRefresh(epoch);
        }
    }

    /// <returns>True when HellGate spawn pack executed for the zone (sync path only).</returns>
    internal static bool RefreshOnLocationTransition(string sceneName)
    {
        if (ShouldIgnoreSceneName(sceneName))
            return false;

        if (SpawnParentInitializeGate.ShouldBlockControllerRefresh)
            return false;

        if (!TryBeginZoneRefresh(sceneName, out int epoch))
            return ShouldSkipDuplicateRefresh(sceneName);

        if (Plugin.Instance != null)
        {
            Plugin.Instance.StartCoroutine(RefreshOnLocationTransitionRoutine(sceneName, epoch));
            return false;
        }

        try
        {
            return RefreshOnLocationTransitionCore(sceneName);
        }
        finally
        {
            ReleaseZoneRefresh(epoch);
        }
    }

    internal static bool IsZoneRefreshPendingOrRecent(string sceneName) =>
        _zoneRefreshInFlight || WasZoneRefreshedRecently(sceneName);

    private static bool TryBeginZoneRefresh(string sceneName, out int epoch)
    {
        epoch = 0;
        if (ShouldIgnoreSceneName(sceneName))
            return false;

        if (_zoneRefreshInFlight)
            return false;

        if (ShouldSkipDuplicateRefresh(sceneName))
            return false;

        _zoneRefreshInFlight = true;
        epoch = _refreshEpoch;
        return true;
    }

    private static void ReleaseZoneRefresh(int epoch)
    {
        // Only the owner of the current epoch may clear the flag (cancelled routines no-op).
        if (epoch == _refreshEpoch)
            _zoneRefreshInFlight = false;
    }

    private static IEnumerator RefreshOnLocationTransitionRoutine(string sceneName, int epoch)
    {
        try
        {
            if (epoch != _refreshEpoch)
                yield break;

            string configPath = GetActiveSpawnConfigPath(sceneName);
            if (!string.IsNullOrEmpty(configPath))
            {
                yield return SpawnTemplateDiskCache.HydrateKeysForConfig(configPath);
                if (epoch != _refreshEpoch)
                    yield break;
                yield return EnemyPrefabDiskCache.HydrateKeysForConfig(configPath);
                if (epoch != _refreshEpoch)
                    yield break;
            }

            SpawnTemplateCatalog.CacheSceneIfLoaded(sceneName);
            string loadedLevel = GetLoadedGameplayLevelScene();
            if (!string.IsNullOrEmpty(loadedLevel)
                && !string.Equals(loadedLevel, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                SpawnTemplateCatalog.CacheSceneIfLoaded(loadedLevel);
            }

            EnemyPrefabRegistry.Initialize();
            // Do NOT call SyncCheckpointSceneName here. Walk transitions only update
            // Idea_Nowscene; _re_Scenename must stay on the last activated altar.

            if (ShouldIgnoreSceneName(sceneName))
                yield break;

            if (NoREroMod.Systems.Pregnancy.ShelterAttack.ShelterAttackSceneGuard.ShouldBlockParishZoneRefresh(sceneName))
                yield break;

            if (epoch != _refreshEpoch)
                yield break;

            ForceZoneSpawnReset($"zone \"{sceneName}\"");
            // Let Destroy() settle one frame before Instantiate flood.
            yield return null;
            if (epoch != _refreshEpoch)
                yield break;

            bool skipVanillaSpawnRe = ShouldSkipVanillaSpawnRe(sceneName);
            if (!skipVanillaSpawnRe && !TryRunVanillaSpawnRe())
            {
                Plugin.Log?.LogWarning("[LOCATION SPAWN] fun_SpawnRE skipped (SpawnParent missing).");
            }

            ReloadEventCoreAnchorsFromSpawnFiles(rediscoverFromSpawnFiles: false);
            HellGateHostageRuntime.ClearReservedSaveSlots();

            bool hadPack = HellGateSpawnSceneHints.TryResolvePackForZone(sceneName, out _, out _);
            if (hadPack)
            {
                yield return HellGateSpawnSceneHints.ExecutePackForZoneBatched(
                    sceneName, skipCleanup: true, batchPerFrame: 8, refreshEpoch: epoch);
                if (epoch != _refreshEpoch)
                    yield break;
                RememberSuccessfulRefresh(sceneName);
            }

            _lastRefreshedReScene = sceneName;
            _lastRefreshedLevelScene = GetLoadedGameplayLevelScene();
            RequestHideoutOffspringSpawn();
        }
        finally
        {
            ReleaseZoneRefresh(epoch);
        }
    }

    internal static void ForceZoneSpawnReset(string reason)
    {
        DestroyAllEnemies();
        SpawnConfigExecutor.CleanupManagedSpawns();
    }

    private static bool RefreshOnLocationTransitionCore(string sceneName)
    {
        if (ShouldIgnoreSceneName(sceneName))
            return false;

        if (NoREroMod.Systems.Pregnancy.ShelterAttack.ShelterAttackSceneGuard.ShouldBlockParishZoneRefresh(sceneName))
            return false;

        try
        {
            ForceZoneSpawnReset($"zone \"{sceneName}\"");

            bool skipVanillaSpawnRe = ShouldSkipVanillaSpawnRe(sceneName);
            if (!skipVanillaSpawnRe && !TryRunVanillaSpawnRe())
            {
                Plugin.Log?.LogWarning("[LOCATION SPAWN] fun_SpawnRE skipped (SpawnParent missing).");
            }

            ReloadEventCoreAnchorsFromSpawnFiles(rediscoverFromSpawnFiles: false);
            HellGateHostageRuntime.ClearReservedSaveSlots();
            bool spawned = RunHellGateSpawnForTargetZone(sceneName);
            if (spawned)
                RememberSuccessfulRefresh(sceneName);

            _lastRefreshedReScene = sceneName;
            _lastRefreshedLevelScene = GetLoadedGameplayLevelScene();

            RequestHideoutOffspringSpawn();

            return spawned;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[LOCATION SPAWN] Refresh failed: {ex.Message}");
            return false;
        }
    }

    internal static bool WasZoneRefreshedRecently(string sceneName) =>
        ShouldSkipDuplicateRefresh(sceneName);

    private static bool ShouldSkipDuplicateRefresh(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        return string.Equals(_lastSuccessfulRefreshZone, sceneName, StringComparison.OrdinalIgnoreCase)
            && Time.unscaledTime - _lastSuccessfulRefreshAt < DuplicateRefreshCooldownSeconds;
    }

    private static void RememberSuccessfulRefresh(string sceneName)
    {
        _lastSuccessfulRefreshZone = sceneName ?? string.Empty;
        _lastSuccessfulRefreshAt = Time.unscaledTime;
    }

    /// <summary>
    /// Vanilla SpawnParent.Initialize just wiped Enemy tags — allow a fresh HellGate pack.
    /// </summary>
    internal static void InvalidateDuplicateCooldown()
    {
        _lastSuccessfulRefreshZone = string.Empty;
        _lastSuccessfulRefreshAt = 0f;
    }

    /// <summary>
    /// Walk door/ladder between two gameplay zones: clear duplicate cooldown so a full pack can respawn.
    /// </summary>
    /// <returns>True when this is a real cross-zone transition (caller may destroy enemies).</returns>
    internal static bool NotifyCrossZoneWalkTransition(string fromZone, string toZone)
    {
        if (string.IsNullOrEmpty(toZone)
            || ShouldIgnoreSceneName(toZone)
            || string.Equals(fromZone, toZone, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _lastSuccessfulRefreshZone = string.Empty;
        _lastSuccessfulRefreshAt = 0f;
        return true;
    }

    /// <summary>After altar: vanilla already cleared enemies and ran fun_SpawnRE.</summary>
    internal static void RefreshAfterAltar()
    {
        try
        {
            string zone = GetActiveGameplayZone();
            if (NoREroMod.Systems.Pregnancy.ShelterAttack.ShelterAttackSceneGuard.ShouldBlockParishZoneRefresh(zone))
                return;

            ForceZoneSpawnReset($"altar reset (zone=\"{zone}\")");
            ReloadEventCoreAnchorsFromSpawnFiles(rediscoverFromSpawnFiles: true);
            EnemyPrefabRegistry.Initialize();
            bool spawned = RunHellGateSpawnForTargetZone(zone);

            if (!spawned)
            {
                Plugin.Log?.LogWarning(
                    $"[SPAWN ALTAR RESET] No HellGate spawn pack matched zone \"{zone}\".");
            }
            else
            {
                RememberSuccessfulRefresh(zone);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN ALTAR RESET] Failed to force respawn: {ex.Message}");
        }
        finally
        {
            OffspringHideoutHealth.RestoreAllHideoutResidentsToFull();
            RequestHideoutOffspringSpawn();
        }
    }

    internal static void TriggerSpawnEditHotReload()
    {
        if (Plugin.Instance != null)
        {
            Plugin.Instance.StartCoroutine(TriggerSpawnEditHotReloadRoutine());
            return;
        }

        TriggerSpawnEditHotReloadCore();
    }

    private static IEnumerator TriggerSpawnEditHotReloadRoutine()
    {
        string configPath = GetActiveSpawnConfigPath();
        if (!string.IsNullOrEmpty(configPath))
        {
            yield return SpawnTemplateDiskCache.HydrateKeysForConfig(configPath);
            yield return EnemyPrefabDiskCache.HydrateKeysForConfig(configPath);
        }

        string zone = GetActiveGameplayZone();
        SpawnTemplateCatalog.CacheSceneIfLoaded(zone);
        EnemyPrefabRegistry.Initialize();

        TriggerSpawnEditHotReloadCore();
    }

    private static void TriggerSpawnEditHotReloadCore()
    {
        try
        {
            DestroyAllEnemies();
            SpawnConfigExecutor.CleanupManagedSpawns();

            if (!TryRunVanillaSpawnRe())
            {
                Plugin.Log?.LogWarning("[SPAWN HOT RELOAD] Gamemng/SpawnParent not found; cannot run fun_SpawnRE.");
                return;
            }

            ReloadEventCoreAnchorsFromSpawnFiles(rediscoverFromSpawnFiles: true);
            EnemyPrefabRegistry.Initialize();
            HellGateHostageRuntime.ClearReservedSaveSlots();
            string zone = GetActiveGameplayZone();
            if (RunHellGateSpawnForTargetZone(zone))
                RememberSuccessfulRefresh(zone);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN HOT RELOAD] Failed: {ex.Message}");
        }
        finally
        {
            try
            {
                if (HideoutSceneUtility.IsParishHideoutActive())
                    OffspringHideoutHealth.RestoreAllHideoutResidentsToFull();
            }
            catch { }
        }
    }

    internal static string GetActiveSpawnConfigPath(string targetZone = null)
    {
        if (!HellGateSpawnSceneHints.TryResolvePackForZone(
                targetZone ?? GetActiveGameplayZone(),
                out string configPath,
                out _))
        {
            return string.Empty;
        }

        return configPath;
    }

    private static bool RunHellGateSpawnForTargetZone(string targetZone)
    {
        if (string.IsNullOrEmpty(targetZone))
            return HellGateSpawnSceneHints.ExecutePackForZone(GetActiveGameplayZone());

        return HellGateSpawnSceneHints.ExecutePackForZone(targetZone);
    }

    private static void DestroyAllEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null)
                continue;

            if (IsProtectedHideoutOffspring(enemy))
                continue;

            if (enemy.GetComponent<NoREroMod.Systems.Pregnancy.ShelterAttack.ShelterAttackEnemyMarker>() != null)
                continue;

            Object.Destroy(enemy);
        }
    }

    private static bool IsProtectedHideoutOffspring(GameObject enemy)
    {
        if (enemy.GetComponentInParent<WitchOffspringController>() != null)
            return true;

        return enemy.name.StartsWith("WitchOffspring_", StringComparison.Ordinal);
    }

    private static void RequestHideoutOffspringSpawn()
    {
        try
        {
            if (!NoREroMod.Systems.Pregnancy.PregnancyConfig.IsEnabled)
                return;

            if (!HideoutSceneUtility.IsParishHideoutActive())
                return;

            OffspringHideoutSpawner.RequestDeferredSpawn();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[Pregnancy.Hideout] Hideout spawn request failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Additive level scene (village_main, scapegoatEntrance, …), not the always-active Common shell.
    /// </summary>
    internal static string GetLoadedGameplayLevelScene()
    {
        try
        {
            string zone = GetActiveGameplayZone();
            if (!string.IsNullOrEmpty(zone) && !ShouldIgnoreSceneName(zone))
            {
                Scene byZone = SceneManager.GetSceneByName(zone);
                if (byZone.IsValid() && byZone.isLoaded)
                    return zone;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                string name = scene.name;
                if (ShouldIgnoreSceneName(name) || IsAuxiliaryLoadedScene(name))
                    continue;

                return name;
            }
        }
        catch
        {
            // fall through
        }

        return string.Empty;
    }

    private static bool IsAuxiliaryLoadedScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return true;

        return sceneName.Equals("GameoverAnime", StringComparison.OrdinalIgnoreCase)
            || sceneName.Equals("village", StringComparison.OrdinalIgnoreCase)
            || sceneName.Equals("miniGrave", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Only skip fun_SpawnRE when the same level file stays loaded but the gameplay zone id changed
    /// (sub-zone title change inside one additive scene). Cross-level walks must reset vanilla SpawnPoint[].
    /// </summary>
    private static bool ShouldSkipVanillaSpawnRe(string targetReScene)
    {
        if (string.IsNullOrEmpty(_lastRefreshedReScene) || string.IsNullOrEmpty(targetReScene))
            return false;

        if (string.Equals(_lastRefreshedReScene, targetReScene, StringComparison.OrdinalIgnoreCase))
            return false;

        string levelScene = GetLoadedGameplayLevelScene();
        if (string.IsNullOrEmpty(levelScene)
            || string.IsNullOrEmpty(_lastRefreshedLevelScene)
            || !string.Equals(levelScene, _lastRefreshedLevelScene, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return HellGateSpawnSceneHints.HasHellGatePack(_lastRefreshedReScene)
            && HellGateSpawnSceneHints.HasHellGatePack(targetReScene);
    }

    private static bool TryRunVanillaSpawnRe()
    {
        GameObject gameMng = GameObject.FindWithTag("Gamemng");
        if (gameMng == null)
            return false;

        SpawnParent spawnParent = gameMng.GetComponent<SpawnParent>();
        if (spawnParent == null)
            return false;

        spawnParent.fun_SpawnRE();
        return true;
    }

    private static void ReloadEventCoreAnchorsFromSpawnFiles(bool rediscoverFromSpawnFiles)
    {
        try
        {
            if (rediscoverFromSpawnFiles)
            {
                ReinforcementEncounterLoader.InvalidateSpawnDiscoveryCache();
                EventTrapEncounterLoader.InvalidateSpawnDiscoveryCache();
            }

            if (Plugin.Instance == null)
                return;

            ReinforcementEncounterDriver reinforcement = Plugin.Instance.GetComponent<ReinforcementEncounterDriver>();
            if (reinforcement != null)
                reinforcement.ReloadFromDisk(rediscoverFromSpawnFiles);

            EventTrapEncounterDriver eventTrap = Plugin.Instance.GetComponent<EventTrapEncounterDriver>();
            if (eventTrap != null)
                eventTrap.ReloadFromDisk(rediscoverFromSpawnFiles);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[LOCATION SPAWN] EventCore anchor reload failed: {ex.Message}");
        }
    }
}
