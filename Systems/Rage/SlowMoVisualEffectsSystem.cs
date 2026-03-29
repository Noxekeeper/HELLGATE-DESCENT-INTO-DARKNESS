using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NoREroMod.Systems.Rage;

/// <summary>
/// Blue edge bars (top/bottom) on Time Slow-Mo (T) activation.
/// </summary>
internal static class SlowMoVisualEffectsSystem
{
    private const string CanvasObjectName = "SlowMoVisualEffectsCanvas_XUAIGNORE";

    private static GameObject? _canvasObject;
    private static RectTransform? _canvasRect;
    private static GameObject? _edgeBarsObject;
    private static Image? _edgeBarsImage;
    private static Coroutine? _edgeBarsCoroutine;
    private static Sprite? _cachedBarsSprite;
    private static bool _initialized;

    private static Color EdgeBarsColor => new Color(
        Plugin.slowMoEdgeBarsColorR?.Value ?? 0.3f,
        Plugin.slowMoEdgeBarsColorG?.Value ?? 0.6f,
        Plugin.slowMoEdgeBarsColorB?.Value ?? 1f,
        1f
    );

    internal static void Initialize()
    {
        if (_initialized) return;
        
        try
        {
            TimeSlowMoSystem.OnActivated += OnSlowMoActivated;
            TimeSlowMoSystem.OnDeactivated += OnSlowMoDeactivated;
            
            // Preload gradient sprite to avoid freeze on first activation
            EnsureBarsSprite();
            
            _initialized = true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[SlowMoVisual] Init error: {ex.Message}");
        }
    }

    private static void OnSlowMoActivated()
    {
        try
        {
            EnsureCanvas();
            EnsureEdgeBars();
            StartEdgeBarsCoroutine();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[SlowMoVisual] Activate error: {ex.Message}");
        }
    }

    private static void OnSlowMoDeactivated()
    {
        try
        {
            StopEdgeBarsCoroutine();
            if (_edgeBarsObject != null) _edgeBarsObject.SetActive(false);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[SlowMoVisual] Deactivate error: {ex.Message}");
        }
    }

    private static void EnsureCanvas()
    {
        if (_canvasObject != null) return;

        _canvasObject = new GameObject(CanvasObjectName);
        UnityEngine.Object.DontDestroyOnLoad(_canvasObject);

        var canvas = _canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 996;

        var scaler = _canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasObject.AddComponent<GraphicRaycaster>();

        _canvasRect = _canvasObject.GetComponent<RectTransform>();
        _canvasRect.anchorMin = Vector2.zero;
        _canvasRect.anchorMax = Vector2.one;
        _canvasRect.sizeDelta = Vector2.zero;
        _canvasRect.anchoredPosition = Vector2.zero;
    }

    private static void EnsureEdgeBars()
    {
        if (_edgeBarsObject != null) return;
        if (_canvasRect == null) return;

        _edgeBarsObject = new GameObject("SlowMoEdgeBars_XUAIGNORE");
        _edgeBarsObject.transform.SetParent(_canvasRect, false);

        var barsRect = _edgeBarsObject.AddComponent<RectTransform>();
        barsRect.anchorMin = Vector2.zero;
        barsRect.anchorMax = Vector2.one;
        barsRect.sizeDelta = Vector2.zero;
        barsRect.anchoredPosition = Vector2.zero;

        _edgeBarsImage = _edgeBarsObject.AddComponent<Image>();
        _edgeBarsImage.color = new Color(EdgeBarsColor.r, EdgeBarsColor.g, EdgeBarsColor.b, 0f);
        _edgeBarsImage.sprite = _cachedBarsSprite;
        _edgeBarsImage.type = Image.Type.Simple;
        _edgeBarsImage.raycastTarget = false;
    }
    
    private static void EnsureBarsSprite()
    {
        if (_cachedBarsSprite != null) return;
        _cachedBarsSprite = CreateHorizontalBarsSprite(64, 64, EdgeBarsColor);
    }

    private static void StartEdgeBarsCoroutine()
    {
        if (_canvasObject == null || _edgeBarsImage == null) return;

        _edgeBarsObject?.SetActive(true);

        var runner = _canvasObject.GetComponent<SlowMoVisualRunner>();
        if (runner == null) runner = _canvasObject.AddComponent<SlowMoVisualRunner>();

        if (_edgeBarsCoroutine != null) runner.StopCoroutine(_edgeBarsCoroutine);
        _edgeBarsCoroutine = runner.StartCoroutine(EdgeBarsCoroutine());
    }

    private static void StopEdgeBarsCoroutine()
    {
        if (_edgeBarsCoroutine != null && _canvasObject != null)
        {
            var runner = _canvasObject.GetComponent<SlowMoVisualRunner>();
            if (runner != null) runner.StopCoroutine(_edgeBarsCoroutine);
            _edgeBarsCoroutine = null;
        }
    }

    private static IEnumerator EdgeBarsCoroutine()
    {
        if (_edgeBarsImage == null) yield break;

        float phase1Duration = 0.3f;
        float elapsed = 0f;
        while (elapsed < phase1Duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 0.5f, elapsed / phase1Duration);
            _edgeBarsImage.color = new Color(EdgeBarsColor.r, EdgeBarsColor.g, EdgeBarsColor.b, alpha);
            yield return null;
        }

        float baseAlpha = Plugin.slowMoEdgeBarsMaxAlpha?.Value ?? 0.5f;
        float minAlpha = baseAlpha - 0.03f;
        float maxAlpha = baseAlpha + 0.03f;
        float pulseSpeed = 0.75f;
        float timeElapsed = 0f;

        while (TimeSlowMoSystem.IsActive)
        {
            timeElapsed += Time.deltaTime;
            float t = (Mathf.Sin(timeElapsed * pulseSpeed * 2f * Mathf.PI) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
            _edgeBarsImage.color = new Color(EdgeBarsColor.r, EdgeBarsColor.g, EdgeBarsColor.b, alpha);
            yield return null;
        }

        elapsed = 0f;
        float startAlpha = _edgeBarsImage.color.a;
        while (elapsed < 0.5f)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, 0f, elapsed / 0.5f);
            _edgeBarsImage.color = new Color(EdgeBarsColor.r, EdgeBarsColor.g, EdgeBarsColor.b, alpha);
            yield return null;
        }

        if (_edgeBarsObject != null) _edgeBarsObject.SetActive(false);
        _edgeBarsCoroutine = null;
    }

    private static Sprite CreateHorizontalBarsSprite(int width, int height, Color color)
    {
        int textureWidth = width;
        int textureHeight = height;
        var texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        var pixels = new Color[textureWidth * textureHeight];
        float barHeight = Mathf.Max(4f, textureHeight * 0.2f);
        float alphaMax = 1f;

        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                float alpha = 0f;
                if (y < barHeight)
                    alpha = (1f - y / barHeight) * alphaMax;
                else if (y >= textureHeight - barHeight)
                    alpha = ((y - (textureHeight - barHeight)) / barHeight) * alphaMax;
                pixels[y * textureWidth + x] = new Color(color.r, color.g, color.b, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, textureWidth, textureHeight), new Vector2(0.5f, 0.5f));
    }
}

internal class SlowMoVisualRunner : MonoBehaviour { }
