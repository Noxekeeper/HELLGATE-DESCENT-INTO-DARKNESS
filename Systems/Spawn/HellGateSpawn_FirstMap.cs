using HarmonyLib;
using UnityEngine;
using System.IO;
using NoREroMod.Systems.Spawn;

namespace NoREroMod;

/// <summary>
/// Enemy spawn system for FirstMap location
/// Reads spawn configuration from HellGateSpawn_FirstMap.txt
/// Format: X,Y,EnemyType,Count,Description
/// Also supports: RANDOM,RANDOM_GROUP,POOL[] (see SpawnConfigExecutor)
/// </summary>
internal class HellGateSpawn_FirstMap
{
    private static HellGateSpawn_FirstMap instance;
    private static readonly string configPath = "BepInEx" + Path.DirectorySeparatorChar + "plugins" + Path.DirectorySeparatorChar +
                                                "HellGateJson" + Path.DirectorySeparatorChar + "HellGateSpawnPoint" + Path.DirectorySeparatorChar +
                                                "HellGateSpawn_FirstMap.txt";

    private static bool spawnCompleted = false;
    private static string lastSceneName = string.Empty;

    [HarmonyPatch(typeof(SpawnParent), "fun_SpawnRE")]
    [HarmonyPostfix]
    private static void OnSpawnReset()
    {
        spawnCompleted = false;
    }

    [HarmonyPatch(typeof(SpawnParent), "Initialize")]
    [HarmonyPostfix]
    private static void OnSpawnInitialize()
    {
        if (instance == null)
        {
            instance = new HellGateSpawn_FirstMap();
        }

        try
        {
            // Проверяем смену локации - сбрасываем флаг if локация изменилась
            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentSceneName != lastSceneName)
            {
                spawnCompleted = false;
                lastSceneName = currentSceneName;
            }

            if (NoREroMod.Patches.Spawn.SpawnSceneTransitionFix.IsSceneTransitioning || !IsInFirstMap() || spawnCompleted)
            {
                return;
            }

            EnemyPrefabRegistry.Initialize();
            SpawnAllEnemies();
            spawnCompleted = true;
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[HELLGATE SPAWN FM] Error in Initialize: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(Spawnenemy), "Update")]
    [HarmonyPostfix]
    private static void OnEnemySpawn(Spawnenemy __instance, GameObject ___enemy, int ___SpawnNumber, SpawnParent ___Spawnparent)
    {
        if (instance == null)
        {
            instance = new HellGateSpawn_FirstMap();
        }

        try
        {
            if (NoREroMod.Patches.Spawn.SpawnSceneTransitionFix.IsSceneTransitioning || !IsInFirstMap() || spawnCompleted)
            {
                return;
            }

            EnemyPrefabRegistry.Initialize();
            SpawnAllEnemies();
            spawnCompleted = true;
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[HELLGATE SPAWN FM] Error: {ex.Message}");
        }
    }

    private static bool IsInFirstMap()
    {
        try
        {
            var fragMng = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetGameFragMng();
            if (fragMng != null && !string.IsNullOrEmpty(fragMng._re_Scenename))
            {
                return fragMng._re_Scenename.Contains("FirstMap") ||
                       fragMng._re_Scenename.ToLower().Contains("first");
            }

            // Fallback on Unity SceneManager
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            return sceneName.Contains("FirstMap") || sceneName.ToLower().Contains("first");
        }
        catch
        {
            return false;
        }
    }

    private static void SpawnAllEnemies()
    {
        try
        {
            SpawnConfigExecutor.Execute(configPath, "[HELLGATE SPAWN FM]");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[HELLGATE SPAWN FM] Error: {ex.Message}");
        }
    }
}
