using System;
using System.Collections;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using NoREroMod.Systems.GrabSystem;
using NoREroMod.Systems.UI;

namespace NoREroMod.Systems.Rage;

/// <summary>
/// Harmony patches + utilities for rendering the Rage Mode UI label.
/// Simple text like MindBroken: "Rage %"
/// </summary>
internal static class RageUISystem
{
    private const string CanvasObjectName = "RageOverlayCanvas";
    private const string LabelObjectName = "RageLabel";
    private const string GrabLabelObjectName = "GrabChanceLabel";
    private const float DefaultY = -25f;
    // Calibration constants (anchor = top-left).
    internal static readonly Vector2 RageRootAnchoredPosition = new Vector2(560f, 24f);
    internal const float RageBarFrameWidth = 400f;
    internal const float RageBarFrameHeight = 80f;
    // Keep manually tuned frame position unchanged even if width changes.
    internal static readonly Vector2 RageBarFrameLocalPos = new Vector2(-320f, -35f);
    internal const float RageBarFillWidth = 350f;
    internal const float RageBarFillHeight = 40f;
    internal static readonly Vector2 RageBarFillLocalPos = new Vector2(-295f, -53f);
    private static readonly float[] SectionParticleScreenX = { 325f, 440f, 555f };
    private const float SectionParticleScreenY = 1030f;

    // Y remains configurable from plugin. X for Rage is driven dynamically from QTE edges
    // so that the text's LAST character stays exactly GapFromQteEdge from the QTE block.
    internal static Vector2 TargetAnchoredPosition => new Vector2(
        Plugin.rageUIPositionX?.Value ?? 860f,
        Plugin.rageUIPositionY?.Value ?? DefaultY
    );
    internal static float RageBarFillMaxPercent => Plugin.rageTier3OverflowThreshold?.Value ?? 103f;

    internal static Vector2 ComputeRageAnchoredPosition(UnityEngine.UI.Text label, string text)
    {
        return RageRootAnchoredPosition;
    }

    internal static Vector2 GetSectionParticleLocalPos(int index)
    {
        int safeIndex = Mathf.Clamp(index, 0, SectionParticleScreenX.Length - 1);
        float screenX = SectionParticleScreenX[safeIndex];
        float anchoredGlobalY = SectionParticleScreenY - 1079f; // top-left anchored UI conversion
        return new Vector2(
            screenX - RageRootAnchoredPosition.x,
            anchoredGlobalY - RageRootAnchoredPosition.y
        );
    }


    internal static Sprite? GetRageBarFrameSprite()
    {
        if (_rageBarFrameSprite != null)
            return _rageBarFrameSprite;

        try
        {
            string path = ResolveRageUiAssetPath("rage_bar_frame.png");
            if (!File.Exists(path))
                return null;

            byte[] bytes = File.ReadAllBytes(path);
            if (bytes == null || bytes.Length == 0)
                return null;

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            if (!tex.LoadImage(bytes, false))
                return null;

            _rageBarFrameSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[RageUI] Failed loading rage_bar_frame.png: " + ex.Message);
        }

        return _rageBarFrameSprite;
    }

    internal static Sprite? GetRageBarFillSprite()
    {
        if (_rageBarFillSprite != null)
            return _rageBarFillSprite;

        try
        {
            string path = ResolveRageUiAssetPath("rage_bar_fill.png");
            if (!File.Exists(path))
                return null;

            byte[] bytes = File.ReadAllBytes(path);
            if (bytes == null || bytes.Length == 0)
                return null;

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            if (!tex.LoadImage(bytes, false))
                return null;

            _rageBarFillSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[RageUI] Failed loading rage_bar_fill.png: " + ex.Message);
        }

        return _rageBarFillSprite;
    }

