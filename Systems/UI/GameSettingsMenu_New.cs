using System;
using UnityEngine;
using BepInEx.Configuration;

namespace NoREroMod.Systems.UI
{
    /// <summary>
    /// Типы кнопок for sounds
    /// </summary>
    public enum ButtonType
    {
        ApplyClose,
        Reset
    }

    /// <summary>
    /// GameSettingsMenu - Система настроек QTE in главном меню игры
    /// </summary>
    internal static class GameSettingsMenu
    {
        private static bool _isInitialized = false;

        /// <summary>
        /// Инициализировать system настроек
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            try
            {
                SettingsDataManager.LoadSettingsDescriptions();
                UISettingsBuilder.CreateCanvas();
                SoundManager.Initialize(UISettingsBuilder.GetCanvas());
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Failed to initialize: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Display window настроек
        /// </summary>
        public static void ShowSettings()
        {
            Plugin.Log?.LogInfo("[GameSettingsMenu] ShowSettings() called");

            try
            {
                if (!_isInitialized)
                {
                    Plugin.Log?.LogInfo("[GameSettingsMenu] Not initialized, initializing...");
                    Initialize();
                }

                var canvas = UISettingsBuilder.GetCanvas();
                if (canvas == null)
                {
                    Plugin.Log?.LogError("[GameSettingsMenu] Canvas is null after initialization!");
                    return;
                }

                // Ensure Canvas активен
                if (!canvas.activeSelf)
                {
                    Plugin.Log?.LogInfo("[GameSettingsMenu] Canvas was inactive, activating...");
                    canvas.SetActive(true);
                }

                var settingsWindow = UISettingsBuilder.GetSettingsWindow();
                if (settingsWindow == null)
                {
                    Plugin.Log?.LogInfo("[GameSettingsMenu] Settings window is null, creating...");
                    UISettingsBuilder.CreateSettingsWindow();
                    settingsWindow = UISettingsBuilder.GetSettingsWindow();
                }

                if (settingsWindow != null)
                {
                    Plugin.Log?.LogInfo("[GameSettingsMenu] Settings window shown");

                    // Ensure Canvas active before показом window
                    canvas.SetActive(true);

                    // Ensure window active
                    settingsWindow.SetActive(true);

                    // Ensure window находится поверх other элементов
                    settingsWindow.transform.SetAsLastSibling();

                    SettingsValueManager.RefreshSettingsValues();

                    Plugin.Log?.LogInfo($"[GameSettingsMenu] Settings window shown. Window active: {settingsWindow.activeSelf}, Canvas active: {canvas.activeSelf}, Canvas sortingOrder: {canvas.GetComponent<UnityEngine.Canvas>()?.sortingOrder ?? -1}");
                }
                else
                {
                    Plugin.Log?.LogError("[GameSettingsMenu] Failed to create settings window!");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Error in ShowSettings(): {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Скрыть window настроек
        /// </summary>
        public static void HideSettings()
        {
            var settingsWindow = UISettingsBuilder.GetSettingsWindow();
            if (settingsWindow != null)
            {
                settingsWindow.SetActive(false);
            }
        }
    }
}
