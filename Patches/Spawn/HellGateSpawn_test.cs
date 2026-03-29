using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace NoREroMod;

/// <summary>
/// Test spawn enemies in Underground Church by recorded coordinates
/// Spawns enemies from ALL_ENEMIES.txt (строки 40-45) by points from spawnpoint.log
/// </summary>
internal class HellGateSpawn_test
{
    // Список enemies for spawn (from ALL_ENEMIES.txt строки 40-45 + Touzoku)
    // Use реальные имеon префабоin from игры
    private static readonly string[] TestEnemies = {
        "OtherSlavebigAxe",    // Другой большой раб with топором -> Axe
        "Pilgrim",            // Пилигрим -> Pilgrim
        "Praymaiden",         // Молящаяся дева -> ? (not found)
        "PrisonOfficer",      // Тюремный офицер -> PrisonofficerB
        "RequiemKnight",      // Рыцарь реквиема -> requiemKnight
        "Sheepheaddemon",     // Овцеheadй демон -> Head
        "Touzoku",            // Мечник -> Touzoku (x1)
        "Touzoku"             // Мечник -> Touzoku (x2)
    };

    // Координаты spawn from spawnpoint.log (Underground Church)
    private static readonly Vector2[] SpawnPoints = {
        new Vector2(305.76f, -80.37f),  // Точка 1
        new Vector2(307.76f, -80.37f),  // Точка 2
        new Vector2(307.78f, -80.37f),  // Точка 3
        new Vector2(322.53f, -80.37f),  // Точка 4
        new Vector2(327.51f, -80.37f)   // Точка 5
    };

    // Кэшированные префабы enemies
    private static readonly Dictionary<string, GameObject> enemyPrefabs = new();
    private static bool prefabsLoaded = false;
    private static bool spawnCompleted = false;

