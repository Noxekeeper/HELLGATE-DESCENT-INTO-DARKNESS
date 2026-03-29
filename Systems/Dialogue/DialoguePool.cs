using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NoREroMod;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// Pool of text objects for reuse
/// </summary>
internal class DialoguePool
{
    private readonly Queue<GameObject> _availableObjects = new();
    private readonly List<GameObject> _allObjects = new();
    private GameObject _canvasRoot;
    private Font _cachedFont;
    private const int PoolSize = 10;

    private bool _initialized = false;

    /// <summary>
    /// Initialize pool
    /// </summary>
    internal void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        EnsureCanvas();
        CacheFont();
        CreatePoolObjects();
        _initialized = true;
    }

    /// <summary>
    /// Get text object from pool
    /// </summary>
    internal GameObject GetTextObject()
    {
        if (_availableObjects.Count > 0)
        {
            GameObject obj = _availableObjects.Dequeue();
            obj.SetActive(true);
            return obj;
        }

        // If pool is empty, create new object
        return CreateNewTextObject();
    }

    /// <summary>
    /// Return object to pool
    /// </summary>
    internal void ReturnTextObject(GameObject obj)
    {
        if (obj == null)
        {
            return;
        }

        UnityEngine.UI.Text text = obj.GetComponent<UnityEngine.UI.Text>();
        if (text != null && _cachedFont != null)
        {
            text.font = _cachedFont;
            text.fontStyle = FontStyle.Normal;
        }

        obj.SetActive(false);
        _availableObjects.Enqueue(obj);
    }

    /// <summary>
    /// Clear pool
    /// </summary>
    internal void ClearPool()
    {
        foreach (var obj in _allObjects)
        {
            if (obj != null)
            {
                Object.Destroy(obj);
            }
        }
        _allObjects.Clear();
        _availableObjects.Clear();

        if (_canvasRoot != null)
        {
            Object.Destroy(_canvasRoot);
            _canvasRoot = null;
        }

        _cachedFont = null;
    }

    private void CacheFont()
    {
        _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        
        if (_cachedFont == null)
        {
            _cachedFont = Resources.Load<Font>("Arial");
        }

        if (_cachedFont == null)
        {
            _cachedFont = Resources.Load<Font>("Fonts/Arial");
        }

        if (_cachedFont == null)
        {
            var fonts = Resources.LoadAll<Font>("");
            if (fonts.Length > 0)
            {
                _cachedFont = fonts[0];
            }
        }
    }

    /// <summary>
    /// Create Canvas for dialogues
    /// </summary>
    private void EnsureCanvas()
    {
        if (_canvasRoot != null)
        {
            return;
        }

        _canvasRoot = new GameObject("DialogueSystemCanvas_XUAIGNORE");
        Canvas canvas = _canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;
        
        CanvasGroup canvasGroup = _canvasRoot.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = _canvasRoot.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        CanvasScaler scaler = _canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        _canvasRoot.AddComponent<UnityEngine.UI.GraphicRaycaster>().enabled = false;
        Object.DontDestroyOnLoad(_canvasRoot);
    }

    /// <summary>
    /// Create pool objects
    /// </summary>
    private void CreatePoolObjects()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            GameObject obj = CreateNewTextObject();
            obj.SetActive(false);
            _availableObjects.Enqueue(obj);
        }
    }

    /// <summary>
    /// Create new text object
    /// </summary>
    private GameObject CreateNewTextObject()
    {
        GameObject obj = new GameObject($"DialogueText_{_allObjects.Count}_XUAIGNORE");
        obj.transform.SetParent(_canvasRoot.transform, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        // Increased size to support long phrases (QTE reactions)
        rect.sizeDelta = new Vector2(800, 100);

        UnityEngine.UI.Text text = obj.AddComponent<UnityEngine.UI.Text>();
        
        if (_cachedFont != null)
        {
            text.font = _cachedFont;
        }

        text.fontSize = 18;
        text.fontStyle = FontStyle.Normal;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.color = Color.white;
        
        // IMPORTANT: Allow text overflow so long phrases don't get cut off
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = false;

        _allObjects.Add(obj);
        return obj;
    }

    internal GameObject CanvasRoot => _canvasRoot;
    
    internal Font GetCachedFont()
    {
        return _cachedFont;
    }
}

