using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using NoREroMod.Systems.EventCore.Core;

namespace NoREroMod.Patches.UI.MindBroken;

/// <summary>
/// Red caption system corruption on MindBroken growth MindBroken
/// Shows captions every 10% and at milestones (50%, 75%, 90%, 100%)
/// At 100% shows captions every 15 seconds
/// </summary>
internal static class CorruptionCaptionsSystem
{
    private static bool IsEnabled => Plugin.enableCorruptionCaptions?.Value ?? false;
    
    internal const string OverlayCanvasObjectName = "CorruptionCaptionsCanvas_XUAIGNORE";
    private const string CaptionObjectName = "CorruptionCaption_XUAIGNORE";
    
    private static RectTransform? overlayCanvasRect;
    private static UnityEngine.UI.Text? currentCaption;
    private static RectTransform? currentCaptionRect;
    private static Coroutine? currentCaptionCoroutine;
    private static CorruptionCaptionsData? _data;
    private static float _lastShownPercent = -1f;
    private static float _lastCaptionTime = 0f;
    private static float CaptionCooldown => Plugin.corruptionCaptionCooldown?.Value ?? 1.5f;
    private static bool _subscribed = false;
    private static bool _initialized = false;
    private static int _lastBucketShown = -1; // steps by 10%: bucket = floor(percent * 10)
    private static HashSet<string> _recentlyShownCaptions = new HashSet<string>(); // Tracking shown phrases
    private static Coroutine? _hundredPercentCoroutine; // Coroutine for showing captions on 100%
    
    private class CorruptionCaptionsData
    {
        public Dictionary<string, List<string>> phases = new();
        public Dictionary<string, string> milestones = new();
        public Dictionary<string, List<string>> buckets = new(); // phrases per step 10% (0-9, 10-19, ..., 90-99, 100)
    }
    
    internal static void Initialize()
    {
        if (!IsEnabled) return;
        if (_initialized) return;
        
        try
        {
            LoadData();
            SubscribeToEvents();
            EnsureOverlayCanvas();
            _initialized = true;
        }
        catch (Exception ex)
        {
        }
    }
    
    private static void LoadData()
    {
        // Clear old data before load
        _data = null;
        _lastShownPercent = -1f;
        _lastBucketShown = -1;
        
        try
        {
            string dataPath = GetDataPath();
            
            string jsonPath = Path.Combine(dataPath, "CorruptionCaptionsData.json");
            
            if (!File.Exists(jsonPath))
            {
                return;
            }
            
            string json = File.ReadAllText(jsonPath);
            _data = ParseJsonManually(json);
            
        }
        catch (Exception ex)
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
        catch (Exception ex)
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
        if (_subscribed) return;
        MindBrokenSystem.OnPercentChanged += OnPercentChanged;
        MindBrokenSystem.OnMilestoneReached += OnMilestoneReached;
        _subscribed = true;
    }
    
    private static void OnPercentChanged(float oldPercent, float newPercent)
    {
        if (!IsEnabled || _data == null) return;
        
        // Special handling for 100%
        if (newPercent >= 1.0f)
        {
            // If reached 100%, start coroutine for periodic display
            if (oldPercent < 1.0f)
            {
                StartHundredPercentLoop();
            }
            return; // Do not show regular captions on 100%
        }
        else
        {
            // If dropped below 100%, stop coroutine
            if (oldPercent >= 1.0f)
            {
                StopHundredPercentLoop();
            }
        }
        
        // Show only on growth
        if (newPercent <= oldPercent) return;
        
        // Check cooldown
        if (Time.time - _lastCaptionTime < CaptionCooldown)
        {
            return;
        }
        
        // Check crossing threshold 10% (more accurate check)
        float oldStep = Mathf.Floor(oldPercent * 10f) / 10f; // Round to 10% step
        float newStep = Mathf.Floor(newPercent * 10f) / 10f;
        
        // If crossed threshold 10%, show caption
        if (newStep > oldStep)
        {
            int newBucket = Mathf.FloorToInt(newPercent * 10f);
            if (newBucket > _lastBucketShown)
            {
                // Clear the list of shown phrases when moving to a new bucket
                _recentlyShownCaptions.Clear();
                _lastBucketShown = newBucket;
                ShowRandomCaption(newPercent);
            }
        }
    }
    