    [HarmonyPatch(typeof(SpawnParent), "fun_SpawnRE")]
    [HarmonyPostfix]
    private static void OnSpawnReset()
    {
        try
        {
            // Reset flag on respawn enemies (активация сейв-поинта)
            spawnCompleted = false;
            Plugin.Log.LogInfo("[HELLGATE SPAWN TEST] Spawn reset detected, will spawn enemies on next update");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[HELLGATE SPAWN TEST] Error in spawn reset: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(Spawnenemy), "Update")]
    [HarmonyPostfix]
    private static void CheckAndSpawnTestEnemies(Spawnenemy __instance,
                                                  GameObject ___enemy,
                                                  int ___SpawnNumber,
                                                  SpawnParent ___Spawnparent)
    {
        try
        {
            // Check if that мы in Underground Church
            if (!IsInUndergroundChurch())
            {
                return;
            }

            // Спавним only один раз за респавн-сессию
            if (spawnCompleted)
            {
                return;
            }

            // Загружаем префабы on первом вызове
            if (!prefabsLoaded)
            {
                LoadEnemyPrefabs();
                prefabsLoaded = true;
            }

            // Спавним тестовых enemies
            SpawnTestEnemies();
            spawnCompleted = true;

        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[HELLGATE SPAWN TEST] Error in spawn check: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks that player is in Underground Church
    /// </summary>
    private static bool IsInUndergroundChurch()
    {
        try
        {
            var fragMng = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetGameFragMng();
            if (fragMng != null && !string.IsNullOrEmpty(fragMng._re_Scenename))
            {
                return fragMng._re_Scenename.Contains("UndergroundChurch") ||
                       fragMng._re_Scenename.Contains("underground");
            }

            // Fallback: проверка by name сцены Unity
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            return sceneName.Contains("UndergroundChurch") || sceneName.Contains("underground");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Load prefabs all test enemies
    /// </summary>
    private static void LoadEnemyPrefabs()
    {
        try
        {
            enemyPrefabs.Clear();

            // Log все доступные префабы for debugging
            var allPrefabs = Resources.FindObjectsOfTypeAll<GameObject>();
            Plugin.Log.LogInfo($"[HELLGATE SPAWN TEST] Found {allPrefabs.Length} total GameObjects in scene");

            // Log префабы связанные with Praymaiden
            foreach (GameObject obj in allPrefabs)
            {
                if (obj != null && (obj.name.ToLower().Contains("pray") || obj.name.ToLower().Contains("maiden")))
                {
                    Plugin.Log.LogInfo($"[HELLGATE SPAWN TEST] Found potential Praymaiden prefab: '{obj.name}'");
                }
            }

            // Ищем префабы среди всех GameObject in сцене
            foreach (GameObject obj in allPrefabs)
            {
                if (obj == null) continue;

                string objName = obj.name.ToLower();

                foreach (string enemyName in TestEnemies)
                {
                    string searchName = enemyName.ToLower();
                    if (!enemyPrefabs.ContainsKey(enemyName))
                    {
                        bool found = false;

                        // Разные варианты поиска
                        if (objName.Contains(searchName) ||
                            searchName.Contains(objName) ||
                            objName.IndexOf(searchName.Replace("slave", "").Replace("big", "").Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            found = true;
                        }
                        // Специальные случаи
                        else if (enemyName == "Praymaiden" && (objName.Contains("pray") || objName.Contains("maiden") || objName == "praymaiden"))
                        {
                            found = true;
                        }
                        else if (enemyName == "OtherSlavebigAxe" && objName == "axe")
                        {
                            found = true;
                        }
                        else if (enemyName == "PrisonOfficer" && objName == "prisonofficerb")
                        {
                            found = true;
                        }
                        else if (enemyName == "RequiemKnight" && objName == "requiemknight")
                        {
                            found = true;
                        }
                        else if (enemyName == "Sheepheaddemon" && objName == "head")
                        {
                            found = true;
                        }
                        else if (enemyName == "Touzoku" && (objName == "touzoku" || objName.Contains("touzoku")))
                        {
                            found = true;
                        }

                        if (found)
                        {
                            enemyPrefabs[enemyName] = obj;
                            Plugin.Log.LogInfo($"[HELLGATE SPAWN TEST] Found prefab '{obj.name}' for enemy '{enemyName}'");
                            break;
                        }
                    }
                }
            }

            // Также ищем via reflection in Spawnenemy объектах
            var enemyField = typeof(Spawnenemy).GetField("enemy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (enemyField != null)
            {
                foreach (Spawnenemy spawnPoint in Object.FindObjectsOfType<Spawnenemy>())
                {
                    GameObject enemyPrefab = enemyField.GetValue(spawnPoint) as GameObject;
                    if (enemyPrefab != null)
                    {
                        string prefabName = enemyPrefab.name.ToLower();

                        foreach (string enemyName in TestEnemies)
                        {
                            if (!enemyPrefabs.ContainsKey(enemyName) &&
                                prefabName.Contains(enemyName.ToLower()))
                            {
                                enemyPrefabs[enemyName] = enemyPrefab;
                                Plugin.Log.LogInfo($"[HELLGATE SPAWN TEST] Found prefab '{enemyPrefab.name}' via reflection for enemy '{enemyName}'");
                                break;
                            }
                        }
                    }
                }
            }

            Plugin.Log.LogInfo($"[HELLGATE SPAWN TEST] Loaded {enemyPrefabs.Count}/{TestEnemies.Length} enemy prefabs");
            if (enemyPrefabs.Count == 0)
            {
                Plugin.Log.LogWarning("[HELLGATE SPAWN TEST] No enemy prefabs found! Check enemy names in ALL_ENEMIES.txt");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[HELLGATE SPAWN TEST] Error loading prefabs: {ex.Message}");
        }
    }

    /// <summary>
    /// Спавнит test enemies by recorded coordinates
    /// </summary>
    private static void SpawnTestEnemies()
    {
        try
        {
            Plugin.Log.LogInfo("[HELLGATE SPAWN TEST] Starting to spawn test enemies (8 total: 6 from ALL_ENEMIES + 2 Touzoku)...");

            int spawnedCount = 0;

            // Распределяем 8 enemies by 5 точкам (некоторые точки получат by 2-3 enemy)
            for (int i = 0; i < TestEnemies.Length; i++)
            {
                string enemyName = TestEnemies[i];
                Vector2 spawnPos = SpawnPoints[Math.Min(i, SpawnPoints.Length - 1)];

                if (enemyPrefabs.TryGetValue(enemyName, out GameObject prefab))
                {
                    // Спавним enemy
                    Vector3 finalPos = new Vector3(spawnPos.x, spawnPos.y, 0f);
                    GameObject enemy = Object.Instantiate(prefab, finalPos, Quaternion.identity);

                    // Добавляем небольшое случайное offset so that enemyи not накладывались
                    if (enemy != null)
                    {
                        Vector3 randomOffset = new Vector3(
                            Random.Range(-1.5f, 1.5f),
                            Random.Range(-1.5f, 1.5f),
                            0f
                        );
                        enemy.transform.position += randomOffset;

                        spawnedCount++;
                        Plugin.Log.LogInfo($"[HELLGATE SPAWN TEST] ✓ Spawned {enemyName} at ({enemy.transform.position.x:F2}, {enemy.transform.position.y:F2})");
                    }
                    else
                    {
                        Plugin.Log.LogWarning($"[HELLGATE SPAWN TEST] ✗ Failed to instantiate {enemyName}");
                    }
                }
                else
                {
                    Plugin.Log.LogWarning($"[HELLGATE SPAWN TEST] ✗ No prefab found for {enemyName}");
                }
            }

            Plugin.Log.LogInfo($"[HELLGATE SPAWN TEST] Successfully spawned {spawnedCount}/{TestEnemies.Length} test enemies (including 2 Touzoku)");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[HELLGATE SPAWN TEST] Error spawning enemies: {ex.Message}");
        }
    }

    /// <summary>
    /// Reset состояния spawn on respawn enemies
    /// </summary>
    internal static void Reset()
    {
        spawnCompleted = false;
        Plugin.Log.LogInfo("[HELLGATE SPAWN TEST] Spawn state reset - will spawn enemies again");
    }
}