using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Spawn;
using UnityEngine;
using Random = UnityEngine.Random;

namespace NoREroMod.Systems.Pregnancy.ShelterAttack;

/// <summary>
/// Wave roster tiers loaded from
/// <c>HellGateJson/Pregnancy/Shelter event/{Low,Medium,Big}GroupWaves.json</c>
/// and selected by hideout child count.
/// </summary>
internal static class ShelterAttackWaves
{
    internal enum ThreatTier
    {
        Low = 0,
        Mid = 1,
        High = 2
    }

    private const string ShelterEventFolder = "Shelter event";
    private const string LowFileName = "LowGroupWaves.json";
    private const string MidFileName = "MediumGroupWaves.json";
    private const string HighFileName = "BigGroupWaves.json";

    private static readonly Dictionary<ThreatTier, Dictionary<string, FactionWaveConfig>> _tiers =
        new Dictionary<ThreatTier, Dictionary<string, FactionWaveConfig>>();

    private static bool _loadAttempted;
    private static bool _loadSucceeded;

    internal sealed class WaveSpawnEntry
    {
        public string Enemy;
        public int Count = 1;
        public int CountMin = -1;
        public int CountMax = -1;
        public float Chance = 1f;
        public List<string> RandomPickOne = new List<string>();
    }

    private sealed class FactionWaveConfig
    {
        public readonly List<List<WaveSpawnEntry>> Waves = new List<List<WaveSpawnEntry>>();
        public readonly List<List<WaveSpawnEntry>> FinalRandomPacks = new List<List<WaveSpawnEntry>>();

        public int TotalWaveCount => Waves.Count + (FinalRandomPacks.Count > 0 ? 1 : 0);
    }

    /// <summary>
    /// 1–3 children → Low, 4–6 → Mid, 7+ → High.
    /// </summary>
    internal static ThreatTier ResolveThreatTier(int childrenInHideout)
    {
        if (childrenInHideout <= 3)
            return ThreatTier.Low;
        if (childrenInHideout <= 6)
            return ThreatTier.Mid;
        return ThreatTier.High;
    }

    internal static ThreatTier GetActiveThreatTier()
    {
        if (ShelterAttackState.IsEventActive && ShelterAttackState.ThreatTierLocked)
            return ShelterAttackState.ThreatTier;

        int children = PregnancySlotStore.GetAliveChildrenInHideout().Count;
        return ResolveThreatTier(children);
    }

    internal static string GetTierFileName(ThreatTier tier)
    {
        return tier switch
        {
            ThreatTier.Low => LowFileName,
            ThreatTier.Mid => MidFileName,
            _ => HighFileName
        };
    }

    internal static string GetFactionKey(int factionId)
    {
        switch (factionId)
        {
            case FactionIds.Bandits: return "bandits";
            case FactionIds.Church: return "church";
            case FactionIds.Demons: return "demons";
            case FactionIds.Undead: return "undead";
            case FactionIds.Monsters: return "monsters";
            case FactionIds.Mafia: return "mafia";
            default: return "bandits";
        }
    }

    internal static int GetTotalWaveCount(int factionId)
    {
        EnsureLoaded();
        ThreatTier tier = GetActiveThreatTier();
        if (!_tiers.TryGetValue(tier, out Dictionary<string, FactionWaveConfig> factions))
            return 0;

        string key = GetFactionKey(factionId);
        if (!factions.TryGetValue(key, out FactionWaveConfig config))
            return 0;

        return config.TotalWaveCount;
    }

    /// <param name="waveIndex">0-based wave index.</param>
    internal static List<string> BuildSpawnQueue(int factionId, int waveIndex)
    {
        EnsureLoaded();

        var queue = new List<string>();
        if (!_loadSucceeded)
            return queue;

        ThreatTier tier = GetActiveThreatTier();
        if (!_tiers.TryGetValue(tier, out Dictionary<string, FactionWaveConfig> factions))
        {
            Plugin.Log?.LogError($"[Pregnancy.ShelterAttack] Wave tier \"{tier}\" not loaded.");
            return queue;
        }

        string factionKey = GetFactionKey(factionId);
        if (!factions.TryGetValue(factionKey, out FactionWaveConfig config))
        {
            Plugin.Log?.LogError(
                $"[Pregnancy.ShelterAttack] {GetTierFileName(tier)}: faction \"{factionKey}\" not found.");
            return queue;
        }

        List<WaveSpawnEntry> entries = ResolveWaveEntries(config, waveIndex);
        if (entries == null || entries.Count == 0)
        {
            Plugin.Log?.LogWarning(
                $"[Pregnancy.ShelterAttack] {GetTierFileName(tier)}: no entries for faction \"{factionKey}\" wave {waveIndex + 1}.");
            return queue;
        }

        for (int i = 0; i < entries.Count; i++)
            AppendEntrySpawns(queue, entries[i]);

        return queue;
    }

