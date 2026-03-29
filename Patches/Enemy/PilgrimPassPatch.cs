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
/// Patch for PilgrimERO - механику handoff of GG after полного цикла (on 2EROJIGO)
/// Первый pilgrim проходит всю последовательность, следующие стартуют рандомно
/// </summary>
class PilgrimPassPatch : BaseEnemyPassPatch<PilgrimERO>
{
    protected override string EnemyName => "Pilgrim";

    protected override int CyclesBeforePass => 1; // Передача after 1 полного цикла

    protected override string[] GetHAnimations()
    {
        return new[]
        {
            // Стартовые animation
            "START", "START2",
            // Путь A: Оральный секс
            "FERA", "FERA1", "FERA2", "FERAFIN", "FERAFIN2", "FERAFIN3", "FERAJIGO",
            // Путь B: Вагинальный секс
            "EROSTART1", "ERO", "ERO1", "ERO2", "ERO3", "EROFIN", "EROFIN2", "EROFIN3", "EROJIGO",
            // Путь C: Второй цикл
            "2ERO", "2ERO1", "2EROFIN", "2EROFIN2", "2EROJIGO"
        };
    }

    protected override bool IsCycleComplete(string animationName, string eventName, int seCount)
    {
        // Передача after completion 2EROJIGO (конец полного цикла)
        return eventName == "2EROJIGO";
    }

    protected override void ForceAnimationToMiddle(SkeletonAnimation spine)
    {
        // For следующtheir pilgrim рандомbut выбираем стартовую точку:
        // A: FERA (оральный), B: ERO (вагинальный), C: 2ERO (второй цикл)
        string[] startOptions = { "FERA", "ERO", "2ERO" };
        string selectedStart = startOptions[UnityEngine.Random.Range(0, startOptions.Length)];

        Plugin.Log?.LogDebug($"[PilgrimPassPatch] ForceAnimationToMiddle: Selected '{selectedStart}' for next pilgrim");
        spine.state.SetAnimation(0, selectedStart, true);
        spine.timeScale = 1f;

        // Дополнительная логика for разных стартовых точек
        switch (selectedStart)
        {
            case "FERA":
                // For орального секса
                break;
            case "ERO":
                // For вагинального секса
                break;
            case "2ERO":
                // For второго цикла
                break;
        }
    }

    protected override string GetEnemyTypeName()
    {
        return "pilgrim";
    }

    internal static void ResetAll()
    {
        BaseEnemyPassPatch<PilgrimERO>.ResetAll();
    }

    [HarmonyPatch(typeof(PilgrimERO), "OnEvent")]
    [HarmonyPostfix]
    private static void PilgrimPass(PilgrimERO __instance, Spine.Event e, int ___se_count)
    {
        var instance = new PilgrimPassPatch();
        SetInstance(instance);

        try
        {
            // Log call патча for debugging
            string eventStr = e?.ToString() ?? "NULL";
            Plugin.Log?.LogDebug($"[Pilgrim PATCH] Called: event={eventStr}, se_count={___se_count}");

            // Check if отключен ли enemy
            var disabledField = typeof(BaseEnemyPassPatch<PilgrimERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);

            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                if (disabledDict != null && disabledDict.ContainsKey(__instance) && disabledDict[__instance])
                {
                    Plugin.Log?.LogDebug($"[Pilgrim PATCH] Enemy disabled, skipping");
                    return;
                }
            }

            // Optimization: use cached playercon
            var player = UnifiedPlayerCacheManager.GetPlayer();
            if (player == null)
            {
                Plugin.Log?.LogDebug($"[Pilgrim PATCH] Player is null");
                return;
            }

            Plugin.Log?.LogDebug($"[Pilgrim PATCH] Player state: eroflag={player.eroflag}, erodown={player.erodown}");

            if (!player.eroflag || player.erodown == 0)
            {
                Plugin.Log?.LogDebug($"[Pilgrim PATCH] H-scene not active (eroflag={player.eroflag}, erodown={player.erodown})");
                return; // H-сцеon not активна
            }

            var spine = GetSpineAnimation(__instance);
            if (spine == null)
            {
                Plugin.Log?.LogDebug($"[Pilgrim PATCH] Spine is null");
                return;
            }

            string currentAnim = spine.AnimationName;
            string eventName = e.Data.Name;

            // Check if this is текущая анимация H-анимацией
            bool isHAnim = instance.IsHAnimation(currentAnim);
            Plugin.Log?.LogDebug($"[Pilgrim PATCH] Is H-animation '{currentAnim}': {isHAnim}");

            if (!isHAnim)
            {
                Plugin.Log?.LogDebug($"[Pilgrim PATCH] Not H-animation: '{currentAnim}'");
                return;
            }

            Plugin.Log?.LogDebug($"[Pilgrim PATCH] Processing: anim='{currentAnim}', event='{eventName}', se_count={___se_count}");

            // Вызываем базовую логику tracking cycles
            instance.TrackCycles(__instance, spine, e, ___se_count);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[PilgrimPassPatch] Error in OnEvent: {ex.Message}");
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
                UnityEngine.Debug.LogError("[PilgrimPassPatch] ExecuteHandoff: Player object not found!");
                return;
            }

            var player = playerObject.GetComponent<playercon>();
            if (player == null)
            {
                UnityEngine.Debug.LogError("[PilgrimPassPatch] ExecuteHandoff: Player component not found!");
                return;
            }

            // Mark enemy as disabled
            var disabledField = typeof(BaseEnemyPassPatch<PilgrimERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);
            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                disabledDict[enemyInstance] = true;
            }

            // Stop H-animation enemy
            var enemyComponent = enemyInstance as PilgrimERO;
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
                    UnityEngine.Debug.LogError($"[PilgrimPassPatch] Error stopping enemy animation: {ex.Message}");
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