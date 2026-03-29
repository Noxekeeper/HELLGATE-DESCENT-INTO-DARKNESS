using System.Collections;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using static HarmonyLib.AccessTools;

namespace NoREroMod.Systems.UI
{
    /// <summary>
    /// Патч for добавления кнопки "HG Settings" in главное меню
    /// </summary>
    [HarmonyPatch(typeof(GameStart_menu), "Start")]
    public class GameSettingsMenuPatch
    {
        private static bool _buttonCreated = false;

        [HarmonyPostfix]
        static void Start_Postfix(GameStart_menu __instance)
        {
            try
            {
                // Проверяем, that this действительbut сцеon главного меню
                string currentSceneName = SceneManager.GetActiveScene().name;
                if (currentSceneName != "Gametitle")
                {
                    return; // Не создаем кнопку in other сценах
                }

                // Проверяем, not создаon ли already кнопка in этой сессии игры
                if (_buttonCreated)
                {
                    Plugin.Log?.LogInfo("[GameSettingsMenuPatch] Button already created in this session, skipping");
                    return;
                }

                CreateSettingsButton(__instance);
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogError($"[GameSettingsMenuPatch] Failed to create settings button: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Scene load handler новой сцены
        /// </summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            string sceneName = scene.name;
            Plugin.Log?.LogInfo($"[GameSettingsMenuPatch] Scene loaded: {sceneName}");

            // Если загружается сцеon главного меню, сбрасываем флаг кнопки
            if (sceneName == "Gametitle")
            {
                _buttonCreated = false;
                Plugin.Log?.LogInfo("[GameSettingsMenuPatch] Button creation flag reset for main menu");
            }
            else
            {
                // Если загружается другая сцена, кнопка может исчезнуть, поэтому сбрасываем флаг
                _buttonCreated = false;
                Plugin.Log?.LogInfo("[GameSettingsMenuPatch] Button creation flag reset for non-main menu scene");
            }
        }
        
        /// <summary>
        /// Создать кнопку "HG Settings" in главном меню
        /// </summary>
        private static void CreateSettingsButton(GameStart_menu menuInstance)
        {
            if (menuInstance == null)
            {
                return;
            }
            
            // Находим Canvas главного меню
            GameObject? menuCanvas = null;
            
            // Пробуем получить canvas from menuInstance через reflection
            try
            {
                var canvasField = AccessTools.Field(typeof(GameStart_menu), "canvas");
                if (canvasField != null)
                {
                    menuCanvas = canvasField.GetValue(menuInstance) as GameObject;
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogWarning($"[GameSettingsMenuPatch] Could not get canvas from GameStart_menu: {ex.Message}");
            }
            
            // Fallback: ищем Canvas in сцене
            if (menuCanvas == null)
            {
                Canvas foundCanvas = Object.FindObjectOfType<Canvas>();
                if (foundCanvas != null)
                {
                    menuCanvas = foundCanvas.gameObject;
                }
            }
            
            if (menuCanvas == null)
            {
                Plugin.Log?.LogError("[GameSettingsMenuPatch] Could not find menu canvas!");
                return;
            }
            
            // Убеждаемся, that Canvas имеет GraphicRaycaster
            Canvas canvas = menuCanvas.GetComponent<Canvas>();
            if (canvas != null)
            {
                GraphicRaycaster raycaster = menuCanvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null)
                {
                    raycaster = menuCanvas.AddComponent<GraphicRaycaster>();
                    Plugin.Log?.LogInfo("[GameSettingsMenuPatch] Added GraphicRaycaster to canvas");
                }
            }
            
            // Убеждаемся, that есть EventSystem
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                UnityEngine.Object.DontDestroyOnLoad(eventSystemObj);
                Plugin.Log?.LogInfo("[GameSettingsMenuPatch] Created EventSystem");
            }
            
            // Проверяем, not создаon ли already кнопка (проверяем ВСЕ дочерние объекты Canvas)
            Transform existingButton = null;
            for (int i = 0; i < menuCanvas.transform.childCount; i++)
            {
                Transform child = menuCanvas.transform.GetChild(i);
                if (child.name == "HGSettingsButton")
                {
                    existingButton = child;
                    break;
                }
            }
            
            if (existingButton != null)
            {
                Plugin.Log?.LogInfo("[GameSettingsMenuPatch] Button already exists, skipping creation");
                return; // Кнопка already существует, not создаем новую
            }
            
            // Создаем кнопку
            GameObject buttonObj = new GameObject("HGSettingsButton");
            buttonObj.transform.SetParent(menuCanvas.transform, false);
            
            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(400f, 0f); // От центра вправо 400px
            rect.sizeDelta = new Vector2(200f, 50f);
            
            // Убеждаемся, that кнопка находится поверх other элементов
            rect.SetAsLastSibling();
            
            // Добавляем Button
            Button button = buttonObj.AddComponent<Button>();
            button.interactable = true; // Явbut включаем интерактивность
            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            image.raycastTarget = true; // Важbut for кликабельности
            button.targetGraphic = image;
            
            // Добавляем текст
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            Text text = textObj.AddComponent<Text>();
            text.text = "HG Settings";
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false; // Важно: текст not must блокировать клики
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            
            // Анимация (as in HellGateSplashScreen)
            UnityEngine.EventSystems.EventTrigger trigger = buttonObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            
            UnityEngine.EventSystems.EventTrigger.Entry enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            enterEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((eventData) =>
            {
                // Увеличиваем кнопку on наведении
                rect.localScale = new Vector3(1.15f, 1.15f, 1f);
            });
            trigger.triggers.Add(enterEntry);
            
            UnityEngine.EventSystems.EventTrigger.Entry exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            exitEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((eventData) =>
            {
                // Возвращаем нормальный размер
                rect.localScale = Vector3.one;
            });
            trigger.triggers.Add(exitEntry);
            
            // Обработчик клика
            button.onClick.AddListener(() =>
            {
                Plugin.Log?.LogInfo("[GameSettingsMenuPatch] Button clicked! Calling ShowSettings()...");
                try
                {
                    GameSettingsMenu.ShowSettings();
                    Plugin.Log?.LogInfo("[GameSettingsMenuPatch] ShowSettings() called successfully");

                    // Убеждаемся that кнопка остается видимой after открытия меню
                    if (buttonObj != null)
                    {
                        buttonObj.transform.SetAsLastSibling();
                    }
                }
                catch (System.Exception ex)
                {
                    Plugin.Log?.LogError($"[GameSettingsMenuPatch] Error in button click handler: {ex.Message}\n{ex.StackTrace}");
                }
            });
            
            // Дополнительная проверка: убеждаемся that все компоненты активны
            buttonObj.SetActive(true);
            button.enabled = true;
            image.enabled = true;
            
            Plugin.Log?.LogInfo($"[GameSettingsMenuPatch] Settings button created successfully. Interactable: {button.interactable}, Enabled: {button.enabled}, Active: {buttonObj.activeSelf}");

            // Отмечаем, that кнопка создана
            _buttonCreated = true;
        }
    }
}
