using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;

namespace NoREroMod.Systems.Fear;

/// <summary>
/// Loads HellGateJson/Fear/FearEnemies.json.
/// Default = shared baseline; enemies[] override by type or object name.
/// JSON field magnitudes are pressure values; callers convert to negative ΔMorale.
/// </summary>
internal static class FearCatalog
{
    private static FearEnemiesFile _cached;
    private static float _lastLoadTime = -999f;
    private const float ReloadInterval = 2f;
    private static readonly Dictionary<string, FearEnemyStats> _byEnemy =
        new Dictionary<string, FearEnemyStats>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, FearHPhaseBeat[]> _hPhasesByEnemy =
        new Dictionary<string, FearHPhaseBeat[]>(StringComparer.OrdinalIgnoreCase);

    internal static FearEnemyStats DefaultStats => GetFile().Default ?? FearEnemyStats.CreateDefault();

    /// <summary>JSON <c>hPhases</c> for an enemy type key, or null.</summary>
    internal static FearHPhaseBeat[] GetHPhases(string enemyType)
    {
        EnsureCache();
        if (string.IsNullOrEmpty(enemyType))
            return null;
        FearHPhaseBeat[] phases;
        if (_hPhasesByEnemy.TryGetValue(enemyType, out phases) && phases != null && phases.Length > 0)
            return phases;
        return null;
    }
    internal static FearEnemyStats ResolveForEnemy(EnemyDate enemy)
    {
        FearEnemyStats defaults = DefaultStats;
        if (enemy == null)
            return defaults;

        EnsureCache();

        // Undead skeleton reskins keep the vanilla C# type (Vagrant/Pilgrim/…) —
        // prefer the spawn key row (VagrantUndead, …) when the marker is present.
        if (NoREroMod.Patches.Enemy.UndeadSkeletonsModCustom.UndeadSkeletonsRuntime.TryGetSpawnKey(enemy, out string undeadKey) &&
            !string.IsNullOrEmpty(undeadKey) &&
            _byEnemy.TryGetValue(undeadKey, out FearEnemyStats byUndead))
        {
            return Merge(defaults, byUndead);
        }

        string typeName = enemy.GetType().Name ?? string.Empty;
        if (!string.IsNullOrEmpty(typeName) && _byEnemy.TryGetValue(typeName, out FearEnemyStats byType))
            return Merge(defaults, byType);

        string objectName = StripClone(enemy.gameObject != null ? enemy.gameObject.name : null);
        if (!string.IsNullOrEmpty(objectName) && _byEnemy.TryGetValue(objectName, out FearEnemyStats byName))
            return Merge(defaults, byName);

        return defaults;
    }

