using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Unified optimized manager spawn for всех локаций
/// 
/// OPTIMIZATIONS:
/// 1. ✅ Single patch on Spawnenemy.Update instead of 5+ separate
/// 2. ✅ Caching game_fragmng (instead of FindGameObjectWithTag every frame)
/// 3. ✅ Early exit if спавн already completed
/// 4. ✅ Minimal checks in Update (only флаги)
/// 
/// SCALABILITY:
/// - Easy to add новые локации via RegisterLocation()
/// - Support for 12+ levels without потери производительности
/// - Automatic cache management
/// </summary>
internal static class UnifiedSpawnManager
{
    private static string lastKnownScene = string.Empty;
    
    private static Dictionary<string, LocationSpawnConfig> locationConfigs = new Dictionary<string, LocationSpawnConfig>();
    private static bool globalSpawnCompleted = false;

    /// <summary>
    /// Конфигурация spawn for single location
    /// </summary>
    private class LocationSpawnConfig
    {
        public string[] SceneIdentifiers { get; set; }
        public string ConfigPath { get; set; }
        public string LogPrefix { get; set; }
        public bool SpawnCompleted { get; set; }
    }

    /// <summary>
    /// System initialization - register all locations
    /// </summary>
    static UnifiedSpawnManager()
    {
        // Базовый путь: BepInEx/plugins/HellGateJson/HellGateSpawnPoint/
        string baseSpawnPath = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), "HellGateSpawnPoint");

        RegisterLocation(
            identifiers: new[] { "FirstMap", "first" },
            configPath: Path.Combine(baseSpawnPath, "HellGateSpawn_FirstMap.txt"),
            logPrefix: "[HELLGATE SPAWN FM]"
        );

        RegisterLocation(
            identifiers: new[] { "village_main", "village" },
            configPath: Path.Combine(baseSpawnPath, "HellGateSpawn_VillageMain.txt"),
            logPrefix: "[HELLGATE SPAWN VM]"
        );

        RegisterLocation(
            identifiers: new[] { "ScapegoatEntrance", "scapegoat" },
            configPath: Path.Combine(baseSpawnPath, "HellGateSpawn_ScapegoatEntrance.txt"),
            logPrefix: "[HELLGATE SPAWN SE]"
        );

        RegisterLocation(
            identifiers: new[] { "UndergroundChurch", "underground" },
            configPath: Path.Combine(baseSpawnPath, "HellGateSpawn_UndergroundChurch.txt"),
            logPrefix: "[HELLGATE SPAWN UC]"
        );

        RegisterLocation(
            identifiers: new[] { "InundergroundChurch", "Inunderground", "inunderground" },
            configPath: Path.Combine(baseSpawnPath, "HellGateSpawn__inunderground church.txt"),
            logPrefix: "[HELLGATE SPAWN IUC]"
        );

        RegisterLocation(
            identifiers: new[] { "InsomniaTownC", "nightless", "ightless" },
            configPath: Path.Combine(baseSpawnPath, "HellGateSpawn_nightless city C.txt"),
            logPrefix: "[HELLGATE SPAWN ITC]"
        );

        RegisterLocation(
            identifiers: new[] { "ForestOfRequiem", "hidden", "forest" },
            configPath: Path.Combine(baseSpawnPath, "HellGateSpawn_hidden Forest area.txt"),
            logPrefix: "[HELLGATE SPAWN FR]"
        );

        RegisterLocation(
            identifiers: new[] { "UndergroundLaboratory", "laboratory" },
            configPath: Path.Combine(baseSpawnPath, "HellGateSpawn_UndergroundLaboratory.txt"),
            logPrefix: "[HELLGATE SPAWN UL]"
        );

        RegisterLocation(
            identifiers: new[] { "PilgrimageEntrance", "pilgrimage" },
            configPath: Path.Combine(baseSpawnPath, "HellGateSpawn_PilgrimageEntrance.txt"),
            logPrefix: "[HELLGATE SPAWN PE]"
        );

        RegisterLocation(
            identifiers: new[] { "WhiteCathedral", "white", "cathedral" },
            configPath: Path.Combine(baseSpawnPath, "HellGateSpawn_WhiteCathedral.txt"),
            logPrefix: "[HELLGATE SPAWN WC]"
        );

        RegisterLocation(
            identifiers: new[] { "WhiteCathedralGarden", "garden" },
            configPath: Path.Combine(baseSpawnPath, "HellGateSpawn_WhiteCathedralGarden.txt"),
            logPrefix: "[HELLGATE SPAWN WCG]"
        );

        RegisterLocation(
            identifiers: new[] { "WhiteCathedralRooftop", "rooftop" },
            configPath: Path.Combine(baseSpawnPath, "HellGateSpawn_WhiteCathedralRooftop.txt"),
            logPrefix: "[HELLGATE SPAWN WCR]"
        );
    }

    /// <summary>
    /// Register new location (for future уровней)
    /// </summary>
    public static void RegisterLocation(string[] identifiers, string configPath, string logPrefix)
    {
        string key = identifiers[0].ToLower();
        locationConfigs[key] = new LocationSpawnConfig
        {
            SceneIdentifiers = identifiers,
            ConfigPath = configPath,
            LogPrefix = logPrefix,
            SpawnCompleted = false
        };
    }

    /// <summary>
    /// SINGLE PATCH on Spawnenemy.Update - instead of 5+ separate!
    /// </summary>
    [HarmonyPatch(typeof(Spawnenemy), "Update")]
    [HarmonyPostfix]
    private static void OnSpawnUpdate()
    {
        try
        {
            // Early exit if спавн already completed
            if (globalSpawnCompleted)
            {
                return;
            }

            // Проверяем блокировку from SpawnSceneTransitionFix
            if (NoREroMod.Patches.Spawn.SpawnSceneTransitionFix.IsSceneTransitioning)
            {
                return;
            }

            // Проверяем смену сцены
            string currentScene = GetCurrentSceneName();
            if (currentScene != lastKnownScene)
            {
                OnSceneChanged(currentScene);
            }

            // Try заспавнить for current location
            TrySpawnForCurrentLocation(currentScene);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[UNIFIED SPAWN] Error in OnSpawnUpdate: {ex.Message}");
        }
    }

    /// <summary>
    /// Patch on SpawnParent.fun_SpawnRE - reset on респавне
    /// </summary>
    [HarmonyPatch(typeof(SpawnParent), "fun_SpawnRE")]
    [HarmonyPostfix]
    private static void OnSpawnReset()
    {
        ResetAllSpawnFlags();
    }

    /// <summary>
    /// Patch on SpawnParent.Initialize - reset on инициализации
    /// </summary>
    [HarmonyPatch(typeof(SpawnParent), "Initialize")]
    [HarmonyPostfix]
    private static void OnSpawnInitialize()
    {
        try
        {
            string currentScene = GetCurrentSceneName();
            if (currentScene != lastKnownScene)
            {
                OnSceneChanged(currentScene);
            }

            TrySpawnForCurrentLocation(currentScene);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[UNIFIED SPAWN] Error in OnSpawnInitialize: {ex.Message}");
        }
    }

    /// <summary>
    /// Get name current scene (через UnifiedGameControllerCacheManager)
    /// </summary>
    private static string GetCurrentSceneName()
    {
        try
        {
            var fragMng = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetGameFragMng();
            if (fragMng != null && !string.IsNullOrEmpty(fragMng._re_Scenename))
            {
                return fragMng._re_Scenename;
            }

            // Fallback: Unity SceneManager
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Process scene change
    /// </summary>
    private static void OnSceneChanged(string newScene)
    {
        lastKnownScene = newScene;
        globalSpawnCompleted = false;
        
        // Reset flagи for всех локаций
        foreach (var config in locationConfigs.Values)
        {
            config.SpawnCompleted = false;
        }

        Plugin.Log?.LogInfo($"[UNIFIED SPAWN] Scene changed to: {newScene}");
    }

    /// <summary>
    /// Попытка spawn for current location
    /// </summary>
    private static void TrySpawnForCurrentLocation(string currentScene)
    {
        if (string.IsNullOrEmpty(currentScene))
        {
            return;
        }

        // Ищем подходящую конфигурацию локации
        foreach (var kvp in locationConfigs)
        {
            var config = kvp.Value;
            
            // Check if подходит ли текущая сцена
            bool isMatch = false;
            foreach (string identifier in config.SceneIdentifiers)
            {
                if (currentScene.Contains(identifier) || 
                    currentScene.ToLower().Contains(identifier.ToLower()))
                {
                    isMatch = true;
                    break;
                }
            }

            if (isMatch && !config.SpawnCompleted)
            {
                // Initialize префабы if нужно
                EnemyPrefabRegistry.Initialize();

                // Выполняем спавн
                SpawnConfigExecutor.Execute(config.ConfigPath, config.LogPrefix);
                
                config.SpawnCompleted = true;
                globalSpawnCompleted = true;
                
                Plugin.Log?.LogInfo($"{config.LogPrefix} Spawn completed for {currentScene}");
                break; // Спавним only for single location
            }
        }
    }

    /// <summary>
    /// Публичный метод for сброса всех flags (используется in SpawnSceneTransitionFix)
    /// </summary>
    public static void ResetAllSpawnFlags()
    {
        globalSpawnCompleted = false;
        
        foreach (var config in locationConfigs.Values)
        {
            config.SpawnCompleted = false;
        }

        Plugin.Log?.LogInfo("[UNIFIED SPAWN] All spawn flags reset");
    }

    /// <summary>
    /// Reset cache (for debugging or after critical ошибок)
    /// </summary>
    public static void ResetCache()
    {
        lastKnownScene = string.Empty;
        ResetAllSpawnFlags();
        
        Plugin.Log?.LogInfo("[UNIFIED SPAWN] Cache reset");
    }
}
