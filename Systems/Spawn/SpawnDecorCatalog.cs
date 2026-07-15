using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Author catalog of scene decorations (corpses, static props) for coordinate spawns.
/// File: HellGateJson/HellGateSpawnPoint/DECOR_CATALOG.txt
/// Spawn format: OBJECT,Key,X,Y,Count[,flip] or DECOR,Key,X,Y,Count[,flip]
/// </summary>
internal static class SpawnDecorCatalog
{
    internal sealed class DecorEntry
    {
        public string Key = string.Empty;
        public string NormalizedKey = string.Empty;
        public string Note = string.Empty;
        public string[] Scenes = new string[0];
    }

    private const string CatalogFileName = "DECOR_CATALOG.txt";

    private static readonly Dictionary<string, DecorEntry> entriesByNormalizedKey =
        new Dictionary<string, DecorEntry>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, HashSet<string>> keysByScene =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> blockedNormalizedKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static float lastLoadTime = -999f;
    private static float _lastMissingLogTime = -999f;

    private static bool _loggedSummaryOnce;
    private const float ReloadInterval = 5f;
    private const float MissingLogInterval = 30f;

    internal static bool HasEntries => entriesByNormalizedKey.Count > 0;

    internal static void EnsureLoaded()
    {
        if (Time.realtimeSinceStartup - lastLoadTime < ReloadInterval)
            return;

        LoadFromDisk();
    }

    internal static bool IsKnownDecorKey(string key)
    {
        EnsureLoaded();
        string normalized = SpawnTemplateCatalog.NormalizeTemplateKey(key);
        return !string.IsNullOrEmpty(normalized) && entriesByNormalizedKey.ContainsKey(normalized);
    }

    internal static bool IsBlocked(string objectOrKeyName)
    {
        EnsureLoaded();
        string normalized = SpawnTemplateCatalog.NormalizeTemplateKey(objectOrKeyName);
        return !string.IsNullOrEmpty(normalized) && blockedNormalizedKeys.Contains(normalized);
    }

    internal static bool TryGetEntry(string key, out DecorEntry entry)
    {
        EnsureLoaded();
        entry = null;
        if (string.IsNullOrEmpty(key))
            return false;

        return entriesByNormalizedKey.TryGetValue(SpawnTemplateCatalog.NormalizeTemplateKey(key), out entry);
    }

