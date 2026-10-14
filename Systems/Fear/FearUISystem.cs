using System;
using System.Collections;
using HarmonyLib;
using NoREroMod.Systems.UI;
using UnityEngine;
using UnityEngine.UI;

namespace NoREroMod.Systems.Fear;

/// <summary>
/// Bipolar Fear / Confidence bar. Own module — not parented under Rage UI.
/// Bottom-center (Mercy mirror inset). Visibility via <see cref="HudVisibilityGate"/>.
/// </summary>
internal static class FearUISystem
{
    private const string CanvasName = "FearOverlayCanvas_XUAIGNORE";
    private const string RootName = "FearBarRoot_XUAIGNORE";
    private const float BarWidth = 300f;
    private const float BarHeight = 14f;
    private const float HalfWidth = BarWidth * 0.5f;

    private static readonly Vector2 HudBasePosition = new Vector2(0f, 32f);
    private static readonly Color FearColorDeep = new Color(0.25f, 0.4f, 0.95f, 1f);
    private static readonly Color FearColorPanic = new Color(0.55f, 0.2f, 0.95f, 1f);
    private static readonly Color ConfColorWarm = new Color(1f, 0.72f, 0.22f, 1f);
    private static readonly Color ConfColorHot = new Color(1f, 0.45f, 0.15f, 1f);

    private static RectTransform _canvasRect;
    private static RectTransform _root;
    private static CanvasGroup _rootCg;
    private static Image _bg;
    private static Image _frame;
    private static Image _leftFill;
    private static Image _rightFill;
    private static Image _centerTick;
    private static Image _markNeg50;
    private static Image _markPos50;
    private static Text _label;
    private static Text _leftCap;
    private static Text _rightCap;
    private static float _displayMorale;
    private static Sprite _whiteSprite;

    internal static bool ShouldShowBarForUI()
    {
        return HudVisibilityGate.ShouldShowGameplayHud();
    }

