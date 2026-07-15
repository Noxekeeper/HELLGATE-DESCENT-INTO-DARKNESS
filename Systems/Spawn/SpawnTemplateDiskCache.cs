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
/// Persists discovered spawn template keys to disk and restores them after leaving the title menu.
/// </summary>
internal static class SpawnTemplateDiskCache
{
    internal sealed class Entry
    {
        public string Key = string.Empty;
        public string Scene = string.Empty;
        public string Component = string.Empty;
        public string ObjectName = string.Empty;
        public string Prefix = "Trap";
        public float SpawnZ;
    }

    private const string FileName = "SPAWN_TEMPLATE_DISK_CACHE.txt";
    private const string ResourcesSceneToken = "__resources__";

    private static readonly Dictionary<string, Entry> entries =
        new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

    private static ConfigEntry<bool> enabledConfig;
    private static ConfigEntry<bool> splashPreloadConfig;
    private static bool restoreScheduled;
    private static bool restoreComplete;
    private static bool splashPreloadStarted;
    private static bool splashPreloadComplete;
    private static readonly HashSet<string> hydratedScenes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    internal static bool RestoreComplete => restoreComplete;

    /// <summary>True when splash background preload was never started or has finished.</summary>
    internal static bool IsSplashPreloadFinished =>
        !splashPreloadStarted || splashPreloadComplete;

    internal static bool HasDiskEntry(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        return entries.ContainsKey(SpawnTemplateCatalog.NormalizeTemplateKey(key));
    }

    internal static void BindConfig(Plugin plugin)
    {
        enabledConfig = plugin.Config.Bind(
            "SpawnTemplates",
            "EnablePersistentCache",
            true,
            "Save discovered spawn keys to SPAWN_TEMPLATE_DISK_CACHE.txt and restore them on next launch (after leaving title menu).");

        splashPreloadConfig = plugin.Config.Bind(
            "SpawnTemplates",
            "PreloadDiskCacheDuringSplash",
            true,
            "While the HELLGATE disclaimer/splash is visible, preload spawn template scenes in the background so gameplay entry does not hitch.");

        plugin.Config.Bind(
            "SpawnTemplates",
            "WhitelistSceneLoad",
            false,
            "Deprecated — use persistent disk cache instead. Additive whitelist scene load breaks Gametitle and is off by default.");
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
                return;

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                Entry entry = ParseLine(line);
                if (entry == null || string.IsNullOrEmpty(entry.Key))
                    continue;

                entries[entry.Key] = entry;
            }

