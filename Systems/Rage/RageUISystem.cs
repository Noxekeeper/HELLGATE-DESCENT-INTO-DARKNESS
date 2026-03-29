using System;
using System.Collections;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NoREroMod.Systems.GrabSystem;

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
    // Use approach like in Corruption/Recovery: fallback to Arial (built-in), no custom TMP assets
    private static TMP_FontAsset? s_customFont;
    private static bool s_fontTried = false;

    // Position: above MindBroken (which is at 360, -174), placed higher
    // Moved near MB UI: screen coordinates ~ (1600, 1000) at anchor (0,1)
    // Raised 25px higher (was -50, now -25)
    internal static Vector2 TargetAnchoredPosition => new Vector2(
        Plugin.rageUIPositionX?.Value ?? 360f,
        Plugin.rageUIPositionY?.Value ?? -25f
    );

    private static RectTransform? overlayCanvasRect;
    private static RageUILabel? currentLabel;
    private static GrabChanceRageUILabel? grabLabel;

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
        rect.sizeDelta = new Vector2(276f, 50f); // Enlarged for readability.

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
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

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
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

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

            bool shouldShow = ShouldShowLabelInternal();
            overlayCanvasRect.gameObject.SetActive(shouldShow);

            if (!shouldShow)
            {
                if (currentLabel != null)
                {
                    currentLabel.gameObject.SetActive(false);
                }
                return;
            }

            var labelRect = currentLabel?.GetComponent<RectTransform>();
            if (labelRect == null)
            {
                return;
            }

            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = TargetAnchoredPosition;

            currentLabel?.gameObject.SetActive(true);
        }
        catch (Exception)
        {
        }
    }

    internal static bool ShouldShowLabelForUI()
    {
        return ShouldShowLabelInternal();
    }

    private static bool? _cachedHasGameplayCanvas;
    private static float _canvasCacheTime;
    private const float CanvasCacheInterval = 2f;

    private static bool ShouldShowLabelInternal()
    {
        try
        {
            var sceneName = SceneManager.GetActiveScene().name;
            bool isMenu = string.Equals(sceneName, "Gametitle", StringComparison.OrdinalIgnoreCase);
            if (isMenu) return false;
            // FPS: cache FindObjectOfType result - invalidates every 2 sec
            float now = Time.unscaledTime;
            if (!_cachedHasGameplayCanvas.HasValue || (now - _canvasCacheTime) > CanvasCacheInterval)
            {
                _cachedHasGameplayCanvas = UnityEngine.Object.FindObjectOfType<CanvasBadstatusinfo>() != null;
                _canvasCacheTime = now;
            }
            return _cachedHasGameplayCanvas.Value;
        }
        catch
        {
            return false;
        }
    }
    }

    /// <summary>
/// Runtime component that keeps the label text in sync with the Rage state.
    /// </summary>
internal class RageUILabel : MonoBehaviour
{
    private UnityEngine.UI.Text? _label;
    private RectTransform? _rect;

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
            string newText = enabled ? RageSystem.GetDisplayText() : string.Empty;
            _label.text = newText;

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

            gameObject.SetActive(shouldShow);
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

        if (_rect.anchoredPosition != RageUISystem.TargetAnchoredPosition)
    {
            _rect.anchoredPosition = RageUISystem.TargetAnchoredPosition;
        }
    }

}
