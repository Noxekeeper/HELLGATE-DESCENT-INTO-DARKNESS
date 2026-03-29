using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Spine.Unity;
using NoREroMod;
using NoREroMod.Patches.Enemy.Base;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Patch for MummyManERO - механику handoff of GG after полного цикла (on JIGO2)
/// Первый MummyMan проходит всю последовательность, следующие стартуют with JIGO2
/// </summary>
class MummyManPassPatch : BaseEnemyPassPatch<MummyManERO>
{
    protected override string EnemyName => "MummyMan";

    protected override int CyclesBeforePass => 1; // Передача after 1 полного цикла

    protected override string[] GetHAnimations()
    {
        return new[]
        {
            // Стартовые animation
            "EROSTART100", "START", "START2", "START3", "START4", "START5",
            "START6", "START7", "START8", "START9", "START10", "START11",
            // Основной цикл
            "ERO", "ERO1", "ERO2", "ERO3", "EROFIN", "EROFIN2", "EROFIN3", "JIGO", "JIGO2",
            // Второй цикл
            "2ERO", "2ERO1", "2ERO2", "2ERO3", "FIN", "FIN2", "FIN3"
        };
    }

    protected override bool IsCycleComplete(string animationName, string eventName, int seCount)
    {
        // Передача on событии JIGO (запуск animation JIGO)
        return eventName == "JIGO";
    }

    protected override void ForceAnimationToMiddle(SkeletonAnimation spine)
    {
        // For следующtheir MummyMan стартуем with JIGO2
        spine.state.SetAnimation(0, "JIGO2", false);
        spine.timeScale = 1f;
    }

    protected override string GetEnemyTypeName()
    {
        return "mummy_man";
    }

    internal static void ResetAll()
    {
        BaseEnemyPassPatch<MummyManERO>.ResetAll();
    }

    [HarmonyPatch(typeof(MummyManERO), "OnEvent")]
    [HarmonyPostfix]
    private static void MummyManPass(MummyManERO __instance, Spine.Event e, int ___se_count)
    {
        var instance = new MummyManPassPatch();
        SetInstance(instance);

        try
        {
            // Log call патча for debugging
            string eventStr = e?.ToString() ?? "NULL";
            Plugin.Log?.LogDebug($"[MummyMan PATCH] Called: event={eventStr}, se_count={___se_count}");

            // Check if отключен ли enemy
            var disabledField = typeof(BaseEnemyPassPatch<MummyManERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);

            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                if (disabledDict != null && disabledDict.ContainsKey(__instance) && disabledDict[__instance])
                {
                    Plugin.Log?.LogDebug($"[MummyMan PATCH] Enemy disabled, skipping");
                    return;
                }
            }

            // Optimization: use cached playercon
            var player = UnifiedPlayerCacheManager.GetPlayer();
            if (player == null)
            {
                Plugin.Log?.LogDebug($"[MummyMan PATCH] Player is null");
                return;
            }

            Plugin.Log?.LogDebug($"[MummyMan PATCH] Player state: eroflag={player.eroflag}, erodown={player.erodown}");

            if (!player.eroflag || player.erodown == 0)
            {
                Plugin.Log?.LogDebug($"[MummyMan PATCH] H-scene not active (eroflag={player.eroflag}, erodown={player.erodown})");
                return; // H-сцеon not активна
            }

            var spine = GetSpineAnimation(__instance);
            if (spine == null)
            {
                Plugin.Log?.LogDebug($"[MummyMan PATCH] Spine is null");
                return;
            }

            string currentAnim = spine.AnimationName;
            string eventName = e.Data.Name;

            // Check if this is текущая анимация H-анимацией
            bool isHAnim = instance.IsHAnimation(currentAnim);
            Plugin.Log?.LogDebug($"[MummyMan PATCH] Is H-animation '{currentAnim}': {isHAnim}");

            if (!isHAnim)
            {
                Plugin.Log?.LogDebug($"[MummyMan PATCH] Not H-animation: '{currentAnim}'");
                return;
            }

            Plugin.Log?.LogDebug($"[MummyMan PATCH] Processing: anim='{currentAnim}', event='{eventName}', se_count={___se_count}");

            // Check if this is event cycle completion
            bool isCycleComplete = instance.IsCycleComplete(currentAnim, eventName, ___se_count);
            if (isCycleComplete)
            {
                Plugin.Log?.LogDebug($"[MummyMan PATCH] CYCLE COMPLETE DETECTED! (anim='{currentAnim}', event='{eventName}')");
            }

            // Вызываем базовую логику tracking cycles
            instance.TrackCycles(__instance, spine, e, ___se_count);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[MummyManPassPatch] Error in OnEvent: {ex.Message}");
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
                UnityEngine.Debug.LogError("[MummyManPassPatch] ExecuteHandoff: Player object not found!");
                return;
            }

            var player = playerObject.GetComponent<playercon>();
            if (player == null)
            {
                UnityEngine.Debug.LogError("[MummyManPassPatch] ExecuteHandoff: Player component not found!");
                return;
            }

            // Mark enemy as disabled
            var disabledField = typeof(BaseEnemyPassPatch<MummyManERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);
            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                disabledDict[enemyInstance] = true;
            }

            // Stop H-animation enemy
            var enemyComponent = enemyInstance as MummyManERO;
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
                    UnityEngine.Debug.LogError($"[MummyManPassPatch] Error stopping enemy animation: {ex.Message}");
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