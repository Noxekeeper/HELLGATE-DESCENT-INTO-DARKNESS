using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using NoREroMod;
using NoREroMod.Systems.EventCore.Core;
using NoREroMod.Systems.Spawn;
using UnityEngine;

namespace NoREroMod.Systems.EventCore.EventTrap;

internal static class EventTrapEncounterLoader
{
    private const string RegistryPreferred = "event_trap_registry.json";
    private const string RegistryLegacy = "ambient_spike_registry.json";

    /// <summary>
    /// System options (anchor, radii, bone offset, cooldowns, spawn type, flank, …) for all languages.
    /// Path: <c>EventCore/_shared/&lt;eventFolder&gt;/config.json</c>. When this file exists, per-language
    /// <c>EventCore/&lt;Lang&gt;/&lt;eventFolder&gt;/config.json</c> is ignored for that encounter.
    /// </summary>
    private const string SharedConfigRootFolder = "_shared";

    /// <summary>
    /// When <see cref="EventCoreLanguage.ResolveFolderCode"/> has no usable pack for the encounter,
    /// try these content folders in order (for legacy per-language <c>config.json</c> + <c>phrases.json</c> packs).
    /// </summary>
    private static readonly string[] TrapEncounterLangFallbackOrder =
    {
        "En", "Ru", "Jp", "Cn", "Kr", "Fr", "De", "Pt", "Br", "Es"
    };

    internal static bool TryLoadAll(out EventTrapRegistryFile registry, out List<EventTrapLoadedEncounter> encounters)
    {
        registry = null;
        encounters = new List<EventTrapLoadedEncounter>();

        EventCorePaths.Initialize();
        string root = EventCorePaths.JsonRoot;
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            Plugin.Log?.LogWarning("[EventTrap] EventCore JSON root missing; EventTrap encounters disabled.");
            return false;
        }

        string registryPath = ResolveRegistryPath(root, out bool usedLegacy);
        if (!File.Exists(registryPath))
        {
            Plugin.Log?.LogInfo(
                $"[EventTrap] Registry not found (optional): {Path.Combine(root, RegistryPreferred)} (or legacy {RegistryLegacy})");
            return false;
        }

        try
        {
            string json = File.ReadAllText(registryPath);
            registry = JsonUtility.FromJson<EventTrapRegistryFile>(json);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[EventTrap] Failed to read registry: {ex.Message}");
            return false;
        }

        if (usedLegacy)
        {
            Plugin.Log?.LogInfo(
                $"[EventTrap] Using legacy registry file name '{RegistryLegacy}'. Prefer renaming to '{RegistryPreferred}'.");
        }

        if (registry == null || !registry.enabled)
            return registry != null;

        List<EventTrapRegistryEntry> work = BuildWorkList(registry, registryPath, root);
        if (work.Count == 0)
        {
            Plugin.Log?.LogWarning(
                "[EventTrap] Registry enabled but no encounter: set discoverAnchorsFromSpawnPoint + EVENTTRAP,event_folder,x,y in HellGateSpawn_*.txt, or set eventFolder / encounters[].eventFolder in the registry JSON.");
            return true;
        }

