using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Sole registry of HellGate spawn packs: <c>HellGateSpawn_*.txt</c> filename → zone hints + log prefix.
/// Zone refresh resolves packs via <see cref="HellGateLocationSpawnRefresh.GetActiveGameplayZone"/>
/// (<c>Idea_Nowscene</c>), never via altar <c>_re_Scenename</c>.
/// </summary>
internal static class HellGateSpawnSceneHints
{
    internal static readonly Dictionary<string, string[]> SpawnFileSceneHints = BuildSpawnFileSceneHints();

    private static readonly Dictionary<string, string> SpawnFileLogPrefixes = BuildLogPrefixes();

    private static Dictionary<string, string[]> BuildSpawnFileSceneHints()
    {
        var d = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        d["HellGateSpawn_FirstMap.txt"] = new[] { "FirstMap", "first" };
        d["HellGateSpawn_VillageMain.txt"] = new[] { "village_main", "village" };
        d["HellGateSpawn_ScapegoatEntrance.txt"] = new[] { "ScapegoatEntrance", "scapegoat" };
        d["HellGateSpawn_ParishChurch.txt"] = new[] { "Parishchurch", "parish", "parish church" };
        d["HellGateSpawn_UndergroundChurch.txt"] = new[] { "UndergroundChurch", "underground" };
        d["HellGateSpawn__inunderground church.txt"] = new[] { "InundergroundChurch", "Inunderground", "inunderground" };
        d["HellGateSpawn_nightless city C.txt"] = new[] { "InsomniaTownC", "nightless", "ightless" };
        d["HellGateSpawn_nightless city ragdum b.txt"] = new[] { "InsomniaTown", "ragdum" };
        d["HellGateSpawn_hidden Forest area.txt"] = new[] { "ForestOfRequiem" };
        d["HellGateSpawn_UndergroundLaboratory.txt"] = new[] { "UndergroundLaboratory", "laboratory" };
        d["HellGateSpawn_PilgrimageEntrance.txt"] = new[] { "PilgrimageEntrance", "pilgrimage" };
        d["HellGateSpawn_WhiteCathedral.txt"] = new[] { "WhiteCathedral", "white", "cathedral" };
        d["HellGateSpawn_WhiteCathedralGarden.txt"] = new[] { "WhiteCathedralGarden", "garden" };
        d["HellGateSpawn_WhiteCathedralRooftop.txt"] = new[] { "WhiteCathedralRooftop", "rooftop" };
        d["HellGateSpawn_Valley.txt"] = new[] { "Valley", "valley" };
        d["HellGateSpawn_LabGarden.txt"] = new[] { "LabGarden", "laboratory", "garden" };
        d["HellGateSpawn_Prison.txt"] = new[] { "Prison", "prison" };
        d["HellGateSpawn_nightless city under road.txt"] = new[] { "under road", "nightless", "InsomniaTownC", "ightless" };
        return d;
    }

