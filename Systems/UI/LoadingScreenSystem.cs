using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NoREroMod.Systems.UI;

/// <summary>
/// HellGateSplashScreen - Two-stage splash screen for HELLGATE mod
/// Stage 1: Language selection (only on first run)
/// Stage 2: Main splash screen with mod info
/// </summary>
internal static class HellGateSplashScreen
{
    private static GameObject? _canvas;
    private static GameObject? _languageSelectionScreen;
    private static GameObject? _mainSplashScreen;
    private static MonoBehaviour? _coroutineRunner;
    private static bool _isInitialized = false;
    private static Sprite? _whiteSprite; // Static sprite for background
    private static Dictionary<string, Sprite> _flagSprites = new(); // Flag cache
    private static Sprite? _logoSprite; // HELLGATE logo PNG
    private static Sprite? _spriteDiscordNoR;   // Discord NoR Community button
    private static Sprite? _spriteDiscordHellGate; // Discord HellGate Support button
    private static Sprite? _spriteKoFi;         // Ko-fi button
    private static Sprite? _spriteStartButton;  // Start button PNG
    
    // Data from JSON
    private static LanguageSelectionData? _languageData;
    private static Dictionary<string, SplashScreenData> _splashData = new();
    
    // UI elements Language selection
    private static UnityEngine.UI.Text? _selectLanguageText;
    
    // UI elements Main Splash
    private static UnityEngine.UI.Text? _titleText;
    private static UnityEngine.UI.Text? _demoText;
    private static UnityEngine.UI.Text? _warningText;
    private static UnityEngine.UI.Text? _infoText;
    private static Button? _startButton;
    
    private const float FADE_DURATION = 0.5f;
    private const float MIN_DISPLAY_TIME = 3f;
    
