using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
using NoREroMod.Patches.Enemy.WolfModCustom;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Unified spawn config executor with support for:
/// - Fixed spawn: X,Y,EnemyType,Count
/// - RANDOM,chance,X,Y,EnemyType,Count - spawn with probability (0.0-1.0)
/// - RANDOM_GROUP,N,START ... RANDOM_GROUP,END - spawn N random points from group
/// - POOL[Type1,Type2,...] - random enemy from list at position
/// </summary>
internal static class SpawnConfigExecutor
{
    private const float DefaultRadius = 2f;
    private const float OffsetScaleY = 0.7f;

    /// <summary>
    /// Execute spawn config. Returns total spawned count.
    /// </summary>
    public static int Execute(string configPath, string logPrefix = "[SPAWN]")
    {
        try
        {
            if (!File.Exists(configPath))
            {
                if (logPrefix != null)
                    Plugin.Log?.LogError($"{logPrefix} Config file not found: {configPath}");
                return 0;
            }

            var pointsToSpawn = new List<SpawnPoint>();
            string[] lines = File.ReadAllLines(configPath);
            bool inRandomGroup = false;
            int groupSpawnCount = 0;
            var currentGroup = new List<SpawnPoint>();

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                // RANDOM_GROUP,N,START
                if (trimmed.IndexOf("RANDOM_GROUP", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    trimmed.IndexOf("START", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    inRandomGroup = true;
                    currentGroup.Clear();
                    groupSpawnCount = ParseGroupCount(trimmed);
                    continue;
                }

                // RANDOM_GROUP,END
                if (trimmed.IndexOf("RANDOM_GROUP", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    trimmed.IndexOf("END", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    inRandomGroup = false;
                    var selected = SelectRandomPoints(currentGroup, groupSpawnCount);
                    pointsToSpawn.AddRange(selected);
                    continue;
                }

                if (inRandomGroup)
                {
                    if (TryParseSpawnPoint(trimmed, out var pt))
                        currentGroup.Add(pt);
                    continue;
                }

                // RANDOM,chance,X,Y,EnemyType,Count
                if (trimmed.StartsWith("RANDOM,", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseRandomPoint(trimmed, out var pt) && Random.value < pt.Chance)
                        pointsToSpawn.Add(pt);
                    continue;
                }

                // Normal or POOL line
                if (TryParseSpawnPoint(trimmed, out var point))
                    pointsToSpawn.Add(point);
            }

            int totalSpawned = 0;
            foreach (var pt in pointsToSpawn)
            {
                totalSpawned += SpawnPointAt(pt);
            }

            if (logPrefix != null)
                // Plugin.Log?.LogInfo($"{logPrefix} Spawned {totalSpawned} enemies from {pointsToSpawn.Count} points"); // Disabled for cleaner logs

            return totalSpawned;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[SPAWN] Error in Execute: {ex.Message}");
            return 0;
        }

        // This should never be reached, but ensures all code paths return a value
        return 0;
    }

    private static int ParseGroupCount(string line)
    {
        var parts = line.Split(',');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (string.Equals(parts[i].Trim(), "RANDOM_GROUP", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(parts[i + 1].Trim(), out int n) && n > 0)
            {
                return n;
            }
        }
        return 1;
    }

    private static List<SpawnPoint> SelectRandomPoints(List<SpawnPoint> points, int count)
    {
        if (points == null || points.Count == 0) return new List<SpawnPoint>();
        if (count >= points.Count) return new List<SpawnPoint>(points);

        var shuffled = new List<SpawnPoint>(points);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = shuffled[i];
            shuffled[i] = shuffled[j];
            shuffled[j] = temp;
        }
        return shuffled.GetRange(0, count);
    }

    private static bool TryParseRandomPoint(string line, out SpawnPoint pt)
    {
        pt = default;
        var parts = line.Split(',');
        // RANDOM,chance,X,Y,EnemyType,Count[,Description]
        // EnemyType may contain commas (e.g. POOL[Type1,Type2,...]) - join parts[4]..[Length-2]
        if (parts.Length < 6) return false;
        if (!float.TryParse(parts[1].Trim(), out float chance)) return false;
        if (!float.TryParse(parts[2].Trim(), out float x)) return false;
        if (!float.TryParse(parts[3].Trim(), out float y)) return false;
        string enemyTypeRaw = string.Join(",", parts, 4, parts.Length - 5);
        string enemyType = ResolvePoolOrType(enemyTypeRaw.Trim());
        if (!int.TryParse(parts[parts.Length - 1].Trim(), out int count)) return false;

        pt = new SpawnPoint { X = x, Y = y, EnemyType = enemyType, Count = count, Chance = Mathf.Clamp01(chance) };
        return true;
    }

    private static bool TryParseSpawnPoint(string line, out SpawnPoint pt)
    {
        pt = default;
        var parts = line.Split(',');
        if (parts.Length < 4) return false;
        if (!float.TryParse(parts[0].Trim(), out float x)) return false;
        if (!float.TryParse(parts[1].Trim(), out float y)) return false;
        // EnemyType may contain commas (e.g. POOL[Type1,Type2,...]) - join parts[2]..[n-2], last part is Count
        string enemyTypeRaw = string.Join(",", parts, 2, parts.Length - 3);
        string enemyType = ResolvePoolOrType(enemyTypeRaw.Trim());
        if (!int.TryParse(parts[parts.Length - 1].Trim(), out int count)) return false;

        pt = new SpawnPoint { X = x, Y = y, EnemyType = enemyType, Count = count, Chance = 1f };
        return true;
    }

    /// <summary>
    /// Resolves POOL[Type1,Type2,...] to a random type, or returns as-is for normal type.
    /// </summary>
    private static string ResolvePoolOrType(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;

        int start = raw.IndexOf("POOL[", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return raw;

        int end = raw.IndexOf(']', start);
        if (end < 0) return raw;

        string inner = raw.Substring(start + 5, end - start - 5);
        var types = new List<string>();
        foreach (var t in inner.Split(','))
        {
            var trimmed = t.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                types.Add(trimmed);
        }
        if (types.Count == 0) return raw;
        return types[Random.Range(0, types.Count)];
    }

    private static int SpawnPointAt(SpawnPoint pt)
    {
        Vector2 center = new Vector2(pt.X, pt.Y);
        GameObject prefab = EnemyPrefabRegistry.GetPrefab(pt.EnemyType);
        if (prefab == null) return 0;

        int spawned = 0;
        if (pt.Count == 1)
        {
            SpawnSingle(prefab, center, pt.EnemyType);
            spawned = 1;
        }
        else
        {
            for (int i = 0; i < pt.Count; i++)
            {
                Vector2 offset = CalculateOffset(i, pt.Count);
                SpawnSingle(prefab, center + offset, pt.EnemyType);
                spawned++;
            }
        }
        return spawned;
    }

    private static void SpawnSingle(GameObject prefab, Vector2 position, string enemyType)
    {
        try
        {
            GameObject spawned = Object.Instantiate(prefab, position, Quaternion.identity);
            if (spawned == null) return;

            if (enemyType == "BigoniBrother")
                spawned.name = "BigoniBrother";
            else if (enemyType == "MafiaBossCustom")
                spawned.name = "MafiaBossCustom";
            else if (enemyType == "Wolf")
            {
                spawned.name = "Wolf";
                WolfSkeletonLoader.ApplyWolfSkeletons(spawned);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN] Failed to spawn {enemyType}: {ex.Message}");
        }
    }

    private static Vector2 CalculateOffset(int index, int total)
    {
        if (total <= 1) return Vector2.zero;
        float angle = (360f / total) * index * Mathf.Deg2Rad;
        return new Vector2(
            Mathf.Cos(angle) * DefaultRadius,
            Mathf.Sin(angle) * DefaultRadius * OffsetScaleY
        );
    }

    private struct SpawnPoint
    {
        public float X, Y;
        public string EnemyType;
        public int Count;
        public float Chance;
    }
}
