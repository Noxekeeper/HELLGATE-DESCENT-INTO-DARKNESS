// ============================================================================
// ШАБЛОН ДЛЯ КОПИРОВАНИЯ
// ============================================================================
// ИНСТРУКЦИЯ:
// 1. Скопируйте этот файл в папку вашего врага (например, goblin/)
// 2. Переименуйте файл (например, GoblinPassLogic.cs)
// 3. Замените все "EnemyName" на имя вашего врага
// 4. Замените "EnemyEroType" на тип класса врага (найдите в коде игры)
// 5. Настройте методы GetHAnimations() и IsCycleComplete()
// 6. Добавьте в EnemyHandoffSystem.cs и DelayedHandoffScript.cs
// 7. Добавьте в .csproj файл
// ============================================================================

using HarmonyLib;
using UnityEngine;
using Spine.Unity;
using NoREroMod.Patches.Enemy.Base;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Логика захвата, анимаций и передачи ГГ для EnemyName
/// </summary>
class EnemyNamePassLogic : BaseEnemyPassPatch<EnemyEroType>
{
    protected override string EnemyName => "EnemyName";

    /// <summary>
    /// Количество циклов до передачи ГГ (1 для какаси, 2 для остальных)
    /// </summary>
    protected override int CyclesBeforePass => 2;

    /// <summary>
    /// Список H-анимаций для этого типа врага
    /// ВАЖНО: Найдите правильные имена анимаций для вашего врага!
    /// </summary>
    protected override string[] GetHAnimations()
    {
        return new[]
        {
            "START", "START2", "START3",
            "ERO", "ERO1", "ERO2", "ERO3", "ERO4", "ERO5",
            "FIN", "FIN2", "FIN3",
            "JIGO", "JIGO2"
        };
    }

    /// <summary>
    /// Определяет завершение полного цикла анимации
    /// ВАЖНО: Настройте под вашу логику анимаций!
    /// </summary>
    protected override bool IsCycleComplete(string animationName, string eventName, int seCount)
    {
        // ПРИМЕР 1: Завершение цикла на JIGO2 (как у TouzokuNormal)
        if (animationName == "JIGO2" && eventName == "JIGO2")
        {
            return true;
        }

        // ПРИМЕР 2: Завершение цикла на FIN (как у TouzokuAxe)
        if (animationName == "FIN" && eventName == "FIN")
        {
            return true;
        }

        // ПРИМЕР 3: Fallback на ERO (начало следующего цикла)
        if (animationName == "ERO" && eventName == "ERO")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Имя врага для системы фраз (используется в EnemySpeechPhrases)
    /// Должно совпадать с именем из списка врагов (например, "goblin", "sraimu")
    /// </summary>
    protected override string GetEnemyTypeName()
    {
        return "enemyname"; // Замените на реальное имя (например, "goblin", "sraimu")
    }

    /// <summary>
    /// Опционально: Переопределите если нужна особая логика принудительного перехода к середине
    /// </summary>
    protected override void ForceAnimationToMiddle(SkeletonAnimation spine)
    {
        // Если нужна особая логика, переопределите здесь
        // Иначе используем базовую реализацию
        base.ForceAnimationToMiddle(spine);
    }

    /// <summary>
    /// Метод сброса данных для этого типа врага
    /// </summary>
    internal static void ResetAll()
    {
        BaseEnemyPassPatch<EnemyEroType>.ResetAll();
    }
    
    /// <summary>
    /// Патч на OnEvent - регистрируется автоматически через Harmony
    /// </summary>
    [HarmonyPatch(typeof(EnemyEroType), "OnEvent")]
    [HarmonyPostfix]
    private static void EnemyNamePass(EnemyEroType __instance, Spine.Event e, int ___se_count)
    {
        // Создаем экземпляр для доступа к методам
        var instance = new EnemyNamePassLogic();
        SetInstance(instance);

        try
        {
            // Проверяем, отключен ли враг
            var disabledField = typeof(BaseEnemyPassPatch<EnemyEroType>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);

            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                if (disabledDict != null && disabledDict.ContainsKey(__instance) && disabledDict[__instance])
                {
                    return;
                }
            }

            // Проверяем H-сцену активна
            var player = GameObject.FindWithTag("Player")?.GetComponent<playercon>();
            if (player == null || !player.eroflag || player.erodown == 0)
            {
                return; // H-сцена не активна
            }

            var spine = GetSpineAnimation(__instance);
            if (spine == null)
            {
                return;
            }

            string currentAnim = spine.AnimationName;

            // Проверяем что это H-анимация
            if (!instance.IsHAnimation(currentAnim))
            {
                return; // Игнорируем боевые анимации
            }

            // Обработка диалоговой системы (если необходимо)
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
                // Игнорируем ошибки диалоговой системы
            }

            // Отслеживаем циклы и передачу
            instance.TrackCycles(__instance, spine, e, ___se_count);
        }
        catch (System.Exception ex)
        {
            // Логируем ошибки для диагностики
            Plugin.Log.LogError($"[EnemyNamePassLogic] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Публичный метод для вызова передачи (используется DelayedHandoffScript)
    /// </summary>
    public static void ExecuteHandoff(object enemyInstance)
    {
        try
        {
            // Находим ГГ
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null)
            {
                return;
            }

            // Помечаем врага как disabled
            var disabledField = typeof(BaseEnemyPassPatch<EnemyEroType>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);
            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                disabledDict[enemyInstance] = true;
            }

            // Останавливаем H-анимацию врага
            var enemyComponent = enemyInstance as EnemyEroType;
            if (enemyComponent != null)
            {
                try
                {
                    var enemySpine = GetSpineAnimation(enemyComponent);
                    if (enemySpine != null)
                    {
                        enemySpine.AnimationState.ClearTracks();

                        // Пробуем разные варианты idle анимаций
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
                                // Пробуем следующую анимацию
                            }
                        }
                    }

                    // Делаем врага невидимым
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

            // Очищаем анимацию ГГ
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

            // Получаем playercon
            var playerComponent = playerObject.GetComponent<playercon>();
            if (playerComponent == null)
            {
                return;
            }

            // Очищаем eroflag
            var eroFlagField = typeof(playercon).GetField("eroflag", BindingFlags.Public | BindingFlags.Instance);
            if (eroFlagField != null)
            {
                eroFlagField.SetValue(playerComponent, false);
            }

            // Устанавливаем анимацию ГГ на лежачую
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

            // Устанавливаем erodown
            var eroDownField = typeof(playercon).GetField("erodown", BindingFlags.Public | BindingFlags.Instance);
            if (eroDownField != null)
            {
                eroDownField.SetValue(playerComponent, 1);
            }

            // Сбрасываем SP
            var playerStatus = playerObject.GetComponent<PlayerStatus>();
            if (playerStatus != null)
            {
                playerStatus.Sp = 0f;
            }

            // Отталкиваем ГГ от врага
            var enemyTransform = (enemyInstance as MonoBehaviour)?.transform;
            if (enemyTransform != null)
            {
                Vector3 enemyPos = enemyTransform.position;
                Vector3 playerPos = playerComponent.transform.position;
                Vector3 direction = playerPos - enemyPos;
                direction.Normalize();

                // Поправка: если враг слева от ГГ, толкаем вправо
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

                // Сбрасываем вертикальную скорость
                var rigi2d = playerComponent.rigi2d;
                if (rigi2d != null)
                {
                    rigi2d.velocity = new Vector2(rigi2d.velocity.x, 0f);
                }
            }

            // Сбрасываем флаг борьбы
            StruggleSystem.setStruggleLevel(-1);

            // Включаем sprite renderer
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