        string activeLang = EventCoreLanguage.ResolveFolderCode();
        var seenAnchorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < work.Count; i++)
        {
            EventTrapRegistryEntry entry = work[i];
            if (entry == null || IsNullOrBlank(entry.folder))
                continue;

            string folder = entry.folder.Trim();
            string anchorId = entry.anchorId != null ? entry.anchorId.Trim() : string.Empty;
            if (anchorId.Length == 0)
                anchorId = folder;

            if (!seenAnchorIds.Add(anchorId))
            {
                Plugin.Log?.LogWarning(
                    $"[EventTrap] Duplicate anchor id '{anchorId}' — skipped; use unique ids in EVENTTRAP,anchorId,pack,x,y lines.");
                continue;
            }

            if (!TryLoadEncounterWithLangFallback(root, activeLang, folder, anchorId, entry, out EventTrapLoadedEncounter loaded))
            {
                Plugin.Log?.LogWarning(
                    $"[EventTrap] Missing encounter '{folder}' anchor '{anchorId}' for HellGate language '{activeLang}' (shared: EventCore/{SharedConfigRootFolder}/<folder>/config.json; legacy: EventCore/<Lang>/{folder}/ with fallbacks {string.Join(", ", TrapEncounterLangFallbackOrder)}).");
                continue;
            }

            encounters.Add(loaded);
        }

        return true;
    }

    private static string ResolveRegistryPath(string root, out bool usedLegacy)
    {
        usedLegacy = false;
        string preferred = Path.Combine(root, RegistryPreferred);
        if (File.Exists(preferred))
            return preferred;

        string legacy = Path.Combine(root, RegistryLegacy);
        if (File.Exists(legacy))
        {
            usedLegacy = true;
            return legacy;
        }

        return preferred;
    }

    private static ulong _spawnDiscoveryFingerprint;
    private static bool _spawnDiscoveryCacheValid;
    private static List<EventTrapRegistryEntry> _spawnDiscoveryCachedEntries;

    /// <summary>Force re-scan of <c>EVENTTRAP,…</c> lines (e.g. after F11 spawn-recorder RMB hot-reload).</summary>
    internal static void InvalidateSpawnDiscoveryCache()
    {
        _spawnDiscoveryCacheValid = false;
        _spawnDiscoveryCachedEntries = null;
    }

    private static List<EventTrapRegistryEntry> BuildWorkList(
        EventTrapRegistryFile registry,
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
            return new List<EventTrapRegistryEntry>(_spawnDiscoveryCachedEntries);

        List<EventTrapRegistryEntry> discovered = EventTrapSpawnDiscovery.Discover(spawnDir, registry);
        if (discovered.Count > 0)
        {
            _spawnDiscoveryFingerprint = fp;
            _spawnDiscoveryCachedEntries = discovered;
            _spawnDiscoveryCacheValid = true;
            return new List<EventTrapRegistryEntry>(discovered);
        }

        _spawnDiscoveryCacheValid = false;
        _spawnDiscoveryCachedEntries = null;
        Plugin.Log?.LogWarning(
            "[EventTrap] discoverAnchorsFromSpawnPoint is true but no EVENTTRAP lines were found under HellGateSpawnPoint — falling back to manual registry entries.");
        return BuildRegistryEntries(registry);
    }

    private static ulong ComputeSpawnDiscoveryFingerprint(
        string spawnDir,
        EventTrapRegistryFile registry,
        string registryPath,
        string jsonRoot)
    {
        ulong h = 1469598103934665603UL;
        h ^= (ulong)(registry.discoverAnchorsFromSpawnPoint ? 1 : 0);
        h *= 1099511628211UL;
        h = MixSharedPackConfigFiles(h, jsonRoot);

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

    private static List<EventTrapRegistryEntry> BuildRegistryEntries(EventTrapRegistryFile registry)
    {
        var list = new List<EventTrapRegistryEntry>();
        if (registry == null)
            return list;

        if (registry.encounters != null && registry.encounters.Length > 0)
        {
            for (int i = 0; i < registry.encounters.Length; i++)
            {
                EventTrapRegistryEncounterSpec spec = registry.encounters[i];
                if (spec == null || IsNullOrBlank(spec.eventFolder))
                    continue;

                string scene = spec.eventSceneContains != null ? spec.eventSceneContains.Trim() : string.Empty;
                list.Add(new EventTrapRegistryEntry
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
        list.Add(new EventTrapRegistryEntry
        {
            anchorId = registry.eventFolder.Trim(),
            folder = registry.eventFolder.Trim(),
            sceneNameContains = legacyScene
        });
        return list;
    }

    private static bool TryLoadEncounterWithLangFallback(
        string jsonRoot,
        string activeLang,
        string folder,
        string anchorId,
        EventTrapRegistryEntry entry,
        out EventTrapLoadedEncounter loaded)
    {
        loaded = null;
        string sharedConfigPath = Path.Combine(Path.Combine(Path.Combine(jsonRoot, SharedConfigRootFolder), folder), "config.json");
        if (File.Exists(sharedConfigPath))
        {
            if (TryLoadEncounterPack(jsonRoot, activeLang, folder, anchorId, entry, sharedConfigPath, isSharedConfig: true, out loaded))
                return true;
        }

        string langConfigPath = Path.Combine(Path.Combine(Path.Combine(jsonRoot, activeLang), folder), "config.json");
        if (TryLoadEncounterPack(jsonRoot, activeLang, folder, anchorId, entry, langConfigPath, isSharedConfig: false, out loaded))
            return true;

        for (int i = 0; i < TrapEncounterLangFallbackOrder.Length; i++)
        {
            string lang = TrapEncounterLangFallbackOrder[i];
            if (string.Equals(lang, activeLang, StringComparison.OrdinalIgnoreCase))
                continue;

            string fallbackConfig = Path.Combine(Path.Combine(Path.Combine(jsonRoot, lang), folder), "config.json");
            if (!TryLoadEncounterPack(jsonRoot, activeLang, folder, anchorId, entry, fallbackConfig, isSharedConfig: false, out loaded))
                continue;

            Plugin.Log?.LogInfo(
                $"[EventTrap] Loaded '{folder}' anchor '{anchorId}' from '{lang}/' (HellGate language folder '{activeLang}' had no pack).");
            return true;
        }

        return false;
    }

    private static bool TryLoadEncounterPack(
        string jsonRoot,
        string phraseLang,
        string folder,
        string anchorId,
        EventTrapRegistryEntry entry,
        string configPath,
        bool isSharedConfig,
        out EventTrapLoadedEncounter loaded)
    {
        loaded = null;
        if (!File.Exists(configPath))
            return false;

        EventTrapConfigFile cfgRaw;
        string configPathUsed = configPath;
        try
        {
            cfgRaw = JsonUtility.FromJson<EventTrapConfigFile>(File.ReadAllText(configPath));
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[EventTrap] Bad config '{configPath}': {ex.Message}");
            return false;
        }

        if (cfgRaw == null)
            return false;

        if (isSharedConfig && !string.IsNullOrEmpty(anchorId))
        {
            string anchorOverridePath = Path.Combine(
                Path.Combine(Path.Combine(jsonRoot, SharedConfigRootFolder), folder),
                Path.Combine("anchors", anchorId + ".json"));
            if (File.Exists(anchorOverridePath))
            {
                try
                {
                    EventTrapConfigFile overrideCfg = JsonUtility.FromJson<EventTrapConfigFile>(File.ReadAllText(anchorOverridePath));
                    if (overrideCfg != null)
                    {
                        cfgRaw = overrideCfg;
                        configPathUsed = anchorOverridePath;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"[EventTrap] Bad anchor override '{anchorOverridePath}': {ex.Message}");
                }
            }
        }

        EventTrapConfigFile cfg = CloneEventTrapConfig(cfgRaw);
        if (cfg == null)
            return false;

        ApplyAnchorToConfig(cfg, entry, folder);

        if (isSharedConfig && !string.Equals(configPathUsed, configPath, StringComparison.OrdinalIgnoreCase))
        {
            Plugin.Log?.LogInfo($"[EventTrap] Anchor '{anchorId}' uses per-anchor config: {configPathUsed}");
        }

        if (isSharedConfig)
        {
            string legacyLangConfig = Path.Combine(Path.Combine(Path.Combine(jsonRoot, phraseLang), folder), "config.json");
            if (File.Exists(legacyLangConfig) &&
                !string.Equals(legacyLangConfig, configPath, StringComparison.OrdinalIgnoreCase))
            {
                Plugin.Log?.LogInfo(
                    $"[EventTrap] '{folder}' anchor '{anchorId}': using shared config at '{configPath}' — per-language config.json at '{legacyLangConfig}' is ignored.");
            }
        }

        string[] enemyTypes = EventAnchorCsv.ParseStrings(cfg.enemyTypesCsv);
        if (enemyTypes.Length == 0 && !IsNullOrBlank(cfg.spawnEnemyType))
            enemyTypes = new[] { cfg.spawnEnemyType.Trim() };

        int[] distances = EventAnchorCsv.ParseInts(cfg.horizontalSpawnDistancesCsv);

        if (enemyTypes.Length == 0)
        {
            Plugin.Log?.LogWarning(
                $"[EventTrap] '{anchorId}' pack '{folder}' ({(isSharedConfig ? "shared" : phraseLang)}): spawnEnemyType / enemyTypesCsv empty — ambush spawn disabled.");
        }

        string phraseFolder = cfg.phrasesFromEventFolder != null ? cfg.phrasesFromEventFolder.Trim() : string.Empty;
        if (phraseFolder.Length == 0)
            phraseFolder = folder;

        if (!TryLoadPhrases(jsonRoot, phraseLang, phraseFolder, out string[] lines, out string phrasesPath))
        {
            Plugin.Log?.LogWarning(
                $"[EventTrap] '{anchorId}' pack '{folder}': no phrases loaded (lang {phraseLang}, phraseFolder '{phraseFolder}').");
            return false;
        }

        loaded = new EventTrapLoadedEncounter(anchorId, entry, cfg, lines, phrasesPath, enemyTypes, distances);
        return true;
    }

    private static void ApplyAnchorToConfig(EventTrapConfigFile cfg, EventTrapRegistryEntry entry, string encounterFolderForLog)
    {
        if (cfg == null)
            return;

        if (entry != null && entry.useSpawnBindingAnchor)
        {
            cfg.anchorX = entry.spawnBindingAnchorX;
            cfg.anchorY = entry.spawnBindingAnchorY;
            return;
        }

        ApplyHellGateSpawnTrapAnchor(cfg, encounterFolderForLog);
    }

    private static EventTrapConfigFile CloneEventTrapConfig(EventTrapConfigFile src)
    {
        if (src == null)
            return null;
        return JsonUtility.FromJson<EventTrapConfigFile>(JsonUtility.ToJson(src));
    }

    private static void ApplyHellGateSpawnTrapAnchor(EventTrapConfigFile cfg, string encounterFolderForLog)
    {
        if (cfg == null)
            return;

        string key = cfg.anchorTrapKey != null ? cfg.anchorTrapKey.Trim() : string.Empty;
        if (string.IsNullOrEmpty(key))
            return;

        string rel = cfg.anchorHellGateSpawnFile != null ? cfg.anchorHellGateSpawnFile.Trim() : string.Empty;
        if (string.IsNullOrEmpty(rel))
        {
            Plugin.Log?.LogWarning(
                $"[EventTrap] '{encounterFolderForLog}': anchorTrapKey is set but anchorHellGateSpawnFile is empty; using JSON anchorX/Y.");
            return;
        }

        string spawnRoot = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "HellGateSpawnPoint");
        string abs = Path.Combine(spawnRoot, rel);
        if (!SpawnTrapAnchorLookup.TryGetFirstTrapAnchor(abs, key, out Vector2 pos))
        {
            Plugin.Log?.LogWarning(
                $"[EventTrap] '{encounterFolderForLog}': no TRAP/SPAWN,Trap line for key '{key}' in '{abs}'; using JSON anchorX/Y.");
            return;
        }

        cfg.anchorX = pos.x;
        cfg.anchorY = pos.y;
        Plugin.Log?.LogInfo($"[EventTrap] '{encounterFolderForLog}': anchor from spawn file '{rel}' key '{key}' => ({pos.x:F2},{pos.y:F2}).");
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

        for (int i = 0; i < TrapEncounterLangFallbackOrder.Length; i++)
        {
            string lang = TrapEncounterLangFallbackOrder[i];
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
            EventTrapPhrasesFile pf = JsonUtility.FromJson<EventTrapPhrasesFile>(File.ReadAllText(path));
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

    private static bool IsNullOrBlank(string s)
    {
        if (string.IsNullOrEmpty(s))
            return true;
        return s.Trim().Length == 0;
    }
}
