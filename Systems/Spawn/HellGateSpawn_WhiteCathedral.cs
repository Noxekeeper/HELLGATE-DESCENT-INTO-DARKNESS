using HarmonyLib;
using UnityEngine;
using System.IO;
using NoREroMod.Systems.Spawn;

namespace NoREroMod;

/// <summary>
/// Enemy spawn system for White Cathedral
/// Reads spawn configuration from HellGateSpawn_WhiteCathedral.txt
/// Format: RANDOM,chance,X,Y,POOL[Type1,Type2,...],1
/// </summary>
internal class HellGateSpawn_WhiteCathedral
{
    private static HellGateSpawn_WhiteCathedral instance;
    private static readonly string configPath = "BepInEx" + Path.DirectorySeparatorChar + "plugins" + Path.DirectorySeparatorChar +
                                                "HellGateJson" + Path.DirectorySeparatorChar + "HellGateSpawnPoint" + Path.DirectorySeparatorChar +
                                                "HellGateSpawn_WhiteCathedral.txt";

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
            instance = new HellGateSpawn_WhiteCathedral();
        }

        try
        {
            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentSceneName != lastSceneName)
            {
                spawnCompleted = false;
                lastSceneName = currentSceneName;
            }

            if (NoREroMod.Patches.Spawn.SpawnSceneTransitionFix.IsSceneTransitioning || !IsInWhiteCathedral() || spawnCompleted)
            {
                return;
            }

            EnemyPrefabRegistry.Initialize();
            SpawnAllEnemies();
            spawnCompleted = true;
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[HELLGATE SPAWN WC] Error in Initialize: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(Spawnenemy), "Update")]
    [HarmonyPostfix]
    private static void OnEnemySpawn(Spawnenemy __instance, GameObject ___enemy, int ___SpawnNumber, SpawnParent ___Spawnparent)
    {
        if (instance == null)
        {
            instance = new HellGateSpawn_WhiteCathedral();
        }

        try
        {
            if (NoREroMod.Patches.Spawn.SpawnSceneTransitionFix.IsSceneTransitioning || !IsInWhiteCathedral() || spawnCompleted)
            {
                return;
            }

            EnemyPrefabRegistry.Initialize();
            SpawnAllEnemies();
            spawnCompleted = true;
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[HELLGATE SPAWN WC] Error: {ex.Message}");
        }
    }

    private static bool IsInWhiteCathedral()
    {
        try
        {
            var fragMng = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetGameFragMng();
            string sceneName = fragMng != null && !string.IsNullOrEmpty(fragMng._re_Scenename)
                ? fragMng._re_Scenename
                : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            // Exclude Rooftop and Garden — they have their own spawn classes
            if (sceneName.Contains("Rooftop") || sceneName.Contains("Garden"))
                return false;
            return sceneName.Contains("WhiteCathedral") ||
                   sceneName.ToLower().Contains("white") ||
                   sceneName.ToLower().Contains("cathedral");
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
            SpawnConfigExecutor.Execute(configPath, "[HELLGATE SPAWN WC]");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[HELLGATE SPAWN WC] Error: {ex.Message}");
        }
    }
}
