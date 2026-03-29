using System;
using UnityEngine;
using UnityEngine.UI;
using Spine.Unity;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Systems.Rage;

/// <summary>
/// Red glow on Aradia hands (bone3, bone8) during Rage.
/// </summary>
internal static class RageHandsGlowSystem
{
    private const string RunnerObjectName = "RageHandsGlowRunner_XUAIGNORE";
    private const string CanvasObjectName = "RageHandsGlowCanvas_XUAIGNORE";

    private static Color GlowColor => new Color(
        Plugin.rageHandsGlowColorR?.Value ?? 1f,
        Plugin.rageHandsGlowColorG?.Value ?? 0f,
        Plugin.rageHandsGlowColorB?.Value ?? 0.15f,
        Plugin.rageHandsGlowAlpha?.Value ?? 0.85f
    );
    private static float GlowSizePx => Plugin.rageHandsGlowSizePx?.Value ?? 96f; // Increased from 48f for bigger glow
    private static readonly string[] BoneNames = { "bone3", "bone8" };

    private static GameObject? _canvasObject;
    private static RectTransform? _canvasRect;
    private static RageHandsGlowRunner? _runner;
    private static Image?[] _glowImages = new Image?[2];
    private static RectTransform?[] _glowRects = new RectTransform?[2];
    private static Sprite? _glowSprite;
    private static bool _initialized;
    private static SkeletonAnimation? _cachedSpine;

    internal static void Initialize()
    {
        if (_initialized) return;
        
        try
        {
            RageSystem.OnActivated += OnRageActivated;
            RageSystem.OnDeactivated += OnRageDeactivated;
            _initialized = true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RageHandsGlow] Init error: {ex.Message}");
        }
    }

    private static void OnRageActivated()
    {
        if (!(Plugin.rageHandsGlowEnable?.Value ?? true)) return;
        try
        {
            EnsureCanvas();
            EnsureRunner();
            EnsureGlowSprites();
            CreateGlowObjects();
            if (_runner != null) _runner.enabled = true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RageHandsGlow] Activate error: {ex.Message}");
        }
    }

    private static void OnRageDeactivated()
    {
        try
        {
            _cachedSpine = null;
            DestroyGlowObjects();
            if (_runner != null) _runner.enabled = false;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RageHandsGlow] Deactivate error: {ex.Message}");
        }
    }

    private static void EnsureCanvas()
    {
        if (_canvasObject != null) return;

        _canvasObject = new GameObject(CanvasObjectName);
        UnityEngine.Object.DontDestroyOnLoad(_canvasObject);

        var canvas = _canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 997;

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

    private static void EnsureRunner()
    {
        if (_runner != null) return;
        if (_canvasObject == null) return;

        var runnerObj = new GameObject(RunnerObjectName);
        runnerObj.transform.SetParent(_canvasObject.transform, false);
        _runner = runnerObj.AddComponent<RageHandsGlowRunner>();
        _runner.SetCanvasRect(_canvasRect!);
    }

    private static void EnsureGlowSprites()
    {
        if (_glowSprite != null) return;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        float cx = size * 0.5f;
        float cy = size * 0.5f;
        float maxR = Mathf.Sqrt(cx * cx + cy * cy);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 1f - Mathf.Clamp01(dist / maxR);
                alpha *= alpha;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);
        _glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static void CreateGlowObjects()
    {
        if (_canvasRect == null || _glowSprite == null) return;

        for (int i = 0; i < 2; i++)
        {
            if (_glowImages[i] != null) continue;

            var go = new GameObject($"RageHandGlow_{BoneNames[i]}_XUAIGNORE");
            go.transform.SetParent(_canvasRect, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(GlowSizePx, GlowSizePx);

            var img = go.AddComponent<Image>();
            img.sprite = _glowSprite;
            img.color = GlowColor;
            img.raycastTarget = false;

            _glowImages[i] = img;
            _glowRects[i] = rect;
        }
    }

    private static void DestroyGlowObjects()
    {
        for (int i = 0; i < 2; i++)
        {
            if (_glowRects[i] != null && _glowRects[i].gameObject != null)
            {
                UnityEngine.Object.Destroy(_glowRects[i].gameObject);
            }
            _glowImages[i] = null;
            _glowRects[i] = null;
        }
    }

    internal static void UpdatePositions(RectTransform canvasRect)
    {
        if (!RageSystem.IsActive) return;
        if (_glowRects[0] == null || _glowRects[1] == null) return;

        var playerObj = UnifiedPlayerCacheManager.GetPlayerObject();
        if (playerObj == null) { _cachedSpine = null; return; }

        if (_cachedSpine == null || _cachedSpine.skeleton == null)
        {
            _cachedSpine = playerObj.GetComponentInChildren<SkeletonAnimation>(true);
            if (_cachedSpine == null || _cachedSpine.skeleton == null) return;
        }

        var camObj = UnifiedCameraCacheManager.GetMainCamera();
        if (camObj == null) return;
        var cam = camObj.GetComponent<UnityEngine.Camera>();
        if (cam == null) return;

        for (int i = 0; i < 2; i++)
        {
            var bone = _cachedSpine.skeleton.FindBone(BoneNames[i]);
            if (bone == null) continue;

            Vector3 worldPos = _cachedSpine.transform.TransformPoint(bone.WorldX, bone.WorldY, 0f);
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0) continue;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out Vector2 localPos);
            _glowRects[i]!.anchoredPosition = localPos;
        }
    }
}

internal class RageHandsGlowRunner : MonoBehaviour
{
    private RectTransform? _canvasRect;
    private int _frameCounter;

    internal void SetCanvasRect(RectTransform rect)
    {
        _canvasRect = rect;
    }

    private void LateUpdate()
    {
        if (_canvasRect == null || !RageSystem.IsActive) return;

        // Performance optimization: update positions less frequently based on performance mode
        _frameCounter++;
        int updateInterval = Plugin.ragePerformanceMode?.Value ?? false ? 6 : 3; // Half frequency in performance mode
        if (_frameCounter % updateInterval != 0) return;

        RageHandsGlowSystem.UpdatePositions(_canvasRect);
    }
}
