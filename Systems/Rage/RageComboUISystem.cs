using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace NoREroMod.Systems.Rage;

/// <summary>
/// Rage combo UI display. Position: 150px above center.
/// </summary>
internal static class RageComboUISystem
{
    private static GameObject comboIndicator;
    private static Text comboText;
    private static RectTransform overlayCanvasRect;
    private static bool isInitialized = false;
    
    private const string CanvasObjectName = "RageComboCanvas";
    private const string IndicatorObjectName = "RageComboIndicator";
    
    /// <summary>
    /// Initializes combo UI and subscribes to combo events.
    /// </summary>
    internal static void Initialize()
    {
        if (!RageSystem.Enabled) return;
        if (isInitialized) return;
        
        EnsureOverlayCanvas();
        InitializeComboIndicator();
        
        RageComboSystem.OnComboChanged += OnComboChanged;
        RageComboSystem.OnComboReset += OnComboReset;
        
        RageComboBloodEffect.Initialize();
        
        isInitialized = true;
    }
    
    /// <summary>
    /// Handles combo count changes.
    /// </summary>
    private static void OnComboChanged(int comboCount)
    {
        UpdateComboDisplay();
    }
    
    /// <summary>
    /// Handles combo reset events.
    /// </summary>
    private static void OnComboReset()
    {
        UpdateComboDisplay();
    }
    
    /// <summary>
    /// Updates combo text, color, and animation state.
    /// </summary>
    internal static void UpdateComboDisplay()
    {
        if (!RageSystem.Enabled)
        {
            if (comboIndicator != null)
            {
                comboIndicator.SetActive(false);
            }
            return;
        }
        
        if (comboIndicator == null || comboText == null)
        {
            Initialize();
        }
        
        if (comboIndicator == null || comboText == null) return;
        
        int comboCount = RageComboSystem.ComboCount;
        
        if (comboCount >= 3)
        {
            comboIndicator.SetActive(true);
            comboText.text = $"x{comboCount}";
            
            int fontSize = 48;
            Color comboColor = Color.white;
            
            if (comboCount >= 20)
            {
                fontSize = 72;
                comboColor = new Color(1f, 0.2f, 0.2f);
            }
            else if (comboCount >= 10)
            {
                fontSize = 64;
                comboColor = new Color(1f, 0.8f, 0f);
            }
            else if (comboCount >= 5)
            {
                fontSize = 56;
                comboColor = new Color(1f, 0.5f, 0f);
            }
            else
            {
                fontSize = 52;
                comboColor = new Color(0.5f, 1f, 0.5f);
            }
            
            comboText.fontSize = fontSize;
            comboText.color = comboColor;
            
            var updater = GetOrCreateUpdater();
            if (updater != null)
            {
                updater.StartCoroutine(ComboIndicatorScaleAnimation(comboIndicator));
            }
        }
        else
        {
            comboIndicator.SetActive(false);
        }
    }
    
    /// <summary>
    /// Ensures overlay canvas exists and is configured.
    /// </summary>
    private static void EnsureOverlayCanvas()
    {
        if (overlayCanvasRect != null) return;
        
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
        canvas.sortingOrder = 998;
        
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
    
    /// <summary>
    /// Creates the combo indicator UI object.
    /// </summary>
    private static void InitializeComboIndicator()
    {
        if (overlayCanvasRect == null || comboIndicator != null) return;
        
        try
        {
            comboIndicator = new GameObject(IndicatorObjectName);
            comboIndicator.transform.SetParent(overlayCanvasRect, false);
            
            RectTransform comboRect = comboIndicator.AddComponent<RectTransform>();
            comboRect.anchorMin = new Vector2(0.5f, 0.5f);
            comboRect.anchorMax = new Vector2(0.5f, 0.5f);
            comboRect.pivot = new Vector2(0.5f, 0.5f);
            comboRect.anchoredPosition = new Vector2(0f, 150f);
            comboRect.sizeDelta = new Vector2(200f, 80f);
            
            comboText = comboIndicator.AddComponent<Text>();
            comboText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            comboText.fontSize = 48;
            comboText.fontStyle = FontStyle.Bold;
            comboText.alignment = TextAnchor.MiddleCenter;
            comboText.color = Color.white;
            
            Outline outline = comboIndicator.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
            
            comboIndicator.SetActive(false);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Rage Combo UI] Failed to initialize combo indicator: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Plays a short pop animation when combo value updates.
    /// </summary>
    private static IEnumerator ComboIndicatorScaleAnimation(GameObject indicator)
    {
        if (indicator == null) yield break;
        
        RectTransform rect = indicator.GetComponent<RectTransform>();
        if (rect == null) yield break;
        
        Vector3 originalScale = Vector3.one;
        float duration = 0.2f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            
            float scale = Mathf.Lerp(1.2f, 1.0f, t);
            rect.localScale = originalScale * scale;
            
            yield return null;
        }
        
        rect.localScale = originalScale;
    }
    
    /// <summary>
    /// Returns updater component used for throttled UI updates.
    /// </summary>
    private static MonoBehaviour GetOrCreateUpdater()
    {
        if (overlayCanvasRect == null) return null;
        
        var updater = overlayCanvasRect.GetComponent<RageComboUIUpdater>();
        if (updater == null)
        {
            updater = overlayCanvasRect.gameObject.AddComponent<RageComboUIUpdater>();
        }
        
        return updater;
    }
    
    /// <summary>
    /// Resets combo UI state.
    /// </summary>
    internal static void Reset()
    {
        if (comboIndicator != null)
        {
            comboIndicator.SetActive(false);
        }
        RageComboBloodEffect.Reset();
    }
}

/// <summary>
/// Throttled: update every 4 frames when Rage enabled (reduces per-frame overhead).
/// </summary>
internal class RageComboUIUpdater : MonoBehaviour
{
    private static int _frameCounter;

    private void Update()
    {
        if (!RageSystem.Enabled) return;

        _frameCounter++;
        if (_frameCounter >= 4)
        {
            _frameCounter = 0;
            RageComboUISystem.UpdateComboDisplay();
        }
    }
}

/// <summary>
/// Harmony bootstrap patches for initializing combo UI with game UI lifecycle.
/// </summary>
[HarmonyPatch]
internal static class RageComboUISystemPatches
{
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
                    return;
                }

                __instance.StartCoroutine(DelayedUISetup());
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[Rage Combo UI] UImngStartPatch error: {ex.Message}");
            }
        }

        private static IEnumerator DelayedUISetup()
        {
            yield return new WaitForSeconds(0.5f);

            try
            {
                RageComboUISystem.Initialize();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[Rage Combo UI] DelayedUISetup error: {ex.Message}");
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
                if (!RageSystem.Enabled)
                {
                    return;
                }

                RageComboUISystem.Initialize();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[Rage Combo UI] CanvasBadstatusStartPatch error: {ex.Message}");
            }
        }
    }
}
