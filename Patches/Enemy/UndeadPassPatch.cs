using HarmonyLib;
using Spine;
using Spine.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using NoREroMod;
using NoREroMod.Patches.Enemy.Base;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Patch for UndeadERO - механику handoff of GG after полного цикла (on 3ERO_JIGO)
/// Первый Undead проходит всю последовательность, следующие стартуют рандомно
/// Спавнит дополнительных Undead on определенных этапах
/// </summary>
class UndeadPassPatch : BaseEnemyPassPatch<UndeadERO>
{
    protected override string EnemyName => "Undead";

    protected override int CyclesBeforePass => 1; // Передача after 1 полного цикла

    protected override string[] GetHAnimations()
    {
        return new[]
        {
            // Стартовые animation
            "START",
            // Первый цикл
            "1ERO", "1ERO2", "1ERO3", "1EROFIN", "1EROFIN2",
            // Второй цикл
            "2ERO_START", "2ERO1", "2ERO1_2", "2ERO2", "2ERO3", "2ERO4",
            // Третий цикл
            "3ERO", "3ERO_2", "3ERO2", "3ERO3", "3ERO_FIN", "3ERO_FIN2",
            // JIGO animation
            "3ERO_JIGO", "3ERO_JIGO2"
        };
    }

    protected override bool IsCycleComplete(string animationName, string eventName, int seCount)
    {
        // Передача on событии 3ERO_JIGO (transition to завершающей JIGO animation)
        return eventName == "3ERO_JIGO";
    }

    protected override void ForceAnimationToMiddle(SkeletonAnimation spine)
    {
        // For следующtheir Undead рандомbut выбираем стартовую точку from разных циклов
        string[] startOptions = {
            "2ERO_START",  // Начало второго цикла
            "1ERO",        // Начало первого цикла
            "2ERO1",       // Середиon второго цикла
            "3ERO3",       // Середиon третьits цикла
            "1ERO2"        // Середиon первого цикла
        };

        string selectedStart = startOptions[UnityEngine.Random.Range(0, startOptions.Length)];

        Plugin.Log?.LogDebug($"[UndeadPassPatch] ForceAnimationToMiddle: Selected '{selectedStart}' for next undead");
        spine.state.SetAnimation(0, selectedStart, true);
        spine.timeScale = 1f;
    }

    protected override string GetEnemyTypeName()
    {
        return "undead";
    }

    internal static void ResetAll()
    {
        BaseEnemyPassPatch<UndeadERO>.ResetAll();
    }

