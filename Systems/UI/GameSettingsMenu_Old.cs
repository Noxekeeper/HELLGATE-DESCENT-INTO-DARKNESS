using System;
using UnityEngine;
using BepInEx.Configuration;

namespace NoREroMod.Systems.UI;

/// <summary>
/// Типы кнопок для звуков
/// </summary>
public enum ButtonType
{
    ApplyClose,
    Reset
}

/// <summary>
/// GameSettingsMenu - Система настроек QTE в главном меню игры
/// </summary>
internal static class GameSettingsMenu
{
    private static bool _isInitialized = false;
    
    /// <summary>
    /// Инициализировать систему настроек
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

    
                    int maxColonPos = jsonContent.IndexOf(':', maxStart + 10);
                    if (maxColonPos != -1)
                    {
    
    /// <summary>
    /// Показать окно настроек
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

            // Убеждаемся, что Canvas активен
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

                // Убеждаемся, что Canvas активен перед показом окна
                canvas.SetActive(true);

                // Убеждаемся, что окно активно
                settingsWindow.SetActive(true);

                // Убеждаемся, что окно находится поверх других элементов
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
    /// Скрыть окно настроек
    /// </summary>
    public static void HideSettings()
    {
        var settingsWindow = UISettingsBuilder.GetSettingsWindow();
        if (settingsWindow != null)
        {
            settingsWindow.SetActive(false);
        }
    }
    
    
    /// <summary>
    /// Создать модальное окно настроек
    /// </summary>
    private static void CreateSettingsWindow()
    {
        if (_canvas == null)
        {
            Plugin.Log?.LogError("[GameSettingsMenu] Cannot create settings window: Canvas is null");
            return;
        }
        
        Plugin.Log?.LogInfo("[GameSettingsMenu] Creating settings window...");
        
        // Основная панель (фон окна)
        _settingsWindow = new GameObject("SettingsWindow");
        _settingsWindow.transform.SetParent(_canvas.transform, false);
        
        RectTransform windowRect = _settingsWindow.AddComponent<RectTransform>();
        windowRect.anchorMin = Vector2.zero;
        windowRect.anchorMax = Vector2.one;
        windowRect.sizeDelta = Vector2.zero;
        windowRect.anchoredPosition = Vector2.zero;
        
        Image windowImage = _settingsWindow.AddComponent<Image>();
        windowImage.color = new Color(0f, 0f, 0f, 0.9f); // Полупрозрачный черный фон
        
        // Заголовок
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(_settingsWindow.transform, false);
        Text headerText = headerObj.AddComponent<Text>();
        headerText.text = "HELLGATE Settings";
        headerText.fontSize = 32;
        headerText.alignment = TextAnchor.MiddleCenter;
        headerText.fontStyle = FontStyle.Bold;
        headerText.color = Color.white;
        headerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0.5f, 1f);
        headerRect.anchorMax = new Vector2(0.5f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -20f);
        headerRect.sizeDelta = new Vector2(800f, 60f);
        
        // ScrollView
        CreateScrollView();
        
        // Footer с кнопками
        CreateFooter();
        
        // Изначально скрываем окно
        _settingsWindow.SetActive(false);
        
        Plugin.Log?.LogInfo($"[GameSettingsMenu] Settings window created successfully. Active: {_settingsWindow.activeSelf}");
    }
    
    /// <summary>
    /// Создать ScrollView с Content
    /// </summary>
    private static void CreateScrollView()
    {
        if (_settingsWindow == null)
        {
            return;
        }
        
        // ScrollView контейнер
        _scrollView = new GameObject("ScrollView");
        _scrollView.transform.SetParent(_settingsWindow.transform, false);
        
        RectTransform scrollRect = _scrollView.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f); // Растягиваем на весь экран
        scrollRect.anchorMax = new Vector2(1f, 1f); // Растягиваем на весь экран
        scrollRect.pivot = new Vector2(0.5f, 0.5f);
        scrollRect.offsetMin = new Vector2(30f, 80f); // Меньше отступы - больше места
        scrollRect.offsetMax = new Vector2(-30f, -80f); // Меньше отступы - больше места
        
        ScrollRect scrollRectComponent = _scrollView.AddComponent<ScrollRect>();
        scrollRectComponent.horizontal = false;
        scrollRectComponent.vertical = true;
        scrollRectComponent.scrollSensitivity = 50f; // Увеличиваем скорость скролла
        
        // Viewport
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(_scrollView.transform, false);
        RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.anchoredPosition = Vector2.zero;
        
        Image viewportImage = viewportObj.AddComponent<Image>();
        viewportImage.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
        
        Mask mask = viewportObj.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        
        scrollRectComponent.viewport = viewportRect;
        
