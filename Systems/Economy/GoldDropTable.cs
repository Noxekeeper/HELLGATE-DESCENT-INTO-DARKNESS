using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using NoREroMod.Systems.CombatAi.Factions;
using UnityEngine;

namespace NoREroMod.Systems.Economy;

/// <summary>
/// Loads <c>HellGateJson/Economic/GoldDropTable.json</c> and resolves the gold-drop
/// rule for a given enemy. Resolution order: <see cref="GoldDropTableConfig.EnemyOverrides"/>
/// keyed by <c>gameObject.name</c> first, then by <c>GetType().Name</c>, finally
/// <see cref="GoldDropTableConfig.FactionRules"/> keyed by faction id.
///
/// Matching by <c>gameObject.name</c> is required because HellGate custom enemies
/// (MafiaBossCustom, BigoniBrother, Wolf, biscord, …) can share a runtime type with
/// their vanilla parent class but are renamed in <c>SpawnConfigExecutor.SpawnSingle</c>.
/// </summary>
internal static class GoldDropTable
{
    private const float ReloadIntervalSec = 2f;
    private static GoldDropTableConfig _cached;
    private static float _lastLoadTime = -999f;
    private static bool _firstLoadLogged;

    public static GoldDropTableConfig Get()
    {
        if (_cached != null && Time.realtimeSinceStartup - _lastLoadTime < ReloadIntervalSec)
            return _cached;

        _cached = LoadFromFile();
        _lastLoadTime = Time.realtimeSinceStartup;
        return _cached;
    }

