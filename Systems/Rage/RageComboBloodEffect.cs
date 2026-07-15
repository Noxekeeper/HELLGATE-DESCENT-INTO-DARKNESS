using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace NoREroMod.Systems.Rage;

/// <summary>
/// Blood sprites (Blood1, Blood2, Blood3, skull) at combo thresholds: x10, x20, x30, x40+.
/// </summary>
internal static class RageComboBloodEffect
{
    private const string CanvasObjectName = "RageComboBloodCanvas";
    
    private static GameObject? canvasObject;
    private static RectTransform? canvasRect;
    private static GameObject? bloodParent;
    
    private static Sprite? _blood1Sprite;
    private static Sprite? _blood2Sprite;
    private static Sprite? _blood3Sprite;
    private static Sprite? _skullSprite;
    private static bool _spritesLoaded;
    
    private static Image? _blood1Image;
    private static Image? _blood2Image;
    private static Image? _blood3Image;
    private static Image? _skullImage;
    
    private static MonoBehaviour? _coroutineHost;
    private static bool _initialized;
    
    internal static void Initialize()
    {
        if (!RageSystem.Enabled) return;
        if (_initialized) return;
        
        EnsureCanvas();
        if (_coroutineHost != null)
        {
            _coroutineHost.StartCoroutine(LoadSpritesAndSetup());
        }
        
        RageComboSystem.OnComboChanged += OnComboChanged;
        RageComboSystem.OnComboReset += OnComboReset;
        
        _initialized = true;
    }
    
    private static void OnComboChanged(int comboCount)
    {
        UpdateBloodVisibility(comboCount);
    }
    
    private static void OnComboReset()
    {
        UpdateBloodVisibility(0);
    }
    
    private static void EnsureCanvas()
    {
        if (canvasObject != null) return;
        
        canvasObject = new GameObject(CanvasObjectName);
        UnityEngine.Object.DontDestroyOnLoad(canvasObject);
        
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 997;
        
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        
        canvasObject.AddComponent<GraphicRaycaster>().enabled = false;
        canvasObject.layer = LayerMask.NameToLayer("UI");
        
        canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.pivot = new Vector2(0.5f, 0.5f);
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;
        
        _coroutineHost = canvasObject.AddComponent<RageComboBloodEffectUpdater>();
    }
    
    private static IEnumerator LoadSpritesAndSetup()
    {
        string baseDir = GetBloodSpritesDirectory();
        string[] files = { "Blood1.png", "Blood2.png", "Blood3.png", "skull.png" };
        Sprite?[] sprites = new Sprite?[4];
        
        for (int i = 0; i < files.Length; i++)
        {
            string path = Path.Combine(baseDir, files[i]);
            if (!File.Exists(path)) continue;
            
            var www = new WWW("file://" + path.Replace("\\", "/"));
            yield return www;
            if (!string.IsNullOrEmpty(www.error)) continue;

            var tex = www.texture;
            if (tex != null)
            {
                sprites[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }
        
        _blood1Sprite = sprites[0];
        _blood2Sprite = sprites[1];
        _blood3Sprite = sprites[2];
        _skullSprite = sprites[3];
        _spritesLoaded = true;
        
        CreateBloodImages();
        UpdateBloodVisibility(RageComboSystem.ComboCount);
    }
    
    private static string GetBloodSpritesDirectory()
    {
        string basePath = Application.dataPath;
        if (basePath.EndsWith("_Data")) basePath = basePath.Substring(0, basePath.Length - 5);
        
        string p1 = Path.Combine(Path.Combine(Path.Combine(basePath, "sources"), "HellGate_sources"), "RAGE_FURY");
        string p2 = Path.Combine(Path.Combine(Path.Combine(Path.Combine(basePath, ".."), "sources"), "HellGate_sources"), "RAGE_FURY");
        string p3 = Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(basePath, "BepInEx"), "plugins"), "NoR_HellGate"), "sources"), "HellGate_sources"), "RAGE_FURY");
        string[] paths = { p1, p2, p3 };
        
        foreach (var p in paths)
            if (Directory.Exists(p)) return p;
        return paths[0];
    }
    
    private static void CreateBloodImages()
    {
        if (canvasRect == null || bloodParent != null) return;
        if (!_spritesLoaded) return;
        
        bloodParent = new GameObject("ComboBloodParent");
        bloodParent.transform.SetParent(canvasRect, false);
        
        var parentRect = bloodParent.AddComponent<RectTransform>();
        parentRect.anchorMin = new Vector2(0.5f, 0.5f);
        parentRect.anchorMax = new Vector2(0.5f, 0.5f);
        parentRect.pivot = new Vector2(0.5f, 0.5f);
        parentRect.sizeDelta = new Vector2(800f, 350f);
        parentRect.anchoredPosition = new Vector2(0f, 150f);
        
        if (_blood1Sprite != null) _blood1Image = CreateImage("Blood1", _blood1Sprite, new Vector2(-120f, 0f));
        if (_blood2Sprite != null) _blood2Image = CreateImage("Blood2", _blood2Sprite, new Vector2(120f, 0f));
        if (_blood3Sprite != null) _blood3Image = CreateImage("Blood3", _blood3Sprite, new Vector2(0f, -40f));
        if (_skullSprite != null) _skullImage = CreateImage("Skull", _skullSprite, new Vector2(0f, 40f));
        
        bloodParent.SetActive(false);
    }
    
    private static Image CreateImage(string name, Sprite sprite, Vector2 offset)
    {
        if (bloodParent == null) return null;
        
        var go = new GameObject($"ComboBlood_{name}");
        go.transform.SetParent(bloodParent.transform, false);
        
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(sprite.texture.width, sprite.texture.height);
        rect.anchoredPosition = offset;
        rect.localScale = Vector3.one * 0.85f;
        
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = new Color(1f, 1f, 1f, 0.75f);
        img.raycastTarget = false;
        
        return img;
    }
    
    private static void UpdateBloodVisibility(int comboCount)
    {
        if (bloodParent == null)
        {
            if (_spritesLoaded) CreateBloodImages();
            else return;
        }
        
        if (bloodParent == null) return;
        
        bool show1 = comboCount >= 10;
        bool show2 = comboCount >= 20;
        bool show3 = comboCount >= 30;
        bool showSkull = comboCount >= 40;
        
        if (_blood1Image != null) _blood1Image.gameObject.SetActive(show1);
        if (_blood2Image != null) _blood2Image.gameObject.SetActive(show2);
        if (_blood3Image != null) _blood3Image.gameObject.SetActive(show3);
        if (_skullImage != null) _skullImage.gameObject.SetActive(showSkull);
        
        bloodParent.SetActive(show1 || show2 || show3 || showSkull);
    }
    
    internal static void Reset()
    {
        UpdateBloodVisibility(0);
    }
}

internal class RageComboBloodEffectUpdater : MonoBehaviour { }
