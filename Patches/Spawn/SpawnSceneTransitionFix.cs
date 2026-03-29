using HarmonyLib;
using UnityEngine;
using System;
using System.Reflection;

namespace NoREroMod.Patches.Spawn;

/// <summary>
/// Фикwith "ghost spawns" и отсутствия spawn on переходах между локациями
/// 
/// ПРОБЛЕМА 1 (Ghost Spawns):
/// On переходе между локациями enemyи могут заспавниться in старой сцеnot with координатами новой локации,
/// that приводит к "телепортации" enemies и their появлению возле игрока after загрузки.
/// 
/// ПРОБЛЕМА 2 (Нет spawn on обычном переходе):
/// On переходе через SceneMove/SceneMoveMainEv/SceneMoveSubmit (двери/триггеры) enemyи not спавнятся,
/// пока not активируешь алтарь. Причина: game_fragmng._re_Scenename not обновляется on обычных переходах.
/// 
/// SOLUTION:
/// 1. Блокируем спавн во время загрузки сцены (isSceneTransitioning flag)
/// 2. Уничтожаем всех enemies before переходом
/// 3. Reset flagи spawn for всех локаций
/// 4. ✨ НОВОЕ: Устанавливаем game_fragmng._re_Scenename before переходом (as алтарь!)
/// 5. ✨ НОВОЕ: Устанавливаем StaticMng.Re_Scenename for синхронизации
/// 
/// БЕЗОПАСНОСТЬ:
/// - Can safely удалить эthe same файл if возникнут проблемы
/// - Не модифицирует существующие spawn классы
/// - Работает независимо from other систем
/// </summary>
internal static class SpawnSceneTransitionFix
{
    private static bool isSceneTransitioning = false;

    /// <summary>
    /// Проверяет, идет ли переход between scenes
    /// Используется in spawn классах for блокировки spawn во время перехода
    /// </summary>
    public static bool IsSceneTransitioning => isSceneTransitioning;

