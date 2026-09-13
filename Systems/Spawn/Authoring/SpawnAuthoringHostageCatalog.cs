using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// F11 Hostage &amp; OtherScenes picker: rescue hostages plus Spine look / scene
/// animations. Aliases stay spawnable from txt; they are not listed here.
/// </summary>
internal static class SpawnAuthoringHostageCatalog
{
    /// <summary>Canonical HOSTAGE keys (SpawnSlave / rescue prefabs).</summary>
    private static readonly string[] BuiltInHostageKeys =
    {
        "MobSlumSlave",
        "MobCrawlingSlave",
        "MobSpiderSlave",
        "MobRosewarm",
        "MobMutude",
        "MobMachineSlave",
        "witchslaveViolin",
        "witchslaveslime",
        "EnemyMobCrowSlaveBack",
        "EnemyMobCrowSlaveStandup"
    };

    /// <summary>Spine look / scene animations (not combat, not Trap).</summary>
    private static readonly string[] BuiltInOtherSceneKeys =
    {
        "gob_look",
        "gob_look2",
        "Look_Dorei",
        "Look_mutude",
        "mob_sister",
        "CowSlavespine",
        "ChainCowSlavespine"
    };

    private static readonly string[] HiddenPickerKeys =
    {
        "help",
        "meatshieldhelp",
        "npcslaveenable",
        "npcslaveflag",
        "colcreateobj",
        "back",
        "cow1",
        "scapegoat_slave",
        "villageslave",
        "npcslave",
        "inchurchslave",
        "inchchurchslave",
        "evslavebigaxearadia",
        "evslavebigaxemob",
        "evbunnyero",
        "slave",
        "stand"
    };

    private static string[] cachedKeys = new string[0];
    private static float nextRebuildUnscaledTime;

    internal static string[] GetKeys()
    {
        EnsureBuilt();
        return cachedKeys;
    }

    internal static void Invalidate()
    {
        cachedKeys = new string[0];
        nextRebuildUnscaledTime = 0f;
    }

    internal static bool IsReady(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        return SpawnTemplateCatalog.HasTemplate(key) || SpawnTemplateCatalog.TryCacheFromResources(key);
    }