    /// <summary>
    /// Initialize splash screen
    /// </summary>
    public static void Initialize()
    {
        if (_isInitialized)
        {
            // Plugin.Log?.LogInfo("[HellGate Splash] Already initialized, skipping..."); // Disabled for release
            return;
        }
        
        try
        {
            // Plugin.Log?.LogInfo("[HellGate Splash] Starting initialization..."); // Disabled for release
            
            // Load data from JSON
            LoadLanguageSelectionData();
            LoadSplashScreenData();
            LoadFlagSprites(); // Load flags
            LoadLogoSprite(); // HELLGATE logo (replaces title text)
            LoadButtonSprites(); // Discord & Ko-fi button images
            
            // Create Canvas
            CreateCanvas();
            
            // Check if language selected
            string selectedLanguage = "";
            if (Plugin.hellGateLanguage != null)
            {
                selectedLanguage = Plugin.hellGateLanguage.Value ?? "";
            }
            
            // Plugin.Log?.LogInfo($"[HellGate Splash] Selected language: '{selectedLanguage}' (empty = show language selection)"); // Disabled for release
            
            if (string.IsNullOrEmpty(selectedLanguage))
            {
                // Show screen language selection
                // Plugin.Log?.LogInfo("[HellGate Splash] Showing language selection screen..."); // Disabled for release
                ShowLanguageSelection();
            }
            else
            {
                // Show main splash screen
                // Plugin.Log?.LogInfo($"[HellGate Splash] Showing main splash screen for language: {selectedLanguage}"); // Disabled for release
                ShowMainSplash(selectedLanguage);
            }
            
            _isInitialized = true;
            
            // Subscribe to scene load
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            // Plugin.Log?.LogInfo("[HellGate Splash] Initialization complete!"); // Disabled for release
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[HellGate Splash] Failed to initialize: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Load data language selection
    /// </summary>
    private static void LoadLanguageSelectionData()
    {
        try
        {
            string dataPath = GetDataPath();
            // Plugin.Log?.LogInfo($"[HellGate Splash] Looking for LanguageSelectionData.json in: {dataPath}"); // Disabled for release
            string jsonPath = Path.Combine(dataPath, "LanguageSelectionData.json");
            
            // Plugin.Log?.LogInfo($"[HellGate Splash] Full path: {jsonPath}"); // Disabled for release
            // Plugin.Log?.LogInfo($"[HellGate Splash] File exists: {File.Exists(jsonPath)}"); // Disabled for release
            
            if (File.Exists(jsonPath))
            {
                string jsonContent = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8); // Explicitly specify UTF-8 for Unicode
                Plugin.Log?.LogInfo($"[HellGate Splash] JSON content length: {jsonContent.Length}");
                
                // Check if there are Japanese and Chinese chars in file
                if (jsonContent.Contains("日本語"))
                {
                    Plugin.Log?.LogInfo("[HellGate Splash] Found Japanese characters in JSON");
                }
                else
                {
                    Plugin.Log?.LogWarning("[HellGate Splash] Japanese characters NOT found in JSON!");
                }
                
                if (jsonContent.Contains("中文"))
                {
                    Plugin.Log?.LogInfo("[HellGate Splash] Found Chinese characters in JSON");
                }
                else
                {
                    Plugin.Log?.LogWarning("[HellGate Splash] Chinese characters NOT found in JSON!");
                }
                
                _languageData = ParseLanguageSelectionData(jsonContent);
                
                // If parsing failed or not all languages found, use default values
                if (_languageData == null || _languageData.languages == null || _languageData.languages.Count < 10)
                {
                    Plugin.Log?.LogWarning($"[HellGate Splash] Parsing failed or incomplete (found {_languageData?.languages?.Count ?? 0} languages), using defaults");
                    _languageData = CreateDefaultLanguageData();
                }
                else
                {
                    // Check if JP and CN parsed correctly
                    if (_languageData.languages.ContainsKey("JP"))
                    {
                        Plugin.Log?.LogInfo($"[HellGate Splash] JP parsed: '{_languageData.languages["JP"].name}'");
                    }
                    else
                    {
                        Plugin.Log?.LogWarning("[HellGate Splash] JP NOT found in parsed data!");
                    }
                    
                    if (_languageData.languages.ContainsKey("CN"))
                    {
                        Plugin.Log?.LogInfo($"[HellGate Splash] CN parsed: '{_languageData.languages["CN"].name}'");
                    }
                    else
                    {
                        Plugin.Log?.LogWarning("[HellGate Splash] CN NOT found in parsed data!");
                    }
                }
                Plugin.Log?.LogInfo($"[HellGate Splash] LanguageSelectionData loaded! Total languages: {_languageData?.languages?.Count ?? 0}");
            }
            else
            {
                Plugin.Log?.LogError($"[HellGate Splash] LanguageSelectionData.json not found at: {jsonPath}");
                // Plugin.Log?.LogInfo($"[HellGate Splash] Directory exists: {Directory.Exists(dataPath)}"); // Disabled for release
                if (Directory.Exists(dataPath))
                {
                    // Plugin.Log?.LogInfo($"[HellGate Splash] Files in directory: {string.Join(", ", Directory.GetFiles(dataPath))}"); // Disabled for release
                }
                // Create default data
                // Plugin.Log?.LogWarning("[HellGate Splash] Using default language selection data"); // Disabled for release
                _languageData = CreateDefaultLanguageData();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[HellGate Splash] Error loading language selection data: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Load splash screen data for all languages
    /// </summary>
    private static void LoadSplashScreenData()
    {
        string[] languages = { "RU", "EN", "JP", "CN", "KR", "FR", "DE", "PT", "BR", "ES" };
        string dataPath = GetDataPath();
        
        // Plugin.Log?.LogInfo($"[HellGate Splash] Loading splash screen data from: {dataPath}"); // Disabled for release
        
        foreach (string lang in languages)
        {
            try
            {
                string langPath = Path.Combine(dataPath, lang);
                string jsonPath = Path.Combine(langPath, "SplashScreenData.json");
                
                if (File.Exists(jsonPath))
                {
                    string jsonContent = File.ReadAllText(jsonPath);
                    SplashScreenData data = ParseSplashScreenData(jsonContent);
                    _splashData[lang] = data;
                    // Plugin.Log?.LogInfo($"[HellGate Splash] Loaded splash data for {lang}"); // Disabled for release
                }
                else
                {
                    // Plugin.Log?.LogWarning($"[HellGate Splash] SplashScreenData.json not found for {lang} at: {jsonPath}, using defaults"); // Disabled for release
                    _splashData[lang] = CreateDefaultSplashData(lang);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[HellGate Splash] Error loading splash data for {lang}: {ex.Message}");
                _splashData[lang] = CreateDefaultSplashData(lang);
            }
        }
    }
    
    /// <summary>
    /// Parse LanguageSelectionData.json
    /// </summary>
    private static LanguageSelectionData ParseLanguageSelectionData(string json)
    {
        var data = new LanguageSelectionData();
        data.languages = new Dictionary<string, LanguageInfo>();
        
        // Simple JSON parsing without libraries
        var selectMatch = Regex.Match(json, @"""selectLanguage""\s*:\s*""([^""]+)""");
        if (selectMatch.Success)
        {
            data.selectLanguage = selectMatch.Groups[1].Value;
        }
        
        // Parse languages - improved regex for multiline JSON
        // Default values in case parsing failed
        var defaultData = CreateDefaultLanguageData();
        
        string[] languages = { "RU", "EN", "JP", "CN", "KR", "FR", "DE", "PT", "BR", "ES" };
        foreach (string lang in languages)
        {
            // More flexible regex, that works with multiline JSON
            // Find block for each language separately
            var langBlockMatch = Regex.Match(json, $@"""{lang}""\s*:\s*\{{([^}}]+)\}}", RegexOptions.Singleline);
            if (langBlockMatch.Success)
            {
                string langBlock = langBlockMatch.Groups[1].Value;
                
                // Parse name and flag from block (use more universal pattern for Unicode)
                var nameMatch = Regex.Match(langBlock, @"""name""\s*:\s*""(.+?)""", RegexOptions.Singleline);
                var flagMatch = Regex.Match(langBlock, @"""flag""\s*:\s*""(.+?)""", RegexOptions.Singleline);
                
                if (nameMatch.Success && !string.IsNullOrEmpty(nameMatch.Groups[1].Value))
                {
                    string parsedName = nameMatch.Groups[1].Value;
                    data.languages[lang] = new LanguageInfo
                    {
                        name = parsedName,
                        flag = flagMatch.Success ? flagMatch.Groups[1].Value : ""
                    };
                    Plugin.Log?.LogInfo($"[HellGate Splash] Parsed language {lang}: '{parsedName}' (length: {parsedName.Length})");
                }
                else
                {
                    // If parsing failed, use default value
                    if (defaultData.languages.ContainsKey(lang))
                    {
                        data.languages[lang] = defaultData.languages[lang];
                        Plugin.Log?.LogWarning($"[HellGate Splash] Failed to parse {lang}, using default: '{defaultData.languages[lang].name}'");
                        if (langBlockMatch.Success)
                        {
                            Plugin.Log?.LogWarning($"[HellGate Splash] Block found for {lang}, but nameMatch failed. Block preview: {langBlock.Substring(0, Math.Min(100, langBlock.Length))}");
                        }
                    }
                }
            }
            else
            {
                // If block not found, use default value
                if (defaultData.languages.ContainsKey(lang))
                {
                    data.languages[lang] = defaultData.languages[lang];
                    // Plugin.Log?.LogWarning($"[HellGate Splash] Block not found for {lang}, using default: {defaultData.languages[lang].name}"); // Disabled for release
                }
            }
        }
        
        // Plugin.Log?.LogInfo($"[HellGate Splash] Total languages parsed: {data.languages.Count}"); // Disabled for release
        return data;
    }
    
    /// <summary>
    /// Parse SplashScreenData.json
    /// </summary>
    private static SplashScreenData ParseSplashScreenData(string json)
    {
        var data = new SplashScreenData();

        var modTitleMatch = Regex.Match(json, @"""modTitle""\s*:\s*""([^""]+)""");
        if (modTitleMatch.Success) data.modTitle = modTitleMatch.Groups[1].Value;

        var titleMatch = Regex.Match(json, @"""title""\s*:\s*""([^""]+)""");
        if (titleMatch.Success) data.title = titleMatch.Groups[1].Value;
        
        var demoMatch = Regex.Match(json, @"""demo""\s*:\s*""([^""]+)""");
        if (demoMatch.Success) data.demo = demoMatch.Groups[1].Value;
        
        var warningMatch = Regex.Match(json, @"""warning""\s*:\s*""([^""]+)""", RegexOptions.Singleline);
        if (warningMatch.Success) data.warning = warningMatch.Groups[1].Value.Replace("\\n", "\n");

        var additionalMatch = Regex.Match(json, @"""additionalText""\s*:\s*""([^""]+)""", RegexOptions.Singleline);
        if (additionalMatch.Success) data.additionalText = additionalMatch.Groups[1].Value.Replace("\\n", "\n");

        var infoMatch = Regex.Match(json, @"""info""\s*:\s*""([^""]+)""", RegexOptions.Singleline);
        if (infoMatch.Success) data.info = infoMatch.Groups[1].Value.Replace("\\n", "\n");
        
        var buttonMatch = Regex.Match(json, @"""startButton""\s*:\s*""([^""]+)""");
        if (buttonMatch.Success) data.startButton = buttonMatch.Groups[1].Value;
        
        return data;
    }
    
    /// <summary>
    /// Base directory for HellGate resources (SplashScreen, Language, DoreiFapping, Wolf Mod, etc.).
    /// Same as DoreiSkeletonLoader, WolfSkeletonLoader, BadEndPlayerLoader: [game root]/sources/HellGate_sources
    /// </summary>
    private static string GetHellGateSourcesPath()
    {
        try
        {
            string gameRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (string.IsNullOrEmpty(gameRoot)) return null;
            return Path.Combine(Path.Combine(gameRoot, "sources"), "HellGate_sources");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get path to folder HellGateJson
    /// </summary>
    private static string GetDataPath()
    {
        // Main path: BepInEx/plugins/HellGateJson/
        try
        {
            string basePath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string bepInEx = Path.Combine(basePath, "BepInEx");
            string plugins = Path.Combine(bepInEx, "plugins");
            string hellGateJson = Path.Combine(plugins, "HellGateJson");
            
            // Plugin.Log?.LogInfo($"[HellGate Splash] Checking path: {hellGateJson}"); // Disabled for release
            // Plugin.Log?.LogInfo($"[HellGate Splash] Directory exists: {Directory.Exists(hellGateJson)}"); // Disabled for release
            
            if (Directory.Exists(hellGateJson))
            {
                // Plugin.Log?.LogInfo($"[HellGate Splash] Using path: {hellGateJson}"); // Disabled for release
                return hellGateJson;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[HellGate Splash] Error in GetDataPath (main): {ex.Message}");
        }
        
        // Last fallback (same as main - ensures path is returned even if Directory.Exists failed above)
        try
        {
            string basePathFallback = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string bepInExFallback = Path.Combine(basePathFallback, "BepInEx");
            string pluginsFallback = Path.Combine(bepInExFallback, "plugins");
            string result = Path.Combine(pluginsFallback, "HellGateJson");
            // Plugin.Log?.LogInfo($"[HellGate Splash] Using last fallback: {result}"); // Disabled for release
            return result;
        }
        catch
        {
            Plugin.Log?.LogError("[HellGate Splash] All paths failed!");
            return "";
        }
    }
    
    /// <summary>
    /// Load flags from PNG files
    /// </summary>
    private static void LoadFlagSprites()
    {
        _flagSprites.Clear();
        string basePath = GetHellGateSourcesPath();
        if (string.IsNullOrEmpty(basePath)) return;
        string flagsPath = Path.Combine(basePath, "Language");
        if (!Directory.Exists(flagsPath)) return;
        
        // Language code mapping to file names
        Dictionary<string, string> flagFileMap = new Dictionary<string, string>
        {
            { "RU", "Russia-Flag.256.png" },
            { "EN", "United-Kingdom-Flag.256.png" },
            { "JP", "Japan-Flag.256.png" },
            { "CN", "China-Flag.256.png" },
            { "KR", "South-Korea-Flag.256.png" },
            { "FR", "France-Flag.256.png" },
            { "DE", "Germany-Flag.256.png" },
            { "PT", "Portugal-Flag.256.png" },
            { "BR", "Brazil-Flag.256.png" },
            { "ES", "Spain-Flag.256.png" }
        };
        
        foreach (var kvp in flagFileMap)
        {
            string filePath = Path.Combine(flagsPath, kvp.Value);
            if (File.Exists(filePath))
            {
                try
                {
                    byte[] fileData = File.ReadAllBytes(filePath);
                    Texture2D texture = new Texture2D(256, 256);
                    if (texture.LoadImage(fileData))
                    {
                        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f), 100f);
                        _flagSprites[kvp.Key] = sprite;
                        // Plugin.Log?.LogInfo($"[HellGate Splash] Loaded flag for {kvp.Key}: {kvp.Value}"); // Disabled for release
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"[HellGate Splash] Failed to load flag {kvp.Value}: {ex.Message}");
                }
            }
            else
            {
                // Plugin.Log?.LogWarning($"[HellGate Splash] Flag file not found: {filePath}"); // Disabled for release
            }
        }
        
        // Plugin.Log?.LogInfo($"[HellGate Splash] Loaded {_flagSprites.Count} flag sprites"); // Disabled for release
    }
    
    private const string LOGO_FILENAME = "HELLGATELOGO.png";
    
    /// <summary>
    /// Load HELLGATE logo PNG from SplashScreen folder (replaces mod title + title text)
    /// </summary>
    private static void LoadLogoSprite()
    {
        string filePath = GetLogoSpritePath();
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
        
        try
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(fileData))
            {
                _logoSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[HellGate Splash] Failed to load logo {LOGO_FILENAME}: {ex.Message}");
        }
    }
    
    private static string GetLogoSpritePath()
    {
        string basePath = GetHellGateSourcesPath();
        if (string.IsNullOrEmpty(basePath)) return null;
        return Path.Combine(Path.Combine(basePath, "SplashScreen"), LOGO_FILENAME);
    }
    
    /// <summary>
    /// Load button sprites from SplashScreen folder
    /// </summary>
    private static void LoadButtonSprites()
    {
        string basePath = GetHellGateSourcesPath();
        if (string.IsNullOrEmpty(basePath)) return;
        string dir = Path.Combine(basePath, "SplashScreen");
        if (Directory.Exists(dir))
        {
            string p1 = Path.Combine(dir, "Discord_NoR_Community.png");
            string p2 = Path.Combine(dir, "Discord_HellGate_Support.png");
            string p3 = Path.Combine(dir, "Ko-fi.png");
            string p4 = Path.Combine(dir, "Start Button.png");
            try
            {
                if (File.Exists(p1)) { byte[] b = File.ReadAllBytes(p1); var t = new Texture2D(2, 2); if (t.LoadImage(b)) _spriteDiscordNoR = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f); }
                if (File.Exists(p2)) { byte[] b = File.ReadAllBytes(p2); var t = new Texture2D(2, 2); if (t.LoadImage(b)) _spriteDiscordHellGate = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f); }
                if (File.Exists(p3)) { byte[] b = File.ReadAllBytes(p3); var t = new Texture2D(2, 2); if (t.LoadImage(b)) _spriteKoFi = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f); }
                if (File.Exists(p4)) { byte[] b = File.ReadAllBytes(p4); var t = new Texture2D(2, 2); if (t.LoadImage(b)) _spriteStartButton = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f); }
            }
            catch (Exception ex) { Plugin.Log?.LogError($"[HellGate Splash] LoadButtonSprites: {ex.Message}"); }
        }
    }
    
    private static void AddCenteredText(GameObject parent, string text, int fontSize, Color color, Font font, float yFromTop)
    {
        GameObject obj = new GameObject();
        obj.transform.SetParent(parent.transform, false);
        var t = obj.AddComponent<UnityEngine.UI.Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.UpperCenter;
        t.color = color;
        t.font = font;
        RectTransform r = obj.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 1f);
        r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(0f, yFromTop);
        r.sizeDelta = new Vector2(460f, 40f);
    }

    private static void AddLeftText(GameObject parent, string richText, Font font, float yFromTop, float height)
    {
        GameObject obj = new GameObject();
        obj.transform.SetParent(parent.transform, false);
        var t = obj.AddComponent<UnityEngine.UI.Text>();
        t.supportRichText = true;
        t.text = richText;
        t.fontSize = 14;
        t.alignment = TextAnchor.UpperLeft;
        t.color = new Color(0.9f, 0.9f, 0.9f);
        t.font = font;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        RectTransform r = obj.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(0f, yFromTop);
        r.sizeDelta = new Vector2(0f, height);
    }

    /// <summary>
    /// Create image button. Size from PNG sprite (no phantom area). Falls back to text if sprite is null.
    /// </summary>
    private static GameObject CreateImageButton(GameObject parent, string name, Sprite sprite, string fallbackText, float x, float y, float fallbackW, float fallbackH, Action onClick)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent.transform, false);
        Button btn = obj.AddComponent<Button>();
        Image img = obj.AddComponent<Image>();
        float w, h;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.preserveAspect = false; // Fill rect exactly — rect = sprite size
            img.color = Color.white;
            w = sprite.rect.width;
            h = sprite.rect.height;
        }
        else
        {
            img.color = new Color(0.2f, 0.2f, 0.3f, 1f);
            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(obj.transform, false);
            var txt = txtObj.AddComponent<UnityEngine.UI.Text>();
            txt.text = fallbackText;
            txt.fontSize = 18;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            RectTransform tr = txtObj.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.sizeDelta = Vector2.zero;
            w = fallbackW;
            h = fallbackH;
        }
        btn.onClick.AddListener(() => onClick());
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
        var trigger = obj.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var enter = new UnityEngine.EventSystems.EventTrigger.Entry();
        enter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        enter.callback.AddListener((UnityEngine.EventSystems.BaseEventData d) => { rect.localScale = new Vector3(1.12f, 1.12f, 1f); });
        trigger.triggers.Add(enter);
        var exit = new UnityEngine.EventSystems.EventTrigger.Entry();
        exit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        exit.callback.AddListener((UnityEngine.EventSystems.BaseEventData d) => { rect.localScale = Vector3.one; });
        trigger.triggers.Add(exit);
        return obj;
    }
    
    /// <summary>
    /// Create white sprite for background (once, reuse)
    /// </summary>
    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite == null)
        {
            Texture2D whiteTexture = new Texture2D(1, 1);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
            _whiteSprite = Sprite.Create(whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        }
        return _whiteSprite;
    }
    
    /// <summary>
    /// Create Canvas
    /// </summary>
    private static void CreateCanvas()
    {
        // Create white sprite in advance
        GetWhiteSprite();
        
        // Create object for coroutines
        GameObject runnerObj = new GameObject("HellGateSplashRunner");
        UnityEngine.Object.DontDestroyOnLoad(runnerObj);
        _coroutineRunner = runnerObj.AddComponent<SplashScreenRunner>();
        
        // Create Canvas (XUAIGNORE - exclude from AutoTranslator)
        _canvas = new GameObject("HELLGATE_SplashScreen_XUAIGNORE");
        Canvas canvas = _canvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32767;
        
        CanvasScaler scaler = _canvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        _canvas.AddComponent<GraphicRaycaster>();
        UnityEngine.Object.DontDestroyOnLoad(_canvas);
        _canvas.SetActive(true);
        
        // Ensure that Canvas visible
        CanvasGroup canvasGroup = _canvas.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = _canvas.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 1f; // Full opacity
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        
        // Plugin.Log?.LogInfo($"[HellGate Splash] Canvas created: active={_canvas.activeSelf}, alpha={canvasGroup.alpha}"); // Disabled for release
        
        // EventSystem
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            UnityEngine.Object.DontDestroyOnLoad(eventSystemObj);
        }
    }
    
    /// <summary>
    /// Display screen language selection
    /// </summary>
    private static void ShowLanguageSelection()
    {
        if (_canvas == null || _languageData == null) return;
        
        // Black background (fullscreen) - create directly on Canvas, as in BadEnd
        GameObject background = new GameObject("LanguageSelectionBackground");
        background.transform.SetParent(_canvas.transform, false);
        Image bgImage = background.AddComponent<Image>();
        
        // Use static white sprite and paint black
        Sprite whiteSprite = GetWhiteSprite();
        bgImage.sprite = whiteSprite;
        bgImage.type = Image.Type.Simple; // Simple type (no stretching)
        bgImage.color = new Color(0f, 0f, 0f, 1f); // Pure black color (RGB=0,0,0, Alpha=1) - explicitly set alpha
        bgImage.raycastTarget = false; // Do not block clicks
        bgImage.enabled = true; // Ensure enabled
        
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        // Ensure background is first (at bottom by z-order)
        background.transform.SetAsFirstSibling();
        
        // Plugin.Log?.LogInfo($"[HellGate Splash] Language selection background created:"); // Disabled for release
        // Plugin.Log?.LogInfo($"  - GameObject active: {background.activeSelf}"); // Disabled for release
        // Plugin.Log?.LogInfo($"  - Image enabled: {bgImage.enabled}"); // Disabled for release
        // Plugin.Log?.LogInfo($"  - Sprite: {whiteSprite != null} (name: {whiteSprite?.name})"); // Disabled for release
        // Plugin.Log?.LogInfo($"  - Color: R={bgImage.color.r}, G={bgImage.color.g}, B={bgImage.color.b}, A={bgImage.color.a}"); // Disabled for release
        // Plugin.Log?.LogInfo($"  - Parent: {background.transform.parent?.name}"); // Disabled for release
        // Plugin.Log?.LogInfo($"  - RectTransform size: {bgRect.sizeDelta}, anchors: min={bgRect.anchorMin}, max={bgRect.anchorMax}"); // Disabled for release
        
        // Create language selection screen (XUAIGNORE - exclude from AutoTranslator)
        _languageSelectionScreen = new GameObject("LanguageSelectionScreen_XUAIGNORE");
        _languageSelectionScreen.transform.SetParent(_canvas.transform, false);
        
        // Container
        GameObject container = new GameObject("Container");
        container.transform.SetParent(_languageSelectionScreen.transform, false);
        RectTransform containerRect = container.AddComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero; // Stretch to full screen
        containerRect.anchorMax = Vector2.one;
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = Vector2.zero; // Size will be automatic
        
        // Text "Select Language" (XUAIGNORE - exclude from AutoTranslator)
        GameObject selectTextObj = new GameObject("SelectLanguageText_XUAIGNORE");
        selectTextObj.transform.SetParent(container.transform, false);
        _selectLanguageText = selectTextObj.AddComponent<UnityEngine.UI.Text>();
        _selectLanguageText.text = _languageData.selectLanguage;
        _selectLanguageText.fontSize = 48;
        _selectLanguageText.alignment = TextAnchor.MiddleCenter;
        _selectLanguageText.fontStyle = FontStyle.Bold;
        _selectLanguageText.color = Color.white;
        _selectLanguageText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        
        RectTransform selectRect = selectTextObj.GetComponent<RectTransform>();
        selectRect.anchorMin = new Vector2(0.5f, 0.5f);
        selectRect.anchorMax = new Vector2(0.5f, 0.5f);
        selectRect.pivot = new Vector2(0.5f, 0.5f);
        selectRect.anchoredPosition = new Vector2(0f, 150f); // Above center, above flags
        selectRect.sizeDelta = new Vector2(800f, 80f);
        
        // Language flags in row left to right above center
        float flagSize = 120f; // Flag size
        float flagSpacing = 140f; // Distance between flags
        float startX = -(_languageData.languages.Count - 1) * flagSpacing / 2f; // Center row
        float flagY = 50f; // Above center of screen
        int index = 0;
        
        // Plugin.Log?.LogInfo($"[HellGate Splash] Creating language flag buttons. Total languages: {_languageData.languages.Count}"); // Disabled for release
        
        foreach (var lang in _languageData.languages)
        {
            // Plugin.Log?.LogInfo($"[HellGate Splash] Creating flag button for language: {lang.Key} ({lang.Value.name})"); // Disabled for release
            
            // Create button (parent object)
            GameObject langButtonObj = new GameObject($"LanguageFlagButton_{lang.Key}_XUAIGNORE");
            langButtonObj.transform.SetParent(container.transform, false);
            
            RectTransform langButtonRect = langButtonObj.AddComponent<RectTransform>();
            langButtonRect.anchorMin = new Vector2(0.5f, 0.5f);
            langButtonRect.anchorMax = new Vector2(0.5f, 0.5f);
            langButtonRect.pivot = new Vector2(0.5f, 0.5f);
            float xPos = startX + (index * flagSpacing);
            langButtonRect.anchoredPosition = new Vector2(xPos, flagY);
            langButtonRect.sizeDelta = new Vector2(flagSize, flagSize);
            
            // Plugin.Log?.LogInfo($"[HellGate Splash] Language {lang.Key} flag positioned at X: {xPos}, Y: {flagY}"); // Disabled for release
            
            // Add Button component
            Button langButton = langButtonObj.AddComponent<Button>();
            langButton.interactable = true;
            langButton.enabled = true;
            
            // Add Image for flag
            Image flagImage = langButtonObj.AddComponent<Image>();
            if (_flagSprites.ContainsKey(lang.Key))
            {
                flagImage.sprite = _flagSprites[lang.Key];
                flagImage.preserveAspect = true; // Preserve aspect
            }
            else
            {
                // If flag not loaded, show text
                flagImage.sprite = GetWhiteSprite();
                flagImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }
            flagImage.raycastTarget = true;
            langButton.targetGraphic = flagImage;
            
            string langCode = lang.Key;
            langButton.onClick.AddListener(() => {
                // Plugin.Log?.LogInfo($"[HellGate Splash] Language {langCode} clicked!"); // Disabled for release
                OnLanguageSelected(langCode);
            });
            
            // Hover effect via EventTrigger (scale and highlight)
            UnityEngine.EventSystems.EventTrigger trigger = langButtonObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            
            UnityEngine.EventSystems.EventTrigger.Entry enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            enterEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((eventData) => {
                // Scale flag on hover
                langButtonRect.localScale = new Vector3(1.15f, 1.15f, 1f);
            });
            trigger.triggers.Add(enterEntry);
            
            UnityEngine.EventSystems.EventTrigger.Entry exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            exitEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((eventData) => {
                // Restore normal size
                langButtonRect.localScale = Vector3.one;
            });
            trigger.triggers.Add(exitEntry);
            
            // Plugin.Log?.LogInfo($"[HellGate Splash] Language {lang.Key} flag button created: active={langButtonObj.activeSelf}, button.interactable={langButton.interactable}"); // Disabled for release
            
            index++;
        }
        
        // Plugin.Log?.LogInfo($"[HellGate Splash] Created {index} language buttons total"); // Disabled for release
    }
    
    /// <summary>
    /// Language selection handler
    /// </summary>
    private static void OnLanguageSelected(string languageCode)
    {
        // Save language
        if (Plugin.hellGateLanguage != null)
        {
            Plugin.hellGateLanguage.Value = languageCode;
        }
        
        // Reload all systems with new language
        try
        {
            // Dialogue systems
            NoREroMod.Systems.Dialogue.DialogueFramework.Reload();
            NoREroMod.Systems.Dialogue.QTEReactionFramework.Reload();
            
            // MindBroken systems (they also load JSON from language folders)
            NoREroMod.Patches.UI.MindBroken.CorruptionCaptionsSystem.Reload();
            NoREroMod.Patches.UI.MindBroken.MindBrokenRecoverySystem.Reload();
            NoREroMod.Patches.UI.MindBroken.MindBrokenBadEndSystem.ReloadEpilogues();
            
            // Camera settings
            NoREroMod.Systems.Camera.CameraSettings.Reload();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[HellGate Splash] Failed to reload systems: {ex.Message}");
        }
        
        // Transition to main splash screen
        if (_coroutineRunner != null)
        {
            _coroutineRunner.StartCoroutine(TransitionToMainSplash(languageCode));
        }
    }
    
    /// <summary>
    /// Transition to main splash screen
    /// </summary>
    private static IEnumerator TransitionToMainSplash(string languageCode)
    {
        // Fade out language selection
        if (_languageSelectionScreen != null)
        {
            CanvasGroup langGroup = _languageSelectionScreen.GetComponent<CanvasGroup>();
            if (langGroup == null) langGroup = _languageSelectionScreen.AddComponent<CanvasGroup>();
            
            float elapsed = 0f;
            while (elapsed < FADE_DURATION)
            {
                elapsed += Time.deltaTime;
                langGroup.alpha = 1f - (elapsed / FADE_DURATION);
                yield return null;
            }
            
            _languageSelectionScreen.SetActive(false);
        }
        
        // Show main splash screen
        ShowMainSplash(languageCode);
    }
    
    /// <summary>
    /// Display main splash screen
    /// </summary>
    private static void ShowMainSplash(string languageCode)
    {
        if (_canvas == null) return;
        
        // Load data for selected language
        if (!_splashData.TryGetValue(languageCode, out SplashScreenData? data))
        {
            // Fallback to EN if no data
            if (!_splashData.TryGetValue("EN", out data))
            {
                Plugin.Log?.LogError("[HellGate Splash] No splash screen data available!");
                return;
            }
        }
        
        // Black background (fullscreen) - create directly on Canvas, as in BadEnd
        GameObject background = new GameObject("MainSplashBackground");
        background.transform.SetParent(_canvas.transform, false);
        Image bgImage = background.AddComponent<Image>();
        
        // Use static white sprite and paint black
        Sprite whiteSprite = GetWhiteSprite();
        bgImage.sprite = whiteSprite;
        bgImage.type = Image.Type.Simple; // Simple type (no stretching)
        bgImage.color = new Color(0f, 0f, 0f, 1f); // Pure black color (RGB=0,0,0, Alpha=1) - explicitly set alpha
        bgImage.raycastTarget = false; // Do not block clicks
        bgImage.enabled = true; // Ensure enabled
        
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        // Ensure background is first (at bottom by z-order)
        background.transform.SetAsFirstSibling();
        
        // Plugin.Log?.LogInfo($"[HellGate Splash] Main splash background created:"); // Disabled for release
        // Plugin.Log?.LogInfo($"  - GameObject active: {background.activeSelf}"); // Disabled for release
        // Plugin.Log?.LogInfo($"  - Image enabled: {bgImage.enabled}"); // Disabled for release
        // Plugin.Log?.LogInfo($"  - Sprite: {whiteSprite != null} (name: {whiteSprite?.name})"); // Disabled for release
        // Plugin.Log?.LogInfo($"  - Color: R={bgImage.color.r}, G={bgImage.color.g}, B={bgImage.color.b}, A={bgImage.color.a}"); // Disabled for release
        // Plugin.Log?.LogInfo($"  - Parent: {background.transform.parent?.name}"); // Disabled for release
        // Plugin.Log?.LogInfo($"  - RectTransform size: {bgRect.sizeDelta}, anchors: min={bgRect.anchorMin}, max={bgRect.anchorMax}"); // Disabled for release
        
        // Create main splash screen (XUAIGNORE - exclude from AutoTranslator) - full screen for widescreen layout
        _mainSplashScreen = new GameObject("MainSplashScreen_XUAIGNORE");
        _mainSplashScreen.transform.SetParent(_canvas.transform, false);
        Image mainBg = _mainSplashScreen.AddComponent<Image>();
        mainBg.color = new Color(0f, 0f, 0f, 0f); // Invisible, just to get RectTransform
        mainBg.raycastTarget = false;
        RectTransform mainRect = _mainSplashScreen.GetComponent<RectTransform>();
        mainRect.anchorMin = Vector2.zero;
        mainRect.anchorMax = Vector2.one;
        mainRect.sizeDelta = Vector2.zero;
        mainRect.anchoredPosition = Vector2.zero;
        
        // Container - full width for widescreen (Credits left edge, Thanks right edge)
        GameObject container = new GameObject("ContentContainer");
        container.transform.SetParent(_mainSplashScreen.transform, false);
        RectTransform containerRect = container.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 0.5f);
        containerRect.anchorMax = new Vector2(1f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = new Vector2(0f, 1100f); // Full width, fixed height
        
        // Logo or fallback to text (mod title + title)
        if (_logoSprite != null)
        {
            // HELLGATE logo PNG (499x328) - replaces HELL GATE + Descent Into Darkness text
            GameObject logoObj = new GameObject("HellGateLogo_XUAIGNORE");
            logoObj.transform.SetParent(container.transform, false);
            Image logoImage = logoObj.AddComponent<Image>();
            logoImage.sprite = _logoSprite;
            logoImage.preserveAspect = true;
            logoImage.color = Color.white;
            logoImage.raycastTarget = false;

            RectTransform logoRect = logoObj.GetComponent<RectTransform>();
            logoRect.anchorMin = new Vector2(0.5f, 1f);
            logoRect.anchorMax = new Vector2(0.5f, 1f);
            logoRect.pivot = new Vector2(0.5f, 1f);
            logoRect.anchoredPosition = new Vector2(0f, -120f); // Logo top 120px from container top
            logoRect.sizeDelta = new Vector2(499f, 328f);
            
            // Created by NoXeKeeper — under logo, centered
            GameObject creatorObj = new GameObject("Creator_XUAIGNORE");
            creatorObj.transform.SetParent(container.transform, false);
            UnityEngine.UI.Text creatorText = creatorObj.AddComponent<UnityEngine.UI.Text>();
            creatorText.text = "Created by NoXeKeeper";
            creatorText.fontSize = 22;
            creatorText.alignment = TextAnchor.MiddleCenter;
            creatorText.fontStyle = FontStyle.Bold;
            creatorText.color = new Color(0.9f, 0.9f, 0.9f);
            creatorText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            RectTransform creatorRect = creatorObj.GetComponent<RectTransform>();
            creatorRect.anchorMin = new Vector2(0.5f, 1f);
            creatorRect.anchorMax = new Vector2(0.5f, 1f);
            creatorRect.pivot = new Vector2(0.5f, 0.5f);
            creatorRect.anchoredPosition = new Vector2(0f, -465f); // Under logo (120+328+17)
            creatorRect.sizeDelta = new Vector2(400f, 35f);
            
            // Version 1.1 — under creator NoXeKeeper, centered
            GameObject versionObj = new GameObject("Version_XUAIGNORE");
            versionObj.transform.SetParent(container.transform, false);
            UnityEngine.UI.Text versionText = versionObj.AddComponent<UnityEngine.UI.Text>();
            versionText.text = "Version: 1.1";
            versionText.fontSize = 20;
            versionText.alignment = TextAnchor.MiddleCenter;
            versionText.color = new Color(0.9f, 0.9f, 0.9f);
            versionText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            RectTransform versionRect = versionObj.GetComponent<RectTransform>();
            versionRect.anchorMin = new Vector2(0.5f, 1f);
            versionRect.anchorMax = new Vector2(0.5f, 1f);
            versionRect.pivot = new Vector2(0.5f, 0.5f);
            versionRect.anchoredPosition = new Vector2(0f, -510f); // Under creator (465+35+10)
            versionRect.sizeDelta = new Vector2(120f, 30f);
        }
        else
        {
            // Fallback: text if logo PNG not found
            GameObject modTitleObj = new GameObject("ModTitle_XUAIGNORE");
            modTitleObj.transform.SetParent(container.transform, false);
            var modTitleText = modTitleObj.AddComponent<UnityEngine.UI.Text>();
            modTitleText.text = data.modTitle;
            modTitleText.fontSize = 48;
            modTitleText.alignment = TextAnchor.MiddleCenter;
            modTitleText.fontStyle = FontStyle.Bold;
            modTitleText.color = new Color(1f, 0.5f, 0f);
            modTitleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            RectTransform modTitleRect = modTitleObj.GetComponent<RectTransform>();
            modTitleRect.anchorMin = new Vector2(0.5f, 1f);
            modTitleRect.anchorMax = new Vector2(0.5f, 1f);
            modTitleRect.pivot = new Vector2(0.5f, 1f);
            modTitleRect.anchoredPosition = new Vector2(0f, -180f);
            modTitleRect.sizeDelta = new Vector2(600f, 60f);

            GameObject titleObj = new GameObject("Title_XUAIGNORE");
            titleObj.transform.SetParent(container.transform, false);
            _titleText = titleObj.AddComponent<UnityEngine.UI.Text>();
            _titleText.text = data.title;
            _titleText.fontSize = 36;
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.fontStyle = FontStyle.Bold;
            _titleText.color = Color.white;
            _titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -250f);
            titleRect.sizeDelta = new Vector2(800f, 50f);
            
            // Version under title, right side - XUAIGNORE (fallback when no logo)
            GameObject versionObj2 = new GameObject("Version_XUAIGNORE");
            versionObj2.transform.SetParent(container.transform, false);
            UnityEngine.UI.Text versionText2 = versionObj2.AddComponent<UnityEngine.UI.Text>();
            versionText2.text = "Version: 1.1";
            versionText2.fontSize = 20;
            versionText2.alignment = TextAnchor.MiddleRight;
            versionText2.color = new Color(0.9f, 0.9f, 0.9f);
            versionText2.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            RectTransform versionRect2 = versionObj2.GetComponent<RectTransform>();
            versionRect2.anchorMin = new Vector2(1f, 1f);
            versionRect2.anchorMax = new Vector2(1f, 1f);
            versionRect2.pivot = new Vector2(1f, 1f);
            versionRect2.anchoredPosition = new Vector2(-60f, -310f);
            versionRect2.sizeDelta = new Vector2(120f, 30f);
        }
        
        // DEMO (XUAIGNORE - exclude from AutoTranslator) - only if text not empty
        if (!string.IsNullOrEmpty(data.demo))
        {
            float demoY = _logoSprite != null ? -470f : -330f; // Below logo (120+328) when logo shown
            GameObject demoObj = new GameObject("Demo_XUAIGNORE");
            demoObj.transform.SetParent(container.transform, false);
            _demoText = demoObj.AddComponent<UnityEngine.UI.Text>();
            _demoText.text = data.demo;
            _demoText.fontSize = 36;
            _demoText.alignment = TextAnchor.MiddleCenter;
            _demoText.fontStyle = FontStyle.Bold;
            _demoText.color = new Color(1f, 0.8f, 0f);
            _demoText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            RectTransform demoRect = demoObj.GetComponent<RectTransform>();
            demoRect.anchorMin = new Vector2(0.5f, 1f);
            demoRect.anchorMax = new Vector2(0.5f, 1f);
            demoRect.pivot = new Vector2(0.5f, 1f);
            demoRect.anchoredPosition = new Vector2(0f, demoY);
            demoRect.sizeDelta = new Vector2(300f, 50f);
        }
        
        // Warning (XUAIGNORE - exclude from AutoTranslator)
        GameObject warningObj = new GameObject("Warning_XUAIGNORE");
        warningObj.transform.SetParent(container.transform, false);
        _warningText = warningObj.AddComponent<UnityEngine.UI.Text>();
        _warningText.text = data.warning;
        _warningText.fontSize = 24;
        _warningText.alignment = TextAnchor.MiddleCenter;
        _warningText.fontStyle = FontStyle.Bold;
        _warningText.color = new Color(1f, 0.3f, 0.3f); // Red for warning
        _warningText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _warningText.horizontalOverflow = HorizontalWrapMode.Wrap;

        // Add Outline for better visibility
        UnityEngine.UI.Outline warningOutline = warningObj.GetComponent<UnityEngine.UI.Outline>();
        if (warningOutline == null)
        {
            warningOutline = warningObj.AddComponent<UnityEngine.UI.Outline>();
        }
        warningOutline.effectColor = new Color(0f, 0f, 0f, 1f); // Black outline
        warningOutline.effectDistance = new Vector2(1f, -1f);
        
        RectTransform warningRect = warningObj.GetComponent<RectTransform>();
        warningRect.anchorMin = new Vector2(0.5f, 0.5f);
        warningRect.anchorMax = new Vector2(0.5f, 0.5f);
        warningRect.pivot = new Vector2(0.5f, 0.5f);
        warningRect.anchoredPosition = new Vector2(0f, -100f); // Raised 150px
        warningRect.sizeDelta = new Vector2(800f, 150f); // Reduce size warning, так as убрали дополнительный text

        // Additional text (XUAIGNORE - exclude from AutoTranslator)
        if (!string.IsNullOrEmpty(data.additionalText))
        {
            GameObject additionalObj = new GameObject("Additional_XUAIGNORE");
            additionalObj.transform.SetParent(container.transform, false);
            UnityEngine.UI.Text additionalText = additionalObj.AddComponent<UnityEngine.UI.Text>();
            additionalText.text = data.additionalText;
            additionalText.fontSize = 20;
            additionalText.alignment = TextAnchor.MiddleCenter;
            additionalText.fontStyle = FontStyle.Italic;
            additionalText.color = new Color(1f, 0.8f, 0.2f); // Soft orange for дополнительного text
            additionalText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            additionalText.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Add Outline for better visibility
            UnityEngine.UI.Outline additionalOutline = additionalObj.AddComponent<UnityEngine.UI.Outline>();
            additionalOutline.effectColor = new Color(0f, 0f, 0f, 1f);
            additionalOutline.effectDistance = new Vector2(1f, -1f);

            RectTransform additionalRect = additionalObj.GetComponent<RectTransform>();
            additionalRect.anchorMin = new Vector2(0.5f, 0.5f);
            additionalRect.anchorMax = new Vector2(0.5f, 0.5f);
            additionalRect.pivot = new Vector2(0.5f, 0.5f);
            additionalRect.anchoredPosition = new Vector2(0f, -180f); // Below warning (moved with warning)
            additionalRect.sizeDelta = new Vector2(800f, 60f);
        }

        // Credits (left) - XUAIGNORE. Header+Creator centered, list left, descriptions smaller
        Color goldColor = new Color(1f, 0.84f, 0f);
        Color whiteColor = new Color(0.9f, 0.9f, 0.9f);
        Font arial = Resources.GetBuiltinResource<Font>("Arial.ttf");
        
        GameObject creditsObj = new GameObject("Credits_XUAIGNORE");
        creditsObj.transform.SetParent(container.transform, false);
        Image credBg = creditsObj.AddComponent<Image>();
        credBg.color = new Color(0f, 0f, 0f, 0f);
        credBg.raycastTarget = false;
        RectTransform creditsRect = creditsObj.GetComponent<RectTransform>();
        creditsRect.anchorMin = new Vector2(0f, 1f);
        creditsRect.anchorMax = new Vector2(0f, 1f);
        creditsRect.pivot = new Vector2(0f, 1f);
        creditsRect.anchoredPosition = new Vector2(80f, -200f); // 100px higher
        creditsRect.sizeDelta = new Vector2(480f, 550f);

        AddCenteredText(creditsObj, "CREDITS", 28, goldColor, arial, 0f);
        AddLeftText(creditsObj, "<b><size=22>Asome10121</size></b>\n   <size=16>─ NorEroMod rebalance & coding</size>\n<b><size=22>Nephilim50</size></b>\n   <size=16>─ Wolf mod integration</size>\n<b><size=22>Rick yeltsA</size></b>\n   <size=16>─ AI image generation</size>\n<b><size=22>0??</size></b>\n   <size=16>─ AI image generation</size>\n<b><size=22>Queen's Blade</size></b>\n   <size=16>─ Bad End scenario author</size>", arial, -45f, 480f);

        // Thanks (right) - headers centered ALL CAPS, list left - XUAIGNORE
        GameObject thanksObj = new GameObject("Thanks_XUAIGNORE");
        thanksObj.transform.SetParent(container.transform, false);
        Image thanksBg = thanksObj.AddComponent<Image>();
        thanksBg.color = new Color(0f, 0f, 0f, 0f);
        thanksBg.raycastTarget = false;
        RectTransform thanksRect = thanksObj.GetComponent<RectTransform>();
        thanksRect.anchorMin = new Vector2(1f, 1f);
        thanksRect.anchorMax = new Vector2(1f, 1f);
        thanksRect.pivot = new Vector2(1f, 1f);
        thanksRect.anchoredPosition = new Vector2(-80f, -200f); // 100px higher
        thanksRect.sizeDelta = new Vector2(480f, 720f);

        AddCenteredText(thanksObj, "THANKS", 28, goldColor, arial, 0f);
        AddCenteredText(thanksObj, "You inspired HellGate", 20, whiteColor, arial, -40f);
        AddLeftText(thanksObj, "<b><size=22>boned</size></b>\n   <size=16>─ Bone Mod (first grab system)</size>\n<b><size=22>BGTBBB</size></b>\n   <size=16>─ NorEroMod (essential base)</size>\n<b><size=22>HellaChaz</size></b>\n   <size=16>─ NorEroMod fork & improvements</size>", arial, -75f, 160f);
        AddCenteredText(thanksObj, "SPECIAL THANKS", 28, goldColor, arial, -255f);
        AddLeftText(thanksObj, "<b><size=22>D-lis</size></b>\n   <size=16>─ Creator of Night of Revenge</size>\n<b><size=22>Krongorka</size></b>\n   <size=16>─ Huge contribution to NoR modding</size>\n<b><size=22>Mnonyhc</size></b>\n   <size=16>─ Creator & builder of the NoR Community</size>", arial, -300f, 220f);


        // Information (XUAIGNORE - exclude from AutoTranslator)
        // Remove links to Discord and Ko-fi from text, keep only version
        GameObject infoObj = new GameObject("Info_XUAIGNORE");
        infoObj.transform.SetParent(container.transform, false);
        _infoText = infoObj.AddComponent<UnityEngine.UI.Text>();
        
        // Remove lines with Ko-fi and Discord from info text
        string infoText = data.info;
        string[] lines = infoText.Split('\n');
        List<string> filteredLines = new List<string>();
        foreach (string line in lines)
        {
            // Skip lines, containing Ko-fi, Discord or "Поддержка разработки"/"Support Development"
            // Check if line not empty (for old versions .NET)
            bool isEmpty = string.IsNullOrEmpty(line) || line.Trim().Length == 0;
            if (!line.Contains("Ko-fi") && 
                !line.Contains("Discord") && 
                !line.Contains("discord.gg") && 
                !line.Contains("ko-fi.com") &&
                !line.Contains("Поддержка разработки") &&
                !line.Contains("Support Development") &&
                !line.Trim().StartsWith("Version") &&
                !isEmpty)
            {
                filteredLines.Add(line);
            }
        }
        _infoText.text = string.Join("\n", filteredLines.ToArray());
        
        _infoText.fontSize = 18;
        _infoText.alignment = TextAnchor.MiddleCenter;
        _infoText.color = new Color(0.8f, 0.8f, 0.8f);
        _infoText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
        
        RectTransform infoRect = infoObj.GetComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0.5f, 0f);
        infoRect.anchorMax = new Vector2(0.5f, 0f);
        infoRect.pivot = new Vector2(0.5f, 0f);
        infoRect.anchoredPosition = new Vector2(0f, 220f);
        infoRect.sizeDelta = new Vector2(800f, 170f);
        
        // Buttons: NoR + HellGate left, Ko-fi right (aligned with Thanks column)
        float btnY = 220f;
        float btnGap = 15f;
        float norW = _spriteDiscordNoR != null ? _spriteDiscordNoR.rect.width : 200f;
        float norH = _spriteDiscordNoR != null ? _spriteDiscordNoR.rect.height : 60f;
        float hgW = _spriteDiscordHellGate != null ? _spriteDiscordHellGate.rect.width : 200f;
        float hgH = _spriteDiscordHellGate != null ? _spriteDiscordHellGate.rect.height : 60f;
        float kofiW = _spriteKoFi != null ? _spriteKoFi.rect.width : 200f;
        float kofiH = _spriteKoFi != null ? _spriteKoFi.rect.height : 60f;
        
        // 1. NoR Community - left edge
        float norX = 80f + norW / 2f;
        GameObject norBtn = CreateImageButton(container, "NoRCommunityButton_XUAIGNORE", _spriteDiscordNoR, "NoR Community", norX, btnY, norW, norH, () => Application.OpenURL("https://discord.gg/VbepPeDUWE"));
        RectTransform norRect = norBtn.GetComponent<RectTransform>();
        norRect.anchorMin = new Vector2(0f, 0f);
        norRect.anchorMax = new Vector2(0f, 0f);
        norRect.pivot = new Vector2(0.5f, 0f);
        norRect.anchoredPosition = new Vector2(norX, btnY);
        
        // 2. HellGate Support - right of NoR (4px lower than NoR)
        float hgX = 80f + norW + btnGap + hgW / 2f;
        float hgBtnY = btnY - 4f;
        GameObject hgBtn = CreateImageButton(container, "HellGateSupportButton_XUAIGNORE", _spriteDiscordHellGate, "HellGate Support", hgX, hgBtnY, hgW, hgH, () => Application.OpenURL("https://discord.gg/eZ8qmUDMT3"));
        RectTransform hgRect = hgBtn.GetComponent<RectTransform>();
        hgRect.anchorMin = new Vector2(0f, 0f);
        hgRect.anchorMax = new Vector2(0f, 0f);
        hgRect.pivot = new Vector2(0.5f, 0f);
        hgRect.anchoredPosition = new Vector2(hgX, hgBtnY);
        
        // Tagline between Discord buttons, 25px below - XUAIGNORE
        float taglineX = 80f + norW + btnGap / 2f; // Center of gap between NoR and HellGate
        float taglineY = 220f - 25f - 50f; // 25px below buttons
        GameObject taglineObj = new GameObject("Tagline_XUAIGNORE");
        taglineObj.transform.SetParent(container.transform, false);
        UnityEngine.UI.Text taglineText = taglineObj.AddComponent<UnityEngine.UI.Text>();
        taglineText.text = "HellGate — Made for the NoR Community, with Community Support";
        taglineText.fontSize = 24;
        taglineText.fontStyle = FontStyle.Bold;
        taglineText.alignment = TextAnchor.MiddleCenter;
        taglineText.color = new Color(0.9f, 0.85f, 0.7f);
        taglineText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        taglineText.horizontalOverflow = HorizontalWrapMode.Wrap;
        RectTransform taglineRect = taglineObj.GetComponent<RectTransform>();
        taglineRect.anchorMin = new Vector2(0f, 0f);
        taglineRect.anchorMax = new Vector2(0f, 0f);
        taglineRect.pivot = new Vector2(0.5f, 0f);
        taglineRect.anchoredPosition = new Vector2(taglineX, taglineY);
        taglineRect.sizeDelta = new Vector2(520f, 60f);
        
        // 3. Ko-fi - center of right column (Thanks), 100px higher than before
        float kofiY = btnY - 100f; // 120 — moved up 100px
        float kofiX = -80f - 240f; // -320 — center of Thanks column (width 480)
        GameObject kofiBtn = CreateImageButton(container, "KoFiButton_XUAIGNORE", _spriteKoFi, "Ko-fi", kofiX, kofiY, kofiW, kofiH, () => Application.OpenURL("https://ko-fi.com/noxeunitydev"));
        RectTransform kofiRect = kofiBtn.GetComponent<RectTransform>();
        kofiRect.anchorMin = new Vector2(1f, 0f);
        kofiRect.anchorMax = new Vector2(1f, 0f);
        kofiRect.pivot = new Vector2(0.5f, 0f);
        kofiRect.anchoredPosition = new Vector2(kofiX, kofiY);
        
        // Extra thanks ~50px above Ko-fi, centered in right column - XUAIGNORE
        float extraThanksY = kofiY + kofiH + 50f;
        float extraThanksX = kofiX;
        GameObject extraThanksObj = new GameObject("ExtraThanks_XUAIGNORE");
        extraThanksObj.transform.SetParent(container.transform, false);
        UnityEngine.UI.Text extraThanksText = extraThanksObj.AddComponent<UnityEngine.UI.Text>();
        extraThanksText.text = "To my wonderful monthly supporters — your support means everything.";
        extraThanksText.fontSize = 24;
        extraThanksText.fontStyle = FontStyle.Italic;
        extraThanksText.alignment = TextAnchor.MiddleCenter;
        extraThanksText.color = new Color(0.95f, 0.9f, 0.75f);
        extraThanksText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        extraThanksText.horizontalOverflow = HorizontalWrapMode.Wrap;
        RectTransform extraThanksRect = extraThanksObj.GetComponent<RectTransform>();
        extraThanksRect.anchorMin = new Vector2(1f, 0f);
        extraThanksRect.anchorMax = new Vector2(1f, 0f);
        extraThanksRect.pivot = new Vector2(0.5f, 0f);
        extraThanksRect.anchoredPosition = new Vector2(extraThanksX, extraThanksY);
        extraThanksRect.sizeDelta = new Vector2(480f, 55f);
        
        // Start button - PNG (382x198) or fallback text
        GameObject buttonObj = new GameObject("StartButton_XUAIGNORE");
        buttonObj.transform.SetParent(container.transform, false);
        _startButton = buttonObj.AddComponent<Button>();
        Image buttonImage = buttonObj.AddComponent<Image>();
        if (_spriteStartButton != null)
        {
            buttonImage.sprite = _spriteStartButton;
            buttonImage.preserveAspect = false;
            buttonImage.color = Color.white;
        }
        else
        {
            buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            GameObject buttonTextObj = new GameObject("ButtonText_XUAIGNORE");
            buttonTextObj.transform.SetParent(buttonObj.transform, false);
            UnityEngine.UI.Text buttonText = buttonTextObj.AddComponent<UnityEngine.UI.Text>();
            buttonText.text = data.startButton;
            buttonText.fontSize = 32;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.fontStyle = FontStyle.Bold;
            buttonText.color = Color.white;
            buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.sizeDelta = Vector2.zero;
        }
        _startButton.onClick.AddListener(HideSplashScreen);
        
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 100f);
        buttonRect.sizeDelta = _spriteStartButton != null ? new Vector2(330f, 171f) : new Vector2(300f, 60f);
        
        // Hover effect for кнопки Start (scale on hover)
        UnityEngine.EventSystems.EventTrigger startTrigger = buttonObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        
        UnityEngine.EventSystems.EventTrigger.Entry startEnterEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
        startEnterEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        startEnterEntry.callback.AddListener((eventData) => {
            // Scale button on hover
            buttonRect.localScale = new Vector3(1.15f, 1.15f, 1f);
        });
        startTrigger.triggers.Add(startEnterEntry);
        
        UnityEngine.EventSystems.EventTrigger.Entry startExitEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
        startExitEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        startExitEntry.callback.AddListener((eventData) => {
            // Restore normal size
            buttonRect.localScale = Vector3.one;
        });
        startTrigger.triggers.Add(startExitEntry);
        
        // Fade in
        if (_coroutineRunner != null)
        {
            _coroutineRunner.StartCoroutine(FadeInMainSplash());
        }
    }
    
    /// <summary>
    /// Fade in for основного splash screen
    /// </summary>
    private static IEnumerator FadeInMainSplash()
    {
        if (_mainSplashScreen == null) yield break;
        
        CanvasGroup group = _mainSplashScreen.GetComponent<CanvasGroup>();
        if (group == null) group = _mainSplashScreen.AddComponent<CanvasGroup>();
        
        group.alpha = 0f;
        float elapsed = 0f;
        
        while (elapsed < FADE_DURATION)
        {
            elapsed += Time.deltaTime;
            group.alpha = elapsed / FADE_DURATION;
            yield return null;
        }
        
        group.alpha = 1f;
    }
    
    /// <summary>
    /// Hide splash screen
    /// </summary>
    private static void HideSplashScreen()
    {
        if (_coroutineRunner != null)
        {
            _coroutineRunner.StartCoroutine(FadeOutAndHide());
        }
    }
    
    /// <summary>
    /// Fade out and hide
    /// </summary>
    private static IEnumerator FadeOutAndHide()
    {
        if (_mainSplashScreen != null)
        {
            CanvasGroup group = _mainSplashScreen.GetComponent<CanvasGroup>();
            if (group == null) group = _mainSplashScreen.AddComponent<CanvasGroup>();
            
            float elapsed = 0f;
            while (elapsed < FADE_DURATION)
            {
                elapsed += Time.deltaTime;
                group.alpha = 1f - (elapsed / FADE_DURATION);
                yield return null;
            }
        }
        
        if (_canvas != null)
        {
            _canvas.SetActive(false);
        }
    }
    
    /// <summary>
    /// Scene load handler сцены
    /// </summary>
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!string.IsNullOrEmpty(scene.name) && scene.name != "Gametitle")
        {
            HideSplashScreen();
        }
    }
    
    /// <summary>
    /// Helper class for корутин
    /// </summary>
    private class SplashScreenRunner : MonoBehaviour
    {
    }
    
    /// <summary>
    /// Create default data for language selection
    /// </summary>
    private static LanguageSelectionData CreateDefaultLanguageData()
    {
        var data = new LanguageSelectionData();
        data.selectLanguage = "Select Language:";
        data.languages = new Dictionary<string, LanguageInfo>
        {
            { "RU", new LanguageInfo { name = "Русский", flag = "" } },
            { "EN", new LanguageInfo { name = "English", flag = "" } },
            { "JP", new LanguageInfo { name = "日本語", flag = "" } },
            { "CN", new LanguageInfo { name = "中文", flag = "" } },
            { "KR", new LanguageInfo { name = "한국어", flag = "" } },
            { "FR", new LanguageInfo { name = "Français", flag = "" } },
            { "DE", new LanguageInfo { name = "Deutsch", flag = "" } },
            { "PT", new LanguageInfo { name = "Português", flag = "" } },
            { "BR", new LanguageInfo { name = "Português (Brasil)", flag = "" } },
            { "ES", new LanguageInfo { name = "Español", flag = "" } }
        };
        return data;
    }
    
    /// <summary>
    /// Create default data for splash screen
    /// </summary>
    private static SplashScreenData CreateDefaultSplashData(string lang)
    {
        var data = new SplashScreenData();
        data.modTitle = "HELL GATE";
        data.title = "HELLGATE";
        data.demo = "";
        data.warning = "⚠️ ADULT CONTENT WARNING\n\nThis mod contains explicit adult content.\n18+ only. Viewer discretion advised.";
        data.additionalText = "Join our Discord server for updates and support!\nConsider supporting development on Ko-fi.";
        data.info = "Version: 1.0.0 (Demo)\n\nSupport Development:\nKo-fi: ko-fi.com/noxeunitydev\nDiscord: discord.gg/RRrUPUQa";
        
        // Default texts кнопок for разных языков
        var buttonTexts = new Dictionary<string, string>
        {
            { "RU", "НАЧАТЬ" },
            { "EN", "START" },
            { "JP", "開始" },
            { "CN", "开始" },
            { "KR", "시작" },
            { "FR", "COMMENCER" },
            { "DE", "STARTEN" },
            { "PT", "COMEÇAR" },
            { "BR", "COMEÇAR" },
            { "ES", "EMPEZAR" }
        };
        
        data.startButton = buttonTexts.TryGetValue(lang, out string? buttonText) ? buttonText : "START";
        return data;
    }
    
    // Data classes
    private class LanguageSelectionData
    {
        public string selectLanguage = "Select Language:";
        public Dictionary<string, LanguageInfo> languages = new();
    }
    
    private class LanguageInfo
    {
        public string name = "";
        public string flag = "";
    }
    
    private class SplashScreenData
    {
        public string modTitle = "HELL GATE";
        public string title = "HELLGATE";
        public string demo = "";
        public string warning = "";
        public string additionalText = "";
        public string info = "";
        public string startButton = "START";
    }
}
