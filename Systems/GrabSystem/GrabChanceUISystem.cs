using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace NoREroMod.Systems.GrabSystem;

/// <summary>
/// Persistent UI label that shows approximate melee grab chance.
/// Screen position (360, 883) with anchor (0,0) (bottom-left).
/// </summary>
    internal static class GrabChanceUISystem
{
    private const string CanvasObjectName = "GrabChanceOverlayCanvas";
    private const string LabelObjectName = "GrabChanceLabel";

        internal static Vector2 TargetAnchoredPosition => new Vector2(360f, 883f);

    private static RectTransform? overlayCanvasRect;
    private static GrabChanceUILabel? currentLabel;

    internal static void InitializeFromPlugin()
    {
        try
        {
            // UI is now rendered via RageUISystem (GrabChanceRageUILabel); keep this as a no-op.
        }
        catch (Exception)
        {
        }
    }

    private static void EnsureOverlayCanvas()
    {
        try
        {
            if (overlayCanvasRect != null)
                return;

            GameObject existing = GameObject.Find(CanvasObjectName);
            if (existing != null)
            {
                overlayCanvasRect = existing.GetComponent<RectTransform>();
                Plugin.Log?.LogInfo("[GrabChanceUI] Reusing existing canvas");
                return;
            }

            GameObject canvasGo = new GameObject(CanvasObjectName);
            overlayCanvasRect = canvasGo.AddComponent<RectTransform>();

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1001; // поверх обычного HUD, рядом with MindBroken/Rage

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
            Plugin.Log?.LogInfo("[GrabChanceUI] Created overlay canvas");
        }
        catch (Exception)
        {
        }
    }

    private static void EnsureLabel()
    {
        if (overlayCanvasRect == null)
            return;

        if (currentLabel != null && currentLabel.gameObject != null)
        {
            var existingRect = currentLabel.GetComponent<RectTransform>();
            if (existingRect != null)
            {
                existingRect.anchorMin = new Vector2(0f, 0f);
                existingRect.anchorMax = new Vector2(0f, 0f);
                existingRect.pivot = new Vector2(0f, 0f);
                existingRect.anchoredPosition = TargetAnchoredPosition;
            }

            if (currentLabel.transform.parent != overlayCanvasRect)
            {
                currentLabel.transform.SetParent(overlayCanvasRect, false);
            }

            currentLabel.gameObject.SetActive(true);
            currentLabel.ForceRefresh();
            Plugin.Log?.LogInfo("[GrabChanceUI] Reusing existing label");
            return;
        }

        GameObject go = new GameObject(LabelObjectName);
        go.transform.SetParent(overlayCanvasRect, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = TargetAnchoredPosition;
        rect.sizeDelta = new Vector2(260f, 30f);

        CanvasGroup cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        var label = go.AddComponent<Text>();
        label.fontSize = 22; // мелкий, but читаемый
        label.alignment = TextAnchor.UpperLeft;
        label.fontStyle = FontStyle.Bold;
        label.color = new Color(0.5f, 0f, 0f, 1f); // тёмно-red
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.resizeTextForBestFit = false;
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var outline = go.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        go.layer = LayerMask.NameToLayer("UI");
        // Изначальbut скрыт, пока not окажемся in игровой сцене
        go.SetActive(false);

        GrabChanceUILabel updater = go.AddComponent<GrabChanceUILabel>();
        updater.Initialise(label);

        label.text = "Grab: 0%";

        Canvas.ForceUpdateCanvases();

        currentLabel = updater;
        Plugin.Log?.LogInfo("[GrabChanceUI] Created new label GameObject");
    }

    private static void ForceLabelPosition()
    {
        try
        {
            if (overlayCanvasRect == null)
                return;

            bool shouldShow = ShouldShowLabelInternal();
            overlayCanvasRect.gameObject.SetActive(shouldShow);

            var labelRect = currentLabel?.GetComponent<RectTransform>();
            if (labelRect == null)
                return;

            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0f, 0f);
            labelRect.pivot = new Vector2(0f, 0f);
            labelRect.anchoredPosition = TargetAnchoredPosition;

            currentLabel?.gameObject.SetActive(shouldShow);
            Plugin.Log?.LogInfo($"[GrabChanceUI] ForceLabelPosition — shouldShow={shouldShow}, anchored={labelRect.anchoredPosition}");
        }
        catch (Exception)
        {
        }
    }

    internal static bool ShouldShowLabelForUI()
    {
        return ShouldShowLabelInternal();
    }

    private static bool ShouldShowLabelInternal()
    {
        try
        {
            var sceneName = SceneManager.GetActiveScene().name;
            bool isMenu = string.Equals(sceneName, "Gametitle", StringComparison.OrdinalIgnoreCase);
            // Always show in non-menu scenes; rely on scene name only (same visibility as Rage/MindBroken from player perspective).
            return !isMenu;
        }
        catch
        {
            return false;
        }
    }

    // Harmony-патчи, so that UI вел себя, as MindBroken/Rage:
    // создаём/обновляем лейбл after инициализации основного UI.
    [HarmonyPatch(typeof(UImng), "Start")]
    internal static class GrabChanceUImngStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UImng __instance)
        {
            try
            {
                __instance.StartCoroutine(DelayedUISetup());
            }
            catch (Exception)
            {
            }
        }

        private static IEnumerator DelayedUISetup()
        {
            // Даем UI прогрузиться
            yield return new WaitForSeconds(0.5f);

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

    [HarmonyPatch(typeof(CanvasBadstatusinfo), "Start")]
    internal static class GrabChanceCanvasBadstatusStartPatch
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
}

/// <summary>
/// Обновляет text UI with текущим шансом захвата.
/// </summary>
internal class GrabChanceUILabel : MonoBehaviour
{
    private Text? _label;
    private RectTransform? _rect;

    internal void Initialise(Text label)
    {
        _label = label;
        _rect = GetComponent<RectTransform>();
        Refresh();
    }

    private void OnEnable()
    {
        if (_rect == null)
            _rect = GetComponent<RectTransform>();
        ApplyAnchors();
        Refresh();
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
                _label = GetComponent<Text>();
                if (_label == null)
                    return;
            }

            if (_rect == null)
                _rect = GetComponent<RectTransform>();

            bool shouldShow = GrabChanceUISystem.ShouldShowLabelForUI();

            float chance = GrabChanceCalculator.GetApproxMeleeGrabChanceForUI();
            int percent = Mathf.RoundToInt(chance * 100f);
            _label.text = $"Grab: {percent}%";

            // Тfrom же тёмно-red цвет, as on создании
            _label.color = new Color(0.5f, 0f, 0f, 1f);
            gameObject.SetActive(shouldShow);
        }
        catch (Exception)
        {
        }
    }

    internal void ForceRefresh()
    {
        Refresh();
    }

    private void ApplyAnchors()
    {
        if (_rect == null)
            _rect = GetComponent<RectTransform>();

        if (_rect == null)
            return;

        _rect.anchorMin = new Vector2(0f, 0f);
        _rect.anchorMax = new Vector2(0f, 0f);
        _rect.pivot = new Vector2(0f, 0f);

        if (_rect.anchoredPosition != GrabChanceUISystem.TargetAnchoredPosition)
        {
            _rect.anchoredPosition = GrabChanceUISystem.TargetAnchoredPosition;
        }
    }
}