    /// <summary>
    /// H / trap-H pressure: prefer enemy with eroflag override, else JSON Default, else cfg.
    /// </summary>
    internal static float ResolveHSceneFearPerSecond()
    {
        EnsureCache();
        try
        {
            EnemyDate[] enemies = UnityEngine.Object.FindObjectsOfType<EnemyDate>();
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyDate enemy = enemies[i];
                    if (enemy == null || !enemy.eroflag)
                        continue;
                    FearEnemyStats stats = ResolveForEnemy(enemy);
                    if (stats.hSceneFearPerSecond > 0f)
                        return stats.hSceneFearPerSecond;
                }
            }
        }
        catch { }

        float fromDefault = DefaultStats.hSceneFearPerSecond;
        if (fromDefault > 0f)
            return fromDefault;

        return FearConfig.HSceneFearPerSecond?.Value ?? 0.4f;
    }

    private static void EnsureCache()
    {
        GetFile();
    }

    private static FearEnemiesFile GetFile()
    {
        if (_cached != null && Time.realtimeSinceStartup - _lastLoadTime < ReloadInterval)
            return _cached;

        _cached = LoadFromFile();
        _lastLoadTime = Time.realtimeSinceStartup;
        RebuildLookup(_cached);
        return _cached;
    }

    private static void RebuildLookup(FearEnemiesFile file)
    {
        _byEnemy.Clear();
        _hPhasesByEnemy.Clear();
        if (file == null)
            return;

        if (file.enemies != null)
        {
            for (int i = 0; i < file.enemies.Length; i++)
            {
                FearEnemyEntry entry = file.enemies[i];
                if (entry == null || string.IsNullOrEmpty(entry.enemyType))
                    continue;
                _byEnemy[entry.enemyType] = entry.ToStats();
                if (entry.hPhases != null && entry.hPhases.Length > 0)
                    _hPhasesByEnemy[entry.enemyType] = entry.hPhases;
            }
        }
    }

    private static FearEnemiesFile LoadFromFile()
    {
        string path = GetConfigPath();
        if (!File.Exists(path))
            return FearEnemiesFile.CreateDefault();

        try
        {
            string json = File.ReadAllText(path);
            FearEnemiesFile loaded = JsonUtility.FromJson<FearEnemiesFile>(json);
            if (loaded == null)
                return FearEnemiesFile.CreateDefault();
            if (loaded.Default == null)
                loaded.Default = FearEnemyStats.CreateDefault();
            return loaded;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Fear] Failed to load FearEnemies.json: " + ex.Message + ". Using defaults.");
            return FearEnemiesFile.CreateDefault();
        }
    }

    private static string GetConfigPath()
    {
        try
        {
            return Path.Combine(Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "Fear"), "FearEnemies.json");
        }
        catch
        {
            string basePath = Path.Combine(Application.dataPath, "..");
            return Path.Combine(Path.Combine(Path.Combine(Path.Combine(basePath, "BepInEx"), "plugins"), "HellGateJson"), Path.Combine("Fear", "FearEnemies.json"));
        }
    }

    private static string StripClone(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;
        int cloneIdx = name.IndexOf("(Clone)", StringComparison.Ordinal);
        if (cloneIdx >= 0)
            name = name.Substring(0, cloneIdx).Trim();
        return name;
    }

    /// <summary>0 on override means inherit from default.</summary>
    private static FearEnemyStats Merge(FearEnemyStats defaults, FearEnemyStats over)
    {
        if (over == null)
            return defaults;
        return new FearEnemyStats
        {
            fearPerSecond = over.fearPerSecond > 0f ? over.fearPerSecond : defaults.fearPerSecond,
            heavyHitFear = over.heavyHitFear > 0f ? over.heavyHitFear : defaults.heavyHitFear,
            projectileFear = over.projectileFear > 0f ? over.projectileFear : defaults.projectileFear,
            hSceneFearPerSecond = over.hSceneFearPerSecond > 0f ? over.hSceneFearPerSecond : defaults.hSceneFearPerSecond,
            grabFear = over.grabFear > 0f ? over.grabFear : defaults.grabFear
        };
    }

    [Serializable]
    internal class FearEnemiesFile
    {
        public FearEnemyStats Default;
        public FearEnemyEntry[] enemies;

        internal static FearEnemiesFile CreateDefault()
        {
            return new FearEnemiesFile
            {
                Default = FearEnemyStats.CreateDefault(),
                enemies = new FearEnemyEntry[0]
            };
        }
    }

    [Serializable]
    internal class FearEnemyEntry
    {
        public string enemyType;
        public float fearPerSecond;
        public float heavyHitFear;
        public float projectileFear;
        public float hSceneFearPerSecond;
        /// <summary>Absolute |ΔMorale| on grab land (size/threat graded). 0 = inherit Default / cfg.</summary>
        public float grabFear;
        /// <summary>Optional H-phase Fear beats, applied by code hooks (see <see cref="FearHPhases"/>).</summary>
        public FearHPhaseBeat[] hPhases;

        internal FearEnemyStats ToStats()
        {
            return new FearEnemyStats
            {
                fearPerSecond = fearPerSecond,
                heavyHitFear = heavyHitFear,
                projectileFear = projectileFear,
                hSceneFearPerSecond = hSceneFearPerSecond,
                grabFear = grabFear
            };
        }
    }

    [Serializable]
    internal class FearEnemyStats
    {
        public float fearPerSecond = 0.4f;
        public float heavyHitFear = 5f;
        public float projectileFear = 1.5f;
        public float hSceneFearPerSecond = 0.4f;
        /// <summary>Absolute grab-land fear; Default 8 matches GrabLandMorale.</summary>
        public float grabFear = 8f;

        internal static FearEnemyStats CreateDefault()
        {
            return new FearEnemyStats();
        }
    }
}

/// <summary>One H-phase Fear beat from FearEnemies.json (matched by clip or spine event name).</summary>
[Serializable]
internal class FearHPhaseBeat
{
    public string animation;

    /// <summary>ΔMorale on entering the phase (negative = fear; positive values are flipped).</summary>
    public float spikeMorale;

    /// <summary>If true, <see cref="spikeMorale"/> is the absolute Morale to set (−100 = max Fear).</summary>
    public bool absolute;

    /// <summary>Continuous Morale/sec while the clip plays during H (negative = fear).</summary>
    public float moralePerSecond;
}
