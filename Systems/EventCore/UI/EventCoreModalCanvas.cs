using System.Collections.Generic;
using NoREroMod;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NoREroMod.Systems.EventCore.Content;
using NoREroMod.Systems.EventCore.Core;

namespace NoREroMod.Systems.EventCore.UI;

/// <summary>One choice row: label, whether it can be chosen, text tint, optional hint when locked.</summary>
internal readonly struct EventCoreChoiceSlotUi
{
    internal readonly string Label;
    internal readonly bool Interactable;
    internal readonly Color TextColor;

    /// <summary>Second line under choice text when <see cref="Interactable"/> is false (e.g. MB/Rage requirement).</summary>
    internal readonly string LockedRequirementHint;

    /// <summary>AradiaAva expression folder shown on the left while this choice row is visible (broker gate).</summary>
    internal readonly string LeftPortraitExpression;

    internal EventCoreChoiceSlotUi(string label, bool interactable, Color textColor, string lockedRequirementHint = null)
        : this(label, interactable, textColor, lockedRequirementHint, null)
    {
    }

    internal EventCoreChoiceSlotUi(
        string label,
        bool interactable,
        Color textColor,
        string lockedRequirementHint,
        string leftPortraitExpression)
    {
        Label = label ?? string.Empty;
        Interactable = interactable;
        TextColor = textColor;
        LockedRequirementHint = lockedRequirementHint ?? string.Empty;
        LeftPortraitExpression = leftPortraitExpression;
    }

    internal EventCoreChoiceSlotUi WithLeftPortrait(string leftPortraitExpression)
    {
        return new EventCoreChoiceSlotUi(Label, Interactable, TextColor, LockedRequirementHint, leftPortraitExpression);
    }
}

/// <summary>
/// EventCore overlay composed of two canvases: a frame layer above the HUD and a chrome layer with a single
/// centered content column for dialogue, continue prompts, and choices.
/// </summary>
internal static class EventCoreModalCanvas
{
    private const int SortOrderFrame = 55000;
    private const int SortOrderChrome = 55100;

    private const float DefaultPanelWidthPx = 1000f;
    private const float DefaultPanelHeightPx = 400f;

    private const float PanelBottomEdgeInsetPx = 65f;

    /// <summary>
    /// Extra height added to the chrome layer so it matches the frame texture vertically.
    /// </summary>
    private const float PanelChromeExtraHeightPx = 50f;

    /// <summary>
    /// Additional downward offset applied to both frame and chrome layers relative to <see cref="PanelBottomEdgeInsetPx"/>.
    /// </summary>
    private const float PanelChromeShiftDownPx = 50f;

    private static readonly Vector2 PanelAnchor = new Vector2(0.5f, 0f);
    private static readonly Vector2 PanelPivot = new Vector2(0.5f, 0f);

    private const float ContentInsetHorizontal = 24f;
    private const float ContentInsetVertical = 32f;

    private const float PortraitSideReservePx = 400f;
    private const float MinMiddleColumnWidthPx = 520f;

    /// <summary>
    /// Lifts the player choice block within the content area using bottom padding in reference pixels.
    /// </summary>
    private const int ChoiceBlockLiftPx = 30;

    /// <summary>
    /// Lifts the broker continue-layout text block by adding bottom padding to the vertical layout group.
    /// </summary>
    private const int ContinueDialogLiftPx = 50;

    /// <summary>
    /// Pushes the speaker label lower on broker continue screens.
    /// </summary>
    private const float BanditSpeakerDropPx = 80f;

    /// <summary>
    /// Body-only continue screens (no speaker label) still need a top inset; otherwise
    /// short terminal lines can cling to the upper frame border.
    /// </summary>
    private const float ContinueBodyOnlyDropPx = BanditSpeakerDropPx;

    /// <summary>
    /// Vertical spacing between speaker label, body text, and continue hint on continue screens.
    /// </summary>
    private const float ContinueVerticalSpacingPx = 18f;

    /// <summary>
    /// Additional offset inserted between the speaker label and the broker body line.
    /// </summary>
    private const float BrokerPhraseDropPx = 100f - (3f * ContinueVerticalSpacingPx);

    /// <summary>
    /// Top spacer used by prelude narration. Smaller values place the text higher in the frame.
    /// </summary>
    private const float PreludeContentTopInsetPx = 60f;

    /// <summary>
    /// Gap between the prelude top spacer and the prelude body text.
    /// </summary>
    private const float PreludeBodyTopGapPx = 16f;

    private const float BrokerDialogLineSpacing = 1.22f;

    private const float ChoiceButtonBgAlpha = 0.22f;

