using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx;
using NoREroMod.Systems.CombatAi.Factions;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.OffspringArchetype;

/// <summary>
/// Loads per-faction weighted offspring archetype pools from JSON.
/// Falls back to built-in defaults when the file is missing or invalid.
/// </summary>
internal static class OffspringArchetypeCatalog
{
    internal static string FallbackArchetype => _fallbackArchetype;
    private static string _fallbackArchetype = "Mafiamuscle";
    private const string CatalogFileName = "OffspringArchetypes.json";

    private static readonly Dictionary<string, List<WeightedEntry>> PoolsByFactionKey =
        new Dictionary<string, List<WeightedEntry>>(StringComparer.OrdinalIgnoreCase);

    private static bool _loaded;

    private sealed class WeightedEntry
    {
        internal string Archetype;
        internal int Weight;
    }

    internal static void EnsureLoaded()
    {
        if (_loaded)
            return;

        Load();
        _loaded = true;
    }

    internal static string RollArchetype(int factionSource)
    {
        EnsureLoaded();

        string factionKey = ResolveFactionKey(factionSource);
        if (!PoolsByFactionKey.TryGetValue(factionKey, out List<WeightedEntry> pool) || pool == null || pool.Count == 0)
            return FallbackArchetype;

        int total = 0;
        for (int i = 0; i < pool.Count; i++)
            total += pool[i].Weight;

        if (total <= 0)
            return FallbackArchetype;

        int roll = UnityEngine.Random.Range(0, total);
        int cursor = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            cursor += pool[i].Weight;
            if (roll < cursor)
                return pool[i].Archetype;
        }