    private static string ResolveRageUiAssetPath(string fileName)
    {
        string gameRoot = Application.dataPath;
        if (gameRoot.EndsWith("_Data"))
            gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

        var candidates = new string[3];
        candidates[0] = Path.Combine(Path.Combine(Path.Combine(Path.Combine(gameRoot, "sources"), "HellGate_sources"), "Rage"), "UI");
        candidates[1] = Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(gameRoot, ".."), "sources"), "HellGate_sources"), "Rage"), "UI");
        candidates[2] = Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(gameRoot, "BepInEx"), "plugins"), "NoR_HellGate"), "sources"), "HellGate_sources"), "Rage"), "UI");

        for (int i = 0; i < candidates.Length; i++)
        {
            string full = Path.Combine(candidates[i], fileName);
            if (File.Exists(full))
                return full;
        }

        return Path.Combine(candidates[0], fileName);
    }

    private static RectTransform? overlayCanvasRect;
    private static RageUILabel? currentLabel;
    private static GrabChanceRageUILabel? grabLabel;
    private static Sprite? _rageBarFrameSprite;
    private static Sprite? _rageBarFillSprite;

    internal static void InitializeFromPlugin()
    {
        try
        {
            RefreshLabel();
        }
        catch (Exception)
        {
        }
    }

    internal static void RefreshLabel()
    {
        if (!RageSystem.Enabled)
        {
            DestroyExisting();
            return;
        }

        EnsureOverlayCanvas();
        EnsureLabel();
        ForceLabelPosition();
        currentLabel?.ForceRefresh();
    }

    [HarmonyPatch(typeof(UImng), "Start")]
    private static class UImngStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UImng __instance)
        {
            try
            {
                if (!RageSystem.Enabled)
                {
                    DestroyExisting();
                    return;
                }

                __instance.StartCoroutine(DelayedUISetup());
            }
            catch (Exception)
            {
            }
        }

        private static IEnumerator DelayedUISetup()
        {
            yield return new WaitForSeconds(0.5f);

            try
            {
                EnsureOverlayCanvas();
                if (overlayCanvasRect == null)
                {
                    yield break;
                }

                EnsureLabel();
                ForceLabelPosition();
            }
            catch (Exception)
            {
            }
        }
    }

    [HarmonyPatch(typeof(CanvasBadstatusinfo), "Start")]
    private static class CanvasBadstatusStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(CanvasBadstatusinfo __instance)
        {
            try
            {
                EnsureOverlayCanvas();
                EnsureLabel();
                ForceLabelPosition();
            }
            catch (Exception)
            {
            }
        }
    }

    private static void DestroyExisting()
    {
        if (currentLabel != null)
        {
            try
            {
                UnityEngine.Object.Destroy(currentLabel.gameObject);
                currentLabel = null;
            }
            catch (Exception)
            {
            }
        }

        GameObject existing = GameObject.Find(LabelObjectName);
        if (existing != null)
        {
            UnityEngine.Object.Destroy(existing);
        }
    }

    private static void EnsureOverlayCanvas()
    {
        if (!RageSystem.Enabled)
        {
            DestroyExisting();
            return;
        }

        try
        {
            if (overlayCanvasRect != null)
            {
                return;
            }

            GameObject existing = GameObject.Find(CanvasObjectName);
            if (existing != null)
            {
                overlayCanvasRect = existing.GetComponent<RectTransform>();
                return;
            }

            GameObject canvasGo = new GameObject(CanvasObjectName);
            overlayCanvasRect = canvasGo.AddComponent<RectTransform>();

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 999; // Slightly below MindBroken (1000).

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasGo.AddComponent<GraphicRaycaster>().enabled = false;
            canvasGo.layer = LayerMask.NameToLayer("UI");

            overlayCanvasRect.anchorMin = Vector2.zero;
            overlayCanvasRect.anchorMax = Vector2.one;
            overlayCanvasRect.pivot = new Vector2(0.5f, 0.5f);
            overlayCanvasRect.offsetMin = Vector2.zero;
            overlayCanvasRect.offsetMax = Vector2.zero;
            overlayCanvasRect.localScale = Vector3.one;

            canvasGo.SetActive(true);
            UnityEngine.Object.DontDestroyOnLoad(canvasGo);
        }
        catch (Exception)
        {
        }
    }

    private static void EnsureLabel()
    {
        if (!RageSystem.Enabled)
        {
            DestroyExisting();
            return;
        }

        if (overlayCanvasRect == null)
        {
            return;
        }

        // Reuse existing Rage label if present
        if (currentLabel != null && currentLabel.gameObject != null)
        {
            var existingRect = currentLabel.GetComponent<RectTransform>();
            if (existingRect != null)
            {
                existingRect.anchorMin = new Vector2(0f, 1f);
                existingRect.anchorMax = new Vector2(0f, 1f);
                existingRect.pivot = new Vector2(0f, 1f);
                existingRect.anchoredPosition = TargetAnchoredPosition;
            }

            if (currentLabel.transform.parent != overlayCanvasRect)
        {
                currentLabel.transform.SetParent(overlayCanvasRect, false);
            }

            currentLabel.gameObject.SetActive(true);
            currentLabel.ForceRefresh();

            EnsureGrabChanceLabel();
            return;
        }

        // Create Rage label
        GameObject go = new GameObject(LabelObjectName);
        go.transform.SetParent(overlayCanvasRect, false);

        // RectTransform setup
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);  // Top-left anchor
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = TargetAnchoredPosition;
        rect.sizeDelta = new Vector2(260f, 120f);

        // CanvasGroup
        CanvasGroup cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        // UnityEngine.UI.Text
        var label = go.AddComponent<UnityEngine.UI.Text>();
        label.fontSize = 40; // Enlarged for readability.
        label.alignment = TextAnchor.UpperLeft;
        label.fontStyle = FontStyle.Bold;
        label.color = new Color(1f, 0f, 0f, 1f); // High-contrast red.
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.resizeTextForBestFit = false;
        label.font = NoREroMod.Systems.UI.HellGateFontProvider.GetUiFont();
        label.enabled = false;

        go.layer = LayerMask.NameToLayer("UI");
        go.SetActive(true);

        // Add Rage updater
        RageUILabel updater = go.AddComponent<RageUILabel>();
        updater.Initialise(label);

        // Set initial text.
        label.text = RageSystem.GetDisplayText();

        Canvas.ForceUpdateCanvases();

        currentLabel = updater;

        // Also ensure GrabChance label on the same canvas
        EnsureGrabChanceLabel();
    }

    private static void EnsureGrabChanceLabel()
    {
        if (overlayCanvasRect == null)
        {
            return;
        }

        if (grabLabel != null && grabLabel.gameObject != null)
        {
            var existingRect = grabLabel.GetComponent<RectTransform>();
            if (existingRect != null)
            {
                existingRect.anchorMin = new Vector2(0f, 0f);
                existingRect.anchorMax = new Vector2(0f, 0f);
                existingRect.pivot = new Vector2(0f, 0f);
                existingRect.anchoredPosition = new Vector2(360f, 883f);
            }

            if (grabLabel.transform.parent != overlayCanvasRect)
            {
                grabLabel.transform.SetParent(overlayCanvasRect, false);
            }

            grabLabel.gameObject.SetActive(true);
            grabLabel.ForceRefresh();
            return;
        }

        GameObject go = new GameObject(GrabLabelObjectName);
        go.transform.SetParent(overlayCanvasRect, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(360f, 883f);
        rect.sizeDelta = new Vector2(260f, 30f);

        CanvasGroup cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        var label = go.AddComponent<UnityEngine.UI.Text>();
        label.fontSize = 22;
        label.alignment = TextAnchor.UpperLeft;
        label.fontStyle = FontStyle.Italic;
        label.color = new Color(0.9f, 0.1f, 0.1f, 1f); // brighter red for Grab chance
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.resizeTextForBestFit = false;
        label.font = NoREroMod.Systems.UI.HellGateFontProvider.GetUiFont();

        // Thin white outline for readability
        var outline = go.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(1f, -1f);

        go.layer = LayerMask.NameToLayer("UI");
        go.SetActive(true);

        GrabChanceRageUILabel updater = go.AddComponent<GrabChanceRageUILabel>();
        updater.Initialise(label);

        label.text = "Grab: 0%";

        Canvas.ForceUpdateCanvases();

        grabLabel = updater;
    }

    private static void ForceLabelPosition()
    {
        try
        {
            if (overlayCanvasRect == null)
            {
                return;
            }

            // Keep canvas and labels always alive so their LateUpdate keeps running;
            // visibility is driven by CanvasGroup.alpha inside the label components.
            if (!overlayCanvasRect.gameObject.activeSelf)
                overlayCanvasRect.gameObject.SetActive(true);

            var labelRect = currentLabel?.GetComponent<RectTransform>();
            if (labelRect == null)
            {
                return;
            }

            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = TargetAnchoredPosition;

            if (currentLabel != null && !currentLabel.gameObject.activeSelf)
                currentLabel.gameObject.SetActive(true);
        }
        catch (Exception)
        {
        }
    }

    internal static bool ShouldShowLabelForUI()
    {
        return ShouldShowLabelInternal();
    }

    // Mirror the vanilla HUD so Rage appears/disappears together with HP bar.
    private static bool ShouldShowLabelInternal()
    {
        return HudVisibilityGate.ShouldShowGameplayHud();
    }
    }

    /// <summary>
