using HarmonyLib;
using UnityEngine;
using System.IO;
using NoREroMod.Systems.Spawn;

namespace NoREroMod;

/// <summary>
/// Enemy spawn system for ScapegoatEntrance location
/// Reads spawn configuration from HellGateSpawn_ScapegoatEntrance.txt
/// Format: X,Y,EnemyType,Count,Description
/// Also supports: RANDOM,RANDOM_GROUP,POOL[] (see SpawnConfigExecutor)
/// </summary>
internal class HellGateSpawn_ScapegoatEntrance
{
    private static HellGateSpawn_ScapegoatEntrance instance;
    private static readonly string configPath = "BepInEx" + Path.DirectorySeparatorChar + "plugins" + Path.DirectorySeparatorChar +
                                                "HellGateJson" + Path.DirectorySeparatorChar + "HellGateSpawnPoint" + Path.DirectorySeparatorChar +
                                                "HellGateSpawn_ScapegoatEntrance.txt";

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
            instance = new HellGateSpawn_ScapegoatEntrance();
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

            if (NoREroMod.Patches.Spawn.SpawnSceneTransitionFix.IsSceneTransitioning || !IsInScapegoatEntrance() || spawnCompleted)
            {
                return;
            }

            // Start coroutine with задержкой for spawn after полной загрузки локации
            var gameController = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetGameController();
            if (gameController != null)
            {
                var coroutineRunner = gameController.GetComponent<SpawnCoroutineRunner>();
                if (coroutineRunner == null)
                {
                    coroutineRunner = gameController.AddComponent<SpawnCoroutineRunner>();
                }
                coroutineRunner.StartCoroutine(DelayedSpawn());
            }
            else
            {
                // Fallback: спавн without delay
                EnemyPrefabRegistry.Initialize();
                SpawnAllEnemies();
                spawnCompleted = true;
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[HELLGATE SPAWN SE] Error in Initialize: {ex.Message}");
        }
    }

    private static System.Collections.IEnumerator DelayedSpawn()
    {
        // Wait 1 second for полной загрузки локации
        yield return new WaitForSeconds(1f);
        
        if (NoREroMod.Patches.Spawn.SpawnSceneTransitionFix.IsSceneTransitioning || spawnCompleted || !IsInScapegoatEntrance())
        {
            yield break;
        }

        try
        {
            EnemyPrefabRegistry.Initialize();
            SpawnAllEnemies();
            spawnCompleted = true;
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[HELLGATE SPAWN SE] Error in DelayedSpawn: {ex.Message}");
        }
    }

    // Helper class for запуска корутин
    private class SpawnCoroutineRunner : MonoBehaviour { }

    [HarmonyPatch(typeof(Spawnenemy), "Update")]
    [HarmonyPostfix]
    private static void OnEnemySpawn(Spawnenemy __instance, GameObject ___enemy, int ___SpawnNumber, SpawnParent ___Spawnparent)
    {
        if (instance == null)
        {
            instance = new HellGateSpawn_ScapegoatEntrance();
        }

        try
        {
            if (NoREroMod.Patches.Spawn.SpawnSceneTransitionFix.IsSceneTransitioning || !IsInScapegoatEntrance() || spawnCompleted)
            {
                return;
            }

            EnemyPrefabRegistry.Initialize();
            SpawnAllEnemies();
            spawnCompleted = true;
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[HELLGATE SPAWN SE] Error: {ex.Message}");
        }
    }

    private static bool IsInScapegoatEntrance()
    {
        try
        {
            var fragMng = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetGameFragMng();
            if (fragMng != null && !string.IsNullOrEmpty(fragMng._re_Scenename))
            {
                return fragMng._re_Scenename.Contains("scapegoatEntrance") ||
                       fragMng._re_Scenename.ToLower().Contains("scapegoat");
            }

            // Fallback on Unity SceneManager
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            return sceneName.Contains("scapegoatEntrance") || sceneName.ToLower().Contains("scapegoat");
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
            SpawnConfigExecutor.Execute(configPath, null); // SE: no log spam
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[HELLGATE SPAWN SE] Error: {ex.Message}");
        }
    }
}
