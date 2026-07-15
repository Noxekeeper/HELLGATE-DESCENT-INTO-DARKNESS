using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Persists scene-locked boss / elite enemy prefab keys (e.g. BossTouzoku in village_main).
/// </summary>
internal static class EnemyPrefabDiskCache
{
    internal sealed class Entry
    {
        public string Key = string.Empty;
        public string Scene = string.Empty;
        public string Component = string.Empty;
        public string ObjectName = string.Empty;
    }

    private const string FileName = "ENEMY_PREFAB_DISK_CACHE.txt";

    private static readonly Dictionary<string, Entry> entries =
        new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> hydratedScenes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static ConfigEntry<bool> enabledConfig;

    internal static void BindConfig(Plugin plugin)
    {
        enabledConfig = plugin.Config.Bind(
            "SpawnTemplates",
            "EnableEnemyPrefabDiskCache",
            true,
            "Save discovered boss/scene-locked enemy keys to ENEMY_PREFAB_DISK_CACHE.txt and restore them on demand.");
    }

    internal static void LoadFromDisk()
    {
        entries.Clear();
        if (enabledConfig != null && !enabledConfig.Value)
            return;

        try
        {
            string path = GetCachePath();
            if (!File.Exists(path))
            {
                SeedDefaults();
                return;
            }

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                Entry entry = ParseLine(lines[i]);
                if (entry == null || string.IsNullOrEmpty(entry.Key))
                    continue;

                entries[entry.Key] = entry;
            }

            if (entries.Count == 0)
                SeedDefaults();
            else
                Plugin.Log?.LogInfo($"[ENEMY DISK CACHE] Loaded {entries.Count} key(s) from {FileName}.");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[ENEMY DISK CACHE] Failed to read cache: {ex.Message}");
        }
    }

    internal static void SchedulePreload(Plugin plugin)
    {
        if (plugin == null || entries.Count == 0)
            return;
        if (enabledConfig != null && !enabledConfig.Value)
            return;

        plugin.StartCoroutine(PreloadBossScenesCoroutine());
    }

    private static IEnumerator PreloadBossScenesCoroutine()
    {
        yield return null;
        yield return null;

        var scenes = new List<string>();
        var sceneSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, Entry> pair in entries)
        {
            Entry entry = pair.Value;
            if (string.IsNullOrEmpty(entry.Scene))
                continue;

            if (sceneSet.Add(entry.Scene))
                scenes.Add(entry.Scene);
        }

        if (scenes.Count == 0)
            yield break;

        Plugin.Log?.LogInfo(
            $"[ENEMY DISK CACHE] Splash preload: {scenes.Count} scene(s), {entries.Count} boss key(s).");

        SpawnCacheWeatherGuard.BeginHydrateBatch();
        try
        {
            for (int i = 0; i < scenes.Count; i++)
            {
                string sceneName = scenes[i];
                if (hydratedScenes.Contains(sceneName))
                    continue;

                yield return LoadSceneCaptureAndMaybeUnload(sceneName, string.Empty);
                hydratedScenes.Add(sceneName);
            }
        }
        finally
        {
            SpawnCacheWeatherGuard.EndHydrateBatch();
        }
    }

    internal static bool HasDiskEntry(string key)
    {
        return TryGetDiskEntry(key, out _);
    }

    private static bool TryGetDiskEntry(string key, out Entry entry)
    {
        entry = null;
        if (string.IsNullOrEmpty(key))
            return false;

        key = NormalizeKey(key);
        if (entries.TryGetValue(key, out entry))
            return true;

        if (string.Equals(key, "BossTouzokuCustom", StringComparison.OrdinalIgnoreCase) &&
            entries.TryGetValue("BossTouzoku", out entry))
        {
            return true;
        }

        return false;
    }

    internal static void RecordBoss(string key, GameObject source, string sceneName)
    {
        if (enabledConfig != null && !enabledConfig.Value)
            return;
        if (string.IsNullOrEmpty(key) || source == null)
            return;

        string normalizedKey = NormalizeKey(key);
        if (string.IsNullOrEmpty(normalizedKey))
            return;

        Entry entry = new Entry
        {
            Key = normalizedKey,
            Scene = NormalizeSceneName(sceneName),
            Component = source.GetComponent<EnemyDate>()?.GetType().Name ?? string.Empty,
            ObjectName = CleanName(source.name)
        };

        if (entries.TryGetValue(normalizedKey, out Entry existing) && EntriesEqual(existing, entry))
            return;

        entries[normalizedKey] = entry;
        SaveToDisk();
    }

    internal static IEnumerator HydrateKeysForConfig(string configPath)
    {
        if (enabledConfig != null && !enabledConfig.Value)
            yield break;
        if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            yield break;
        if (entries.Count == 0)
            yield break;

        var neededKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectBossKeysFromConfig(configPath, neededKeys);
        if (neededKeys.Count == 0)
            yield break;

        // Warm cache: skip additive scene loads (main hitch source on re-entry).
        if (EnemyKeysSatisfied(neededKeys))
            yield break;

        var scenes = new List<string>();
        var sceneSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in neededKeys)
        {
            if (!TryGetDiskEntry(key, out Entry entry))
                continue;

            if (string.IsNullOrEmpty(entry.Scene))
                continue;

            if (sceneSet.Add(entry.Scene))
                scenes.Add(entry.Scene);
        }

        if (scenes.Count == 0)
            yield break;

        // Protect the physical gameplay zone (Idea_Nowscene), not the altar checkpoint scene.
        string gameplayScene = HellGateLocationSpawnRefresh.GetActiveGameplayZone();
        if (string.IsNullOrEmpty(gameplayScene))
            gameplayScene = HellGateLocationSpawnRefresh.GetLoadedGameplayLevelScene();
        if (string.IsNullOrEmpty(gameplayScene))
            gameplayScene = SceneManager.GetActiveScene().name;

        Plugin.Log?.LogInfo(
            $"[ENEMY DISK CACHE] Hydrate {neededKeys.Count} boss key(s) via {scenes.Count} scene(s) for {Path.GetFileName(configPath)}.");

        // Prefer NoRSceneLoader assets first — Spawnenemy refs survive splash unload.
        EnemyPrefabRegistry.RefreshFromLoadedScenes();

        SpawnCacheWeatherGuard.BeginHydrateBatch();
        try
        {
            for (int i = 0; i < scenes.Count; i++)
            {
                string sceneName = scenes[i];
                if (hydratedScenes.Contains(sceneName)
                    && EnemyKeysSatisfied(neededKeys))
                    continue;

                yield return LoadSceneCaptureAndMaybeUnload(sceneName, gameplayScene);
                hydratedScenes.Add(sceneName);
            }
        }
        finally
        {
            SpawnCacheWeatherGuard.EndHydrateBatch();
        }

        EnemyPrefabRegistry.RefreshFromLoadedScenes();
    }

    private static bool EnemyKeysSatisfied(HashSet<string> neededKeys)
    {
        foreach (string key in neededKeys)
        {
            if (string.Equals(key, "BossTouzokuCustom", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "HellishTouzokuBoss", StringComparison.OrdinalIgnoreCase))
            {
                if (!EnemyPrefabRegistry.TryGetPrefab("BossTouzoku", out _))
                    return false;
                continue;
            }

            if (!EnemyPrefabRegistry.TryGetPrefab(key, out GameObject prefab)
                || !EnemyPrefabRegistry.PrefabMatchesConfigKey(key, prefab))
                return false;
        }

        return true;
    }

    private static IEnumerator LoadSceneCaptureAndMaybeUnload(string sceneName, string gameplayScene)
    {
        if (string.IsNullOrEmpty(sceneName))
            yield break;

        bool loadedHere = false;
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            try
            {
                Plugin.Log?.LogInfo($"[ENEMY DISK CACHE] On-demand load: {sceneName}");
                SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
                loadedHere = true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[ENEMY DISK CACHE] Failed to load \"{sceneName}\": {ex.Message}");
                yield break;
            }

            for (int wait = 0; wait < 120; wait++)
            {
                scene = SceneManager.GetSceneByName(sceneName);
                if (scene.IsValid() && scene.isLoaded)
                    break;
                yield return null;
            }
        }

        if (!scene.IsValid() || !scene.isLoaded)
            yield break;

        SpawnCacheWeatherGuard.OnAdditiveSceneLoaded(scene);
        EnemyPrefabRegistry.CacheBossesFromScene(scene);

        if (loadedHere && !ShouldKeepHydrateSceneLoaded(sceneName, gameplayScene))
        {
            try
            {
                SceneManager.UnloadScene(sceneName);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[ENEMY DISK CACHE] Failed to unload \"{sceneName}\": {ex.Message}");
            }

            SpawnCacheWeatherGuard.OnAdditiveSceneUnloaded();
            yield return null;
        }
    }

    /// <summary>
    /// Never unload the player's current zone / loaded level — hydrate used to compare against
    /// altar <c>_re_Scenename</c> and could unload InundergroundChurch right before spawn.
    /// </summary>
    private static bool ShouldKeepHydrateSceneLoaded(string sceneName, string gameplayScene)
    {
        if (string.IsNullOrEmpty(sceneName))
            return true;

        if (!string.IsNullOrEmpty(gameplayScene)
            && string.Equals(sceneName, gameplayScene, StringComparison.OrdinalIgnoreCase))
            return true;

        string zone = HellGateLocationSpawnRefresh.GetActiveGameplayZone();
        if (!string.IsNullOrEmpty(zone)
            && string.Equals(sceneName, zone, StringComparison.OrdinalIgnoreCase))
            return true;

        string level = HellGateLocationSpawnRefresh.GetLoadedGameplayLevelScene();
        if (!string.IsNullOrEmpty(level)
            && string.Equals(sceneName, level, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static void CollectBossKeysFromConfig(string configPath, HashSet<string> keys)
    {
        string[] lines = File.ReadAllLines(configPath);
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
                continue;

            if (TryParseEnemyKey(trimmed, out string enemyKey) &&
                (HasDiskEntry(enemyKey)
                 || string.Equals(enemyKey, "BossTouzokuCustom", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(enemyKey, "Slaughterer", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(enemyKey, "Butcher", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(enemyKey, "Sisterknight", StringComparison.OrdinalIgnoreCase)))
            {
                keys.Add(enemyKey);
                // Ensure disk entry exists so hydrate knows which scene to load.
                if (!HasDiskEntry(enemyKey)
                    && (string.Equals(enemyKey, "Slaughterer", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(enemyKey, "Butcher", StringComparison.OrdinalIgnoreCase)))
                {
                    entries[NormalizeKey(enemyKey)] = new Entry
                    {
                        Key = NormalizeKey(enemyKey),
                        Scene = "UndergroundChurch",
                        Component = "Slaughterer",
                        ObjectName = "Slaughterer"
                    };
                    SaveToDisk();
                }
                else if (!HasDiskEntry(enemyKey)
                         && string.Equals(enemyKey, "Sisterknight", StringComparison.OrdinalIgnoreCase))
                {
                    entries["Sisterknight"] = new Entry
                    {
                        Key = "Sisterknight",
                        Scene = "InundergroundChurch",
                        Component = "Sisterknight",
                        ObjectName = "Sisterknight"
                    };
                    SaveToDisk();
                }
            }
        }
    }

    private static bool TryParseEnemyKey(string line, out string enemyKey)
    {
        enemyKey = string.Empty;
        if (SpawnConfigExecutor.TryParseEnemyTypeFromSpawnLine(line, out enemyKey))
            return !string.IsNullOrEmpty(enemyKey);

        return false;
    }

    private static Entry ParseLine(string line)
    {
        line = line.Trim();
        if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
            return null;

        string[] parts = line.Split('|');
        if (parts.Length < 2)
            return null;

        return new Entry
        {
            Key = parts[0].Trim(),
            Scene = parts.Length > 1 ? parts[1].Trim() : string.Empty,
            Component = parts.Length > 2 ? parts[2].Trim() : string.Empty,
            ObjectName = parts.Length > 3 ? parts[3].Trim() : string.Empty
        };
    }

    private static void SeedDefaults()
    {
        entries["BossTouzoku"] = new Entry
        {
            Key = "BossTouzoku",
            Scene = "BOSS_Touzoku",
            Component = "BossTouzoku",
            ObjectName = "BossTouzoku"
        };
        entries["BossTouzokuCustom"] = new Entry
        {
            Key = "BossTouzokuCustom",
            Scene = "BOSS_Touzoku",
            Component = "BossTouzoku",
            ObjectName = "BossTouzoku"
        };
        // Slaughterer / Butcher — often missing after walk into InundergroundChurch if never cached.
        entries["Slaughterer"] = new Entry
        {
            Key = "Slaughterer",
            Scene = "UndergroundChurch",
            Component = "Slaughterer",
            ObjectName = "Slaughterer"
        };
        entries["Butcher"] = new Entry
        {
            Key = "Butcher",
            Scene = "UndergroundChurch",
            Component = "Slaughterer",
            ObjectName = "Slaughterer"
        };
        entries["Sisterknight"] = new Entry
        {
            Key = "Sisterknight",
            Scene = "InundergroundChurch",
            Component = "Sisterknight",
            ObjectName = "Sisterknight"
        };
        SaveToDisk();
        Plugin.Log?.LogInfo($"[ENEMY DISK CACHE] Seeded default boss entries ({FileName}).");
    }

    private static void SaveToDisk()
    {
        try
        {
            string path = GetCachePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);

            using (StreamWriter writer = new StreamWriter(path, false))
            {
                writer.WriteLine("# ConfigKey|Scene|Component|ObjectName");
                foreach (KeyValuePair<string, Entry> pair in entries)
                {
                    Entry entry = pair.Value;
                    writer.WriteLine(
                        entry.Key + "|" +
                        entry.Scene + "|" +
                        entry.Component + "|" +
                        entry.ObjectName);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[ENEMY DISK CACHE] Failed to write cache: {ex.Message}");
        }
    }

    private static string GetCachePath()
    {
        string spawnDir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "HellGateSpawnPoint");
        return Path.Combine(spawnDir, FileName);
    }

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrEmpty(key) ? string.Empty : key.Trim();
    }

    private static string NormalizeSceneName(string sceneName)
    {
        return string.IsNullOrEmpty(sceneName) ? string.Empty : sceneName.Trim();
    }

    private static string CleanName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        int cloneIndex = name.IndexOf("(Clone)", StringComparison.Ordinal);
        if (cloneIndex >= 0)
            name = name.Substring(0, cloneIndex);

        return name.Trim();
    }

    private static bool EntriesEqual(Entry a, Entry b)
    {
        return string.Equals(a.Key, b.Key, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(a.Scene, b.Scene, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(a.Component, b.Component, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(a.ObjectName, b.ObjectName, StringComparison.OrdinalIgnoreCase);
    }
}