    private static void OnMilestoneReached(float milestone)
    {
        if (!IsEnabled || _data == null) return;
        
        string milestoneKey = milestone.ToString("F2");
        if (_data.milestones.TryGetValue(milestoneKey, out string? caption))
        {
            ShowCaption(caption, true); // Guaranteed caption
        }
    }
    
    private static void ShowRandomCaption(float percent)
    {
        if (_data == null) return;
        
        List<string> availablePhrases = new List<string>();
        
        // Determine bucket by 10% steps
        int bucket = Mathf.Clamp(Mathf.FloorToInt(percent * 10f), 0, 10); // 0..10 (0-9%, 10-19%, ..., 90-99%)
        string bucketKey;
        
        if (bucket == 10) // 100%
        {
            bucketKey = "100";
        }
        else
        {
            // Build key range: "0-9", "10-19", ..., "90-99"
            int rangeStart = bucket * 10;
            int rangeEnd = rangeStart + 9;
            bucketKey = $"{rangeStart}-{rangeEnd}";
        }
        
        if (_data.buckets != null && _data.buckets.TryGetValue(bucketKey, out var bucketPhrases) && bucketPhrases != null && bucketPhrases.Count > 0)
        {
            availablePhrases.AddRange(bucketPhrases);
        }

        // Fallback to legacy phases if the bucket is empty
        if (availablePhrases.Count == 0)
        {
            string phaseKey = GetPhaseKey(percent);
            if (_data.phases.TryGetValue(phaseKey, out List<string>? phrases) && phrases != null && phrases.Count > 0)
            {
                availablePhrases.AddRange(phrases);
            }
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
    
    /// <summary>
    /// Starts a coroutine that shows captions every 15 seconds at 100% MindBroken
    /// </summary>
    private static void StartHundredPercentLoop()
    {
        if (overlayCanvasRect == null) return;
        
        StopHundredPercentLoop(); // Stop the previous coroutine if it exists
        
        var mono = overlayCanvasRect.GetComponent<MonoBehaviour>();
        if (mono == null)
        {
            mono = overlayCanvasRect.gameObject.AddComponent<CorruptionCaptionsMono>();
        }
        
        _hundredPercentCoroutine = mono.StartCoroutine(HundredPercentLoopCoroutine());
    }
    
    /// <summary>
    /// Stops the coroutine that shows captions at 100%
    /// </summary>
    private static void StopHundredPercentLoop()
    {
        if (_hundredPercentCoroutine != null && overlayCanvasRect != null)
        {
            var mono = overlayCanvasRect.GetComponent<MonoBehaviour>();
            if (mono != null)
            {
                mono.StopCoroutine(_hundredPercentCoroutine);
            }
            _hundredPercentCoroutine = null;
        }
    }
    
    /// <summary>
    /// Coroutine for showing captions every 15 seconds on 100% MindBroken
    /// </summary>
    private static IEnumerator HundredPercentLoopCoroutine()
    {
        const float interval = 15f; // Interval in seconds
        
        while (true)
        {
            yield return new WaitForSeconds(interval);
            
            // Check whether we are still at 100%
            if (MindBrokenSystem.Percent >= 1.0f && IsEnabled && _data != null)
            {
                // Show a random caption from bucket "100"
                ShowRandomCaption(1.0f);
            }
            else
            {
                // If dropped below 100%, exit the loop
                break;
            }
        }
        
        _hundredPercentCoroutine = null;
    }
    
    private static string GetPhaseKey(float percent)
    {
        if (percent < 0.5f) return "0-49";
        if (percent < 0.8f) return "50-79";
        return "80-99";
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
        if (currentCaption != null) {
            if (currentCaptionCoroutine != null && overlayCanvasRect != null) {
                var mono = overlayCanvasRect.GetComponent<MonoBehaviour>();
                if (mono != null)
                    mono.StopCoroutine(currentCaptionCoroutine);
            }
            currentCaption.text = text;
            currentCaption.color = new Color(0.7f, 0f, 0f, 1f); // Dark saturated red (for all captions)
            
            // Start display coroutine
            if (overlayCanvasRect != null)
            {
                var mono = overlayCanvasRect.GetComponent<MonoBehaviour>();
                if (mono == null)
                {
                    mono = overlayCanvasRect.gameObject.AddComponent<CorruptionCaptionsMono>();
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
        
        // Phase 1: expand from compressed state
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
        
        currentCaptionRect.localScale = Vector3.one;
        currentCaption.gameObject.SetActive(false);
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
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None; // Standard shader channels
            
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f; // Balance between width and height (same as dialogs)
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            
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
            canvasGo.AddComponent<CorruptionCaptionsMono>();

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
            textRect.anchoredPosition = new Vector2(0f, 130f); // Center on X, 130px from bottom
            textRect.sizeDelta = new Vector2(1200f, 60f); // Width for wrapping; height for 2 lines: 60px
            
            UnityEngine.UI.Text label = textGo.AddComponent<UnityEngine.UI.Text>();
            label.fontSize = 52; // Font size 52 (30% larger than 40)
            label.alignment = TextAnchor.MiddleCenter; // Center alignment
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(0.7f, 0f, 0f, 1f); // Dark saturated red
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; // Wrap to 2 lines
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = false;
            label.font = NoREroMod.Systems.UI.HellGateFontProvider.GetUiFont();
            
            // Add black outline
            UnityEngine.UI.Outline outline = textGo.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
            
            // Neon glow removed by request
            
            textGo.layer = LayerMask.NameToLayer("UI");
            textGo.SetActive(false);
            
            currentCaption = label;
            currentCaptionRect = textRect;
            
        }
        catch (Exception ex)
        {
        }
    }
    
    private static CorruptionCaptionsData ParseJsonManually(string json)
    {
        var data = new CorruptionCaptionsData();
        
        try
        {
            // Parse phases (optional, for backward compatibility)
            Match phasesMatch = Regex.Match(json, "\"phases\"\\s*:\\s*\\{([^}]+)\\}", RegexOptions.Singleline);
            if (phasesMatch.Success)
            {
                string phasesSection = phasesMatch.Groups[1].Value;
                
                // Parse each phase
                foreach (string phaseKey in new[] { "0-49", "50-79", "80-99" })
                {
                    Match phaseMatch = Regex.Match(phasesSection, $"\"{phaseKey}\"\\s*:\\s*\\[([^\\]]+)\\]", RegexOptions.Singleline);
                    if (phaseMatch.Success)
                    {
                        string phrasesStr = phaseMatch.Groups[1].Value;
                        var phrases = new List<string>();
                        
                        // Parse phrases
                        MatchCollection phraseMatches = Regex.Matches(phrasesStr, "\"([^\"]+)\"");
                        foreach (Match phraseMatch in phraseMatches)
                        {
                            phrases.Add(phraseMatch.Groups[1].Value);
                        }
                        
                        data.phases[phaseKey] = phrases;
                    }
                }
            }
            
            // Parse buckets (10% ranges: "0-9", "10-19", ..., "90-99", "100")
            Match bucketsMatch = Regex.Match(json, "\"buckets\"\\s*:\\s*\\{([^}]+(?:\\{[^}]*\\}[^}]*)*)\\}", RegexOptions.Singleline);
            if (bucketsMatch.Success)
            {
                string bucketsSection = bucketsMatch.Groups[1].Value;
                
                // Parse each bucket (key may be "0-9", "10-19", ..., "90-99", "100")
                MatchCollection bucketMatches = Regex.Matches(bucketsSection, "\"([0-9]+-[0-9]+|[0-9]+)\"\\s*:\\s*\\[([^\\]]+)\\]", RegexOptions.Singleline);
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
        
        StopHundredPercentLoop(); // Stop the 100% coroutine
        
        MindBrokenSystem.OnPercentChanged -= OnPercentChanged;
        MindBrokenSystem.OnMilestoneReached -= OnMilestoneReached;
        _subscribed = false;
        _lastBucketShown = -1;
    }
}

/// <summary>
/// MonoBehaviour host for coroutines
/// </summary>
internal class CorruptionCaptionsMono : MonoBehaviour
{
    // Empty class used as a coroutine host
}

