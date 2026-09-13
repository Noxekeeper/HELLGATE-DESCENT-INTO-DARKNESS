using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Builds the F11 Enemies picker list: registry mappings + enemy disk-cache keys,
/// minus <c>SPAWN_AUTHORING_ENEMY_EXCLUDE.txt</c> and a small built-in trap-like filter.
/// </summary>
internal static class SpawnAuthoringEnemyCatalog
{
    private const string ExcludeFileName = "SPAWN_AUTHORING_ENEMY_EXCLUDE.txt";

    /// <summary>Keys that live in <see cref="EnemyPrefabRegistry"/> but are not combat enemies for authoring.</summary>
    private static readonly string[] BuiltInExclude =
    {
        "WaveSpike",
        "WaveSpikeGuard",
        "TrapNormal",
        "WoodWana",
        "IronmaidenDamage",
        "Cocoonspear",
        "Cocoonman"
    };

    /// <summary>
    /// Extra combat keys that are prefab names (not the broken registry label).
    /// </summary>
    private static readonly string[] BuiltInExtra =
    {
        "CocoonmanStart"
    };

    /// <summary>
    /// Known broken / non-combat authoring keys — stay visible but marked unavailable.
    /// </summary>
    private static readonly string[] BuiltInUnavailable =
    {
        "GobBigAlter",
        "Sheepheaddemon"
    };

    private static string[] cachedList = new string[0];
    private static float nextRebuildUnscaledTime;
    private static HashSet<string> fileExclude =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> unavailableSet =
        new HashSet<string>(BuiltInUnavailable, StringComparer.OrdinalIgnoreCase);

    internal static bool IsUnavailable(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        return unavailableSet.Contains(key);
    }

    internal static string[] GetEnemyKeys()
    {
        if (cachedList.Length == 0 || Time.unscaledTime >= nextRebuildUnscaledTime)
            Rebuild();
        return cachedList;
    }

    internal static void Invalidate()
    {
        cachedList = new string[0];
        nextRebuildUnscaledTime = 0f;
    }

    private static void Rebuild()
    {
        nextRebuildUnscaledTime = Time.unscaledTime + 2f;
        ReloadFileExclude();

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string[] registry = EnemyPrefabRegistry.GetAvailableEnemyTypes();
        for (int i = 0; i < registry.Length; i++)
        {
            if (!string.IsNullOrEmpty(registry[i]))
                set.Add(registry[i]);
        }

        EnemyPrefabDiskCache.CollectKeys(set);

        for (int i = 0; i < BuiltInExtra.Length; i++)
        {
            if (!string.IsNullOrEmpty(BuiltInExtra[i]))
                set.Add(BuiltInExtra[i]);
        }

        ApplyExcludes(set);

        var list = new List<string>(set.Count);
        foreach (string key in set)
            list.Add(key);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        cachedList = list.ToArray();
    }

    private static void ApplyExcludes(HashSet<string> set)
    {
        for (int i = 0; i < BuiltInExclude.Length; i++)
            set.Remove(BuiltInExclude[i]);

        if (fileExclude.Count == 0)
            return;

        var remove = new List<string>();
        foreach (string key in set)
        {
            if (fileExclude.Contains(key))
                remove.Add(key);
        }

        for (int i = 0; i < remove.Count; i++)
            set.Remove(remove[i]);
    }

    private static void ReloadFileExclude()
    {
        fileExclude.Clear();
        try
        {
            string path = Path.Combine(Paths.PluginPath, "HellGateJson");
            path = Path.Combine(path, "HellGateSpawnPoint");
            path = Path.Combine(path, ExcludeFileName);
            if (!File.Exists(path))
                return;

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;
                int hash = line.IndexOf('#');
                if (hash >= 0)
                    line = line.Substring(0, hash).Trim();
                if (line.Length > 0)
                    fileExclude.Add(line);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN AUTHORING] Failed to read {ExcludeFileName}: {ex.Message}");
        }
    }
}