    internal static void InitializeFromPlugin()
    {
        try { Refresh(); }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[FearUI] Initialize failed: " + ex.Message);
        }
    }

    internal static void Refresh()
    {
        if (!FearConfig.IsEnabled || !(FearConfig.ShowBar?.Value ?? true))
        {
            DestroyExisting();
            return;
        }
        EnsureCanvas();
        EnsureBar();
        ForcePosition();
    }

    internal static void DestroyExisting()
    {
        if (_canvasRect != null)
        {
            UnityEngine.Object.Destroy(_canvasRect.gameObject);
            _canvasRect = null;
            _root = null;
            _rootCg = null;
            _bg = null;
            _frame = null;
            _leftFill = null;
            _rightFill = null;
            _centerTick = null;
            _markNeg50 = null;
            _markPos50 = null;
            _label = null;
            _leftCap = null;
            _rightCap = null;
        }
    }

    [HarmonyPatch(typeof(UImng), "Start")]
    private static class UImngStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UImng __instance)
        {
            try
            {
                if (!FearConfig.IsEnabled || !(FearConfig.ShowBar?.Value ?? true))
                {
                    DestroyExisting();
                    return;
                }
                __instance.StartCoroutine(DelayedSetup());
            }
            catch { }
        }

        private static IEnumerator DelayedSetup()
        {
            yield return null;
            yield return null;
            Refresh();
        }
    }

    private static void EnsureCanvas()
    {
        if (_canvasRect != null) return;
        GameObject existing = GameObject.Find(CanvasName);
        if (existing != null)
            UnityEngine.Object.Destroy(existing);

        GameObject go = new GameObject(CanvasName);
        UnityEngine.Object.DontDestroyOnLoad(go);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 460;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        go.AddComponent<GraphicRaycaster>().enabled = false;
        _canvasRect = go.GetComponent<RectTransform>();
    }

    private static void EnsureBar()
    {
        if (_root != null) return;

        Sprite white = GetWhiteSprite();
        GameObject rootGo = new GameObject(RootName);
        rootGo.transform.SetParent(_canvasRect, false);
        _root = rootGo.AddComponent<RectTransform>();
        _root.anchorMin = new Vector2(0.5f, 0f);
        _root.anchorMax = new Vector2(0.5f, 0f);
        _root.pivot = new Vector2(0.5f, 0f);
        _root.sizeDelta = new Vector2(BarWidth + 80f, 52f);
        _rootCg = rootGo.AddComponent<CanvasGroup>();
        _rootCg.blocksRaycasts = false;
        _rootCg.interactable = false;
        _rootCg.alpha = 0f;

        _frame = CreateImage("FearBarFrame", _root, white, new Color(1f, 1f, 1f, 0.18f));
        SetBarChild(_frame.rectTransform, 0f, -1f, BarWidth + 4f, BarHeight + 4f);

        _bg = CreateImage("FearBarBg", _root, white, new Color(0.04f, 0.05f, 0.08f, 0.72f));
        SetBarChild(_bg.rectTransform, 0f, 0f, BarWidth, BarHeight);

        _leftFill = CreateImage("FearBarLeft", _root, white, FearColorDeep);
        RectTransform leftRt = _leftFill.rectTransform;
        leftRt.anchorMin = new Vector2(0.5f, 0f);
        leftRt.anchorMax = new Vector2(0.5f, 0f);
        leftRt.pivot = new Vector2(1f, 0f);
        leftRt.anchoredPosition = new Vector2(0f, 1f);
        leftRt.sizeDelta = new Vector2(0f, BarHeight - 2f);

        _rightFill = CreateImage("FearBarRight", _root, white, ConfColorWarm);
        RectTransform rightRt = _rightFill.rectTransform;
        rightRt.anchorMin = new Vector2(0.5f, 0f);
        rightRt.anchorMax = new Vector2(0.5f, 0f);
        rightRt.pivot = new Vector2(0f, 0f);
        rightRt.anchoredPosition = new Vector2(0f, 1f);
        rightRt.sizeDelta = new Vector2(0f, BarHeight - 2f);

        _markNeg50 = CreateImage("FearMarkNeg50", _root, white, new Color(1f, 1f, 1f, 0.35f));
        SetBarChild(_markNeg50.rectTransform, -HalfWidth * 0.5f, 0f, 2f, BarHeight + 2f);

        _markPos50 = CreateImage("FearMarkPos50", _root, white, new Color(1f, 1f, 1f, 0.35f));
        SetBarChild(_markPos50.rectTransform, HalfWidth * 0.5f, 0f, 2f, BarHeight + 2f);

        _centerTick = CreateImage("FearBarTick", _root, white, new Color(1f, 1f, 1f, 0.95f));
        SetBarChild(_centerTick.rectTransform, 0f, -1f, 3f, BarHeight + 6f);

        _label = CreateLabel("FearBarLabel", _root, new Vector2(0f, 20f), BarWidth + 60f, 18f, 14, TextAnchor.MiddleCenter);
        _label.text = "FEAR  ·  CONFIDENCE";
        Outline outline = _label.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = new Vector2(1f, -1f);

        _leftCap = CreateLabel("FearCapLeft", _root, new Vector2(-HalfWidth - 4f, 1f), 40f, 14f, 11, TextAnchor.MiddleRight);
        _leftCap.text = "FEAR";
        _leftCap.color = FearColorDeep;

        _rightCap = CreateLabel("FearCapRight", _root, new Vector2(HalfWidth + 4f, 1f), 56f, 14f, 11, TextAnchor.MiddleLeft);
        _rightCap.text = "CONF";
        _rightCap.color = ConfColorWarm;
        RectTransform leftCapRt = _leftCap.rectTransform;
        leftCapRt.pivot = new Vector2(1f, 0f);
        RectTransform rightCapRt = _rightCap.rectTransform;
        rightCapRt.pivot = new Vector2(0f, 0f);

        FearBarDriver driver = rootGo.AddComponent<FearBarDriver>();
        driver.Bind(UpdateVisuals);
        ForcePosition();
        _displayMorale = FearSystem.Morale;
        UpdateVisuals();
    }

    private static void SetBarChild(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static Text CreateLabel(string name, Transform parent, Vector2 pos, float w, float h, int size, TextAnchor align)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(w, h);
        Text t = go.AddComponent<Text>();
        t.font = HellGateFontProvider.GetUiFont();
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        img.type = Image.Type.Simple;
        return img;
    }

    private static void ForcePosition()
    {
        if (_root == null) return;
        _root.anchorMin = new Vector2(0.5f, 0f);
        _root.anchorMax = new Vector2(0.5f, 0f);
        _root.pivot = new Vector2(0.5f, 0f);
        float x = HudBasePosition.x + (FearConfig.BarOffsetX?.Value ?? 0f);
        float y = HudBasePosition.y + (FearConfig.BarOffsetY?.Value ?? 0f);
        _root.anchoredPosition = new Vector2(x, y);
    }

    private static void UpdateVisuals()
    {
        if (_leftFill == null || _rightFill == null) return;
        if (!FearConfig.IsEnabled || !(FearConfig.ShowBar?.Value ?? true))
        {
            if (_rootCg != null) _rootCg.alpha = 0f;
            return;
        }

        if (_root != null && !_root.gameObject.activeSelf)
            _root.gameObject.SetActive(true);

        ForcePosition();

        bool shouldShow = ShouldShowBarForUI();
        float opacity = FearConfig.BarOpacity?.Value ?? 0.9f;
        if (_rootCg == null && _root != null)
            _rootCg = _root.GetComponent<CanvasGroup>();
        if (_rootCg != null)
        {
            _rootCg.alpha = shouldShow ? opacity : 0f;
            _rootCg.blocksRaycasts = false;
            _rootCg.interactable = false;
        }
        if (!shouldShow) return;

        float target = FearSystem.Morale;
        _displayMorale = Mathf.Lerp(_displayMorale, target, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));

        float left = Mathf.Clamp01(-_displayMorale / 100f);
        float right = Mathf.Clamp01(_displayMorale / 100f);
        _leftFill.rectTransform.sizeDelta = new Vector2(HalfWidth * left, BarHeight - 2f);
        _rightFill.rectTransform.sizeDelta = new Vector2(HalfWidth * right, BarHeight - 2f);

        float panicT = FearSystem.PanicT;
        float confT = FearSystem.ConfidenceT;
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * (2.2f + 2.5f * Mathf.Max(panicT, confT)));

        Color fearCol = Color.Lerp(FearColorDeep, FearColorPanic, panicT);
        fearCol.a = left > 0.01f ? pulse : 0.95f;
        _leftFill.color = fearCol;

        Color confCol = Color.Lerp(ConfColorWarm, ConfColorHot, confT);
        confCol.a = right > 0.01f ? pulse : 0.95f;
        _rightFill.color = confCol;

        if (_centerTick != null)
        {
            float tickPulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 1.6f);
            _centerTick.color = new Color(1f, 1f, 1f, tickPulse);
        }

        if (_label != null)
        {
            if (_displayMorale < -0.5f)
            {
                _label.text = "FEAR  " + Mathf.RoundToInt(-_displayMorale) + "%";
                _label.color = Color.Lerp(Color.white, FearColorPanic, panicT * 0.7f);
            }
            else if (_displayMorale > 0.5f)
            {
                _label.text = "CONFIDENCE  " + Mathf.RoundToInt(_displayMorale) + "%";
                _label.color = Color.Lerp(Color.white, ConfColorHot, confT * 0.7f);
            }
            else
            {
                _label.text = "FEAR  ·  CONFIDENCE";
                _label.color = new Color(0.92f, 0.92f, 0.95f, 1f);
            }
        }

        if (_leftCap != null) _leftCap.color = Color.Lerp(FearColorDeep, FearColorPanic, panicT);
        if (_rightCap != null) _rightCap.color = Color.Lerp(ConfColorWarm, ConfColorHot, confT);
    }

    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        return _whiteSprite;
    }

    private sealed class FearBarDriver : MonoBehaviour
    {
        private Action _tick;
        internal void Bind(Action tick) { _tick = tick; }
        private void LateUpdate()
        {
            try { _tick?.Invoke(); } catch { }
        }
    }
}