    private static Dictionary<string, string> BuildLogPrefixes()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        d["HellGateSpawn_FirstMap.txt"] = "[HELLGATE SPAWN FM]";
        d["HellGateSpawn_VillageMain.txt"] = "[HELLGATE SPAWN VM]";
        d["HellGateSpawn_ScapegoatEntrance.txt"] = "[HELLGATE SPAWN SE]";
        d["HellGateSpawn_ParishChurch.txt"] = "[HELLGATE SPAWN PC]";
        d["HellGateSpawn_UndergroundChurch.txt"] = "[HELLGATE SPAWN UC]";
        d["HellGateSpawn__inunderground church.txt"] = "[HELLGATE SPAWN IUC]";
        d["HellGateSpawn_nightless city C.txt"] = "[HELLGATE SPAWN ITC]";
        d["HellGateSpawn_nightless city ragdum b.txt"] = "[HELLGATE SPAWN IT]";
        d["HellGateSpawn_hidden Forest area.txt"] = "[HELLGATE SPAWN FR]";
        d["HellGateSpawn_UndergroundLaboratory.txt"] = "[HELLGATE SPAWN UL]";
        d["HellGateSpawn_PilgrimageEntrance.txt"] = "[HELLGATE SPAWN PE]";
        d["HellGateSpawn_WhiteCathedral.txt"] = "[HELLGATE SPAWN WC]";
        d["HellGateSpawn_WhiteCathedralGarden.txt"] = "[HELLGATE SPAWN WCG]";
        d["HellGateSpawn_WhiteCathedralRooftop.txt"] = "[HELLGATE SPAWN WCR]";
        d["HellGateSpawn_Valley.txt"] = "[HELLGATE SPAWN VALLEY]";
        d["HellGateSpawn_LabGarden.txt"] = "[HELLGATE SPAWN LG]";
        d["HellGateSpawn_Prison.txt"] = "[HELLGATE SPAWN PRISON]";
        d["HellGateSpawn_nightless city under road.txt"] = "[HELLGATE SPAWN UR]";
        return d;
    }

    internal static string GetSpawnPointDirectory()
    {
        return Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "HellGateSpawnPoint");
    }

    /// <summary>
    /// Longest matching hint wins (e.g. InundergroundChurch over UndergroundChurch).
    /// </summary>
    internal static bool TryResolvePackForZone(string zoneName, out string configPath, out string logPrefix)
    {
        configPath = string.Empty;
        logPrefix = "[HELLGATE SPAWN]";

        if (string.IsNullOrEmpty(zoneName))
            return false;

        string bestFile = null;
        int bestHintLength = 0;

        foreach (KeyValuePair<string, string[]> kv in SpawnFileSceneHints)
        {
            string[] hints = kv.Value;
            if (hints == null)
                continue;

            for (int h = 0; h < hints.Length; h++)
            {
                string hint = hints[h];
                if (string.IsNullOrEmpty(hint))
                    continue;

                if (zoneName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (hint.Length <= bestHintLength)
                    continue;

                bestHintLength = hint.Length;
                bestFile = kv.Key;
            }
        }

        if (string.IsNullOrEmpty(bestFile))
            return false;

        configPath = Path.Combine(GetSpawnPointDirectory(), bestFile);
        if (SpawnFileLogPrefixes.TryGetValue(bestFile, out string prefix) && !string.IsNullOrEmpty(prefix))
            logPrefix = prefix;

        return true;
    }

    /// <returns>True when a pack file was resolved and <see cref="SpawnConfigExecutor.Execute"/> ran.</returns>
    internal static bool ExecutePackForZone(string zoneName)
    {
        if (!TryResolvePackForZone(zoneName, out string configPath, out string logPrefix))
            return false;

        EnemyPrefabRegistry.RefreshFromLoadedScenes();
        EnemyPrefabRegistry.Initialize();
        SpawnConfigExecutor.Execute(configPath, logPrefix);
        return true;
    }

    /// <summary>Batched pack execute for walk/load refresh (spreads Instantiate across frames).</summary>
    internal static IEnumerator ExecutePackForZoneBatched(
        string zoneName,
        bool skipCleanup = true,
        int batchPerFrame = 8,
        int refreshEpoch = -1)
    {
        if (!TryResolvePackForZone(zoneName, out string configPath, out string logPrefix))
            yield break;

        EnemyPrefabRegistry.RefreshFromLoadedScenes();
        EnemyPrefabRegistry.Initialize();
        yield return SpawnConfigExecutor.ExecuteBatched(
            configPath, logPrefix, skipCleanup, batchPerFrame, refreshEpoch);
    }

    internal static string JoinSceneHints(string[] sceneHints)
    {
        if (sceneHints == null || sceneHints.Length == 0)
            return string.Empty;
        return string.Join(";", sceneHints);
    }

    internal static bool IsAllowedEventFolder(string eventFolder, string[] allowed)
    {
        if (allowed == null || allowed.Length == 0)
            return true;
        for (int i = 0; i < allowed.Length; i++)
        {
            string a = allowed[i];
            if (a == null)
                continue;
            a = a.Trim();
            if (a.Length == 0)
                continue;
            if (string.Equals(a, eventFolder, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    internal static bool ReSceneMatchesHints(string reSceneName, string[] hints)
    {
        if (string.IsNullOrEmpty(reSceneName) || hints == null || hints.Length == 0)
            return false;

        for (int i = 0; i < hints.Length; i++)
        {
            string hint = hints[i];
            if (string.IsNullOrEmpty(hint))
                continue;

            if (reSceneName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    internal static bool HasHellGatePack(string reSceneName)
    {
        if (string.IsNullOrEmpty(reSceneName))
            return false;

        foreach (KeyValuePair<string, string[]> kv in SpawnFileSceneHints)
        {
            if (ReSceneMatchesHints(reSceneName, kv.Value))
                return true;
        }

        return false;
    }
}
