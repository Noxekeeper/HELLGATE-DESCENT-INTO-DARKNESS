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

namespace NoREroMod.Patches.UI.MindBroken;

/// <summary>
/// Red caption system corruption on MindBroken growth MindBroken
/// Shows captions every 10% и at milestones (50%, 75%, 90%, 100%)
/// At 100% shows надписи every 15 seconds
/// </summary>
internal static class CorruptionCaptionsSystem
{
    private static bool IsEnabled => Plugin.enableCorruptionCaptions?.Value ?? false;
    
    private const string CanvasObjectName = "CorruptionCaptionsCanvas_XUAIGNORE";
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
            // Canvas and subscriptions do not recreate, only данные
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
                // Clear список shown phrases on переходе к новому bucket
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

        // Фолбек on старые фазы, if bucket пустой
        if (availablePhrases.Count == 0)
        {
            string phaseKey = GetPhaseKey(percent);
            if (_data.phases.TryGetValue(phaseKey, out List<string>? phrases) && phrases != null && phrases.Count > 0)
            {
                availablePhrases.AddRange(phrases);
            }
        }
        
        if (availablePhrases.Count == 0) return;
        
        // Фильтруем phrases, исключая недавbut показанные
        List<string> unshownPhrases = availablePhrases.Where(phrase => !_recentlyShownCaptions.Contains(phrase)).ToList();
        
        // If все phrases already показаны, очищаем список и начинаем заново
        if (unshownPhrases.Count == 0)
        {
            _recentlyShownCaptions.Clear();
            unshownPhrases = availablePhrases.ToList();
        }
        
        // Выбираем случайную phrase from непоказанных
        string caption = unshownPhrases[UnityEngine.Random.Range(0, unshownPhrases.Count)];
        
        // Добавляем in list of shown
        _recentlyShownCaptions.Add(caption);
        