    public static string GetConfigPath()
    {
        string dir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "Economic");
        return Path.Combine(dir, "GoldDropTable.json");
    }

    private static GoldDropTableConfig LoadFromFile()
    {
        string path = GetConfigPath();
        if (!File.Exists(path))
            return GoldDropTableConfig.Default();

        try
        {
            string json = File.ReadAllText(path);
            if (!string.IsNullOrEmpty(json) && json[0] == '\uFEFF')
                json = json.TrimStart('\uFEFF');

            // Same reason as EconomicConfig: don't use JsonUtility on PowerShell-formatted JSON.
            GoldDropTableConfig parsed = ParseConfig(json);
            if (parsed == null || parsed.FactionRules == null || parsed.FactionRules.Length == 0)
            {
                Plugin.Log?.LogWarning("[Economic] GoldDropTable.json parsed empty FactionRules; using built-in defaults.");
                return GoldDropTableConfig.Default();
            }
            if (EconomicConfig.DebugLogging && !_firstLoadLogged)
            {
                _firstLoadLogged = true;
                Plugin.Log?.LogInfo($"[Economic] GoldDropTable.json loaded: factionRules={parsed.FactionRules.Length} overrides={(parsed.EnemyOverrides!=null?parsed.EnemyOverrides.Length:0)} excludeBiscord={parsed.ExcludeBiscord}");
            }
            return parsed;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Economic] Failed to load GoldDropTable.json: " + ex.Message + ". Using defaults.");
            return GoldDropTableConfig.Default();
        }
    }

    private static GoldDropTableConfig ParseConfig(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        GoldDropTableConfig cfg = new GoldDropTableConfig();
        cfg.ExcludeBiscord = EconomicJsonParser.ReadBool(json, "ExcludeBiscord", true);

        string settingsBody = EconomicJsonParser.ReadObjectBlock(json, "Settings");
        if (!string.IsNullOrEmpty(settingsBody))
        {
            cfg.Settings.ApplyDifficultyScaling = EconomicJsonParser.ReadBool(settingsBody, "ApplyDifficultyScaling", cfg.Settings.ApplyDifficultyScaling);
            cfg.Settings.MinAmountFloor = EconomicJsonParser.ReadInt(settingsBody, "MinAmountFloor", cfg.Settings.MinAmountFloor);
        }

        var factionRows = EconomicJsonParser.ReadObjectArray(json, "FactionRules");
        if (factionRows != null && factionRows.Count > 0)
        {
            GoldDropFactionRule[] arr = new GoldDropFactionRule[factionRows.Count];
            for (int i = 0; i < factionRows.Count; i++)
            {
                arr[i] = new GoldDropFactionRule
                {
                    Faction = EconomicJsonParser.ReadString(factionRows[i], "Faction", null),
                    Chance = EconomicJsonParser.ReadFloat(factionRows[i], "Chance", 0f),
                    MinAmount = EconomicJsonParser.ReadInt(factionRows[i], "MinAmount", 0),
                    MaxAmount = EconomicJsonParser.ReadInt(factionRows[i], "MaxAmount", 0)
                };
            }
            cfg.FactionRules = arr;
        }

        var overrideRows = EconomicJsonParser.ReadObjectArray(json, "EnemyOverrides");
        if (overrideRows != null && overrideRows.Count > 0)
        {
            GoldDropEnemyOverride[] arr = new GoldDropEnemyOverride[overrideRows.Count];
            for (int i = 0; i < overrideRows.Count; i++)
            {
                arr[i] = new GoldDropEnemyOverride
                {
                    EnemyType = EconomicJsonParser.ReadString(overrideRows[i], "EnemyType", null),
                    Chance = EconomicJsonParser.ReadFloat(overrideRows[i], "Chance", 0f),
                    MinAmount = EconomicJsonParser.ReadInt(overrideRows[i], "MinAmount", 0),
                    MaxAmount = EconomicJsonParser.ReadInt(overrideRows[i], "MaxAmount", 0)
                };
            }
            cfg.EnemyOverrides = arr;
        }

        return cfg;
    }

    /// <summary>
    /// Resolve the rule for an enemy or returns null if no drop should happen.
    /// </summary>
    public static GoldRule Resolve(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null)
            return null;

        GoldDropTableConfig cfg = Get();
        if (cfg.ExcludeBiscord && IsBiscordRuntimeObject(enemy.gameObject))
            return null;

        // 1. Enemy overrides — match by gameObject.name first (HellGate custom names like
        //    MafiaBossCustom / Wolf are renamed in SpawnConfigExecutor.SpawnSingle), then by
        //    runtime type name (vanilla classes). Unity's "(Clone)" suffix on instantiated
        //    prefabs is stripped so JSON entries can use clean prefab names.
        string goName = StripCloneSuffix(enemy.gameObject.name);
        string typeName = enemy.GetType().Name ?? string.Empty;

        if (cfg.EnemyOverrides != null)
        {
            for (int i = 0; i < cfg.EnemyOverrides.Length; i++)
            {
                GoldDropEnemyOverride row = cfg.EnemyOverrides[i];
                if (row == null || string.IsNullOrEmpty(row.EnemyType))
                    continue;
                if (string.Equals(row.EnemyType, goName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(row.EnemyType, typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return GoldRule.From(row.Chance, row.MinAmount, row.MaxAmount);
                }
            }
        }

        // 2. Faction rule.
        int factionId = EnemyFactionRuntime.GetFaction(enemy.gameObject);
        string factionKey = EconomicFactionUtil.FactionIdToKey(factionId);
        if (cfg.FactionRules != null)
        {
            for (int i = 0; i < cfg.FactionRules.Length; i++)
            {
                GoldDropFactionRule row = cfg.FactionRules[i];
                if (row == null || string.IsNullOrEmpty(row.Faction))
                    continue;
                if (string.Equals(row.Faction, factionKey, StringComparison.OrdinalIgnoreCase))
                    return GoldRule.From(row.Chance, row.MinAmount, row.MaxAmount);
            }
        }

        return null;
    }

    private static bool IsBiscordRuntimeObject(GameObject go)
    {
        if (go == null) return false;
        // Avoid hard-typing BiscodMarker here to keep this file decoupled from Patches/Enemy.
        if (go.GetComponent("BiscodMarker") != null) return true;
        string name = go.name;
        return !string.IsNullOrEmpty(name) &&
               name.IndexOf("biscord", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string StripCloneSuffix(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        const string suffix = "(Clone)";
        if (raw.EndsWith(suffix, StringComparison.Ordinal))
            return raw.Substring(0, raw.Length - suffix.Length).TrimEnd();
        return raw.TrimEnd();
    }

}

/// <summary>
/// Resolved drop rule (chance + amount range). Created from JSON rows by <see cref="GoldDropTable.Resolve"/>.
/// </summary>
internal sealed class GoldRule
{
    public float Chance;
    public int MinAmount;
    public int MaxAmount;

    public static GoldRule From(float chance, int min, int max)
    {
        if (chance <= 0f || max <= 0)
            return null;
        if (max < min) max = min;
        return new GoldRule
        {
            Chance = Mathf.Clamp01(chance),
            MinAmount = Mathf.Max(0, min),
            MaxAmount = Mathf.Max(0, max)
        };
    }

    public int Roll()
    {
        if (MinAmount >= MaxAmount) return MinAmount;
        return UnityEngine.Random.Range(MinAmount, MaxAmount + 1);
    }
}

[Serializable]
public sealed class GoldDropTableConfig
{
    public GoldDropTableSettings Settings = new GoldDropTableSettings();
    public bool ExcludeBiscord = true;
    public GoldDropFactionRule[] FactionRules = new GoldDropFactionRule[0];
    public GoldDropEnemyOverride[] EnemyOverrides = new GoldDropEnemyOverride[0];

    public static GoldDropTableConfig Default()
    {
        return new GoldDropTableConfig
        {
            Settings = new GoldDropTableSettings(),
            ExcludeBiscord = true,
            FactionRules = new[]
            {
                new GoldDropFactionRule { Faction = "bandits",             Chance = 0.55f, MinAmount = 5,  MaxAmount = 15 },
                new GoldDropFactionRule { Faction = "bandits_inquisition", Chance = 0.50f, MinAmount = 6,  MaxAmount = 18 },
                new GoldDropFactionRule { Faction = "bandits_mafia",       Chance = 0.60f, MinAmount = 12, MaxAmount = 28 },
                new GoldDropFactionRule { Faction = "bandits_demons",      Chance = 0.45f, MinAmount = 8,  MaxAmount = 20 },
                new GoldDropFactionRule { Faction = "mafia",               Chance = 0.65f, MinAmount = 15, MaxAmount = 35 },
                new GoldDropFactionRule { Faction = "church",              Chance = 0.40f, MinAmount = 8,  MaxAmount = 18 },
                new GoldDropFactionRule { Faction = "demons",              Chance = 0.20f, MinAmount = 3,  MaxAmount = 10 },
                new GoldDropFactionRule { Faction = "undead",              Chance = 0.10f, MinAmount = 1,  MaxAmount = 4  },
                new GoldDropFactionRule { Faction = "monsters",            Chance = 0.10f, MinAmount = 1,  MaxAmount = 5  },
                new GoldDropFactionRule { Faction = "neutral",             Chance = 0.00f, MinAmount = 0,  MaxAmount = 0  }
            },
            EnemyOverrides = new GoldDropEnemyOverride[0]
        };
    }
}

[Serializable]
public sealed class GoldDropTableSettings
{
    public bool ApplyDifficultyScaling = true;
    public int MinAmountFloor = 1;
}

[Serializable]
public sealed class GoldDropFactionRule
{
    public string Faction;
    public float Chance;
    public int MinAmount;
    public int MaxAmount;
}

[Serializable]
public sealed class GoldDropEnemyOverride
{
    public string EnemyType;
    public float Chance;
    public int MinAmount;
    public int MaxAmount;
}
