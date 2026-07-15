using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using NoREroMod;
using NoREroMod.Systems.EventCore.Core;
using UnityEngine;

namespace NoREroMod.Systems.EventCore.Reinforcement;

internal static class ReinforcementEncounterLoader
{
    private const string RegistryFileName = "reinforcement_registry.json";
    private const string SharedConfigRootFolder = "_shared";

    private static readonly string[] PhraseLangFallbackOrder =
    {
        "En", "Ru", "Jp", "Cn", "Kr", "Fr", "De", "Pt", "Br", "Es"
    };

    private static ulong _spawnDiscoveryFingerprint;
    private static bool _spawnDiscoveryCacheValid;
    private static List<ReinforcementRegistryEntry> _spawnDiscoveryCachedEntries;

    /// <summary>Force re-scan of <c>REINFORCEMENT,…</c> lines (e.g. after F11 spawn-recorder RMB hot-reload).</summary>
    internal static void InvalidateSpawnDiscoveryCache()
    {
        _spawnDiscoveryCacheValid = false;
        _spawnDiscoveryCachedEntries = null;
    }

    internal static bool TryLoadAll(out ReinforcementRegistryFile registry, out List<ReinforcementLoadedEncounter> encounters)
    {
        registry = null;
        encounters = new List<ReinforcementLoadedEncounter>();

        EventCorePaths.Initialize();
        string root = EventCorePaths.JsonRoot;
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            Plugin.Log?.LogWarning("[Reinforcement] EventCore JSON root missing; encounters disabled.");
            return false;
        }

        string registryPath = Path.Combine(root, RegistryFileName);
        if (!File.Exists(registryPath))
        {
            Plugin.Log?.LogInfo($"[Reinforcement] Registry not found (optional): {registryPath}");
            return false;
        }

        try
        {
            string json = File.ReadAllText(registryPath);
            registry = JsonUtility.FromJson<ReinforcementRegistryFile>(json);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[Reinforcement] Failed to read registry: {ex.Message}");
            return false;
        }

        if (registry == null || !registry.enabled)
            return registry != null;

        List<ReinforcementRegistryEntry> work = BuildWorkList(registry, registryPath, root);
        if (work.Count == 0)
        {
            Plugin.Log?.LogWarning(
                "[Reinforcement] Registry enabled but no encounter: set discoverAnchorsFromSpawnPoint + REINFORCEMENT,folder,x,y in HellGateSpawn_*.txt, or set encounters[].eventFolder / eventFolder in the registry JSON.");
            return true;
        }

