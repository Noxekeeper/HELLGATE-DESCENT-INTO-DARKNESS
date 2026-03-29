using System;
using System.Linq;
using System.Collections;
using UnityEngine;
using HarmonyLib;
using NoREroMod;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Handoff logic ГГ for BigoniBrother
/// </summary>
class BigoniBrotherPassLogic
{
    // Dictionary for tracking disabled BigoniBrother enemies (analogичbut TouzokuNormalPassPatch)
    private static System.Collections.Generic.Dictionary<object, bool> enemyDisabled = new System.Collections.Generic.Dictionary<object, bool>();
    /// <summary>
    /// Reset состояния гангбанг цикла on вырывании from BigoniBrother
    /// Поскольку BigoniBrother может быть частью цепочки with goblinми,
    /// сбрасываем state всех enemies
    /// </summary>
    internal static void ResetAll()
    {
        try
        {
            // Plugin.Log.LogInfo("[BIGONI BROTHER] === RESET GANGBANG STATE ===");
            
            // Clear dictionary disabled enemies (они already not disabled after сброса)
            enemyDisabled.Clear();
            
            // Reset guard JIGO3 (предотвращает дублирование эро-анимаций goblin)
            BigoniBrotherPatch.ClearJigo3HandoffState();
            
            // Reset state всех enemies (for случаеin цепочки гангбанг)
            // TouzokuNormalPassPatch.ResetAll();
            // GoblinPassPatch.ResetAll();
            
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[BIGONI BROTHER] Error during ResetAll: {ex.Message}");
        }
    }

    /// <summary>
    /// Отключение BigoniBrother enemy
    /// </summary>
    /// <param name="enemyInstance">Экземпляр BigoniBrother enemy</param>
    internal static void DisableEnemy(object enemyInstance)
    {
        try
        {
            if (enemyInstance == null)
            {
                Plugin.Log.LogWarning("[BIGONI BROTHER] DisableEnemy called with null enemyInstance");
                return;
            }

            // Отмечаем enemy as disabled
            enemyDisabled[enemyInstance] = true;
            
            Plugin.Log.LogInfo($"[BIGONI BROTHER] Enemy {enemyInstance.GetType().Name} disabled");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[BIGONI BROTHER] Error in DisableEnemy: {ex.Message}");
        }
    }