    // Спавн дополнительных Undead on определенных этапах
    [HarmonyPatch(typeof(UndeadERO), "OnEvent")]
    [HarmonyPostfix]
    private static void SpawnAdditionalUndead(UndeadERO __instance, Spine.Event e, int ___se_count)
    {
        try
        {
            string eventName = e.Data.Name;
            var spine = GetSpineAnimation(__instance);
            if (spine == null) return;
            string animationName = spine.AnimationName;

            // Спавним дополнительного Undead on определенных этапах
            if (ShouldSpawnAdditionalUndead(eventName, animationName))
            {
                SpawnUndeadNearPlayer(__instance);
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[UndeadPassPatch] Error in SpawnAdditionalUndead: {ex.Message}");
        }
    }

    private static bool ShouldSpawnAdditionalUndead(string eventName, string animationName)
    {
        // Спавним on переходах между циклами
        return eventName == "2ERO_START" ||  // Transition toо второму циклу
               eventName == "3ERO" ||        // Transition to третьему циклу
               eventName == "3ERO_JIGO";     // Transition to завершению
    }

    private static void SpawnUndeadNearPlayer(UndeadERO currentUndead)
    {
        try
        {
            // Get префаб Undead
            GameObject undeadPrefab = NoREroMod.Systems.Spawn.EnemyPrefabRegistry.GetPrefab("Undead");
            if (undeadPrefab == null)
            {
                UnityEngine.Debug.LogError("[UndeadPassPatch] Undead prefab not found!");
                return;
            }

            // Optimization: use cached playercon
            GameObject playerObj = UnifiedPlayerCacheManager.GetPlayerObject();
            if (playerObj == null) return;

            Vector2 playerPos = playerObj.transform.position;

            // Спавним Undead at distance 3 Unity единиц слева or справа
            float spawnDistance = 3f;
            bool spawnLeft = UnityEngine.Random.Range(0, 2) == 0;

            Vector2 spawnPos = spawnLeft
                ? new Vector2(playerPos.x - spawnDistance, playerPos.y)
                : new Vector2(playerPos.x + spawnDistance, playerPos.y);

            // Создаем Undead
            GameObject newUndead = UnityEngine.Object.Instantiate(undeadPrefab, spawnPos, Quaternion.identity);
            if (newUndead != null)
            {
                newUndead.SetActive(true);
                Plugin.Log?.LogDebug($"[UndeadPassPatch] Spawned additional Undead at ({spawnPos.x:F2}, {spawnPos.y:F2})");
                // Спавненный Undead начнёт with force mid через shared EnemyHandoffSystem.GlobalHandoffCount
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[UndeadPassPatch] Error spawning additional Undead: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(UndeadERO), "OnEvent")]
    [HarmonyPostfix]
    private static void UndeadPass(UndeadERO __instance, Spine.Event e, int ___se_count)
    {
        var instance = new UndeadPassPatch();
        SetInstance(instance);

        try
        {
            // Log call патча for debugging
            string eventStr = e?.ToString() ?? "NULL";
            Plugin.Log?.LogDebug($"[Undead PATCH] Called: event={eventStr}, se_count={___se_count}");

            // Check if отключен ли enemy
            var disabledField = typeof(BaseEnemyPassPatch<UndeadERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);

            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                if (disabledDict != null && disabledDict.ContainsKey(__instance) && disabledDict[__instance])
                {
                    Plugin.Log?.LogDebug($"[Undead PATCH] Enemy disabled, skipping");
                    return;
                }
            }

            // Optimization: use cached playercon
            var player = UnifiedPlayerCacheManager.GetPlayer();
            if (player == null)
            {
                Plugin.Log?.LogDebug($"[Undead PATCH] Player is null");
                return;
            }

            Plugin.Log?.LogDebug($"[Undead PATCH] Player state: eroflag={player.eroflag}, erodown={player.erodown}");

            if (!player.eroflag || player.erodown == 0)
            {
                Plugin.Log?.LogDebug($"[Undead PATCH] H-scene not active (eroflag={player.eroflag}, erodown={player.erodown})");
                return; // H-сцеon not активна
            }

            var spine = GetSpineAnimation(__instance);
            if (spine == null)
            {
                Plugin.Log?.LogDebug($"[Undead PATCH] Spine is null");
                return;
            }

            string currentAnim = spine.AnimationName;
            string eventName = e.Data.Name;

            // Check if this is текущая анимация H-анимацией
            bool isHAnim = instance.IsHAnimation(currentAnim);
            Plugin.Log?.LogDebug($"[Undead PATCH] Is H-animation '{currentAnim}': {isHAnim}");

            if (!isHAnim)
            {
                Plugin.Log?.LogDebug($"[Undead PATCH] Not H-animation: '{currentAnim}'");
                return;
            }

            Plugin.Log?.LogDebug($"[Undead PATCH] Processing: anim='{currentAnim}', event='{eventName}', se_count={___se_count}");

            // Check if this is event cycle completion
            bool isCycleComplete = instance.IsCycleComplete(currentAnim, eventName, ___se_count);
            if (isCycleComplete)
            {
                Plugin.Log?.LogDebug($"[Undead PATCH] CYCLE COMPLETE DETECTED! (anim='{currentAnim}', event='{eventName}')");
            }

            // Вызываем базовую логику tracking cycles
            instance.TrackCycles(__instance, spine, e, ___se_count);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[UndeadPassPatch] Error in OnEvent: {ex.Message}");
        }
    }

    /// <summary>
    /// Public method for invoking handoff (used by DelayedHandoffScript)
    /// </summary>
    public static void ExecuteHandoff(object enemyInstance)
    {
        try
        {
            // Optimization: use cached playercon
            GameObject playerObject = UnifiedPlayerCacheManager.GetPlayerObject();
            if (playerObject == null)
            {
                UnityEngine.Debug.LogError("[UndeadPassPatch] ExecuteHandoff: Player object not found!");
                return;
            }

            var player = playerObject.GetComponent<playercon>();
            if (player == null)
            {
                UnityEngine.Debug.LogError("[UndeadPassPatch] ExecuteHandoff: Player component not found!");
                return;
            }

            // Mark enemy as disabled
            var disabledField = typeof(BaseEnemyPassPatch<UndeadERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);
            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                disabledDict[enemyInstance] = true;
            }

            // Stop H-animation enemy
            var enemyComponent = enemyInstance as UndeadERO;
            if (enemyComponent != null)
            {
                try
                {
                    var enemySpine = GetSpineAnimation(enemyComponent);
                    if (enemySpine != null)
                    {
                        enemySpine.AnimationState.ClearTracks();

                        // Try разные варианты idle animations
                        string[] idleAnimations = { "idle", "Idle", "IDLE", "wait", "Wait", "WAIT" };
                        foreach (string animName in idleAnimations)
                        {
                            try
                            {
                                enemySpine.AnimationState.SetAnimation(0, animName, true);
                                break;
                            }
                            catch
                            {
                                // Try next animation
                            }
                        }
                    }

                    // Make enemy неvisibleым
                    var enemyMonoBehaviour = enemyComponent as MonoBehaviour;
                    if (enemyMonoBehaviour != null)
                    {
                        var meshRenderer = enemyMonoBehaviour.GetComponent<MeshRenderer>();
                        if (meshRenderer != null)
                        {
                            meshRenderer.enabled = false;
                        }

                        var spriteRenderer = enemyMonoBehaviour.GetComponent<SpriteRenderer>();
                        if (spriteRenderer != null)
                        {
                            spriteRenderer.enabled = false;
                        }

                        // Деактивируем объект enemy
                        enemyMonoBehaviour.gameObject.SetActive(false);
                    }
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[UndeadPassPatch] Error stopping enemy animation: {ex.Message}");
                }
            }

            // Отключаем eroflag so that прервать текущую H-сцену
            player.eroflag = false;

            // Enable sprite renderer игрока
            var playerSpriteRenderer = playerObject.GetComponent<SpriteRenderer>();
            if (playerSpriteRenderer != null)
            {
                playerSpriteRenderer.enabled = true;
            }
        }
        catch (System.Exception ex)
        {
            // Игнорируем ошибки
        }
    }
}