        ShowCaption(caption, false);
    }
    
    /// <summary>
    /// Запускает корутину for showing captions every 15 seconds on 100% MindBroken
    /// </summary>
    private static void StartHundredPercentLoop()
    {
        if (overlayCanvasRect == null) return;
        
        StopHundredPercentLoop(); // Stop предыдущую, if exists
        
        var mono = overlayCanvasRect.GetComponent<MonoBehaviour>();
        if (mono == null)
        {
            mono = overlayCanvasRect.gameObject.AddComponent<CorruptionCaptionsMono>();
        }
        
        _hundredPercentCoroutine = mono.StartCoroutine(HundredPercentLoopCoroutine());
    }
    
    /// <summary>
    /// Останавливает корутину for showing captions on 100%
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
        const float interval = 15f; // Интервал in secondх
        
        while (true)
        {
            yield return new WaitForSeconds(interval);
            
            // Check if that все еще on 100%
            if (MindBrokenSystem.Percent >= 1.0f && IsEnabled && _data != null)
            {
                // Show случайную надпись from bucket "100"
                ShowRandomCaption(1.0f);
            }
            else
            {
                // If dropped below 100%, exit from цикла
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
            currentCaption.color = new Color(0.7f, 0f, 0f, 1f); // Темный red, насыщенный (for всех надписей)
            
            // Start coroutine показа
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
        
        // Начальное state: сжато (scale 0 by X)
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
            // Резкое разжатие with ease out
            t = 1f - Mathf.Pow(1f - t, 3f);
            float scaleX = Mathf.Lerp(0f, 1f, t);
            currentCaptionRect.localScale = new Vector3(scaleX, 1f, 1f);
            
            yield return null;
        }
        
        // Set полный size after разжатия
        currentCaptionRect.localScale = Vector3.one;
        currentCaptionRect.anchoredPosition = originalPosition;
        
        // Фаза 2: Пульсация (оставшееся время) - больше и плавнее
        float scaleSpeed = 0.5f; // Более медленная скорость (0.5 цикла in second - плавнее)
        float scaleAmount = 0.08f; // Увеличеon амплитуда пульсации (8% - более заметно)
        float scaleElapsed = expandDuration;
        
        float elapsed = expandDuration;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            scaleElapsed += Time.deltaTime;
            
            // Loop scaler: масштабирование from 1.0 until 1.0 + scaleAmount и back
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
            canvas.sortingOrder = 32767; // Max priority - always on первом плане
            canvas.pixelPerfect = false; // Отключаем pixel perfect for лучшей читаемости
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None; // Стандартные каналы
            
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f; // Баланwith между шириной и высотой (as in диалогах)
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
            
            // Добавляем MonoBehaviour for корутин
            canvasGo.AddComponent<CorruptionCaptionsMono>();
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
            // Позиция by центру экраon by X, высота 130px from низа (такое же расстояние, as было from верха)
            textRect.anchorMin = new Vector2(0.5f, 0f); // Центр by X, нижний край by Y
            textRect.anchorMax = new Vector2(0.5f, 0f);
            textRect.pivot = new Vector2(0.5f, 0f); // Pivot in нижнем центре
            textRect.anchoredPosition = new Vector2(0f, 130f); // Центр by X, 130px from низа
            textRect.sizeDelta = new Vector2(1200f, 60f); // Шириon for переноса, высота for 2 строк: 60px
            
            UnityEngine.UI.Text label = textGo.AddComponent<UnityEngine.UI.Text>();
            label.fontSize = 52; // Размер шрифта 52 (увеличен on 30% with 40)
            label.alignment = TextAnchor.MiddleCenter; // Выравнивание by центру
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(0.7f, 0f, 0f, 1f); // Темный red, насыщенный
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; // Переноwith on 2 строки
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = false;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            
            // Добавляем черную обводку
            UnityEngine.UI.Outline outline = textGo.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
            
            // Неоновое свечение убраbut by request
            
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
            // Парсим phases (опционально, for backward compatibility)
            Match phasesMatch = Regex.Match(json, "\"phases\"\\s*:\\s*\\{([^}]+)\\}", RegexOptions.Singleline);
            if (phasesMatch.Success)
            {
                string phasesSection = phasesMatch.Groups[1].Value;
                
                // Парсим каждую фазу
                foreach (string phaseKey in new[] { "0-49", "50-79", "80-99" })
                {
                    Match phaseMatch = Regex.Match(phasesSection, $"\"{phaseKey}\"\\s*:\\s*\\[([^\\]]+)\\]", RegexOptions.Singleline);
                    if (phaseMatch.Success)
                    {
                        string phrasesStr = phaseMatch.Groups[1].Value;
                        var phrases = new List<string>();
                        
                        // Парсим phrases
                        MatchCollection phraseMatches = Regex.Matches(phrasesStr, "\"([^\"]+)\"");
                        foreach (Match phraseMatch in phraseMatches)
                        {
                            phrases.Add(phraseMatch.Groups[1].Value);
                        }
                        
                        data.phases[phaseKey] = phrases;
                    }
                }
            }
            
            // Парсим buckets (новые 10% диапазоны: "0-9", "10-19", ..., "90-99", "100")
            Match bucketsMatch = Regex.Match(json, "\"buckets\"\\s*:\\s*\\{([^}]+(?:\\{[^}]*\\}[^}]*)*)\\}", RegexOptions.Singleline);
            if (bucketsMatch.Success)
            {
                string bucketsSection = bucketsMatch.Groups[1].Value;
                
                // Парсим каждый bucket (ключ может быть "0-9", "10-19", ..., "90-99", "100")
                MatchCollection bucketMatches = Regex.Matches(bucketsSection, "\"([0-9]+-[0-9]+|[0-9]+)\"\\s*:\\s*\\[([^\\]]+)\\]", RegexOptions.Singleline);
                foreach (Match bucketMatch in bucketMatches)
                {
                    string bucketKey = bucketMatch.Groups[1].Value;
                    string phrasesStr = bucketMatch.Groups[2].Value;
                    var phrases = new List<string>();
                    
                    // Парсим phrases
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
            
            // Парсим milestones
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
        
        StopHundredPercentLoop(); // Stop корутину for 100%
        
        MindBrokenSystem.OnPercentChanged -= OnPercentChanged;
        MindBrokenSystem.OnMilestoneReached -= OnMilestoneReached;
        _subscribed = false;
        _lastBucketShown = -1;
    }
}

/// <summary>
/// MonoBehaviour for корутин
/// </summary>
internal class CorruptionCaptionsMono : MonoBehaviour
{
    // Пустой класwith for корутин
}

