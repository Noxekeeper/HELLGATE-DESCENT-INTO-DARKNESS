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
    private static GameObject? _mainSplashBackground;
    private static MonoBehaviour? _coroutineRunner;
    private static bool _isInitialized = false;
    private static bool _titleMenuBackdropActive = false;
    private static Sprite? _whiteSprite; // Static sprite for background
    private static Dictionary<string, Sprite> _flagSprites = new(); // Flag cache
    private static Sprite? _logoSprite; // HELLGATE logo PNG (disclaimer)
    private static Sprite? _menuArtSprite; // MainHellGate.png (title menu)
    private static GameObject? _titleMenuArtObject;
    private static Sprite? _spriteDiscordNoR;   // Discord NoR Community button
    private static Sprite? _spriteDiscordHellGate; // Discord HellGate Support button
    private static Sprite? _spriteKoFi;         // Ko-fi button
    private static Sprite? _spritePatreon;    // Patreon button
    private static Sprite? _spriteStartButton;  // Start button PNG
    private const float DonationButtonRowHeight = 105.6f;
    private const string PatreonSupportUrl =
        "https://patreon.com/NoxeKeeper?utm_medium=unknown&utm_source=join_link&utm_campaign=creatorshare_creator&utm_content=copyLink";
    
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
    private static GameObject? _splashLoadingRoot;
    private static Image? _splashLoadingFill;
    private static UnityEngine.UI.Text? _splashLoadingLabel;
    private static GameObject? _splashInputBlocker;
    private static bool _splashInputReady;

    private const float FADE_DURATION = 0.5f;
    private const float SplashLoadMinSeconds = 10f;
    /// <summary>Bottom offset for START / loading stack (container-local, anchor bottom). Was 100; -80px clears warning block.</summary>
    private const float SplashStartButtonBottomY = 20f;
    private const float SplashLoadingGapAboveStart = 12f;
    private const int TITLE_MENU_CANVAS_SORT_ORDER = -50;
    private const string SplashDisplayVersion = "1.2.4";
    /// <summary>ASCII separator for credit lines (built-in Arial cannot render UTF-8 mojibake em dashes).</summary>
    private const string CreditLineSep = " - ";

    internal static bool IsTitleMenuBackdropActive => _titleMenuBackdropActive;
    
    /// <summary>
    /// Initialize splash screen
    /// </summary>
    public static void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }
        
        try
        {
            
            // Load data from JSON
            LoadLanguageSelectionData();
            LoadSplashScreenData();
            LoadFlagSprites(); // Load flags
            LoadLogoSprite(); // HELLGATE logo (replaces title text)
            LoadMenuArtSprite(); // MainHellGate.png for title menu backdrop
            LoadButtonSprites(); // Discord & Ko-fi button images
            
            // Create Canvas
            CreateCanvas();
            
            // Check if language selected
            string selectedLanguage = "";
            if (Plugin.hellGateLanguage != null)
            {
                selectedLanguage = Plugin.hellGateLanguage.Value ?? "";
            }
            
            
            if (string.IsNullOrEmpty(selectedLanguage))
            {
                // Show screen language selection
                ShowLanguageSelection();
            }
            else
            {
                // Show main splash screen
                ShowMainSplash(selectedLanguage);
            }
            
            _isInitialized = true;
            
            HellGateTitleMenuBackdrop.Initialize();

            // Subscribe to scene load
            SceneManager.sceneLoaded += OnSceneLoaded;
            
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
            string jsonPath = Path.Combine(dataPath, "LanguageSelectionData.json");
            
            
            if (File.Exists(jsonPath))
            {
                string jsonContent = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8); // Explicitly specify UTF-8 for Unicode
                Plugin.Log?.LogInfo($"[HellGate Splash] JSON content length: {jsonContent.Length}");
                
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
                if (Directory.Exists(dataPath))
                {
                }
                // Create default data
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
                }
                else
                {
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
                }
            }
        }
        
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
    /// Built-in Arial cannot render emoji; strip warning glyphs that appear as tofu/krakozyabra.
    /// </summary>
    private static string SanitizeSplashWarning(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return text
            .Replace("\uFE0F", string.Empty)
            .Replace("\u26A0\uFE0F", string.Empty)
            .Replace("\u26A0", string.Empty)
            .TrimStart();
    }

    /// <summary>
    /// Unity UI Text + Outline can clip the first glyph on wrapped lines (e.g. FR "Réservé" → "éservé").
    /// Pad each non-empty line so the opening character stays inside the rect.
    /// </summary>
    private static string FormatSplashWarningForDisplay(string text)
    {
        text = SanitizeSplashWarning(text);
        if (string.IsNullOrEmpty(text))
            return text;

        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrEmpty(lines[i]))
                lines[i] = "\u2009" + lines[i];
        }

        return string.Join("\n", lines);
    }

    private static bool IsVersionInfoLine(string line)
    {
        if (string.IsNullOrEmpty(line) || line.Trim().Length == 0)
            return false;

        string trimmed = line.Trim();
        return trimmed.StartsWith("Version", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Versión", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Versão", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Версия", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("版本", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("バージョン", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("버전", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsIgnoreCase(string text, string value)
    {
        return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsSupportDevelopmentLine(string line)
    {
        if (string.IsNullOrEmpty(line) || line.Trim().Length == 0)
            return false;

        string trimmed = line.Trim();
        return ContainsIgnoreCase(trimmed, "Support Development")
            || ContainsIgnoreCase(trimmed, "Поддержка разработки")
            || ContainsIgnoreCase(trimmed, "Entwicklung unterstützen")
            || ContainsIgnoreCase(trimmed, "Soutien au développement")
            || ContainsIgnoreCase(trimmed, "Soutien au developpement")
            || ContainsIgnoreCase(trimmed, "Apoya el desarrollo")
            || ContainsIgnoreCase(trimmed, "Apoie o desenvolvimento")
            || ContainsIgnoreCase(trimmed, "Apoiar o desenvolvimento")
            || trimmed.IndexOf("支持开发", StringComparison.Ordinal) >= 0
            || trimmed.IndexOf("開発サポート", StringComparison.Ordinal) >= 0
            || trimmed.IndexOf("개발 지원", StringComparison.Ordinal) >= 0;
    }

    private static string BuildFilteredInfoText(string infoText)
    {
        if (string.IsNullOrEmpty(infoText))
            return string.Empty;

        string[] lines = infoText.Split('\n');
        List<string> filteredLines = new List<string>();
        foreach (string line in lines)
        {
            bool isEmpty = string.IsNullOrEmpty(line) || line.Trim().Length == 0;
            if (!line.Contains("Ko-fi")
                && !line.Contains("Discord")
                && !line.Contains("discord.gg")
                && !line.Contains("ko-fi.com")
                && !IsSupportDevelopmentLine(line)
                && !IsVersionInfoLine(line)
                && !isEmpty)
            {
                filteredLines.Add(line);
            }
        }

        return string.Join("\n", filteredLines.ToArray()).Trim();
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
            
            
            if (Directory.Exists(hellGateJson))
            {
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
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogError($"[HellGate Splash] Failed to load flag {kvp.Value}: {ex.Message}");
                }
            }
            else
            {
            }
        }
        
    }
    
    private const string LOGO_FILENAME = "HELLGATELOGO.png";
    private const string MENU_ART_FILENAME = "MainHellGate.png";
    
    private static Sprite? LoadSpriteFromSplashScreen(string fileName)
    {
        string basePath = GetHellGateSourcesPath();
        if (string.IsNullOrEmpty(basePath))
            return null;

        string dir = Path.Combine(basePath, "SplashScreen");
        if (!Directory.Exists(dir))
            return null;

        string filePath = Path.Combine(dir, fileName);
        if (!File.Exists(filePath))
        {
            try
            {
                foreach (string candidate in Directory.GetFiles(dir, "*.png"))
                {
                    if (string.Equals(Path.GetFileName(candidate), fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        filePath = candidate;
                        break;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        if (!File.Exists(filePath))
            return null;

        try
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(fileData))
            {
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[HellGate Splash] Failed to load {fileName}: {ex.Message}");
        }

        return null;
    }
    
    /// <summary>
    /// Load HELLGATE logo PNG from SplashScreen folder (replaces mod title + title text)
    /// </summary>
    private static void LoadLogoSprite()
    {
        _logoSprite = LoadSpriteFromSplashScreen(LOGO_FILENAME);
    }

    /// <summary>
    /// Load MainHellGate.png — custom art shown on Gametitle after disclaimer START.
    /// </summary>
    private static void LoadMenuArtSprite()
    {
        _menuArtSprite = LoadSpriteFromSplashScreen(MENU_ART_FILENAME);
        if (_menuArtSprite != null)
        {
            Plugin.Log?.LogInfo(
                "[HellGate Splash] Menu art loaded: "
                + MENU_ART_FILENAME
                + " ("
                + _menuArtSprite.texture.width
                + "x"
                + _menuArtSprite.texture.height
                + ")");
        }
        else
        {
            string dir = Path.Combine(GetHellGateSourcesPath() ?? string.Empty, "SplashScreen");
            Plugin.Log?.LogWarning(
                "[HellGate Splash] Menu art missing: "
                + MENU_ART_FILENAME
                + " expected in "
                + dir);
        }
    }

    private static void VerifyParsedLanguageLabel(string code, string expectedName)
    {
        if (_languageData?.languages == null || !_languageData.languages.TryGetValue(code, out LanguageInfo info))
        {
            Plugin.Log?.LogWarning("[HellGate Splash] Language '" + code + "' missing in parsed JSON.");
            return;
        }

        if (string.Equals(info.name, expectedName, StringComparison.Ordinal))
            Plugin.Log?.LogInfo("[HellGate Splash] UTF-8 OK: " + code + "='" + info.name + "'");
        else
            Plugin.Log?.LogWarning("[HellGate Splash] UTF-8 mismatch for " + code + ": got '" + info.name + "', expected '" + expectedName + "'");
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
            string p5 = Path.Combine(dir, "Patreon.png");
            try
            {
                if (File.Exists(p1)) { byte[] b = File.ReadAllBytes(p1); var t = new Texture2D(2, 2); if (t.LoadImage(b)) _spriteDiscordNoR = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f); }
                if (File.Exists(p2)) { byte[] b = File.ReadAllBytes(p2); var t = new Texture2D(2, 2); if (t.LoadImage(b)) _spriteDiscordHellGate = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f); }
                if (File.Exists(p3)) { byte[] b = File.ReadAllBytes(p3); var t = new Texture2D(2, 2); if (t.LoadImage(b)) _spriteKoFi = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f); }
                if (File.Exists(p4)) { byte[] b = File.ReadAllBytes(p4); var t = new Texture2D(2, 2); if (t.LoadImage(b)) _spriteStartButton = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f); }
                if (File.Exists(p5)) { byte[] b = File.ReadAllBytes(p5); var t = new Texture2D(2, 2); if (t.LoadImage(b)) _spritePatreon = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f); }
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

    private static string ColorToHex(Color color)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
        int g = Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
        int b = Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
        return r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
    }

    private static Vector2 GetScaledButtonSize(Sprite? sprite, float targetHeight, float fallbackW, float fallbackH)
    {
        if (sprite == null || sprite.rect.height <= 0f)
            return new Vector2(fallbackW, fallbackH);

        float aspect = sprite.rect.width / sprite.rect.height;
        return new Vector2(targetHeight * aspect, targetHeight);
    }

    /// <summary>
    /// Create image button. Size from PNG sprite (no phantom area). Falls back to text if sprite is null.
    /// </summary>
    private static GameObject CreateImageButton(GameObject parent, string name, Sprite sprite, string fallbackText, float x, float y, float fallbackW, float fallbackH, Action onClick, float displayW = 0f, float displayH = 0f)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent.transform, false);
        Button btn = obj.AddComponent<Button>();
        Image img = obj.AddComponent<Image>();
        float w, h;
        bool preserveAspect = false;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.color = Color.white;
            if (displayW > 0f && displayH > 0f)
            {
                w = displayW;
                h = displayH;
                preserveAspect = true;
            }
            else
            {
                w = sprite.rect.width;
                h = sprite.rect.height;
            }
            img.preserveAspect = preserveAspect;
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
            txt.font = HellGateFontProvider.GetUiFont();
            RectTransform tr = txtObj.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.sizeDelta = Vector2.zero;
            w = displayW > 0f ? displayW : fallbackW;
            h = displayH > 0f ? displayH : fallbackH;
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
        _selectLanguageText.font = HellGateFontProvider.GetUiFont();
        
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
        
        
        foreach (var lang in _languageData.languages)
        {
            
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
            
            
            index++;
        }
        
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

            // EventCore modal content (loads its language packs once at boot — must
            // re-read after the language changes, otherwise modals stay on the boot language).
            NoREroMod.Systems.EventCore.Content.EventCoreDefinitionRegistry.ReloadFromDisk();
            NoREroMod.Systems.EventCore.Content.EventCoreStringRegistry.ReloadFromDisk();
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
        _mainSplashBackground = background;
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
        
        string supportersHeader = SplashScreenUILabels.GetSupportersHeader(languageCode);

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
            creatorText.font = HellGateFontProvider.GetUiFont();
            RectTransform creatorRect = creatorObj.GetComponent<RectTransform>();
            creatorRect.anchorMin = new Vector2(0.5f, 1f);
            creatorRect.anchorMax = new Vector2(0.5f, 1f);
            creatorRect.pivot = new Vector2(0.5f, 0.5f);
            creatorRect.anchoredPosition = new Vector2(0f, -465f); // Under logo (120+328+17)
            creatorRect.sizeDelta = new Vector2(400f, 35f);
            
            // Version label under creator NoXeKeeper, centered
            GameObject versionObj = new GameObject("Version_XUAIGNORE");
            versionObj.transform.SetParent(container.transform, false);
            UnityEngine.UI.Text versionText = versionObj.AddComponent<UnityEngine.UI.Text>();
            versionText.text = $"Version: {SplashDisplayVersion}";
            versionText.fontSize = 20;
            versionText.alignment = TextAnchor.MiddleCenter;
            versionText.color = new Color(0.9f, 0.9f, 0.9f);
            versionText.font = HellGateFontProvider.GetUiFont();
            versionText.horizontalOverflow = HorizontalWrapMode.Overflow;
            versionText.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform versionRect = versionObj.GetComponent<RectTransform>();
            versionRect.anchorMin = new Vector2(0.5f, 1f);
            versionRect.anchorMax = new Vector2(0.5f, 1f);
            versionRect.pivot = new Vector2(0.5f, 0.5f);
            versionRect.anchoredPosition = new Vector2(0f, -510f); // Under creator (465+35+10)
            versionRect.sizeDelta = new Vector2(300f, 30f);
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
            modTitleText.font = HellGateFontProvider.GetUiFont();

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
            _titleText.font = HellGateFontProvider.GetUiFont();

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
            versionText2.text = $"Version: {SplashDisplayVersion}";
            versionText2.fontSize = 20;
            versionText2.alignment = TextAnchor.MiddleRight;
            versionText2.color = new Color(0.9f, 0.9f, 0.9f);
            versionText2.font = HellGateFontProvider.GetUiFont();
            versionText2.horizontalOverflow = HorizontalWrapMode.Overflow;
            versionText2.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform versionRect2 = versionObj2.GetComponent<RectTransform>();
            versionRect2.anchorMin = new Vector2(1f, 1f);
            versionRect2.anchorMax = new Vector2(1f, 1f);
            versionRect2.pivot = new Vector2(1f, 1f);
            versionRect2.anchoredPosition = new Vector2(-60f, -310f);
            versionRect2.sizeDelta = new Vector2(300f, 30f);
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
            _demoText.font = HellGateFontProvider.GetUiFont();

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
        _warningText.text = FormatSplashWarningForDisplay(data.warning);
        _warningText.fontSize = 24;
        _warningText.alignment = TextAnchor.MiddleCenter;
        _warningText.fontStyle = FontStyle.Bold;
        _warningText.color = new Color(1f, 0.3f, 0.3f); // Red for warning
        _warningText.font = HellGateFontProvider.GetUiFont();
        _warningText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _warningText.verticalOverflow = VerticalWrapMode.Overflow;

        // Add Outline for better visibility
        UnityEngine.UI.Outline warningOutline = warningObj.GetComponent<UnityEngine.UI.Outline>();
        if (warningOutline == null)
        {
            warningOutline = warningObj.AddComponent<UnityEngine.UI.Outline>();
        }
        warningOutline.effectColor = new Color(0f, 0f, 0f, 1f); // Black outline
        warningOutline.effectDistance = new Vector2(0.5f, -0.5f);
        
        RectTransform warningRect = warningObj.GetComponent<RectTransform>();
        warningRect.anchorMin = new Vector2(0.5f, 0.5f);
        warningRect.anchorMax = new Vector2(0.5f, 0.5f);
        warningRect.pivot = new Vector2(0.5f, 0.5f);
        warningRect.anchoredPosition = new Vector2(0f, -100f); // Raised 150px
        warningRect.sizeDelta = new Vector2(920f, 180f);

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
            additionalText.color = new Color(1f, 0.8f, 0.2f);
            additionalText.font = HellGateFontProvider.GetUiFont();
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
        Font arial = HellGateFontProvider.GetUiFont();
        
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
        creditsRect.sizeDelta = new Vector2(480f, 720f);

        AddCenteredText(creditsObj, "CREDITS", 28, goldColor, arial, 0f);
        AddLeftText(creditsObj,
            $"<b><size=22>Asome10121</size></b>\n   <size=16>{CreditLineSep}NorEroMod rebalance & coding</size>\n" +
            $"<b><size=22>Nephilim50</size></b>\n   <size=16>{CreditLineSep}Wolf mod integration</size>\n" +
            $"<b><size=22>Rick yeltsA</size></b>\n   <size=16>{CreditLineSep}AI image generation</size>\n" +
            $"<b><size=22>0??</size></b>\n   <size=16>{CreditLineSep}AI image generation</size>\n" +
            $"<b><size=22>Queen's Blade</size></b>\n   <size=16>{CreditLineSep}Bad End scenario author</size>\n" +
            $"<b><size=22>Sai</size></b>\n   <size=16>{CreditLineSep}Spine Animator</size>\n" +
            $"<b><size=22>Bandit</size></b>\n   <size=16>{CreditLineSep}Frame-by-Frame Animator</size>\n" +
            $"<b><size=22>TheAgilityMaster_750</size></b>\n   <size=16>{CreditLineSep}Lore Integrator</size>\n" +
            $"<b><size=22>Caliblri</size></b>\n   <size=16>{CreditLineSep}Writer</size>",
            arial, -45f, 650f);

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
        AddLeftText(thanksObj,
            $"<b><size=22>boned</size></b>\n   <size=16>{CreditLineSep}Bone Mod (first grab system)</size>\n" +
            $"<b><size=22>BGTBBB</size></b>\n   <size=16>{CreditLineSep}NorEroMod (essential base)</size>\n" +
            $"<b><size=22>HellaChaz</size></b>\n   <size=16>{CreditLineSep}NorEroMod fork & improvements</size>",
            arial, -75f, 160f);
        AddCenteredText(thanksObj, "SPECIAL THANKS", 28, goldColor, arial, -255f);
        AddLeftText(thanksObj,
            $"<b><size=22>D-LIS</size></b>\n   <size=16>{CreditLineSep}Creator of Night of Revenge</size>\n" +
            $"<b><size=22>Krongorka</size></b>\n   <size=16>{CreditLineSep}Huge contribution to NoR modding</size>\n" +
            $"<b><size=22>Mnonyhc</size></b>\n   <size=16>{CreditLineSep}Creator & builder of the NoR Community</size>",
            arial, -300f, 220f);

        // Supporters — in thanks column (~20px below SPECIAL THANKS list); header localized per language
        Color patronNameRedGold = new Color(0.93f, 0.38f, 0.14f, 1f);
        const float supportersHeaderY = -540f;
        const float supportersHeaderHeight = 80f;
        const float supportersNamesGapBelowHeader = 20f;
        float supportersNamesY = supportersHeaderY - supportersHeaderHeight - supportersNamesGapBelowHeader;
        GameObject supportersObj = new GameObject("SubscriberThanks_XUAIGNORE");
        supportersObj.transform.SetParent(thanksObj.transform, false);
        UnityEngine.UI.Text supportersHeaderText = supportersObj.AddComponent<UnityEngine.UI.Text>();
        supportersHeaderText.supportRichText = true;
        supportersHeaderText.text =
            $"<color=#{ColorToHex(goldColor)}><b><i><size=28>{supportersHeader}</size></i></b></color>";
        supportersHeaderText.fontSize = 14;
        supportersHeaderText.alignment = TextAnchor.UpperCenter;
        supportersHeaderText.color = whiteColor;
        supportersHeaderText.font = arial;
        supportersHeaderText.horizontalOverflow = HorizontalWrapMode.Wrap;
        RectTransform supportersHeaderRect = supportersObj.GetComponent<RectTransform>();
        supportersHeaderRect.anchorMin = new Vector2(0.5f, 1f);
        supportersHeaderRect.anchorMax = new Vector2(0.5f, 1f);
        supportersHeaderRect.pivot = new Vector2(0.5f, 1f);
        supportersHeaderRect.anchoredPosition = new Vector2(0f, supportersHeaderY);
        supportersHeaderRect.sizeDelta = new Vector2(460f, supportersHeaderHeight);

        GameObject supportersNamesObj = new GameObject("SubscriberNames_XUAIGNORE");
        supportersNamesObj.transform.SetParent(thanksObj.transform, false);
        UnityEngine.UI.Text supportersNamesText = supportersNamesObj.AddComponent<UnityEngine.UI.Text>();
        supportersNamesText.supportRichText = true;
        supportersNamesText.text =
            $"<color=#{ColorToHex(patronNameRedGold)}><b><size=22>Calvia</size></b></color>\n" +
            $"<color=#{ColorToHex(patronNameRedGold)}><b><size=22>vadaszzsomik</size></b></color>";
        supportersNamesText.fontSize = 14;
        supportersNamesText.alignment = TextAnchor.UpperCenter;
        supportersNamesText.color = whiteColor;
        supportersNamesText.font = arial;
        supportersNamesText.horizontalOverflow = HorizontalWrapMode.Wrap;
        RectTransform supportersNamesRect = supportersNamesObj.GetComponent<RectTransform>();
        supportersNamesRect.anchorMin = new Vector2(0.5f, 1f);
        supportersNamesRect.anchorMax = new Vector2(0.5f, 1f);
        supportersNamesRect.pivot = new Vector2(0.5f, 1f);
        supportersNamesRect.anchoredPosition = new Vector2(0f, supportersNamesY + 25f);
        supportersNamesRect.sizeDelta = new Vector2(460f, 100f);

        // Legacy info block (version/support links) — hidden when nothing remains after filtering.
        _infoText = null;
        string filteredInfoText = BuildFilteredInfoText(data.info);
        if (!string.IsNullOrEmpty(filteredInfoText))
        {
            GameObject infoObj = new GameObject("Info_XUAIGNORE");
            infoObj.transform.SetParent(container.transform, false);
            _infoText = infoObj.AddComponent<UnityEngine.UI.Text>();
            _infoText.text = filteredInfoText;
            _infoText.fontSize = 18;
            _infoText.alignment = TextAnchor.MiddleCenter;
            _infoText.color = new Color(0.8f, 0.8f, 0.8f);
            _infoText.font = HellGateFontProvider.GetUiFont();
            _infoText.horizontalOverflow = HorizontalWrapMode.Wrap;

            RectTransform infoRect = infoObj.GetComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0.5f, 0f);
            infoRect.anchorMax = new Vector2(0.5f, 0f);
            infoRect.pivot = new Vector2(0.5f, 0f);
            infoRect.anchoredPosition = new Vector2(0f, 220f);
            infoRect.sizeDelta = new Vector2(800f, 170f);
        }
        
        // Buttons: NoR + HellGate left; Ko-fi + Patreon in one row (Thanks column)
        float btnY = 220f;
        float btnGap = 15f;
        float norW = _spriteDiscordNoR != null ? _spriteDiscordNoR.rect.width : 200f;
        float norH = _spriteDiscordNoR != null ? _spriteDiscordNoR.rect.height : 60f;
        float hgW = _spriteDiscordHellGate != null ? _spriteDiscordHellGate.rect.width : 200f;
        float hgH = _spriteDiscordHellGate != null ? _spriteDiscordHellGate.rect.height : 60f;
        Vector2 kofiSize = GetScaledButtonSize(_spriteKoFi, DonationButtonRowHeight, 160f, DonationButtonRowHeight);
        Vector2 patreonSize = GetScaledButtonSize(_spritePatreon, DonationButtonRowHeight, 180f, DonationButtonRowHeight);
        float kofiW = kofiSize.x;
        float kofiH = kofiSize.y;
        float patreonW = patreonSize.x;
        float patreonH = patreonSize.y;
        
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
        taglineText.text = "HellGate - Made for the NoR Community, with Community Support";
        taglineText.fontSize = 24;
        taglineText.fontStyle = FontStyle.Bold;
        taglineText.alignment = TextAnchor.MiddleCenter;
        taglineText.color = new Color(0.9f, 0.85f, 0.7f);
        taglineText.font = HellGateFontProvider.GetUiFont();
        taglineText.horizontalOverflow = HorizontalWrapMode.Wrap;
        RectTransform taglineRect = taglineObj.GetComponent<RectTransform>();
        taglineRect.anchorMin = new Vector2(0f, 0f);
        taglineRect.anchorMax = new Vector2(0f, 0f);
        taglineRect.pivot = new Vector2(0.5f, 0f);
        taglineRect.anchoredPosition = new Vector2(taglineX, taglineY);
        taglineRect.sizeDelta = new Vector2(520f, 60f);
        
        // 3. Ko-fi / Patreon row (Thanks column, right edge -80, width 480)
        const float donationRowGap = 16f;
        float donationRowY = 82f;
        float thanksColumnCenterX = -80f - 240f;
        float donationRowWidth = kofiW + donationRowGap + patreonW;
        float kofiX = thanksColumnCenterX - donationRowWidth / 2f + kofiW / 2f;
        float patreonX = thanksColumnCenterX + donationRowWidth / 2f - patreonW / 2f;
        float donationRowHeight = Mathf.Max(kofiH, patreonH);

        GameObject kofiBtn = CreateImageButton(container, "KoFiButton_XUAIGNORE", _spriteKoFi, "Ko-fi", kofiX, donationRowY, kofiW, kofiH, () => Application.OpenURL("https://ko-fi.com/noxeunitydev"), kofiW, kofiH);
        RectTransform kofiRect = kofiBtn.GetComponent<RectTransform>();
        kofiRect.anchorMin = new Vector2(1f, 0f);
        kofiRect.anchorMax = new Vector2(1f, 0f);
        kofiRect.pivot = new Vector2(0.5f, 0f);
        kofiRect.anchoredPosition = new Vector2(kofiX, donationRowY);

        GameObject patreonBtn = CreateImageButton(container, "PatreonButton_XUAIGNORE", _spritePatreon, "Patreon", patreonX, donationRowY, patreonW, patreonH, () => Application.OpenURL(PatreonSupportUrl), patreonW, patreonH);
        RectTransform patreonRect = patreonBtn.GetComponent<RectTransform>();
        patreonRect.anchorMin = new Vector2(1f, 0f);
        patreonRect.anchorMax = new Vector2(1f, 0f);
        patreonRect.pivot = new Vector2(0.5f, 0f);
        patreonRect.anchoredPosition = new Vector2(patreonX, donationRowY);
        
        Vector2 startBtnSize = _spriteStartButton != null ? new Vector2(330f, 171f) : new Vector2(300f, 60f);

        CreateSplashLoadingPanel(container, startBtnSize, SplashStartButtonBottomY);

        // Start button - PNG (382x198) or fallback text (hidden until preload gate completes)
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
            buttonText.font = HellGateFontProvider.GetUiFont();
            RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.sizeDelta = Vector2.zero;
        }
        _startButton.onClick.AddListener(EnterTitleMenuMode);
        _startButton.interactable = false;
        
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, SplashStartButtonBottomY);
        buttonRect.sizeDelta = startBtnSize;
        buttonObj.SetActive(false);
        
        // Start button hover scale.
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

        CreateSplashInputBlocker(container);
        _splashInputReady = false;

        if (Plugin.Instance != null)
            NoREroMod.Systems.Spawn.SpawnTemplateDiskCache.ScheduleSplashPreload(Plugin.Instance);
        
        // Fade in
        if (_coroutineRunner != null)
        {
            _coroutineRunner.StartCoroutine(FadeInMainSplash());
            _coroutineRunner.StartCoroutine(SplashLoadingGateRoutine(buttonObj));
        }
    }

    private static void CreateSplashLoadingPanel(GameObject container, Vector2 startBtnSize, float startBtnY)
    {
        Font arial = HellGateFontProvider.GetUiFont();
        Sprite whiteSprite = GetWhiteSprite();

        _splashLoadingRoot = new GameObject("SplashLoadingPanel_XUAIGNORE");
        _splashLoadingRoot.transform.SetParent(container.transform, false);
        RectTransform panelRect = _splashLoadingRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.sizeDelta = startBtnSize;
        panelRect.anchoredPosition = new Vector2(0f, startBtnY + startBtnSize.y + SplashLoadingGapAboveStart);

        GameObject bgObj = new GameObject("LoadingBarBackground");
        bgObj.transform.SetParent(_splashLoadingRoot.transform, false);
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.sprite = whiteSprite;
        bgImage.type = Image.Type.Simple;
        bgImage.color = new Color(0.12f, 0.12f, 0.14f, 0.95f);
        bgImage.raycastTarget = false;
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.08f, 0.22f);
        bgRect.anchorMax = new Vector2(0.92f, 0.42f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        GameObject fillObj = new GameObject("LoadingBarFill");
        fillObj.transform.SetParent(bgObj.transform, false);
        _splashLoadingFill = fillObj.AddComponent<Image>();
        _splashLoadingFill.sprite = whiteSprite;
        _splashLoadingFill.type = Image.Type.Filled;
        _splashLoadingFill.fillMethod = Image.FillMethod.Horizontal;
        _splashLoadingFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _splashLoadingFill.fillAmount = 0f;
        _splashLoadingFill.color = new Color(1f, 0.55f, 0.1f, 1f);
        _splashLoadingFill.raycastTarget = false;
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        GameObject labelObj = new GameObject("LoadingLabel");
        labelObj.transform.SetParent(_splashLoadingRoot.transform, false);
        _splashLoadingLabel = labelObj.AddComponent<UnityEngine.UI.Text>();
        _splashLoadingLabel.text = "Loading...";
        _splashLoadingLabel.fontSize = 34;
        _splashLoadingLabel.fontStyle = FontStyle.Bold;
        _splashLoadingLabel.alignment = TextAnchor.MiddleCenter;
        _splashLoadingLabel.color = new Color(0.95f, 0.9f, 0.8f, 1f);
        _splashLoadingLabel.font = arial;
        _splashLoadingLabel.raycastTarget = false;
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.45f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private static void CreateSplashInputBlocker(GameObject container)
    {
        _splashInputBlocker = new GameObject("SplashInputBlocker_XUAIGNORE");
        _splashInputBlocker.transform.SetParent(container.transform, false);
        Image blockerImage = _splashInputBlocker.AddComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0.01f);
        blockerImage.raycastTarget = true;
        RectTransform blockerRect = _splashInputBlocker.GetComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = Vector2.zero;
        blockerRect.offsetMax = Vector2.zero;
        _splashInputBlocker.transform.SetAsLastSibling();
    }

    private static IEnumerator SplashLoadingGateRoutine(GameObject startButtonObject)
    {
        float elapsed = 0f;
        float blinkTimer = 0f;
        bool blinkOn = true;
        float displayedProgress = 0f;

        while (true)
        {
            elapsed += Time.deltaTime;
            blinkTimer += Time.deltaTime;
            if (blinkTimer >= 0.45f)
            {
                blinkTimer = 0f;
                blinkOn = !blinkOn;
            }

            bool preloadDone = NoREroMod.Systems.Spawn.SpawnTemplateDiskCache.IsSplashPreloadFinished;
            float timeProgress = Mathf.Clamp01(elapsed / SplashLoadMinSeconds);
            float targetProgress;
            if (elapsed >= SplashLoadMinSeconds && preloadDone)
                targetProgress = 1f;
            else if (!preloadDone && elapsed >= SplashLoadMinSeconds)
                targetProgress = 0.92f;
            else
                targetProgress = timeProgress * (preloadDone ? 1f : 0.9f);

            displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, Time.deltaTime * 0.35f);
            if (_splashLoadingFill != null)
                _splashLoadingFill.fillAmount = displayedProgress;

            if (_splashLoadingLabel != null)
            {
                Color c = _splashLoadingLabel.color;
                c.a = blinkOn ? 1f : 0.35f;
                _splashLoadingLabel.color = c;
            }

            if (elapsed >= SplashLoadMinSeconds && preloadDone)
                break;

            yield return null;
        }

        FinishSplashLoading(startButtonObject);
    }

    private static void FinishSplashLoading(GameObject startButtonObject)
    {
        _splashInputReady = true;

        if (_splashLoadingRoot != null)
            _splashLoadingRoot.SetActive(false);

        if (_splashInputBlocker != null)
            _splashInputBlocker.SetActive(false);

        if (startButtonObject != null)
            startButtonObject.SetActive(true);

        if (_startButton != null)
            _startButton.interactable = true;
    }
    
    /// <summary>
    /// <summary>Fade in main splash content.</summary>
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
    /// Switch disclaimer to Gametitle backdrop: black fill + MainHellGate.png (or logo fallback).
    /// </summary>
    private static void EnterTitleMenuMode()
    {
        if (_titleMenuBackdropActive)
            return;

        if (!_splashInputReady)
            return;

        if (_startButton != null)
            _startButton.interactable = false;

        ApplyTitleMenuBackdrop();
    }

    private static void ApplyTitleMenuBackdrop()
    {
        HideDisclaimerUiForTitleMenu();
        ShowTitleMenuArt();

        if (_mainSplashBackground != null)
            _mainSplashBackground.SetActive(true);

        if (_canvas != null)
        {
            Canvas canvas = _canvas.GetComponent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = TITLE_MENU_CANVAS_SORT_ORDER;

            CanvasGroup canvasGroup = _canvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = _canvas.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            GraphicRaycaster raycaster = _canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = false;
        }

        _titleMenuBackdropActive = true;
        if (_menuArtSprite == null)
            Plugin.Log?.LogWarning("[HellGate Splash] Title menu backdrop active without MainHellGate.png sprite.");
        else
            Plugin.Log?.LogInfo("[HellGate Splash] Title menu backdrop active with MainHellGate.png.");

        HellGateTitleMenuBackdrop.Apply();
    }

    private static void HideDisclaimerUiForTitleMenu()
    {
        if (_mainSplashScreen == null)
            return;

        Transform? container = _mainSplashScreen.transform.Find("ContentContainer");
        if (container == null)
            return;

        bool keepDisclaimerLogo = _menuArtSprite == null;

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            string name = child.name;
            if (keepDisclaimerLogo &&
                (name == "HellGateLogo_XUAIGNORE" ||
                 name == "ModTitle_XUAIGNORE" ||
                 name == "Title_XUAIGNORE"))
            {
                continue;
            }

            child.gameObject.SetActive(false);
        }
    }

    private static void ShowTitleMenuArt()
    {
        if (_canvas == null)
            return;

        if (_menuArtSprite == null)
            return;

        if (_titleMenuArtObject == null)
        {
            _titleMenuArtObject = new GameObject("TitleMenuArt_XUAIGNORE");
            _titleMenuArtObject.transform.SetParent(_canvas.transform, false);
            Image menuArtImage = _titleMenuArtObject.AddComponent<Image>();
            menuArtImage.sprite = _menuArtSprite;
            menuArtImage.preserveAspect = true;
            menuArtImage.color = Color.white;
            menuArtImage.raycastTarget = false;

            RectTransform menuArtRect = _titleMenuArtObject.GetComponent<RectTransform>();
            menuArtRect.anchorMin = Vector2.zero;
            menuArtRect.anchorMax = Vector2.one;
            menuArtRect.pivot = new Vector2(0.5f, 0.5f);
            menuArtRect.anchoredPosition = Vector2.zero;
            menuArtRect.sizeDelta = Vector2.zero;
        }
        else
        {
            Image menuArtImage = _titleMenuArtObject.GetComponent<Image>();
            if (menuArtImage != null)
                menuArtImage.sprite = _menuArtSprite;
            _titleMenuArtObject.SetActive(true);
        }

        if (_mainSplashBackground != null)
            _mainSplashBackground.transform.SetAsFirstSibling();
        _titleMenuArtObject.transform.SetSiblingIndex(1);
    }

    /// <summary>
    /// Fully remove splash overlay when leaving Gametitle.
    /// </summary>
    private static void FullyHideSplash()
    {
        if (_canvas != null)
            _canvas.SetActive(false);

        _titleMenuBackdropActive = false;
    }
    
    /// <summary>
    /// <summary>Hide overlay when leaving Gametitle.</summary>
    /// </summary>
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive)
            return;

        if (!string.IsNullOrEmpty(scene.name) && scene.name != "Gametitle")
        {
            FullyHideSplash();
        }
        else if (_titleMenuBackdropActive)
        {
            HellGateTitleMenuBackdrop.Apply();
        }
    }
    
    /// <summary>
    /// <summary>Coroutine host for splash transitions.</summary>
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
            { "RU", new LanguageInfo { name = "\u0420\u0443\u0441\u0441\u043A\u0438\u0439", flag = "" } },
            { "EN", new LanguageInfo { name = "English", flag = "" } },
            { "JP", new LanguageInfo { name = "\u65E5\u672C\u8A9E", flag = "" } },
            { "CN", new LanguageInfo { name = "\u4E2D\u6587", flag = "" } },
            { "KR", new LanguageInfo { name = "\uD55C\uAD6D\uC5B4", flag = "" } },
            { "FR", new LanguageInfo { name = "Fran\u00E7ais", flag = "" } },
            { "DE", new LanguageInfo { name = "Deutsch", flag = "" } },
            { "PT", new LanguageInfo { name = "Portugu\u00EAs", flag = "" } },
            { "BR", new LanguageInfo { name = "Portugu\u00EAs (Brasil)", flag = "" } },
            { "ES", new LanguageInfo { name = "Espa\u00F1ol", flag = "" } }
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
        data.warning = "ADULT CONTENT WARNING\n\nThis mod contains explicit adult content.\n18+ only. Viewer discretion advised.";
        data.additionalText = "Join our Discord server for updates and support!\nConsider supporting development on Ko-fi.";
        data.info = string.Empty;
        
        // Localized START button labels.
        var buttonTexts = new Dictionary<string, string>
        {
            { "RU", "\u041D\u0410\u0427\u0410\u0422\u042C" },
            { "EN", "START" },
            { "JP", "\u958B\u59CB" },
            { "CN", "\u5F00\u59CB" },
            { "KR", "\uC2DC\uC791" },
            { "FR", "COMMENCER" },
            { "DE", "STARTEN" },
            { "PT", "COME\u00C7AR" },
            { "BR", "COME\u00C7AR" },
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
