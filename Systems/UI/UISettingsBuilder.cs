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
            // Заголовок in верхней части экраon (выше таблицы)
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(0, -120); // 120px высота заголовка from верхнits края
            titleRect.offsetMax = new Vector2(0, 0);

            UnityEngine.UI.Text titleText = titleObj.AddComponent<UnityEngine.UI.Text>();
            titleText.text = "HELLGATE QTE SETTINGS";
            titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); // Как in MindBroken for кириллицы
            titleText.fontSize = 51; // Увеличен on 3
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.fontStyle = FontStyle.Bold;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleText.verticalOverflow = VerticalWrapMode.Truncate;
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
            // Таблица on весь экран by ширине, поднята выше: from 100px сверху until 150px снизу
            scrollRect.anchorMin = new Vector2(0, 0);
            scrollRect.anchorMax = new Vector2(1, 1);
            scrollRect.offsetMin = new Vector2(0, 150); // 0px слева, 150px снизу (подняли on 50px)
            scrollRect.offsetMax = new Vector2(0, -100); // 0px справа, 100px сверху (подняли on 50px)

            ScrollRect scrollComponent = _scrollView.AddComponent<ScrollRect>();
            scrollComponent.scrollSensitivity = 100f; // Увеличеon скорость скролла for устранения подклинивания
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
            viewportRect.offsetMax = Vector2.zero; // Убрали отступ for скроллбара

            viewport.AddComponent<Mask>();
            scrollComponent.viewport = viewportRect;

            // Content - таблица настроек
            _content = new GameObject("SettingsTable");
            _content.transform.SetParent(viewport.transform, false);

            RectTransform contentRect = _content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1); // Верхний левый угол Viewport
            contentRect.anchorMax = new Vector2(1, 1); // Верхний правый угол Viewport
            contentRect.pivot = new Vector2(0.5f, 1); // Центр by X, верх by Y
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup layoutGroup = _content.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 2; // Небольшой отступ между строками
            layoutGroup.padding = new RectOffset(5, 5, 5, 5);
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.childControlWidth = true;     // Контролируем ширину строк - растягиваем on всю ширину
            layoutGroup.childControlHeight = false;    // Не контролируем высоту строк
            layoutGroup.childForceExpandWidth = true;  // Растягиваем строки on всю ширину
            layoutGroup.childForceExpandHeight = false; // Не расширяем by высоте

            // ContentSizeFitter for автоматического расчета высоты Content on основе дочернtheir элементов
            ContentSizeFitter sizeFitter = _content.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; // Шириon контролируется anchor

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
            CreateHeaderCell("Comment", 0); // Flexible width - занимает всё оставшееся место
        }


        /// <summary>
        /// Создать главный тумблер QTE System
        /// </summary>
        private static void CreateQTESystemToggle()
        {
            if (_content == null) return;

            GameObject toggleObj = new GameObject("QTE_Enable_Toggle");
            toggleObj.transform.SetParent(_content.transform, false);

            RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
            // Anchor и pivot будут установлены автоматически VerticalLayoutGroup
            toggleRect.sizeDelta = new Vector2(0, 60); // Высокая строка for главного тумблера

            LayoutElement toggleRowLayout = toggleObj.AddComponent<LayoutElement>();
            toggleRowLayout.minHeight = 60;
            toggleRowLayout.preferredHeight = 60;
            toggleRowLayout.flexibleWidth = 1; // Растягиваем on всю ширину Content

            HorizontalLayoutGroup toggleLayout = toggleObj.AddComponent<HorizontalLayoutGroup>();
            toggleLayout.spacing = 20;
            toggleLayout.padding = new RectOffset(20, 20, 10, 10);
            toggleLayout.childAlignment = TextAnchor.MiddleLeft;
            toggleLayout.childControlWidth = false;
            toggleLayout.childControlHeight = true;
            toggleLayout.childForceExpandWidth = false;
            toggleLayout.childForceExpandHeight = false;

            // Заголовок "QTE SYSTEM ENABLE"
            GameObject labelObj = new GameObject("QTE_Enable_Label");
            labelObj.transform.SetParent(toggleObj.transform, false);

            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(400, 0);

            UnityEngine.UI.Text labelText = labelObj.AddComponent<UnityEngine.UI.Text>();
            labelText.text = "QTE SYSTEM ENABLE";
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.fontSize = 24; // Крупный шрифт for главного заголовка
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.color = Color.yellow; // Выделить цветом
            labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            labelText.verticalOverflow = VerticalWrapMode.Truncate;

            // Тумблер
            GameObject toggleComponentObj = new GameObject("QTE_Enable_ToggleComponent");
            toggleComponentObj.transform.SetParent(toggleObj.transform, false);

            RectTransform toggleComponentRect = toggleComponentObj.AddComponent<RectTransform>();
            toggleComponentRect.sizeDelta = new Vector2(60, 30);

            LayoutElement toggleLayoutElement = toggleComponentObj.AddComponent<LayoutElement>();
            toggleLayoutElement.minWidth = 60;
            toggleLayoutElement.preferredWidth = 60;
            toggleLayoutElement.minHeight = 30;
            toggleLayoutElement.preferredHeight = 30;

            UnityEngine.UI.Toggle toggle = toggleComponentObj.AddComponent<UnityEngine.UI.Toggle>();
            toggle.isOn = Plugin.enableQTESystem.Value;

            // Background
            GameObject background = new GameObject("Background");
            background.transform.SetParent(toggleComponentObj.transform, false);
            UnityEngine.UI.Image bgImage = background.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            RectTransform bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // Checkmark
            GameObject checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(toggleComponentObj.transform, false);
            UnityEngine.UI.Image checkImage = checkmark.AddComponent<UnityEngine.UI.Image>();
            checkImage.color = Color.green;
            toggle.graphic = checkImage;
            RectTransform checkRect = checkmark.GetComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.sizeDelta = new Vector2(-8, -8);

            // Обработчик изменения
            toggle.onValueChanged.AddListener((value) =>
            {
                Plugin.enableQTESystem.Value = value;
                Plugin.Instance.Config.Save();
                Plugin.Log?.LogInfo($"[QTE Settings] QTE System {(value ? "ENABLED" : "DISABLED")}");
            });
        }

        /// <summary>
        /// Создать заголовок категории
        /// </summary>
        private static void CreateCategoryHeader(string categoryName)
        {
            if (_content == null) return;

            GameObject headerObj = new GameObject("Category_" + categoryName.Replace(" ", "_"));
            headerObj.transform.SetParent(_content.transform, false);

            RectTransform headerRect = headerObj.AddComponent<RectTransform>();
            // Anchor и pivot будут установлены автоматически VerticalLayoutGroup
            headerRect.sizeDelta = new Vector2(0, 50); // Высокая строка for заголовка категории

            LayoutElement headerRowLayout = headerObj.AddComponent<LayoutElement>();
            headerRowLayout.minHeight = 50;
            headerRowLayout.preferredHeight = 50;
            headerRowLayout.flexibleWidth = 1; // Растягиваем on всю ширину Content

            HorizontalLayoutGroup headerLayout = headerObj.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 10;
            headerLayout.padding = new RectOffset(10, 10, 5, 5);
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = false;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;

            // Текст заголовка категории
            GameObject textObj = new GameObject("CategoryText");
            textObj.transform.SetParent(headerObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(0, 0); // Flexible width

            LayoutElement textLayout = textObj.AddComponent<LayoutElement>();
            textLayout.flexibleWidth = 1;
            textLayout.minHeight = 35;
            textLayout.preferredHeight = 35;

            UnityEngine.UI.Text headerText = textObj.AddComponent<UnityEngine.UI.Text>();
            headerText.text = categoryName;
            headerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            headerText.fontSize = 20; // Крупный шрифт for категории
            headerText.fontStyle = FontStyle.Bold;
            headerText.alignment = TextAnchor.MiddleLeft;
            headerText.color = new Color(0.8f, 0.8f, 1f); // Светло-синий for заголовкоin категорий
            headerText.horizontalOverflow = HorizontalWrapMode.Wrap;
            headerText.verticalOverflow = VerticalWrapMode.Truncate;
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
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); // Как in MindBroken for кириллицы
            textComponent.fontSize = 17; // Увеличен on 3
            textComponent.fontStyle = FontStyle.Bold;
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.color = new Color(1f, 0.8f, 0f); // Золотистый
            textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComponent.verticalOverflow = VerticalWrapMode.Truncate;

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
                // ГЛАВНЫЙ ТУМБЛЕР QTE SYSTEM
                CreateQTESystemToggle();

                // КАТЕГОРИЯ: SP GAIN SETTINGS
                CreateCategoryHeader("SP GAIN SETTINGS");
                CreateTableRow("", "SP Gain Base (0% MB)", Plugin.qteSPGainBase, 0.1f, 5.0f, 3);
                CreateTableRow("", "SP Gain Min (100% MB)", Plugin.qteSPGainMin, 0.1f, 2.0f, 3);
                CreateTableRow("", "Yellow Button SP Gain Min", Plugin.qteYellowButtonSPGainMin, 1.0f, 10.0f, 1);
                CreateTableRow("", "Yellow Button SP Gain Max", Plugin.qteYellowButtonSPGainMax, 5.0f, 30.0f, 1);

                // КАТЕГОРИЯ: PENALTY SETTINGS
                CreateCategoryHeader("PENALTY SETTINGS");
                CreateTableRow("", "MP Penalty Percent", Plugin.qteMPPenaltyPercent, 10.0f, 50.0f, 1);
                CreateTableRow("", "MindBroken Penalty Percent", Plugin.qteMindBrokenPenaltyPercent, 0.5f, 5.0f, 1);
                CreateTableRow("", "Red Button MB Penalty", Plugin.qteRedButtonMindBrokenPenalty, 1.0f, 10.0f, 1);
                CreateTableRow("", "SP Penalty Multiplier", Plugin.qteSPPenaltyMultiplier, 1.0f, 5.0f, 1);

                // КАТЕГОРИЯ: TIMER SETTINGS
                CreateCategoryHeader("TIMER SETTINGS");
                CreateTableRow("", "Window Duration Min", Plugin.qteWindowDurationMin, 1.0f, 5.0f, 1);
                CreateTableRow("", "Window Duration Max", Plugin.qteWindowDurationMax, 2.0f, 6.0f, 1);
                CreateTableRow("", "Cooldown Duration Min", Plugin.qteCooldownDurationMin, 1.0f, 5.0f, 1);
                CreateTableRow("", "Cooldown Duration Max", Plugin.qteCooldownDurationMax, 2.0f, 8.0f, 1);

                // КАТЕГОРИЯ: VISUAL EFFECTS
                CreateCategoryHeader("VISUAL EFFECTS");
                CreateTableRow("", "Rotation Speed", Plugin.qteRotationSpeed, 30.0f, 180.0f, 0);
                CreateTableRow("", "Rotation Start Time", Plugin.qteRotationStartTime, 0.1f, 2.0f, 1);
                CreateTableRow("", "Color Change Interval", Plugin.qteColorChangeInterval, 0.5f, 3.0f, 1);
                CreateTableRow("", "Press Indicator Duration", Plugin.qtePressIndicatorDuration, 0.05f, 0.5f, 2);
                CreateTableRow("", "Max Button Transparency", Plugin.qteMaxButtonTransparency, 0.0f, 1.0f, 2);
                CreateTableRow("", "Max Pink Shadow Intensity", Plugin.qteMaxPinkShadowIntensity, 0.0f, 2.0f, 1);

                // КАТЕГОРИЯ: SOUND SETTINGS
                CreateCategoryHeader("SOUND SETTINGS");
                CreateTableRow("", "Success Volume Multiplier", Plugin.qteSuccessVolumeMultiplier, 0.0f, 2.0f, 1);
                CreateTableRow("", "Failure Volume Multiplier", Plugin.qteFailureVolumeMultiplier, 0.0f, 2.0f, 1);

                // КАТЕГОРИЯ: COMBO SETTINGS
                CreateCategoryHeader("COMBO SETTINGS");
                CreateTableRow("", "Combo Milestone", Plugin.qteComboMilestone, 5, 50);

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
            // Anchor и pivot будут установлены автоматически VerticalLayoutGroup
            rowRect.sizeDelta = new Vector2(0, 40); // Фиксированная высота строки

            // Добавляем LayoutElement with фиксированной высотой и flexible шириной
            LayoutElement rowLayoutElement = rowObj.AddComponent<LayoutElement>();
            rowLayoutElement.minHeight = 40;
            rowLayoutElement.preferredHeight = 40;
            rowLayoutElement.flexibleWidth = 1; // Растягиваем on всю ширину Content

            HorizontalLayoutGroup rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 15; // Увеличим spacing for лучшits разделения колонок
            rowLayout.padding = new RectOffset(10, 10, 5, 5);
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = false; // Отключаем контроль ширины - используем LayoutElement
            rowLayout.childControlHeight = false; // Отключаем контроль высоты - используем LayoutElement
            rowLayout.childForceExpandWidth = false; // Не заставляем расширяться
            rowLayout.childForceExpandHeight = false;

            // Получить описание from SettingsDataManager
            var settingDesc = SettingsDataManager.GetSettingDescription(settingName);

            // 1. Section
            CreateTableCell(section, 150, TextAnchor.MiddleCenter);

            // 2. Setting Name
            CreateTableCell(settingName, 250, TextAnchor.MiddleLeft); // Увеличеon ширина

            // 3. Value (пока просто текст)
            CreateValueCell(configEntry, minValue, maxValue, decimals, 120);

            // 4. Default Value
            string defaultValue = settingDesc?.defaultValue ?? "N/A";
            CreateTableCell(defaultValue, 80, TextAnchor.MiddleCenter);

            // 5. Comment (теперь содержит description)
            string description = settingDesc?.description ?? "No description available";
            CreateTableCell(description, 0, TextAnchor.UpperLeft, true);
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
            // Anchor и pivot будут установлены автоматически VerticalLayoutGroup
            rowRect.sizeDelta = new Vector2(0, 40); // Фиксированная высота строки

            // Добавляем LayoutElement with фиксированной высотой и flexible шириной
            LayoutElement rowLayoutElement = rowObj.AddComponent<LayoutElement>();
            rowLayoutElement.minHeight = 40;
            rowLayoutElement.preferredHeight = 40;
            rowLayoutElement.flexibleWidth = 1; // Растягиваем on всю ширину Content

            HorizontalLayoutGroup rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 15; // Увеличим spacing for лучшits разделения колонок
            rowLayout.padding = new RectOffset(10, 10, 5, 5);
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = false; // Отключаем контроль ширины - используем LayoutElement
            rowLayout.childControlHeight = false; // Отключаем контроль высоты - используем LayoutElement
            rowLayout.childForceExpandWidth = false; // Не заставляем расширяться
            rowLayout.childForceExpandHeight = false;

            // Получить описание from SettingsDataManager
            var settingDesc = SettingsDataManager.GetSettingDescription(settingName);

            // 1. Section
            CreateTableCell(section, 150, TextAnchor.MiddleCenter);

            // 2. Setting Name
            CreateTableCell(settingName, 250, TextAnchor.MiddleLeft); // Увеличеon ширина

            // 3. Value (пока просто текст)
            CreateValueCell(configEntry, minValue, maxValue, 120);

            // 4. Default Value
            string defaultValue = settingDesc?.defaultValue ?? "N/A";
            CreateTableCell(defaultValue, 80, TextAnchor.MiddleCenter);

            // 5. Comment (теперь содержит description)
            string description = settingDesc?.description ?? "No description available";
            CreateTableCell(description, 0, TextAnchor.UpperLeft, true);
        }

        /// <summary>
        /// Создать ячейку таблицы
        /// </summary>
        private static void CreateTableCell(string text, float width, TextAnchor alignment, bool flexible = false)
        {
            Plugin.Log?.LogInfo($"[UISettingsBuilder] Creating cell with text: '{text.Replace("\n", "\\n")}', flexible: {flexible}");

            GameObject cellObj = new GameObject("Cell");
            cellObj.transform.SetParent(_content.transform.GetChild(_content.transform.childCount - 1), false);

            RectTransform cellRect = cellObj.AddComponent<RectTransform>();
            cellRect.sizeDelta = new Vector2(width, 0);

            // Настройка размероin ячейки через LayoutElement
            LayoutElement cellLayoutElement = cellObj.AddComponent<LayoutElement>();
            if (flexible)
            {
                // Для flexible ячеек - flexible ширина, фиксированная высота
                cellLayoutElement.flexibleWidth = 1;
                cellLayoutElement.minHeight = 35;
                cellLayoutElement.preferredHeight = 35;
            }
            else if (width > 0)
            {
                // Для не-flexible ячеек устанавливаем фиксированную ширину и высоту
                cellLayoutElement.minWidth = width;
                cellLayoutElement.preferredWidth = width;
                cellLayoutElement.minHeight = 35;
                cellLayoutElement.preferredHeight = 35;
            }

            UnityEngine.UI.Text textComponent = cellObj.AddComponent<UnityEngine.UI.Text>();
            textComponent.text = text;
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); // Как in MindBroken for кириллицы
            textComponent.fontSize = 18; // Увеличен on 6 (еще +3)
            textComponent.alignment = alignment;
            textComponent.color = Color.white;
            textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComponent.verticalOverflow = VerticalWrapMode.Truncate;

            // Для многострочного текста in comment колонке - выравнивание by верхнему краю
            if (flexible && text.Contains("\n"))
            {
                textComponent.alignment = TextAnchor.UpperLeft;
            }
            else
            {
                textComponent.alignment = alignment; // Используем переданное выравнивание
            }

            Plugin.Log?.LogInfo($"[UISettingsBuilder] TextMeshPro component created: alignment={textComponent.alignment}, text.Length={textComponent.text.Length}, hasNewlines={textComponent.text.Contains("\\n")}");
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
            valueText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); // Как in MindBroken for кириллицы
            valueText.fontSize = 15; // Увеличен on 3
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.color = Color.white;
            valueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            valueText.verticalOverflow = VerticalWrapMode.Truncate;
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
            valueText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); // Как in MindBroken for кириллицы
            valueText.fontSize = 15; // Увеличен on 3
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.color = Color.white;
            valueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            valueText.verticalOverflow = VerticalWrapMode.Truncate;
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
            // Кнопки in нижней части экраon (ниже таблицы)
            containerRect.anchorMin = new Vector2(0, 0);
            containerRect.anchorMax = new Vector2(1, 0);
            containerRect.offsetMin = new Vector2(0, 0);
            containerRect.offsetMax = new Vector2(0, 150); // 150px высота for кнопок from нижнits края

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
            buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); // Как in MindBroken for кириллицы
            buttonText.fontSize = 31; // Увеличен on 3
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.fontStyle = FontStyle.Bold;
            buttonText.color = Color.white;
            buttonText.horizontalOverflow = HorizontalWrapMode.Wrap;
            buttonText.verticalOverflow = VerticalWrapMode.Truncate;

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