    internal static HashSet<string> GetNormalizedKeysForScene(string sceneName)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(sceneName))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (keysByScene.TryGetValue(sceneName.Trim(), out HashSet<string> set) && set != null)
            return set;

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    internal static IEnumerable<DecorEntry> EnumerateEntries()
    {
        EnsureLoaded();
        return entriesByNormalizedKey.Values;
    }

    internal static string[] GetScenesForKey(string key)
    {
        EnsureLoaded();
        string normalized = SpawnTemplateCatalog.NormalizeTemplateKey(key);
        if (string.IsNullOrEmpty(normalized))
            return new string[0];

        if (entriesByNormalizedKey.TryGetValue(normalized, out DecorEntry entry) &&
            entry != null && entry.Scenes != null && entry.Scenes.Length > 0)
            return entry.Scenes;

        var scenes = new List<string>();
        foreach (KeyValuePair<string, HashSet<string>> kvp in keysByScene)
        {
            if (kvp.Value != null && kvp.Value.Contains(normalized))
                scenes.Add(kvp.Key);
        }

        return scenes.ToArray();
    }

    private static void LoadFromDisk()
    {
        entriesByNormalizedKey.Clear();
        keysByScene.Clear();
        blockedNormalizedKeys.Clear();
        lastLoadTime = Time.realtimeSinceStartup;

        string path = GetCatalogPath();
        if (!File.Exists(path))
        {
            if (Time.realtimeSinceStartup - _lastMissingLogTime >= MissingLogInterval)
            {
                _lastMissingLogTime = Time.realtimeSinceStartup;
                Plugin.Log?.LogInfo("[SPAWN DECOR] Catalog file not found: " + path);
            }
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(path);
            string section = string.Empty;

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                if (string.IsNullOrEmpty(raw) || raw.Trim().Length == 0)
                    continue;

                string trimmed = raw.Trim();
                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                    continue;

                if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                {
                    section = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    continue;
                }

                if (string.Equals(section, "NOT_ROOT_OBJECTS", StringComparison.OrdinalIgnoreCase))
                {
                    RegisterBlocked(trimmed);
                    continue;
                }

                if (string.Equals(section, "SCENES_FOR_CACHE", StringComparison.OrdinalIgnoreCase))
                {
                    ParseSceneCacheLine(trimmed);
                    continue;
                }

                if (string.Equals(section, "DECOR_CORPSES", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(section, "DECOR_PROPS", StringComparison.OrdinalIgnoreCase))
                {
                    ParseDecorLine(trimmed);
                }
            }

            if (!_loggedSummaryOnce)
            {
                _loggedSummaryOnce = true;
                Plugin.Log?.LogInfo(
                    $"[SPAWN DECOR] Loaded {entriesByNormalizedKey.Count} decor key(s), " +
                    $"{keysByScene.Count} scene map(s), {blockedNormalizedKeys.Count} blocked name(s).");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[SPAWN DECOR] Failed to load catalog: " + ex.Message);
        }
    }

    private static void ParseDecorLine(string line)
    {
        string[] parts = line.Split('|');
        if (parts.Length == 0)
            return;

        string key = parts[0].Trim();
        if (string.IsNullOrEmpty(key))
            return;

        DecorEntry entry = new DecorEntry
        {
            Key = key,
            NormalizedKey = SpawnTemplateCatalog.NormalizeTemplateKey(key)
        };

        if (parts.Length > 1)
            entry.Note = parts[1].Trim();

        if (parts.Length > 2)
            entry.Scenes = SplitSceneList(parts[2]);

        if (string.IsNullOrEmpty(entry.NormalizedKey))
            return;

        entriesByNormalizedKey[entry.NormalizedKey] = entry;

        for (int i = 0; i < entry.Scenes.Length; i++)
            AddKeyToScene(entry.Scenes[i], entry.NormalizedKey);
    }

    private static void ParseSceneCacheLine(string line)
    {
        int pipe = line.IndexOf('|');
        if (pipe < 0)
            return;

        string sceneName = line.Substring(0, pipe).Trim();
        string keysPart = line.Substring(pipe + 1).Trim();
        if (string.IsNullOrEmpty(sceneName) || string.IsNullOrEmpty(keysPart))
            return;

        string[] keyTokens = keysPart.Split(',');
        for (int i = 0; i < keyTokens.Length; i++)
        {
            string token = keyTokens[i].Trim();
            if (string.IsNullOrEmpty(token))
                continue;

            string normalized = SpawnTemplateCatalog.NormalizeTemplateKey(token);
            AddKeyToScene(sceneName, normalized);

            if (!entriesByNormalizedKey.ContainsKey(normalized))
            {
                entriesByNormalizedKey[normalized] = new DecorEntry
                {
                    Key = token,
                    NormalizedKey = normalized,
                    Note = "(from SCENES_FOR_CACHE)",
                    Scenes = new[] { sceneName }
                };
            }
        }
    }

    private static void RegisterBlocked(string line)
    {
        string token = line;
        int hash = token.IndexOf('#');
        if (hash >= 0)
            token = token.Substring(0, hash);

        token = token.Trim();
        if (string.IsNullOrEmpty(token))
            return;

        blockedNormalizedKeys.Add(SpawnTemplateCatalog.NormalizeTemplateKey(token));
    }

    private static string[] SplitSceneList(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return new string[0];

        string[] parts = raw.Split(',');
        var result = new List<string>();
        for (int i = 0; i < parts.Length; i++)
        {
            string scene = parts[i].Trim();
            if (!string.IsNullOrEmpty(scene))
                result.Add(scene);
        }

        return result.ToArray();
    }

    private static void AddKeyToScene(string sceneName, string normalizedKey)
    {
        if (string.IsNullOrEmpty(sceneName) || string.IsNullOrEmpty(normalizedKey))
            return;

        if (!keysByScene.TryGetValue(sceneName, out HashSet<string> set) || set == null)
        {
            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            keysByScene[sceneName] = set;
        }

        set.Add(normalizedKey);
    }

    private static string GetCatalogPath()
    {
        try
        {
            string spawnDir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "HellGateSpawnPoint");
            return Path.Combine(spawnDir, CatalogFileName);
        }
        catch
        {
            string basePath = Path.Combine(Application.dataPath, "..");
            string plugins = Path.Combine(Path.Combine(Path.Combine(basePath, "BepInEx"), "plugins"), "HellGateJson");
            return Path.Combine(Path.Combine(plugins, "HellGateSpawnPoint"), CatalogFileName);
        }
    }
}
