using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using NoREroMod.Patches.HellTraps;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// F11 Trap / LethalTrap picker: one canonical key per unique prefab.
/// Aliases stay spawnable from txt; they are not listed here.
/// </summary>
internal static class SpawnAuthoringTrapCatalog
{
    private static readonly string[] LethalKeys =
    {
        "lethal_magictrap",
        "lethal_cocoontrap",
        "lightningTrap_button"
    };

    /// <summary>Canonical trap keys shown in F11 (aliases resolved onto these).</summary>
    private static readonly string[] BuiltInTrapKeys =
    {
        "trapthreadofspider",
        "woodwana",
        "trap_button",
        "trapshot",
        "spearthrowtrap",
        "magictrap",
        "magictrapcreateobject",
        "picturetrap",
        "ivy_trap",
        "ivy_monster",
        "cocoontrap",
        "cocoontrapnot",
        "blackoozetrap",
        "blackoozetraptypeb",
        "trapnormal",
        "spiketrap",
        "impactdamage",
        "impactdamagebox",
        "ironmaidendamage",
        "tent_ironmaiden",
        "trapmachine",
        "trap_mokubaenemy",
        "pictureeronon",
        "rosewarm",
        "wallhip",
        "tubo2"
    };

    /// <summary>Never show in Trap / Lethal — junk, broken, or owned by another tab.</summary>
    private static readonly string[] HiddenPickerKeys =
    {
        "help",
        "meatshieldhelp",
        "npcslaveenable",
        "npcslaveflag",
        "wavespike",
        "wavespikeguard",
        "cocooncreat",
        "cocoonmanstart",
        "trap_hari",
        "traphari",
        "trap",
        "breakobjct",
        "back",
        "chaincowslavespine",
        "cowslavespine",
        "cow1",
        "scapegoat_slave",
        "villageslave",
        "npcslave",
        "inchurchslave",
        "inchchurchslave"
    };

    private static string[] cachedTrap = new string[0];
    private static string[] cachedLethal = new string[0];
    private static float nextRebuildUnscaledTime;

    internal static string[] GetTrapKeys()
    {
        EnsureBuilt();
        return cachedTrap;
    }

    internal static string[] GetLethalKeys()
    {
        EnsureBuilt();
        return cachedLethal;
    }

    internal static void Invalidate()
    {
        cachedTrap = new string[0];
        cachedLethal = new string[0];
        nextRebuildUnscaledTime = 0f;
    }

    internal static bool IsReady(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        return SpawnTemplateCatalog.HasTemplate(key) || SpawnTemplateCatalog.TryCacheFromResources(key);
    }

    internal static bool IsLethalPickerKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        return LethalMagicTrapPaths.IsLethalMagicTrapKey(key) ||
               LethalCocoonTrapPaths.IsLethalCocoonTrapKey(key) ||
               LethalLightningTrapPaths.IsLethalLightningTrapKey(key);
    }

    private static void EnsureBuilt()
    {
        if ((cachedTrap.Length > 0 || cachedLethal.Length > 0) &&
            Time.unscaledTime < nextRebuildUnscaledTime)
            return;
        Rebuild();
    }

    private static void Rebuild()
    {
        nextRebuildUnscaledTime = Time.unscaledTime + 2f;

        var candidates = new List<string>();
        for (int i = 0; i < BuiltInTrapKeys.Length; i++)
            candidates.Add(BuiltInTrapKeys[i]);

        LoadSectionFromFile("TRAP", candidates);
        CollectCachedTrapKeys(candidates);

        var trapList = new List<string>();
        var seenCompact = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPrefab = new HashSet<int>();

        for (int i = 0; i < candidates.Count; i++)
            TryAddUniquePickerKey(candidates[i], trapList, seenCompact, seenPrefab, allowLethal: false);

        trapList.Sort(StringComparer.OrdinalIgnoreCase);
        cachedTrap = trapList.ToArray();

        cachedLethal = (string[])LethalKeys.Clone();
    }

    private static void CollectCachedTrapKeys(List<string> into)
    {
        var extra = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        SpawnTemplateCatalog.CollectAuthoringTrapKeys(extra);
        foreach (string key in extra)
            into.Add(key);
    }

    private static void TryAddUniquePickerKey(
        string rawKey,
        List<string> into,
        HashSet<string> seenCompact,
        HashSet<int> seenPrefab,
        bool allowLethal)
    {
        if (string.IsNullOrEmpty(rawKey))
            return;

        string key = SpawnTemplateCatalog.ResolveAuthoringCanonicalKey(rawKey);
        if (string.IsNullOrEmpty(key))
            return;

        if (IsListedHiddenKey(key) || IsListedHiddenKey(rawKey))
            return;
        if (IsSceneChildPickerName(key))
            return;
        if (SpawnTemplateCatalog.IsHostageTemplateKey(key))
            return;
        if (SpawnAuthoringHostageCatalog.IsPickerKey(key) || SpawnAuthoringHostageCatalog.IsPickerKey(rawKey))
            return;
        if (SpawnDecorCatalog.IsKnownDecorKey(key) || SpawnDecorCatalog.IsKnownDecorKey(rawKey))
            return;

        bool lethal = IsLethalPickerKey(key) || IsLethalPickerKey(rawKey);
        if (lethal && !allowLethal)
            return;
        if (!lethal && allowLethal)
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

    /// <summary>Cached child GO names, not spawn keys (spaces / parens / spine leftovers).</summary>
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
            Plugin.Log?.LogWarning("[SPAWN AUTHORING] Trap catalog file read failed: " + ex.Message);
        }
    }
}