    /// <summary>
    /// Crisp black outline matching the legacy dialogue system.
    /// </summary>
    private static readonly Vector2 ModalTextOutlineDistance = new Vector2(1f, -1f);

    private static float _panelWidthPx = DefaultPanelWidthPx;
    private static float _panelHeightPx = DefaultPanelHeightPx;
    private static float _horizontalContentGutterPx = ContentInsetHorizontal;
    private static float _textColumnWidthPx = 400f;

    private static readonly Color DisabledGrey = new Color(0.52f, 0.52f, 0.55f, 1f);

    private static GameObject _root;
    private static Text _brokerText;
    private static EventCoreModalPortraitDisplay _portraitDisplay;
    private static float _lastSidePortraitWidthPx;
    private static float _lastPanelHeightPx;
    private static readonly List<Button> ChoiceButtons = new List<Button>();

    internal static bool IsVisible => _root != null && _root.activeSelf;

    private const string AutoTranslatorIgnoreSuffix = "_XUAIGNORE";

    private static string IgnoreAutoTranslator(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return AutoTranslatorIgnoreSuffix;

        return objectName.EndsWith(AutoTranslatorIgnoreSuffix)
            ? objectName
            : objectName + AutoTranslatorIgnoreSuffix;
    }

    internal static void Hide()
    {
        if (_root != null)
            Object.Destroy(_root);

        _root = null;
        _brokerText = null;
        _portraitDisplay = null;
        ChoiceButtons.Clear();
        EventCorePortraitClipLoader.ClearCache();
    }