    /// <summary>
    /// Проверка, отключен ли BigoniBrother enemy
    /// </summary>
    /// <param name="enemyInstance">Экземпляр BigoniBrother enemy</param>
    /// <returns>true if enemy отключен</returns>
    internal static bool IsEnemyDisabled(object enemyInstance)
    {
        try
        {
            if (enemyInstance == null)
                return false;
                
            return enemyDisabled.ContainsKey(enemyInstance) && enemyDisabled[enemyInstance];
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[BIGONI BROTHER] Error in IsEnemyDisabled: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Public method for invoking handoff (used by DelayedHandoffScript)
    /// </summary>
    public static void ExecuteHandoff(object enemyInstance)
    {
        EnemyHandoffSystem.GlobalHandoffCount++;
        PushPlayerAwayFromEnemy(enemyInstance);
    }

    /// <summary>
    /// Удаление BigoniBrother enemy from списка отключенных
    /// (on смерти or окончании H-scene)
    /// </summary>
    /// <param name="enemyInstance">Экземпляр BigoniBrother enemy</param>
    internal static void RemoveDisabledEnemy(object enemyInstance)
    {
        try
        {
            if (enemyInstance != null && enemyDisabled.ContainsKey(enemyInstance))
            {
                enemyDisabled.Remove(enemyInstance);
                Plugin.Log.LogInfo($"[BIGONI BROTHER] Removed disabled enemy {enemyInstance.GetType().Name}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[BIGONI BROTHER] Error in RemoveDisabledEnemy: {ex.Message}");
        }
    }

    /// <summary>
    /// Оттолкнуть ГГ from enemy и скрыть BigoniBrother (analogичbut GoblinPassLogic)
    /// </summary>
    private static void PushPlayerAwayFromEnemy(object enemyInstance)
    {
        // Plugin.Log.LogInfo( "[BIGONI BROTHER] === Pushing GG away ===");

        try
        {
            // Находим ГГ
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null)
            {
                return;
            }

            // Get component enemy
            var bigoni = enemyInstance as Bigoni;
            if (bigoni == null)
            {
                return;
            }

            // Reset eroflag enemy ПЕРЕД остановкой animation
            try
            {
                bigoni.eroflag = false;
            }
            catch (System.Exception ex)
            {
            }

            // Stop H-animation enemy before скрытием (важно! иначе анимация зависнет)
            try
            {
                // Stop основную animation enemy (erospine)
                var erospineField = typeof(Bigoni).GetField("erospine",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (erospineField != null)
                {
                    var erospine = erospineField.GetValue(bigoni) as Spine.Unity.SkeletonAnimation;
                    if (erospine != null)
                    {
                        erospine.AnimationState.ClearTracks();
                    }
                }

                // Get erodata via reflection
                var erodataField = typeof(Bigoni).GetField("erodata",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (erodataField != null)
                {
                    GameObject erodata = erodataField.GetValue(bigoni) as GameObject;
                    if (erodata != null)
                    {
                        // Деактивируем erodata (важно! останавливает H-animation)
                        erodata.SetActive(false);

                        // Get StartBigoniERO component
                        var startBigoniERO = erodata.GetComponent<StartBigoniERO>();
                        if (startBigoniERO != null)
                        {
                            // Get myspine via reflection
                            var myspineField = typeof(StartBigoniERO).GetField("myspine",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (myspineField != null)
                            {
                                var enemySpine = myspineField.GetValue(startBigoniERO) as Spine.Unity.SkeletonAnimation;
                                if (enemySpine != null)
                                {
                                    enemySpine.AnimationState.ClearTracks();
                                }
                            }
                        }

                        // Также проверяем BigoniERO (if используется)
                        var bigoniERO = erodata.GetComponent<BigoniERO>();
                        if (bigoniERO != null)
                        {
                            var myspineField = typeof(BigoniERO).GetField("myspine",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (myspineField != null)
                            {
                                var enemySpine = myspineField.GetValue(bigoniERO) as Spine.Unity.SkeletonAnimation;
                                if (enemySpine != null)
                                {
                                    enemySpine.AnimationState.ClearTracks();
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                // Игнорируем ошибки - not критично
            }

            // Скрываем enemy полностью (SetActive(false) - as было раньше)
            try
            {
                bigoni.gameObject.SetActive(false);
                // Plugin.Log.LogInfo( "[BIGONI BROTHER] Enemy GameObject hidden (SetActive(false))");
            }
            catch (System.Exception ex)
            {
            }

            // ОЧИЩАЕМ АНИМАЦИЮ ГГ (важно! without этого ГГ останется in H-сцене)
            var playerSpine = playerObject.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
            if (playerSpine != null)
            {
                try
                {
                    playerSpine.AnimationState.ClearTracks();
                    // Plugin.Log.LogInfo( "[BIGONI BROTHER] Player spine cleared");
                }
                catch (System.Exception ex)
                {
                }
            }

            // Get playercon и сбрасываем state
            var playerComponent = playerObject.GetComponent<playercon>();
            if (playerComponent != null)
            {
                playerComponent.eroflag = false;
                playerComponent._eroflag2 = false;

                // CRITICAL: устанавливаем player.state = "DOWN" for корректной работы goblin
                // Гоблины проверяют this.com_player.state == "DOWN" before захватом
                var stateField = typeof(playercon).GetField("state",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (stateField != null)
                {
                    try
                    {
                        stateField.SetValue(playerComponent, "DOWN");
                        // Plugin.Log.LogInfo("[BIGONI BROTHER] Player state set to 'DOWN' for goblin compatibility");
                    }
                    catch (System.Exception ex)
                    {
                        // Игнорируем ошибки
                    }
                }

                // Set GG animation to lying (if еще not setа)
                if (playerSpine != null)
                {
                    string[] downAnims = { "DOWN", "down", "Idle", "idle" };
                    foreach (string animName in downAnims)
                    {
                        try
                        {
                            playerSpine.AnimationState.SetAnimation(0, animName, true);
                            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] GG animation set to '{animName}'");
                            break;
                        }
                        catch (System.Exception ex)
                        {
                        }
                    }
                }

                // Set erodown via reflection (ВСЕГДА set to 1 for DOWN состояния)
                var eroDownField = typeof(playercon).GetField("erodown", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (eroDownField != null)
                {
                    try
                    {
                        eroDownField.SetValue(playerComponent, 1);
                        // Plugin.Log.LogInfo( "[BIGONI BROTHER] erodown set to 1 (DOWN state)");
                    }
                    catch (System.Exception ex)
                    {
                    }
                }

                // Reset блокирующие флаги боевых действий и защиты
                playerComponent.Attacknow = false;
                playerComponent.Actstate = false;
                playerComponent.stepfrag = false;
                playerComponent.magicnow = false;
                playerComponent.guard = false;

                var parryField = typeof(playercon).GetField("Parry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                parryField?.SetValue(playerComponent, false);

                var itemUseField = typeof(playercon).GetField("Itemuse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                itemUseField?.SetValue(playerComponent, false);

                var stabNowField = typeof(playercon).GetField("stabnow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                stabNowField?.SetValue(playerComponent, false);

                playerComponent._easyESC = false;
                playerComponent.nowdamage = playerComponent.erodown != 0;
                StruggleSystem.setStruggleLevel(-1f);
                Time.timeScale = 1f;
                
                // CRITICAL: восстанавливаем физику игрока (иначе ГГ застревает: not двигается, атаки проходят сквозь)
                try
                {
                    if (playerComponent.rigi2d != null)
                    {
                        playerComponent.rigi2d.simulated = true;
                        playerComponent.rigi2d.velocity = Vector2.zero; // Reset скорость
                        playerComponent.rigi2d.angularVelocity = 0f; // Reset угловую скорость
                        // Plugin.Log.LogInfo( "[BIGONI BROTHER] Restored player physics: simulated=true, velocity=zero");
                    }
                }
                catch (System.Exception ex)
                {
                    // Plugin.Log.LogError($"[BIGONI BROTHER] Failed to restore player physics: {ex.Message}");
                }
            }
            else
            {
            }

            // Plugin.Log.LogInfo( "[BIGONI BROTHER] Player should be free now");
        }
        catch (System.Exception ex)
        {
        }
    }

    // Patch on ImmediatelyERO for cleanup on escape через GiveUp
    // By analogии with TouzokuNormalPassPatch, TouzokuAxePassPatch и GoblinPassLogic
    [HarmonyPatch(typeof(playercon), "ImmediatelyERO")]
    [HarmonyPostfix]
    static void ClearStateOnImmediatelyERO()
    {
        try
        {
            // Проверяем тип enemy - очищаем only for BigoniBrother
            Bigoni currentEnemy = UnityEngine.Object.FindObjectOfType<Bigoni>();
            if (currentEnemy == null || currentEnemy.gameObject == null ||
                !currentEnemy.gameObject.name.Contains("BigoniBrother"))
            {
                // Do not BigoniBrother - not очищаем
                return;
            }

            // Plugin.Log.LogInfo( "[BIGONI BROTHER] === CLEAR ON IMMEDIATELYERO (GiveUp) ===");
            ResetAll();
        }
        catch (System.Exception ex)
        {
        }
    }

    // Patch on StruggleSystem.startGrabInvul for cleanup on ручной борьбе
    // By analogии with TouzokuNormalPassPatch, TouzokuAxePassPatch и GoblinPassLogic
    [HarmonyPatch(typeof(StruggleSystem), "startGrabInvul")]
    [HarmonyPostfix]
    static void ClearStateOnStruggleEscape()
    {
        try
        {
            // Plugin.Log.LogInfo( "[BIGONI BROTHER] === CLEAR ON STRUGGLE ESCAPE ===");
            ResetAll();
        }
        catch (System.Exception ex)
        {
        }
    }
}