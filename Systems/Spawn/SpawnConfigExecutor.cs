using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
using Spine.Unity;
using NoREroMod.Patches.Enemy.WolfModCustom;
using NoREroMod.Patches.Enemy.ButcherModCustom;
using NoREroMod.Patches.Enemy.HellishTouzokuModCustom;
using NoREroMod.Patches.Enemy.BossTouzokuCustom;
using NoREroMod.Patches.Enemy.HeckGateEnemy;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.CombatAi.Factions.Patches;
using NoREroMod.Systems.Economy;
using NoREroMod.Systems.EventCore.Host;

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
    private const string NightlessCityCFileName = "HellGateSpawn_nightless city C.txt";
    private const string BrokerGateEventId = "eventcore_broker_gate";
    private const float BrokerGateDebugX = 742.88f;
    private const float BrokerGateDebugY = -286.79f;
    private const float BrokerGateDebugTolerance = 0.05f;

    internal struct RuntimeSpawnPoint
    {
        public Vector2 Center;
        public string EnemyType;
        public string FactionIdRaw;
        public string EventCoreEventId;
        public int Count;
    }

    /// <summary>
    /// Execute spawn config. Returns total spawned count.
    /// </summary>
    /// <param name="skipCleanup">When true, skip CleanupManagedSpawns (caller already wiped).</param>
    public static int Execute(string configPath, string logPrefix = "[SPAWN]", bool skipCleanup = false)
    {
        int spawned = 0;
        IEnumerator e = ExecuteCore(configPath, logPrefix, skipCleanup, batchPerFrame: 0, onTotal: n => spawned = n, refreshEpoch: -1);
        while (e.MoveNext()) { }
        return spawned;
    }

    /// <summary>
    /// Same as Execute, but yields every <paramref name="batchPerFrame"/> spawn ops to reduce hitch.
    /// </summary>
    public static IEnumerator ExecuteBatched(
        string configPath,
        string logPrefix = "[SPAWN]",
        bool skipCleanup = false,
        int batchPerFrame = 8,
        int refreshEpoch = -1)
    {
        if (batchPerFrame < 1)
            batchPerFrame = 8;
        IEnumerator e = ExecuteCore(configPath, logPrefix, skipCleanup, batchPerFrame, null, refreshEpoch);
        while (e.MoveNext())
            yield return e.Current;
    }

    private static IEnumerator ExecuteCore(
        string configPath,
        string logPrefix,
        bool skipCleanup,
        int batchPerFrame,
        Action<int> onTotal,
        int refreshEpoch = -1)
    {
        int totalSpawned = 0;
        bool traceBrokerGate = false;
        var pointsToSpawn = new List<SpawnPoint>();
        var templatePointsToSpawn = new List<TemplateSpawnPoint>();
        var goldPointsToSpawn = new List<GoldSpawnPoint>();

        bool parseOk = false;
        try
        {
            traceBrokerGate = ShouldTraceBrokerGateConfig(configPath);
            if (traceBrokerGate)
                Plugin.Log?.LogInfo($"{logPrefix} [DEBUG] Execute start. Config path: {Path.GetFullPath(configPath)}");

            if (!File.Exists(configPath))
            {
                if (logPrefix != null)
                    Plugin.Log?.LogError($"{logPrefix} Config file not found: {configPath}");
            }
            else
            {
                if (!skipCleanup)
                    CleanupManagedSpawns();

                string[] lines = File.ReadAllLines(configPath);
                if (traceBrokerGate)
                    Plugin.Log?.LogInfo($"{logPrefix} [DEBUG] Loaded {lines.Length} line(s) from config.");
                bool inRandomGroup = false;
                int groupSpawnCount = 0;
                var currentGroup = new List<SpawnPoint>();

                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].Trim();
                    if (HellGateSpawnLineFormat.IsIgnorableConfigLine(trimmed))
                        continue;

                    if (trimmed.StartsWith("EVENTTRAP,", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (trimmed.StartsWith("REINFORCEMENT,", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (traceBrokerGate && ShouldTraceBrokerGateLine(trimmed))
                        Plugin.Log?.LogInfo($"{logPrefix} [DEBUG] Candidate line {i + 1}: {trimmed}");

                    if (trimmed.IndexOf("RANDOM_GROUP", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        trimmed.IndexOf("START", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        inRandomGroup = true;
                        currentGroup.Clear();
                        groupSpawnCount = ParseGroupCount(trimmed);
                        continue;
                    }

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
                        {
                            if (traceBrokerGate && IsTrackedBrokerGatePoint(pt))
                                LogTrackedBrokerGatePoint(logPrefix, "Queued point from RANDOM_GROUP", pt, i + 1);
                            currentGroup.Add(pt);
                        }
                        continue;
                    }

                    if (TryParseGoldShortcutLine(trimmed, out GoldSpawnPoint goldShortcutPoint))
                    {
                        goldPointsToSpawn.Add(goldShortcutPoint);
                        continue;
                    }

                    if (TryParseTemplateSpawnPoint(trimmed, out var templatePoint))
                    {
                        templatePointsToSpawn.Add(templatePoint);
                        continue;
                    }

                    if (trimmed.StartsWith("RANDOM_HOSTAGE,", StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryParseRandomHostageTemplate(trimmed, out var hostagePt))
                            templatePointsToSpawn.Add(hostagePt);
                        continue;
                    }

                    if (trimmed.StartsWith("RANDOM,", StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryParseRandomGoldPoint(trimmed, out GoldSpawnPoint randomGoldPoint))
                        {
                            float roll = Random.value;
                            if (roll < randomGoldPoint.Chance)
                                goldPointsToSpawn.Add(randomGoldPoint);
                        }
                        else if (TryParseRandomPoint(trimmed, out var pt))
                        {
                            float roll = Random.value;
                            if (traceBrokerGate && IsTrackedBrokerGatePoint(pt))
                            {
                                LogTrackedBrokerGatePoint(logPrefix, $"Parsed RANDOM point (roll={roll:F3}, chance={pt.Chance:F3})", pt, i + 1);
                            }

                            if (roll < pt.Chance)
                                pointsToSpawn.Add(pt);
                        }
                        continue;
                    }

                    if (TryParseGoldSpawnPoint(trimmed, out GoldSpawnPoint goldPoint))
                    {
                        goldPointsToSpawn.Add(goldPoint);
                        continue;
                    }

                    if (TryParseSpawnPoint(trimmed, out var point))
                    {
                        if (traceBrokerGate && IsTrackedBrokerGatePoint(point))
                            LogTrackedBrokerGatePoint(logPrefix, "Queued fixed point", point, i + 1);
                        pointsToSpawn.Add(point);
                    }
                }

                if (inRandomGroup && currentGroup.Count > 0)
                {
                    var selected = SelectRandomPoints(currentGroup, groupSpawnCount);
                    pointsToSpawn.AddRange(selected);
                }

                if (traceBrokerGate)
                    Plugin.Log?.LogInfo($"{logPrefix} [DEBUG] Queue summary: fixed/random={pointsToSpawn.Count}, template={templatePointsToSpawn.Count}, gold={goldPointsToSpawn.Count}.");

                parseOk = true;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[SPAWN] Error in Execute: {ex.Message}");
            parseOk = false;
        }

        if (!parseOk)
        {
            if (onTotal != null)
                onTotal(0);
            yield break;
        }

        // Yields must stay outside try/catch (C# restriction).
        int enemyPointsQueued = pointsToSpawn.Count;
        int enemyPointsFailed = 0;
        int opsThisFrame = 0;
        foreach (var pt in pointsToSpawn)
        {
            if (traceBrokerGate && IsTrackedBrokerGatePoint(pt))
                LogTrackedBrokerGatePoint(logPrefix, "Dispatching tracked point to SpawnPointAt", pt);
            int spawnedHere = SpawnPointAt(pt);
            totalSpawned += spawnedHere;
            if (spawnedHere <= 0)
                enemyPointsFailed++;

                if (batchPerFrame > 0)
                {
                    opsThisFrame++;
                    if (opsThisFrame >= batchPerFrame)
                    {
                        opsThisFrame = 0;
                        yield return null;
                        if (refreshEpoch >= 0 && !HellGateLocationSpawnRefresh.IsRefreshEpochCurrent(refreshEpoch))
                        {
                            if (onTotal != null)
                                onTotal(totalSpawned);
                            yield break;
                        }
                    }
                }
            }

            string currentSceneName = GetCurrentSceneName();
            foreach (var pt in templatePointsToSpawn)
            {
                float rollChance = pt.Chance <= 0f ? 1f : Mathf.Clamp01(pt.Chance);
                if (rollChance < 1f && Random.value >= rollChance)
                    continue;

                string templateLog = logPrefix;
                if (HellGateSpawnLineFormat.IsHostageShortcut(pt.Category))
                    templateLog = $"{logPrefix} [HOSTAGE]";
                else if (HellGateSpawnLineFormat.IsDecorShortcut(pt.Category))
                    templateLog = $"{logPrefix} [DECOR]";
                else if (HellGateSpawnLineFormat.IsSceneObjectTemplateSemantic(pt.Category))
                    templateLog = $"{logPrefix} [OBJECT]";
                if (SpawnTemplateCatalog.TrySpawn(
                        pt.Category, pt.Key, new Vector2(pt.X, pt.Y), pt.Count, templateLog, currentSceneName,
                        pt.FlipX, pt.RotationZ, pt.Depth))
                    totalSpawned += pt.Count;

                if (batchPerFrame > 0)
                {
                    opsThisFrame++;
                    if (opsThisFrame >= batchPerFrame)
                    {
                        opsThisFrame = 0;
                        yield return null;
                        if (refreshEpoch >= 0 && !HellGateLocationSpawnRefresh.IsRefreshEpochCurrent(refreshEpoch))
                        {
                            if (onTotal != null)
                                onTotal(totalSpawned);
                            yield break;
                        }
                    }
                }
            }

            foreach (var pt in goldPointsToSpawn)
            {
                totalSpawned += SpawnGoldPointAt(pt, logPrefix);
                if (batchPerFrame > 0)
                {
                    opsThisFrame++;
                    if (opsThisFrame >= batchPerFrame)
                    {
                        opsThisFrame = 0;
                        yield return null;
                        if (refreshEpoch >= 0 && !HellGateLocationSpawnRefresh.IsRefreshEpochCurrent(refreshEpoch))
                        {
                            if (onTotal != null)
                                onTotal(totalSpawned);
                            yield break;
                        }
                    }
                }
            }

        if (logPrefix != null && enemyPointsQueued > 0)
        {
            Plugin.Log?.LogInfo(
                $"{logPrefix} Enemy points: queued={enemyPointsQueued}, failed={enemyPointsFailed}, spawnedInstances≈{enemyPointsQueued - enemyPointsFailed}.");

            LogEliteLiveSnapshot(logPrefix);
            if (Plugin.Instance != null)
                Plugin.Instance.StartCoroutine(LogEliteLiveSnapshotDelayed(logPrefix, 2f));
        }

        if (goldPointsToSpawn.Count > 0 && logPrefix != null)
            Plugin.Log?.LogInfo($"{logPrefix} [GOLD] Spawned piles from {goldPointsToSpawn.Count} config line(s).");

        if (traceBrokerGate)
            Plugin.Log?.LogInfo($"{logPrefix} [DEBUG] Execute finished. Total spawned: {totalSpawned}.");

        if (onTotal != null)
            onTotal(totalSpawned);
    }

    internal static void CollectTemplateKeysFromConfig(string configPath, HashSet<string> keys)
    {
        if (keys == null || string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            return;

        string[] lines = File.ReadAllLines(configPath);
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (HellGateSpawnLineFormat.IsIgnorableConfigLine(trimmed))
                continue;

            if (trimmed.StartsWith("EVENTTRAP,", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("REINFORCEMENT,", StringComparison.OrdinalIgnoreCase))
                continue;

            if (TryParseTemplateSpawnPoint(trimmed, out TemplateSpawnPoint templatePoint))
            {
                keys.Add(SpawnTemplateCatalog.NormalizeTemplateKey(templatePoint.Key));
                continue;
            }

            if (trimmed.StartsWith("RANDOM_HOSTAGE,", StringComparison.OrdinalIgnoreCase) &&
                TryParseRandomHostageTemplate(trimmed, out TemplateSpawnPoint hostagePoint))
            {
                keys.Add(SpawnTemplateCatalog.NormalizeTemplateKey(hostagePoint.Key));
                continue;
            }

            if (TryParseSpawnPoint(trimmed, out SpawnPoint point))
            {
                string normalized = SpawnTemplateCatalog.NormalizeTemplateKey(point.EnemyType);
                if (SpawnTemplateDiskCache.HasDiskEntry(normalized))
                    keys.Add(normalized);
            }
        }
    }

    internal static int SpawnRuntimePack(
        RuntimeSpawnPoint[] points,
        string logPrefix = "[SPAWN]",
        bool markHostileToPlayer = false,
        bool suppressFactionMarker = false)
    {
        try
        {
            if (points == null || points.Length == 0)
                return 0;

            EnemyPrefabRegistry.Initialize();

            int totalSpawned = 0;
            for (int i = 0; i < points.Length; i++)
            {
                RuntimeSpawnPoint pt = points[i];
                if (string.IsNullOrEmpty(pt.EnemyType) || pt.Count <= 0)
                    continue;

                if (!EnemyPrefabRegistry.TryGetPrefab(pt.EnemyType, out GameObject prefab))
                {
                    if (logPrefix != null)
                        Plugin.Log?.LogWarning($"{logPrefix} Prefab not found for runtime spawn: {pt.EnemyType}");
                    continue;
                }

                if (pt.Count == 1)
                {
                    SpawnSingle(prefab, pt.Center, pt.EnemyType, pt.FactionIdRaw, pt.EventCoreEventId, false, markHostileToPlayer, suppressFactionMarker);
                    totalSpawned++;
                    continue;
                }

                for (int j = 0; j < pt.Count; j++)
                {
                    Vector2 offset = CalculateOffset(j, pt.Count);
                    SpawnSingle(prefab, pt.Center + offset, pt.EnemyType, pt.FactionIdRaw, pt.EventCoreEventId, false, markHostileToPlayer, suppressFactionMarker);
                    totalSpawned++;
                }
            }

            return totalSpawned;
        }
        catch (Exception ex)
        {
            if (logPrefix != null)
                Plugin.Log?.LogError($"{logPrefix} Error in SpawnRuntimePack: {ex.Message}");
            return 0;
        }
    }

    /// <summary>Spawn one runtime enemy at a world position (used by Shelter Attack and similar drivers).</summary>
    internal static GameObject TrySpawnRuntimeEnemy(
        string enemyType,
        Vector2 position,
        string factionIdRaw,
        bool markHostileToPlayer = false,
        bool suppressFactionMarker = false)
    {
        if (string.IsNullOrEmpty(enemyType))
            return null;

        EnemyPrefabRegistry.Initialize();
        if (!EnemyPrefabRegistry.TryGetPrefab(enemyType, out GameObject prefab))
            return null;

        return SpawnSingle(
            prefab,
            position,
            enemyType,
            factionIdRaw,
            null,
            false,
            markHostileToPlayer,
            suppressFactionMarker);
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
        // RANDOM,chance,X,Y,EnemyType,Count[,flip]
        // EnemyType may contain commas (e.g. POOL[Type1,Type2,...]) - join parts[4]..[Length-2]
        if (parts.Length < 6) return false;
        if (!float.TryParse(parts[1].Trim(), out float chance)) return false;
        if (!float.TryParse(parts[2].Trim(), out float x)) return false;
        if (!float.TryParse(parts[3].Trim(), out float y)) return false;
        bool flipX = false;
        float rotationZ = 0f;
        int countIndex = parts.Length - 1;
        if (parts.Length >= 7 &&
            HellGateSpawnLineFormat.TryParseFlipField(parts[parts.Length - 1].Trim(), out flipX))
        {
            countIndex = parts.Length - 2;
        }

        string enemyTypeRaw = string.Join(",", parts, 4, countIndex - 4);
        ParseEnemySpec(enemyTypeRaw.Trim(), out string enemyTypeSpec, out string factionIdRaw, out string eventCoreEventId);
        string enemyType = ResolvePoolOrType(enemyTypeSpec);
        if (!int.TryParse(parts[countIndex].Trim(), out int count)) return false;

        pt = new SpawnPoint
        {
            X = x,
            Y = y,
            EnemyType = enemyType,
            Count = count,
            Chance = Mathf.Clamp01(chance),
            FactionIdRaw = factionIdRaw,
            EventCoreEventId = eventCoreEventId,
            FlipX = flipX,
            RotationZ = rotationZ
        };
        return true;
    }

    private static bool TryParseSpawnPoint(string line, out SpawnPoint pt)
    {
        pt = default;
        var parts = line.Split(',');
        if (parts.Length < 4) return false;
        if (!float.TryParse(parts[0].Trim(), out float x)) return false;
        if (!float.TryParse(parts[1].Trim(), out float y)) return false;

        bool flipX = false;
        float rotationZ = 0f;
        SpawnDepthSettings depth = SpawnDepthSettings.Empty;
        int countIndex = HellGateSpawnLineFormat.ResolveTrailingCountIndex(
            parts, 3, out flipX, out rotationZ, out depth);

        if (countIndex < 3)
            return false;

        string enemyTypeRaw = string.Join(",", parts, 2, countIndex - 2);
        ParseEnemySpec(enemyTypeRaw.Trim(), out string enemyTypeSpec, out string factionIdRaw, out string eventCoreEventId);
        string enemyType = ResolvePoolOrType(enemyTypeSpec);
        if (!int.TryParse(parts[countIndex].Trim(), out int count))
            return false;

        pt = new SpawnPoint
        {
            X = x,
            Y = y,
            EnemyType = enemyType,
            Count = count,
            Chance = 1f,
            FactionIdRaw = factionIdRaw,
            EventCoreEventId = eventCoreEventId,
            FlipX = flipX,
            RotationZ = rotationZ,
            Depth = depth
        };
        return true;
    }

    private static bool TryParseTemplateSpawnPoint(string line, out TemplateSpawnPoint pt)
    {
        pt = default;
        var parts = line.Split(',');
        if (parts.Length < 5)
            return false;

        string command = parts[0].Trim();
        if (string.Equals(command, "SPAWN", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "TEMPLATE", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 6)
                return false;
            string category = parts[1].Trim();
            if (!HellGateSpawnLineFormat.IsTrapCategory(category) &&
                !HellGateSpawnLineFormat.IsSpawnObjectTemplateCategory(category))
                return false;
            string key = parts[2].Trim();
            if (!float.TryParse(parts[3].Trim(), out float x))
                return false;
            if (!float.TryParse(parts[4].Trim(), out float y))
                return false;
            if (!int.TryParse(parts[5].Trim(), out int count))
                count = 1;

            bool flipX = false;
            float rotationZ = 0f;
            SpawnDepthSettings depth = SpawnDepthSettings.Empty;
            if (parts.Length > 6)
            {
                HellGateSpawnLineFormat.ParseOptionalPlacementFields(
                    parts, 6, out flipX, out rotationZ, out depth);
            }

            pt = new TemplateSpawnPoint
            {
                Category = category,
                Key = key,
                X = x,
                Y = y,
                Count = count,
                Chance = 1f,
                FlipX = flipX,
                RotationZ = rotationZ,
                Depth = depth
            };
            return true;
        }

        if (IsTemplateShortcutCommand(command))
        {
            string key = parts[1].Trim();
            if (!float.TryParse(parts[2].Trim(), out float x))
                return false;
            if (!float.TryParse(parts[3].Trim(), out float y))
                return false;
            if (!int.TryParse(parts[4].Trim(), out int count))
                count = 1;

            bool flipX = false;
            float rotationZ = 0f;
            SpawnDepthSettings depth = SpawnDepthSettings.Empty;
            if (parts.Length > 5)
            {
                HellGateSpawnLineFormat.ParseOptionalPlacementFields(
                    parts, 5, out flipX, out rotationZ, out depth);
            }

            pt = new TemplateSpawnPoint
            {
                Category = command,
                Key = key,
                X = x,
                Y = y,
                Count = count,
                Chance = 1f,
                FlipX = flipX,
                RotationZ = rotationZ,
                Depth = depth
            };
            return true;
        }

        return false;
    }

    private static bool IsTemplateShortcutCommand(string command)
    {
        return HellGateSpawnLineFormat.IsTrapShortcut(command) ||
               HellGateSpawnLineFormat.IsObjectShortcut(command) ||
               HellGateSpawnLineFormat.IsDecorShortcut(command) ||
               HellGateSpawnLineFormat.IsHostageShortcut(command);
    }

    private static bool TryParseRandomHostageTemplate(string line, out TemplateSpawnPoint pt)
    {
        pt = default;
        var parts = line.Split(',');
        if (parts.Length < 6)
            return false;

        if (!float.TryParse(parts[1].Trim(), out float chance))
            return false;
        string key = parts[2].Trim();
        if (!float.TryParse(parts[3].Trim(), out float x))
            return false;
        if (!float.TryParse(parts[4].Trim(), out float y))
            return false;
        if (!int.TryParse(parts[5].Trim(), out int count))
            count = 1;

        bool flipX = false;
        float rotationZ = 0f;
        SpawnDepthSettings depth = SpawnDepthSettings.Empty;
        if (parts.Length > 6)
        {
            HellGateSpawnLineFormat.ParseOptionalPlacementFields(
                parts, 6, out flipX, out rotationZ, out depth);
        }

        pt = new TemplateSpawnPoint
        {
            Category = "HOSTAGE",
            Key = key,
            X = x,
            Y = y,
            Count = count,
            Chance = Mathf.Clamp01(chance),
            FlipX = flipX,
            RotationZ = rotationZ,
            Depth = depth
        };
        return true;
    }

    /// <summary>
    /// Supports per-point metadata in EnemyType field, example:
    /// TouzokuNormal|faction=bandits_inquisition
    /// TouzokuNormal|ec_event=eventcore_smoke_test (alias: |ec=)
    /// TouzokuNormal|ec_pool=id_a,id_b — uniform random EventCore id per spawn (overrides ec_event when present)
    /// POOL[TouzokuNormal,TouzokuAxe]|faction=bandits_mafia
    /// </summary>
    private static void ParseEnemySpec(string raw, out string enemyTypeSpec, out string factionIdRaw, out string eventCoreEventId)
    {
        enemyTypeSpec = raw;
        factionIdRaw = null;
        eventCoreEventId = null;
        if (string.IsNullOrEmpty(raw))
            return;

        string[] chunks = raw.Split('|');
        if (chunks.Length == 0)
            return;

        enemyTypeSpec = chunks[0].Trim();
        string ecSingleId = null;
        string ecPoolCsv = null;

        for (int i = 1; i < chunks.Length; i++)
        {
            string chunk = chunks[i].Trim();
            if (chunk.StartsWith("faction=", StringComparison.OrdinalIgnoreCase))
            {
                string value = chunk.Substring("faction=".Length).Trim();
                if (!string.IsNullOrEmpty(value))
                    factionIdRaw = value;
            }
            else if (chunk.StartsWith("ec_pool=", StringComparison.OrdinalIgnoreCase))
            {
                string value = chunk.Substring("ec_pool=".Length).Trim();
                if (!string.IsNullOrEmpty(value))
                    ecPoolCsv = value;
            }
            else if (chunk.StartsWith("ec_event=", StringComparison.OrdinalIgnoreCase))
            {
                string value = chunk.Substring("ec_event=".Length).Trim();
                if (!string.IsNullOrEmpty(value))
                    ecSingleId = value;
            }
            else if (chunk.StartsWith("ec=", StringComparison.OrdinalIgnoreCase))
            {
                string value = chunk.Substring("ec=".Length).Trim();
                if (!string.IsNullOrEmpty(value))
                    ecSingleId = value;
            }
        }

        if (!string.IsNullOrEmpty(ecPoolCsv))
        {
            var ids = new List<string>();
            foreach (string piece in ecPoolCsv.Split(','))
            {
                string id = piece.Trim();
                if (!string.IsNullOrEmpty(id))
                    ids.Add(id);
            }

            if (ids.Count > 0)
                eventCoreEventId = ids[Random.Range(0, ids.Count)];
            return;
        }

        eventCoreEventId = ecSingleId;
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
        bool traceElite = IsEliteSpawnTrace(pt.EnemyType);

        if (!EnemyPrefabRegistry.TryGetPrefab(pt.EnemyType, out GameObject prefab)
            || !EnemyPrefabRegistry.PrefabMatchesConfigKey(pt.EnemyType, prefab))
        {
            string currentSceneName = GetCurrentSceneName();
            string templateCategory = SpawnTemplateCatalog.ResolveTemplateCategory(pt.EnemyType);
            string templateLog = HellGateSpawnLineFormat.IsHostageShortcut(templateCategory)
                ? "[SPAWN TEMPLATE] [HOSTAGE]"
                : "[SPAWN TEMPLATE]";
            if (SpawnTemplateCatalog.TrySpawn(
                    templateCategory, pt.EnemyType, center, pt.Count, templateLog, currentSceneName,
                    pt.FlipX, pt.RotationZ, pt.Depth))
                return pt.Count;

            Plugin.Log?.LogWarning(
                $"[ENEMY REGISTRY] Prefab not found for: {pt.EnemyType} (scene=\"{currentSceneName}\", templateReady={SpawnTemplateCatalog.IsReady})");
            return 0;
        }

        if (traceElite)
        {
            Plugin.Log?.LogInfo(
                $"[SPAWN ELITE] {pt.EnemyType} prefab=\"{prefab.name}\" componentOk={EnemyPrefabRegistry.PrefabMatchesConfigKey(pt.EnemyType, prefab)} at ({center.x:F2},{center.y:F2})");
        }

        int spawned = 0;
        if (pt.Count == 1)
        {
            GameObject instance = SpawnSingle(prefab, center, pt.EnemyType, pt.FactionIdRaw, pt.EventCoreEventId, pt.FlipX);
            if (instance != null)
                spawned = 1;
            else if (traceElite)
                Plugin.Log?.LogWarning($"[SPAWN ELITE] {pt.EnemyType} SpawnSingle returned null at ({center.x:F2},{center.y:F2})");
        }
        else
        {
            for (int i = 0; i < pt.Count; i++)
            {
                Vector2 offset = CalculateOffset(i, pt.Count);
                GameObject instance = SpawnSingle(
                    prefab, center + offset, pt.EnemyType, pt.FactionIdRaw, pt.EventCoreEventId, pt.FlipX);
                if (instance != null)
                    spawned++;
            }
        }
        return spawned;
    }

    private static bool IsEliteSpawnTrace(string enemyType)
    {
        return string.Equals(enemyType, "Butcher", StringComparison.OrdinalIgnoreCase)
            || string.Equals(enemyType, "Slaughterer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(enemyType, "Sisterknight", StringComparison.OrdinalIgnoreCase);
    }

    private static GameObject SpawnSingle(
        GameObject prefab,
        Vector2 position,
        string enemyType,
        string factionIdRaw,
        string eventCoreEventId,
        bool flipX = false,
        bool markHostileToPlayer = false,
        bool suppressFactionMarker = false)
    {
        try
        {
            bool traceBrokerGate = ShouldTraceBrokerGateSpawn(position, eventCoreEventId);
            string appliedFactionIdRaw = factionIdRaw;
            if (!string.IsNullOrEmpty(eventCoreEventId) && string.IsNullOrEmpty(appliedFactionIdRaw))
                appliedFactionIdRaw = "eventcore_encounter";

            if (traceBrokerGate)
                Plugin.Log?.LogInfo($"[SPAWN DEBUG] SpawnSingle requested. EnemyType={enemyType}, Position=({position.x:F2},{position.y:F2}), Faction={appliedFactionIdRaw ?? "<none>"}, EventCore={eventCoreEventId ?? "<none>"}.");

            // Keep prefab Z (Vector2 Instantiate forces z=0 and can hide enemies behind level art).
            float spawnZ = prefab != null ? prefab.transform.position.z : 0f;
            Vector3 worldPos = new Vector3(position.x, position.y, spawnZ);
            GameObject spawned = Object.Instantiate(prefab, worldPos, Quaternion.identity);
            if (spawned == null) return null;

            MoveSpawnedToGameplayScene(spawned);

            // Wire faction/metadata before first start_fun (OnEnable) to avoid roster-default race.
            if (spawned.activeSelf)
                spawned.SetActive(false);

            if (string.Equals(enemyType, "BossTouzokuCustom", StringComparison.OrdinalIgnoreCase)
                || string.Equals(enemyType, "HellishTouzokuBoss", StringComparison.OrdinalIgnoreCase))
                BossTouzokuCustomRuntime.PrepareSpawnedInstance(spawned);
            else if (IsBigoniBrotherEnemyType(enemyType))
            {
                spawned.name = "BigoniBrother";
                Bigoni bigoni = spawned.GetComponent<Bigoni>();
                if (bigoni != null)
                    NoREroMod.Patches.Enemy.BigoniBrotherIdentity.RegisterBrother(bigoni);
            }
            else if (string.Equals(enemyType, "MafiaBossCustom", StringComparison.OrdinalIgnoreCase))
                spawned.name = "MafiaBossCustom";

            MarkManagedSpawn(spawned);
            if (suppressFactionMarker)
            {
                SpawnManagedInstance managedMarker = spawned.GetComponent<SpawnManagedInstance>();
                if (managedMarker != null)
                    managedMarker.SuppressFactionMarker = true;
            }
            if (!string.IsNullOrEmpty(appliedFactionIdRaw))
            {
                SpawnFactionOverride overrideComponent = spawned.GetComponent<SpawnFactionOverride>();
                if (overrideComponent == null)
                    overrideComponent = spawned.AddComponent<SpawnFactionOverride>();
                overrideComponent.FactionIdRaw = appliedFactionIdRaw;
            }

            if (!string.IsNullOrEmpty(eventCoreEventId))
            {
                EventCoreHost host = spawned.GetComponent<EventCoreHost>();
                if (host == null)
                    host = spawned.AddComponent<EventCoreHost>();
                host.Configure(eventCoreEventId);

                if (traceBrokerGate)
                    Plugin.Log?.LogInfo($"[SPAWN DEBUG] EventCoreHost configured on tracked spawn. Host attached={host != null}, EventId={eventCoreEventId}.");
            }

            if (IsBigoniBrotherEnemyType(enemyType))
            {
                if (spawned.name != "BigoniBrother")
                    spawned.name = "BigoniBrother";
                Bigoni brother = spawned.GetComponent<Bigoni>();
                if (brother != null)
                    NoREroMod.Patches.Enemy.BigoniBrotherIdentity.RegisterBrother(brother);
            }
            else if (string.Equals(enemyType, "MafiaBossCustom", StringComparison.OrdinalIgnoreCase))
            {
                if (spawned.name != "MafiaBossCustom")
                    spawned.name = "MafiaBossCustom";
            }
            else if (string.Equals(enemyType, "biscord", StringComparison.OrdinalIgnoreCase))
            {
                spawned.name = "biscord";
                if (spawned.GetComponent<BiscodMarker>() == null)
                    spawned.AddComponent<BiscodMarker>();
                if (spawned.GetComponent<BiscodRuntimeProfile>() == null)
                    spawned.AddComponent<BiscodRuntimeProfile>();
                if (spawned.GetComponent<BiscodVisualProfile>() == null)
                    spawned.AddComponent<BiscodVisualProfile>();
                if (spawned.GetComponent<BiscodEyesAttachment>() == null)
                    spawned.AddComponent<BiscodEyesAttachment>();
            }
            else if (enemyType == "Wolf")
            {
                spawned.name = "Wolf";
                WolfSkeletonLoader.ApplyWolfSkeletons(spawned);
            }
            else if (string.Equals(enemyType, "Butcher", StringComparison.OrdinalIgnoreCase))
            {
                spawned.name = "Butcher";
                ButcherFatalityLoader.ApplyButcherFatality(spawned);
            }
            else if (string.Equals(enemyType, "HellishTouzokuBoss", StringComparison.OrdinalIgnoreCase))
            {
                spawned.name = "HellishTouzokuBoss";
                HellishTouzokuSkeletonLoader.ApplySkeletons(spawned, HellishTouzokuVariant.Boss);
            }
            else if (string.Equals(enemyType, "HellishTouzokuAxe", StringComparison.OrdinalIgnoreCase))
            {
                spawned.name = "HellishTouzokuAxe";
                HellishTouzokuSkeletonLoader.ApplySkeletons(spawned, HellishTouzokuVariant.Axe);
            }
            else if (string.Equals(enemyType, "HellishTouzokuSword", StringComparison.OrdinalIgnoreCase))
            {
                spawned.name = "HellishTouzokuSword";
                HellishTouzokuSkeletonLoader.ApplySkeletons(spawned, HellishTouzokuVariant.Sword);
            }

            if (flipX)
                SpawnFlipUtility.LockHorizontalFlipLeft(spawned);

            if (!string.Equals(enemyType, "BossTouzokuCustom", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(enemyType, "HellishTouzokuBoss", StringComparison.OrdinalIgnoreCase))
                HellGateBossSpawnRuntime.ConfigureSpawnedBossIfNeeded(spawned);

            if (markHostileToPlayer)
            {
                SpawnManagedInstance managed = spawned.GetComponent<SpawnManagedInstance>();
                if (managed != null)
                    managed.SpawnHostileToPlayer = true;
            }

            if (!spawned.activeSelf)
                spawned.SetActive(true);

            PrepareSpawnedEnemyPresentation(spawned);
            StabilizeSpawnedEnemyPhysics(spawned, worldPos);

            FinalizeSpawnedEnemyFaction(spawned);

            if (markHostileToPlayer)
            {
                EnemyDate spawnedEnemy = spawned.GetComponent<EnemyDate>();
                if (spawnedEnemy != null)
                    EnemyFactionRuntime.MarkSessionHostileToPlayer(spawnedEnemy);
            }

            if (traceBrokerGate || IsEliteSpawnTrace(enemyType))
            {
                MeshRenderer mesh = spawned.GetComponent<MeshRenderer>();
                Plugin.Log?.LogInfo(
                    $"[SPAWN DEBUG] SpawnSingle completed. name={spawned.name}, type={enemyType}, " +
                    $"pos={spawned.transform.position}, scene={spawned.scene.name}, " +
                    $"activeSelf={spawned.activeSelf}, activeInHierarchy={spawned.activeInHierarchy}, " +
                    $"meshEnabled={(mesh != null && mesh.enabled)}.");
            }

            return spawned;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN] Failed to spawn {enemyType}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parent spawned objects into the loaded gameplay zone scene (enemies and templates).
    /// </summary>
    internal static void MoveSpawnedToGameplayScene(GameObject spawned)
    {
        if (spawned == null)
            return;

        try
        {
            string zone = HellGateLocationSpawnRefresh.GetLoadedGameplayLevelScene();
            if (string.IsNullOrEmpty(zone))
                zone = HellGateLocationSpawnRefresh.GetActiveGameplayZone();
            if (string.IsNullOrEmpty(zone))
                return;

            Scene target = SceneManager.GetSceneByName(zone);
            if (!target.IsValid() || !target.isLoaded)
                return;
            if (string.Equals(spawned.scene.name, target.name, StringComparison.OrdinalIgnoreCase))
                return;

            SceneManager.MoveGameObjectToScene(spawned, target);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN] MoveGameObjectToScene failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Templates cloned while inactive often keep a dead Spine mesh (exists, but invisible).
    /// Force renderer + skeleton refresh after the instance is activated.
    /// </summary>
    internal static void PrepareSpawnedEnemyPresentation(GameObject spawned)
    {
        if (spawned == null)
            return;

        try
        {
            MeshRenderer[] meshes = spawned.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] != null)
                    meshes[i].enabled = true;
            }

            SkeletonAnimation[] spines = spawned.GetComponentsInChildren<SkeletonAnimation>(true);
            for (int i = 0; i < spines.Length; i++)
            {
                SkeletonAnimation spine = spines[i];
                if (spine == null || spine.skeletonDataAsset == null)
                    continue;

                // Skip erodata until grab — combat body must be visible.
                if (spine.gameObject != null
                    && spine.gameObject.name != null
                    && spine.gameObject.name.IndexOf("ero", StringComparison.OrdinalIgnoreCase) >= 0
                    && spine.gameObject != spawned)
                    continue;

                spine.Initialize(true);
                try
                {
                    if (spine.state != null
                        && spine.state.GetCurrent(0) == null
                        && !string.IsNullOrEmpty(spine.AnimationName))
                    {
                        spine.state.SetAnimation(0, spine.AnimationName, true);
                    }
                }
                catch
                {
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN] PrepareSpawnedEnemyPresentation failed: {ex.Message}");
        }
    }

    private static void StabilizeSpawnedEnemyPhysics(GameObject spawned, Vector3 worldPos)
    {
        if (spawned == null)
            return;

        try
        {
            Rigidbody2D body = spawned.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            // Snap back if Start/physics nudged the clone before colliders settled.
            spawned.transform.position = worldPos;
        }
        catch
        {
        }
    }

    private static void LogEliteLiveSnapshot(string logPrefix)
    {
        try
        {
            Slaughterer[] butchers = Object.FindObjectsOfType<Slaughterer>();
            Sisterknight[] sisters = Object.FindObjectsOfType<Sisterknight>();
            Plugin.Log?.LogInfo(
                $"{logPrefix} Elite live: Slaughterer/Butcher={butchers?.Length ?? 0}, Sisterknight={sisters?.Length ?? 0}.");

            if (butchers != null)
            {
                for (int i = 0; i < butchers.Length; i++)
                {
                    Slaughterer b = butchers[i];
                    if (b == null) continue;
                    MeshRenderer mesh = b.GetComponent<MeshRenderer>();
                    Plugin.Log?.LogInfo(
                        $"{logPrefix}   Butcher[{i}] name={b.name} pos={b.transform.position} scene={b.gameObject.scene.name} " +
                        $"active={b.gameObject.activeInHierarchy} mesh={(mesh != null && mesh.enabled)}");
                }
            }

            if (sisters != null)
            {
                for (int i = 0; i < sisters.Length; i++)
                {
                    Sisterknight s = sisters[i];
                    if (s == null) continue;
                    MeshRenderer mesh = s.GetComponent<MeshRenderer>();
                    Plugin.Log?.LogInfo(
                        $"{logPrefix}   Sister[{i}] name={s.name} pos={s.transform.position} scene={s.gameObject.scene.name} " +
                        $"active={s.gameObject.activeInHierarchy} mesh={(mesh != null && mesh.enabled)}");
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"{logPrefix} Elite snapshot failed: {ex.Message}");
        }
    }

    private static IEnumerator LogEliteLiveSnapshotDelayed(string logPrefix, float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        LogEliteLiveSnapshot($"{logPrefix} [+{delaySeconds:0.#}s]");
    }

    private static void FinalizeSpawnedEnemyFaction(GameObject spawned)
    {
        if (spawned == null || !EnemyFactionsConfig.Enable)
            return;

        SpawnManagedInstance managed = spawned.GetComponent<SpawnManagedInstance>();
        if (managed == null)
            return;

        EnemyDate enemy = spawned.GetComponent<EnemyDate>();
        if (enemy == null)
            return;

        EnemyFactionRuntime.RegisterEnemy(enemy);
        EnemyDateFactionColorBootstrapPatch.ApplyFactionMarker(enemy);
    }

    private static bool IsBigoniBrotherEnemyType(string enemyType)
    {
        return string.Equals(enemyType, "BigoniBrother", StringComparison.OrdinalIgnoreCase);
    }

    internal static void CleanupManagedSpawns()
    {
        GoldPickup.CleanupAllUncollectedInScene();

        SpawnManagedInstance[] markers = Object.FindObjectsOfType<SpawnManagedInstance>();
        for (int i = 0; i < markers.Length; i++)
        {
            SpawnManagedInstance marker = markers[i];
            if (marker == null || marker.gameObject == null)
                continue;

            Object.Destroy(marker.gameObject);
        }
    }

    private static void MarkManagedSpawn(GameObject spawned)
    {
        if (spawned == null)
            return;

        if (spawned.GetComponent<SpawnManagedInstance>() == null)
            spawned.AddComponent<SpawnManagedInstance>();
    }

    internal static Vector2 GetGroupSpawnOffset(int index, int total) => CalculateOffset(index, total);

    private static Vector2 CalculateOffset(int index, int total)
    {
        if (total <= 1) return Vector2.zero;
        float angle = (360f / total) * index * Mathf.Deg2Rad;
        return new Vector2(
            Mathf.Cos(angle) * DefaultRadius,
            Mathf.Sin(angle) * DefaultRadius * OffsetScaleY
        );
    }

    private static string GetCurrentSceneName()
    {
        try
        {
            var fragMng = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetGameFragMng();
            if (fragMng != null && !string.IsNullOrEmpty(fragMng._re_Scenename))
                return fragMng._re_Scenename;
        }
        catch
        {
        }

        try
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool ShouldTraceBrokerGateConfig(string configPath)
    {
        if (string.IsNullOrEmpty(configPath))
            return false;

        return string.Equals(Path.GetFileName(configPath), NightlessCityCFileName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldTraceBrokerGateLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return false;

        return line.IndexOf(BrokerGateEventId, StringComparison.OrdinalIgnoreCase) >= 0 ||
               line.IndexOf("742.88,-286.79", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ShouldTraceBrokerGateSpawn(Vector2 position, string eventCoreEventId)
    {
        if (string.Equals(eventCoreEventId, BrokerGateEventId, StringComparison.OrdinalIgnoreCase))
            return true;

        return Mathf.Abs(position.x - BrokerGateDebugX) <= BrokerGateDebugTolerance &&
               Mathf.Abs(position.y - BrokerGateDebugY) <= BrokerGateDebugTolerance;
    }

    private static bool IsTrackedBrokerGatePoint(SpawnPoint pt)
    {
        if (string.Equals(pt.EventCoreEventId, BrokerGateEventId, StringComparison.OrdinalIgnoreCase))
            return true;

        return Mathf.Abs(pt.X - BrokerGateDebugX) <= BrokerGateDebugTolerance &&
               Mathf.Abs(pt.Y - BrokerGateDebugY) <= BrokerGateDebugTolerance;
    }

    private static void LogTrackedBrokerGatePoint(string logPrefix, string stage, SpawnPoint pt, int lineNumber = -1)
    {
        string lineSuffix = lineNumber > 0 ? $", line={lineNumber}" : string.Empty;
        Plugin.Log?.LogInfo(
            $"{logPrefix} [DEBUG] {stage}{lineSuffix}. Position=({pt.X:F2},{pt.Y:F2}), EnemyType={pt.EnemyType}, Count={pt.Count}, Faction={pt.FactionIdRaw ?? "<none>"}, EventCore={pt.EventCoreEventId ?? "<none>"}.");
    }

    private struct SpawnPoint
    {
        public float X, Y;
        public string EnemyType;
        public string FactionIdRaw;
        public string EventCoreEventId;
        public int Count;
        public float Chance;
        public bool FlipX;
        public float RotationZ;
        public SpawnDepthSettings Depth;
    }

    private struct TemplateSpawnPoint
    {
        public float X, Y;
        public string Category;
        public string Key;
        public int Count;
        public float Chance;
        public bool FlipX;
        public float RotationZ;
        public SpawnDepthSettings Depth;
    }

    private struct GoldSpawnPoint
    {
        public float X, Y;
        public int MinAmount;
        public int MaxAmount;
        public int Count;
        public float Chance;
    }

    /// <summary>
    /// Coordinate shorthand: <c>X,Y,gold=100-300,Count</c> or fixed <c>gold=150,Count</c>.
    /// </summary>
    private static bool TryParseGoldSpawnPoint(string line, out GoldSpawnPoint pt)
    {
        pt = default;
        if (!TryParseSpawnPoint(line, out SpawnPoint point))
            return false;

        if (!TryParseGoldAmountSpec(point.EnemyType, out int minAmount, out int maxAmount))
            return false;

        pt = new GoldSpawnPoint
        {
            X = point.X,
            Y = point.Y,
            MinAmount = minAmount,
            MaxAmount = maxAmount,
            Count = point.Count > 0 ? point.Count : 1,
            Chance = 1f
        };
        return true;
    }

    /// <summary>
    /// Explicit shortcut: <c>GOLD,X,Y,100-300,Count</c> or <c>GOLD,X,Y,150,Count</c>.
    /// </summary>
    private static bool TryParseGoldShortcutLine(string line, out GoldSpawnPoint pt)
    {
        pt = default;
        var parts = line.Split(',');
        if (parts.Length < 5)
            return false;

        if (!string.Equals(parts[0].Trim(), "GOLD", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!float.TryParse(parts[1].Trim(), out float x))
            return false;
        if (!float.TryParse(parts[2].Trim(), out float y))
            return false;
        if (!TryParseGoldRangeToken(parts[3].Trim(), out int minAmount, out int maxAmount))
            return false;

        int count = 1;
        if (parts.Length >= 5 && !int.TryParse(parts[4].Trim(), out count))
            count = 1;
        if (count <= 0)
            count = 1;

        pt = new GoldSpawnPoint
        {
            X = x,
            Y = y,
            MinAmount = minAmount,
            MaxAmount = maxAmount,
            Count = count,
            Chance = 1f
        };
        return true;
    }

    /// <summary>
    /// <c>RANDOM,chance,X,Y,gold=100-300,Count</c>
    /// </summary>
    private static bool TryParseRandomGoldPoint(string line, out GoldSpawnPoint pt)
    {
        pt = default;
        if (!TryParseRandomPoint(line, out SpawnPoint point))
            return false;

        if (!TryParseGoldAmountSpec(point.EnemyType, out int minAmount, out int maxAmount))
            return false;

        pt = new GoldSpawnPoint
        {
            X = point.X,
            Y = point.Y,
            MinAmount = minAmount,
            MaxAmount = maxAmount,
            Count = point.Count > 0 ? point.Count : 1,
            Chance = point.Chance
        };
        return true;
    }

    private static bool TryParseGoldAmountSpec(string raw, out int minAmount, out int maxAmount)
    {
        minAmount = 0;
        maxAmount = 0;
        if (string.IsNullOrEmpty(raw))
            return false;

        string spec = raw.Trim();
        if (!spec.StartsWith("gold=", StringComparison.OrdinalIgnoreCase))
            return false;

        return TryParseGoldRangeToken(spec.Substring("gold=".Length).Trim(), out minAmount, out maxAmount);
    }

    private static bool TryParseGoldRangeToken(string token, out int minAmount, out int maxAmount)
    {
        minAmount = 0;
        maxAmount = 0;
        if (string.IsNullOrEmpty(token))
            return false;

        token = token.Trim();
        int dash = token.IndexOf('-');
        if (dash > 0)
        {
            if (!int.TryParse(token.Substring(0, dash).Trim(), out minAmount))
                return false;
            if (!int.TryParse(token.Substring(dash + 1).Trim(), out maxAmount))
                return false;
        }
        else if (!int.TryParse(token, out minAmount))
        {
            return false;
        }
        else
        {
            maxAmount = minAmount;
        }

        if (minAmount > maxAmount)
        {
            int swap = minAmount;
            minAmount = maxAmount;
            maxAmount = swap;
        }

        if (minAmount < 1)
            minAmount = 1;
        if (maxAmount < minAmount)
            maxAmount = minAmount;

        return true;
    }

    private static int SpawnGoldPointAt(GoldSpawnPoint pt, string logPrefix)
    {
        if (!EconomicConfig.Enable)
            return 0;

        Vector2 center = new Vector2(pt.X, pt.Y);
        int spawned = 0;
        int count = pt.Count > 0 ? pt.Count : 1;

        for (int i = 0; i < count; i++)
        {
            Vector2 position = count == 1 ? center : center + CalculateOffset(i, count);
            int amount = Random.Range(pt.MinAmount, pt.MaxAmount + 1);
            GameObject pickup = GoldDropAwarder.TrySpawnPlacedPickup(position, amount);
            if (pickup == null)
            {
                if (logPrefix != null)
                    Plugin.Log?.LogWarning($"{logPrefix} [GOLD] Failed to spawn placed gold at ({position.x:F2},{position.y:F2}), amount={amount}.");
                continue;
            }

            MarkManagedSpawn(pickup);
            spawned++;

            // Per-pile verbose (disabled): see the "[GOLD] Spawned N placed pile(s)" summary instead.
            // if (EconomicConfig.DebugLogging && logPrefix != null)
            //     Plugin.Log?.LogInfo($"{logPrefix} [GOLD] Placed {amount} at ({position.x:F2},{position.y:F2}).");
        }

        return spawned;
    }

    internal static bool TryParseEnemyTypeFromSpawnLine(string line, out string enemyType)
    {
        enemyType = string.Empty;
        if (string.IsNullOrEmpty(line) || line.Trim().Length == 0)
            return false;

        if (TryParseSpawnPoint(line.Trim(), out SpawnPoint point))
        {
            enemyType = point.EnemyType;
            return !string.IsNullOrEmpty(enemyType);
        }

        if (TryParseRandomPoint(line.Trim(), out SpawnPoint randomPoint))
        {
            enemyType = randomPoint.EnemyType;
            return !string.IsNullOrEmpty(enemyType);
        }

        return false;
    }
}
