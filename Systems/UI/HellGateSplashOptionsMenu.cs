using System;
using NoREroMod.Systems.Difficulty;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NoREroMod.Systems.UI;

/// <summary>
/// Splash Options: plain "Options" text; panel under it with Gore, Simple QTE,
/// Difficulty, Language submenu (2-column grid), Done / Exit.
/// </summary>
internal static class HellGateSplashOptionsMenu
{
    // Keep short so Options label stays below the adult-content warning (esp. JP wrap).
    internal const float PanelHeight = 286f;
    internal const float PanelBottomMargin = 20f;
    internal const float PanelGapUnderLabel = 4f;

    private static GameObject _optionsLabelRoot;
    private static GameObject _panelRoot;
    private static GameObject _languagePanelRoot;
    private static GameObject _startButtonRoot;
    private static Text _restartNotice;
    private static Toggle _goreToggle;
    private static bool _goreSyncing;
    private static Toggle _simpleQteToggle;
    private static bool _simpleQteSyncing;
    private static Image _easyBtnBg;
    private static Image _mediumBtnBg;
    private static Image _hardBtnBg;
    private static HellGateDifficultyPresetModule.DifficultyLevel _current;
    private static bool _panelOpen;
    private static bool _languagePanelOpen;
    private static string _languageCode = "EN";
    private static string _goreLabel = "HellGate Gore Content";
    private static float _optionsBottomY;

    private static Sprite _whiteSprite;

    internal static void Create(
        GameObject container,
        string goreContentLabel,
        GameObject startButtonRoot,
        float optionsRowY,
        float optionsRowH,
        string languageCode)
    {
        _optionsLabelRoot = null;
        _panelRoot = null;
        _languagePanelRoot = null;
        _startButtonRoot = startButtonRoot;
        _restartNotice = null;
        _goreToggle = null;
        _goreSyncing = false;
        _simpleQteToggle = null;
        _simpleQteSyncing = false;
        _easyBtnBg = null;
        _mediumBtnBg = null;
        _hardBtnBg = null;
        _panelOpen = false;
        _languagePanelOpen = false;
        _optionsBottomY = optionsRowY;
        _languageCode = string.IsNullOrEmpty(languageCode) ? "EN" : languageCode;
        _current = HellGateDifficultyPresetModule.GetSelected();
        _goreLabel = string.IsNullOrEmpty(goreContentLabel)
            ? "HellGate Gore Content"
            : goreContentLabel;

        Sprite white = GetOrCreateWhiteSprite();
        Font font = HellGateFontProvider.GetUiFont();

        CreateOptionsLabel(container, optionsRowY, optionsRowH, font, white);
        CreateOptionsPanel(container, font, white);
        CreateLanguagePanel(container, font, white);
        SetPanelOpen(false);
        SetLanguagePanelOpen(false);

        if (_optionsLabelRoot != null)
            _optionsLabelRoot.SetActive(false);
    }

    private static Sprite GetOrCreateWhiteSprite()
    {
        if (_whiteSprite != null)
            return _whiteSprite;
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        return _whiteSprite;
    }

    private static void CreateOptionsLabel(
        GameObject container,
        float anchoredY,
        float rowH,
        Font font,
        Sprite white)
    {
        const float btnW = 260f;
        float btnH = Mathf.Max(40f, rowH);

        _optionsLabelRoot = new GameObject("OptionsLabel_XUAIGNORE");
        _optionsLabelRoot.transform.SetParent(container.transform, false);
        RectTransform rt = _optionsLabelRoot.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, anchoredY);
        rt.sizeDelta = new Vector2(btnW, btnH);

        Image hit = _optionsLabelRoot.AddComponent<Image>();
        hit.sprite = white;
        hit.color = new Color(1f, 1f, 1f, 0.001f);
        hit.raycastTarget = true;

        Button btn = _optionsLabelRoot.AddComponent<Button>();
        btn.targetGraphic = hit;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.fadeDuration = 0f;
        btn.colors = colors;
        btn.onClick.AddListener(TogglePanel);