        // Content - привязываем к ЛЕВОМУ КРАЮ ЭКРАНА
        _content = new GameObject("Content");
        _content.transform.SetParent(viewportObj.transform, false);
        RectTransform contentRect = _content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f); // Верхний левый угол
        contentRect.anchorMax = new Vector2(0f, 1f); // Верхний левый угол
        contentRect.pivot = new Vector2(0f, 1f); // Pivot в верхнем левом углу
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(900f, 100f); // Ширина для левой части экрана
        
        VerticalLayoutGroup contentLayout = _content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 15f;
        contentLayout.padding = new RectOffset(30, 30, 30, 30); // Больше паддинга слева
        contentLayout.childControlWidth = false; // Используем LayoutElement
        contentLayout.childControlHeight = false; // Используем LayoutElement
        contentLayout.childForceExpandWidth = false;
        contentLayout.childForceExpandHeight = false;
        
        ContentSizeFitter contentFitter = _content.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        scrollRectComponent.content = contentRect;
        
        // Scrollbar
        GameObject scrollbarObj = new GameObject("Scrollbar");
        scrollbarObj.transform.SetParent(_scrollView.transform, false);
        RectTransform scrollbarRect = scrollbarObj.AddComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.anchoredPosition = Vector2.zero;
        scrollbarRect.sizeDelta = new Vector2(20f, 0f);
        
        Scrollbar scrollbar = scrollbarObj.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        
        // Scrollbar background
        GameObject scrollbarBg = new GameObject("Background");
        scrollbarBg.transform.SetParent(scrollbarObj.transform, false);
        Image scrollbarBgImage = scrollbarBg.AddComponent<Image>();
        scrollbarBgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        RectTransform scrollbarBgRect = scrollbarBg.GetComponent<RectTransform>();
        scrollbarBgRect.anchorMin = Vector2.zero;
        scrollbarBgRect.anchorMax = Vector2.one;
        scrollbarBgRect.sizeDelta = Vector2.zero;
        
        // В Unity 5.6 background может быть Image, устанавливаем через reflection
        try
        {
            var backgroundField = AccessTools.Field(typeof(Scrollbar), "m_Background");
            if (backgroundField != null)
            {
                backgroundField.SetValue(scrollbar, scrollbarBgImage);
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning($"[GameSettingsMenu] Could not set scrollbar background: {ex.Message}");
        }
        
        // Scrollbar handle
        GameObject scrollbarHandle = new GameObject("Handle");
        scrollbarHandle.transform.SetParent(scrollbarBg.transform, false);
        Image scrollbarHandleImage = scrollbarHandle.AddComponent<Image>();
        scrollbarHandleImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        RectTransform scrollbarHandleRect = scrollbarHandle.GetComponent<RectTransform>();
        scrollbarHandleRect.anchorMin = Vector2.zero;
        scrollbarHandleRect.anchorMax = Vector2.one;
        scrollbarHandleRect.sizeDelta = Vector2.zero;
        scrollbar.handleRect = scrollbarHandleRect;
        
        scrollRectComponent.verticalScrollbar = scrollbar;
        
        // Создаем элементы настроек
        CreateSettingsElements();
    }
    
    /// <summary>
    /// Создать все элементы настроек
    /// </summary>
    private static void CreateSettingsElements()
    {
        if (_content == null)
        {
            Plugin.Log?.LogError("[GameSettingsMenu] CreateSettingsElements: _content is null!");
            return;
        }
        
        Plugin.Log?.LogInfo("[GameSettingsMenu] Creating settings elements...");
        
        try
        {
            // Toggle для включения/выключения QTE системы
            CreateQTESystemToggle();
            Plugin.Log?.LogInfo("[GameSettingsMenu] QTE Toggle created");
            
            // Section: SP Gain Settings
            CreateSectionHeader("SP Gain Settings");
            CreateFloatSetting("SP Gain Base (0% MB)", Plugin.qteSPGainBase, 0.1f, 5.0f, 3); // 3 знака после запятой для точности
            CreateFloatSetting("SP Gain Min (100% MB)", Plugin.qteSPGainMin, 0.1f, 2.0f, 3); // 3 знака после запятой для точности
            CreateFloatSetting("Yellow Button SP Gain Min", Plugin.qteYellowButtonSPGainMin, 1.0f, 10.0f, 1);
            CreateFloatSetting("Yellow Button SP Gain Max", Plugin.qteYellowButtonSPGainMax, 5.0f, 30.0f, 1);
            Plugin.Log?.LogInfo("[GameSettingsMenu] SP Gain Settings created");
            
            // Section: Penalty Settings
            CreateSectionHeader("Penalty Settings");
            CreateFloatSetting("MP Penalty Percent", Plugin.qteMPPenaltyPercent, 10.0f, 50.0f, 1);
            CreateFloatSetting("MindBroken Penalty Percent", Plugin.qteMindBrokenPenaltyPercent, 0.5f, 5.0f, 1);
            CreateFloatSetting("Red Button MB Penalty", Plugin.qteRedButtonMindBrokenPenalty, 1.0f, 10.0f, 1);
            CreateFloatSetting("SP Penalty Multiplier", Plugin.qteSPPenaltyMultiplier, 1.0f, 5.0f, 1);
            Plugin.Log?.LogInfo("[GameSettingsMenu] Penalty Settings created");
            
            // Section: Timer Settings
            CreateSectionHeader("Timer Settings");
            CreateFloatSetting("Window Duration Min", Plugin.qteWindowDurationMin, 1.0f, 5.0f, 1);
            CreateFloatSetting("Window Duration Max", Plugin.qteWindowDurationMax, 2.0f, 6.0f, 1);
            CreateFloatSetting("Cooldown Duration Min", Plugin.qteCooldownDurationMin, 1.0f, 5.0f, 1);
            CreateFloatSetting("Cooldown Duration Max", Plugin.qteCooldownDurationMax, 2.0f, 8.0f, 1);
            Plugin.Log?.LogInfo("[GameSettingsMenu] Timer Settings created");
            
            // Section: Visual Effects
            CreateSectionHeader("Visual Effects");
            CreateFloatSetting("Rotation Speed", Plugin.qteRotationSpeed, 30.0f, 180.0f, 0);
            CreateFloatSetting("Rotation Start Time", Plugin.qteRotationStartTime, 0.1f, 2.0f, 1);
            CreateFloatSetting("Color Change Interval", Plugin.qteColorChangeInterval, 0.5f, 3.0f, 1);
            CreateFloatSetting("Press Indicator Duration", Plugin.qtePressIndicatorDuration, 0.05f, 0.5f, 2);
            CreateFloatSetting("Max Button Transparency", Plugin.qteMaxButtonTransparency, 0.0f, 1.0f, 2);
            CreateFloatSetting("Max Pink Shadow Intensity", Plugin.qteMaxPinkShadowIntensity, 0.0f, 2.0f, 1);
            Plugin.Log?.LogInfo("[GameSettingsMenu] Visual Effects created");
            
            // Section: Sound Settings
            CreateSectionHeader("Sound Settings");
            CreateFloatSetting("Success Volume Multiplier", Plugin.qteSuccessVolumeMultiplier, 0.0f, 2.0f, 1);
            CreateFloatSetting("Failure Volume Multiplier", Plugin.qteFailureVolumeMultiplier, 0.0f, 2.0f, 1);
            Plugin.Log?.LogInfo("[GameSettingsMenu] Sound Settings created");
            
            // Section: Combo Settings
            CreateSectionHeader("Combo Settings");
            CreateIntSetting("Combo Milestone", Plugin.qteComboMilestone, 5, 50);
            Plugin.Log?.LogInfo("[GameSettingsMenu] Combo Settings created");
            
            Plugin.Log?.LogInfo($"[GameSettingsMenu] All settings elements created. Total: {_settingsElements.Count}");
            
            // ПРИНУДИТЕЛЬНО ОБНОВЛЯЕМ ВСЕ LAYOUT - КРИТИЧЕСКИ ВАЖНО!
            if (_content != null)
            {
                RectTransform contentRect = _content.GetComponent<RectTransform>();
                VerticalLayoutGroup layout = _content.GetComponent<VerticalLayoutGroup>();
                if (layout != null && contentRect != null)
                {
                    // Принудительно обновляем все дочерние элементы
                    foreach (Transform child in _content.transform)
                    {
                        RectTransform childRect = child.GetComponent<RectTransform>();
                        if (childRect != null)
                        {
                            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(childRect);
                        }
                    }
                    // Обновляем сам content
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
                    Plugin.Log?.LogInfo($"[GameSettingsMenu] Force rebuilt all layouts. Content size: {contentRect.sizeDelta}");
                }
                
                ContentSizeFitter fitter = _content.GetComponent<ContentSizeFitter>();
                if (fitter != null && contentRect != null && layout != null)
                {
                    // Принудительно обновляем размер контента
                    float totalHeight = 0f;
                    foreach (Transform child in _content.transform)
                    {
                        RectTransform childRect = child.GetComponent<RectTransform>();
                        if (childRect != null)
                        {
                            totalHeight += childRect.sizeDelta.y + layout.spacing;
                        }
                    }
                    totalHeight += layout.padding.top + layout.padding.bottom;
                    contentRect.sizeDelta = new Vector2(0f, totalHeight);
                    Plugin.Log?.LogInfo($"[GameSettingsMenu] Content height updated to: {totalHeight}");
                }
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogError($"[GameSettingsMenu] Error in CreateSettingsElements: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Создать Toggle для включения/выключения QTE системы
    /// </summary>
    private static void CreateQTESystemToggle()
    {
        if (_content == null)
        {
            return;
        }
        
        GameObject toggleContainer = new GameObject("QTESystemToggle");
        toggleContainer.transform.SetParent(_content.transform, false);
        
        HorizontalLayoutGroup layout = toggleContainer.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = true;
        
        RectTransform toggleRect = toggleContainer.GetComponent<RectTransform>();
        toggleRect.sizeDelta = new Vector2(0f, 50f); // Высота для видимости при скролле
        
        // Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(toggleContainer.transform, false);
        Text label = labelObj.AddComponent<Text>();
        label.text = "QTE ENABLE";
        label.fontSize = 20;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.alignment = TextAnchor.MiddleLeft;
        
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(350f, 35f);
        
        // Toggle
        GameObject toggleObj = new GameObject("Toggle");
        toggleObj.transform.SetParent(toggleContainer.transform, false);
        Toggle toggle = toggleObj.AddComponent<Toggle>();
        toggle.isOn = Plugin.enableQTESystem?.Value ?? true;
        
        RectTransform toggleRectTransform = toggleObj.GetComponent<RectTransform>();
        toggleRectTransform.sizeDelta = new Vector2(35f, 35f); // Увеличен размер toggle
        
        // Toggle background
        GameObject toggleBg = new GameObject("Background");
        toggleBg.transform.SetParent(toggleObj.transform, false);
        Image toggleBgImage = toggleBg.AddComponent<Image>();
        toggleBgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        RectTransform toggleBgRect = toggleBg.GetComponent<RectTransform>();
        toggleBgRect.anchorMin = Vector2.zero;
        toggleBgRect.anchorMax = Vector2.one;
        toggleBgRect.sizeDelta = Vector2.zero;
        toggle.targetGraphic = toggleBgImage;
        
        // Toggle checkmark
        GameObject toggleCheck = new GameObject("Checkmark");
        toggleCheck.transform.SetParent(toggleBg.transform, false);
        Image toggleCheckImage = toggleCheck.AddComponent<Image>();
        toggleCheckImage.color = Color.green;
        RectTransform toggleCheckRect = toggleCheck.GetComponent<RectTransform>();
        toggleCheckRect.anchorMin = Vector2.zero;
        toggleCheckRect.anchorMax = Vector2.one;
        toggleCheckRect.sizeDelta = Vector2.zero;
        toggle.graphic = toggleCheckImage;
        
        _qteSystemToggle = toggle;
        
        // Обработчик изменения Toggle
        toggle.onValueChanged.AddListener((value) =>
        {
            if (Plugin.enableQTESystem != null)
            {
                Plugin.enableQTESystem.Value = value;
            }
            UpdateSettingsInteractability(value);
        });
        
        // Инициализируем состояние элементов
        UpdateSettingsInteractability(toggle.isOn);
        
        // Добавляем дефолтное значение и описание справа от toggle (отдельные элементы)
        var description = GetSettingDescription("QTE ENABLE");
        if (description != null)
        {
            string descriptionString = description.description ?? "";
            string defaultValueString = description.defaultValue ?? "";
            
            // Элемент с дефолтным значением (сначала)
            if (!string.IsNullOrEmpty(defaultValueString))
            {
                GameObject defaultValueObj = new GameObject("DefaultValue");
                defaultValueObj.transform.SetParent(toggleContainer.transform, false);
                
                Text defaultValueText = defaultValueObj.AddComponent<Text>();
                defaultValueText.text = $"(Default: {defaultValueString})";
                defaultValueText.fontSize = 13; // Увеличен на 2 (было 11)
                defaultValueText.fontStyle = FontStyle.Italic;
                defaultValueText.color = new Color(0.6f, 0.6f, 0.6f, 1f); // Темнее чем описание
                defaultValueText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                defaultValueText.alignment = TextAnchor.MiddleLeft;
                defaultValueText.raycastTarget = false;
                defaultValueText.resizeTextForBestFit = false;
                defaultValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
                defaultValueText.verticalOverflow = VerticalWrapMode.Truncate;
                
                RectTransform defaultValueRect = defaultValueObj.GetComponent<RectTransform>();
                defaultValueRect.sizeDelta = new Vector2(150f, 35f);
                defaultValueRect.anchorMin = new Vector2(0f, 0.5f);
                defaultValueRect.anchorMax = new Vector2(0f, 0.5f);
                defaultValueRect.pivot = new Vector2(0f, 0.5f);
                
                LayoutElement defaultValueLayout = defaultValueObj.AddComponent<LayoutElement>();
                defaultValueLayout.preferredWidth = 150f;
                defaultValueLayout.preferredHeight = 35f;
                defaultValueLayout.flexibleWidth = 0f;
            }
            
            // Элемент с описанием (после дефолтного значения, большой шрифт как у названия)
            if (!string.IsNullOrEmpty(descriptionString))
            {
                GameObject descriptionObj = new GameObject("Description");
                descriptionObj.transform.SetParent(toggleContainer.transform, false);
                
                Text descriptionText = descriptionObj.AddComponent<Text>();
                descriptionText.text = descriptionString;
                descriptionText.fontSize = 16; // Увеличенный размер, как у системного названия (но немного меньше)
                descriptionText.fontStyle = FontStyle.Bold; // Жирный шрифт как у системного названия
                descriptionText.color = Color.white; // Белый цвет как у системного названия
                descriptionText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                descriptionText.alignment = TextAnchor.MiddleLeft;
                descriptionText.raycastTarget = false;
                descriptionText.resizeTextForBestFit = false;
                descriptionText.horizontalOverflow = HorizontalWrapMode.Overflow; // Не переносим текст
                descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
                
                RectTransform descriptionRect = descriptionObj.GetComponent<RectTransform>();
                descriptionRect.sizeDelta = new Vector2(350f, 35f); // Увеличена ширина для большего текста
                descriptionRect.anchorMin = new Vector2(0f, 0.5f);
                descriptionRect.anchorMax = new Vector2(0f, 0.5f);
                descriptionRect.pivot = new Vector2(0f, 0.5f);
                
                LayoutElement descriptionLayout = descriptionObj.AddComponent<LayoutElement>();
                descriptionLayout.preferredWidth = 350f; // Увеличена ширина
                descriptionLayout.preferredHeight = 35f;
                descriptionLayout.flexibleWidth = 1f; // Может растягиваться для длинных описаний
            }
            
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Added default value and description for 'QTE ENABLE'");
        }
        else
        {
            Plugin.Log?.LogWarning($"[GameSettingsMenu] Description not found for 'QTE ENABLE'");
        }
    }
    
    /// <summary>
    /// Обновить интерактивность элементов настроек в зависимости от состояния Toggle
    /// </summary>
    private static void UpdateSettingsInteractability(bool isEnabled)
    {
        Plugin.Log?.LogInfo($"[GameSettingsMenu] UpdateSettingsInteractability called: isEnabled={isEnabled}, total elements={_settingsElements.Count}");
        
        int updatedCount = 0;
        foreach (var kvp in _settingsElements)
        {
            string labelText = kvp.Key;
            var element = kvp.Value;
            
            if (element.slider != null)
            {
                element.slider.interactable = isEnabled;
                updatedCount++;
            }
            if (element.inputField != null)
            {
                element.inputField.interactable = isEnabled;
                updatedCount++;
                Plugin.Log?.LogInfo($"[GameSettingsMenu] Updated interactability for '{labelText}': {isEnabled}");
            }
        }
        
        Plugin.Log?.LogInfo($"[GameSettingsMenu] Updated {updatedCount} elements interactability to {isEnabled}");
    }
    
    /// <summary>
    /// Создать заголовок секции
    /// </summary>
    private static void CreateSectionHeader(string text)
    {
        if (_content == null)
        {
            return;
        }
        
        GameObject headerObj = new GameObject($"SectionHeader_{text}");
        headerObj.transform.SetParent(_content.transform, false);
        
        Text headerText = headerObj.AddComponent<Text>();
        headerText.text = text;
        headerText.fontSize = 22;
        headerText.fontStyle = FontStyle.Bold;
        headerText.color = new Color(0.7f, 0.5f, 1f, 1f); // Фиолетовый цвет
        headerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        headerText.alignment = TextAnchor.MiddleLeft; // По левому краю для левой части экрана
        headerText.resizeTextForBestFit = false; // Не изменять размер текста
        headerText.horizontalOverflow = HorizontalWrapMode.Overflow; // Не переносить текст
        
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.sizeDelta = new Vector2(850f, 40f); // Ширина для левой части экрана
        headerRect.pivot = new Vector2(0f, 1f); // Pivot в верхнем левом углу
        headerRect.anchorMin = new Vector2(0f, 1f); // Верхний левый угол
        headerRect.anchorMax = new Vector2(0f, 1f); // Верхний левый угол
        headerRect.anchoredPosition = Vector2.zero; // Позиция будет управляться VerticalLayoutGroup
        
        LayoutElement headerLayout = headerObj.AddComponent<LayoutElement>();
        headerLayout.preferredHeight = 40f;
        headerLayout.preferredWidth = 850f; // Ширина для левой части
        headerLayout.flexibleWidth = 0f;
    }
    
    /// <summary>
    /// Создать элемент настройки (только InputField) для float
    /// </summary>
    private static void CreateFloatSetting(string labelText, ConfigEntry<float> configEntry, float minValue, float maxValue, int decimals)
    {
        if (_content == null)
        {
            Plugin.Log?.LogError($"[GameSettingsMenu] CreateFloatSetting: _content is null for '{labelText}'");
            return;
        }
        
        if (configEntry == null)
        {
            Plugin.Log?.LogError($"[GameSettingsMenu] CreateFloatSetting: configEntry is null for '{labelText}'");
            return;
        }
        
        try
        {
            if (_content.transform == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] CreateFloatSetting: _content.transform is null for '{labelText}'");
                return;
            }
            
            SettingElement element = new SettingElement
            {
                configEntry = configEntry,
                minValue = minValue,
                maxValue = maxValue,
                decimals = decimals
            };
            
            // Контейнер - привязываем к ЛЕВОМУ КРАЮ
            GameObject container = new GameObject($"Setting_{labelText}");
            if (container == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] CreateFloatSetting: Failed to create container GameObject for '{labelText}'");
                return;
            }
            
            container.transform.SetParent(_content.transform, false);
            
            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.sizeDelta = new Vector2(850f, 50f);
            containerRect.anchorMin = new Vector2(0f, 1f); // Верхний левый угол
            containerRect.anchorMax = new Vector2(0f, 1f); // Верхний левый угол
            containerRect.pivot = new Vector2(0f, 1f); // Pivot в верхнем левом углу
            
            LayoutElement containerLayout = container.AddComponent<LayoutElement>();
            containerLayout.preferredHeight = 50f;
            containerLayout.preferredWidth = 850f; // Ширина контейнера
            containerLayout.flexibleWidth = 0f;
            
            HorizontalLayoutGroup layout = container.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f; // Увеличенный отступ между элементами
            layout.childControlWidth = false; // Используем LayoutElement
            layout.childControlHeight = false; // Используем LayoutElement
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.padding = new RectOffset(0, 0, 5, 5); // Паддинг только сверху и снизу
            
            // Label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(container.transform, false);
            Text label = labelObj.AddComponent<Text>();
            label.text = labelText;
            label.fontSize = 16;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            label.resizeTextForBestFit = false; // Важно: не изменять размер текста
            label.horizontalOverflow = HorizontalWrapMode.Overflow; // Не переносить текст
            
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(350f, 40f); // Уменьшена ширина чтобы освободить место для описания
            labelRect.anchorMin = new Vector2(0f, 0.5f); // Центр по вертикали для выравнивания
            labelRect.anchorMax = new Vector2(0f, 0.5f); // Центр по вертикали для выравнивания
            labelRect.pivot = new Vector2(0f, 0.5f); // Pivot в центре слева для выравнивания
            
            LayoutElement labelLayout = labelObj.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 350f; // Уменьшена ширина чтобы освободить место для описания
            labelLayout.preferredHeight = 40f;
            labelLayout.flexibleWidth = 0f;
            
            element.label = label;
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Label created successfully for '{labelText}'");
            
            // InputField
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Creating InputField for '{labelText}'...");
            GameObject inputObj = new GameObject("InputField");
            if (inputObj == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Failed to create inputObj for '{labelText}'");
                return;
            }
            
            inputObj.transform.SetParent(container.transform, false);
            Plugin.Log?.LogInfo($"[GameSettingsMenu] InputField parent set for '{labelText}'");
            
            // ПОЛУЧАЕМ RectTransform СРАЗУ ПОСЛЕ SetParent, ДО добавления InputField
            RectTransform inputRect = inputObj.transform as RectTransform;
            if (inputRect == null)
            {
                // Если приведение не сработало, пробуем GetComponent
                inputRect = inputObj.GetComponent<RectTransform>();
            }
            if (inputRect == null)
            {
                // Если все еще null, добавляем явно
                inputRect = inputObj.AddComponent<RectTransform>();
            }
            if (inputRect == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Failed to get or create RectTransform from inputObj for '{labelText}'");
                return;
            }
            Plugin.Log?.LogInfo($"[GameSettingsMenu] RectTransform obtained for '{labelText}'");
            
            InputField input = inputObj.AddComponent<InputField>();
            if (input == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Failed to add InputField component for '{labelText}'");
                return;
            }
            
            input.contentType = InputField.ContentType.DecimalNumber;
            input.interactable = true;
            input.readOnly = true; // Отключаем стандартную обработку ввода, так как мы обрабатываем вручную
            input.shouldHideMobileInput = false;
            
            // Настройка Navigation для InputField (может помочь с фокусом)
            var navigation = input.navigation;
            navigation.mode = Navigation.Mode.None; // Отключаем навигацию клавиатурой
            input.navigation = navigation;
            Plugin.Log?.LogInfo($"[GameSettingsMenu] InputField component configured for '{labelText}'");
            
            inputRect.sizeDelta = new Vector2(120f, 40f); // Уменьшено для максимум 5 цифр // Уменьшенный размер
            inputRect.anchorMin = new Vector2(0f, 0.5f); // Центр по вертикали для выравнивания
            inputRect.anchorMax = new Vector2(0f, 0.5f); // Центр по вертикали для выравнивания
            inputRect.pivot = new Vector2(0f, 0.5f); // Pivot в центре слева для выравнивания
            inputRect.anchoredPosition = Vector2.zero; // Позиция контролируется LayoutGroup
            
            LayoutElement inputLayout = inputObj.AddComponent<LayoutElement>();
            inputLayout.preferredWidth = 120f; // Ширина для максимум 5 цифр
            inputLayout.preferredHeight = 40f;
            inputLayout.flexibleWidth = 0f;
            inputLayout.ignoreLayout = false;
            
            // Убеждаемся что InputField активен и виден
            inputObj.SetActive(true);
            
            // Background - делаем ОЧЕНЬ ЗАМЕТНЫМ (светлый фон с темным border)
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Creating background for '{labelText}'...");
            GameObject inputBg = new GameObject("Background");
            if (inputBg == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Failed to create inputBg for '{labelText}'");
                return;
            }
            
            inputBg.transform.SetParent(inputObj.transform, false);
            Image inputBgImage = inputBg.AddComponent<Image>();
            if (inputBgImage == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Failed to add Image to inputBg for '{labelText}'");
                return;
            }
            
            inputBgImage.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            inputBgImage.raycastTarget = true; // Background должен получать raycasts для InputField
            RectTransform inputBgRect = inputBg.GetComponent<RectTransform>();
            if (inputBgRect == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Failed to get RectTransform from inputBg for '{labelText}'");
                return;
            }
            
            inputBgRect.anchorMin = Vector2.zero;
            inputBgRect.anchorMax = Vector2.one;
            inputBgRect.sizeDelta = Vector2.zero;
            // Устанавливаем targetGraphic - InputField нужен targetGraphic для работы
            input.targetGraphic = inputBgImage;
            
            // Добавляем простой компонент для активации InputField при клике на background
            // Это необходимо, так как InputField может не получать события клика напрямую
            var inputFieldActivator = inputBg.AddComponent<SimpleInputFieldActivator>();
            inputFieldActivator.inputField = input;
            Plugin.Log?.LogInfo($"[GameSettingsMenu] SimpleInputFieldActivator added to background for '{labelText}'");
            
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Background created for '{labelText}'");
            
            // Border для визуализации изменений - по умолчанию прозрачный
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(inputObj.transform, false);
            Image borderImage = borderObj.AddComponent<Image>();
            borderImage.color = new Color(0.9f, 0.9f, 0.1f, 0f); // ПРОЗРАЧНЫЙ по умолчанию, желтый при изменении
            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = new Vector2(0f, 0f);
            borderRect.anchorMax = new Vector2(1f, 1f);
            borderRect.sizeDelta = new Vector2(3f, 3f);
            borderRect.anchoredPosition = Vector2.zero;
            borderImage.raycastTarget = false;
            
            element.borderObj = borderObj;
            element.borderImage = borderImage;
            element.originalValue = configEntry.Value; // Сохраняем оригинальное значение
            element.isChanged = false;
            
            // Text - делаем текст МАКСИМАЛЬНО заметным
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Creating input text for '{labelText}'...");
            GameObject inputTextObj = new GameObject("Text");
            if (inputTextObj == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Failed to create inputTextObj for '{labelText}'");
                return;
            }
            
            inputTextObj.transform.SetParent(inputObj.transform, false);
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Input text parent set for '{labelText}'");
            
            Text inputText = inputTextObj.AddComponent<Text>();
            if (inputText == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Failed to add Text component to inputTextObj for '{labelText}'");
                return;
            }
            
            Font inputArialFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (inputArialFont == null)
            {
                Plugin.Log?.LogWarning($"[GameSettingsMenu] Arial font not found for input text, trying LegacyRuntime");
                inputArialFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            
            inputText.fontSize = 16; // Уменьшенный размер шрифта
            inputText.color = Color.white; // Белый цвет как у названий
            inputText.font = inputArialFont;
            inputText.fontStyle = FontStyle.Normal; // Обычный стиль как у названий
            inputText.alignment = TextAnchor.MiddleLeft; // Левое выравнивание для удобного ввода дробных чисел
            inputText.raycastTarget = false;
            inputText.resizeTextForBestFit = false;
            inputText.horizontalOverflow = HorizontalWrapMode.Overflow;
            inputText.verticalOverflow = VerticalWrapMode.Truncate;
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Input text configured for '{labelText}'");
            
            RectTransform inputTextRect = inputTextObj.GetComponent<RectTransform>();
            if (inputTextRect == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Failed to get RectTransform from inputTextObj for '{labelText}'");
                return;
            }
            
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.sizeDelta = new Vector2(-10f, -6f); // Меньше паддинг - больше текст
            inputTextRect.anchoredPosition = Vector2.zero;
            input.textComponent = inputText;
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Input text component assigned for '{labelText}'");
            
            // Placeholder (опционально, но может помочь с фокусом)
            GameObject placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(inputObj.transform, false);
            Text placeholderText = placeholderObj.AddComponent<Text>();
            placeholderText.text = "";
            placeholderText.fontSize = 16;
            placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            placeholderText.font = inputArialFont;
            placeholderText.alignment = TextAnchor.MiddleCenter;
            placeholderText.raycastTarget = false;
            RectTransform placeholderRect = placeholderObj.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.sizeDelta = Vector2.zero;
            
            // Устанавливаем placeholder через reflection (Unity 5.6)
            try
            {
                var placeholderField = Field(typeof(InputField), "m_Placeholder");
                if (placeholderField != null)
                {
                    placeholderField.SetValue(input, placeholderText);
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogWarning($"[GameSettingsMenu] Could not set placeholder for '{labelText}': {ex.Message}");
            }
            
            // Инициализация значения - ЯВНО УСТАНАВЛИВАЕМ ПЕРЕД АКТИВАЦИЕЙ
            string initialValue = configEntry.Value.ToString($"F{decimals}");
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Setting initial value for '{labelText}': '{initialValue}'");
            input.text = initialValue;
            inputText.text = initialValue; // ЯВНО УСТАНАВЛИВАЕМ ТЕКСТ
            // Убеждаемся что текст установлен
            if (string.IsNullOrEmpty(inputText.text))
            {
                Plugin.Log?.LogWarning($"[GameSettingsMenu] Text is empty after setting! Trying again...");
                inputText.text = initialValue;
                input.text = initialValue;
            }
            
            // ПРИНУДИТЕЛЬНО ОБНОВЛЯЕМ ВСЕ КОМПОНЕНТЫ
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(inputRect);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(inputTextRect);
            
            // Убеждаемся что все видно
            inputObj.SetActive(true);
            inputTextObj.SetActive(true);
            inputBg.SetActive(true);
            borderObj.SetActive(true);
            
            Plugin.Log?.LogInfo($"[GameSettingsMenu] InputField '{labelText}' initialized: value='{initialValue}', fontSize={inputText.fontSize}, color={inputText.color}, bgColor={inputBgImage.color}, size={inputRect.sizeDelta}, active={inputObj.activeSelf}, textLength={inputText.text.Length}");
            
            // Синхронизация InputField ↔ ConfigEntry с отслеживанием изменений
            input.onEndEdit.AddListener((text) =>
            {
                
                if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
                {
                    value = Mathf.Clamp(value, minValue, maxValue);
                    
                    // Проверяем изменилось ли значение относительно originalValue (значение из ConfigEntry)
                    // ВАЖНО: Проверяем ДО форматирования, чтобы правильно определить изменения
                    bool hasChanged = Mathf.Abs(value - element.originalValue) > 0.0001f;
                    element.isChanged = hasChanged;
                    
                    
                    // Форматируем значение с нужным количеством знаков после запятой (автоматически дописывает нули: "5" -> "5.0")
                    string formattedValue = value.ToString($"F{decimals}");
                    input.text = formattedValue;
                    // Обновляем textComponent для правильного отображения
                    if (input.textComponent != null)
                    {
                        input.textComponent.text = formattedValue;
                    }
                    
                    
                    // Показываем желтый border только если значение действительно изменилось
                    if (element.borderImage != null)
                    {
                        element.borderImage.color = hasChanged ? new Color(0.9f, 0.9f, 0.1f, 1f) : new Color(0.9f, 0.9f, 0.1f, 0f);
                    }
                    
                    // НЕ сохраняем в ConfigEntry сразу - только при Apply
                }
                else
                {
                    Plugin.Log?.LogWarning($"[GameSettingsMenu] Failed to parse '{text}' for '{labelText}', restoring from ConfigEntry");
                    // Если парсинг не удался, возвращаем значение из ConfigEntry
                    string formattedValue = configEntry.Value.ToString($"F{decimals}");
                    input.text = formattedValue;
                    if (input.textComponent != null)
                    {
                        input.textComponent.text = formattedValue;
                    }
                    element.isChanged = false;
                    if (element.borderImage != null)
                    {
                        element.borderImage.color = new Color(0.9f, 0.9f, 0.1f, 0f);
                    }
                }
            });
            
            element.inputField = input;
            element.container = container;
            element.slider = null;
            
            // Добавляем дефолтное значение и описание справа от значения (отдельные элементы)
            var description = GetSettingDescription(labelText);
            if (description != null)
            {
                string descriptionString = description.description ?? "";
                string defaultValueString = description.defaultValue ?? "";
                
                // Элемент с дефолтным значением (сначала)
                if (!string.IsNullOrEmpty(defaultValueString))
                {
                    GameObject defaultValueObj = new GameObject("DefaultValue");
                    defaultValueObj.transform.SetParent(container.transform, false);
                    
                    Text defaultValueText = defaultValueObj.AddComponent<Text>();
                    defaultValueText.text = $"(Default: {defaultValueString})";
                    defaultValueText.fontSize = 13; // Увеличен на 2 (было 11)
                    defaultValueText.fontStyle = FontStyle.Italic;
                    defaultValueText.color = new Color(0.6f, 0.6f, 0.6f, 1f); // Темнее чем описание
                    defaultValueText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    defaultValueText.alignment = TextAnchor.MiddleLeft;
                    defaultValueText.raycastTarget = false;
                    defaultValueText.resizeTextForBestFit = false;
                    defaultValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
                    defaultValueText.verticalOverflow = VerticalWrapMode.Truncate;
                    
                    RectTransform defaultValueRect = defaultValueObj.GetComponent<RectTransform>();
                    defaultValueRect.sizeDelta = new Vector2(150f, 40f);
                    defaultValueRect.anchorMin = new Vector2(0f, 0.5f);
                    defaultValueRect.anchorMax = new Vector2(0f, 0.5f);
                    defaultValueRect.pivot = new Vector2(0f, 0.5f);
                    
                    LayoutElement defaultValueLayout = defaultValueObj.AddComponent<LayoutElement>();
                    defaultValueLayout.preferredWidth = 150f;
                    defaultValueLayout.preferredHeight = 40f;
                    defaultValueLayout.flexibleWidth = 0f;
                }
                
                // Элемент с описанием (после дефолтного значения, большой шрифт как у названия)
                if (!string.IsNullOrEmpty(descriptionString))
                {
                    GameObject descriptionObj = new GameObject("Description");
                    descriptionObj.transform.SetParent(container.transform, false);
                    
                    Text descriptionText = descriptionObj.AddComponent<Text>();
                    descriptionText.text = descriptionString;
                    descriptionText.fontSize = 16; // Увеличенный размер, как у системного названия (но немного меньше)
                    descriptionText.fontStyle = FontStyle.Bold; // Жирный шрифт как у системного названия
                    descriptionText.color = Color.white; // Белый цвет как у системного названия
                    descriptionText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    descriptionText.alignment = TextAnchor.MiddleLeft;
                    descriptionText.raycastTarget = false;
                    descriptionText.resizeTextForBestFit = false;
                    descriptionText.horizontalOverflow = HorizontalWrapMode.Overflow; // Не переносим текст
                    descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
                    
                    RectTransform descriptionRect = descriptionObj.GetComponent<RectTransform>();
                    descriptionRect.sizeDelta = new Vector2(350f, 40f); // Увеличена ширина для большего текста
                    descriptionRect.anchorMin = new Vector2(0f, 0.5f);
                    descriptionRect.anchorMax = new Vector2(0f, 0.5f);
                    descriptionRect.pivot = new Vector2(0f, 0.5f);
                    
                    LayoutElement descriptionLayout = descriptionObj.AddComponent<LayoutElement>();
                    descriptionLayout.preferredWidth = 350f; // Увеличена ширина
                    descriptionLayout.preferredHeight = 40f;
                    descriptionLayout.flexibleWidth = 1f; // Может растягиваться для длинных описаний
                }
                
                Plugin.Log?.LogInfo($"[GameSettingsMenu] Added default value and description for '{labelText}'");
            }
            else
            {
                Plugin.Log?.LogWarning($"[GameSettingsMenu] Description not found for '{labelText}'");
            }
            
            _settingsElements[labelText] = element;
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogError($"[GameSettingsMenu] Error creating float setting '{labelText}': {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Создать элемент настройки (Slider + InputField) для int
    /// </summary>
    private static void CreateIntSetting(string labelText, ConfigEntry<int> configEntry, int minValue, int maxValue)
    {
        if (_content == null)
        {
            Plugin.Log?.LogError($"[GameSettingsMenu] CreateIntSetting: _content is null for '{labelText}'");
            return;
        }
        
        if (configEntry == null)
        {
            Plugin.Log?.LogError($"[GameSettingsMenu] CreateIntSetting: configEntry is null for '{labelText}'");
            return;
        }
        
        try
        {
            SettingElement element = new SettingElement
            {
                configEntryInt = configEntry,
                minValue = minValue,
                maxValue = maxValue,
                decimals = 0
            };
            
            // Контейнер - привязываем к ЛЕВОМУ КРАЮ
            GameObject container = new GameObject($"Setting_{labelText}");
            if (container == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] CreateFloatSetting: Failed to create container GameObject for '{labelText}'");
                return;
            }
            
            container.transform.SetParent(_content.transform, false);
            
            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.sizeDelta = new Vector2(850f, 50f);
            containerRect.anchorMin = new Vector2(0f, 1f); // Верхний левый угол
            containerRect.anchorMax = new Vector2(0f, 1f); // Верхний левый угол
            containerRect.pivot = new Vector2(0f, 1f); // Pivot в верхнем левом углу
            
            LayoutElement containerLayout = container.AddComponent<LayoutElement>();
            containerLayout.preferredHeight = 50f;
            containerLayout.preferredWidth = 850f; // Ширина контейнера
            containerLayout.flexibleWidth = 0f;
            
            HorizontalLayoutGroup layout = container.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f; // Увеличенный отступ между элементами
            layout.childControlWidth = false; // Используем LayoutElement
            layout.childControlHeight = false; // Используем LayoutElement
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.padding = new RectOffset(0, 0, 5, 5); // Паддинг только сверху и снизу
            
            // Label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(container.transform, false);
            Text label = labelObj.AddComponent<Text>();
            label.text = labelText;
            label.fontSize = 16;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            label.resizeTextForBestFit = false; // Важно: не изменять размер текста
            label.horizontalOverflow = HorizontalWrapMode.Overflow; // Не переносить текст
            
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(350f, 40f); // Уменьшена ширина чтобы освободить место для описания
            labelRect.anchorMin = new Vector2(0f, 0.5f); // Центр по вертикали для выравнивания
            labelRect.anchorMax = new Vector2(0f, 0.5f); // Центр по вертикали для выравнивания
            labelRect.pivot = new Vector2(0f, 0.5f); // Pivot в центре слева для выравнивания
            
            LayoutElement labelLayout = labelObj.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 350f; // Уменьшена ширина чтобы освободить место для описания
            labelLayout.preferredHeight = 40f;
            labelLayout.flexibleWidth = 0f;
            
            element.label = label;
            
            // InputField
            GameObject inputObj = new GameObject("InputField");
            inputObj.transform.SetParent(container.transform, false);
            
            // ПОЛУЧАЕМ RectTransform СРАЗУ ПОСЛЕ SetParent, ДО добавления InputField
            RectTransform inputRect = inputObj.transform as RectTransform;
            if (inputRect == null)
            {
                inputRect = inputObj.GetComponent<RectTransform>();
            }
            if (inputRect == null)
            {
                inputRect = inputObj.AddComponent<RectTransform>();
            }
            if (inputRect == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Failed to get or create RectTransform from inputObj for '{labelText}'");
                return;
            }
            
            InputField input = inputObj.AddComponent<InputField>();
            input.contentType = InputField.ContentType.IntegerNumber;
            input.interactable = true;
            input.readOnly = false;
            input.shouldHideMobileInput = false;
            
            // Настройка Navigation для InputField (может помочь с фокусом)
            var navigation = input.navigation;
            navigation.mode = Navigation.Mode.None; // Отключаем навигацию клавиатурой
            input.navigation = navigation;
            
            inputRect.sizeDelta = new Vector2(120f, 40f); // Уменьшено для максимум 5 цифр // Уменьшенный размер
            inputRect.anchorMin = new Vector2(0f, 0.5f); // Центр по вертикали для выравнивания
            inputRect.anchorMax = new Vector2(0f, 0.5f); // Центр по вертикали для выравнивания
            inputRect.pivot = new Vector2(0f, 0.5f); // Pivot в центре слева для выравнивания
            inputRect.anchoredPosition = Vector2.zero; // Позиция контролируется LayoutGroup
            
            LayoutElement inputLayout = inputObj.AddComponent<LayoutElement>();
            inputLayout.preferredWidth = 120f; // Ширина для максимум 5 цифр
            inputLayout.preferredHeight = 40f;
            inputLayout.flexibleWidth = 0f;
            inputLayout.ignoreLayout = false;
            
            // Убеждаемся что InputField активен и виден
            inputObj.SetActive(true);
            
            // Background - делаем ОЧЕНЬ ЗАМЕТНЫМ (светлый фон с темным border)
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Creating background for '{labelText}'...");
            GameObject inputBg = new GameObject("Background");
            if (inputBg == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Failed to create inputBg for '{labelText}'");
                return;
            }
            
            inputBg.transform.SetParent(inputObj.transform, false);
            Image inputBgImage = inputBg.AddComponent<Image>();
            if (inputBgImage == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Failed to add Image to inputBg for '{labelText}'");
                return;
            }
            
            inputBgImage.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            inputBgImage.raycastTarget = true; // Background должен получать raycasts для InputField
            RectTransform inputBgRect = inputBg.GetComponent<RectTransform>();
            if (inputBgRect == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Failed to get RectTransform from inputBg for '{labelText}'");
                return;
            }
            
            inputBgRect.anchorMin = Vector2.zero;
            inputBgRect.anchorMax = Vector2.one;
            inputBgRect.sizeDelta = Vector2.zero;
            input.targetGraphic = inputBgImage; // InputField нужен targetGraphic для работы
            
            // InputField сам обрабатывает клики через OnPointerClick, поэтому не нужно добавлять активатор
            // Просто убеждаемся, что InputField.interactable = true (это уже установлено выше)
            
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Background created for '{labelText}'");
            
            // Border для визуализации изменений - по умолчанию прозрачный
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(inputObj.transform, false);
            Image borderImage = borderObj.AddComponent<Image>();
            borderImage.color = new Color(0.9f, 0.9f, 0.1f, 0f); // ПРОЗРАЧНЫЙ по умолчанию, желтый при изменении
            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = new Vector2(0f, 0f);
            borderRect.anchorMax = new Vector2(1f, 1f);
            borderRect.sizeDelta = new Vector2(3f, 3f);
            borderRect.anchoredPosition = Vector2.zero;
            borderImage.raycastTarget = false;
            
            element.borderObj = borderObj;
            element.borderImage = borderImage;
            element.originalValue = configEntry.Value; // Сохраняем оригинальное значение
            element.isChanged = false;
            
            // Text - делаем текст МАКСИМАЛЬНО заметным
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Creating input text for '{labelText}'...");
            GameObject inputTextObj = new GameObject("Text");
            if (inputTextObj == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Failed to create inputTextObj for '{labelText}'");
                return;
            }
            
            inputTextObj.transform.SetParent(inputObj.transform, false);
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Input text parent set for '{labelText}'");
            
            Text inputText = inputTextObj.AddComponent<Text>();
            if (inputText == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Failed to add Text component to inputTextObj for '{labelText}'");
                return;
            }
            
            Font inputArialFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (inputArialFont == null)
            {
                Plugin.Log?.LogWarning($"[GameSettingsMenu] Arial font not found for input text, trying LegacyRuntime");
                inputArialFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            
            inputText.fontSize = 16; // Уменьшенный размер шрифта
            inputText.color = Color.white; // Белый цвет как у названий
            inputText.font = inputArialFont;
            inputText.fontStyle = FontStyle.Normal; // Обычный стиль как у названий
            inputText.alignment = TextAnchor.MiddleLeft; // Левое выравнивание для удобного ввода дробных чисел
            inputText.raycastTarget = false;
            inputText.resizeTextForBestFit = false;
            inputText.horizontalOverflow = HorizontalWrapMode.Overflow;
            inputText.verticalOverflow = VerticalWrapMode.Truncate;
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Input text configured for '{labelText}'");
            
            RectTransform inputTextRect = inputTextObj.GetComponent<RectTransform>();
            if (inputTextRect == null)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Failed to get RectTransform from inputTextObj for '{labelText}'");
                return;
            }
            
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.sizeDelta = new Vector2(-10f, -6f); // Меньше паддинг - больше текст
            inputTextRect.anchoredPosition = Vector2.zero;
            input.textComponent = inputText;
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Input text component assigned for '{labelText}'");
            
            // Placeholder (опционально, но может помочь с фокусом)
            GameObject placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(inputObj.transform, false);
            Text placeholderText = placeholderObj.AddComponent<Text>();
            placeholderText.text = "";
            placeholderText.fontSize = 16;
            placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            placeholderText.font = inputArialFont;
            placeholderText.alignment = TextAnchor.MiddleCenter;
            placeholderText.raycastTarget = false;
            RectTransform placeholderRect = placeholderObj.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.sizeDelta = Vector2.zero;
            
            // Устанавливаем placeholder через reflection (Unity 5.6)
            try
            {
                var placeholderField = Field(typeof(InputField), "m_Placeholder");
                if (placeholderField != null)
                {
                    placeholderField.SetValue(input, placeholderText);
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogWarning($"[GameSettingsMenu] Could not set placeholder for '{labelText}': {ex.Message}");
            }
            
            // Инициализация значения - УБЕЖДАЕМСЯ ЧТО ЗНАЧЕНИЕ УСТАНОВЛЕНО
            string initialValue = configEntry.Value.ToString();
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Setting initial value for '{labelText}': '{initialValue}'");
            input.text = initialValue;
            inputText.text = initialValue; // ЯВНО УСТАНАВЛИВАЕМ ТЕКСТ
            // Убеждаемся что текст установлен
            if (string.IsNullOrEmpty(inputText.text))
            {
                Plugin.Log?.LogWarning($"[GameSettingsMenu] Text is empty after setting! Trying again...");
                inputText.text = initialValue;
                input.text = initialValue;
            }
            
            // ПРИНУДИТЕЛЬНО ОБНОВЛЯЕМ ВСЕ КОМПОНЕНТЫ
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(inputRect);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(inputTextRect);
            
            // Убеждаемся что все видно
            inputObj.SetActive(true);
            inputTextObj.SetActive(true);
            inputBg.SetActive(true);
            borderObj.SetActive(true);
            
            Plugin.Log?.LogInfo($"[GameSettingsMenu] InputField '{labelText}' initialized: value='{initialValue}', fontSize={inputText.fontSize}, color={inputText.color}, bgColor={inputBgImage.color}, size={inputRect.sizeDelta}, active={inputObj.activeSelf}, textLength={inputText.text.Length}");
            
            // Синхронизация InputField ↔ ConfigEntry с отслеживанием изменений
            input.onEndEdit.AddListener((text) =>
            {
                if (int.TryParse(text, out int value))
                {
                    value = Mathf.Clamp(value, minValue, maxValue);
                    input.text = value.ToString();
                    
                    // Проверяем изменилось ли значение относительно originalValue (значение из ConfigEntry)
                    bool hasChanged = Mathf.Abs(value - element.originalValue) > 0.5f; // Для int используем 0.5 вместо 0.0001
                    element.isChanged = hasChanged;
                    
                    // Показываем желтый border только если значение действительно изменилось
                    if (element.borderImage != null)
                    {
                        element.borderImage.color = hasChanged ? new Color(0.9f, 0.9f, 0.1f, 1f) : new Color(0.9f, 0.9f, 0.1f, 0f);
                    }
                    
                    // НЕ сохраняем в ConfigEntry сразу - только при Apply
                }
                else
                {
                    // Если парсинг не удался, возвращаем значение из ConfigEntry
                    input.text = configEntry.Value.ToString();
                    element.isChanged = false;
                    if (element.borderImage != null)
                    {
                        element.borderImage.color = new Color(0.9f, 0.9f, 0.1f, 0f);
                    }
                }
            });
            
            element.inputField = input;
            element.container = container;
            element.slider = null;
            
            // Добавляем дефолтное значение и описание справа от значения (отдельные элементы)
            var description = GetSettingDescription(labelText);
            if (description != null)
            {
                string descriptionString = description.description ?? "";
                string defaultValueString = description.defaultValue ?? "";
                
                // Элемент с дефолтным значением (сначала)
                if (!string.IsNullOrEmpty(defaultValueString))
                {
                    GameObject defaultValueObj = new GameObject("DefaultValue");
                    defaultValueObj.transform.SetParent(container.transform, false);
                    
                    Text defaultValueText = defaultValueObj.AddComponent<Text>();
                    defaultValueText.text = $"(Default: {defaultValueString})";
                    defaultValueText.fontSize = 13; // Увеличен на 2 (было 11)
                    defaultValueText.fontStyle = FontStyle.Italic;
                    defaultValueText.color = new Color(0.6f, 0.6f, 0.6f, 1f); // Темнее чем описание
                    defaultValueText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    defaultValueText.alignment = TextAnchor.MiddleLeft;
                    defaultValueText.raycastTarget = false;
                    defaultValueText.resizeTextForBestFit = false;
                    defaultValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
                    defaultValueText.verticalOverflow = VerticalWrapMode.Truncate;
                    
                    RectTransform defaultValueRect = defaultValueObj.GetComponent<RectTransform>();
                    defaultValueRect.sizeDelta = new Vector2(150f, 40f);
                    defaultValueRect.anchorMin = new Vector2(0f, 0.5f);
                    defaultValueRect.anchorMax = new Vector2(0f, 0.5f);
                    defaultValueRect.pivot = new Vector2(0f, 0.5f);
                    
                    LayoutElement defaultValueLayout = defaultValueObj.AddComponent<LayoutElement>();
                    defaultValueLayout.preferredWidth = 150f;
                    defaultValueLayout.preferredHeight = 40f;
                    defaultValueLayout.flexibleWidth = 0f;
                }
                
                // Элемент с описанием (после дефолтного значения, большой шрифт как у названия)
                if (!string.IsNullOrEmpty(descriptionString))
                {
                    GameObject descriptionObj = new GameObject("Description");
                    descriptionObj.transform.SetParent(container.transform, false);
                    
                    Text descriptionText = descriptionObj.AddComponent<Text>();
                    descriptionText.text = descriptionString;
                    descriptionText.fontSize = 16; // Увеличенный размер, как у системного названия (но немного меньше)
                    descriptionText.fontStyle = FontStyle.Bold; // Жирный шрифт как у системного названия
                    descriptionText.color = Color.white; // Белый цвет как у системного названия
                    descriptionText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    descriptionText.alignment = TextAnchor.MiddleLeft;
                    descriptionText.raycastTarget = false;
                    descriptionText.resizeTextForBestFit = false;
                    descriptionText.horizontalOverflow = HorizontalWrapMode.Overflow; // Не переносим текст
                    descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
                    
                    RectTransform descriptionRect = descriptionObj.GetComponent<RectTransform>();
                    descriptionRect.sizeDelta = new Vector2(350f, 40f); // Увеличена ширина для большего текста
                    descriptionRect.anchorMin = new Vector2(0f, 0.5f);
                    descriptionRect.anchorMax = new Vector2(0f, 0.5f);
                    descriptionRect.pivot = new Vector2(0f, 0.5f);
                    
                    LayoutElement descriptionLayout = descriptionObj.AddComponent<LayoutElement>();
                    descriptionLayout.preferredWidth = 350f; // Увеличена ширина
                    descriptionLayout.preferredHeight = 40f;
                    descriptionLayout.flexibleWidth = 1f; // Может растягиваться для длинных описаний
                }
                
                Plugin.Log?.LogInfo($"[GameSettingsMenu] Added default value and description for '{labelText}'");
            }
            else
            {
                Plugin.Log?.LogWarning($"[GameSettingsMenu] Description not found for '{labelText}'");
            }
            
            _settingsElements[labelText] = element;
            Plugin.Log?.LogInfo($"[GameSettingsMenu] Created int setting: '{labelText}'");
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogError($"[GameSettingsMenu] Error creating int setting '{labelText}': {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Создать Footer с кнопками
    /// </summary>
    private static void CreateFooter()
    {
        if (_settingsWindow == null)
        {
            return;
        }
        
        _footerObj = new GameObject("Footer");
        _footerObj.transform.SetParent(_settingsWindow.transform, false);
        
        RectTransform footerRect = _footerObj.AddComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0.5f, 0f);
        footerRect.anchorMax = new Vector2(0.5f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = new Vector2(0f, 20f);
        footerRect.sizeDelta = new Vector2(900f, 60f);
        
        HorizontalLayoutGroup footerLayout = _footerObj.AddComponent<HorizontalLayoutGroup>();
        footerLayout.spacing = 20f;
        footerLayout.childControlWidth = false;
        footerLayout.childControlHeight = true;
        footerLayout.childForceExpandHeight = true;
        footerLayout.childAlignment = TextAnchor.MiddleCenter;
        
        // Кнопка Apply
        CreateFooterButton("Apply", new Vector2(200f, 50f), () =>
        {
            SoundManager.PlayButtonSound(ButtonType.Apply);
            ApplySettings();
            // НЕ закрываем окно - только Close должен закрывать
        });

        // Кнопка Reset
        CreateFooterButton("Reset", new Vector2(200f, 50f), () =>
        {
            SoundManager.PlayButtonSound(ButtonType.Reset);
            ResetToDefaults();
        });

        // Кнопка Close
        CreateFooterButton("Close", new Vector2(200f, 50f), () =>
        {
            SoundManager.PlayButtonSound(ButtonType.Close);
            HideSettings();
        });
    }
    
    /// <summary>
    /// Создать кнопку в Footer
    /// </summary>
    private static void CreateFooterButton(string text, Vector2 size, System.Action onClick)
    {
        if (_settingsWindow == null)
        {
            return;
        }
        
        if (_footerObj == null)
        {
            return;
        }
        
        GameObject buttonObj = new GameObject($"Button_{text}");
        buttonObj.transform.SetParent(_footerObj.transform, false);
        
        Button button = buttonObj.AddComponent<Button>();
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.sizeDelta = size;
        
        GameObject buttonTextObj = new GameObject("Text");
        buttonTextObj.transform.SetParent(buttonObj.transform, false);
        Text buttonText = buttonTextObj.AddComponent<Text>();
        buttonText.text = text;
        buttonText.fontSize = 18;
        buttonText.fontStyle = FontStyle.Bold;
        buttonText.color = Color.white;
        buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        buttonText.alignment = TextAnchor.MiddleCenter;
        
        RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.sizeDelta = Vector2.zero;
        
        button.onClick.AddListener(() => onClick?.Invoke());
        
        // Hover эффект (как в HellGateSplashScreen)
        UnityEngine.EventSystems.EventTrigger trigger = buttonObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        
        UnityEngine.EventSystems.EventTrigger.Entry enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
        enterEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((eventData) =>
        {
            buttonRect.localScale = new Vector3(1.15f, 1.15f, 1f);
        });
        trigger.triggers.Add(enterEntry);
        
        UnityEngine.EventSystems.EventTrigger.Entry exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
        exitEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((eventData) =>
        {
            buttonRect.localScale = Vector3.one;
        });
        trigger.triggers.Add(exitEntry);
    }
    
    /// <summary>
    /// Обновить значения всех элементов настроек из ConfigEntry
    /// </summary>
    private static void RefreshSettingsValues()
    {
        Plugin.Log?.LogInfo("[GameSettingsMenu] Refreshing all settings values...");
        
        foreach (var kvp in _settingsElements)
        {
            string labelText = kvp.Key;
            var element = kvp.Value;
            
            try
            {
                if (element.inputField == null)
                {
                    Plugin.Log?.LogWarning($"[GameSettingsMenu] Skipping '{labelText}' - inputField is null");
                    continue;
                }
                
                if (element.configEntry != null)
                {
                    float newValue = element.configEntry.Value;
                    // Обновляем InputField
                    element.inputField.text = newValue.ToString($"F{element.decimals}");
                    // Синхронизируем originalValue с ConfigEntry
                    element.originalValue = newValue;
                    // Сбрасываем флаг изменений
                    element.isChanged = false;
                    // Убираем желтый border
                    if (element.borderImage != null)
                    {
                        element.borderImage.color = new Color(0.9f, 0.9f, 0.1f, 0f);
                    }
                    Plugin.Log?.LogInfo($"[GameSettingsMenu] Refreshed '{labelText}' to {newValue}");
                }
                else if (element.configEntryInt != null)
                {
                    int newValue = element.configEntryInt.Value;
                    // Обновляем InputField
                    element.inputField.text = newValue.ToString();
                    // Синхронизируем originalValue с ConfigEntry
                    element.originalValue = newValue;
                    // Сбрасываем флаг изменений
                    element.isChanged = false;
                    // Убираем желтый border
                    if (element.borderImage != null)
                    {
                        element.borderImage.color = new Color(0.9f, 0.9f, 0.1f, 0f);
                    }
                    Plugin.Log?.LogInfo($"[GameSettingsMenu] Refreshed '{labelText}' to {newValue}");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Error refreshing '{labelText}': {ex.Message}");
            }
        }
        
        if (_qteSystemToggle != null && Plugin.enableQTESystem != null)
        {
            _qteSystemToggle.isOn = Plugin.enableQTESystem.Value;
        }
    }
    
    /// <summary>
    /// Применить настройки (сохранить в ConfigEntry)
    /// </summary>
    private static void ApplySettings()
    {
        Plugin.Log?.LogInfo("[GameSettingsMenu] Applying settings...");
        
        List<GameObject> changedContainers = new List<GameObject>();
        
        foreach (var kvp in _settingsElements)
        {
            string labelText = kvp.Key;
            var element = kvp.Value;
            
            try
            {
                if (element.inputField == null)
                {
                    continue;
                }
                
                // Получаем текущее значение из InputField и сравниваем с originalValue
                // Применяем изменения если они есть (даже если isChanged не установлен)
                if (element.configEntry != null)
                {
                    // Получаем значение из InputField
                    string text = element.inputField.text;
                    if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float currentValue))
                    {
                        currentValue = Mathf.Clamp(currentValue, element.minValue, element.maxValue);
                        
                        // Проверяем изменилось ли значение относительно originalValue
                        bool hasChanged = Mathf.Abs(currentValue - element.originalValue) > 0.0001f;
                        
                        Plugin.Log?.LogInfo($"[GameSettingsMenu] Apply check for '{labelText}': currentValue={currentValue}, originalValue={element.originalValue}, hasChanged={hasChanged}, isChanged={element.isChanged}");
                        
                        if (hasChanged || element.isChanged)
                        {
                            // ВАЖНО: Сохраняем в ConfigEntry ПЕРЕД обновлением originalValue
                            element.configEntry.Value = currentValue;
                            
                            // Обновляем originalValue ПОСЛЕ сохранения в ConfigEntry
                            element.originalValue = currentValue;
                            
                            // Обновляем текст в InputField с правильным форматированием
                            string formattedValue = currentValue.ToString($"F{element.decimals}");
                            element.inputField.text = formattedValue;
                            if (element.inputField.textComponent != null)
                            {
                                element.inputField.textComponent.text = formattedValue;
                            }
                            
                            // Скрываем желтый border
                            if (element.borderImage != null)
                            {
                                element.borderImage.color = new Color(0.9f, 0.9f, 0.1f, 0f);
                            }
                            
                            element.isChanged = false;
                            
                            // Добавляем контейнер для зеленого моргания
                            if (element.container != null)
                            {
                                changedContainers.Add(element.container);
                            }
                            
                        }
                    }
                }
                else if (element.configEntryInt != null)
                {
                    string text = element.inputField.text;
                    if (int.TryParse(text, out int currentValue))
                    {
                        currentValue = Mathf.Clamp(currentValue, (int)element.minValue, (int)element.maxValue);
                        
                        // Проверяем изменилось ли значение относительно originalValue
                        bool hasChanged = Mathf.Abs(currentValue - element.originalValue) > 0.5f;
                        
                        Plugin.Log?.LogInfo($"[GameSettingsMenu] Apply check for '{labelText}': currentValue={currentValue}, originalValue={element.originalValue}, hasChanged={hasChanged}, isChanged={element.isChanged}");
                        
                        if (hasChanged || element.isChanged)
                        {
                            // ВАЖНО: Сохраняем в ConfigEntry ПЕРЕД обновлением originalValue
                            element.configEntryInt.Value = currentValue;
                            
                            // Обновляем originalValue ПОСЛЕ сохранения в ConfigEntry
                            element.originalValue = currentValue;
                            
                            // Обновляем текст в InputField
                            string formattedValue = currentValue.ToString();
                            element.inputField.text = formattedValue;
                            if (element.inputField.textComponent != null)
                            {
                                element.inputField.textComponent.text = formattedValue;
                            }
                            
                            // Скрываем желтый border
                            if (element.borderImage != null)
                            {
                                element.borderImage.color = new Color(0.9f, 0.9f, 0.1f, 0f);
                            }
                            
                            element.isChanged = false;
                            
                            // Добавляем контейнер для зеленого моргания
                            if (element.container != null)
                            {
                                changedContainers.Add(element.container);
                            }
                            
                            Plugin.Log?.LogInfo($"[GameSettingsMenu] ✅ Applied '{labelText}': {currentValue}");
                        }
                    }
                }
                else
                {
                    // Если значение не изменилось, синхронизируем originalValue с ConfigEntry
                    // Это нужно для случаев, когда ConfigEntry был изменен извне
                    if (element.configEntry != null)
                    {
                        element.originalValue = element.configEntry.Value;
                    }
                    else if (element.configEntryInt != null)
                    {
                        element.originalValue = element.configEntryInt.Value;
                    }
                    
                    // Убеждаемся что border скрыт
                    if (element.borderImage != null)
                    {
                        element.borderImage.color = new Color(0.9f, 0.9f, 0.1f, 0f);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogError($"[GameSettingsMenu] Error applying '{labelText}': {ex.Message}");
            }
        }
        
        // Показываем зеленое моргание для измененных секций
        if (changedContainers.Count > 0)
        {
            StartFlashGreenCoroutine(changedContainers);
        }
        
        // НЕ вызываем RefreshSettingsValues() здесь, так как мы уже обновили все значения в ApplySettings
        // RefreshSettingsValues() перезапишет InputField значениями из ConfigEntry, что может сбросить изменения
        // Вместо этого мы уже обновили InputField.text в ApplySettings для каждого измененного элемента
        
        Plugin.Log?.LogInfo("[GameSettingsMenu] ✅ Settings applied successfully");
    }
    
    /// <summary>
    /// Корутина для зеленого моргания измененных секций
    /// </summary>
    private static void StartFlashGreenCoroutine(List<GameObject> containers)
    {
        if (_canvas == null) return;
        
        // Создаем или получаем runner для корутины
        var runnerObj = _canvas.transform.Find("SettingsMenuRunner");
        if (runnerObj == null)
        {
            runnerObj = new GameObject("SettingsMenuRunner").transform;
            runnerObj.SetParent(_canvas.transform, false);
        }
        
        var runner = runnerObj.GetComponent<SettingsMenuRunner>();
        if (runner == null)
        {
            runner = runnerObj.gameObject.AddComponent<SettingsMenuRunner>();
        }
        
        runner.StartFlashGreen(containers);
    }
    
    /// <summary>
    /// Сбросить настройки к дефолтным значениям
    /// </summary>
    private static void ResetToDefaults()
    {
        Plugin.Log?.LogInfo("[GameSettingsMenu] Resetting all settings to defaults...");
        
        // QTE System Toggle
        if (Plugin.enableQTESystem != null)
        {
            Plugin.enableQTESystem.Value = true;
            Plugin.Log?.LogInfo("[GameSettingsMenu] Reset enableQTESystem to true");
        }
        
        // SP Gain
        if (Plugin.qteSPGainBase != null) { Plugin.qteSPGainBase.Value = 0.05f; Plugin.Log?.LogInfo("Reset qteSPGainBase to 0.05f"); }
        if (Plugin.qteSPGainMin != null) { Plugin.qteSPGainMin.Value = 0.02f; Plugin.Log?.LogInfo("Reset qteSPGainMin to 0.02f"); }
        if (Plugin.qteYellowButtonSPGainMin != null) { Plugin.qteYellowButtonSPGainMin.Value = 0.15f; Plugin.Log?.LogInfo("Reset qteYellowButtonSPGainMin to 0.15f"); }
        if (Plugin.qteYellowButtonSPGainMax != null) { Plugin.qteYellowButtonSPGainMax.Value = 0.3f; Plugin.Log?.LogInfo("Reset qteYellowButtonSPGainMax to 0.3f"); }
        
        // Penalties
        if (Plugin.qteMPPenaltyPercent != null) { Plugin.qteMPPenaltyPercent.Value = 0.3f; Plugin.Log?.LogInfo("Reset qteMPPenaltyPercent to 0.3f"); }
        if (Plugin.qteMindBrokenPenaltyPercent != null) { Plugin.qteMindBrokenPenaltyPercent.Value = 0.01f; Plugin.Log?.LogInfo("Reset qteMindBrokenPenaltyPercent to 0.01f"); }
        if (Plugin.qteRedButtonMindBrokenPenalty != null) { Plugin.qteRedButtonMindBrokenPenalty.Value = 0.02f; Plugin.Log?.LogInfo("Reset qteRedButtonMindBrokenPenalty to 0.02f"); }
        if (Plugin.qteSPPenaltyMultiplier != null) { Plugin.qteSPPenaltyMultiplier.Value = 2.0f; Plugin.Log?.LogInfo("Reset qteSPPenaltyMultiplier to 2.0f"); }
        
        // Timers
        if (Plugin.qteWindowDurationMin != null) { Plugin.qteWindowDurationMin.Value = 2f; Plugin.Log?.LogInfo("Reset qteWindowDurationMin to 2f"); }
        if (Plugin.qteWindowDurationMax != null) { Plugin.qteWindowDurationMax.Value = 3.5f; Plugin.Log?.LogInfo("Reset qteWindowDurationMax to 3.5f"); }
        if (Plugin.qteCooldownDurationMin != null) { Plugin.qteCooldownDurationMin.Value = 2f; Plugin.Log?.LogInfo("Reset qteCooldownDurationMin to 2f"); }
        if (Plugin.qteCooldownDurationMax != null) { Plugin.qteCooldownDurationMax.Value = 4f; Plugin.Log?.LogInfo("Reset qteCooldownDurationMax to 4f"); }
        
        // Visual Effects
        if (Plugin.qteRotationSpeed != null) { Plugin.qteRotationSpeed.Value = 90f; Plugin.Log?.LogInfo("Reset qteRotationSpeed to 90f"); }
        if (Plugin.qteRotationStartTime != null) { Plugin.qteRotationStartTime.Value = 0.5f; Plugin.Log?.LogInfo("Reset qteRotationStartTime to 0.5f"); }
        if (Plugin.qteColorChangeInterval != null) { Plugin.qteColorChangeInterval.Value = 1f; Plugin.Log?.LogInfo("Reset qteColorChangeInterval to 1f"); }
        if (Plugin.qtePressIndicatorDuration != null) { Plugin.qtePressIndicatorDuration.Value = 0.15f; Plugin.Log?.LogInfo("Reset qtePressIndicatorDuration to 0.15f"); }
        if (Plugin.qteMaxButtonTransparency != null) { Plugin.qteMaxButtonTransparency.Value = 0.5f; Plugin.Log?.LogInfo("Reset qteMaxButtonTransparency to 0.5f"); }
        if (Plugin.qteMaxPinkShadowIntensity != null) { Plugin.qteMaxPinkShadowIntensity.Value = 1f; Plugin.Log?.LogInfo("Reset qteMaxPinkShadowIntensity to 1f"); }
        
        // Sound
        if (Plugin.qteSuccessVolumeMultiplier != null) { Plugin.qteSuccessVolumeMultiplier.Value = 1.0f; Plugin.Log?.LogInfo("Reset qteSuccessVolumeMultiplier to 1.0f"); }
        if (Plugin.qteFailureVolumeMultiplier != null) { Plugin.qteFailureVolumeMultiplier.Value = 1.0f; Plugin.Log?.LogInfo("Reset qteFailureVolumeMultiplier to 1.0f"); }
        
        // Combo
        if (Plugin.qteComboMilestone != null) { Plugin.qteComboMilestone.Value = 10; Plugin.Log?.LogInfo("Reset qteComboMilestone to 10"); }
        
            Plugin.Log?.LogInfo("[GameSettingsMenu] Settings reset to defaults");
        
        // Обновляем UI - это ОБЯЗАТЕЛЬНО должно обновить слайдеры
        RefreshSettingsValues();
        
    }
    
    /// <summary>
    /// Простой компонент для активации InputField при клике и обработки ввода клавиатуры
    /// </summary>
    private class SimpleInputFieldActivator : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IDragHandler
    {
        public InputField inputField;
        private ScrollRect _scrollRect;
        private bool _isActivated = false;
        private GameObject _caretObj = null; // Визуальный курсор
        private UnityEngine.Coroutine _caretBlinkCoroutine = null;
        private int _caretPosition = 0; // Позиция курсора в тексте
        private int _selectionStart = 0; // Начало выделения
        private int _selectionEnd = 0; // Конец выделения
        private bool _isSelecting = false; // Флаг выделения
        
        // Статическая ссылка на текущий активный активатор (чтобы только один был активен)
        private static SimpleInputFieldActivator _currentActive = null;
        
        void Start()
        {
            Plugin.Log?.LogInfo($"[GameSettingsMenu] SimpleInputFieldActivator: Start called, inputField={(inputField != null ? inputField.name : "NULL")}, interactable={inputField?.interactable}");
            
            // Находим ScrollRect в родительской иерархии
            Transform parent = transform.parent;
            while (parent != null && _scrollRect == null)
            {
                _scrollRect = parent.GetComponentInParent<ScrollRect>();
                if (_scrollRect != null) break;
                parent = parent.parent;
            }
            
            if (_scrollRect != null)
            {
                Plugin.Log?.LogInfo($"[GameSettingsMenu] SimpleInputFieldActivator: Found ScrollRect: {_scrollRect.gameObject.name}");
            }
        }
        
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
        {
            Plugin.Log?.LogInfo($"[GameSettingsMenu] SimpleInputFieldActivator: OnPointerDown called, inputField={(inputField != null ? inputField.name : "NULL")}");
            
            // Определяем начальную позицию клика в тексте для начала выделения
            if (inputField != null && inputField.textComponent != null)
            {
                int clickPosition = GetCharacterIndexFromPosition(eventData.position);
                _caretPosition = clickPosition;
                _selectionStart = clickPosition;
                _selectionEnd = clickPosition;
                _isSelecting = false; // Начинаем выделение при drag
                UpdateCaretPosition();
                Plugin.Log?.LogInfo($"[GameSettingsMenu] SimpleInputFieldActivator: PointerDown position in text: {clickPosition}");
            }
            
            // Используем корутину для задержки активации
            StartCoroutine(ActivateInputFieldDelayed());
        }
        
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            Plugin.Log?.LogInfo($"[GameSettingsMenu] SimpleInputFieldActivator: OnPointerClick called, inputField={(inputField != null ? inputField.name : "NULL")}");
            
            // Определяем позицию клика в тексте
            if (inputField != null && inputField.textComponent != null)
            {
                int clickPosition = GetCharacterIndexFromPosition(eventData.position);
                _caretPosition = clickPosition;
                _selectionStart = clickPosition;
                _selectionEnd = clickPosition;
                _isSelecting = false;
                UpdateCaretPosition();
                Plugin.Log?.LogInfo($"[GameSettingsMenu] SimpleInputFieldActivator: Click position in text: {clickPosition}");
            }
            
            // Используем корутину для задержки активации
            StartCoroutine(ActivateInputFieldDelayed());
        }
        
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _isSelecting = false;
        }
        
        public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (inputField != null && inputField.textComponent != null && _isActivated)
            {
                _isSelecting = true;
                int dragPosition = GetCharacterIndexFromPosition(eventData.position);
                _selectionEnd = dragPosition;
                _caretPosition = dragPosition;
                UpdateCaretPosition();
            }
        }
        
        private int GetCharacterIndexFromPosition(Vector2 screenPosition)
        {
            if (inputField == null || inputField.textComponent == null) return 0;
            
            Text textComponent = inputField.textComponent;
            RectTransform textRect = textComponent.GetComponent<RectTransform>();
            if (textRect == null) return 0;
            
            // Конвертируем screen position в local position текста
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(textRect, screenPosition, null, out localPoint);
            
            // Получаем текст
            string text = inputField.text;
            if (string.IsNullOrEmpty(text)) return 0;
            
            // Вычисляем позицию символа на основе ширины текста
            // Для левого выравнивания начинаем с левого края
            float charWidth = textComponent.fontSize * 0.6f; // Примерная ширина символа
            float textStartX = -textRect.rect.width / 2f + 2f; // Левое начало текста с отступом
            
            // Находим индекс символа
            int charIndex = Mathf.RoundToInt((localPoint.x - textStartX) / charWidth);
            charIndex = Mathf.Clamp(charIndex, 0, text.Length);
            
            return charIndex;
        }
        
        private System.Collections.IEnumerator ActivateInputFieldDelayed()
        {
            // Ждем конец кадра, чтобы другие обработчики событий завершились
            yield return null;
            yield return null; // Дополнительная задержка для надежности
            
            ActivateInputField();
            
            // Проверяем фокус через некоторое время
            yield return new WaitForSeconds(0.1f);
            if (inputField != null)
            {
                Plugin.Log?.LogInfo($"[GameSettingsMenu] SimpleInputFieldActivator: After 0.1s delay, isFocused={inputField.isFocused}, _isActivated={_isActivated}");
                
                // Если фокус потерян, пытаемся активировать снова
                if (!inputField.isFocused && inputField.interactable && !_isActivated)
                {
                    Plugin.Log?.LogInfo($"[GameSettingsMenu] SimpleInputFieldActivator: InputField lost focus, retrying activation...");
                    ActivateInputField();
                }
            }
        }
        
        void OnDisable()
        {
            // Деактивируем при отключении
            Deactivate();
        }
        
        void OnDestroy()
        {
            // Деактивируем при уничтожении
            DestroyCaret();
            if (_currentActive == this)
            {
                _currentActive = null;
            }
            // ScrollRect больше не отключается, поэтому не нужно его включать обратно
        }
        
        private void ActivateInputField()
        {
            if (inputField != null && inputField.interactable)
            {
                Plugin.Log?.LogInfo($"[GameSettingsMenu] SimpleInputFieldActivator: Activating InputField {inputField.name}, isFocused={inputField.isFocused}");
                
                // Деактивируем предыдущий активный активатор
                if (_currentActive != null && _currentActive != this)
                {
                    Plugin.Log?.LogInfo($"[GameSettingsMenu] SimpleInputFieldActivator: Deactivating previous active InputField {_currentActive.inputField?.name}");
                    _currentActive.Deactivate();
                }
                
                // Устанавливаем этот активатор как текущий активный
                _currentActive = this;
                
                // НЕ отключаем ScrollRect, так как это блокирует скролл навсегда
                // Вместо этого используем другой подход - обрабатываем события клавиатуры напрямую
                // ScrollRect будет работать нормально, когда InputField не в фокусе
                
                // В Unity 5.6 на Windows нужно явно включить IME composition mode для ввода текста
                UnityEngine.Input.imeCompositionMode = UnityEngine.IMECompositionMode.On;
                
                // Устанавливаем выделенный объект ПЕРЕД активацией
                if (UnityEngine.EventSystems.EventSystem.current != null)
                {
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(inputField.gameObject, null);
                }
                
                // Прямой вызов активации
                inputField.ActivateInputField();
                
                // Также пробуем Select
                inputField.Select();
                
                // Принудительно обновляем Canvas
                UnityEngine.Canvas.ForceUpdateCanvases();
                
                // Устанавливаем флаг активации для обработки ввода
                _isActivated = true;
                
                // Инициализируем позицию курсора в конец текста (если не была установлена кликом)
                if (_caretPosition == 0 && string.IsNullOrEmpty(inputField.text) == false)
                {
                    _caretPosition = inputField.text.Length;
                }
                _selectionStart = _caretPosition;
                _selectionEnd = _caretPosition;
                _isSelecting = false;
                
                // Создаем визуальный курсор
                CreateCaret();
                UpdateCaretPosition();
                
                Plugin.Log?.LogInfo($"[GameSettingsMenu] SimpleInputFieldActivator: After activation, isFocused={inputField.isFocused}, textComponent={(inputField.textComponent != null ? "OK" : "NULL")}, _isActivated={_isActivated}");
            }
            else
            {
                Plugin.Log?.LogWarning($"[GameSettingsMenu] SimpleInputFieldActivator: Cannot activate - inputField={(inputField != null ? inputField.name : "NULL")}, interactable={inputField?.interactable}");
            }
        }
        
        private void Deactivate()
        {
            Plugin.Log?.LogInfo($"[GameSettingsMenu] SimpleInputFieldActivator: Deactivating InputField {inputField?.name}");
            _isActivated = false;
            
            // Уничтожаем курсор
            DestroyCaret();
            
            // Если это был текущий активный, сбрасываем ссылку
            if (_currentActive == this)
            {
                _currentActive = null;
            }
            
            // ScrollRect больше не отключается, поэтому не нужно его включать обратно
        }
        
        void Update()
        {
            // Обрабатываем ввод только если это текущий активный активатор
            if (_currentActive == this && _isActivated && inputField != null && inputField.textComponent != null)
            {
                // Проверяем, что InputField все еще выделен (для дополнительной безопасности)
                bool isSelected = UnityEngine.EventSystems.EventSystem.current != null && 
                                 UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == inputField.gameObject;
                
                if (isSelected || inputField.isFocused)
                {
                    // Обрабатываем основные клавиши
                    if (UnityEngine.Input.inputString.Length > 0)
                    {
                        string inputString = UnityEngine.Input.inputString;
                        Plugin.Log?.LogInfo($"[GameSettingsMenu] SimpleInputFieldActivator: Received input: '{inputString}' for {inputField.name}");
                        
                        // Обрабатываем ввод с учетом выделения и позиции курсора
                        string currentText = inputField.text;
                        
                        // Если есть выделение, заменяем выделенный текст
                        if (_isSelecting && _selectionStart != _selectionEnd)
                        {
                            int start = Mathf.Min(_selectionStart, _selectionEnd);
                            int end = Mathf.Max(_selectionStart, _selectionEnd);
                            currentText = currentText.Substring(0, start) + inputString + currentText.Substring(end);
                            _caretPosition = start + inputString.Length;
                        }
                        else
                        {
                            // Вставляем в позицию курсора
                            _caretPosition = Mathf.Clamp(_caretPosition, 0, currentText.Length);
                            currentText = currentText.Insert(_caretPosition, inputString);
                            _caretPosition += inputString.Length;
                        }
                        
                        inputField.text = currentText;
                        _selectionStart = _caretPosition;
                        _selectionEnd = _caretPosition;
                        _isSelecting = false;
                        
                        // Обновляем позицию курсора
                        UpdateCaretPosition();
                        
                        // Активируем InputField снова, чтобы обновить отображение
                        inputField.ActivateInputField();
                    }
                    
                    // Обрабатываем точку на NumLock (KeypadPeriod)
                    if (UnityEngine.Input.GetKeyDown(KeyCode.KeypadPeriod))
                    {
                        string currentText = inputField.text;
                        
                        // Если есть выделение, заменяем выделенный текст
                        if (_isSelecting && _selectionStart != _selectionEnd)
                        {
                            int start = Mathf.Min(_selectionStart, _selectionEnd);
                            int end = Mathf.Max(_selectionStart, _selectionEnd);
                            currentText = currentText.Substring(0, start) + "." + currentText.Substring(end);
                            _caretPosition = start + 1;
                        }
                        else
                        {
                            // Вставляем точку в позицию курсора
                            _caretPosition = Mathf.Clamp(_caretPosition, 0, currentText.Length);
                            currentText = currentText.Insert(_caretPosition, ".");
                            _caretPosition++;
                        }
                        
                        inputField.text = currentText;
                        _selectionStart = _caretPosition;
                        _selectionEnd = _caretPosition;
                        _isSelecting = false;
                        UpdateCaretPosition();
                        inputField.ActivateInputField();
                    }
                    
                    // Обрабатываем Backspace
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Backspace))
                    {
                        string currentText = inputField.text;
                        if (currentText.Length > 0)
                        {
                            // Если есть выделение, удаляем выделенный текст
                            if (_isSelecting && _selectionStart != _selectionEnd)
                            {
                                int start = Mathf.Min(_selectionStart, _selectionEnd);
                                int end = Mathf.Max(_selectionStart, _selectionEnd);
                                currentText = currentText.Substring(0, start) + currentText.Substring(end);
                                _caretPosition = start;
                            }
                            else if (_caretPosition > 0)
                            {
                                // Удаляем символ перед курсором
                                currentText = currentText.Substring(0, _caretPosition - 1) + currentText.Substring(_caretPosition);
                                _caretPosition--;
                            }
                            
                            inputField.text = currentText;
                            _selectionStart = _caretPosition;
                            _selectionEnd = _caretPosition;
                            _isSelecting = false;
                            UpdateCaretPosition();
                            inputField.ActivateInputField();
                        }
                    }
                    
                    // Обрабатываем Delete
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Delete))
                    {
                        string currentText = inputField.text;
                        if (currentText.Length > 0)
                        {
                            // Если есть выделение, удаляем выделенный текст
                            if (_isSelecting && _selectionStart != _selectionEnd)
                            {
                                int start = Mathf.Min(_selectionStart, _selectionEnd);
                                int end = Mathf.Max(_selectionStart, _selectionEnd);
                                currentText = currentText.Substring(0, start) + currentText.Substring(end);
                                _caretPosition = start;
                            }
                            else if (_caretPosition < currentText.Length)
                            {
                                // Удаляем символ после курсора
                                currentText = currentText.Substring(0, _caretPosition) + currentText.Substring(_caretPosition + 1);
                            }
                            
                            inputField.text = currentText;
                            _selectionStart = _caretPosition;
                            _selectionEnd = _caretPosition;
                            _isSelecting = false;
                            UpdateCaretPosition();
                            inputField.ActivateInputField();
                        }
                    }
                    
                    // Обрабатываем стрелки для перемещения курсора
                    if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow))
                    {
                        _caretPosition = Mathf.Max(0, _caretPosition - 1);
                        _selectionStart = _caretPosition;
                        _selectionEnd = _caretPosition;
                        _isSelecting = false;
                        UpdateCaretPosition();
                    }
                    
                    if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
                    {
                        _caretPosition = Mathf.Min(inputField.text.Length, _caretPosition + 1);
                        _selectionStart = _caretPosition;
                        _selectionEnd = _caretPosition;
                        _isSelecting = false;
                        UpdateCaretPosition();
                    }
                }
                else
                {
                    // Если InputField больше не выделен, деактивируем этот активатор
                    Deactivate();
                }
            }
        }
        
        private void CreateCaret()
        {
            if (_caretObj != null || inputField == null || inputField.textComponent == null) return;
            
            // Создаем визуальный курсор
            _caretObj = new GameObject("Caret");
            _caretObj.transform.SetParent(inputField.textComponent.transform, false);
            
            RectTransform caretRect = _caretObj.AddComponent<RectTransform>();
            caretRect.sizeDelta = new Vector2(2f, inputField.textComponent.fontSize * 1.2f);
            caretRect.pivot = new Vector2(0f, 0.5f); // Левая сторона курсора для точного позиционирования
            caretRect.anchorMin = new Vector2(0f, 0.5f);
            caretRect.anchorMax = new Vector2(0f, 0.5f);
            caretRect.anchoredPosition = new Vector2(2f, 0f); // Небольшой отступ слева для пустого поля
            
            UnityEngine.UI.Image caretImage = _caretObj.AddComponent<UnityEngine.UI.Image>();
            caretImage.color = Color.white;
            
            // Запускаем мигание курсора
            _caretBlinkCoroutine = StartCoroutine(CaretBlinkCoroutine());
        }
        
        private void UpdateCaretPosition()
        {
            if (_caretObj == null || inputField == null || inputField.textComponent == null) return;
            
            RectTransform caretRect = _caretObj.GetComponent<RectTransform>();
            RectTransform textRect = inputField.textComponent.GetComponent<RectTransform>();
            if (caretRect == null || textRect == null) return;
            
            // Позиционируем курсор в конец текста
            Text textComponent = inputField.textComponent;
            string text = inputField.text;
            
            // Принудительно обновляем Canvas для правильного расчета preferredWidth
            UnityEngine.Canvas.ForceUpdateCanvases();
            
            // Убеждаемся, что позиция курсора в допустимых пределах
            _caretPosition = Mathf.Clamp(_caretPosition, 0, text.Length);
            
            if (string.IsNullOrEmpty(text) || _caretPosition == 0)
            {
                // Если текста нет или курсор в начале, позиционируем курсор слева с небольшим отступом
                caretRect.anchoredPosition = new Vector2(2f, 0f);
            }
            else
            {
                // Вычисляем ширину текста до позиции курсора
                string textBeforeCaret = text.Substring(0, _caretPosition);
                float textWidth = 0f;
                
                // Метод 1: Используем preferredWidth для текста до курсора
                try
                {
                    TextGenerator generator = new TextGenerator();
                    TextGenerationSettings settings = textComponent.GetGenerationSettings(textRect.rect.size);
                    settings.generateOutOfBounds = true;
                    settings.scaleFactor = 1f;
                    
                    textWidth = generator.GetPreferredWidth(textBeforeCaret, settings);
                    
                    // Если не получилось, используем простой расчет
                    if (textWidth <= 0f || float.IsNaN(textWidth) || float.IsInfinity(textWidth))
                    {
                        // Примерная ширина символа для Arial и цифр
                        float charWidth = textComponent.fontSize * 0.6f;
                        textWidth = textBeforeCaret.Length * charWidth;
                    }
                }
                catch
                {
                    // Fallback: простой расчет
                    float charWidth = textComponent.fontSize * 0.6f;
                    textWidth = textBeforeCaret.Length * charWidth;
                }
                
                // Позиционируем курсор в указанной позиции
                // Текст выровнен по левому краю (TextAnchor.MiddleLeft)
                caretRect.anchoredPosition = new Vector2(textWidth + 2f, 0f);
            }
        }
        
        private System.Collections.IEnumerator CaretBlinkCoroutine()
        {
            if (_caretObj == null) yield break;
            
            UnityEngine.UI.Image caretImage = _caretObj.GetComponent<UnityEngine.UI.Image>();
            if (caretImage == null) yield break;
            
            while (_currentActive == this && _isActivated && _caretObj != null)
            {
                // Показываем курсор
                caretImage.color = new Color(1f, 1f, 1f, 1f);
                yield return new WaitForSeconds(0.5f);
                
                if (_currentActive != this || !_isActivated || _caretObj == null) break;
                
                // Скрываем курсор
                caretImage.color = new Color(1f, 1f, 1f, 0f);
                yield return new WaitForSeconds(0.5f);
            }
        }
        
        private void DestroyCaret()
        {
            if (_caretBlinkCoroutine != null)
            {
                StopCoroutine(_caretBlinkCoroutine);
                _caretBlinkCoroutine = null;
            }
            
            if (_caretObj != null)
            {
                UnityEngine.Object.Destroy(_caretObj);
                _caretObj = null;
            }
        }
    }
    
    /// <summary>
    /// Вспомогательный класс для активации InputField при клике (старая версия, не используется)
    /// </summary>
    private class InputFieldActivator : MonoBehaviour
    {
        private InputField _inputField;
        
        public void SetInputField(InputField inputField)
        {
            _inputField = inputField;
            Plugin.Log?.LogInfo($"[GameSettingsMenu] InputFieldActivator: InputField set, interactable={inputField?.interactable}");
            
            // Используем EventTrigger вместо IPointerClickHandler
            UnityEngine.EventSystems.EventTrigger trigger = gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            
            // PointerClick
            UnityEngine.EventSystems.EventTrigger.Entry pointerClickEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerClickEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick;
            pointerClickEntry.callback.AddListener((eventData) =>
            {
                Plugin.Log?.LogInfo($"[GameSettingsMenu] InputFieldActivator: PointerClick event triggered");
                StartCoroutine(ActivateInputFieldDelayed());
            });
            trigger.triggers.Add(pointerClickEntry);
            
            // PointerDown
            UnityEngine.EventSystems.EventTrigger.Entry pointerDownEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerDownEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
            pointerDownEntry.callback.AddListener((eventData) =>
            {
                Plugin.Log?.LogInfo($"[GameSettingsMenu] InputFieldActivator: PointerDown event triggered");
                StartCoroutine(ActivateInputFieldDelayed());
            });
            trigger.triggers.Add(pointerDownEntry);
        }
        
        private System.Collections.IEnumerator ActivateInputFieldDelayed()
        {
            // Ждем конец кадра перед активацией
            yield return null;
            ActivateInputField();
        }
        
        private void ActivateInputField()
        {
            if (_inputField != null && _inputField.interactable)
            {
                Plugin.Log?.LogInfo($"[GameSettingsMenu] InputFieldActivator: Activating InputField... isFocused={_inputField.isFocused}");
                try
                {
                    // Убеждаемся, что textComponent установлен
                    if (_inputField.textComponent == null)
                    {
                        Plugin.Log?.LogError($"[GameSettingsMenu] InputFieldActivator: textComponent is null!");
                        return;
                    }
                    
                    // Устанавливаем выделенный объект ПЕРЕД активацией
                    if (UnityEngine.EventSystems.EventSystem.current != null)
                    {
                        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(_inputField.gameObject, null);
                        Plugin.Log?.LogInfo($"[GameSettingsMenu] InputFieldActivator: SetSelectedGameObject called");
                        
                        // Принудительно обновляем EventSystem
                        UnityEngine.EventSystems.EventSystem.current.UpdateModules();
                    }
                    
                    // Пробуем активировать через прямой вызов
                    _inputField.OnSelect(null); // Вызываем OnSelect вручную
                    _inputField.Select(); // Select
                    UnityEngine.Canvas.ForceUpdateCanvases(); // Обновляем Canvas
                    
                    // Затем ActivateInputField
                    _inputField.ActivateInputField();
                    
                    Plugin.Log?.LogInfo($"[GameSettingsMenu] InputFieldActivator: After activation, isFocused={_inputField.isFocused}, isActiveAndEnabled={_inputField.isActiveAndEnabled}");
                    
                    // Если не сработало, пробуем еще раз через небольшую задержку
                    StartCoroutine(RetryActivation());
                }
                catch (System.Exception ex)
                {
                    Plugin.Log?.LogError($"[GameSettingsMenu] InputFieldActivator: Error activating InputField: {ex.Message}\n{ex.StackTrace}");
                }
            }
            else
            {
                Plugin.Log?.LogWarning($"[GameSettingsMenu] InputFieldActivator: Cannot activate - inputField={_inputField != null}, interactable={_inputField?.interactable}");
            }
        }
        
        private System.Collections.IEnumerator RetryActivation()
        {
            yield return new WaitForSeconds(0.05f);
            if (_inputField != null && _inputField.interactable)
            {
                Plugin.Log?.LogInfo($"[GameSettingsMenu] InputFieldActivator: Retrying activation... isFocused={_inputField.isFocused}");
                if (!_inputField.isFocused)
                {
                    _inputField.Select();
                    _inputField.ActivateInputField();
                    
                    // Проверяем результат
                    yield return new WaitForSeconds(0.05f);
                    Plugin.Log?.LogInfo($"[GameSettingsMenu] InputFieldActivator: After retry, isFocused={_inputField.isFocused}");
                }
            }
        }
    }
    
    /// <summary>
    /// Вспомогательный класс для корутин
    /// </summary>
    private class SettingsMenuRunner : MonoBehaviour
    {
        public void StartFlashGreen(List<GameObject> containers)
        {
            StartCoroutine(FlashGreenCoroutine(containers));
        }
        
        private System.Collections.IEnumerator FlashGreenCoroutine(List<GameObject> containers)
        {
            // Сохраняем оригинальные цвета и временно меняем на зеленый
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
                containerImage.color = new Color(1f, 1f, 1f, 0.2f); // Белый с небольшой прозрачностью
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
        }
    }
    
    /// <summary>
    /// Структура элемента настройки
    /// </summary>
    private class SettingElement
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
        public GameObject? borderObj; // Border для визуализации изменений
        public Image? borderImage; // Image компонент border
        public float originalValue; // Оригинальное значение для отслеживания изменений
        public bool isChanged; // Флаг изменения значения
    }
}

