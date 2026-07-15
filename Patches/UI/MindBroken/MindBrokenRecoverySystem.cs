using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.EventCore.Core;

namespace NoREroMod.Patches.UI.MindBroken;

/// <summary>
/// MindBroken recovery system via enemy kills
/// -1% for a normal enemy, -5% for a boss
/// Shows captions every 5% of decrease
/// </summary>
internal static class MindBrokenRecoverySystem
{
    internal static bool IsEnabled => Plugin.enableMindBrokenRecovery?.Value ?? false;
    
    internal const string OverlayCanvasObjectName = "RecoveryCaptionsCanvas_XUAIGNORE";
    private const string CaptionObjectName = "RecoveryCaption_XUAIGNORE";
    
    private static RectTransform? overlayCanvasRect;
    private static UnityEngine.UI.Text? currentCaption; // Restored: UnityEngine.UI.Text with Arial, as in the old version
    private static RectTransform? currentCaptionRect;
    private static Coroutine? currentCaptionCoroutine;
    
    // Pink pulsing frame along screen edges (single full-screen layer)
    private static GameObject? borderFrameContainer;
    private static Image? borderFrameImage;
    private static Coroutine? borderPulseCoroutine;
    
    private static RecoveryCaptionsData? _data;
    private static float _lastShownPercent = -1f;
    private static float _lastCaptionTime = 0f;
    private static float CaptionCooldown => Plugin.recoveryCaptionCooldown?.Value ?? 1.5f;
    private static HashSet<string> _recentlyShownCaptions = new HashSet<string>(); // Tracking shown phrases
    private static int _lastStepShown = -1; // Tracks last shown step (every 10%); recovery uses reverse order
    
    /// <summary>Optional extra boss type keys from cfg (lowercase EnemyDate class names).</summary>
    private static HashSet<string> _extraBossNamesFromConfig = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    
    private class RecoveryCaptionsData
    {
        public List<string> recovery = new();
        public Dictionary<string, string> milestones = new();
        public List<string> partial = new();
        public Dictionary<string, List<string>> buckets = new(); // phrases per step 10% (90-99, 80-89, ..., 0-9)
    }
    
    internal static void Initialize()
    {
        if (!IsEnabled) return;
        
        try
        {
            LoadBossNames();
            LoadData();
            SubscribeToEvents();
            EnsureOverlayCanvas();
        }
        catch (Exception)
        {
        }
    }
    
    private static void LoadBossNames()
    {
        try
        {
            string bossNamesStr = Plugin.recoveryBossNames?.Value ?? string.Empty;
            string[] names = bossNamesStr.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

            _extraBossNamesFromConfig.Clear();
            foreach (string name in names)
            {
                string trimmed = name.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    _extraBossNamesFromConfig.Add(trimmed.ToLowerInvariant());
            }
        }
        catch (Exception)
        {
        }
    }
    
    private static void LoadData()
    {
        // Clear old data before load
        _data = null;
        _lastShownPercent = -1f;
        
        try
        {
            string dataPath = GetDataPath();
            
            string jsonPath = Path.Combine(dataPath, "RecoveryCaptionsData.json");
            
            if (!File.Exists(jsonPath))
            {
                return;
            }
            
            string json = File.ReadAllText(jsonPath);
            _data = ParseJsonManually(json);
            
        }
        catch (Exception)
        {
            _data = null; // Ensure data cleared on error
        }
    }
    
    /// <summary>
    /// Reload data on new language selection
    /// Called after language selection on splash screen
    /// </summary>
    internal static void Reload()
    {
        if (!IsEnabled) return;
        
        try
        {
            LoadData();
            // Canvas and subscriptions are not recreated; only data is refreshed
        }
        catch (Exception)
        {
        }
    }
    
