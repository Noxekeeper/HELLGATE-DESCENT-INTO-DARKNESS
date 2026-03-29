using HarmonyLib;
using UnityEngine;
using System.IO;
using NoREroMod.Systems.Spawn;

namespace NoREroMod;

/// <summary>
/// Enemy spawn system for InundergroundChurch location
/// Reads spawn configuration from HellGateSpawn__inunderground church.txt
/// Format: X,Y,EnemyType,Count,Description
/// Also supports: RANDOM,RANDOM_GROUP,POOL[] (see SpawnConfigExecutor)
/// </summary>
internal class HellGateSpawn_InundergroundChurch
{
    private static HellGateSpawn_InundergroundChurch instance;
    private static readonly string configPath = "BepInEx" + Path.DirectorySeparatorChar + "plugins" + Path.DirectorySeparatorChar +
                                                "HellGateJson" + Path.DirectorySeparatorChar + "HellGateSpawnPoint" + Path.DirectorySeparatorChar +
                                                "HellGateSpawn__inunderground church.txt";

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
            instance = new HellGateSpawn_InundergroundChurch();
        }

        try
        {
            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentSceneName != lastSceneName)
            {
                spawnCompleted = false;
                lastSceneName = currentSceneName;
            }

            if (NoREroMod.Patches.Spawn.SpawnSceneTransitionFix.IsSceneTransitioning || !IsInInundergroundChurch() || spawnCompleted)
            {
                return;
            }

            EnemyPrefabRegistry.Initialize();
            SpawnAllEnemies();
            spawnCompleted = true;
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[HELLGATE SPAWN IUC] Error in Initialize: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(Spawnenemy), "Update")]
    [HarmonyPostfix]
    private static void OnEnemySpawn(Spawnenemy __instance, GameObject ___enemy, int ___SpawnNumber, SpawnParent ___Spawnparent)
    {
        if (instance == null)
        {
            instance = new HellGateSpawn_InundergroundChurch();
        }

        try
        {
            if (NoREroMod.Patches.Spawn.SpawnSceneTransitionFix.IsSceneTransitioning || !IsInInundergroundChurch() || spawnCompleted)
            {
                return;
            }

            EnemyPrefabRegistry.Initialize();
            SpawnAllEnemies();
            spawnCompleted = true;
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[HELLGATE SPAWN IUC] Error: {ex.Message}");
        }
    }

    private static bool IsInInundergroundChurch()
    {
        try
        {
            var fragMng = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetGameFragMng();
            if (fragMng != null && !string.IsNullOrEmpty(fragMng._re_Scenename))
            {
                return fragMng._re_Scenename.Contains("InundergroundChurch") ||
                       fragMng._re_Scenename.Contains("Inunderground") ||
                       fragMng._re_Scenename.ToLower().Contains("inunderground");
            }

            // Fallback on Unity SceneManager
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            return sceneName.Contains("InundergroundChurch") || 
                   sceneName.Contains("Inunderground") ||
                   sceneName.ToLower().Contains("inunderground");
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
            SpawnConfigExecutor.Execute(configPath, "[HELLGATE SPAWN IUC]");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[HELLGATE SPAWN IUC] Error: {ex.Message}");
        }
    }
}