        var seenAnchorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < work.Count; i++)
        {
            ReinforcementRegistryEntry entry = work[i];
            if (entry == null || IsNullOrBlank(entry.folder))
                continue;

            string folder = entry.folder.Trim();
            string anchorId = entry.anchorId != null ? entry.anchorId.Trim() : string.Empty;
            if (anchorId.Length == 0)
                anchorId = folder;

            if (!seenAnchorIds.Add(anchorId))
            {
                Plugin.Log?.LogWarning(
                    $"[Reinforcement] Duplicate anchor id '{anchorId}' in spawn/registry — only the first instance is kept; fix HellGateSpawn_*.txt lines.");
                continue;
            }

            if (!TryLoadEncounter(root, folder, anchorId, entry, out ReinforcementLoadedEncounter loaded))
            {
                Plugin.Log?.LogWarning(
                    $"[Reinforcement] Missing pack '{folder}' for anchor '{anchorId}' (expected EventCore/{SharedConfigRootFolder}/{folder}/config.json).");
                continue;
            }

            encounters.Add(loaded);
        }

        return true;
    }

    private static void LogLoadedEncounterSummary(ReinforcementLoadedEncounter ev)
    {
        if (ev == null || ev.Config == null)
            return;

        string dist = "?";
        if (ev.HorizontalDistances != null && ev.HorizontalDistances.Length > 0)
        {
            var distParts = new string[ev.HorizontalDistances.Length];
            for (int di = 0; di < ev.HorizontalDistances.Length; di++)
                distParts[di] = ev.HorizontalDistances[di].ToString();
            dist = string.Join(",", distParts);
        }
        string types = ev.EnemyTypes != null && ev.EnemyTypes.Length > 0
            ? string.Join(",", ev.EnemyTypes)
            : "?";

        Plugin.Log?.LogInfo(
            $"[Reinforcement] Anchor '{ev.LogLabel}' @ ({ev.Anchor.x:F2},{ev.Anchor.y:F2}) " +
            $"triggerR={ev.TriggerR:F1} spawnDist=[{dist}] rightOnly={ev.Config.spawnRightOnly} " +
            $"maxKo={ev.Config.maxKnockdownSpawns} types=[{types}] phrases={(ev.PhraseLines.Length > 0 ? "yes" : "no")}.");
    }

    private static List<ReinforcementRegistryEntry> BuildWorkList(
        ReinforcementRegistryFile registry,
        string registryPathForFingerprint,
        string eventCoreRoot)
    {
        if (registry == null || !registry.discoverAnchorsFromSpawnPoint)
        {
            _spawnDiscoveryCacheValid = false;
            _spawnDiscoveryCachedEntries = null;
            return BuildRegistryEntries(registry);
        }

        string spawnDir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "HellGateSpawnPoint");
        ulong fp = ComputeSpawnDiscoveryFingerprint(spawnDir, registry, registryPathForFingerprint, eventCoreRoot);
        if (_spawnDiscoveryCacheValid && fp == _spawnDiscoveryFingerprint && _spawnDiscoveryCachedEntries != null && _spawnDiscoveryCachedEntries.Count > 0)
            return new List<ReinforcementRegistryEntry>(_spawnDiscoveryCachedEntries);

        List<ReinforcementRegistryEntry> discovered = ReinforcementSpawnDiscovery.Discover(spawnDir, registry);
        if (discovered.Count > 0)
        {
            _spawnDiscoveryFingerprint = fp;
            _spawnDiscoveryCachedEntries = discovered;
            _spawnDiscoveryCacheValid = true;
            return new List<ReinforcementRegistryEntry>(discovered);
        }

        _spawnDiscoveryCacheValid = false;
        _spawnDiscoveryCachedEntries = null;
        Plugin.Log?.LogWarning(
            "[Reinforcement] discoverAnchorsFromSpawnPoint is true but no REINFORCEMENT lines were found — falling back to manual registry entries.");
        return BuildRegistryEntries(registry);
    }

    private static ulong ComputeSpawnDiscoveryFingerprint(
        string spawnDir,
        ReinforcementRegistryFile registry,
        string registryPath,
        string eventCoreRoot)
    {
        ulong h = 1469598103934665603UL;
        h ^= (ulong)(registry.discoverAnchorsFromSpawnPoint ? 1 : 0);
        h *= 1099511628211UL;
        h = MixSharedPackConfigFiles(h, eventCoreRoot);

        if (registry.eventFoldersAllowed != null)
        {
            for (int i = 0; i < registry.eventFoldersAllowed.Length; i++)
            {
                string s = registry.eventFoldersAllowed[i];
                if (string.IsNullOrEmpty(s))
                    continue;
                h ^= (ulong)s.GetHashCode();
                h *= 1099511628211UL;
            }
        }

        try
        {
            if (!string.IsNullOrEmpty(registryPath) && File.Exists(registryPath))
            {
                h ^= (ulong)File.GetLastWriteTimeUtc(registryPath).Ticks;
                h *= 1099511628211UL;
            }
        }
        catch
        {
        }

        try
        {
            if (!Directory.Exists(spawnDir))
                return h;

            string[] files = Directory.GetFiles(spawnDir, "HellGateSpawn_*.txt", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                FileInfo fi = new FileInfo(path);
                h ^= (ulong)fi.Name.GetHashCode();
                h *= 1099511628211UL;
                h ^= (ulong)fi.Length;
                h *= 1099511628211UL;
                h ^= (ulong)fi.LastWriteTimeUtc.Ticks;
                h *= 1099511628211UL;
            }
        }
        catch
        {
        }

        return h;
    }

    private static ulong MixSharedPackConfigFiles(ulong h, string eventCoreRoot)
    {
        if (string.IsNullOrEmpty(eventCoreRoot) || !Directory.Exists(eventCoreRoot))
            return h;

        try
        {
            string sharedRoot = Path.Combine(eventCoreRoot, SharedConfigRootFolder);
            if (!Directory.Exists(sharedRoot))
                return h;

            string[] packDirs = Directory.GetDirectories(sharedRoot);
            for (int i = 0; i < packDirs.Length; i++)
            {
                string configPath = Path.Combine(packDirs[i], "config.json");
                h = MixFileIntoHash(h, configPath);

                string anchorsDir = Path.Combine(packDirs[i], "anchors");
                if (!Directory.Exists(anchorsDir))
                    continue;

                string[] anchorFiles = Directory.GetFiles(anchorsDir, "*.json", SearchOption.TopDirectoryOnly);
                for (int j = 0; j < anchorFiles.Length; j++)
                    h = MixFileIntoHash(h, anchorFiles[j]);
            }
        }
        catch
        {
        }

        return h;
    }

    private static ulong MixFileIntoHash(ulong h, string path)
    {
        try
        {
            if (!File.Exists(path))
                return h;
            FileInfo fi = new FileInfo(path);
            h ^= (ulong)fi.FullName.GetHashCode();
            h *= 1099511628211UL;
            h ^= (ulong)fi.Length;
            h *= 1099511628211UL;
            h ^= (ulong)fi.LastWriteTimeUtc.Ticks;
            h *= 1099511628211UL;
        }
        catch
        {
        }

        return h;
    }

    private static List<ReinforcementRegistryEntry> BuildRegistryEntries(ReinforcementRegistryFile registry)
    {
        var list = new List<ReinforcementRegistryEntry>();
        if (registry == null)
            return list;

        if (registry.encounters != null && registry.encounters.Length > 0)
        {
            for (int i = 0; i < registry.encounters.Length; i++)
            {
                ReinforcementRegistryEncounterSpec spec = registry.encounters[i];
                if (spec == null || IsNullOrBlank(spec.eventFolder))
                    continue;

                string scene = spec.eventSceneContains != null ? spec.eventSceneContains.Trim() : string.Empty;
                list.Add(new ReinforcementRegistryEntry
                {
                    anchorId = spec.eventFolder.Trim(),
                    folder = spec.eventFolder.Trim(),
                    sceneNameContains = scene
                });
            }

            return list;
        }

        if (IsNullOrBlank(registry.eventFolder))
            return list;

        string legacyScene = registry.eventSceneContains != null ? registry.eventSceneContains.Trim() : string.Empty;
        list.Add(new ReinforcementRegistryEntry
        {
            anchorId = registry.eventFolder.Trim(),
            folder = registry.eventFolder.Trim(),
            sceneNameContains = legacyScene
        });
        return list;
    }

    private static bool TryLoadEncounter(
        string jsonRoot,
        string folder,
        string anchorId,
        ReinforcementRegistryEntry entry,
        out ReinforcementLoadedEncounter loaded)
    {
        loaded = null;
        string packDir = Path.Combine(Path.Combine(jsonRoot, SharedConfigRootFolder), folder);
        string sharedConfigPath = Path.Combine(packDir, "config.json");
        if (!File.Exists(sharedConfigPath))
            return false;

        ReinforcementConfigFile cfgRaw;
        string configPathUsed = sharedConfigPath;
        try
        {
            cfgRaw = JsonUtility.FromJson<ReinforcementConfigFile>(File.ReadAllText(sharedConfigPath));
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[Reinforcement] Bad config '{sharedConfigPath}': {ex.Message}");
            return false;
        }

        if (cfgRaw == null)
            return false;

        string anchorOverridePath = Path.Combine(Path.Combine(packDir, "anchors"), anchorId + ".json");
        if (File.Exists(anchorOverridePath))
        {
            try
            {
                ReinforcementConfigFile overrideCfg = JsonUtility.FromJson<ReinforcementConfigFile>(File.ReadAllText(anchorOverridePath));
                if (overrideCfg != null)
                {
                    cfgRaw = overrideCfg;
                    configPathUsed = anchorOverridePath;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[Reinforcement] Bad anchor override '{anchorOverridePath}': {ex.Message}");
            }
        }

        ReinforcementConfigFile cfg = JsonUtility.FromJson<ReinforcementConfigFile>(JsonUtility.ToJson(cfgRaw));
        ApplyAnchorToConfig(cfg, entry);

        if (!string.Equals(configPathUsed, sharedConfigPath, StringComparison.OrdinalIgnoreCase))
        {
            Plugin.Log?.LogInfo($"[Reinforcement] Anchor '{anchorId}' uses per-anchor config: {configPathUsed}");
        }

        string[] enemyTypes = EventAnchorCsv.ParseStrings(cfg.enemyTypesCsv);
        if (enemyTypes.Length == 0)
            enemyTypes = new[] { "TouzokuNormal", "TouzokuAxe" };

        int[] distances = EventAnchorCsv.ParseInts(cfg.horizontalSpawnDistancesCsv);
        if (distances.Length == 0)
            distances = new[] { 2, 4, 5, 6 };

        string[] phraseLines = new string[0];
        string phraseFolder = cfg.phrasesFromEventFolder != null ? cfg.phrasesFromEventFolder.Trim() : string.Empty;
        if (phraseFolder.Length > 0)
        {
            string activeLang = EventCoreLanguage.ResolveFolderCode();
            if (!TryLoadPhrases(jsonRoot, activeLang, phraseFolder, out phraseLines, out string phrasesPath))
            {
                Plugin.Log?.LogWarning(
                    $"[Reinforcement] '{anchorId}' (pack {folder}): phrasesFromEventFolder '{phraseFolder}' — no phrases loaded (lang {activeLang}).");
            }
        }

        loaded = new ReinforcementLoadedEncounter(anchorId, entry, cfg, enemyTypes, distances, phraseLines);
        return true;
    }

    private static bool TryLoadPhrases(string jsonRoot, string activeLang, string folder, out string[] lines, out string pathUsed)
    {
        lines = new string[0];
        pathUsed = string.Empty;

        string primary = Path.Combine(Path.Combine(Path.Combine(jsonRoot, activeLang), folder), "phrases.json");
        if (TryReadPhrasesFile(primary, out lines) && lines.Length > 0)
        {
            pathUsed = primary;
            return true;
        }

        for (int i = 0; i < PhraseLangFallbackOrder.Length; i++)
        {
            string lang = PhraseLangFallbackOrder[i];
            if (string.Equals(lang, activeLang, StringComparison.OrdinalIgnoreCase))
                continue;
            string path = Path.Combine(Path.Combine(Path.Combine(jsonRoot, lang), folder), "phrases.json");
            if (!TryReadPhrasesFile(path, out lines) || lines.Length == 0)
                continue;
            pathUsed = path;
            return true;
        }

        return false;
    }

    private static bool TryReadPhrasesFile(string path, out string[] lines)
    {
        lines = new string[0];
        if (!File.Exists(path))
            return false;

        try
        {
            ReinforcementPhrasesFile pf = JsonUtility.FromJson<ReinforcementPhrasesFile>(File.ReadAllText(path));
            if (pf == null || pf.lines == null || pf.lines.Length == 0)
                return false;

            var cleaned = new List<string>();
            for (int i = 0; i < pf.lines.Length; i++)
            {
                string s = pf.lines[i];
                if (s == null)
                    continue;
                s = s.Trim();
                if (!string.IsNullOrEmpty(s))
                    cleaned.Add(s);
            }

            lines = cleaned.ToArray();
            return lines.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyAnchorToConfig(ReinforcementConfigFile cfg, ReinforcementRegistryEntry entry)
    {
        if (cfg == null)
            return;

        if (entry != null && entry.useSpawnBindingAnchor)
        {
            cfg.anchorX = entry.spawnBindingAnchorX;
            cfg.anchorY = entry.spawnBindingAnchorY;
        }
    }

    private static bool IsNullOrBlank(string s)
    {
        if (string.IsNullOrEmpty(s))
            return true;
        return s.Trim().Length == 0;
    }
}
