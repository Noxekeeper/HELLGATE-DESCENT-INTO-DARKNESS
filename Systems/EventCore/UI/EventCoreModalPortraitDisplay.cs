using NoREroMod;
using UnityEngine;
using UnityEngine.UI;

namespace NoREroMod.Systems.EventCore.UI;

/// <summary>Side portrait clips on the EventCore modal (PNG frame cycle).</summary>
internal sealed class EventCoreModalPortraitDisplay : MonoBehaviour
{
    private const float ReferenceBustHeightPx = 288f;
    private const float ReferenceBustWidthPx = 306f;

    private Image _leftImage;
    private Image _rightImage;
    private float _fitWidthPx = 320f;
    private float _fitHeightPx = 360f;
    private float _unifiedPortraitHeightPx = 360f;

    private string _leftExpr;
    private string _rightExpr;
    private Sprite[] _leftFrames = new Sprite[0];
    private Sprite[] _rightFrames = new Sprite[0];
    private int _leftIndex;
    private int _rightIndex;
    private float _timer;

    private static string _aradiaRoot;
    private static string _touzokuRoot;

    internal static EventCoreModalPortraitDisplay Attach(GameObject root)
    {
        var display = root.GetComponent<EventCoreModalPortraitDisplay>();
        if (display == null)
            display = root.AddComponent<EventCoreModalPortraitDisplay>();
        return display;
    }

    internal void ConfigureLayout(Transform panelTransform, float sideWidthPx, float panelHeightPx)
    {
        _fitWidthPx = Mathf.Max(64f, sideWidthPx - 16f);
        _fitHeightPx = Mathf.Max(64f, panelHeightPx * 0.92f);
        _unifiedPortraitHeightPx = ComputeUnifiedPortraitHeight(_fitWidthPx, _fitHeightPx);
        EnsureImages(panelTransform);
    }

    internal void SetPair(EventCorePortraitPair pair)
    {
        if (_aradiaRoot == null)
            _aradiaRoot = EventCorePortraitPaths.ResolveAradiaRoot();
        if (_touzokuRoot == null)
            _touzokuRoot = EventCorePortraitPaths.ResolveTouzokuRoot();

        _leftExpr = pair.LeftExpression;
        _rightExpr = pair.RightExpression;

        _leftFrames = LoadSide(_aradiaRoot, _leftExpr);
        _rightFrames = LoadSide(_touzokuRoot, _rightExpr);

        _leftIndex = 0;
        _rightIndex = 0;
        _timer = 0f;

        ApplyFrame(_leftImage, _leftFrames, ref _leftIndex, isLeft: true);
        ApplyFrame(_rightImage, _rightFrames, ref _rightIndex, isLeft: false);
    }

    private void Update()
    {
        if (!EventCoreModalCanvas.IsVisible)
            return;

        float frameSeconds = Plugin.portraitModFrameSeconds != null
            ? Mathf.Max(0.04f, Plugin.portraitModFrameSeconds.Value)
            : 0.12f;

        _timer += Time.unscaledDeltaTime;
        if (_timer < frameSeconds)
            return;

        _timer = 0f;

        if (_leftFrames != null && _leftFrames.Length > 1)
        {
            _leftIndex = (_leftIndex + 1) % _leftFrames.Length;
            ApplyFrame(_leftImage, _leftFrames, ref _leftIndex, isLeft: true);
        }

        if (_rightFrames != null && _rightFrames.Length > 1)
        {
            _rightIndex = (_rightIndex + 1) % _rightFrames.Length;
            ApplyFrame(_rightImage, _rightFrames, ref _rightIndex, isLeft: false);
        }
    }

    private static Sprite[] LoadSide(string root, string expression)
    {
        if (string.IsNullOrEmpty(expression))
            return new Sprite[0];
        return EventCorePortraitClipLoader.GetFrames(root, expression);
    }

    private void ApplyFrame(Image image, Sprite[] frames, ref int index, bool isLeft)
    {
        if (image == null)
            return;

        if (frames == null || frames.Length == 0)
        {
            image.enabled = false;
            image.sprite = null;
            return;
        }

        if (index < 0 || index >= frames.Length)
            index = 0;

        image.enabled = true;
        image.sprite = frames[index];
        FitPortraitBust(image, _fitWidthPx, _unifiedPortraitHeightPx, isLeft);
    }

    private static float ComputeUnifiedPortraitHeight(float maxW, float maxH)
    {
        float heightCapFromWidth = maxW * (ReferenceBustHeightPx / ReferenceBustWidthPx);
        return Mathf.Min(maxH, heightCapFromWidth);
    }

    private static float GetCharacterDisplayScale(bool isLeft)
    {
        if (isLeft)
        {
            if (Plugin.eventCoreBrokerPortraitAradiaScale != null)
                return Mathf.Max(0.25f, Plugin.eventCoreBrokerPortraitAradiaScale.Value);
            return 1f;
        }

        if (Plugin.eventCoreBrokerPortraitTouzokuScale != null)
            return Mathf.Max(0.25f, Plugin.eventCoreBrokerPortraitTouzokuScale.Value);
        return 1f;
    }

    /// <summary>
    /// One shared base scale (tall bust height) times per-character tuning.
    /// Aradia PNGs are often cropped tighter on the face than Touzoku hood sprites.
    /// </summary>
    private void FitPortraitBust(Image image, float maxW, float targetHeightPx, bool isLeft)
    {
        if (image == null || image.sprite == null)
            return;

        float w = image.sprite.rect.width;
        float h = image.sprite.rect.height;
        if (w <= 0.01f || h <= 0.01f)
            return;

        float baseScale = (targetHeightPx / ReferenceBustHeightPx) * GetCharacterDisplayScale(isLeft);
        float displayW = w * baseScale;
        float displayH = h * baseScale;
        if (displayW > maxW)
        {
            float clamp = maxW / displayW;
            displayW = maxW;
            displayH *= clamp;
        }

        image.rectTransform.sizeDelta = new Vector2(displayW, displayH);
    }

    private void EnsureImages(Transform panelTransform)
    {
        if (_leftImage == null)
            _leftImage = CreatePortraitImage(panelTransform, "EventCore_PortraitLeft", leftSide: true);
        if (_rightImage == null)
            _rightImage = CreatePortraitImage(panelTransform, "EventCore_PortraitRight", leftSide: false);
    }

    private static Image CreatePortraitImage(Transform parent, string name, bool leftSide)
    {
        var go = new GameObject(name);
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        if (leftSide)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(10f, 12f);
        }
        else
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-10f, 12f);
        }

        rt.sizeDelta = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = false;
        img.color = Color.white;
        img.enabled = false;
        return img;
    }

    private void OnDestroy()
    {
        _leftImage = null;
        _rightImage = null;
    }
}