        return pool[pool.Count - 1].Archetype;
    }

    private static string ResolveFactionKey(int factionSource)
    {
        switch (PregnancyConfig.NormalizeSourceFaction(factionSource))
        {
            case FactionIds.Bandits: return "bandits";
            case FactionIds.Church: return "church";
            case FactionIds.Demons: return "demons";
            case FactionIds.Mafia: return "mafia";
            case FactionIds.Undead: return "undead";
            case FactionIds.Monsters: return "monsters";
            default: return string.Empty;
        }
    }

    private static void Load()
    {
        PoolsByFactionKey.Clear();
        _fallbackArchetype = "Mafiamuscle";
        InstallBuiltInDefaults();

        string path = ResolveCatalogPath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Plugin.Log?.LogInfo("[Pregnancy.Archetype] Catalog file not found; using built-in defaults");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            string fallback = ReadJsonString(json, "fallbackArchetype");
            if (!string.IsNullOrEmpty(fallback))
                _fallbackArchetype = fallback;

            string factionsBlock = ReadJsonObjectBlock(json, "factions");
            if (string.IsNullOrEmpty(factionsBlock))
                return;

            foreach (KeyValuePair<string, string> factionEntry in ReadNamedObjectBlocks(factionsBlock))
            {
                string weightsBlock = ReadJsonObjectBlock(factionEntry.Value, "weights");
                if (string.IsNullOrEmpty(weightsBlock))
                    continue;

                List<WeightedEntry> entries = ParseWeightsObject(weightsBlock);
                if (entries.Count > 0)
                    PoolsByFactionKey[factionEntry.Key] = entries;
            }

            Plugin.Log?.LogInfo($"[Pregnancy.Archetype] Loaded catalog from {path} ({PoolsByFactionKey.Count} faction pools)");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Pregnancy.Archetype] Failed to load catalog; using built-in defaults: " + ex.Message);
            PoolsByFactionKey.Clear();
            _fallbackArchetype = "Mafiamuscle";
            InstallBuiltInDefaults();
        }
    }

    private static void InstallBuiltInDefaults()
    {
        SetPool("bandits", "TouzokuNormal", "TouzokuAxe");
        SetPool("mafia", "MafiaBossCustom", "Mafia");
        SetPool("church", "Sisterknight", "PrisonOfficer");
        SetPool("demons", "Mutude", "Goblin");
        SetPool("undead", "Undead");
        SetPool("monsters", "BlackOoze");
    }

    private static void SetPool(string factionKey, params string[] archetypes)
    {
        var entries = new List<WeightedEntry>(archetypes.Length);
        for (int i = 0; i < archetypes.Length; i++)
        {
            entries.Add(new WeightedEntry
            {
                Archetype = archetypes[i],
                Weight = 1
            });
        }

        PoolsByFactionKey[factionKey] = entries;
    }

    private static List<WeightedEntry> ParseWeightsObject(string weightsBlock)
    {
        var entries = new List<WeightedEntry>();
        MatchCollection matches = Regex.Matches(
            weightsBlock,
            "\"((?:[^\"\\\\]|\\\\.)*)\"\\s*:\\s*(-?\\d+)",
            RegexOptions.CultureInvariant);

        for (int i = 0; i < matches.Count; i++)
        {
            Match match = matches[i];
            string archetype = match.Groups[1].Value;
            if (string.IsNullOrEmpty(archetype))
                continue;

            if (!int.TryParse(match.Groups[2].Value, out int weight) || weight <= 0)
                continue;

            entries.Add(new WeightedEntry { Archetype = archetype, Weight = weight });
        }

        return entries;
    }

    private static Dictionary<string, string> ReadNamedObjectBlocks(string objectBody)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int cursor = 0;
        while (cursor < objectBody.Length)
        {
            Match keyMatch = Regex.Match(objectBody.Substring(cursor), "\"((?:[^\"\\\\]|\\\\.)*)\"\\s*:", RegexOptions.CultureInvariant);
            if (!keyMatch.Success)
                break;

            string key = keyMatch.Groups[1].Value;
            int valueStart = cursor + keyMatch.Index + keyMatch.Length;
            while (valueStart < objectBody.Length && char.IsWhiteSpace(objectBody[valueStart]))
                valueStart++;

            if (valueStart >= objectBody.Length || objectBody[valueStart] != '{')
                break;

            int valueEnd = MatchBracket(objectBody, valueStart, '{', '}');
            if (valueEnd < 0)
                break;

            result[key] = objectBody.Substring(valueStart + 1, valueEnd - valueStart - 1);
            cursor = valueEnd + 1;
        }

        return result;
    }

    private static string ReadJsonObjectBlock(string json, string key)
    {
        Match keyMatch = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:", RegexOptions.CultureInvariant);
        if (!keyMatch.Success)
            return null;

        int braceStart = json.IndexOf('{', keyMatch.Index);
        if (braceStart < 0)
            return null;

        int braceEnd = MatchBracket(json, braceStart, '{', '}');
        if (braceEnd < 0)
            return null;

        return json.Substring(braceStart + 1, braceEnd - braceStart - 1);
    }

    private static string ReadJsonString(string json, string key)
    {
        Match match = Regex.Match(
            json,
            "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static int MatchBracket(string text, int openIndex, char open, char close)
    {
        int depth = 0;
        bool inString = false;
        bool escape = false;
        for (int i = openIndex; i < text.Length; i++)
        {
            char ch = text[i];
            if (inString)
            {
                if (escape)
                    escape = false;
                else if (ch == '\\')
                    escape = true;
                else if (ch == '"')
                    inString = false;
                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == open)
                depth++;
            else if (ch == close)
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    private static string ResolveCatalogPath()
    {
        try
        {
            string dir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "Pregnancy");
            return Path.Combine(dir, CatalogFileName);
        }
        catch
        {
            string gameRoot = Application.dataPath;
            if (gameRoot.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
                gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

            string dir = Path.Combine(
                Path.Combine(Path.Combine(Path.Combine(gameRoot, "BepInEx"), "plugins"), "HellGateJson"),
                "Pregnancy");
            return Path.Combine(dir, CatalogFileName);
        }
    }
}
