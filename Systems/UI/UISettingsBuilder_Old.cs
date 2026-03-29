using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BepInEx.Configuration;

namespace NoREroMod.Systems.UI
{
    /// <summary>
    /// Строитель UI элементоin for меню настроек
    /// </summary>
    public static class UISettingsBuilder
    {
        // UI элементы
        private static GameObject? _canvas;
        private static GameObject? _settingsWindow;
        private static GameObject? _scrollView;
        private static GameObject? _content;
        private static GameObject? _footerObj;
        private static Toggle? _qteSystemToggle;
        private static Dictionary<string, SettingElement> _settingsElements = new();

        /// <summary>
        /// Структура for хранения элементоin настройки
        /// </summary>
        public class SettingElement
        {
            public GameObject? container;
            public Text? label;
            public Slider? slider;
            public InputField? inputField;
            public ConfigEntry<float>? configEntry;
            public ConfigEntry<int>? configEntryInt;
            public float minValue;
            public float maxValue;
            public int decimals;
            public GameObject? borderObj; // Border for визуализации изменений
            public Image? borderImage; // Image компонент border
            public float originalValue; // Оригинальное значение for отслеживания изменений
            public bool isChanged; // Флаг изменения значения
        }

        /// <summary>
        /// Получить Canvas
        /// </summary>
        public static GameObject? GetCanvas() => _canvas;

        /// <summary>
        /// Получить window настроек
        /// </summary>
        public static GameObject? GetSettingsWindow() => _settingsWindow;

        /// <summary>
        /// Получить dictionary элементоin настроек
        /// </summary>
        public static Dictionary<string, SettingElement> GetSettingsElements() => _settingsElements;

        /// <summary>
        /// Получить toggle QTE системы
        /// </summary>
        public static Toggle? GetQTESystemToggle() => _qteSystemToggle;

        /// <summary>
        /// Создать Canvas for меню настроек
        /// </summary>
        public static void CreateCanvas()
        {
            if (_canvas != null)
            {
                return;
            }

            _canvas = new GameObject("HELLGATE_SettingsCanvas");
            Canvas canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32767; // Максимальный приоритет, as in HellGateSplashScreen

            CanvasScaler scaler = _canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _canvas.AddComponent<GraphicRaycaster>();
            UnityEngine.Object.DontDestroyOnLoad(_canvas);
            _canvas.SetActive(true); // Активируем Canvas сразу, чтобы он был готов

            // EventSystem
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                UnityEngine.Object.DontDestroyOnLoad(eventSystemObj);
            }

            Plugin.Log?.LogInfo("[UISettingsBuilder] Canvas created");
        }

        /// <summary>
        /// Создать модальное window настроек
        /// </summary>
        public static void CreateSettingsWindow()
        {
            if (_canvas == null)
            {
                Plugin.Log?.LogError("[UISettingsBuilder] Cannot create settings window: Canvas is null");
                return;
            }

            Plugin.Log?.LogInfo("[UISettingsBuilder] Creating settings window...");

            // Полноэкранный черный фон
            _settingsWindow = new GameObject("SettingsWindow");
            _settingsWindow.transform.SetParent(_canvas.transform, false);

            RectTransform windowRect = _settingsWindow.AddComponent<RectTransform>();
            windowRect.anchorMin = Vector2.zero; // Полный экран
            windowRect.anchorMax = Vector2.one;
            windowRect.offsetMin = Vector2.zero;
            windowRect.offsetMax = Vector2.zero;

            // Черный фон on весь экран
            Image windowImage = _settingsWindow.AddComponent<Image>();
            windowImage.color = new Color(0f, 0f, 0f, 0.9f); // Почти черный фон
            windowImage.raycastTarget = false; // Отключаем raycast for фона

            // Создаем содержимое окна
            CreateWindowContent();

            Plugin.Log?.LogInfo("[UISettingsBuilder] Settings window created");
        }

        /// <summary>
        /// Создать содержимое окon настроек
        /// </summary>
        private static void CreateWindowContent()
        {
            if (_settingsWindow == null) return;

            // Заголовок вверху
            CreateWindowTitle();

            // Создаем область настроек (таблица)
            CreateSettingsArea();

            // Создаем три кнопки downу
            CreateBottomButtons();
        }