/// Runtime component that keeps the label text in sync with the Rage state.
    /// </summary>
internal class RageUILabel : MonoBehaviour
{
    private UnityEngine.UI.Text? _label;
    private RectTransform? _rect;
    private CanvasGroup? _cg;
    private UnityEngine.UI.Image? _barFillImage;
    private UnityEngine.UI.Image? _barFrameImage;
    private readonly RectTransform[] _sectionSparkRoots = new RectTransform[3];
    private readonly UnityEngine.UI.Text[,] _sectionSparkDots = new UnityEngine.UI.Text[3, 14];

    internal void Initialise(UnityEngine.UI.Text label)
    {
        _label = label;
        _rect = GetComponent<RectTransform>();
        Refresh();
    }

    private void OnEnable()
    {
        RageSystem.OnChanged += Refresh;
        RageSystem.OnActivated += Refresh;
        RageSystem.OnDeactivated += Refresh;
        if (_rect == null)
        {
            _rect = GetComponent<RectTransform>();
        }
        ApplyAnchors();
        Refresh();
    }

    private void OnDisable()
    {
        RageSystem.OnChanged -= Refresh;
        RageSystem.OnActivated -= Refresh;
        RageSystem.OnDeactivated -= Refresh;
        DestroySectionParticles();
    }

