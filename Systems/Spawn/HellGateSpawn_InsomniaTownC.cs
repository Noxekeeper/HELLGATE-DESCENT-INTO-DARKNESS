using HarmonyLib;
using UnityEngine;
using System.IO;
using NoREroMod.Systems.Spawn;

namespace NoREroMod;

/// <summary>
/// Enemy spawn system for Nightless City C (InsomniaTownC)
/// Reads spawn configuration from HellGateSpawn_nightless city C.txt
/// Format: X,Y,EnemyType,Count or POOL[Type1,Type2,...]
/// Also supports: RANDOM,RANDOM_GROUP,POOL[] (see SpawnConfigExecutor)
/// </summary>
internal class HellGateSpawn_InsomniaTownC
{
    private static HellGateSpawn_InsomniaTownC instance;
    private static readonly string configPath = "BepInEx" + Path.DirectorySeparatorChar + "plugins" + Path.DirectorySeparatorChar +
                                                "HellGateJson" + Path.DirectorySeparatorChar + "HellGateSpawnPoint" + Path.DirectorySeparatorChar +
                                                "HellGateSpawn_nightless city C.txt";

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
            instance = new HellGateSpawn_InsomniaTownC();
        }

        try
        {
            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentSceneName != lastSceneName)
            {
                spawnCompleted = false;
                lastSceneName = currentSceneName;
            }

            if (NoREroMod.Patches.Spawn.SpawnSceneTransitionFix.IsSceneTransitioning || !IsInInsomniaTownC() || spawnCompleted)
            {
                return;
            }

            EnemyPrefabRegistry.Initialize();
            SpawnAllEnemies();
            spawnCompleted = true;
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[HELLGATE SPAWN ITC] Error in Initialize: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(Spawnenemy), "Update")]
    [HarmonyPostfix]
    private static void OnEnemySpawn(Spawnenemy __instance, GameObject ___enemy, int ___SpawnNumber, SpawnParent ___Spawnparent)
    {
        if (instance == null)
        {
            instance = new HellGateSpawn_InsomniaTownC();
        }

        try
        {
            if (NoREroMod.Patches.Spawn.SpawnSceneTransitionFix.IsSceneTransitioning || !IsInInsomniaTownC() || spawnCompleted)
            {
                return;
            }

            EnemyPrefabRegistry.Initialize();
            SpawnAllEnemies();
            spawnCompleted = true;
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[HELLGATE SPAWN ITC] Error: {ex.Message}");
        }
    }

    private static bool IsInInsomniaTownC()
    {
        try
        {
            var fragMng = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetGameFragMng();
            if (fragMng != null && !string.IsNullOrEmpty(fragMng._re_Scenename))
            {
                return fragMng._re_Scenename.Contains("InsomniaTownC") ||
                       fragMng._re_Scenename.ToLower().Contains("nightless") ||
                       fragMng._re_Scenename.ToLower().Contains("ightless");
            }

            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            return sceneName.Contains("InsomniaTownC") ||
                   sceneName.ToLower().Contains("nightless") ||
                   sceneName.ToLower().Contains("ightless");
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
            SpawnConfigExecutor.Execute(configPath, "[HELLGATE SPAWN ITC]");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[HELLGATE SPAWN ITC] Error: {ex.Message}");
        }
    }
}