    /// <summary>
    /// Patch on SceneMove.SceneMOVE() - обычные переходы через двери/триггеры
    /// </summary>
    [HarmonyPatch(typeof(SceneMove), "SceneMOVE")]
    [HarmonyPrefix]
    private static void OnSceneMoveStart(SceneMove __instance)
    {
        try
        {
            Plugin.Log?.LogInfo("[SPAWN FIX] Scene transition started (SceneMove)");
            isSceneTransitioning = true;

            // ✨ НОВОЕ: Устанавливаем _re_Scenename ПЕРЕД переходом (as алтарь)
            SetSceneName(__instance, typeof(SceneMove));

            // Уничтожаем всех enemies before переходом (as алтарь)
            DestroyAllEnemies();

            // Reset flagи спавна
            ResetSpawnFlags();

            // Reset spawnCompleted for всех локаций
            ResetAllLocationSpawnFlags();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN FIX] Error in OnSceneMoveStart: {ex.Message}");
        }
    }

    /// <summary>
    /// Patch on SceneMoveMainEv.SceneMOVE() - переходы with проверкой event флагов
    /// </summary>
    [HarmonyPatch(typeof(SceneMoveMainEv), "SceneMOVE")]
    [HarmonyPrefix]
    private static void OnSceneMoveMainEvStart(SceneMoveMainEv __instance)
    {
        try
        {
            Plugin.Log?.LogInfo("[SPAWN FIX] Scene transition started (SceneMoveMainEv)");
            isSceneTransitioning = true;
            
            // ✨ НОВОЕ: Устанавливаем _re_Scenename ПЕРЕД переходом
            SetSceneName(__instance, typeof(SceneMoveMainEv));
            
            DestroyAllEnemies();
            ResetSpawnFlags();
            ResetAllLocationSpawnFlags();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN FIX] Error in OnSceneMoveMainEvStart: {ex.Message}");
        }
    }

    /// <summary>
    /// Patch on SceneMoveSubmit.SceneMOVE() - переходы через Submit (E/Enter)
    /// </summary>
    [HarmonyPatch(typeof(SceneMoveSubmit), "SceneMOVE")]
    [HarmonyPrefix]
    private static void OnSceneMoveSubmitStart(SceneMoveSubmit __instance)
    {
        try
        {
            Plugin.Log?.LogInfo("[SPAWN FIX] Scene transition started (SceneMoveSubmit)");
            isSceneTransitioning = true;
            
            // ✨ НОВОЕ: Устанавливаем _re_Scenename ПЕРЕД переходом
            SetSceneName(__instance, typeof(SceneMoveSubmit));
            
            DestroyAllEnemies();
            ResetSpawnFlags();
            ResetAllLocationSpawnFlags();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN FIX] Error in OnSceneMoveSubmitStart: {ex.Message}");
        }
    }

    /// <summary>
    /// Patch on SpawnParent.Initialize() - разблокируем спавн after полной инициализации
    /// </summary>
    [HarmonyPatch(typeof(SpawnParent), "Initialize")]
    [HarmonyPrefix]
    private static void BeforeSpawnInitialize()
    {
        try
        {
            // Reset flagи before инициализацией (on всякий случай)
            ResetAllLocationSpawnFlags();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN FIX] Error in BeforeSpawnInitialize: {ex.Message}");
        }
    }

    /// <summary>
    /// Patch on SpawnParent.Initialize() - разблокируем спавн after полной инициализации
    /// </summary>
    [HarmonyPatch(typeof(SpawnParent), "Initialize")]
    [HarmonyPostfix]
    private static void AfterSpawnInitialize()
    {
        try
        {
            Plugin.Log?.LogInfo("[SPAWN FIX] Scene transition completed, spawn unlocked");
            isSceneTransitioning = false; // Разблокируем спавн
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN FIX] Error in AfterSpawnInitialize: {ex.Message}");
        }
    }

    /// <summary>
    /// ✨ НОВАЯ ФУНКЦИЯ: Устанавливает _re_Scenename before переходом (as алтарь)
    /// This решает проблему отсутствия spawn on обычных переходах
    /// </summary>
    private static void SetSceneName(object sceneMoverInstance, Type sceneMoverType)
    {
        try
        {
            // Get целевую сцену via reflection (поле SceneName приватное)
            var sceneNameField = sceneMoverType.GetField("SceneName", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (sceneNameField == null)
            {
                Plugin.Log?.LogWarning($"[SPAWN FIX] SceneName field not found in {sceneMoverType.Name}");
                return;
            }

            string targetScene = sceneNameField.GetValue(sceneMoverInstance) as string;
            
            if (string.IsNullOrEmpty(targetScene))
            {
                Plugin.Log?.LogWarning("[SPAWN FIX] Target scene name is empty");
                return;
            }

            // Set _re_Scenename in game_fragmng (КАК АЛТАРЬ!)
            var fragMng = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetGameFragMng();
            if (fragMng != null)
            {
                fragMng._re_Scenename = targetScene; // ✅ Ключевой момент!
                Plugin.Log?.LogInfo($"[SPAWN FIX] Set game_fragmng._re_Scenename to: {targetScene}");
            }

            // Также обновляем StaticMng.Re_Scenename for синхронизации
            StaticMng.Re_Scenename = targetScene;
            Plugin.Log?.LogInfo($"[SPAWN FIX] Set StaticMng.Re_Scenename to: {targetScene}");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN FIX] Error in SetSceneName: {ex.Message}");
        }
    }

    /// <summary>
    /// Уничтожает всех enemies with тегом "Enemy" (as алтарь)
    /// </summary>
    private static void DestroyAllEnemies()
    {
        try
        {
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            int count = enemies.Length;
            
            foreach (GameObject enemy in enemies)
            {
                if (enemy != null)
                {
                    UnityEngine.Object.Destroy(enemy);
                }
            }

            if (count > 0)
            {
                Plugin.Log?.LogInfo($"[SPAWN FIX] Destroyed {count} enemies before scene transition");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN FIX] Error destroying enemies: {ex.Message}");
        }
    }

    /// <summary>
    /// Сбрасывает флаги SpawnParent (as алтарь)
    /// </summary>
    private static void ResetSpawnFlags()
    {
        try
        {
            var spawnParent = GameObject.FindWithTag("Gamemng")?.GetComponent<SpawnParent>();
            if (spawnParent != null)
            {
                spawnParent.fun_SpawnRE(); // Сбрасывает SpawnPoint[] = false
                Plugin.Log?.LogInfo("[SPAWN FIX] SpawnParent flags reset");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN FIX] Error resetting spawn flags: {ex.Message}");
        }
    }

    /// <summary>
    /// Сбрасывает spawnCompleted флаги for всех локаций
    /// Optimized: Uses UnifiedSpawnManager instead of reflection
    /// </summary>
    private static void ResetAllLocationSpawnFlags()
    {
        try
        {
            // Use новый оптимизированный менеджер
            NoREroMod.Systems.Spawn.UnifiedSpawnManager.ResetAllSpawnFlags();
            
            // Также сбрасываем старые spawn классы via reflection (for backward compatibility)
            ResetLegacySpawnClasses();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN FIX] Error in ResetAllLocationSpawnFlags: {ex.Message}");
        }
    }

    /// <summary>
    /// Сбрасывает флаги старых spawn классоin (for backward compatibility)
    /// </summary>
    private static void ResetLegacySpawnClasses()
    {
        try
        {
            Type[] spawnClasses = new Type[]
            {
                typeof(HellGateSpawn_FirstMap),
                typeof(HellGateSpawn_VillageMain),
                typeof(HellGateSpawn_ScapegoatEntrance),
                typeof(HellGateSpawn_UndergroundChurch),
                typeof(HellGateSpawn_InundergroundChurch),
                typeof(HellGateSpawn_InsomniaTownC),
                typeof(HellGateSpawn_ForestOfRequiem),
                typeof(HellGateSpawn_UndergroundLaboratory),
                typeof(HellGateSpawn_PilgrimageEntrance),
                typeof(HellGateSpawn_WhiteCathedral),
                typeof(HellGateSpawn_WhiteCathedralGarden),
                typeof(HellGateSpawn_WhiteCathedralRooftop)
            };

            int resetCount = 0;
            foreach (Type spawnClass in spawnClasses)
            {
                try
                {
                    var spawnCompletedField = spawnClass.GetField("spawnCompleted", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    
                    if (spawnCompletedField != null)
                    {
                        spawnCompletedField.SetValue(null, false);
                        resetCount++;
                    }

                    var lastSceneNameField = spawnClass.GetField("lastSceneName",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    
                    if (lastSceneNameField != null)
                    {
                        lastSceneNameField.SetValue(null, string.Empty);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"[SPAWN FIX] Error resetting {spawnClass.Name}: {ex.Message}");
                }
            }

            if (resetCount > 0)
            {
                Plugin.Log?.LogInfo($"[SPAWN FIX] Reset legacy spawn flags for {resetCount} classes");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SPAWN FIX] Error in ResetLegacySpawnClasses: {ex.Message}");
        }
    }
}