    /// <summary>Removes trap/template keys that are not enemy prefabs (e.g. tubo2 vase) so a wave cannot soft-lock.</summary>
    internal static void FilterUnspawnableEntries(List<string> queue, int waveIndex)
    {
        if (queue == null || queue.Count == 0)
            return;

        EnemyPrefabRegistry.Initialize();
        int removed = 0;

        for (int i = queue.Count - 1; i >= 0; i--)
        {
            string enemy = queue[i];
            if (string.IsNullOrEmpty(enemy))
            {
                queue.RemoveAt(i);
                removed++;
                continue;
            }

            if (EnemyPrefabRegistry.TryGetPrefab(enemy, out _))
                continue;

            queue.RemoveAt(i);
            removed++;
            Plugin.Log?.LogWarning(
                $"[Pregnancy.ShelterAttack] Wave {waveIndex + 1}: skipped unspawnable roster entry '{enemy}' (not an enemy prefab).");
        }

        if (removed > 0 && queue.Count == 0)
        {
            Plugin.Log?.LogError(
                $"[Pregnancy.ShelterAttack] Wave {waveIndex + 1}: all {removed} queued spawn(s) were invalid — wave will auto-clear.");
        }
    }

    private static List<WaveSpawnEntry> ResolveWaveEntries(FactionWaveConfig config, int waveIndex)
    {
        if (waveIndex < config.Waves.Count)
            return config.Waves[waveIndex];

        if (config.FinalRandomPacks.Count > 0 && waveIndex == config.Waves.Count)
        {
            // System.Random avoids UnityEngine.Random seed quirks that can bias short ranges.
            int packIndex = System.Math.Abs(Guid.NewGuid().GetHashCode()) % config.FinalRandomPacks.Count;
            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            {
                Plugin.Log?.LogInfo(
                    $"[Pregnancy.ShelterAttack] Final wave pack pick {packIndex + 1}/{config.FinalRandomPacks.Count}.");
            }
            return config.FinalRandomPacks[packIndex];
        }

        return null;
    }

    private static void AppendEntrySpawns(List<string> queue, WaveSpawnEntry entry)
    {
        if (entry == null)
            return;

        float chance = Mathf.Clamp01(entry.Chance);
        if (chance < 1f && Random.value > chance)
            return;

        int count = ResolveSpawnCount(entry);
        if (count <= 0)
            return;

        for (int i = 0; i < count; i++)
        {
            string enemy = ResolveEnemyType(entry);
            if (!string.IsNullOrEmpty(enemy))
                queue.Add(enemy);
        }
    }

    private static int ResolveSpawnCount(WaveSpawnEntry entry)
    {
        if (entry.CountMin >= 0 && entry.CountMax >= entry.CountMin)
            return Random.Range(entry.CountMin, entry.CountMax + 1);

        return Mathf.Max(0, entry.Count);
    }

    private static string ResolveEnemyType(WaveSpawnEntry entry)
    {
        if (entry.RandomPickOne != null && entry.RandomPickOne.Count > 0)
        {
            int index = Random.Range(0, entry.RandomPickOne.Count);
            return entry.RandomPickOne[index];
        }

        return entry.Enemy;
    }

    private static void EnsureLoaded()
    {
        if (_loadAttempted)
            return;

        _loadAttempted = true;
        LoadAllTiersFromDisk();
    }

    private static void LoadAllTiersFromDisk()
    {
        _tiers.Clear();
        _loadSucceeded = false;

        bool any = false;
        any |= TryLoadTier(ThreatTier.Low, LowFileName);
        any |= TryLoadTier(ThreatTier.Mid, MidFileName);
        any |= TryLoadTier(ThreatTier.High, HighFileName);

        if (!any)
        {
            Plugin.Log?.LogError("[Pregnancy.ShelterAttack] No wave group JSON files loaded.");
            return;
        }

        _loadSucceeded = true;
    }