        GameObject textObj = new GameObject("Label_XUAIGNORE");
        textObj.transform.SetParent(_optionsLabelRoot.transform, false);
        Text text = textObj.AddComponent<Text>();
        text.text = SplashScreenUILabels.GetOptionsLabel(_languageCode);
        text.font = font;
        text.fontSize = 28;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.92f, 0.18f, 0.18f, 1f);
        text.raycastTarget = false;
        StretchFull(textObj);

        EventTrigger trigger = _optionsLabelRoot.AddComponent<EventTrigger>();
        EventTrigger.Entry enter = new EventTrigger.Entry();
        enter.eventID = EventTriggerType.PointerEnter;
        enter.callback.AddListener(_ =>
        {
            if (_optionsLabelRoot != null)
                _optionsLabelRoot.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
            text.color = new Color(1f, 0.32f, 0.28f, 1f);
        });
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry();
        exit.eventID = EventTriggerType.PointerExit;
        exit.callback.AddListener(_ =>
        {
            if (_optionsLabelRoot != null)
                _optionsLabelRoot.transform.localScale = Vector3.one;
            text.color = new Color(0.92f, 0.18f, 0.18f, 1f);
        });
        trigger.triggers.Add(exit);
    }

    private static void CreateOptionsPanel(GameObject container, Font font, Sprite white)
    {
        const float panelW = 640f;
        float panelTopY = _optionsBottomY - PanelGapUnderLabel;

        _panelRoot = new GameObject("SplashOptionsPanel_XUAIGNORE");
        _panelRoot.transform.SetParent(container.transform, false);
        RectTransform panelRt = _panelRoot.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0f);
        panelRt.anchorMax = new Vector2(0.5f, 0f);
        panelRt.pivot = new Vector2(0.5f, 1f);
        panelRt.anchoredPosition = new Vector2(0f, panelTopY);
        panelRt.sizeDelta = new Vector2(panelW, PanelHeight);

        Image panelBg = _panelRoot.AddComponent<Image>();
        panelBg.sprite = white;
        panelBg.color = new Color(0.06f, 0.06f, 0.07f, 0.96f);
        panelBg.raycastTarget = true;

        float y = -10f;

        y = PlaceGoreRow(_panelRoot, font, white, y);
        y -= 4f;
        y = PlaceSimpleQteRow(_panelRoot, font, white, y);
        y -= 8f;
        y = PlaceDifficultyBlock(_panelRoot, font, white, y);
        y -= 6f;
        y = PlaceRestartNotice(_panelRoot, font, y);
        y -= 8f;
        PlaceDoneLanguageExitRow(_panelRoot, font, white, y);
    }

    private static void CreateLanguagePanel(GameObject container, Font font, Sprite white)
    {
        const float panelW = 560f;
        const float panelH = 430f;

        _languagePanelRoot = new GameObject("SplashLanguagePanel_XUAIGNORE");
        _languagePanelRoot.transform.SetParent(container.transform, false);
        RectTransform panelRt = _languagePanelRoot.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = new Vector2(0f, 40f);
        panelRt.sizeDelta = new Vector2(panelW, panelH);

        Image panelBg = _languagePanelRoot.AddComponent<Image>();
        panelBg.sprite = white;
        panelBg.color = new Color(0.06f, 0.06f, 0.07f, 0.96f);
        panelBg.raycastTarget = true;

        float y = -18f;

        GameObject warnObj = new GameObject("LanguageRestartWarning_XUAIGNORE");
        warnObj.transform.SetParent(_languagePanelRoot.transform, false);
        Text warn = warnObj.AddComponent<Text>();
        warn.text = SplashScreenUILabels.GetLanguageRestartNotice(_languageCode);
        warn.font = font;
        warn.fontSize = 16;
        warn.fontStyle = FontStyle.Bold;
        warn.alignment = TextAnchor.MiddleCenter;
        warn.color = new Color(1f, 0.55f, 0.2f, 1f);
        warn.horizontalOverflow = HorizontalWrapMode.Wrap;
        warn.verticalOverflow = VerticalWrapMode.Overflow;
        warn.raycastTarget = false;
        RectTransform warnRt = warnObj.GetComponent<RectTransform>();
        warnRt.anchorMin = new Vector2(0.5f, 1f);
        warnRt.anchorMax = new Vector2(0.5f, 1f);
        warnRt.pivot = new Vector2(0.5f, 1f);
        warnRt.anchoredPosition = new Vector2(0f, y);
        warnRt.sizeDelta = new Vector2(520f, 48f);
        y -= 56f;

        const float btnW = 230f;
        const float btnH = 40f;
        const float gapX = 16f;
        const float gapY = 10f;
        string[] codes = SplashScreenUILabels.LanguageCodesInGridOrder;
        float gridW = btnW * 2f + gapX;
        float xLeft = -gridW * 0.5f + btnW * 0.5f;
        float xRight = xLeft + btnW + gapX;

        for (int i = 0; i < codes.Length; i++)
        {
            int col = i % 2;
            int row = i / 2;
            float x = col == 0 ? xLeft : xRight;
            float rowTop = y - row * (btnH + gapY);
            string code = codes[i];
            PlaceLanguageChoiceButton(
                _languagePanelRoot,
                code,
                new Vector2(x, rowTop),
                btnW,
                btnH,
                font,
                white);
        }

        int rows = (codes.Length + 1) / 2;
        float afterGrid = y - rows * btnH - (rows - 1) * gapY - 18f;

        PlaceActionButton(
            _languagePanelRoot,
            "CancelButton_XUAIGNORE",
            SplashScreenUILabels.GetCancelLabel(_languageCode),
            new Color(0.85f, 0.85f, 0.88f, 1f),
            new Vector2(0f, afterGrid),
            160f,
            40f,
            font,
            white,
            CloseLanguagePanel);
    }

    private static void PlaceLanguageChoiceButton(
        GameObject panel,
        string languageCode,
        Vector2 topCenter,
        float btnW,
        float btnH,
        Font font,
        Sprite white)
    {
        bool selected = string.Equals(languageCode, _languageCode, StringComparison.OrdinalIgnoreCase);
        Color accent = selected
            ? new Color(0.95f, 0.35f, 0.3f, 1f)
            : new Color(0.95f, 0.93f, 0.88f, 1f);

        GameObject btnObj = new GameObject("Lang_" + languageCode + "_XUAIGNORE");
        btnObj.transform.SetParent(panel.transform, false);
        Image bg = btnObj.AddComponent<Image>();
        bg.sprite = white;
        bg.color = selected
            ? new Color(0.35f, 0.12f, 0.12f, 1f)
            : new Color(0.14f, 0.14f, 0.16f, 1f);
        Button button = btnObj.AddComponent<Button>();
        button.targetGraphic = bg;

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = topCenter;
        rt.sizeDelta = new Vector2(btnW, btnH);

        GameObject textObj = new GameObject("Label_XUAIGNORE");
        textObj.transform.SetParent(btnObj.transform, false);
        Text text = textObj.AddComponent<Text>();
        text.text = SplashScreenUILabels.GetLanguageDisplayName(languageCode);
        text.font = font;
        text.fontSize = 20;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = accent;
        text.raycastTarget = false;
        StretchFull(textObj);

        string captured = languageCode;
        button.onClick.AddListener(() => ApplyLanguageAndQuit(captured));
    }

    private static float PlaceGoreRow(GameObject panel, Font font, Sprite white, float topY)
    {
        const float checkSize = 28f;
        const float rowH = 34f;
        const float gap = 10f;

        GameObject row = new GameObject("GoreRow");
        row.transform.SetParent(panel.transform, false);
        RectTransform rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 1f);
        rowRt.anchorMax = new Vector2(0.5f, 1f);
        rowRt.pivot = new Vector2(0.5f, 1f);
        rowRt.anchoredPosition = new Vector2(0f, topY);
        rowRt.sizeDelta = new Vector2(560f, rowH);

        GameObject toggleObj = new GameObject("Toggle");
        toggleObj.transform.SetParent(row.transform, false);
        _goreToggle = toggleObj.AddComponent<Toggle>();
        _goreToggle.isOn = Plugin.IsGoreContentEnabled;
        RectTransform toggleRt = toggleObj.GetComponent<RectTransform>();
        toggleRt.anchorMin = new Vector2(0f, 0.5f);
        toggleRt.anchorMax = new Vector2(0f, 0.5f);
        toggleRt.pivot = new Vector2(0f, 0.5f);
        toggleRt.sizeDelta = new Vector2(checkSize, checkSize);

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(toggleObj.transform, false);
        Image bg = bgObj.AddComponent<Image>();
        bg.sprite = white;
        bg.color = new Color(0.15f, 0.15f, 0.17f, 0.95f);
        StretchFull(bgObj);

        GameObject checkObj = new GameObject("Checkmark");
        checkObj.transform.SetParent(bgObj.transform, false);
        Image check = checkObj.AddComponent<Image>();
        check.sprite = white;
        check.color = new Color(0.85f, 0.2f, 0.22f, 1f);
        RectTransform checkRt = checkObj.GetComponent<RectTransform>();
        checkRt.anchorMin = new Vector2(0.18f, 0.18f);
        checkRt.anchorMax = new Vector2(0.82f, 0.82f);
        checkRt.offsetMin = Vector2.zero;
        checkRt.offsetMax = Vector2.zero;

        _goreToggle.targetGraphic = bg;
        _goreToggle.graphic = check;

        GameObject labelObj = new GameObject("GoreLabel_XUAIGNORE");
        labelObj.transform.SetParent(row.transform, false);
        Text label = labelObj.AddComponent<Text>();
        label.text = _goreLabel;
        label.font = font;
        label.fontSize = 20;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = new Color(0.95f, 0.93f, 0.88f, 1f);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        float labelW = Mathf.Clamp(label.preferredWidth + 8f, 160f, 480f);
        RectTransform labelRt = labelObj.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 0.5f);
        labelRt.anchorMax = new Vector2(0f, 0.5f);
        labelRt.pivot = new Vector2(0f, 0.5f);
        labelRt.sizeDelta = new Vector2(labelW, rowH);

        float contentW = checkSize + gap + labelW;
        float startX = (560f - contentW) * 0.5f;
        toggleRt.anchoredPosition = new Vector2(startX, 0f);
        labelRt.anchoredPosition = new Vector2(startX + checkSize + gap, 0f);

        _goreToggle.onValueChanged.AddListener(isOn =>
        {
            if (_goreSyncing)
                return;

            if (Plugin.enableGoreContent != null)
                Plugin.enableGoreContent.Value = isOn;

            if (!isOn)
                return;

            try
            {
                NoREroMod.Patches.HellTraps.LethalMagicTrapRuntime.TryEnsureTemplateRegistered();
                NoREroMod.Patches.HellTraps.LethalCocoonTrapRuntime.TryEnsureTemplateRegistered();
                NoREroMod.Patches.HellTraps.LethalLightningTrapRuntime.TryEnsureTemplateRegistered();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[Gore] Lethal trap re-register failed: " + ex.Message);
            }
        });

        return topY - rowH;
    }

    private static float PlaceSimpleQteRow(GameObject panel, Font font, Sprite white, float topY)
    {
        const float checkSize = 28f;
        const float rowH = 34f;
        const float gap = 10f;

        GameObject row = new GameObject("SimpleQteRow");
        row.transform.SetParent(panel.transform, false);
        RectTransform rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.5f, 1f);
        rowRt.anchorMax = new Vector2(0.5f, 1f);
        rowRt.pivot = new Vector2(0.5f, 1f);
        rowRt.anchoredPosition = new Vector2(0f, topY);
        rowRt.sizeDelta = new Vector2(560f, rowH);

        GameObject toggleObj = new GameObject("Toggle");
        toggleObj.transform.SetParent(row.transform, false);
        _simpleQteToggle = toggleObj.AddComponent<Toggle>();
        _simpleQteToggle.isOn = Plugin.qteFreeStruggleEnable != null && Plugin.qteFreeStruggleEnable.Value;
        RectTransform toggleRt = toggleObj.GetComponent<RectTransform>();
        toggleRt.anchorMin = new Vector2(0f, 0.5f);
        toggleRt.anchorMax = new Vector2(0f, 0.5f);
        toggleRt.pivot = new Vector2(0f, 0.5f);
        toggleRt.sizeDelta = new Vector2(checkSize, checkSize);

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(toggleObj.transform, false);
        Image bg = bgObj.AddComponent<Image>();
        bg.sprite = white;
        bg.color = new Color(0.15f, 0.15f, 0.17f, 0.95f);
        StretchFull(bgObj);

        GameObject checkObj = new GameObject("Checkmark");
        checkObj.transform.SetParent(bgObj.transform, false);
        Image check = checkObj.AddComponent<Image>();
        check.sprite = white;
        check.color = new Color(0.85f, 0.2f, 0.22f, 1f);
        RectTransform checkRt = checkObj.GetComponent<RectTransform>();
        checkRt.anchorMin = new Vector2(0.18f, 0.18f);
        checkRt.anchorMax = new Vector2(0.82f, 0.82f);
        checkRt.offsetMin = Vector2.zero;
        checkRt.offsetMax = Vector2.zero;

        _simpleQteToggle.targetGraphic = bg;
        _simpleQteToggle.graphic = check;

        GameObject labelObj = new GameObject("SimpleQteLabel_XUAIGNORE");
        labelObj.transform.SetParent(row.transform, false);
        Text label = labelObj.AddComponent<Text>();
        label.text = SplashScreenUILabels.GetSimpleQteLabel(_languageCode);
        label.font = font;
        label.fontSize = 20;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = new Color(0.95f, 0.93f, 0.88f, 1f);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        float labelW = Mathf.Clamp(label.preferredWidth + 8f, 160f, 480f);
        RectTransform labelRt = labelObj.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 0.5f);
        labelRt.anchorMax = new Vector2(0f, 0.5f);
        labelRt.pivot = new Vector2(0f, 0.5f);
        labelRt.sizeDelta = new Vector2(labelW, rowH);

        float contentW = checkSize + gap + labelW;
        float startX = (560f - contentW) * 0.5f;
        toggleRt.anchoredPosition = new Vector2(startX, 0f);
        labelRt.anchoredPosition = new Vector2(startX + checkSize + gap, 0f);

        _simpleQteToggle.onValueChanged.AddListener(isOn =>
        {
            if (_simpleQteSyncing)
                return;

            if (Plugin.qteFreeStruggleEnable != null)
                Plugin.qteFreeStruggleEnable.Value = isOn;
        });

        return topY - rowH;
    }

    private static float PlaceDifficultyBlock(GameObject panel, Font font, Sprite white, float topY)
    {
        const float labelH = 22f;
        const float btnH = 38f;
        const float btnW = 140f;
        const float gap = 10f;

        GameObject labelObj = new GameObject("DifficultyLabel_XUAIGNORE");
        labelObj.transform.SetParent(panel.transform, false);
        Text label = labelObj.AddComponent<Text>();
        label.text = SplashScreenUILabels.GetDifficultyLabel(_languageCode);
        label.font = font;
        label.fontSize = 18;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.95f, 0.93f, 0.88f, 1f);
        label.raycastTarget = false;
        RectTransform labelRt = labelObj.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0.5f, 1f);
        labelRt.anchorMax = new Vector2(0.5f, 1f);
        labelRt.pivot = new Vector2(0.5f, 1f);
        labelRt.anchoredPosition = new Vector2(0f, topY);
        labelRt.sizeDelta = new Vector2(400f, labelH);

        float buttonsTop = topY - labelH - 8f;
        float totalW = btnW * 3f + gap * 2f;
        float x0 = -totalW * 0.5f + btnW * 0.5f;

        _easyBtnBg = PlaceDiffButton(
            panel,
            HellGateDifficultyPresetModule.DifficultyLevel.Easy,
            new Vector2(x0, buttonsTop),
            btnW,
            btnH,
            font,
            white);
        _mediumBtnBg = PlaceDiffButton(
            panel,
            HellGateDifficultyPresetModule.DifficultyLevel.Medium,
            new Vector2(x0 + btnW + gap, buttonsTop),
            btnW,
            btnH,
            font,
            white);
        _hardBtnBg = PlaceDiffButton(
            panel,
            HellGateDifficultyPresetModule.DifficultyLevel.Hard,
            new Vector2(x0 + (btnW + gap) * 2f, buttonsTop),
            btnW,
            btnH,
            font,
            white);

        RefreshDifficultyButtons();
        return buttonsTop - btnH;
    }

    private static Image PlaceDiffButton(
        GameObject panel,
        HellGateDifficultyPresetModule.DifficultyLevel level,
        Vector2 topCenter,
        float btnW,
        float btnH,
        Font font,
        Sprite white)
    {
        GameObject btnObj = new GameObject("Diff_" + HellGateDifficultyPresetModule.FolderName(level));
        btnObj.transform.SetParent(panel.transform, false);
        Image bg = btnObj.AddComponent<Image>();
        bg.sprite = white;
        bg.color = new Color(0.16f, 0.16f, 0.18f, 1f);
        bg.raycastTarget = true;

        Button button = btnObj.AddComponent<Button>();
        button.targetGraphic = bg;

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = topCenter;
        rt.sizeDelta = new Vector2(btnW, btnH);

        GameObject textObj = new GameObject("Label_XUAIGNORE");
        textObj.transform.SetParent(btnObj.transform, false);
        Text text = textObj.AddComponent<Text>();
        text.text = HellGateDifficultyPresetModule.DisplayName(level);
        text.font = font;
        text.fontSize = 20;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = HellGateDifficultyPresetModule.ColorFor(level);
        text.raycastTarget = false;
        StretchFull(textObj);

        HellGateDifficultyPresetModule.DifficultyLevel captured = level;
        button.onClick.AddListener(() => SelectDifficulty(captured));
        return bg;
    }

    private static float PlaceRestartNotice(GameObject panel, Font font, float topY)
    {
        const float h = 24f;
        GameObject noticeObj = new GameObject("RestartNotice_XUAIGNORE");
        noticeObj.transform.SetParent(panel.transform, false);
        _restartNotice = noticeObj.AddComponent<Text>();
        _restartNotice.font = font;
        _restartNotice.fontSize = 13;
        _restartNotice.fontStyle = FontStyle.Bold;
        _restartNotice.alignment = TextAnchor.MiddleCenter;
        _restartNotice.color = new Color(1f, 0.55f, 0.2f, 1f);
        _restartNotice.horizontalOverflow = HorizontalWrapMode.Wrap;
        _restartNotice.verticalOverflow = VerticalWrapMode.Truncate;
        _restartNotice.raycastTarget = false;
        _restartNotice.text = string.Empty;
        RectTransform rt = noticeObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, topY);
        rt.sizeDelta = new Vector2(600f, h);
        return topY - h;
    }

    private static void PlaceDoneLanguageExitRow(GameObject panel, Font font, Sprite white, float topY)
    {
        const float btnW = 130f;
        const float btnH = 36f;
        const float gap = 12f;
        float totalW = btnW * 3f + gap * 2f;
        float x0 = -totalW * 0.5f + btnW * 0.5f;

        PlaceActionButton(
            panel,
            "DoneButton_XUAIGNORE",
            SplashScreenUILabels.GetDoneLabel(_languageCode),
            new Color(0.35f, 0.85f, 0.4f, 1f),
            new Vector2(x0, topY),
            btnW,
            btnH,
            font,
            white,
            ClosePanel);

        PlaceActionButton(
            panel,
            "LanguageButton_XUAIGNORE",
            SplashScreenUILabels.GetLanguageLabel(_languageCode),
            new Color(0.7f, 0.82f, 1f, 1f),
            new Vector2(x0 + btnW + gap, topY),
            btnW,
            btnH,
            font,
            white,
            OpenLanguagePanel);

        PlaceActionButton(
            panel,
            "ExitButton_XUAIGNORE",
            SplashScreenUILabels.GetExitLabel(_languageCode),
            new Color(0.95f, 0.25f, 0.25f, 1f),
            new Vector2(x0 + (btnW + gap) * 2f, topY),
            btnW,
            btnH,
            font,
            white,
            QuitGame);
    }

    private static void PlaceActionButton(
        GameObject panel,
        string name,
        string label,
        Color labelColor,
        Vector2 topCenter,
        float btnW,
        float btnH,
        Font font,
        Sprite white,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(panel.transform, false);
        Image bg = btnObj.AddComponent<Image>();
        bg.sprite = white;
        bg.color = new Color(0.14f, 0.14f, 0.16f, 1f);
        Button button = btnObj.AddComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(onClick);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = topCenter;
        rt.sizeDelta = new Vector2(btnW, btnH);

        GameObject textObj = new GameObject("Label_XUAIGNORE");
        textObj.transform.SetParent(btnObj.transform, false);
        Text text = textObj.AddComponent<Text>();
        text.text = label;
        text.font = font;
        text.fontSize = 18;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = labelColor;
        text.raycastTarget = false;
        StretchFull(textObj);
    }

    private static void StretchFull(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null)
            rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void TogglePanel()
    {
        if (_languagePanelOpen)
        {
            CloseLanguagePanel();
            return;
        }

        SetPanelOpen(!_panelOpen);
    }

    private static void ClosePanel()
    {
        SetLanguagePanelOpen(false);
        SetPanelOpen(false);
    }

    private static void OpenLanguagePanel()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
        SetLanguagePanelOpen(true);
    }

    private static void CloseLanguagePanel()
    {
        SetLanguagePanelOpen(false);
        if (_panelOpen && _panelRoot != null)
            _panelRoot.SetActive(true);
    }

    private static void SetLanguagePanelOpen(bool open)
    {
        _languagePanelOpen = open;
        if (_languagePanelRoot != null)
            _languagePanelRoot.SetActive(open);
    }

    private static void ApplyLanguageAndQuit(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
            return;

        if (string.Equals(languageCode, _languageCode, StringComparison.OrdinalIgnoreCase))
        {
            CloseLanguagePanel();
            return;
        }

        try
        {
            if (Plugin.hellGateLanguage != null)
                Plugin.hellGateLanguage.Value = languageCode;

            Plugin.Instance?.Config?.Save();
            Plugin.Log?.LogInfo("[HellGate] Language changed to " + languageCode + " — quitting for restart.");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[HellGate] Failed to save language: " + ex.Message);
            return;
        }

        Application.Quit();
    }

    private static void QuitGame()
    {
        Plugin.Log?.LogInfo("[HellGate] Exit requested from splash Options.");
        Application.Quit();
    }

    private static void SetPanelOpen(bool open)
    {
        _panelOpen = open;

        if (!open)
            SetLanguagePanelOpen(false);

        if (_panelRoot != null)
            _panelRoot.SetActive(open && !_languagePanelOpen);

        if (open)
        {
            SyncGoreToggleFromActiveConfig();
            SyncSimpleQteToggleFromActiveConfig();
        }

        if (_startButtonRoot != null)
            _startButtonRoot.SetActive(!open && !_languagePanelOpen && HellGateSplashScreen.IsSplashInputReady);

        if (_optionsLabelRoot != null)
            _optionsLabelRoot.transform.localScale = Vector3.one;
    }

    private static void SelectDifficulty(HellGateDifficultyPresetModule.DifficultyLevel level)
    {
        // Always re-copy — same-level click repairs disk if Config.Save had wiped the preset.
        if (!HellGateDifficultyPresetModule.TryApplyPreset(level, out string error))
        {
            if (_restartNotice != null)
            {
                _restartNotice.color = HellGateDifficultyPresetModule.ColorFor(
                    HellGateDifficultyPresetModule.DifficultyLevel.Hard);
                _restartNotice.text = string.IsNullOrEmpty(error)
                    ? "Failed to apply difficulty preset."
                    : error;
            }
            return;
        }

        _current = level;
        RefreshDifficultyButtons();
        SyncGoreToggleFromActiveConfig();
        SyncSimpleQteToggleFromActiveConfig();

        if (_restartNotice != null)
        {
            _restartNotice.color = new Color(1f, 0.55f, 0.2f, 1f);
            _restartNotice.text = SplashScreenUILabels.GetDifficultyRestartNotice(_languageCode);
        }
    }

    /// <summary>
    /// After a preset copy (or when opening Options), checkbox must match
    /// EnableGoreContent on disk / in the live ConfigEntry.
    /// </summary>
    private static void SyncGoreToggleFromActiveConfig()
    {
        bool enabled = Plugin.IsGoreContentEnabled;
        if (HellGateDifficultyPresetModule.TryReadEnableGoreContent(out bool fromFile))
            enabled = fromFile;

        if (Plugin.enableGoreContent != null && Plugin.enableGoreContent.Value != enabled)
            Plugin.enableGoreContent.Value = enabled;

        if (_goreToggle == null || _goreToggle.isOn == enabled)
            return;

        _goreSyncing = true;
        try
        {
            _goreToggle.isOn = enabled;
        }
        finally
        {
            _goreSyncing = false;
        }
    }

    /// <summary>
    /// After a preset copy (or when opening Options), checkbox must match
    /// [QTEFreeStruggle] Enable on disk / in the live ConfigEntry.
    /// Live Value applies without restart (QTEFreeStruggleMode reads .Value).
    /// </summary>
    private static void SyncSimpleQteToggleFromActiveConfig()
    {
        bool enabled = Plugin.qteFreeStruggleEnable != null && Plugin.qteFreeStruggleEnable.Value;
        if (HellGateDifficultyPresetModule.TryReadQteFreeStruggleEnable(out bool fromFile))
            enabled = fromFile;

        if (Plugin.qteFreeStruggleEnable != null && Plugin.qteFreeStruggleEnable.Value != enabled)
            Plugin.qteFreeStruggleEnable.Value = enabled;

        if (_simpleQteToggle == null || _simpleQteToggle.isOn == enabled)
            return;

        _simpleQteSyncing = true;
        try
        {
            _simpleQteToggle.isOn = enabled;
        }
        finally
        {
            _simpleQteSyncing = false;
        }
    }

    private static void RefreshDifficultyButtons()
    {
        StyleDiffButton(_easyBtnBg, HellGateDifficultyPresetModule.DifficultyLevel.Easy);
        StyleDiffButton(_mediumBtnBg, HellGateDifficultyPresetModule.DifficultyLevel.Medium);
        StyleDiffButton(_hardBtnBg, HellGateDifficultyPresetModule.DifficultyLevel.Hard);
    }

    private static void StyleDiffButton(Image bg, HellGateDifficultyPresetModule.DifficultyLevel level)
    {
        if (bg == null)
            return;

        bool selected = level == _current;
        Color accent = HellGateDifficultyPresetModule.ColorFor(level);
        bg.color = selected
            ? new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 1f)
            : new Color(0.16f, 0.16f, 0.18f, 1f);

        Text text = bg.GetComponentInChildren<Text>();
        if (text != null)
            text.color = accent;
    }

    internal static void OnSplashInputReady(GameObject startButtonObject)
    {
        _startButtonRoot = startButtonObject;
        if (_optionsLabelRoot != null)
            _optionsLabelRoot.SetActive(true);

        if (_panelOpen || _languagePanelOpen)
        {
            if (startButtonObject != null)
                startButtonObject.SetActive(false);
            if (_panelRoot != null)
                _panelRoot.SetActive(_panelOpen && !_languagePanelOpen);
            if (_languagePanelRoot != null)
                _languagePanelRoot.SetActive(_languagePanelOpen);
        }
    }
}
