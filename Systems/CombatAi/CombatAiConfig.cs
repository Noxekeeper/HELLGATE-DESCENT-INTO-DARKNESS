using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi;

/// <summary>
/// Combat AI module config. Loaded from BepInEx/plugins/HellGateJson/CombatAi/CombatAi.json.
/// Standard distance check; melee attack rate can be increased via MeleeAttackRateMultiplier.
/// </summary>
internal static class CombatAiConfig
{
    private static CombatAiSettings _cached;
    private static string _lastLoadedPath;
    private static float _lastLoadTime = -999f;
    private const float ReloadInterval = 2f;

    public static bool Enable => Get().Enable;
    public static bool DebugLogging => Get().DebugLogging;
    /// <summary>Melee statecount accumulation speed multiplier (melee attacks more often). 1.0 = unchanged, 1.5 = 50% more often.</summary>
    public static float MeleeAttackRateMultiplier => Get().MeleeAttackRateMultiplier;
    public static bool ReactToCombo => Get().ReactToCombo;
    public static float ReactToComboDodgeChance => Get().ReactToComboDodgeChance;
    public static int ReactToComboMaxAvoidcount => Get().ReactToComboMaxAvoidcount;
    public static bool ReactToComboOnlyFirstHit => Get().ReactToComboOnlyFirstHit;
    public static int ReactToComboMinAvoidcount => Get().ReactToComboMinAvoidcount;

    public static bool DoreiEnable => Get().DoreiEnable;
    public static bool DoreiDisableFlee => Get().DoreiDisableFlee;
    public static float DoreiPreferMeleeOverFleeChance => Get().DoreiPreferMeleeOverFleeChance;
    public static float DoreiMeleeRangeThreshold => Get().DoreiMeleeRangeThreshold;
    public static float DoreiMeleeAttackRateMultiplier => Get().DoreiMeleeAttackRateMultiplier;

    public static CombatAiSettings Get()
    {
        if (_cached != null && Time.realtimeSinceStartup - _lastLoadTime < ReloadInterval)
            return _cached;
        _cached = LoadFromFile();
        _lastLoadTime = Time.realtimeSinceStartup;
        return _cached;
    }

    /// <summary>Force reload config from disk (e.g. after editing JSON).</summary>
    public static void Reload()
    {
        _lastLoadTime = -999f;
        _cached = LoadFromFile();
    }

    private static CombatAiSettings LoadFromFile()
    {
        string path = GetConfigPath();
        _lastLoadedPath = path;
        if (!File.Exists(path))
            return CombatAiSettings.Default();
        try
        {
            string json = File.ReadAllText(path);
            var loaded = JsonUtility.FromJson<CombatAiSettings>(json);
            if (loaded == null)
                return CombatAiSettings.Default();
            return loaded;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[CombatAi] Failed to load config: {ex.Message}. Using defaults.");
            return CombatAiSettings.Default();
        }
    }

    private static string GetConfigPath()
    {
        try
        {
            string combatAiDir = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "CombatAi");
            return Path.Combine(combatAiDir, "CombatAi.json");
        }
        catch
        {
            string basePath = Path.Combine(Application.dataPath, "..");
            string bepInEx = Path.Combine(basePath, "BepInEx");
            string plugins = Path.Combine(bepInEx, "plugins");
            string hellGateJson = Path.Combine(plugins, "HellGateJson");
            string combatAi = Path.Combine(hellGateJson, "CombatAi");
            return Path.Combine(combatAi, "CombatAi.json");
        }
    }

    [Serializable]
    public class CombatAiSettings
    {
        public bool Enable = true;
        public bool DebugLogging = false;
        public float MeleeAttackRateMultiplier = 1.5f;
        public bool ReactToCombo = true;
        public float ReactToComboDodgeChance = 0.4f;
        public int ReactToComboMaxAvoidcount = 2;
        public bool ReactToComboOnlyFirstHit = false;
        public int ReactToComboMinAvoidcount = 1;

        // ---- Dorei (SinnerslaveCrossbow): single config here, path BepInEx/plugins/HellGateJson/CombatAi/CombatAi.json ----
        public bool DoreiEnable = true;
        public bool DoreiDisableFlee = true;
        public float DoreiPreferMeleeOverFleeChance = 0.8f;
        public float DoreiMeleeRangeThreshold = 6f;
        public float DoreiMeleeAttackRateMultiplier = 1.5f;

        public static CombatAiSettings Default() => new CombatAiSettings();
    }
}
