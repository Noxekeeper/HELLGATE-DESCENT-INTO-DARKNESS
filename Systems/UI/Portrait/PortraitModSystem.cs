using NoREroMod.Patches.UI.MindBroken;
using Spine.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NoREroMod.Systems.UI.Portrait;

/// <summary>
/// HUD portrait override: hides <c>UIface</c> <see cref="SkeletonGraphic"/> and draws a sibling <see cref="Image"/> with cycled PNG frames.
/// </summary>
internal static class PortraitModSystem
{
    private static bool _sceneHooked;
    private static GameObject _uiFaceGo;
    private static SkeletonGraphic _vanillaSpine;
    private static GameObject _overlayGo;
    private static Image _overlayImage;

    private static string _rootDir;
    private static string _lastStateKey;
    private static Sprite[] _activeSprites;
    private static int _spriteIndex;
    private static float _frameTimer;
    private static Sprite _lastSpriteForNativeSize;

    /// <summary>Registers <see cref="SceneManager.sceneLoaded"/>; call from <see cref="Plugin.Awake"/>. When disabled, <see cref="Process"/> is a no-op.</summary>
    internal static void Initialize()
    {
        if (_sceneHooked) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
        _sceneHooked = true;
    }

    internal static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InvalidateUiReferences();
    }

    internal static void Cleanup()
    {
        if (_sceneHooked)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _sceneHooked = false;
        }

        DestroyOverlay();
        PortraitAssetLoader.ClearCacheAndDestroySprites();
        _uiFaceGo = null;
        _vanillaSpine = null;
        _rootDir = null;
        _lastStateKey = null;
        _activeSprites = null;
        _lastSpriteForNativeSize = null;
    }

    private static void InvalidateUiReferences()
    {
        DestroyOverlay();
        _uiFaceGo = null;
        _vanillaSpine = null;
        _lastStateKey = null;
        _activeSprites = null;
        _spriteIndex = 0;
        _frameTimer = 0f;
        _lastSpriteForNativeSize = null;
        PortraitAssetLoader.ClearCacheAndDestroySprites();
    }

    private static void DestroyOverlay()
    {
        if (_overlayGo != null)
        {
            Object.Destroy(_overlayGo);
            _overlayGo = null;
            _overlayImage = null;
        }
    }

    internal static void Process(playercon pc, PlayerStatus ps, bool eroflag)
    {
        if (!Plugin.enablePortraitMod.Value)
        {
            if (_vanillaSpine != null)
                _vanillaSpine.enabled = true;
            DestroyOverlay();
            return;
        }

        if (pc == null || ps == null)
            return;

        if (_uiFaceGo == null)
        {
            _uiFaceGo = GameObject.Find("UIface");
            if (_uiFaceGo == null)
                return;
            _vanillaSpine = _uiFaceGo.GetComponent<SkeletonGraphic>();
        }

        if (_vanillaSpine != null)
            _vanillaSpine.enabled = false;

        EnsureOverlay();
        if (_overlayImage == null)
            return;

        SyncOverlayScale();

        if (string.IsNullOrEmpty(_rootDir))
            _rootDir = PortraitAssetLoader.ResolveRootDirectory();

        string stateKey = PortraitStateResolver.ResolveKey(eroflag, ps.CostumeBreak, MindBrokenSystem.Percent);

        if (stateKey != _lastStateKey)
        {
            _lastStateKey = stateKey;
            _spriteIndex = 0;
            _frameTimer = 0f;
            _lastSpriteForNativeSize = null;
            _activeSprites = PortraitAssetLoader.GetOrLoadSprites(_rootDir, stateKey);
            if (_activeSprites == null || _activeSprites.Length == 0)
                Plugin.Log?.LogWarning($"[PortraitMod] No PNGs for state '{stateKey}' under {_rootDir}");
        }

        float interval = Plugin.portraitModFrameSeconds.Value;
        const float minInterval = 1e-3f;
        if (interval < minInterval)
            interval = minInterval;
        _frameTimer += Time.deltaTime;

        if (_activeSprites != null && _activeSprites.Length > 0)
        {
            while (_frameTimer >= interval)
            {
                _frameTimer -= interval;
                _spriteIndex = (_spriteIndex + 1) % _activeSprites.Length;
            }

            ApplyPortraitSprite(_activeSprites[_spriteIndex]);
            _overlayImage.enabled = true;
        }
        else
        {
            _overlayImage.sprite = null;
            _overlayImage.enabled = false;
        }
    }

    private static void EnsureOverlay()
    {
        if (_overlayGo != null || _uiFaceGo == null)
            return;

        Transform parent = _uiFaceGo.transform.parent;
        if (parent == null)
            return;

        var faceRt = _uiFaceGo.GetComponent<RectTransform>();
        if (faceRt == null)
            return;

        _overlayGo = new GameObject("HellGate_PortraitMod_Overlay", typeof(RectTransform));
        _overlayGo.layer = _uiFaceGo.layer;
        var overlayRt = _overlayGo.GetComponent<RectTransform>();
        overlayRt.SetParent(parent, false);
        overlayRt.anchorMin = faceRt.anchorMin;
        overlayRt.anchorMax = faceRt.anchorMax;
        overlayRt.pivot = faceRt.pivot;
        overlayRt.anchoredPosition = faceRt.anchoredPosition;
        // Intentionally omit UIface sizeDelta/localScale: Spine uses a tight layout box; PNG dimensions come from SetNativeSize + DisplayScale.
        overlayRt.sizeDelta = Vector2.zero;
        overlayRt.localRotation = faceRt.localRotation;
        ApplyScaleToOverlay(overlayRt);

        _overlayGo.AddComponent<CanvasRenderer>();
        _overlayImage = _overlayGo.AddComponent<Image>();
        _overlayImage.raycastTarget = false;
        _overlayImage.preserveAspect = true;

        _overlayGo.transform.SetSiblingIndex(_uiFaceGo.transform.GetSiblingIndex() + 1);
    }

    private static void SyncOverlayScale()
    {
        if (_overlayGo == null)
            return;
        var overlayRt = _overlayGo.GetComponent<RectTransform>();
        if (overlayRt == null)
            return;
        ApplyScaleToOverlay(overlayRt);
    }

    private static void ApplyScaleToOverlay(RectTransform overlayRt)
    {
        float m = Plugin.portraitModDisplayScale.Value;
        const float minScale = 0.01f;
        if (m < minScale)
            m = minScale;
        overlayRt.localScale = new Vector3(m, m, 1f);
    }

    private static void ApplyPortraitSprite(Sprite sp)
    {
        if (_overlayImage == null || sp == null)
            return;
        bool sizeMayChange = _lastSpriteForNativeSize == null ||
                             !Mathf.Approximately(_lastSpriteForNativeSize.rect.width, sp.rect.width) ||
                             !Mathf.Approximately(_lastSpriteForNativeSize.rect.height, sp.rect.height);
        _overlayImage.sprite = sp;
        if (sizeMayChange)
        {
            _overlayImage.SetNativeSize();
            ClampPortraitSizeDelta();
        }
        _lastSpriteForNativeSize = sp;
    }

    private static void ClampPortraitSizeDelta()
    {
        if (_overlayImage == null)
            return;
        float maxW = Plugin.portraitModMaxNativeWidth.Value;
        if (maxW <= 0f)
            return;
        RectTransform rt = _overlayImage.rectTransform;
        Vector2 sd = rt.sizeDelta;
        float w = sd.x;
        float h = sd.y;
        if (w <= maxW || w < 0.01f)
            return;
        float r = maxW / w;
        rt.sizeDelta = new Vector2(maxW, h * r);
    }
}