    private void LateUpdate()
    {
        ApplyAnchors();
        Refresh();
    }

    private void Refresh()
    {
        try
        {
            if (_label == null)
            {
                _label = GetComponent<UnityEngine.UI.Text>();
                if (_label == null)
                {
                    return;
                }
            }

            if (_rect == null)
            {
                _rect = GetComponent<RectTransform>();
            }

            bool enabled = RageSystem.Enabled;
            bool shouldShow = enabled && RageUISystem.ShouldShowLabelForUI();
            // Text renderer is kept only as a host component; visual title is PNG-only.
            _label.text = string.Empty;
            _label.enabled = false;
            EnsureBarFillImage();
            EnsureBarFrameImage();
            UpdateBarFillImage();
            EnsureSectionSparks();
            UpdateSectionSparks(shouldShow);
            // Apply visual style.
            if (enabled && _label != null)
            {
                // Keep Rage label color stable for readability.
                _label.color = Color.red;
                _label.fontStyle = FontStyle.Bold;
                if (!RageSystem.IsActive && RageSystem.IsTier3Ready)
                {
                    // Blink when Tier3 is primed by overflow readiness.
                    float blink = Mathf.PingPong(Time.unscaledTime * 4f, 1f);
                    _label.color = new Color(1f, 0f, 0f, 0.35f + 0.65f * blink);
                }
            }

            // Drive visibility via CanvasGroup.alpha so LateUpdate keeps running
            // and the label can react to gate changes immediately.
            if (_cg == null)
            {
                _cg = GetComponent<CanvasGroup>();
                if (_cg == null)
                    _cg = gameObject.AddComponent<CanvasGroup>();
            }
            _cg.alpha = shouldShow ? 1f : 0f;
            _cg.blocksRaycasts = false;
            _cg.interactable = false;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
        }
        catch (Exception)
        {
        }
    }

