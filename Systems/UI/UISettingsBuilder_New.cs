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
        private static Dictionary<string, SettingElement> _settingsElements = new();

        /// <summary>
        /// Структура for хранения элементоin настройки
        /// </summary>
        public class SettingElement
        {
            public GameObject? container;
            public UnityEngine.UI.Text? label;
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
        public static Toggle? GetQTESystemToggle() => null; // Убрали toggle

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
            canvas.sortingOrder = 32767; // Максимальный приоритет

            CanvasScaler scaler = _canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _canvas.AddComponent<GraphicRaycaster>();
            UnityEngine.Object.DontDestroyOnLoad(_canvas);
            _canvas.SetActive(true); // Активируем Canvas сразу

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
            windowImage.raycastTarget = false; // Отключаем raycast

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
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
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

            // 3. Value (пока просто текст)
            CreateValueCell(configEntry, minValue, maxValue, decimals, 120);

            // 4. Default Value
            string defaultValue = settingDesc?.defaultValue ?? "N/A";
            CreateTableCell(defaultValue, 80, TextAnchor.MiddleCenter);

            // 5. Comment (пока пустой)
            CreateTableCell("", 120, TextAnchor.MiddleLeft);

            // 6. Description
            string description = settingDesc?.description ?? "No description available";
            CreateTableCell(description, 0, TextAnchor.MiddleLeft, true);
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

            // 3. Value (пока просто текст)
            CreateValueCell(configEntry, minValue, maxValue, 120);

            // 4. Default Value
            string defaultValue = settingDesc?.defaultValue ?? "N/A";
            CreateTableCell(defaultValue, 80, TextAnchor.MiddleCenter);

            // 5. Comment (пока пустой)
            CreateTableCell("", 120, TextAnchor.MiddleLeft);

            // 6. Description
            string description = settingDesc?.description ?? "No description available";
            CreateTableCell(description, 0, TextAnchor.MiddleLeft, true);
        }

        /// <summary>
        /// Создать ячейку таблицы
        /// </summary>
        private static void CreateTableCell(string text, float width, TextAnchor alignment, bool flexible = false)
        {
            GameObject cellObj = new GameObject("Cell");
            cellObj.transform.SetParent(_content.transform.GetChild(_content.transform.childCount - 1), false);

            RectTransform cellRect = cellObj.AddComponent<RectTransform>();
            cellRect.sizeDelta = new Vector2(width, 0);

            LayoutElement cellLayoutElement = cellObj.AddComponent<LayoutElement>();
            cellLayoutElement.minWidth = width;
            if (flexible)
            {
                cellLayoutElement.flexibleWidth = 1;
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
        }

        /// <summary>
        /// Создать ячейку со значением (пока просто текст)
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
            valueText.text = configEntry.Value.ToString($"F{decimals}");
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
            containerRect.offsetMax = new Vector2(0, 150);

            HorizontalLayoutGroup layoutGroup = buttonsContainer.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = 50;
            layoutGroup.padding = new RectOffset(100, 100, 50, 20);
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            // Создаем три кнопки with анимацией
            CreateAnimatedButton("Apply", () => SettingsValueManager.ApplySettings());
            CreateAnimatedButton("Reset", () => SettingsValueManager.ResetToDefaults());
            CreateAnimatedButton("Close", () => GameSettingsMenu.HideSettings());

            Plugin.Log?.LogInfo("[UISettingsBuilder] Bottom buttons created");
        }

        /// <summary>
        /// Создать анимированную кнопку
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

            // Настраиваем цвета кнопки for hover эффекта
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
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

            UnityEngine.UI.Text buttonText = textObj.AddComponent<UnityEngine.UI.Text>();
            buttonText.text = text;
            buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            buttonText.fontSize = 28;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.fontStyle = FontStyle.Bold;
            buttonText.color = Color.white;
            buttonText.supportRichText = false;

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
    }
}