    private static bool TryLoadTier(ThreatTier tier, string fileName)
    {
        try
        {
            string path = Path.Combine(
                Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "Pregnancy"),
                Path.Combine(ShelterEventFolder, fileName));

            if (!File.Exists(path))
            {
                Plugin.Log?.LogError($"[Pregnancy.ShelterAttack] {fileName} not found: {path}");
                return false;
            }

            string json = File.ReadAllText(path);
            var factions = new Dictionary<string, FactionWaveConfig>(StringComparer.OrdinalIgnoreCase);
            ParseRootInto(json, factions);

            if (factions.Count == 0)
            {
                Plugin.Log?.LogError($"[Pregnancy.ShelterAttack] {fileName} loaded but contains no faction sections.");
                return false;
            }

            _tiers[tier] = factions;
            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            {
                Plugin.Log?.LogInfo(
                    $"[Pregnancy.ShelterAttack] Loaded {fileName} ({factions.Count} faction(s), tier={tier}) from {path}");
            }
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Pregnancy.ShelterAttack] Failed to load {fileName}: {ex.Message}");
            return false;
        }
    }

    private static void ParseRootInto(string json, Dictionary<string, FactionWaveConfig> factions)
    {
        ParseFaction(json, "bandits", factions);
        ParseFaction(json, "church", factions);
        ParseFaction(json, "demons", factions);
        ParseFaction(json, "undead", factions);
        ParseFaction(json, "monsters", factions);
        ParseFaction(json, "mafia", factions);
    }

    private static void ParseFaction(string json, string factionKey, Dictionary<string, FactionWaveConfig> factions)
    {
        string block = ShelterAttackWavesJsonParser.ReadObjectBlock(json, factionKey);
        if (string.IsNullOrEmpty(block))
            return;

        var config = new FactionWaveConfig();
        string wrapped = "{" + block + "}";

        List<string> waveBodies = ShelterAttackWavesJsonParser.ReadObjectArray(wrapped, "waves");
        for (int i = 0; i < waveBodies.Count; i++)
            config.Waves.Add(ParseEntryList(waveBodies[i]));

        List<string> packBodies = ShelterAttackWavesJsonParser.ReadObjectArray(wrapped, "finalRandomPacks");
        for (int i = 0; i < packBodies.Count; i++)
            config.FinalRandomPacks.Add(ParseEntryList(packBodies[i]));

        if (config.Waves.Count == 0 && config.FinalRandomPacks.Count == 0)
            return;

        factions[factionKey] = config;
    }

    private static List<WaveSpawnEntry> ParseEntryList(string waveBody)
    {
        var entries = new List<WaveSpawnEntry>();
        List<string> entryBodies = ShelterAttackWavesJsonParser.ReadObjectArray("{" + waveBody + "}", "entries");
        for (int i = 0; i < entryBodies.Count; i++)
        {
            WaveSpawnEntry entry = ParseEntry(entryBodies[i]);
            if (entry != null)
                entries.Add(entry);
        }

        return entries;
    }

    private static WaveSpawnEntry ParseEntry(string body)
    {
        if (string.IsNullOrEmpty(body))
            return null;

        string wrapped = "{" + body + "}";
        var entry = new WaveSpawnEntry
        {
            Enemy = ShelterAttackWavesJsonParser.ReadString(wrapped, "enemy"),
            Count = ShelterAttackWavesJsonParser.ReadInt(wrapped, "count", 1),
            CountMin = ShelterAttackWavesJsonParser.ReadInt(wrapped, "countMin", -1),
            CountMax = ShelterAttackWavesJsonParser.ReadInt(wrapped, "countMax", -1),
            Chance = ShelterAttackWavesJsonParser.ReadFloat(wrapped, "chance", 1f),
            RandomPickOne = ShelterAttackWavesJsonParser.ReadStringArray(wrapped, "randomPickOne")
        };

        if (string.IsNullOrEmpty(entry.Enemy) && entry.RandomPickOne.Count == 0)
            return null;

        return entry;
    }
}
