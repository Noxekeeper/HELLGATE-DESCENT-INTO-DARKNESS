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

namespace NoREroMod.Patches.UI.MindBroken;

/// <summary>
/// Система восстановления MindBroken через убийства enemies
/// -1% за обычного enemy, -5% за босса
/// Shows captions каждые 5% понижения
/// </summary>
internal static class MindBrokenRecoverySystem
{
    internal static bool IsEnabled => Plugin.enableMindBrokenRecovery?.Value ?? false;
    
    private const string CanvasObjectName = "RecoveryCaptionsCanvas_XUAIGNORE";
    private const string CaptionObjectName = "RecoveryCaption_XUAIGNORE";
    
    private static RectTransform? overlayCanvasRect;
    private static UnityEngine.UI.Text? currentCaption; // Восстановлено: UnityEngine.UI.Text with Arial, as in старой версии
    private static RectTransform? currentCaptionRect;
    private static Coroutine? currentCaptionCoroutine;
    
    // Розовая пульсирующая рамка by краям экраon (один слой on full screen)
    private static GameObject? borderFrameContainer;
    private static Image? borderFrameImage;
    private static Coroutine? borderPulseCoroutine;
    
    private static RecoveryCaptionsData? _data;
    private static float _lastShownPercent = -1f;
    private static float _lastCaptionTime = 0f;
    private static float CaptionCooldown => Plugin.recoveryCaptionCooldown?.Value ?? 1.5f;
    private static HashSet<string> _recentlyShownCaptions = new HashSet<string>(); // Tracking shown phrases
    private static int _lastStepShown = -1; // Tracking последнits показанного шага (every 10%) - for recovery this обратный порядок
    
    private static HashSet<string> BossNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    
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
            string bossNamesStr = Plugin.recoveryBossNames?.Value ?? "bigonibrother";
            string[] names = bossNamesStr.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            
            BossNames.Clear();
            foreach (string name in names)
            {
                BossNames.Add(name.Trim());
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
            // Canvas and subscriptions do not recreate, only данные
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
        
        // Show only on понижении
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
                // Clear список shown phrases on переходе к новому шагу
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
            bucketKey = "90-99"; // Use самый высокий bucket for 100%
        }
        else
        {
            // Build key range: "0-9", "10-19", ..., "90-99"
            int rangeStart = bucket * 10;
            int rangeEnd = rangeStart + 9;
            bucketKey = $"{rangeStart}-{rangeEnd}";
        }
        
        // Сначала пробуем bucket
        if (_data.buckets != null && _data.buckets.TryGetValue(bucketKey, out var bucketPhrases) && bucketPhrases != null && bucketPhrases.Count > 0)
        {
            availablePhrases.AddRange(bucketPhrases);
        }
        