            Plugin.Log?.LogInfo($"[SPAWN DISK CACHE] Loaded {entries.Count} key(s) from {FileName}.");
            if (entries.Count == 0)
                SeedFromWhitelistHints();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN DISK CACHE] Failed to read cache: {ex.Message}");
        }
    }

    internal static void RecordTemplate(string key, GameObject source, string prefix, string sceneName, float spawnZ)
    {
        if (enabledConfig != null && !enabledConfig.Value)
            return;
        if (string.IsNullOrEmpty(key) || source == null)
            return;

        string normalizedKey = SpawnTemplateCatalog.NormalizeTemplateKey(key);
        if (string.IsNullOrEmpty(normalizedKey))
            return;

        Entry entry = new Entry
        {
            Key = normalizedKey,
            Scene = NormalizeSceneName(sceneName),
            Component = DetectPrimaryComponent(source, prefix),
            ObjectName = CleanName(source.name),
            Prefix = string.IsNullOrEmpty(prefix) ? "Trap" : prefix,
            SpawnZ = spawnZ
        };

        if (entries.TryGetValue(normalizedKey, out Entry existing) && EntriesEqual(existing, entry))
            return;

        entries[normalizedKey] = entry;
        SaveToDisk();
    }

    internal static void ScheduleRestore(Plugin plugin)
    {
        if (plugin == null || restoreScheduled)
            return;
        if (enabledConfig != null && !enabledConfig.Value)
        {
            restoreComplete = true;
            return;
        }
        if (entries.Count == 0)
        {
            restoreComplete = true;
            return;
        }

        restoreScheduled = true;
        plugin.StartCoroutine(RestoreAfterGameplay());
    }

    internal static void ScheduleSplashPreload(Plugin plugin)
    {
        if (plugin == null || splashPreloadStarted)
            return;
        if (enabledConfig != null && !enabledConfig.Value)
            return;
        if (splashPreloadConfig != null && !splashPreloadConfig.Value)
            return;
        if (entries.Count == 0)
            return;

        splashPreloadStarted = true;
        plugin.StartCoroutine(SplashPreloadCoroutine());
    }

    private static IEnumerator SplashPreloadCoroutine()
    {
        yield return null;
        yield return null;

        var scenes = new List<string>();
        var sceneSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, Entry> pair in entries)
        {
            Entry entry = pair.Value;
            if (string.IsNullOrEmpty(entry.Scene) ||
                string.Equals(entry.Scene, ResourcesSceneToken, StringComparison.OrdinalIgnoreCase))
                continue;

            if (sceneSet.Add(entry.Scene))
                scenes.Add(entry.Scene);
        }

        Plugin.Log?.LogInfo(
            $"[SPAWN DISK CACHE] Splash preload: {scenes.Count} scene(s), {entries.Count} disk key(s) while disclaimer is visible.");

        int restoredResources = 0;
        SpawnCacheWeatherGuard.BeginHydrateBatch();
        try
        {
            foreach (KeyValuePair<string, Entry> pair in entries)
            {
                Entry entry = pair.Value;
                if (string.IsNullOrEmpty(entry.Scene) ||
                    !string.Equals(entry.Scene, ResourcesSceneToken, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (SpawnTemplateCatalog.HasTemplate(entry.Key))
                {
                    restoredResources++;
                    continue;
                }

                if (SpawnTemplateCatalog.TryCacheFromResources(entry.Key))
                    restoredResources++;
            }

            for (int i = 0; i < scenes.Count; i++)
            {
                string sceneName = scenes[i];
                if (hydratedScenes.Contains(sceneName))
                    continue;

                yield return LoadSceneCacheAndMaybeUnload(sceneName, string.Empty);
                hydratedScenes.Add(sceneName);
            }

            SpawnTemplateCatalog.RefreshAliasesAndDump();
        }
        finally
        {
            SpawnCacheWeatherGuard.EndHydrateBatch();
        }

        splashPreloadComplete = true;
        restoreComplete = true;
        Plugin.Log?.LogInfo(
            $"[SPAWN DISK CACHE] Splash preload complete. ready={SpawnTemplateCatalog.TemplateCount}, resources~={restoredResources}.");
    }

    private static IEnumerator RestoreAfterGameplay()
    {
        if (splashPreloadConfig != null && splashPreloadConfig.Value)
        {
            while (!splashPreloadStarted && IsWaitingForTitleScreen())
                yield return null;

            while (splashPreloadStarted && !splashPreloadComplete)
                yield return null;

            if (splashPreloadComplete)
                yield break;
        }

        while (IsWaitingForTitleScreen())
            yield return null;

        yield return null;

        int restored = 0;
        foreach (KeyValuePair<string, Entry> pair in entries)
        {
            Entry entry = pair.Value;
            if (string.IsNullOrEmpty(entry.Scene) ||
                !string.Equals(entry.Scene, ResourcesSceneToken, StringComparison.OrdinalIgnoreCase))
                continue;

            if (SpawnTemplateCatalog.HasTemplate(entry.Key))
            {
                restored++;
                continue;
            }

            if (SpawnTemplateCatalog.TryCacheFromResources(entry.Key))
                restored++;
        }

        SpawnTemplateCatalog.RefreshAliasesAndDump();
        restoreComplete = true;
        Plugin.Log?.LogInfo(
            $"[SPAWN DISK CACHE] Bootstrap complete (Resources only, {restored} key(s)). " +
            "Map scenes load on demand when a spawn pack needs them.");
    }

    internal static IEnumerator HydrateKeysForConfig(string configPath)
    {
        if (enabledConfig != null && !enabledConfig.Value)
            yield break;
        if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            yield break;

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        SpawnConfigExecutor.CollectTemplateKeysFromConfig(configPath, keys);
        if (keys.Count == 0)
            yield break;

        var scenes = new List<string>();
        var sceneSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in keys)
        {
            if (entries.TryGetValue(key, out Entry entry))
            {
                if (string.IsNullOrEmpty(entry.Scene) ||
                    string.Equals(entry.Scene, ResourcesSceneToken, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (sceneSet.Add(entry.Scene))
                    scenes.Add(entry.Scene);
                continue;
            }

            string[] decorScenes = SpawnDecorCatalog.GetScenesForKey(key);
            for (int i = 0; i < decorScenes.Length; i++)
            {
                string decorScene = decorScenes[i];
                if (string.IsNullOrEmpty(decorScene))
                    continue;
                if (sceneSet.Add(decorScene))
                    scenes.Add(decorScene);
            }
        }

        if (scenes.Count == 0)
            yield break;

        if (splashPreloadComplete)
        {
            foreach (string key in keys)
            {
                if (!entries.TryGetValue(key, out Entry entry))
                    continue;

                if (!string.Equals(entry.Scene, ResourcesSceneToken, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!SpawnTemplateCatalog.HasTemplate(key))
                    SpawnTemplateCatalog.TryCacheFromResources(key);
            }

            SpawnTemplateCatalog.RefreshAliasesAndDump();
            yield break;
        }

        // Warm path: templates already cached and source scenes already hydrated — skip loads.
        bool allTemplatesReady = true;
        foreach (string key in keys)
        {
            if (!SpawnTemplateCatalog.HasTemplate(key))
            {
                allTemplatesReady = false;
                break;
            }
        }

        if (allTemplatesReady)
        {
            bool allScenesHydrated = true;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (!hydratedScenes.Contains(scenes[i]))
                {
                    allScenesHydrated = false;
                    break;
                }
            }

            if (allScenesHydrated)
                yield break;
        }

        string gameplayScene = HellGateLocationSpawnRefresh.GetActiveGameplayZone();
        if (string.IsNullOrEmpty(gameplayScene))
            gameplayScene = HellGateLocationSpawnRefresh.GetLoadedGameplayLevelScene();
        if (string.IsNullOrEmpty(gameplayScene))
            gameplayScene = SceneManager.GetActiveScene().name;

        Plugin.Log?.LogInfo(
            $"[SPAWN DISK CACHE] Hydrate {keys.Count} key(s) via {scenes.Count} scene(s) for {Path.GetFileName(configPath)}.");

        SpawnCacheWeatherGuard.BeginHydrateBatch();
        try
        {
            for (int i = 0; i < scenes.Count; i++)
            {
                string sceneName = scenes[i];
                if (hydratedScenes.Contains(sceneName))
                    continue;

                yield return LoadSceneCacheAndMaybeUnload(sceneName, gameplayScene);
                hydratedScenes.Add(sceneName);
            }

            foreach (string key in keys)
            {
                if (!entries.TryGetValue(key, out Entry entry))
                    continue;

                if (!string.Equals(entry.Scene, ResourcesSceneToken, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!SpawnTemplateCatalog.HasTemplate(key))
                    SpawnTemplateCatalog.TryCacheFromResources(key);
            }

            SpawnTemplateCatalog.RefreshAliasesAndDump();
        }
        finally
        {
            SpawnCacheWeatherGuard.EndHydrateBatch();
        }
    }

    private static IEnumerator LoadSceneCacheAndMaybeUnload(string sceneName, string gameplayScene)
    {
        if (string.IsNullOrEmpty(sceneName))
            yield break;

        bool loadedHere = false;
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            try
            {
                Plugin.Log?.LogInfo($"[SPAWN DISK CACHE] On-demand load: {sceneName}");
                SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
                loadedHere = true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[SPAWN DISK CACHE] Failed to load \"{sceneName}\": {ex.Message}");
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
        SpawnTemplateCatalog.CacheFromSceneForDiskRestore(scene);

        if (loadedHere && !ShouldKeepHydrateSceneLoaded(sceneName, gameplayScene))
        {
            try
            {
                SceneManager.UnloadScene(sceneName);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[SPAWN DISK CACHE] Failed to unload \"{sceneName}\": {ex.Message}");
            }

            SpawnCacheWeatherGuard.OnAdditiveSceneUnloaded();
            yield return null;
        }
    }

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

    private static Entry ParseLine(string line)
    {
        string[] parts = line.Split('|');
        if (parts.Length < 2)
            return null;

        Entry entry = new Entry
        {
            Key = parts[0].Trim(),
            Scene = parts.Length > 1 ? parts[1].Trim() : string.Empty,
            Component = parts.Length > 2 ? parts[2].Trim() : string.Empty,
            ObjectName = parts.Length > 3 ? parts[3].Trim() : string.Empty,
            Prefix = parts.Length > 4 && !string.IsNullOrEmpty(parts[4].Trim()) ? parts[4].Trim() : "Trap"
        };

        if (parts.Length > 5)
        {
            float z;
            if (float.TryParse(parts[5].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out z))
                entry.SpawnZ = z;
        }

        return entry;
    }

    private static void SaveToDisk()
    {
        try
        {
            string path = GetCachePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            List<string> keys = new List<string>(entries.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);

            using (StreamWriter writer = new StreamWriter(path, false))
            {
                writer.WriteLine("# HellGate persistent spawn template cache (auto-generated)");
                writer.WriteLine("# Format: key|scene|component|objectName|prefix|spawnZ");
                writer.WriteLine("# scene=__resources__ means Resources scan (no map load)");
                writer.WriteLine("# Resources bootstrap at launch; map scenes load per spawn pack on demand.");
                writer.WriteLine("# Count: " + keys.Count);
                writer.WriteLine();

                for (int i = 0; i < keys.Count; i++)
                {
                    Entry entry = entries[keys[i]];
                    writer.WriteLine(string.Join("|", new[]
                    {
                        entry.Key,
                        entry.Scene,
                        entry.Component,
                        entry.ObjectName,
                        entry.Prefix,
                        entry.SpawnZ.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    }));
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN DISK CACHE] Failed to write cache: {ex.Message}");
        }
    }

    private static string GetCachePath()
    {
        string spawnDir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "HellGateSpawnPoint");
        return Path.Combine(spawnDir, FileName);
    }

    private static string NormalizeSceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return ResourcesSceneToken;
        return sceneName.Trim();
    }

    private static string CleanName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;
        return name.Replace("(Clone)", string.Empty).Trim();
    }

    private static bool EntriesEqual(Entry left, Entry right)
    {
        return left != null && right != null &&
               string.Equals(left.Key, right.Key, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.Scene, right.Scene, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.Component, right.Component, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.ObjectName, right.ObjectName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.Prefix, right.Prefix, StringComparison.OrdinalIgnoreCase) &&
               Mathf.Approximately(left.SpawnZ, right.SpawnZ);
    }

    private static string DetectPrimaryComponent(GameObject source, string prefix)
    {
        if (source == null)
            return string.Empty;

        if (string.Equals(prefix, "Hostage", StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < SpawnTemplateCatalog.HostageMobScriptTypeNames.Length; i++)
            {
                string typeName = SpawnTemplateCatalog.HostageMobScriptTypeNames[i];
                Type type = SpawnTemplateCatalog.ResolveMobScriptType(typeName);
                if (type != null && source.GetComponentInChildren(type, true) != null)
                    return typeName;
            }

            if (source.GetComponent<SpawnSlave>() != null)
                return "SpawnSlave";

            if (source.GetComponentInChildren<SpawnSlave>(true) != null)
                return "SpawnSlave";
        }

        Trapdata trapdata = source.GetComponentInChildren<Trapdata>(true);
        if (trapdata != null)
            return trapdata.GetType().Name;

        for (int i = 0; i < SpawnTemplateCatalog.TrapLikeComponentTypeNames.Length; i++)
        {
            string typeName = SpawnTemplateCatalog.TrapLikeComponentTypeNames[i];
            Type type = SpawnTemplateCatalog.ResolveMobScriptType(typeName);
            if (type != null && source.GetComponentInChildren(type, true) != null)
                return typeName;
        }

        return source.name;
    }

    private static void SeedFromWhitelistHints()
    {
        try
        {
            string path = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "HellGateSpawnPoint");
            path = Path.Combine(path, "SPAWN_TEMPLATE_WHITELIST.txt");
            if (!File.Exists(path))
                return;

            int added = 0;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                int at = line.IndexOf('@');
                if (at <= 0)
                    continue;

                string key = SpawnTemplateCatalog.NormalizeTemplateKey(line.Substring(0, at).Trim());
                string scene = line.Substring(at + 1).Trim();
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(scene))
                    continue;

                if (entries.ContainsKey(key))
                    continue;

                entries[key] = new Entry { Key = key, Scene = scene };
                added++;
            }

            if (added > 0)
                Plugin.Log?.LogInfo($"[SPAWN DISK CACHE] Seeded {added} key(s) from whitelist @Scene hints (will restore after title).");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN DISK CACHE] Whitelist seed failed: {ex.Message}");
        }
    }

    /// <summary>Wait only for Gametitle / boot — not for Common (gameplay uses fragReScene).</summary>
    private static bool IsWaitingForTitleScreen()
    {
        string reScene = HellGateLocationSpawnRefresh.GetReSceneName();
        if (!HellGateLocationSpawnRefresh.ShouldIgnoreSceneName(reScene))
            return false;

        try
        {
            string active = SceneManager.GetActiveScene().name;
            if (string.Equals(active, "Gametitle", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch
        {
            return true;
        }

        return string.IsNullOrEmpty(reScene);
    }
}
