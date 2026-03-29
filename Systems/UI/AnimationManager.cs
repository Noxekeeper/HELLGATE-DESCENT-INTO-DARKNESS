using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NoREroMod.Systems.UI
{
    /// <summary>
    /// Менеджер анимаций UI элементов
    /// </summary>
    public static class AnimationManager
    {
        private static GameObject? _animationRunner;

        /// <summary>
        /// Запустить анимацию моргания for измененных контейнеров
        /// </summary>
        public static void StartFlashAnimation(List<GameObject> containers)
        {
            var canvas = UISettingsBuilder.GetCanvas();
            if (canvas == null) return;

            // Создаем or получаем runner for корутины
            var runnerObj = canvas.transform.Find("AnimationRunner");
            if (runnerObj == null)
            {
                runnerObj = new GameObject("AnimationRunner").transform;
                runnerObj.SetParent(canvas.transform, false);
                _animationRunner = runnerObj.gameObject;
            }

            var runner = runnerObj.GetComponent<AnimationRunner>();
            if (runner == null)
            {
                runner = runnerObj.gameObject.AddComponent<AnimationRunner>();
            }

            runner.StartFlashAnimation(containers);
        }

        /// <summary>
        /// Внутренний компонент for запуска корутин анимации
        /// </summary>
        private class AnimationRunner : MonoBehaviour
        {
            public void StartFlashAnimation(List<GameObject> containers)
            {
                StartCoroutine(FlashAnimationCoroutine(containers));
            }

            private System.Collections.IEnumerator FlashAnimationCoroutine(List<GameObject> containers)
            {
                // Сохраняем оригинальные цвета и временbut меняем on white with прозрачностью
                Dictionary<GameObject, Color> originalColors = new Dictionary<GameObject, Color>();

                foreach (var container in containers)
                {
                    if (container == null) continue;

                    var containerImage = container.GetComponent<Image>();
                    if (containerImage == null)
                    {
                        containerImage = container.AddComponent<Image>();
                        containerImage.color = new Color(0f, 0f, 0f, 0f);
                    }

                    originalColors[container] = containerImage.color;
                    containerImage.color = new Color(1f, 1f, 1f, 0.2f); // Белый with небольшой прозрачностью
                }

                // Ждем 0.5 секунды
                yield return new WaitForSeconds(0.5f);

                // Восстанавливаем оригинальные цвета
                foreach (var kvp in originalColors)
                {
                    if (kvp.Key != null)
                    {
                        var containerImage = kvp.Key.GetComponent<Image>();
                        if (containerImage != null)
                        {
                            containerImage.color = kvp.Value;
                        }
                    }
                }

                Plugin.Log?.LogInfo("[AnimationManager] Flash animation completed");
            }
        }
    }
}