        /// <summary>
        /// Создать заголовок окна
        /// </summary>
        private static void CreateWindowTitle()
        {
            if (_settingsWindow == null) return;

            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_settingsWindow.transform, false);

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1); // Верхний край
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(0, -100); // 100px высота заголовка
            titleRect.offsetMax = new Vector2(0, 0);

            UnityEngine.UI.Text titleText = titleObj.AddComponent<UnityEngine.UI.Text>();
            titleText.text = "HELLGATE QTE SETTINGS";
            titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleText.fontSize = 48;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.fontStyle = FontStyle.Bold;
        }

        /// <summary>
        /// Создать область настроек with таблицей
        /// </summary>
        private static void CreateSettingsArea()
        {
            if (_settingsWindow == null) return;

            // Создаем ScrollView for таблицы настроек
            _scrollView = new GameObject("SettingsScrollView");
            _scrollView.transform.SetParent(_settingsWindow.transform, false);

            RectTransform scrollRect = _scrollView.AddComponent<RectTransform>();
            // Область from заголовка (100px сверху) until кнопок (150px снизу)
            scrollRect.anchorMin = new Vector2(0, 0);
            scrollRect.anchorMax = new Vector2(1, 1);
            scrollRect.offsetMin = new Vector2(20, 160); // 160px снизу for кнопок
            scrollRect.offsetMax = new Vector2(-40, -120); // 120px сверху for заголовка

            ScrollRect scrollComponent = _scrollView.AddComponent<ScrollRect>();
            scrollComponent.scrollSensitivity = 30f; // Нормальная скорость скролла
            scrollComponent.movementType = ScrollRect.MovementType.Clamped;
            scrollComponent.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollComponent.verticalScrollbar = CreateScrollbar();

            // Viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(_scrollView.transform, false);

            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(0, 0);
            viewportRect.offsetMax = new Vector2(-20, 0); // Место for скроллбара

            viewport.AddComponent<Mask>();
            scrollComponent.viewport = viewportRect;

            // Content - таблица настроек
            _content = new GameObject("SettingsTable");
            _content.transform.SetParent(viewport.transform, false);

            RectTransform contentRect = _content.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup layoutGroup = _content.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 1;
            layoutGroup.padding = new RectOffset(5, 5, 5, 5);
            layoutGroup.childAlignment = TextAnchor.UpperLeft; // Align to top-left
            layoutGroup.childControlWidth = true; // Control child width
            layoutGroup.childControlHeight = true; // Control child height
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            ContentSizeFitter sizeFitter = _content.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollComponent.content = contentRect;

            // Создаем заголовки таблицы
            CreateTableHeaders();

            // Создаем элементы настроек
            CreateSettingsElements();

            Plugin.Log?.LogInfo("[UISettingsBuilder] Settings area created");
        }

        /// <summary>
        /// Создать скроллбар
        /// </summary>
        private static Scrollbar CreateScrollbar()
        {
            GameObject scrollbarObj = new GameObject("Scrollbar");
            scrollbarObj.transform.SetParent(_scrollView.transform, false);

            RectTransform scrollbarRect = scrollbarObj.AddComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1, 0);
            scrollbarRect.anchorMax = new Vector2(1, 1);
            scrollbarRect.offsetMin = new Vector2(-20, 10);
            scrollbarRect.offsetMax = new Vector2(0, -10);

            Scrollbar scrollbar = scrollbarObj.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            // Создаем полосу скроллбара
            GameObject slidingArea = new GameObject("SlidingArea");
            slidingArea.transform.SetParent(scrollbarObj.transform, false);

            RectTransform slidingRect = slidingArea.AddComponent<RectTransform>();
            slidingRect.anchorMin = Vector2.zero;
            slidingRect.anchorMax = Vector2.one;
            slidingRect.offsetMin = new Vector2(5, 5);
            slidingRect.offsetMax = new Vector2(-5, -5);

            // Handle
            GameObject handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(slidingArea.transform, false);

            RectTransform handleRect = handleObj.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 20);

            Image handleImage = handleObj.AddComponent<Image>();
            handleImage.color = new Color(0.8f, 0.8f, 0.8f, 1f);

            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;

            return scrollbar;
        }

        /// <summary>
        /// Создать заголовки таблицы
        /// </summary>
        private static void CreateTableHeaders()
        {
            if (_content == null) return;

            GameObject headerRow = new GameObject("TableHeader");
            headerRow.transform.SetParent(_content.transform, false);

            RectTransform headerRect = headerRow.AddComponent<RectTransform>();
            headerRect.sizeDelta = new Vector2(0, 40);

            HorizontalLayoutGroup headerLayout = headerRow.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 10;
            headerLayout.padding = new RectOffset(5, 5, 5, 5);
            headerLayout.childAlignment = TextAnchor.MiddleCenter;
            headerLayout.childControlWidth = false;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = true;

            // Заголовки колонок
            CreateHeaderCell("Section", 150);
            CreateHeaderCell("Setting", 200);
            CreateHeaderCell("Value", 120);
            CreateHeaderCell("Default", 80);
            CreateHeaderCell("Comment", 120);
            CreateHeaderCell("Description", 0); // Flexible width
        }

        /// <summary>
        /// Создать ячейку заголовка
        /// </summary>
        private static void CreateHeaderCell(string text, float width)
        {
            GameObject cellObj = new GameObject("Header_" + text);
            cellObj.transform.SetParent(_content.transform.GetChild(_content.transform.childCount - 1), false);

            RectTransform cellRect = cellObj.AddComponent<RectTransform>();
            if (width > 0)
            {
                cellRect.sizeDelta = new Vector2(width, 0);
            }

            UnityEngine.UI.Text textComponent = cellObj.AddComponent<UnityEngine.UI.Text>();
            textComponent.text = text;
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComponent.fontSize = 14;
            textComponent.fontStyle = FontStyle.Bold;
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.color = new Color(1f, 0.8f, 0f); // Золотистый

            LayoutElement layoutElement = cellObj.AddComponent<LayoutElement>();
            if (width > 0)
            {
                layoutElement.minWidth = width;
                layoutElement.flexibleWidth = 0;
            }
            else
            {
                layoutElement.flexibleWidth = 1;
            }
        }

        /// <summary>
        /// Создать три кнопки downу
        /// </summary>
        private static void CreateBottomButtons()
        {
            if (_settingsWindow == null) return;

            GameObject buttonsContainer = new GameObject("BottomButtons");
            buttonsContainer.transform.SetParent(_settingsWindow.transform, false);

            RectTransform containerRect = buttonsContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0);
            containerRect.anchorMax = new Vector2(1, 0);
            containerRect.offsetMin = new Vector2(0, 0);
            containerRect.offsetMax = new Vector2(0, 150); // 150px высота for кнопок

            HorizontalLayoutGroup layoutGroup = buttonsContainer.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = 50;
            layoutGroup.padding = new RectOffset(100, 100, 50, 20);
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            // Создаем три кнопки with анимацией as in LoadingScreenSystem
            CreateAnimatedButton("Apply", () => SettingsValueManager.ApplySettings());
            CreateAnimatedButton("Reset", () => SettingsValueManager.ResetToDefaults());
            CreateAnimatedButton("Close", () => GameSettingsMenu.HideSettings());

            Plugin.Log?.LogInfo("[UISettingsBuilder] Bottom buttons created");
        }

        /// <summary>
        /// Создать анимированную кнопку (as in LoadingScreenSystem)
        /// </summary>
        private static void CreateAnimatedButton(string text, Action onClick)
        {
            GameObject buttonObj = new GameObject(text + "Button");
            buttonObj.transform.SetParent(_settingsWindow.transform.Find("BottomButtons"), false);

            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(250, 60);

            // Button component
            Button button = buttonObj.AddComponent<Button>();
            button.interactable = true;

            // Фон кнопки
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            buttonImage.raycastTarget = true;

            // Текст кнопки
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            UnityEngine.UI.Text buttonText = textObj.AddComponent<UnityEngine.UI.Text>();
            buttonText.text = text;
            buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            buttonText.fontSize = 28;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.fontStyle = FontStyle.Bold;
            buttonText.color = Color.white;
            buttonText.supportRichText = false;

            // Анимация наведения (as in LoadingScreenSystem)
            UnityEngine.EventSystems.EventTrigger trigger = buttonObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            // PointerEnter - увеличение и подсветка
            UnityEngine.EventSystems.EventTrigger.Entry enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            enterEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((eventData) =>
            {
                buttonObj.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
                buttonImage.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            });
            trigger.triggers.Add(enterEntry);

            // PointerExit - возврат к нормальному состоянию
            UnityEngine.EventSystems.EventTrigger.Entry exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            exitEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((eventData) =>
            {
                buttonObj.transform.localScale = Vector3.one;
                buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            });
            trigger.triggers.Add(exitEntry);

            // Обработчик клика
            button.onClick.AddListener(() =>
            {
                try
                {
                    // Воспроизвести sound
                    if (text == "Reset")
                    {
                        SoundManager.PlayButtonSound(ButtonType.Reset);
                    }
                    else
                    {
                        SoundManager.PlayButtonSound(ButtonType.ApplyClose);
                    }

                    onClick?.Invoke();
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"[UISettingsBuilder] Error in button click handler: {ex.Message}");
                }
            });

            Plugin.Log?.LogInfo($"[UISettingsBuilder] Animated button '{text}' created");
        }

        /// <summary>
        /// Создать кнопку in Footer
        /// </summary>
        public static void CreateFooterButton(string text, Vector2 size, Action onClick)
        {
            if (_footerObj == null) return;

            GameObject buttonObj = new GameObject(text + "Button");
            buttonObj.transform.SetParent(_footerObj.transform, false);

            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.sizeDelta = size;

            Button button = buttonObj.AddComponent<Button>();

            // Настраиваем цвета кнопки for hover эффекта
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            colors.highlightedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            colors.colorMultiplier = 1;
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            // Фон кнопки
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = colors.normalColor;

            // Текст кнопки
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text buttonText = textObj.AddComponent<Text>();
            buttonText.text = text;
            buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            buttonText.fontSize = 18;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.color = Color.white;

            // Обработчик клика
            button.onClick.AddListener(() =>
            {
                try
                {
                    // Воспроизвести sound
                    if (text == "Reset")
                    {
                        SoundManager.PlayButtonSound(ButtonType.Reset);
                    }
                    else
                    {
                        SoundManager.PlayButtonSound(ButtonType.ApplyClose);
                    }

                    onClick?.Invoke();
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"[UISettingsBuilder] Error in button click handler: {ex.Message}");
                }
            });

            Plugin.Log?.LogInfo($"[UISettingsBuilder] Footer button '{text}' created");
        }

        /// <summary>
        /// Создать элементы настроек in виде таблицы
        /// </summary>
        public static void CreateSettingsElements()
        {
            if (_content == null)
            {
                Plugin.Log?.LogError("[UISettingsBuilder] CreateSettingsElements: _content is null!");
                return;
            }

            try
            {
                // Настройки SP Gain
                CreateTableRow("SP Gain", "SP Gain Base (0% MB)", Plugin.qteSPGainBase, 0.1f, 5.0f, 3);
                CreateTableRow("SP Gain", "SP Gain Min (100% MB)", Plugin.qteSPGainMin, 0.1f, 2.0f, 3);
                CreateTableRow("SP Gain", "Yellow Button SP Gain Min", Plugin.qteYellowButtonSPGainMin, 1.0f, 10.0f, 1);
                CreateTableRow("SP Gain", "Yellow Button SP Gain Max", Plugin.qteYellowButtonSPGainMax, 5.0f, 30.0f, 1);

                // Настройки Penalty
                CreateTableRow("Penalty", "MP Penalty Percent", Plugin.qteMPPenaltyPercent, 10.0f, 50.0f, 1);
                CreateTableRow("Penalty", "MindBroken Penalty Percent", Plugin.qteMindBrokenPenaltyPercent, 0.5f, 5.0f, 1);
                CreateTableRow("Penalty", "Red Button MB Penalty", Plugin.qteRedButtonMindBrokenPenalty, 1.0f, 10.0f, 1);
                CreateTableRow("Penalty", "SP Penalty Multiplier", Plugin.qteSPPenaltyMultiplier, 1.0f, 5.0f, 1);

                // Настройки Timer
                CreateTableRow("Timer", "Window Duration Min", Plugin.qteWindowDurationMin, 1.0f, 5.0f, 1);
                CreateTableRow("Timer", "Window Duration Max", Plugin.qteWindowDurationMax, 2.0f, 6.0f, 1);
                CreateTableRow("Timer", "Cooldown Duration Min", Plugin.qteCooldownDurationMin, 1.0f, 5.0f, 1);
                CreateTableRow("Timer", "Cooldown Duration Max", Plugin.qteCooldownDurationMax, 2.0f, 8.0f, 1);

                // Visual Effects
                CreateTableRow("Visual", "Rotation Speed", Plugin.qteRotationSpeed, 30.0f, 180.0f, 0);
                CreateTableRow("Visual", "Rotation Start Time", Plugin.qteRotationStartTime, 0.1f, 2.0f, 1);
                CreateTableRow("Visual", "Color Change Interval", Plugin.qteColorChangeInterval, 0.5f, 3.0f, 1);
                CreateTableRow("Visual", "Press Indicator Duration", Plugin.qtePressIndicatorDuration, 0.05f, 0.5f, 2);
                CreateTableRow("Visual", "Max Button Transparency", Plugin.qteMaxButtonTransparency, 0.0f, 1.0f, 2);
                CreateTableRow("Visual", "Max Pink Shadow Intensity", Plugin.qteMaxPinkShadowIntensity, 0.0f, 2.0f, 1);

                // Sound Settings
                CreateTableRow("Sound", "Success Volume Multiplier", Plugin.qteSuccessVolumeMultiplier, 0.0f, 2.0f, 1);
                CreateTableRow("Sound", "Failure Volume Multiplier", Plugin.qteFailureVolumeMultiplier, 0.0f, 2.0f, 1);

                // Combo Settings
                CreateTableRow("Combo", "Combo Milestone", Plugin.qteComboMilestone, 5, 50);

                Plugin.Log?.LogInfo("[UISettingsBuilder] Settings table created");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[UISettingsBuilder] Error in CreateSettingsElements: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Создать строку таблицы for настройки
        /// </summary>
        private static void CreateTableRow(string section, string settingName, ConfigEntry<float> configEntry, float minValue, float maxValue, int decimals)
        {
            if (_content == null) return;

            GameObject rowObj = new GameObject("Row_" + settingName.Replace(" ", "").Replace("(", "").Replace(")", "").Replace("%", ""));
            rowObj.transform.SetParent(_content.transform, false);

            RectTransform rowRect = rowObj.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 35);

            HorizontalLayoutGroup rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10;
            rowLayout.padding = new RectOffset(5, 5, 2, 2);
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            // Получить описание from SettingsDataManager
            var settingDesc = SettingsDataManager.GetSettingDescription(settingName);

            // 1. Section
            CreateTableCell(section, 150, TextAnchor.MiddleCenter);

            // 2. Setting Name
            CreateTableCell(settingName, 200, TextAnchor.MiddleLeft);

            // 3. Value (InputField + Slider)
            CreateValueCell(configEntry, minValue, maxValue, decimals, 120);

            // 4. Default Value
            string defaultValue = settingDesc?.defaultValue ?? "N/A";
            CreateTableCell(defaultValue, 80, TextAnchor.MiddleCenter);

            // 5. Comment (короткий комментарий - пока пустой)
            CreateTableCell("", 120, TextAnchor.MiddleLeft);

            // 6. Description
            string description = settingDesc?.description ?? "No description available";
            CreateTableCell(description, 0, TextAnchor.MiddleLeft, true); // Flexible width
        }

        /// <summary>
        /// Создать строку таблицы for int настройки
        /// </summary>
        private static void CreateTableRow(string section, string settingName, ConfigEntry<int> configEntry, int minValue, int maxValue)
        {
            if (_content == null) return;

            GameObject rowObj = new GameObject("Row_" + settingName.Replace(" ", "").Replace("(", "").Replace(")", "").Replace("%", ""));
            rowObj.transform.SetParent(_content.transform, false);

            RectTransform rowRect = rowObj.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 35);

            HorizontalLayoutGroup rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10;
            rowLayout.padding = new RectOffset(5, 5, 2, 2);
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            // Получить описание from SettingsDataManager
            var settingDesc = SettingsDataManager.GetSettingDescription(settingName);

            // 1. Section
            CreateTableCell(section, 150, TextAnchor.MiddleCenter);

            // 2. Setting Name
            CreateTableCell(settingName, 200, TextAnchor.MiddleLeft);

            // 3. Value (InputField + Slider)
            CreateValueCell(configEntry, minValue, maxValue, 120);

            // 4. Default Value
            string defaultValue = settingDesc?.defaultValue ?? "N/A";
            CreateTableCell(defaultValue, 80, TextAnchor.MiddleCenter);

            // 5. Comment (короткий комментарий - пока пустой)
            CreateTableCell("", 120, TextAnchor.MiddleLeft);

            // 6. Description
            string description = settingDesc?.description ?? "No description available";
            CreateTableCell(description, 0, TextAnchor.MiddleLeft, true); // Flexible width
        }

        /// <summary>
        /// Создать ячейку таблицы
        /// </summary>
        private static void CreateTableCell(string text, float width, TextAnchor alignment, bool flexible = false)
        {
            GameObject cellObj = new GameObject("Cell");
            cellObj.transform.SetParent(_content.transform.GetChild(_content.transform.childCount - 1), false);

            RectTransform cellRect = cellObj.AddComponent<RectTransform>();
            if (width > 0)
            {
                cellRect.sizeDelta = new Vector2(width, 0);
            }

            UnityEngine.UI.Text textComponent = cellObj.AddComponent<UnityEngine.UI.Text>();
            textComponent.text = text;
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComponent.fontSize = 12;
            textComponent.alignment = alignment;
            textComponent.color = Color.white;
            textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComponent.verticalOverflow = VerticalWrapMode.Truncate;
            textComponent.supportRichText = false;

            LayoutElement layoutElement = cellObj.AddComponent<LayoutElement>();
            if (width > 0 && !flexible)
            {
                layoutElement.minWidth = width;
                layoutElement.flexibleWidth = 0;
            }
            else if (flexible)
            {
                layoutElement.flexibleWidth = 1;
            }
        }

        /// <summary>
        /// Создать ячейку со значением (пока просто текст for отображения)
        /// </summary>
        private static void CreateValueCell(ConfigEntry<float> configEntry, float minValue, float maxValue, int decimals, float width)
        {
            GameObject cellObj = new GameObject("ValueCell");
            cellObj.transform.SetParent(_content.transform.GetChild(_content.transform.childCount - 1), false);

            RectTransform cellRect = cellObj.AddComponent<RectTransform>();
            cellRect.sizeDelta = new Vector2(width, 0);

            LayoutElement cellLayoutElement = cellObj.AddComponent<LayoutElement>();
            cellLayoutElement.minWidth = width;

            // Пока просто отображаем значение as текст
            UnityEngine.UI.Text valueText = cellObj.AddComponent<UnityEngine.UI.Text>();
            valueText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            valueText.fontSize = 12;
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.color = Color.white;
            valueText.supportRichText = false;
            valueText.text = configEntry.Value.ToString($"F{decimals}"); // Для отображения
            slider.minValue = minValue;
            slider.maxValue = maxValue;

            // Background slider
            GameObject sliderBg = new GameObject("Background");
            sliderBg.transform.SetParent(sliderObj.transform, false);
            RectTransform sliderBgRect = sliderBg.AddComponent<RectTransform>();
            sliderBgRect.anchorMin = Vector2.zero;
            sliderBgRect.anchorMax = Vector2.one;
            sliderBgRect.offsetMin = new Vector2(5, 5);
            sliderBgRect.offsetMax = new Vector2(-5, -5);

            Image sliderBgImage = sliderBg.AddComponent<Image>();
            sliderBgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Fill slider
            GameObject sliderFill = new GameObject("Fill");
            sliderFill.transform.SetParent(sliderBg.transform, false);
            RectTransform sliderFillRect = sliderFill.AddComponent<RectTransform>();
            sliderFillRect.anchorMin = new Vector2(0, 0);
            sliderFillRect.anchorMax = new Vector2(0, 1);
            sliderFillRect.offsetMin = Vector2.zero;
            sliderFillRect.offsetMax = Vector2.zero;

            Image sliderFillImage = sliderFill.AddComponent<Image>();
            sliderFillImage.color = new Color(0.4f, 0.6f, 1f, 1f);

            slider.fillRect = sliderFillRect;

            // Handle slider
            GameObject sliderHandle = new GameObject("Handle");
            sliderHandle.transform.SetParent(sliderObj.transform, false);
            RectTransform sliderHandleRect = sliderHandle.AddComponent<RectTransform>();
            sliderHandleRect.sizeDelta = new Vector2(12, 20);

            Image sliderHandleImage = sliderHandle.AddComponent<Image>();
            sliderHandleImage.color = Color.white;

            slider.handleRect = sliderHandleRect;

            // TODO: Добавить возможность редактирования позже
        }

        /// <summary>
        /// Создать ячейку со значением for int настройки (пока просто текст)
        /// </summary>
        private static void CreateValueCell(ConfigEntry<int> configEntry, int minValue, int maxValue, float width)
        {
            GameObject cellObj = new GameObject("ValueCell");
            cellObj.transform.SetParent(_content.transform.GetChild(_content.transform.childCount - 1), false);

            RectTransform cellRect = cellObj.AddComponent<RectTransform>();
            cellRect.sizeDelta = new Vector2(width, 0);

            LayoutElement cellLayoutElement = cellObj.AddComponent<LayoutElement>();
            cellLayoutElement.minWidth = width;

            // Пока просто отображаем значение as текст
            UnityEngine.UI.Text valueText = cellObj.AddComponent<UnityEngine.UI.Text>();
            valueText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            valueText.fontSize = 12;
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.color = Color.white;
            valueText.supportRichText = false;
            valueText.text = configEntry.Value.ToString();

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.wholeNumbers = true;

            // Background slider
            GameObject sliderBg = new GameObject("Background");
            sliderBg.transform.SetParent(sliderObj.transform, false);
            RectTransform sliderBgRect = sliderBg.AddComponent<RectTransform>();
            sliderBgRect.anchorMin = Vector2.zero;
            sliderBgRect.anchorMax = Vector2.one;
            sliderBgRect.offsetMin = new Vector2(5, 5);
            sliderBgRect.offsetMax = new Vector2(-5, -5);

            Image sliderBgImage = sliderBg.AddComponent<Image>();
            sliderBgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Fill slider
            GameObject sliderFill = new GameObject("Fill");
            sliderFill.transform.SetParent(sliderBg.transform, false);
            RectTransform sliderFillRect = sliderFill.AddComponent<RectTransform>();
            sliderFillRect.anchorMin = new Vector2(0, 0);
            sliderFillRect.anchorMax = new Vector2(0, 1);
            sliderFillRect.offsetMin = Vector2.zero;
            sliderFillRect.offsetMax = Vector2.zero;

            Image sliderFillImage = sliderFill.AddComponent<Image>();
            sliderFillImage.color = new Color(0.4f, 0.6f, 1f, 1f);

            slider.fillRect = sliderFillRect;

            // Handle slider
            GameObject sliderHandle = new GameObject("Handle");
            sliderHandle.transform.SetParent(sliderObj.transform, false);
            RectTransform sliderHandleRect = sliderHandle.AddComponent<RectTransform>();
            sliderHandleRect.sizeDelta = new Vector2(12, 20);

            Image sliderHandleImage = sliderHandle.AddComponent<Image>();
            sliderHandleImage.color = Color.white;

            slider.handleRect = sliderHandleRect;

            LayoutElement sliderLayout = sliderObj.AddComponent<LayoutElement>();
            sliderLayout.flexibleWidth = 1;
            sliderLayout.minHeight = 20;

            // Устанавливаем значения
            int currentValue = configEntry.Value;
            inputField.text = currentValue.ToString();
            slider.value = currentValue;

            // Создаем SettingElement
            SettingElement element = new SettingElement
            {
                container = _content.transform.GetChild(_content.transform.childCount - 1).gameObject,
                label = _content.transform.GetChild(_content.transform.childCount - 1).GetChild(1).GetComponent<UnityEngine.UI.Text>(), // Setting name
                slider = slider,
                inputField = inputField,
                configEntryInt = configEntry,
                minValue = minValue,
                maxValue = maxValue,
                decimals = 0,
                borderObj = null,
                borderImage = null,
                originalValue = currentValue,
                isChanged = false
            };

            _settingsElements[configEntry.Definition.Key] = element;

            // Обработчики событий
            inputField.onEndEdit.AddListener((value) =>
            {
                try
                {
                    if (int.TryParse(value, out int parsedValue))
                    {
                        parsedValue = Mathf.Clamp(parsedValue, minValue, maxValue);
                        configEntry.Value = parsedValue;
                        slider.value = parsedValue;
                        inputField.text = parsedValue.ToString();
                        if (inputField.textComponent != null)
                        {
                            inputField.textComponent.text = parsedValue.ToString();
                        }
                        CheckForChanges(element);
                    }
                    else
                    {
                        inputField.text = configEntry.Value.ToString();
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"[UISettingsBuilder] Error in input field onEndEdit for '{configEntry.Definition.Key}': {ex.Message}");
                    inputField.text = configEntry.Value.ToString();
                }
            });

            slider.onValueChanged.AddListener((value) =>
            {
                try
                {
                    int intValue = (int)value;
                    configEntry.Value = intValue;
                    inputField.text = intValue.ToString();
                    CheckForChanges(element);
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"[UISettingsBuilder] Error in slider onValueChanged for '{configEntry.Definition.Key}': {ex.Message}");
                }
            });
        }


        /// <summary>
        /// Удалить старые методы - они больше not нужны
        /// </summary>
        // Старые методы удалены - теперь используется табличная структура
    }
}

            try
            {
                // Контейнер for всей настройки
                GameObject container = new GameObject("Setting_" + labelText.Replace(" ", "").Replace("(", "").Replace(")", "").Replace("%", ""));
                if (container == null)
                {
                    Plugin.Log?.LogError($"[UISettingsBuilder] CreateFloatSetting: Failed to create container GameObject for '{labelText}'");
                    return;
                }

                container.transform.SetParent(_content.transform, false);
                RectTransform containerRect = container.AddComponent<RectTransform>();
                containerRect.sizeDelta = new Vector2(0, 80);

                // Добавляем LayoutElement for правильного layout
                LayoutElement layoutElement = container.AddComponent<LayoutElement>();
                layoutElement.minHeight = 80;
                layoutElement.flexibleWidth = 1;

                // Border for подсветки изменений
                GameObject borderObj = new GameObject("Border");
                borderObj.transform.SetParent(container.transform, false);
                RectTransform borderRect = borderObj.AddComponent<RectTransform>();
                borderRect.anchorMin = Vector2.zero;
                borderRect.anchorMax = Vector2.one;
                borderRect.offsetMin = new Vector2(-5, -5);
                borderRect.offsetMax = new Vector2(5, 5);

                Image borderImage = borderObj.AddComponent<Image>();
                borderImage.color = new Color(1f, 1f, 1f, 0f); // Прозрачный by умолчанию, станет белым on изменении

                // Создаем горизонтальный layout for настройки
                HorizontalLayoutGroup horizontalLayout = container.AddComponent<HorizontalLayoutGroup>();
                horizontalLayout.spacing = 10;
                horizontalLayout.padding = new RectOffset(5, 5, 2, 2);
                horizontalLayout.childAlignment = TextAnchor.UpperLeft;

                // Получить описание from SettingsDataManager
                var settingDesc = SettingsDataManager.GetSettingDescription(labelText);

                // 1. Обозначение in конфиге (ключ)
                GameObject keyObj = new GameObject("Key");
                keyObj.transform.SetParent(container.transform, false);
                RectTransform keyRect = keyObj.AddComponent<RectTransform>();
                keyRect.sizeDelta = new Vector2(200, 0);

                UnityEngine.UI.Text keyText = keyObj.AddComponent<UnityEngine.UI.Text>();
                keyText.text = labelText;
                keyText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                keyText.fontSize = 12;
                keyText.alignment = TextAnchor.MiddleLeft;
                keyText.color = Color.white;
                keyText.horizontalOverflow = HorizontalWrapMode.Wrap;
                keyText.verticalOverflow = VerticalWrapMode.Truncate;

                LayoutElement keyLayout = keyObj.AddComponent<LayoutElement>();
                keyLayout.minWidth = 200;
                keyLayout.flexibleHeight = 1;

                // 2. Окbut со значением by умолчанию и редактированием
                GameObject valueContainer = new GameObject("ValueContainer");
                valueContainer.transform.SetParent(container.transform, false);
                RectTransform valueRect = valueContainer.AddComponent<RectTransform>();
                valueRect.sizeDelta = new Vector2(150, 0);

                VerticalLayoutGroup valueLayout = valueContainer.AddComponent<VerticalLayoutGroup>();
                valueLayout.spacing = 2;
                valueLayout.padding = new RectOffset(2, 2, 2, 2);

                LayoutElement valueContainerLayout = valueContainer.AddComponent<LayoutElement>();
                valueContainerLayout.minWidth = 150;
                valueContainerLayout.flexibleHeight = 1;

                // InputField for редактирования
                GameObject inputObj = new GameObject("InputField");
                inputObj.transform.SetParent(valueContainer.transform, false);
                RectTransform inputRect = inputObj.AddComponent<RectTransform>();
                inputRect.sizeDelta = new Vector2(0, 25);

                Image inputBg = inputObj.AddComponent<Image>();
                inputBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

                InputField inputField = inputObj.AddComponent<InputField>();
                inputField.textComponent = inputObj.AddComponent<UnityEngine.UI.Text>();
                inputField.textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                inputField.textComponent.fontSize = 12;
                inputField.textComponent.alignment = TextAnchor.MiddleCenter;
                inputField.textComponent.color = Color.white;
                inputField.textComponent.supportRichText = false;

                // Улучшаем InputField
                inputField.Enhance();

                LayoutElement inputLayout = inputObj.AddComponent<LayoutElement>();
                inputLayout.minHeight = 25;
                inputLayout.flexibleWidth = 1;

                // Slider под InputField
                GameObject sliderObj = new GameObject("Slider");
                sliderObj.transform.SetParent(valueContainer.transform, false);
                RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
                sliderRect.sizeDelta = new Vector2(0, 20);

                Slider slider = sliderObj.AddComponent<Slider>();
                slider.minValue = minValue;
                slider.maxValue = maxValue;

                // Background slider
                GameObject sliderBg = new GameObject("Background");
                sliderBg.transform.SetParent(sliderObj.transform, false);
                RectTransform sliderBgRect = sliderBg.AddComponent<RectTransform>();
                sliderBgRect.anchorMin = Vector2.zero;
                sliderBgRect.anchorMax = Vector2.one;
                sliderBgRect.offsetMin = new Vector2(5, 5);
                sliderBgRect.offsetMax = new Vector2(-5, -5);

                Image sliderBgImage = sliderBg.AddComponent<Image>();
                sliderBgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

                // Fill slider
                GameObject sliderFill = new GameObject("Fill");
                sliderFill.transform.SetParent(sliderBg.transform, false);
                RectTransform sliderFillRect = sliderFill.AddComponent<RectTransform>();
                sliderFillRect.anchorMin = new Vector2(0, 0);
                sliderFillRect.anchorMax = new Vector2(0, 1);
                sliderFillRect.offsetMin = Vector2.zero;
                sliderFillRect.offsetMax = Vector2.zero;

                Image sliderFillImage = sliderFill.AddComponent<Image>();
                sliderFillImage.color = new Color(0.4f, 0.6f, 1f, 1f);

                slider.fillRect = sliderFillRect;

                // Handle slider
                GameObject sliderHandle = new GameObject("Handle");
                sliderHandle.transform.SetParent(sliderObj.transform, false);
                RectTransform sliderHandleRect = sliderHandle.AddComponent<RectTransform>();
                sliderHandleRect.sizeDelta = new Vector2(20, 30);

                Image sliderHandleImage = sliderHandle.AddComponent<Image>();
                sliderHandleImage.color = Color.white;

                slider.handleRect = sliderHandleRect;

                LayoutElement sliderLayout = sliderObj.AddComponent<LayoutElement>();
                sliderLayout.minHeight = 20;
                sliderLayout.flexibleWidth = 1;

                // 3. Комментарий (значение by умолчанию)
                GameObject defaultObj = new GameObject("Default");
                defaultObj.transform.SetParent(container.transform, false);
                RectTransform defaultRect = defaultObj.AddComponent<RectTransform>();
                defaultRect.sizeDelta = new Vector2(80, 0);

                UnityEngine.UI.Text defaultText = defaultObj.AddComponent<UnityEngine.UI.Text>();
                string defaultValue = settingDesc?.defaultValue ?? "N/A";
                defaultText.text = $"Default:\n{defaultValue}";
                defaultText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                defaultText.fontSize = 10;
                defaultText.alignment = TextAnchor.MiddleCenter;
                defaultText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                defaultText.horizontalOverflow = HorizontalWrapMode.Wrap;
                defaultText.verticalOverflow = VerticalWrapMode.Truncate;

                LayoutElement defaultLayout = defaultObj.AddComponent<LayoutElement>();
                defaultLayout.minWidth = 80;
                defaultLayout.flexibleHeight = 1;

                // 4. Расширенный комментарий
                GameObject descObj = new GameObject("Description");
                descObj.transform.SetParent(container.transform, false);
                RectTransform descRect = descObj.AddComponent<RectTransform>();
                descRect.sizeDelta = new Vector2(0, 0);

                UnityEngine.UI.Text descText = descObj.AddComponent<UnityEngine.UI.Text>();
                string description = settingDesc?.description ?? "No description available";
                descText.text = description;
                descText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                descText.fontSize = 11;
                descText.alignment = TextAnchor.UpperLeft;
                descText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                descText.horizontalOverflow = HorizontalWrapMode.Wrap;
                descText.verticalOverflow = VerticalWrapMode.Truncate;
                descText.supportRichText = false;

                LayoutElement descLayout = descObj.AddComponent<LayoutElement>();
                descLayout.flexibleWidth = 1;
                descLayout.flexibleHeight = 1;

                // Устанавливаем значения
                float currentValue = configEntry.Value;
                inputField.text = currentValue.ToString($"F{decimals}");
                slider.value = currentValue;

                // Создаем SettingElement
                SettingElement element = new SettingElement
                {
                    container = container,
                    label = keyText, // Используем keyText вместо label
                    slider = slider,
                    inputField = inputField,
                    configEntry = configEntry,
                    minValue = minValue,
                    maxValue = maxValue,
                    decimals = decimals,
                    borderObj = borderObj,
                    borderImage = borderImage,
                    originalValue = currentValue,
                    isChanged = false
                };

                _settingsElements[labelText] = element;

                // Обработчики событий
                inputField.onEndEdit.AddListener((value) =>
                {
                    try
                    {
                        if (float.TryParse(value, out float parsedValue))
                        {
                            parsedValue = Mathf.Clamp(parsedValue, minValue, maxValue);
                            configEntry.Value = parsedValue;
                            slider.value = parsedValue;

                            // Форматируем текст
                            string formattedValue = parsedValue.ToString($"F{decimals}");
                            inputField.text = formattedValue;

                            // Проверяем изменения
                            CheckForChanges(element);
                        }
                        else
                        {
                            // Восстанавливаем предыдущее значение
                            inputField.text = configEntry.Value.ToString($"F{decimals}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogError($"[UISettingsBuilder] Error in input field onEndEdit for '{labelText}': {ex.Message}");
                        inputField.text = configEntry.Value.ToString($"F{decimals}");
                    }
                });

                slider.onValueChanged.AddListener((value) =>
                {
                    try
                    {
                        configEntry.Value = value;
                        inputField.text = value.ToString($"F{decimals}");
                        CheckForChanges(element);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogError($"[UISettingsBuilder] Error in slider onValueChanged for '{labelText}': {ex.Message}");
                    }
                });

                Plugin.Log?.LogInfo($"[UISettingsBuilder] Float setting '{labelText}' created");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[UISettingsBuilder] CreateFloatSetting failed for '{labelText}': {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Создать настройку типа int with InputField и Slider
        /// </summary>
        private static void CreateIntSetting(string labelText, ConfigEntry<int> configEntry, int minValue, int maxValue)
        {
            if (_content == null)
            {
                Plugin.Log?.LogError($"[UISettingsBuilder] CreateIntSetting: _content is null for '{labelText}'");
                return;
            }

            if (configEntry == null)
            {
                Plugin.Log?.LogError($"[UISettingsBuilder] CreateIntSetting: configEntry is null for '{labelText}'");
                return;
            }

            if (_content.transform == null)
            {
                Plugin.Log?.LogError($"[UISettingsBuilder] CreateIntSetting: _content.transform is null for '{labelText}'");
                return;
            }

            try
            {
                // Контейнер for всей настройки
                GameObject container = new GameObject("Setting_" + labelText.Replace(" ", "").Replace("(", "").Replace(")", "").Replace("%", ""));
                if (container == null)
                {
                    Plugin.Log?.LogError($"[UISettingsBuilder] CreateIntSetting: Failed to create container GameObject for '{labelText}'");
                    return;
                }

                container.transform.SetParent(_content.transform, false);
                RectTransform containerRect = container.AddComponent<RectTransform>();
                containerRect.sizeDelta = new Vector2(0, 80);

                // Добавляем LayoutElement for правильного layout
                LayoutElement layoutElement = container.AddComponent<LayoutElement>();
                layoutElement.minHeight = 80;
                layoutElement.flexibleWidth = 1;

                // Border for подсветки изменений
                GameObject borderObj = new GameObject("Border");
                borderObj.transform.SetParent(container.transform, false);
                RectTransform borderRect = borderObj.AddComponent<RectTransform>();
                borderRect.anchorMin = Vector2.zero;
                borderRect.anchorMax = Vector2.one;
                borderRect.offsetMin = new Vector2(-5, -5);
                borderRect.offsetMax = new Vector2(5, 5);

                Image borderImage = borderObj.AddComponent<Image>();
                borderImage.color = new Color(1f, 1f, 1f, 0f); // Прозрачный by умолчанию, станет белым on изменении

                // Создаем горизонтальный layout for настройки
                HorizontalLayoutGroup horizontalLayout = container.AddComponent<HorizontalLayoutGroup>();
                horizontalLayout.spacing = 10;
                horizontalLayout.padding = new RectOffset(5, 5, 2, 2);
                horizontalLayout.childAlignment = TextAnchor.UpperLeft;

                // Получить описание from SettingsDataManager
                var settingDesc = SettingsDataManager.GetSettingDescription(labelText);

                // 1. Обозначение in конфиге (ключ)
                GameObject keyObj = new GameObject("Key");
                keyObj.transform.SetParent(container.transform, false);
                RectTransform keyRect = keyObj.AddComponent<RectTransform>();
                keyRect.sizeDelta = new Vector2(200, 0);

                UnityEngine.UI.Text keyText = keyObj.AddComponent<UnityEngine.UI.Text>();
                keyText.text = labelText;
                keyText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                keyText.fontSize = 12;
                keyText.alignment = TextAnchor.MiddleLeft;
                keyText.color = Color.white;
                keyText.horizontalOverflow = HorizontalWrapMode.Wrap;
                keyText.verticalOverflow = VerticalWrapMode.Truncate;

                LayoutElement keyLayout = keyObj.AddComponent<LayoutElement>();
                keyLayout.minWidth = 200;
                keyLayout.flexibleHeight = 1;

                // 2. Окbut со значением by умолчанию и редактированием
                GameObject valueContainer = new GameObject("ValueContainer");
                valueContainer.transform.SetParent(container.transform, false);
                RectTransform valueRect = valueContainer.AddComponent<RectTransform>();
                valueRect.sizeDelta = new Vector2(150, 0);

                VerticalLayoutGroup valueLayout = valueContainer.AddComponent<VerticalLayoutGroup>();
                valueLayout.spacing = 2;
                valueLayout.padding = new RectOffset(2, 2, 2, 2);

                LayoutElement valueContainerLayout = valueContainer.AddComponent<LayoutElement>();
                valueContainerLayout.minWidth = 150;
                valueContainerLayout.flexibleHeight = 1;

                // InputField for редактирования
                GameObject inputObj = new GameObject("InputField");
                inputObj.transform.SetParent(valueContainer.transform, false);
                RectTransform inputRect = inputObj.AddComponent<RectTransform>();
                inputRect.sizeDelta = new Vector2(0, 25);

                Image inputBg = inputObj.AddComponent<Image>();
                inputBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

                InputField inputField = inputObj.AddComponent<InputField>();
                inputField.textComponent = inputObj.AddComponent<UnityEngine.UI.Text>();
                inputField.textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                inputField.textComponent.fontSize = 12;
                inputField.textComponent.alignment = TextAnchor.MiddleCenter;
                inputField.textComponent.color = Color.white;
                inputField.textComponent.supportRichText = false;

                // Улучшаем InputField
                inputField.Enhance();

                LayoutElement inputLayout = inputObj.AddComponent<LayoutElement>();
                inputLayout.minHeight = 25;
                inputLayout.flexibleWidth = 1;

                // Slider под InputField
                GameObject sliderObj = new GameObject("Slider");
                sliderObj.transform.SetParent(valueContainer.transform, false);
                RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
                sliderRect.sizeDelta = new Vector2(0, 20);

                Slider slider = sliderObj.AddComponent<Slider>();
                slider.minValue = minValue;
                slider.maxValue = maxValue;
                slider.wholeNumbers = true; // Только целые числа

                // Background slider
                GameObject sliderBg = new GameObject("Background");
                sliderBg.transform.SetParent(sliderObj.transform, false);
                RectTransform sliderBgRect = sliderBg.AddComponent<RectTransform>();
                sliderBgRect.anchorMin = Vector2.zero;
                sliderBgRect.anchorMax = Vector2.one;
                sliderBgRect.offsetMin = new Vector2(5, 5);
                sliderBgRect.offsetMax = new Vector2(-5, -5);

                Image sliderBgImage = sliderBg.AddComponent<Image>();
                sliderBgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

                // Fill slider
                GameObject sliderFill = new GameObject("Fill");
                sliderFill.transform.SetParent(sliderBg.transform, false);
                RectTransform sliderFillRect = sliderFill.AddComponent<RectTransform>();
                sliderFillRect.anchorMin = new Vector2(0, 0);
                sliderFillRect.anchorMax = new Vector2(0, 1);
                sliderFillRect.offsetMin = Vector2.zero;
                sliderFillRect.offsetMax = Vector2.zero;

                Image sliderFillImage = sliderFill.AddComponent<Image>();
                sliderFillImage.color = new Color(0.4f, 0.6f, 1f, 1f);

                slider.fillRect = sliderFillRect;

                // Handle slider
                GameObject sliderHandle = new GameObject("Handle");
                sliderHandle.transform.SetParent(sliderObj.transform, false);
                RectTransform sliderHandleRect = sliderHandle.AddComponent<RectTransform>();
                sliderHandleRect.sizeDelta = new Vector2(20, 30);

                Image sliderHandleImage = sliderHandle.AddComponent<Image>();
                sliderHandleImage.color = Color.white;

                slider.handleRect = sliderHandleRect;

                LayoutElement sliderLayout = sliderObj.AddComponent<LayoutElement>();
                sliderLayout.minHeight = 20;
                sliderLayout.flexibleWidth = 1;

                // 3. Комментарий (значение by умолчанию)
                GameObject defaultObj = new GameObject("Default");
                defaultObj.transform.SetParent(container.transform, false);
                RectTransform defaultRect = defaultObj.AddComponent<RectTransform>();
                defaultRect.sizeDelta = new Vector2(80, 0);

                UnityEngine.UI.Text defaultText = defaultObj.AddComponent<UnityEngine.UI.Text>();
                string defaultValue = settingDesc?.defaultValue ?? "N/A";
                defaultText.text = $"Default:\n{defaultValue}";
                defaultText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                defaultText.fontSize = 10;
                defaultText.alignment = TextAnchor.MiddleCenter;
                defaultText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                defaultText.horizontalOverflow = HorizontalWrapMode.Wrap;
                defaultText.verticalOverflow = VerticalWrapMode.Truncate;

                LayoutElement defaultLayout = defaultObj.AddComponent<LayoutElement>();
                defaultLayout.minWidth = 80;
                defaultLayout.flexibleHeight = 1;

                // 4. Расширенный комментарий
                GameObject descObj = new GameObject("Description");
                descObj.transform.SetParent(container.transform, false);
                RectTransform descRect = descObj.AddComponent<RectTransform>();
                descRect.sizeDelta = new Vector2(0, 0);

                UnityEngine.UI.Text descText = descObj.AddComponent<UnityEngine.UI.Text>();
                string description = settingDesc?.description ?? "No description available";
                descText.text = description;
                descText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                descText.fontSize = 11;
                descText.alignment = TextAnchor.UpperLeft;
                descText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                descText.horizontalOverflow = HorizontalWrapMode.Wrap;
                descText.verticalOverflow = VerticalWrapMode.Truncate;
                descText.supportRichText = false;

                LayoutElement descLayout = descObj.AddComponent<LayoutElement>();
                descLayout.flexibleWidth = 1;
                descLayout.flexibleHeight = 1;

                // Устанавливаем значения
                int currentValue = configEntry.Value;
                inputField.text = currentValue.ToString();
                slider.value = currentValue;

                // Создаем SettingElement
                SettingElement element = new SettingElement
                {
                    container = container,
                    label = keyText, // Используем keyText вместо label
                    slider = slider,
                    inputField = inputField,
                    configEntryInt = configEntry,
                    minValue = minValue,
                    maxValue = maxValue,
                    decimals = 0,
                    borderObj = borderObj,
                    borderImage = borderImage,
                    originalValue = currentValue,
                    isChanged = false
                };

                _settingsElements[labelText] = element;

                // Обработчики событий
                inputField.onEndEdit.AddListener((value) =>
                {
                    try
                    {
                        if (int.TryParse(value, out int parsedValue))
                        {
                            parsedValue = Mathf.Clamp(parsedValue, minValue, maxValue);
                            configEntry.Value = parsedValue;
                            slider.value = parsedValue;

                            // Проверяем изменения
                            CheckForChanges(element);
                        }
                        else
                        {
                            // Восстанавливаем предыдущее значение
                            inputField.text = configEntry.Value.ToString();
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogError($"[UISettingsBuilder] Error in input field onEndEdit for '{labelText}': {ex.Message}");
                        inputField.text = configEntry.Value.ToString();
                    }
                });

                slider.onValueChanged.AddListener((value) =>
                {
                    try
                    {
                        int intValue = (int)value;
                        configEntry.Value = intValue;
                        inputField.text = intValue.ToString();
                        CheckForChanges(element);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogError($"[UISettingsBuilder] Error in slider onValueChanged for '{labelText}': {ex.Message}");
                    }
                });

                Plugin.Log?.LogInfo($"[UISettingsBuilder] Int setting '{labelText}' created");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[UISettingsBuilder] CreateIntSetting failed for '{labelText}': {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Проверить изменения значения настройки
        /// </summary>
        private static void CheckForChanges(SettingElement element)
        {
            if (element == null) return;

            float currentValue;
            if (element.configEntry != null)
            {
                currentValue = element.configEntry.Value;
            }
            else if (element.configEntryInt != null)
            {
                currentValue = element.configEntryInt.Value;
            }
            else
            {
                return;
            }

            bool wasChanged = element.isChanged;
            element.isChanged = !Mathf.Approximately(currentValue, element.originalValue);

            // Обновляем визуализацию only if статуwith изменился
            if (wasChanged != element.isChanged && element.borderImage != null)
            {
                if (element.isChanged)
                {
                    // Белая подсветка on изменении
                    element.borderImage.color = new Color(1f, 1f, 1f, 0.3f);
                }
                else
                {
                    // Прозрачная when нет изменений
                    element.borderImage.color = new Color(1f, 1f, 1f, 0f);
                }
            }
        }

        /// <summary>
        /// Обновить интерактивность всех настроек depending on состояния QTE системы
        /// </summary>
        private static void UpdateSettingsInteractability(bool isEnabled)
        {
            foreach (var element in _settingsElements.Values)
            {
                if (element.slider != null)
                {
                    element.slider.interactable = isEnabled;
                }
                if (element.inputField != null)
                {
                    element.inputField.interactable = isEnabled;
                }
            }
        }

    }
}
