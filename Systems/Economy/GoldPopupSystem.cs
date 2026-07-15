using UnityEngine;
using UnityEngine.UI;

namespace NoREroMod.Systems.Economy;

/// <summary>
/// Floating "+N" gold popup. Renders on a dedicated <see cref="RenderMode.ScreenSpaceOverlay"/>
/// canvas with crisp UI text — much more legible than a tiny world-space canvas at fractional
/// scale, and avoids the subpixel shimmer some users reported around the moving player.
/// </summary>
internal static class GoldPopupSystem
{
    private const string CanvasName = "GoldPopupCanvas_XUAIGNORE";

    private static Canvas s_canvas;

    public static void ShowOverPlayer(long amount)
    {
        if (amount == 0) return;

        try
        {
            EnsureCanvas();
            if (s_canvas == null) return;

            GameObject row = new GameObject("GoldPopupRow", typeof(RectTransform));
            row.transform.SetParent(s_canvas.transform, false);
            RectTransform rowRt = (RectTransform)row.transform;
            rowRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.sizeDelta = new Vector2(220f, 48f);

            int fontSize = Mathf.Max(8, EconomicConfig.Popup.FontSize);
            float iconSize = fontSize + 6f;

            GameObject iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(row.transform, false);
            Image icon = iconGo.AddComponent<Image>();
            if (GoldAssetLoader.HasUiIcon)
                icon.sprite = GoldAssetLoader.UiIcon;
            icon.color = Color.white;
            icon.raycastTarget = false;
            RectTransform iconRt = icon.rectTransform;
            iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(iconSize, iconSize);
            iconRt.anchoredPosition = new Vector2(-50f, 0f);

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(row.transform, false);
            Text label = labelGo.AddComponent<Text>();
            label.font = NoREroMod.Systems.UI.HellGateFontProvider.GetUiFont();
            label.alignment = TextAnchor.MiddleLeft;
            label.text = (amount > 0 ? "+" : "") + amount.ToString();
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            if (!ColorUtility.TryParseHtmlString(EconomicConfig.Popup.TextColorHex, out Color color))
                color = new Color(1f, 0.82f, 0.29f, 1f);
            label.color = color;

            RectTransform labelRt = label.rectTransform;
            labelRt.anchorMin = labelRt.anchorMax = labelRt.pivot = new Vector2(0.5f, 0.5f);
            labelRt.sizeDelta = new Vector2(160f, 48f);
            labelRt.anchoredPosition = new Vector2(20f, 0f);

            GoldPopupRunner runner = row.AddComponent<GoldPopupRunner>();
            runner.Configure(
                EconomicConfig.Popup.RiseDistance,
                EconomicConfig.Popup.DurationSec,
                EconomicConfig.Popup.FadeStartFraction,
                icon,
                label);
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[GoldPopup] Show failed: " + ex.Message);
        }
    }

    private static void EnsureCanvas()
    {
        if (s_canvas != null && s_canvas.gameObject != null) return;

        GameObject host = new GameObject(CanvasName, typeof(RectTransform));
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.hideFlags = HideFlags.HideAndDontSave;
        host.layer = LayerMask.NameToLayer("UI");

        s_canvas = host.AddComponent<Canvas>();
        s_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        s_canvas.overrideSorting = true;
        s_canvas.sortingOrder = 990;

        CanvasScaler scaler = host.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        host.AddComponent<GraphicRaycaster>().enabled = false;
    }
}

/// <summary>
/// Per-popup tween: projects the player's current world position into screen space every
/// frame, applies a configurable rise + fade, then destroys itself.
/// </summary>
internal sealed class GoldPopupRunner : MonoBehaviour
{
    private const float HeadOffsetWorldY = 1.4f;

    private RectTransform _rt;
    private Image _icon;
    private Text _label;
    private float _rise;
    private float _duration;
    private float _fadeStart;
    private float _t;
    private GameObject _player;

    public void Configure(float rise, float duration, float fadeStartFraction, Image icon, Text label)
    {
        _rise = Mathf.Max(0f, rise) * 100f;
        _duration = Mathf.Max(0.1f, duration);
        _fadeStart = Mathf.Clamp01(fadeStartFraction);
        _icon = icon;
        _label = label;
        _rt = (RectTransform)transform;
        _player = NoREroMod.Systems.Cache.UnifiedPlayerCacheManager.GetPlayerObject();
    }

    private void Update()
    {
        _t += Time.deltaTime;
        float n = Mathf.Clamp01(_t / _duration);
        float yOffsetPx = Mathf.SmoothStep(0f, _rise, n);

        if (_player == null)
            _player = NoREroMod.Systems.Cache.UnifiedPlayerCacheManager.GetPlayerObject();

        UnityEngine.Camera mainCam = UnityEngine.Camera.main;
        if (_player != null && mainCam != null)
        {
            Vector3 head = _player.transform.position + new Vector3(0f, HeadOffsetWorldY, 0f);
            Vector3 sp = mainCam.WorldToScreenPoint(head);
            float refY = 1080f;
            float refX = 1920f;
            float screenW = Screen.width <= 0 ? refX : Screen.width;
            float screenH = Screen.height <= 0 ? refY : Screen.height;
            float anchoredX = (sp.x / screenW * refX) - refX * 0.5f;
            float anchoredY = (sp.y / screenH * refY) - refY * 0.5f + yOffsetPx;
            _rt.anchoredPosition = new Vector2(anchoredX, anchoredY);
        }

        float alpha = 1f;
        if (n > _fadeStart && _fadeStart < 1f)
        {
            float fadeT = (n - _fadeStart) / (1f - _fadeStart);
            alpha = 1f - Mathf.Clamp01(fadeT);
        }
        if (_icon != null)
        {
            Color c = _icon.color; c.a = alpha; _icon.color = c;
        }
        if (_label != null)
        {
            Color c = _label.color; c.a = alpha; _label.color = c;
        }

        if (_t >= _duration)
            Destroy(gameObject);
    }
}