    /// <summary>True if F11 should list this key on Hostage &amp; OtherScenes (not Trap).</summary>
    internal static bool IsPickerKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        if (IsListedHiddenKey(key) || IsSceneChildPickerName(key))
            return false;
        if (IsBuiltInHostageKey(key) || IsOtherSceneKey(key))
            return true;
        return SpawnTemplateCatalog.IsHostageTemplateKey(key);
    }

    internal static bool IsOtherSceneKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        for (int i = 0; i < BuiltInOtherSceneKeys.Length; i++)
        {
            if (SpawnTemplateCatalog.TemplateKeysMatch(key, BuiltInOtherSceneKeys[i]))
                return true;
        }

        return false;
    }

    /// <summary>Canonical Spine look / scene keys for F11 Decorations and Hostage lists.</summary>
    internal static string[] GetOtherSceneKeys()
    {
        var list = new List<string>(BuiltInOtherSceneKeys.Length);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < BuiltInOtherSceneKeys.Length; i++)
        {
            string key = BuiltInOtherSceneKeys[i];
            if (string.IsNullOrEmpty(key) || !seen.Add(CompactPickerKey(key)))
                continue;
            list.Add(key);
        }

        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list.ToArray();
    }

    internal static bool IsHostageLineKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        if (IsOtherSceneKey(key))
            return false;
        return IsBuiltInHostageKey(key) || SpawnTemplateCatalog.IsHostageTemplateKey(key);
    }

    /// <summary>Look / hostage keys inherit <c>|faction=</c> onto the combatant after H / rescue.</summary>
    internal static bool AcceptsFaction(string key)
    {
        return IsHostageLineKey(key) || IsOtherSceneKey(key);
    }

    private static bool IsBuiltInHostageKey(string key)
    {
        for (int i = 0; i < BuiltInHostageKeys.Length; i++)
        {
            if (SpawnTemplateCatalog.TemplateKeysMatch(key, BuiltInHostageKeys[i]))
                return true;
        }

        return false;
    }

    private static void EnsureBuilt()
    {
        if (cachedKeys.Length > 0 && Time.unscaledTime < nextRebuildUnscaledTime)
            return;
        Rebuild();
    }

    private static void Rebuild()
    {
        nextRebuildUnscaledTime = Time.unscaledTime + 2f;

        var candidates = new List<string>();
        for (int i = 0; i < BuiltInHostageKeys.Length; i++)
            candidates.Add(BuiltInHostageKeys[i]);
        for (int i = 0; i < BuiltInOtherSceneKeys.Length; i++)
            candidates.Add(BuiltInOtherSceneKeys[i]);

        LoadSectionFromFile("HOSTAGE", candidates);
        LoadSectionFromFile("OTHER_SCENES", candidates);

        var extra = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        SpawnTemplateCatalog.CollectAuthoringHostageKeys(extra);
        foreach (string key in extra)
            candidates.Add(key);

        var list = new List<string>();
        var seenCompact = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPrefab = new HashSet<int>();

        for (int i = 0; i < candidates.Count; i++)
            TryAddUniquePickerKey(candidates[i], list, seenCompact, seenPrefab);

        list.Sort(StringComparer.OrdinalIgnoreCase);
        cachedKeys = list.ToArray();
    }

    private static void TryAddUniquePickerKey(
        string rawKey,
        List<string> into,
        HashSet<string> seenCompact,
        HashSet<int> seenPrefab)
    {
        if (string.IsNullOrEmpty(rawKey))
            return;

        string key = SpawnTemplateCatalog.ResolveAuthoringCanonicalKey(rawKey);
        if (string.IsNullOrEmpty(key))
            key = rawKey.Trim();
        if (string.IsNullOrEmpty(key))
            return;

        if (IsListedHiddenKey(key) || IsListedHiddenKey(rawKey))
            return;
        if (IsSceneChildPickerName(key))
            return;
        if (!IsPickerKey(key) && !IsPickerKey(rawKey))
            return;
        if (SpawnAuthoringTrapCatalog.IsLethalPickerKey(key))
            return;

        if (SpawnTemplateCatalog.TryGetTrapTemplate(key, out GameObject prefab) && prefab != null)
        {
            int id = prefab.GetInstanceID();
            if (!seenPrefab.Add(id))
                return;
        }

        string compact = CompactPickerKey(key);
        if (!seenCompact.Add(compact))
            return;

        into.Add(key);
    }

    private static bool IsListedHiddenKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        for (int i = 0; i < HiddenPickerKeys.Length; i++)
        {
            if (SpawnTemplateCatalog.TemplateKeysMatch(key, HiddenPickerKeys[i]))
                return true;
        }

        return false;
    }

    private static bool IsSceneChildPickerName(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        if (key.IndexOf(' ') >= 0 || key.IndexOf('(') >= 0)
            return true;

        string compact = CompactPickerKey(key);
        return compact.StartsWith("spinegameobject", StringComparison.Ordinal);
    }

    private static string CompactPickerKey(string key)
    {
        return key.Trim().Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
    }

    private static void LoadSectionFromFile(string section, List<string> into)
    {
        try
        {
            string path = Path.Combine(Paths.PluginPath, "HellGateJson");
            path = Path.Combine(path, "HellGateSpawnPoint");
            path = Path.Combine(path, "SPAWN_TEMPLATE_KEYS.txt");
            if (!File.Exists(path))
                return;

            string[] lines = File.ReadAllLines(path);
            bool inSection = false;
            string want = "[" + section + "]";
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;
                if (line[0] == '[')
                {
                    inSection = string.Equals(line, want, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inSection)
                    continue;

                int hash = line.IndexOf('#');
                if (hash >= 0)
                    line = line.Substring(0, hash).Trim();
                if (line.Length > 0)
                    into.Add(line);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[SPAWN AUTHORING] Hostage catalog file read failed: " + ex.Message);
        }
    }
}