    internal static void Show(
        string speakerLabel,
        string bodyLine,
        EventCoreChoiceSlotUi[] choiceSlots,
        bool continueOnly,
        bool preludeNarration = false,
        EventCorePortraitPair portraits = default)
    {
        Hide();

        _root = new GameObject(IgnoreAutoTranslator("EventCore_ModalCanvas"));
        Object.DontDestroyOnLoad(_root);
        _root.layer = LayerMask.NameToLayer("UI");

        Texture2D frameTex = EventCoreFrameArt.TryGetFrameTexture();
        ResolvePanelSizeFromFrameTexture(frameTex, out float pw, out float ph);
        _panelWidthPx = pw;
        _panelHeightPx = ph + PanelChromeExtraHeightPx;

        float maxSidePortrait = Mathf.Max(0f, (pw - MinMiddleColumnWidthPx) * 0.5f);
        float sidePortrait = Mathf.Min(PortraitSideReservePx, maxSidePortrait);
        _horizontalContentGutterPx = sidePortrait + ContentInsetHorizontal;
        _lastSidePortraitWidthPx = sidePortrait;
        _lastPanelHeightPx = ph + PanelChromeExtraHeightPx;

        Transform frameCanvas = CreateCanvasStack(_root.transform, "EventCore_FrameCanvas", SortOrderFrame, raycaster: false);
        CreateFramePanel(frameCanvas, frameTex, pw, ph);

        Transform chromeCanvas = CreateCanvasStack(_root.transform, "EventCore_ChromeCanvas", SortOrderChrome, raycaster: true);
        bool liftChoices = !continueOnly && choiceSlots != null && choiceSlots.Length > 0;
        CreateChromePanel(chromeCanvas, pw, ph, liftChoices, continueOnly, out Transform layoutRoot, out Transform panelTransform);

        _portraitDisplay = EventCoreModalPortraitDisplay.Attach(_root);
        _portraitDisplay.ConfigureLayout(panelTransform, _lastSidePortraitWidthPx, _lastPanelHeightPx);
        _portraitDisplay.SetPair(portraits);

        var insetMin = new Vector2(_horizontalContentGutterPx, ContentInsetVertical);
        var insetMax = new Vector2(-_horizontalContentGutterPx, -ContentInsetVertical);

        Font uiFont = EventCoreUiFont.GetUiFont();

        if (continueOnly && preludeNarration && !string.IsNullOrEmpty(bodyLine?.Trim()))
            CreateVerticalLayoutSpacer(layoutRoot, PreludeContentTopInsetPx);
        else if (continueOnly && !string.IsNullOrEmpty(speakerLabel?.Trim()))
            CreateVerticalLayoutSpacer(layoutRoot, BanditSpeakerDropPx);
        else if (continueOnly && !string.IsNullOrEmpty(bodyLine?.Trim()))
            CreateVerticalLayoutSpacer(layoutRoot, ContinueBodyOnlyDropPx);

        if (!string.IsNullOrEmpty(speakerLabel?.Trim()))
        {
            Text speakerText = continueOnly
                ? CreateSpeakerLabelBandit(layoutRoot, uiFont)
                : CreateSpeakerLabel(layoutRoot, uiFont);
            speakerText.text = speakerLabel.Trim();
        }

        if (continueOnly && !string.IsNullOrEmpty(bodyLine?.Trim()))
        {
            float phraseGap = preludeNarration ? PreludeBodyTopGapPx : BrokerPhraseDropPx;
            if (phraseGap > 0.001f)
                CreateVerticalLayoutSpacer(layoutRoot, phraseGap);
        }

        if (!string.IsNullOrEmpty(bodyLine?.Trim()))
        {
            _brokerText = continueOnly
                ? CreateBrokerLabelBanditCentered(layoutRoot, uiFont)
                : CreateBrokerLabel(layoutRoot, uiFont);
            _brokerText.text = bodyLine.Trim();
        }

        if (continueOnly)
        {
            CreateContinueClickCatcherBehindContent(layoutRoot.parent, insetMin, insetMax);
            CreateContinueClickHint(layoutRoot, uiFont);
            Canvas.ForceUpdateCanvases();
            return;
        }

        if (choiceSlots != null && choiceSlots.Length > 0)
        {
            int n = Mathf.Min(choiceSlots.Length, 5);
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                EventCoreChoiceSlotUi captured = choiceSlots[i];
                var btn = CreateChoiceButton(layoutRoot, uiFont, i + 1, captured);
                btn.onClick.AddListener(() =>
                {
                    if (!captured.Interactable)
                        return;
                    EventCoreRuntime.AdvanceChoiceStep(idx);
                });
                ChoiceButtons.Add(btn);
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// Applies the bold-italic face and the legacy one-pixel outline used by dialogue UI.
    /// </summary>
    private static void ApplyModalTextBoldOutline(GameObject textGameObject)
    {
        Text t = textGameObject.GetComponent<Text>();
        if (t != null)
            t.fontStyle = FontStyle.BoldAndItalic;

        Outline outline = textGameObject.GetComponent<Outline>();
        if (outline == null)
            outline = textGameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = ModalTextOutlineDistance;
    }

    private static void CreateVerticalLayoutSpacer(Transform parent, float heightPx)
    {
        if (heightPx <= 0f)
            return;

        var go = new GameObject(IgnoreAutoTranslator("BrokerPhraseSpacer"));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = heightPx;
        le.preferredHeight = heightPx;
        le.flexibleHeight = 0f;
        le.flexibleWidth = 1f;
    }

    private static void CreateContinueClickHint(Transform parent, Font font)
    {
        if (!EventCoreStringRegistry.TryGet("eventcore_ui_click_continue", out string hint) || string.IsNullOrEmpty(hint))
            return;

        var go = new GameObject(IgnoreAutoTranslator("ContinueClickHint"));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = 17;
        text.color = new Color(0.72f, 0.76f, 0.82f, 0.92f);
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = hint;
        ApplyModalTextBoldOutline(go);

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 26f;
        le.preferredWidth = _textColumnWidthPx;
        le.flexibleWidth = 1f;

        var rt = text.rectTransform;
        rt.sizeDelta = new Vector2(_textColumnWidthPx, 26f);
    }

    /// <summary>
    /// Creates a transparent click target behind the content area so the modal can advance on click.
    /// The text itself remains non-raycast and does not intercept the input.
    /// </summary>
    private static void CreateContinueClickCatcherBehindContent(Transform panelGo, Vector2 insetMin, Vector2 insetMax)
    {
        var go = new GameObject(IgnoreAutoTranslator("EventCore_ClickContinue"));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(panelGo, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = insetMin;
        rt.offsetMax = insetMax;

        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
        go.AddComponent<EventCoreContinueClickForwarder>();

        Transform contentTf = panelGo.Find(IgnoreAutoTranslator("Content"));
        if (contentTf != null)
            go.transform.SetSiblingIndex(contentTf.GetSiblingIndex());
    }

    private static Transform CreateCanvasStack(Transform parent, string name, int sortOrder, bool raycaster)
    {
        var go = new GameObject(IgnoreAutoTranslator(name));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;
        canvas.overrideSorting = true;

        if (raycaster)
        {
            var gr = go.AddComponent<GraphicRaycaster>();
            gr.blockingObjects = GraphicRaycaster.BlockingObjects.All;
        }

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return go.transform;
    }

    private static void ApplyPanelRect(RectTransform rt, float widthPx, float heightPx, float bottomInsetY)
    {
        rt.anchorMin = PanelAnchor;
        rt.anchorMax = PanelAnchor;
        rt.pivot = PanelPivot;
        rt.sizeDelta = new Vector2(widthPx, heightPx);
        rt.anchoredPosition = new Vector2(0f, bottomInsetY);
    }

    private static void ResolvePanelSizeFromFrameTexture(Texture2D frameTex, out float widthPx, out float heightPx)
    {
        if (frameTex != null && frameTex.width >= 64 && frameTex.height >= 64)
        {
            widthPx = frameTex.width;
            heightPx = frameTex.height;
            return;
        }

        widthPx = DefaultPanelWidthPx;
        heightPx = DefaultPanelHeightPx;
    }

    private static void CreateFramePanel(Transform frameCanvas, Texture2D tex, float pw, float ph)
    {
        var go = new GameObject(IgnoreAutoTranslator("Panel"));
        go.transform.SetParent(frameCanvas, false);

        var rt = go.AddComponent<RectTransform>();
        float frameBottom = PanelBottomEdgeInsetPx - PanelChromeShiftDownPx;
        ApplyPanelRect(rt, pw, ph + PanelChromeExtraHeightPx, frameBottom);
        TryAddFrameOverlay(go.transform, tex);
    }

    private static void TryAddFrameOverlay(Transform panelTransform, Texture2D tex)
    {
        if (tex == null)
            return;

        var go = new GameObject(IgnoreAutoTranslator("EventCore_FrameBackdrop"));
        go.transform.SetParent(panelTransform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var raw = go.AddComponent<RawImage>();
        raw.texture = tex;
        raw.uvRect = new Rect(0f, 0f, 1f, 1f);
        raw.color = Color.white;
        raw.raycastTarget = false;
    }

    private static void CreateChromePanel(
        Transform chromeCanvas,
        float pw,
        float phTexture,
        bool liftChoicesBlock,
        bool banditDialogLayout,
        out Transform layoutRoot,
        out Transform panelTransform)
    {
        float chromeH = phTexture + PanelChromeExtraHeightPx;
        float chromeBottom = PanelBottomEdgeInsetPx - PanelChromeShiftDownPx;

        var panelGo = new GameObject(IgnoreAutoTranslator("Panel"));
        panelGo.transform.SetParent(chromeCanvas, false);
        var panelRt = panelGo.AddComponent<RectTransform>();
        ApplyPanelRect(panelRt, pw, chromeH, chromeBottom);

        var contentInsetMin = new Vector2(_horizontalContentGutterPx, ContentInsetVertical);
        var contentInsetMax = new Vector2(-_horizontalContentGutterPx, -ContentInsetVertical);

        float dimA = Mathf.Clamp01(Plugin.eventCoreModalDimAlpha?.Value ?? 0f);
        if (dimA > 0.001f)
        {
            var dimGo = new GameObject(IgnoreAutoTranslator("EventCore_Dim"));
            dimGo.transform.SetParent(panelGo.transform, false);
            dimGo.transform.SetAsFirstSibling();
            var dimRt = dimGo.AddComponent<RectTransform>();
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = contentInsetMin;
            dimRt.offsetMax = contentInsetMax;
            var dimImg = dimGo.AddComponent<Image>();
            dimImg.color = new Color(0.05f, 0.05f, 0.07f, dimA);
            dimImg.raycastTarget = false;
        }

        var content = new GameObject(IgnoreAutoTranslator("Content"));
        content.transform.SetParent(panelGo.transform, false);
        var crt = content.AddComponent<RectTransform>();
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = contentInsetMin;
        crt.offsetMax = contentInsetMax;

        float innerW = pw - (_horizontalContentGutterPx * 2f);
        _textColumnWidthPx = innerW;

        var v = content.AddComponent<VerticalLayoutGroup>();
        int lift = liftChoicesBlock ? ChoiceBlockLiftPx : 0;
        if (banditDialogLayout)
            lift += ContinueDialogLiftPx;
        v.padding = new RectOffset(0, 0, 0, lift);
        v.spacing = banditDialogLayout ? ContinueVerticalSpacingPx : 10f;
        v.childAlignment = banditDialogLayout ? TextAnchor.UpperCenter : TextAnchor.MiddleCenter;
        v.childControlHeight = true;
        v.childControlWidth = true;
        v.childForceExpandHeight = false;
        v.childForceExpandWidth = true;

        content.AddComponent<RectMask2D>();
        layoutRoot = content.transform;
        panelTransform = panelGo.transform;
    }

    private static Text CreateSpeakerLabel(Transform parent, Font font)
    {
        var go = new GameObject(IgnoreAutoTranslator("Speaker"));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontStyle = FontStyle.Bold;
        text.fontSize = 22;
        text.color = new Color(0.88f, 0.78f, 0.58f, 1f);
        text.alignment = TextAnchor.UpperCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 28f;
        le.preferredWidth = _textColumnWidthPx;
        le.flexibleWidth = 1f;

        var rt = text.rectTransform;
        rt.sizeDelta = new Vector2(_textColumnWidthPx, 28f);
        ApplyModalTextBoldOutline(go);

        return text;
    }

    /// <summary>
    /// Speaker label aligned to the right for broker continue screens.
    /// </summary>
    private static Text CreateSpeakerLabelBandit(Transform parent, Font font)
    {
        var go = new GameObject(IgnoreAutoTranslator("Speaker"));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontStyle = FontStyle.Bold;
        text.fontSize = 22;
        text.color = new Color(0.88f, 0.78f, 0.58f, 1f);
        text.alignment = TextAnchor.UpperRight;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 30f;
        le.preferredWidth = _textColumnWidthPx;
        le.flexibleWidth = 1f;

        var rt = text.rectTransform;
        rt.sizeDelta = new Vector2(_textColumnWidthPx, 30f);
        ApplyModalTextBoldOutline(go);

        return text;
    }

    /// <summary>
    /// Broker body text centered within the single-column continue layout.
    /// </summary>
    private static Text CreateBrokerLabelBanditCentered(Transform parent, Font font)
    {
        var go = new GameObject(IgnoreAutoTranslator("BrokerLine"));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = 24;
        text.color = Color.white;
        text.alignment = TextAnchor.UpperCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;
        text.lineSpacing = BrokerDialogLineSpacing;
        text.raycastTarget = false;

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 40f;
        le.preferredWidth = _textColumnWidthPx;
        le.flexibleWidth = 1f;

        var rt = text.rectTransform;
        rt.sizeDelta = new Vector2(_textColumnWidthPx, 0f);
        ApplyModalTextBoldOutline(go);

        return text;
    }

    private static Text CreateBrokerLabel(Transform parent, Font font)
    {
        var go = new GameObject(IgnoreAutoTranslator("BrokerLine"));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = 24;
        text.color = Color.white;
        text.alignment = TextAnchor.UpperCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;
        text.raycastTarget = false;

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 40f;
        le.preferredWidth = _textColumnWidthPx;
        le.flexibleWidth = 1f;

        var rt = text.rectTransform;
        rt.sizeDelta = new Vector2(_textColumnWidthPx, 0f);
        ApplyModalTextBoldOutline(go);

        return text;
    }

    private static Button CreateChoiceButton(Transform parent, Font font, int hotkeyIndex, EventCoreChoiceSlotUi slot)
    {
        var go = new GameObject(IgnoreAutoTranslator($"Choice_{hotkeyIndex}"));
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.11f, 0.12f, 0.16f, ChoiceButtonBgAlpha);

        var btn = go.AddComponent<Button>();
        btn.interactable = slot.Interactable;

        var colors = btn.colors;
        colors.disabledColor = new Color(0.32f, 0.32f, 0.35f, ChoiceButtonBgAlpha * 0.65f);
        colors.highlightedColor = new Color(0.32f, 0.36f, 0.46f, Mathf.Clamp01(ChoiceButtonBgAlpha + 0.25f));
        colors.pressedColor = new Color(0.26f, 0.28f, 0.36f, Mathf.Clamp01(ChoiceButtonBgAlpha + 0.2f));
        btn.colors = colors;

        bool showHint = !slot.Interactable && !string.IsNullOrEmpty(slot.LockedRequirementHint);

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = showHint ? 58f : 44f;
        le.flexibleWidth = 1f;

        var labelGo = new GameObject(IgnoreAutoTranslator("Label"));
        labelGo.transform.SetParent(go.transform, false);
        var txt = labelGo.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = 24;
        txt.lineSpacing = 0.9f;
        txt.supportRichText = true;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        Color useCol = slot.Interactable ? slot.TextColor : DisabledGrey;
        txt.color = useCol;
        txt.alignment = TextAnchor.MiddleCenter;
        string primary = $"{hotkeyIndex}. {slot.Label}";
        txt.text = showHint
            ? primary + "\n<size=16><color=#8f96a3>" + slot.LockedRequirementHint + "</color></size>"
            : primary;
        ApplyModalTextBoldOutline(labelGo);

        var rt = txt.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(12f, 4f);
        rt.offsetMax = new Vector2(-12f, -4f);

        return btn;
    }
}

internal sealed class EventCoreContinueClickForwarder : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        EventCoreRuntime.AdvanceContinuePrompt();
    }
}
