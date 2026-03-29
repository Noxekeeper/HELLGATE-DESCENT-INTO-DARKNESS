using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Spine.Unity;
using NoREroMod.Patches.Enemy.Base;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Patch for InquisitionWhite - механику handoff of GG after 1 цикла
/// </summary>
class InquisitionWhitePassPatch : BaseEnemyPassPatch<InquisitionWhiteERO>
{
    protected override string EnemyName => "InquisitionWhite";

    protected override int CyclesBeforePass => 1;

    protected override string[] GetHAnimations()
    {
        return new[]
        {
            "START", "ERO_START", "ERO_START2", "ERO_START3",
            "ERO", "ERO2", "ERO3", "ERO4",
            "2ERO", "2ERO2", "2ERO3", "2ERO4",
            "FIN", "FIN2", "FIN3",
            "JIGO", "JIGO2"
        };
    }

    protected override bool IsCycleComplete(string animationName, string eventName, int seCount)
    {
        // Завершение цикла white inquisitor - event JIGO2
        if (eventName == "JIGO2")
        {
            return true;
        }
        // Fallback: возврат к ERO (начало следующits цикла)
        else if (animationName == "ERO" && eventName == "ERO")
        {
            return true;
        }

        return false;
    }

    protected override string GetEnemyTypeName()
    {
        return "inquisition_white";
    }

    internal static void ResetAll()
    {
        BaseEnemyPassPatch<InquisitionWhiteERO>.ResetAll();
    }

    [HarmonyPatch(typeof(InquisitionWhiteERO), "OnEvent")]
    [HarmonyPostfix]
    private static void InquisitionWhitePass(InquisitionWhiteERO __instance, Spine.Event e, int ___se_count)
    {
        var instance = new InquisitionWhitePassPatch();
        SetInstance(instance);

        try
        {
            // Check if отключен ли enemy
            var disabledField = typeof(BaseEnemyPassPatch<InquisitionWhiteERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);

            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                if (disabledDict != null && disabledDict.ContainsKey(__instance) && disabledDict[__instance])
                {
                    return;
                }
            }

            // Optimization: use cached playercon
            var player = UnifiedPlayerCacheManager.GetPlayer();
            if (player == null || !player.eroflag || player.erodown == 0)
            {
                return; // H-сцеon not активна
            }

            var spine = GetSpineAnimation(__instance);
            if (spine == null)
            {
                return;
            }

            string currentAnim = spine.AnimationName;

            // Check that this H-анимация
            if (!instance.IsHAnimation(currentAnim))
            {
                return; // Ignore combat animations
            }

            // Processing dialogue system (if необходимо)
            try
            {
                string eventName = e?.Data?.Name ?? e?.ToString() ?? string.Empty;
                NoREroMod.Systems.Dialogue.DialogueFramework.ProcessAnimationEvent(
                    __instance,
                    currentAnim,
                    eventName,
                    ___se_count
                );
            }
            catch (Exception ex)
            {
                // Игнорируем ошибки dialogue system
            }

            // Отслеживаем циклы и передачу
            instance.TrackCycles(__instance, spine, e, ___se_count);
        }
        catch (System.Exception ex)
        {
            // Log ошибки for диагностики
            Plugin.Log.LogError($"[InquisitionWhitePassPatch] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Force перевести animation к center for последующtheir enemies
    /// </summary>
    protected override void ForceAnimationToMiddle(SkeletonAnimation spine)
    {
        try
        {
            if (spine == null) return;

            // For white inquisitor start with ERO_START (короткая прелюдия)
            spine.AnimationState.ClearTracks();
            spine.AnimationState.AddAnimation(0, "ERO_START", false, 0f);
        }
        catch (System.Exception ex)
        {
            // Игнорируем ошибки animation
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
                return;
            }

            // Mark enemy as disabled
            var disabledField = typeof(BaseEnemyPassPatch<InquisitionWhiteERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);
            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                disabledDict[enemyInstance] = true;
            }

            // Stop H-animation enemy
            var enemyComponent = enemyInstance as InquisitionWhiteERO;
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
                        enemyMonoBehaviour.gameObject.SetActive(false);
                    }
                }
                catch (System.Exception ex)
                {
                    // Игнорируем ошибки
                }
            }

            // Clear animation ГГ
            var playerSpine = playerObject.GetComponentInChildren<SkeletonAnimation>();
            if (playerSpine != null)
            {
                try
                {
                    playerSpine.AnimationState.ClearTracks();
                }
                catch (System.Exception ex)
                {
                    // Игнорируем ошибки
                }
            }

            // Get playercon
            var playerComponent = playerObject.GetComponent<playercon>();
            if (playerComponent == null)
            {
                return;
            }

            // Clear eroflag
            var eroFlagField = typeof(playercon).GetField("eroflag", BindingFlags.Public | BindingFlags.Instance);
            if (eroFlagField != null)
            {
                eroFlagField.SetValue(playerComponent, false);
            }

            // Set GG animation to lying
            string[] downAnims = { "DOWN", "down", "Idle", "idle" };
            foreach (string animName in downAnims)
            {
                if (playerSpine != null)
                {
                    try
                    {
                        playerSpine.AnimationState.SetAnimation(0, animName, true);
                        break;
                    }
                    catch (System.Exception ex)
                    {
                        // Игнорируем ошибки
                    }
                }
            }

            // Set erodown
            var eroDownField = typeof(playercon).GetField("erodown", BindingFlags.Public | BindingFlags.Instance);
            if (eroDownField != null)
            {
                eroDownField.SetValue(playerComponent, 1);
            }

            // Reset SP
            var playerStatus = playerObject.GetComponent<PlayerStatus>();
            if (playerStatus != null)
            {
                playerStatus.Sp = 0f;
            }

            // Push ГГ from enemy
            var enemyTransform = (enemyInstance as MonoBehaviour)?.transform;
            if (enemyTransform != null)
            {
                Vector3 enemyPos = enemyTransform.position;
                Vector3 playerPos = playerComponent.transform.position;
                Vector3 direction = playerPos - enemyPos;
                direction.Normalize();

                // Fix: if enemy is left from ГГ, push right
                if (direction.x < 0)
                {
                    direction = Vector3.right;
                }
                else
                {
                    direction = Vector3.left;
                }

                float pushDistance = 2f;
                Vector3 newPosition = playerComponent.transform.position + (direction * pushDistance);
                playerComponent.transform.position = newPosition;

                // Reset vertical velocity
                var rigi2d = playerComponent.rigi2d;
                if (rigi2d != null)
                {
                    rigi2d.velocity = new Vector2(rigi2d.velocity.x, 0f);
                }
            }

            // Reset flag борьбы
            StruggleSystem.setStruggleLevel(-1);

            // Enable sprite renderer
            var spriteRenderer = playerObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
            }
        }
        catch (System.Exception ex)
        {
            // Игнорируем ошибки
        }
    }
}