    private static string GetDataPath()
    {
        // Main path: BepInEx/plugins/HellGateJson/
        try
        {
            string basePath = Path.Combine(Application.dataPath, "..");
            string bepInEx = Path.Combine(basePath, "BepInEx");
            string plugins = Path.Combine(bepInEx, "plugins");
            string hellGateJson = Path.Combine(plugins, "HellGateJson");
            
            if (Directory.Exists(hellGateJson))
            {
                // Get language from config, fallback on "EN" if not set
                string languageCode = Plugin.hellGateLanguage?.Value ?? "EN";
                if (string.IsNullOrEmpty(languageCode))
                {
                    languageCode = "EN"; // Fallback
                }
                
                // Add language folder
                string langPath = Path.Combine(hellGateJson, languageCode);
                
                // Check existence, if not - fallback on EN
                if (Directory.Exists(langPath))
                {
                    return langPath;
                }
                
                // Fallback to EN if selected language not found
                string enPath = Path.Combine(hellGateJson, "EN");
                if (Directory.Exists(enPath))
                {
                    return enPath;
                }
                
                // If even EN is missing, return root folder (for backward compatibility)
                return hellGateJson;
            }
        }
        catch { }
        
        // Fallback: from project
        try
        {
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string fallbackPath = Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(projectPath, "REZERVNIE COPY"), "NoRHellGate3"), "Patches"), "UI"), "MindBroken");
            fallbackPath = Path.Combine(fallbackPath, "Data");
            
            if (Directory.Exists(fallbackPath))
            {
                return fallbackPath;
            }
        }
        catch { }
        
        // Last fallback
        string basePathFallback = Path.Combine(Application.dataPath, "..");
        string bepInExFallback = Path.Combine(basePathFallback, "BepInEx");
        string pluginsFallback = Path.Combine(bepInExFallback, "plugins");
        string hellGateJsonFallback = Path.Combine(pluginsFallback, "HellGateJson");
        
        // Try to add language folder and in fallback
        try
        {
            string languageCode = Plugin.hellGateLanguage?.Value ?? "EN";
            if (string.IsNullOrEmpty(languageCode))
            {
                languageCode = "EN";
            }
            string langPathFallback = Path.Combine(hellGateJsonFallback, languageCode);
            if (Directory.Exists(langPathFallback))
            {
                return langPathFallback;
            }
            string enPathFallback = Path.Combine(hellGateJsonFallback, "EN");
            if (Directory.Exists(enPathFallback))
            {
                return enPathFallback;
            }
        }
        catch { }
        
        return hellGateJsonFallback;
    }
    
    private static void SubscribeToEvents()
    {
        MindBrokenSystem.OnPercentChanged += OnPercentChanged;
    }
    
    private static void OnPercentChanged(float oldPercent, float newPercent)
    {
        if (!IsEnabled || _data == null) return;
        
        // Show only on decrease
        if (newPercent < oldPercent)
        {
            // Check crossing threshold 10%
            float oldStep = Mathf.Floor(oldPercent * 10f) / 10f; // Round to 10% step
            float newStep = Mathf.Floor(newPercent * 10f) / 10f;
            
            if (newStep < oldStep)
            {
                // Check cooldown
                if (Time.time - _lastCaptionTime < CaptionCooldown)
                {
                    return;
                }
                
                int newStepInt = Mathf.FloorToInt(newPercent * 10f);
                // Clear the list of shown phrases when moving to a new step
                if (newStepInt != _lastStepShown)
                {
                    _recentlyShownCaptions.Clear();
                    _lastStepShown = newStepInt;
                }
                
                // Check milestone
                string milestoneKey = GetMilestoneKey(newPercent);
                if (!string.IsNullOrEmpty(milestoneKey) && _data.milestones.TryGetValue(milestoneKey, out string? milestoneCaption))
                {
                    ShowCaption(milestoneCaption, true);
                }
                else
                {
                    ShowRandomCaption(newPercent);
                }
            }
        }
    }
    
    private static string GetMilestoneKey(float percent)
    {
        if (Mathf.Approximately(percent, 0.9f) || (percent >= 0.9f && percent < 0.95f)) return "90";
        if (Mathf.Approximately(percent, 0.75f) || (percent >= 0.75f && percent < 0.8f)) return "75";
        if (Mathf.Approximately(percent, 0.5f) || (percent >= 0.5f && percent < 0.55f)) return "50";
        if (Mathf.Approximately(percent, 0.25f) || (percent >= 0.25f && percent < 0.3f)) return "25";
        if (Mathf.Approximately(percent, 0f)) return "0";
        return string.Empty;
    }
    
    private static void ShowRandomCaption(float percent)
    {
        if (_data == null) return;
        
        List<string> availablePhrases = new List<string>();
        
        // Determine bucket by 10% steps
        // For recovery: percent 0.95 (95%) -> bucket 9 -> "90-99"
        // percent 0.85 (85%) -> bucket 8 -> "80-89"
        // percent 0.05 (5%) -> bucket 0 -> "0-9"
        int bucket = Mathf.Clamp(Mathf.FloorToInt(percent * 10f), 0, 10); // 0..10 (0-9%, 10-19%, ..., 90-99%, 100%)
        string bucketKey;
        
        if (bucket >= 10) // 100%
        {
            bucketKey = "90-99"; // Use the highest bucket for 100%
        }
        else
        {
            // Build key range: "0-9", "10-19", ..., "90-99"
            int rangeStart = bucket * 10;
            int rangeEnd = rangeStart + 9;
            bucketKey = $"{rangeStart}-{rangeEnd}";
        }
        
        // Try the bucket first
        if (_data.buckets != null && _data.buckets.TryGetValue(bucketKey, out var bucketPhrases) && bucketPhrases != null && bucketPhrases.Count > 0)
        {
            availablePhrases.AddRange(bucketPhrases);
        }
        
        // Fallback to the legacy recovery list if the bucket is empty
        if (availablePhrases.Count == 0 && _data.recovery != null && _data.recovery.Count > 0)
        {
            availablePhrases.AddRange(_data.recovery);
        }
        
        if (availablePhrases.Count == 0) return;
        
        // Filter phrases, excluding recently shown ones
        List<string> unshownPhrases = availablePhrases.Where(phrase => !_recentlyShownCaptions.Contains(phrase)).ToList();
        
        // If all phrases were already shown, clear the list and start over
        if (unshownPhrases.Count == 0)
        {
            _recentlyShownCaptions.Clear();
            unshownPhrases = availablePhrases.ToList();
        }
        
        // Pick a random phrase from those not yet shown
        string caption = unshownPhrases[UnityEngine.Random.Range(0, unshownPhrases.Count)];
        
        // Add to the list of shown phrases
        _recentlyShownCaptions.Add(caption);
        
        ShowCaption(caption, false);
    }
    
    private static void ShowCaption(string text, bool isMilestone)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (EventCoreRuntime.IsSessionOpen)
            return;

        _lastCaptionTime = Time.time;
        _lastShownPercent = MindBrokenSystem.Percent;
        
        EnsureOverlayCanvas();
        EnsureCaption();
        
        if (currentCaption != null)
        {
            // Stop the previous coroutine if it exists
            if (currentCaptionCoroutine != null && overlayCanvasRect != null)
            {
                var mono = overlayCanvasRect.GetComponent<MonoBehaviour>();
                if (mono != null)
                {
                    mono.StopCoroutine(currentCaptionCoroutine);
                }
            }
            
            currentCaption.text = text;
            currentCaption.color = new Color(0.2f, 1f, 0.2f, 1f); // Bright green for Recovery
            
            // Start display coroutine
            if (overlayCanvasRect != null)
            {
                var mono = overlayCanvasRect.GetComponent<MonoBehaviour>();
                if (mono == null)
                {
                    mono = overlayCanvasRect.gameObject.AddComponent<RecoveryCaptionsMono>();
                }
                currentCaptionCoroutine = mono.StartCoroutine(ShowCaptionCoroutine(8f));
            }
            
        }
    }
    
    private static IEnumerator ShowCaptionCoroutine(float duration)
    {
        if (currentCaption == null || currentCaptionRect == null) yield break;
        
        // Initial state: compressed (scale 0 on X)
        currentCaptionRect.localScale = new Vector3(0f, 1f, 1f);
        Vector2 originalPosition = currentCaptionRect.anchoredPosition;
        
        currentCaption.gameObject.SetActive(true);
        
        // Activate and start frame pulse
        EnsureBorderFrame();
        if (borderFrameContainer != null)
        {
            borderFrameContainer.SetActive(true);
            if (overlayCanvasRect != null)
            {
                var mono = overlayCanvasRect.GetComponent<MonoBehaviour>();
                if (mono == null)
                {
                    mono = overlayCanvasRect.gameObject.AddComponent<RecoveryCaptionsMono>();
                }
                if (borderPulseCoroutine != null)
                {
                    mono.StopCoroutine(borderPulseCoroutine);
                }
                borderPulseCoroutine = mono.StartCoroutine(BorderPulseCoroutine(duration));
            }
        }
        
        // Phase 1: sharp expand from compressed state (0.15 seconds)
        float expandDuration = 0.15f;
        float expandElapsed = 0f;
        
        while (expandElapsed < expandDuration)
        {
            expandElapsed += Time.deltaTime;
            float t = expandElapsed / expandDuration;
            // Sharp expand with ease-out
            t = 1f - Mathf.Pow(1f - t, 3f);
            float scaleX = Mathf.Lerp(0f, 1f, t);
            currentCaptionRect.localScale = new Vector3(scaleX, 1f, 1f);
            
            yield return null;
        }
        
        // Set full size after expand
        currentCaptionRect.localScale = Vector3.one;
        currentCaptionRect.anchoredPosition = originalPosition;
        
        // Phase 2: pulse for remaining time (larger and smoother)
        float scaleSpeed = 0.5f; // Slower speed (0.5 cycles per second — smoother)
        float scaleAmount = 0.08f; // Increased pulse amplitude (8% — more noticeable)
        float scaleElapsed = expandDuration;
        
        float elapsed = expandDuration;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            scaleElapsed += Time.deltaTime;
            
            // Loop scaler: scale from 1.0 to 1.0 + scaleAmount and back
            float scale = 1f + scaleAmount * Mathf.Sin(scaleElapsed * scaleSpeed * Mathf.PI * 2f);
            currentCaptionRect.localScale = new Vector3(scale, scale, 1f);
            
            yield return null;
        }
        
        // Restore scale to the initial state
        currentCaptionRect.localScale = Vector3.one;
        currentCaption.gameObject.SetActive(false);
        
        // Hide the frame
        if (borderFrameContainer != null)
        {
            borderFrameContainer.SetActive(false);
        }
        borderPulseCoroutine = null;
        currentCaptionCoroutine = null;
    }
    
    private static void EnsureOverlayCanvas()
    {
        if (overlayCanvasRect != null) return;
        
        try
        {
            GameObject existing = GameObject.Find(OverlayCanvasObjectName);
            if (existing != null)
            {
                overlayCanvasRect = existing.GetComponent<RectTransform>();
                return;
            }
            
            GameObject canvasGo = new GameObject(OverlayCanvasObjectName);
            overlayCanvasRect = canvasGo.AddComponent<RectTransform>();
            
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32767; // Max priority — always in the foreground
            canvas.pixelPerfect = false; // Disable pixel perfect for better readability
            
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
            
            // Add MonoBehaviour for coroutines
            canvasGo.AddComponent<RecoveryCaptionsMono>();

            if (EventCoreRuntime.IsSessionOpen)
                canvasGo.SetActive(false);
        }
        catch (Exception ex)
        {
        }
    }
    
    private static void EnsureCaption()
    {
        if (overlayCanvasRect == null) return;
        
        if (currentCaption != null && currentCaption.gameObject != null)
        {
            return;
        }
        
        try
        {
            GameObject textGo = new GameObject(CaptionObjectName);
            textGo.transform.SetParent(overlayCanvasRect, false);
            
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            // Centered on X; 130px from bottom (same offset as before from top)
            textRect.anchorMin = new Vector2(0.5f, 0f); // Center on X, bottom edge on Y
            textRect.anchorMax = new Vector2(0.5f, 0f);
            textRect.pivot = new Vector2(0.5f, 0f); // Pivot at bottom center
            textRect.anchoredPosition = new Vector2(0f, 200f); // Center on X, 200px from bottom (above Corruption)
            textRect.sizeDelta = new Vector2(1200f, 60f); // Width for wrapping; height for 2 lines: 60px
            
            // Restored: UnityEngine.UI.Text with Arial, as in the old version and Corruption
            UnityEngine.UI.Text label = textGo.AddComponent<UnityEngine.UI.Text>();
            label.fontSize = 52; // Font size 52 (30% larger than 40)
            label.alignment = TextAnchor.MiddleCenter; // Center alignment
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(0.2f, 1f, 0.2f, 1f); // Bright green for Recovery
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; // Wrap to 2 lines
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = false;
            label.font = NoREroMod.Systems.UI.HellGateFontProvider.GetUiFont(); // Same as the old version and Corruption
            
            // Add black outline
            UnityEngine.UI.Outline outline = textGo.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
            
            textGo.layer = LayerMask.NameToLayer("UI");
            textGo.SetActive(false);
            
            currentCaption = label;
            currentCaptionRect = textRect;
            
        }
        catch (Exception ex)
        {
        }
    }
    
    private static void EnsureBorderFrame()
    {
        if (overlayCanvasRect == null) return;
        
        if (borderFrameContainer != null && borderFrameContainer.activeSelf) return;
        
        try
        {
            // Create container for the frame
            if (borderFrameContainer == null)
            {
                borderFrameContainer = new GameObject("RecoveryBorderFrame_XUAIGNORE");
                borderFrameContainer.transform.SetParent(overlayCanvasRect, false);
                
                RectTransform containerRect = borderFrameContainer.AddComponent<RectTransform>();
                containerRect.anchorMin = Vector2.zero;
                containerRect.anchorMax = Vector2.one;
                containerRect.pivot = new Vector2(0.5f, 0.5f);
                containerRect.offsetMin = Vector2.zero;
                containerRect.offsetMax = Vector2.zero;
                containerRect.localScale = Vector3.one;
                
                borderFrameContainer.layer = LayerMask.NameToLayer("UI");
            }
            
            // Create a single full-screen Image with a center-to-edge gradient
            if (borderFrameImage == null)
            {
                GameObject frameGo = new GameObject("BorderFrameImage_XUAIGNORE");
                frameGo.transform.SetParent(borderFrameContainer.transform, false);
                
                RectTransform frameRect = frameGo.AddComponent<RectTransform>();
                frameRect.anchorMin = Vector2.zero;
                frameRect.anchorMax = Vector2.one;
                frameRect.pivot = new Vector2(0.5f, 0.5f);
                frameRect.offsetMin = Vector2.zero;
                frameRect.offsetMax = Vector2.zero;
                
                borderFrameImage = frameGo.AddComponent<Image>();
                borderFrameImage.color = new Color(0.4f, 0.7f, 0.5f, 0f); // Green color for Recovery
                borderFrameImage.raycastTarget = false;
                
                // Create a texture with a center-to-edge gradient
                borderFrameImage.sprite = CreateRadialGradientSprite(1920, 1080);
                
                frameGo.layer = LayerMask.NameToLayer("UI");
            }
            
            borderFrameContainer.SetActive(false);
        }
        catch (Exception ex)
        {
        }
    }
    
    // Horizontal strips at top and bottom (instead of a radial edge gradient)
    private static Sprite CreateRadialGradientSprite(int width, int height)
    {
        int textureWidth = Mathf.Max(width, 64);
        int textureHeight = Mathf.Max(height, 64);
        
        Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        
        Color[] pixels = new Color[textureWidth * textureHeight];
        float barHeight = Mathf.Max(4f, textureHeight * 0.18f);
        float alphaMax = 0.5f;
        
        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                float alpha = 0f;
                if (y < barHeight)
                    alpha = (1f - y / barHeight) * alphaMax;
                else if (y >= textureHeight - barHeight)
                    alpha = ((y - (textureHeight - barHeight)) / barHeight) * alphaMax;
                
                pixels[y * textureWidth + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, textureWidth, textureHeight), new Vector2(0.5f, 0.5f), 100f);
    }
    
    private static IEnumerator BorderPulseCoroutine(float duration)
    {
        if (borderFrameImage == null) yield break;
        
        float pulseSpeed = 0.75f;
        float pulseAmount = 0.04f;
        float baseAlpha = 0.25f;
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // Pulse: sine from 0 to 1, multiply by pulseAmount and add baseAlpha
            float pulse = baseAlpha + pulseAmount * (0.5f + 0.5f * Mathf.Sin(elapsed * pulseSpeed * Mathf.PI * 2f));
            
            // Apply pulse to the single layer
            Color pulseColor = new Color(0.4f, 0.7f, 0.5f, pulse); // Green color for Recovery
            borderFrameImage.color = pulseColor;
            
            yield return null;
        }
        
        // Restore transparency to 0
        Color transparent = new Color(0.4f, 0.7f, 0.5f, 0f); // Green color for Recovery
        borderFrameImage.color = transparent;
    }
    
    /// <summary>Story boss check (vanilla BOSSflag + FactionBossDetection, optional cfg extras).</summary>
    internal static bool IsBossEnemy(EnemyDate enemy)
    {
        if (enemy == null)
            return false;
        if (FactionBossDetection.IsBossEnemy(enemy))
            return true;
        string key = enemy.GetType().Name.ToLowerInvariant();
        return _extraBossNamesFromConfig.Contains(key);
    }

    /// <summary>
    /// Registers a kill and restores MindBroken. Prefer this overload — uses faction boss detection.
    /// </summary>
    internal static void RegisterKill(EnemyDate enemy)
    {
        if (!IsEnabled || enemy == null)
            return;

        string enemyName = enemy.GetType().Name.ToLowerInvariant();
        RegisterKillInternal(enemyName, IsBossEnemy(enemy));
    }

    /// <summary>Legacy string-only kill registration (treated as non-boss unless cfg extra name matches).</summary>
    internal static void RegisterKill(string enemyName)
    {
        if (!IsEnabled)
            return;

        string key = string.IsNullOrEmpty(enemyName) ? "unknown" : enemyName.ToLowerInvariant();
        bool isBoss = _extraBossNamesFromConfig.Contains(key);
        RegisterKillInternal(key, isBoss);
    }

    private static void RegisterKillInternal(string enemyName, bool isBoss)
    {
        float recoveryAmount = isBoss
            ? Plugin.recoveryPercentPerBossKill?.Value ?? 0.05f
            : Plugin.recoveryPercentPerKill?.Value ?? 0.01f;

        if (recoveryAmount > 0f)
            MindBrokenSystem.AddPercent(-recoveryAmount, isBoss ? $"boss_kill_{enemyName}" : $"kill_{enemyName}");
    }
    
    private static RecoveryCaptionsData ParseJsonManually(string json)
    {
        var data = new RecoveryCaptionsData();
        
        try
        {
            // Parse buckets (10% ranges: "90-99", "80-89", ..., "0-9")
            Match bucketsMatch = Regex.Match(json, "\"buckets\"\\s*:\\s*\\{([^}]+(?:\\{[^}]*\\}[^}]*)*)\\}", RegexOptions.Singleline);
            if (bucketsMatch.Success)
            {
                string bucketsSection = bucketsMatch.Groups[1].Value;
                
                // Parse each bucket (key may be "90-99", "80-89", ..., "0-9")
                MatchCollection bucketMatches = Regex.Matches(bucketsSection, "\"([0-9]+-[0-9]+)\"\\s*:\\s*\\[([^\\]]+)\\]", RegexOptions.Singleline);
                foreach (Match bucketMatch in bucketMatches)
                {
                    string bucketKey = bucketMatch.Groups[1].Value;
                    string phrasesStr = bucketMatch.Groups[2].Value;
                    var phrases = new List<string>();
                    
                    // Parse phrases
                    MatchCollection phraseMatches = Regex.Matches(phrasesStr, "\"([^\"]+)\"");
                    foreach (Match phraseMatch in phraseMatches)
                    {
                        phrases.Add(phraseMatch.Groups[1].Value);
                    }
                    
                    if (phrases.Count > 0)
                    {
                        data.buckets[bucketKey] = phrases;
                    }
                }
            }
            
            // Parse recovery phrases (for backward compatibility)
            Match recoveryMatch = Regex.Match(json, "\"recovery\"\\s*:\\s*\\[([^\\]]+)\\]", RegexOptions.Singleline);
            if (recoveryMatch.Success)
            {
                string recoveryStr = recoveryMatch.Groups[1].Value;
                MatchCollection phraseMatches = Regex.Matches(recoveryStr, "\"([^\"]+)\"");
                foreach (Match phraseMatch in phraseMatches)
                {
                    data.recovery.Add(phraseMatch.Groups[1].Value);
                }
            }
            
            // Parse milestones
            Match milestonesMatch = Regex.Match(json, "\"milestones\"\\s*:\\s*\\{([^}]+)\\}", RegexOptions.Singleline);
            if (milestonesMatch.Success)
            {
                string milestonesSection = milestonesMatch.Groups[1].Value;
                MatchCollection milestoneMatches = Regex.Matches(milestonesSection, "\"([^\"]+)\"\\s*:\\s*\"([^\"]+)\"");
                foreach (Match milestoneMatch in milestoneMatches)
                {
                    string key = milestoneMatch.Groups[1].Value;
                    string value = milestoneMatch.Groups[2].Value;
                    data.milestones[key] = value;
                }
            }
            
            // Parse partial phrases (optional)
            Match partialMatch = Regex.Match(json, "\"partial\"\\s*:\\s*\\[([^\\]]+)\\]", RegexOptions.Singleline);
            if (partialMatch.Success)
            {
                string partialStr = partialMatch.Groups[1].Value;
                MatchCollection phraseMatches = Regex.Matches(partialStr, "\"([^\"]+)\"");
                foreach (Match phraseMatch in phraseMatches)
                {
                    data.partial.Add(phraseMatch.Groups[1].Value);
                }
            }
        }
        catch (Exception ex)
        {
        }
        
        return data;
    }
    
    internal static void Cleanup()
    {
        if (currentCaptionCoroutine != null && overlayCanvasRect != null)
        {
            var mono = overlayCanvasRect.GetComponent<MonoBehaviour>();
            if (mono != null)
            {
                mono.StopCoroutine(currentCaptionCoroutine);
            }
        }
        
        MindBrokenSystem.OnPercentChanged -= OnPercentChanged;
    }
}

/// <summary>
/// MonoBehaviour host for coroutines
/// </summary>
internal class RecoveryCaptionsMono : MonoBehaviour
{
    // Empty class used as a coroutine host
}