        // Фолбек on старый recovery список, if bucket пустой
        if (availablePhrases.Count == 0 && _data.recovery != null && _data.recovery.Count > 0)
        {
            availablePhrases.AddRange(_data.recovery);
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
    
    private static void ShowCaption(string text, bool isMilestone)
    {
        if (string.IsNullOrEmpty(text)) return;
        
        _lastCaptionTime = Time.time;
        _lastShownPercent = MindBrokenSystem.Percent;
        
        EnsureOverlayCanvas();
        EnsureCaption();
        
        if (currentCaption != null)
        {
            // Stop предыдущую корутину if exists
            if (currentCaptionCoroutine != null && overlayCanvasRect != null)
            {
                var mono = overlayCanvasRect.GetComponent<MonoBehaviour>();
                if (mono != null)
                {
                    mono.StopCoroutine(currentCaptionCoroutine);
                }
            }
            
            currentCaption.text = text;
            currentCaption.color = new Color(0.2f, 1f, 0.2f, 1f); // Ярко-зеленый for Recovery
            
            // Start coroutine показа
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
        
        // Начальное state: сжато (scale 0 by X)
        currentCaptionRect.localScale = new Vector3(0f, 1f, 1f);
        Vector2 originalPosition = currentCaptionRect.anchoredPosition;
        
        currentCaption.gameObject.SetActive(true);
        
        // Активируем и запускаем пульсацию рамки
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
        
        // Фаза 1: Резкое разжатие from сжатого состояния (0.15 seconds)
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
        
        // Возвращаем масштаб in исходное state
        currentCaptionRect.localScale = Vector3.one;
        currentCaption.gameObject.SetActive(false);
        
        // Скрываем рамку
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
            
            // Добавляем MonoBehaviour for корутин
            canvasGo.AddComponent<RecoveryCaptionsMono>();
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
            textRect.anchoredPosition = new Vector2(0f, 200f); // Центр by X, 200px from низа (выше Corruption)
            textRect.sizeDelta = new Vector2(1200f, 60f); // Шириon for переноса, высота for 2 строк: 60px
            
            // Восстановлено: Используем UnityEngine.UI.Text with Arial, as in старой версии и Corruption
            UnityEngine.UI.Text label = textGo.AddComponent<UnityEngine.UI.Text>();
            label.fontSize = 52; // Размер шрифта 52 (увеличен on 30% with 40)
            label.alignment = TextAnchor.MiddleCenter; // Выравнивание by центру
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(0.2f, 1f, 0.2f, 1f); // Ярко-зеленый for Recovery
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; // Переноwith on 2 строки
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = false;
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); // As in старой версии и Corruption
            
            // Добавляем черную обводку
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
            // Create container for рамки
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
            
            // Создаем один Image on full screen with градиентом from центра к краям
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
                borderFrameImage.color = new Color(0.4f, 0.7f, 0.5f, 0f); // Зеленый цвет for Recovery
                borderFrameImage.raycastTarget = false;
                
                // Create textуру with градиентом from центра к краям
                borderFrameImage.sprite = CreateRadialGradientSprite(1920, 1080);
                
                frameGo.layer = LayerMask.NameToLayer("UI");
            }
            
            borderFrameContainer.SetActive(false);
        }
        catch (Exception ex)
        {
        }
    }
    
    // Горизонтальные полосы upу и downу (вместо радиального градиента by краям)
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
            
            // Пульсация: синусоида from 0 until 1, умножаем on pulseAmount и добавляем baseAlpha
            float pulse = baseAlpha + pulseAmount * (0.5f + 0.5f * Mathf.Sin(elapsed * pulseSpeed * Mathf.PI * 2f));
            
            // Применяем пульсацию к единственному слою
            Color pulseColor = new Color(0.4f, 0.7f, 0.5f, pulse); // Зеленый цвет for Recovery
            borderFrameImage.color = pulseColor;
            
            yield return null;
        }
        
        // Возвращаем прозрачность in 0
        Color transparent = new Color(0.4f, 0.7f, 0.5f, 0f); // Зеленый цвет for Recovery
        borderFrameImage.color = transparent;
    }
    
    /// <summary>
    /// Проверяет, является ли enemy боссом
    /// </summary>
    internal static bool IsBoss(string enemyName)
    {
        if (string.IsNullOrEmpty(enemyName)) return false;
        return BossNames.Contains(enemyName);
    }
    
    /// <summary>
    /// Регистрирует убийство enemy и восстанавливает MindBroken
    /// </summary>
    internal static void RegisterKill(string enemyName)
    {
        
        if (!IsEnabled)
        {
            return;
        }
        
        bool isBoss = IsBoss(enemyName);
        float recoveryAmount = isBoss ? Plugin.recoveryPercentPerBossKill?.Value ?? 0.05f : Plugin.recoveryPercentPerKill?.Value ?? 0.01f;
        
        
        if (recoveryAmount > 0f)
        {
            float oldPercent = MindBrokenSystem.Percent;
            
            MindBrokenSystem.AddPercent(-recoveryAmount, isBoss ? $"boss_kill_{enemyName}" : $"kill_{enemyName}");
            
            float newPercent = MindBrokenSystem.Percent;
            
        }
        else
        {
        }
    }
    
    private static RecoveryCaptionsData ParseJsonManually(string json)
    {
        var data = new RecoveryCaptionsData();
        
        try
        {
            // Парсим buckets (новые 10% диапазоны: "90-99", "80-89", ..., "0-9")
            Match bucketsMatch = Regex.Match(json, "\"buckets\"\\s*:\\s*\\{([^}]+(?:\\{[^}]*\\}[^}]*)*)\\}", RegexOptions.Singleline);
            if (bucketsMatch.Success)
            {
                string bucketsSection = bucketsMatch.Groups[1].Value;
                
                // Парсим каждый bucket (ключ может быть "90-99", "80-89", ..., "0-9")
                MatchCollection bucketMatches = Regex.Matches(bucketsSection, "\"([0-9]+-[0-9]+)\"\\s*:\\s*\\[([^\\]]+)\\]", RegexOptions.Singleline);
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
            
            // Парсим recovery phrases (for backward compatibility)
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
            
            // Парсим partial phrases (опционально)
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
/// MonoBehaviour for корутин
/// </summary>
internal class RecoveryCaptionsMono : MonoBehaviour
{
    // Пустой класwith for корутин
}