    internal void AssignLabel(UnityEngine.UI.Text label)
    {
        _label = label;
        if (_rect == null)
        {
            _rect = GetComponent<RectTransform>();
        }
        ApplyAnchors();
        ForceRefresh();
    }

    internal void ForceRefresh()
    {
        Refresh();
    }

    private void ApplyAnchors()
    {
        if (_rect == null)
        {
            _rect = GetComponent<RectTransform>();
        }

        if (_rect == null)
        {
            return;
        }

        _rect.anchorMin = new Vector2(0f, 1f);
        _rect.anchorMax = new Vector2(0f, 1f);
        _rect.pivot = new Vector2(0f, 1f);

        string currentText = _label != null ? _label.text : string.Empty;
        Vector2 target = RageUISystem.ComputeRageAnchoredPosition(_label, currentText);
        if (_rect.anchoredPosition != target)
        {
            _rect.anchoredPosition = target;
        }
    }

    private void EnsureBarFrameImage()
    {
        if (_barFrameImage != null)
            return;

        Sprite? sprite = RageUISystem.GetRageBarFrameSprite();
        if (sprite == null)
            return;

        GameObject go = new GameObject("RageBarFrameImage");
        go.transform.SetParent(transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = RageUISystem.RageBarFrameLocalPos;
        rt.sizeDelta = new Vector2(RageUISystem.RageBarFrameWidth, RageUISystem.RageBarFrameHeight);

        _barFrameImage = go.AddComponent<UnityEngine.UI.Image>();
        _barFrameImage.sprite = sprite;
        _barFrameImage.type = Image.Type.Simple;
        _barFrameImage.raycastTarget = false;
        _barFrameImage.color = Color.white;
        _barFrameImage.transform.SetAsLastSibling();
    }

    private void EnsureBarFillImage()
    {
        if (_barFillImage != null)
            return;

        Sprite? sprite = RageUISystem.GetRageBarFillSprite();
        if (sprite == null)
            return;

        GameObject go = new GameObject("RageBarFillImage");
        go.transform.SetParent(transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = RageUISystem.RageBarFillLocalPos;
        rt.sizeDelta = new Vector2(RageUISystem.RageBarFillWidth, RageUISystem.RageBarFillHeight);

        _barFillImage = go.AddComponent<UnityEngine.UI.Image>();
        _barFillImage.sprite = sprite;
        _barFillImage.type = Image.Type.Filled;
        _barFillImage.fillMethod = Image.FillMethod.Horizontal;
        _barFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        _barFillImage.fillAmount = 0f;
        _barFillImage.raycastTarget = false;
        _barFillImage.color = Color.white;
        _barFillImage.transform.SetSiblingIndex(0);
    }

    private void UpdateBarFillImage()
    {
        if (_barFillImage == null)
            return;

        float maxPercent = Mathf.Max(1f, RageUISystem.RageBarFillMaxPercent);
        float currentPercent = Mathf.Clamp(RageSystem.Percent, 0f, maxPercent);
        _barFillImage.fillAmount = currentPercent / maxPercent;
    }

    private void EnsureSectionSparks()
    {
        for (int i = 0; i < _sectionSparkRoots.Length; i++)
        {
            if (_sectionSparkRoots[i] != null)
                continue;

            GameObject rootGo = new GameObject("RageBarSectionSparks" + (i + 1));
            rootGo.transform.SetParent(transform, false);
            RectTransform rootRt = rootGo.AddComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0f, 1f);
            rootRt.anchorMax = new Vector2(0f, 1f);
            rootRt.pivot = new Vector2(0.5f, 1f);
            rootRt.anchoredPosition = RageUISystem.GetSectionParticleLocalPos(i);
            rootRt.sizeDelta = new Vector2(36f, 46f);
            _sectionSparkRoots[i] = rootRt;

            for (int j = 0; j < 14; j++)
            {
                GameObject dotGo = new GameObject("Spark" + j);
                dotGo.transform.SetParent(rootRt, false);
                RectTransform dotRt = dotGo.AddComponent<RectTransform>();
                dotRt.anchorMin = new Vector2(0.5f, 1f);
                dotRt.anchorMax = new Vector2(0.5f, 1f);
                dotRt.pivot = new Vector2(0.5f, 0.5f);
                dotRt.sizeDelta = new Vector2(10f, 10f);

                var dot = dotGo.AddComponent<UnityEngine.UI.Text>();
                dot.font = NoREroMod.Systems.UI.HellGateFontProvider.GetUiFont();
                dot.text = "●";
                dot.fontStyle = FontStyle.Bold;
                dot.fontSize = 18;
                dot.alignment = TextAnchor.MiddleCenter;
                dot.horizontalOverflow = HorizontalWrapMode.Overflow;
                dot.verticalOverflow = VerticalWrapMode.Overflow;
                dot.raycastTarget = false;
                dot.color = new Color(1f, 0.2f, 0.1f, 0f);
                _sectionSparkDots[i, j] = dot;
            }
        }
    }

    private void UpdateSectionSparks(bool shouldShow)
    {
        bool rageActive = RageSystem.IsActive;
        float p = RageSystem.Percent;
        float t1 = Plugin.rageTier1Threshold?.Value ?? 30f;
        float t2 = Plugin.rageTier2Threshold?.Value ?? 60f;
        float t3 = Plugin.rageTier3OverflowThreshold?.Value ?? 103f;
        int litByPercent = 0;
        if (p >= t1) litByPercent = 1;
        if (p >= t2) litByPercent = 2;
        if (p >= t3 || RageSystem.IsTier3Ready) litByPercent = 3;
        int activeTierCount = rageActive ? Mathf.Clamp((int)RageSystem.CurrentTier, 0, 3) : litByPercent;
        for (int i = 0; i < _sectionSparkRoots.Length; i++)
        {
            RectTransform? root = _sectionSparkRoots[i];
            if (root == null)
                continue;

            bool active = shouldShow && i < activeTierCount;
            root.gameObject.SetActive(active);
            if (!active)
                continue;

            for (int j = 0; j < 14; j++)
            {
                var dot = _sectionSparkDots[i, j];
                if (dot == null)
                    continue;

                RectTransform dotRt = dot.rectTransform;
                float seed = (i * 0.73f) + (j * 0.37f);
                float cycle = Mathf.Repeat(Time.unscaledTime * (0.21f + 0.015f * j) + seed, 1f);
                // Full 360-degree spread with travel distance around 50px (+/-).
                float angle = Mathf.Repeat(seed * 6.283185f, 6.283185f);
                float targetRadius = 50f + Mathf.Sin(seed * 11.37f) * 8f; // ~42..58 px
                float radius = cycle * targetRadius;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                dotRt.anchoredPosition = new Vector2(x, y);
                float scale = 0.62f + 0.38f * (1f - cycle);
                dotRt.localScale = new Vector3(scale, scale, 1f);
                float alpha = (1f - cycle) * (0.4f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.2f + seed)));
                dot.color = i == 2
                    ? new Color(1f, 0.12f, 0.05f, alpha)
                    : new Color(1f, 0.26f, 0.12f, alpha);
            }
        }
    }

    private void DestroySectionParticles()
    {
        for (int i = 0; i < _sectionSparkRoots.Length; i++)
        {
            for (int j = 0; j < 14; j++)
                _sectionSparkDots[i, j] = null;
            if (_sectionSparkRoots[i] != null)
            {
                try { UnityEngine.Object.Destroy(_sectionSparkRoots[i].gameObject); } catch { }
                _sectionSparkRoots[i] = null;
            }
        }
    }


}
