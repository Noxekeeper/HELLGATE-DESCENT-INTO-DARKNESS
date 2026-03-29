using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using NoREroMod;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// InquisitionBlack dirty phrases system during H-scenes
/// Bound to specific animations and events
/// </summary>
internal static class InquisitionBlackHSceneDialogues
{
    // Comments data storage
    private static Dictionary<string, Dictionary<string, List<string>>> _animationComments = new();
    private static bool _initialized = false;
    
    // Track cooldowns for each enemy
    private static readonly Dictionary<object, float> _lastCommentTime = new();
    
    // Track active comments
    private static readonly List<object> _activeComments = new();
    
    // Settings from JSON
    private static float _commentCooldown = 2.0f;
    private static int _maxSimultaneousComments = 1;
    private static float _displayDuration = 4.0f;
    private static float _fontSize = 22.0f;
    private static float _verticalOffset = 150.0f;
    private static float _streamingSpeed = 0.03f;
    private static Color _goblinColor = Color.white;
    private static Color _goblinOutlineColor = Color.black;
    // Aradia colors centralized in DialogueDisplay
    
    // Reference to DialogueDisplay
    private static DialogueDisplay _display = null;

    /// <summary>
    /// Initialize system
    /// </summary>
    internal static void Initialize()
    {
        if (_initialized) return;
        
        try
        {
            LoadInquisitionBlackHSceneData();
            LoadAradiaData();
            _initialized = true;
        }
        catch (Exception ex)
        {
        }
    }
    
    /// <summary>
    /// Set DialogueDisplay for display
    /// </summary>
    internal static void SetDisplay(DialogueDisplay display)
    {
        _display = display;
    }
    
    /// <summary>
    /// Load data from JSON (manual parsing)
    /// </summary>
    private static void LoadInquisitionBlackHSceneData()
    {
        // Clear old data before loading
        _animationComments?.Clear();
        
        try
        {
            string dataPath = GetDataPath();
            string jsonPath = Path.Combine(dataPath, "InquiBlackHSceneData.json");

            if (!File.Exists(jsonPath))
            {
                return;
            }

            string jsonText = File.ReadAllText(jsonPath);
            
            ParseJsonManually(jsonText);
            
            // Logging disabled for reducing volume logs
            // try
// Debug logging disabled
        }
        catch (Exception ex)
        {
            // Ensure data cleared on error
            _animationComments?.Clear();
        }
    }
    
    /// <summary>
    /// Get path to folder with data considering selected language
    /// Uses the same same method as DialogueDatabase
    /// </summary>
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

        // Fallback: try find relative to project
        try
        {
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            string fallbackPath = Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(projectPath, "REZERVNIE COPY"), "NoRHellGate3"), "Systems"), "Dialogue"), "Data");
            
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
    
    /// <summary>
    /// Manual parsing JSON (without Newtonsoft.Json)
    /// </summary>
    private static void ParseJsonManually(string jsonText)
    {
        _animationComments.Clear();
        
        // Parse settings
        ParseSettings(jsonText);
        
        // Parse comments by animations
        ParseAnimationComments(jsonText);
    }
    
    /// <summary>
    /// Parse settings
    /// </summary>
    private static void ParseSettings(string jsonText)
    {
        try
        {
            // Find section settings
            int settingsStart = jsonText.IndexOf("\"settings\"");
            if (settingsStart == -1) return;
            
            int settingsEnd = FindMatchingBrace(jsonText, settingsStart + jsonText.Substring(settingsStart).IndexOf('{'));
            if (settingsEnd == -1) return;
            
            string settingsSection = jsonText.Substring(settingsStart, settingsEnd - settingsStart + 1);
            
            // Parse individual parameters
            Match match;
            
            match = Regex.Match(settingsSection, "\"commentCooldownSeconds\"\\s*:\\s*([0-9.]+)");
            if (match.Success) _commentCooldown = float.Parse(match.Groups[1].Value);
            
            match = Regex.Match(settingsSection, "\"maxSimultaneousComments\"\\s*:\\s*([0-9]+)");
            if (match.Success) _maxSimultaneousComments = int.Parse(match.Groups[1].Value);
            
            match = Regex.Match(settingsSection, "\"commentDisplayDuration\"\\s*:\\s*([0-9.]+)");
            if (match.Success) _displayDuration = float.Parse(match.Groups[1].Value);
            
            // fontSize now taken from config Plugin.dialogueFontSize.Value
            
            match = Regex.Match(settingsSection, "\"verticalOffset\"\\s*:\\s*([0-9.]+)");
            if (match.Success) _verticalOffset = float.Parse(match.Groups[1].Value);
            
            match = Regex.Match(settingsSection, "\"streamingSpeed\"\\s*:\\s*([0-9.]+)");
            if (match.Success) _streamingSpeed = float.Parse(match.Groups[1].Value);

            // Парсим цвета
            _goblinColor = ColorParser.ParseColorFromJson(settingsSection, "goblinColor", Color.white);
            _goblinOutlineColor = ColorParser.ParseColorFromJson(settingsSection, "goblinOutlineColor", Color.black);
        }
        catch (Exception ex)
        {
        }
    }
    
    /// <summary>
    /// Parsing comments by animations
    /// Анимации for InquisitionBlack: START, START2, ERO, ERO1, ERO2, ERO3, ERO4, FIN, FIN2, JIGO, JIGO2, JIGOFIN, JIGOFIN2
    /// </summary>
    private static void ParseAnimationComments(string jsonText)
    {
        try
        {
            // Find section animations
            int animStart = jsonText.IndexOf("\"animations\"");
            if (animStart == -1) return;
            
            int animEnd = FindMatchingBrace(jsonText, animStart + jsonText.Substring(animStart).IndexOf('{'));
            if (animEnd == -1) return;
            
            string animSection = jsonText.Substring(animStart, animEnd - animStart + 1);
            
            // List of animations for parsing (соответствует InquiBlackEro.cs)
            string[] animations = { 
                "START", "START2",
                "ERO", "ERO1", "ERO2", "ERO3", "ERO4",
                "FIN", "FIN2",
                "JIGO", "JIGO2",
                "JIGOFIN", "JIGOFIN2"
            };
            
            foreach (string animName in animations)
            {
                Dictionary<string, List<string>> eventComments = new();
                
                // Find section этой animation
                string animPattern = $"\"{animName}\"\\s*:\\s*\\{{";
                Match animMatch = Regex.Match(animSection, animPattern);
                if (!animMatch.Success) continue;
                
                int animBlockStart = animMatch.Index + animMatch.Length - 1;
                int animBlockEnd = FindMatchingBrace(animSection, animBlockStart);
                if (animBlockEnd == -1) continue;
                
                string animBlock = animSection.Substring(animBlockStart, animBlockEnd - animBlockStart + 1);
                
                // Parse events
                ParseEventsForAnimation(animBlock, eventComments);
                
                if (eventComments.Count > 0)
                {
                    _animationComments[animName] = eventComments;
                }
            }
            
            // Parsing completed
        }
        catch (Exception ex)
        {
        }
    }
    
    /// <summary>
    /// Parse events for specific animation
    /// </summary>
    private static void ParseEventsForAnimation(string animBlock, Dictionary<string, List<string>> eventComments)
    {
        // Find section events
        int eventsStart = animBlock.IndexOf("\"events\"");
        if (eventsStart == -1) return;
        
        int eventsBraceStart = eventsStart + animBlock.Substring(eventsStart).IndexOf('{');
        int eventsEnd = FindMatchingBrace(animBlock, eventsBraceStart);
        if (eventsEnd == -1) return;
        
        string eventsSection = animBlock.Substring(eventsBraceStart, eventsEnd - eventsBraceStart + 1);
        
        // Parse events: SE, SE1, SE2, SE3, SE8, START, START2, ERO, ERO1, ERO2, ERO3, ERO4, FIN, FIN2, JIGO, JIGO2, JIGOFIN, JIGOFIN2
        string[] eventNames = { 
            "SE", "SE1", "SE2", "SE3", "SE8",
            "START", "START2",
            "ERO", "ERO1", "ERO2", "ERO3", "ERO4",
            "FIN", "FIN2",
            "JIGO", "JIGO2",
            "JIGOFIN", "JIGOFIN2"
        };
        
        foreach (string eventName in eventNames)
        {
            List<string> phrases = ParseStringArray(eventsSection, eventName);
            if (phrases.Count > 0)
            {
                eventComments[eventName] = phrases;
            }
            
            // For SE events also parse se_count_1, se_count_2, se_count_3
            if (eventName == "SE")
            {
                for (int i = 1; i <= 3; i++)
                {
                    string seCountKey = $"se_count_{i}";
                    List<string> sePhrases = ParseStringArray(eventsSection, seCountKey);
                    if (sePhrases.Count > 0)
                    {
                        eventComments[seCountKey] = sePhrases;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Parse string array from JSON
    /// </summary>
    private static List<string> ParseStringArray(string jsonSection, string key)
    {
        List<string> result = new();
        
        try
        {
            // Find key
            string pattern = $"\"{key}\"\\s*:\\s*\\[";
            Match match = Regex.Match(jsonSection, pattern);
            if (!match.Success) return result;
            
            int arrayStart = match.Index + match.Length - 1;
            int arrayEnd = FindMatchingBracket(jsonSection, arrayStart);
            if (arrayEnd == -1) return result;
            
            string arrayContent = jsonSection.Substring(arrayStart, arrayEnd - arrayStart + 1);
            
            // Parse strings inside массива
            MatchCollection stringMatches = Regex.Matches(arrayContent, "\"([^\"]+)\"");
            foreach (Match stringMatch in stringMatches)
            {
                result.Add(stringMatch.Groups[1].Value);
            }
        }
        catch (Exception ex)
        {
        }
        
        return result;
    }
    
    /// <summary>
    /// Find matching closing parenthesis
    /// </summary>
    private static int FindMatchingBrace(string text, int startIndex)
    {
        int depth = 0;
        for (int i = startIndex; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}') depth--;
            if (depth == 0) return i;
        }
        return -1;
    }
    
    /// <summary>
    /// Find matching closing bracket
    /// </summary>
    private static int FindMatchingBracket(string text, int startIndex)
    {
        int depth = 0;
        for (int i = startIndex; i < text.Length; i++)
        {
            if (text[i] == '[') depth++;
            else if (text[i] == ']') depth--;
            if (depth == 0) return i;
        }
        return -1;
    }
    
    /// <summary>
    /// Process H-scene events InquisitionBlack
    /// </summary>
    internal static void ProcessHSceneEvent(object enemyInstance, string animationName, string eventName, int seCount)
    {
        // TEMPORARY LOGGING FOR DIAGNOSTICS
        
        if (!_initialized)
        {
            return;
        }
        
        if (_display == null)
        {
            return;
        }
        
        // Check cooldown
        if (_lastCommentTime.ContainsKey(enemyInstance))
        {
            if (Time.time - _lastCommentTime[enemyInstance] < _commentCooldown)
            {
                return;
            }
        }
        
        // Check limit simultaneous comments
        CleanupExpiredComments();
        if (_activeComments.Count >= _maxSimultaneousComments)
        {
            return;
        }
        
        // Get comment for this event
        string comment = GetCommentForEvent(animationName, eventName, seCount);
        if (string.IsNullOrEmpty(comment))
        {
            // Logging disabled for reducing volume logs
            return;
        }
        
        // Show comment
        _display.ShowTouzokuHSceneComment(enemyInstance, comment, _displayDuration, Plugin.dialogueFontSize.Value, _verticalOffset, 0f, Plugin.ParseColor(Plugin.enemyColor.Value), Plugin.ParseColor(Plugin.enemyOutlineColor.Value));
        // Logging disabled for reducing volume logs
        
        // Update cooldown and active comments
        _lastCommentTime[enemyInstance] = Time.time;
        if (!_activeComments.Contains(enemyInstance))
        {
            _activeComments.Add(enemyInstance);
        }
    }
    
    /// <summary>
    /// Reset system for reload with new language
    /// </summary>
    internal static void Reset()
    {
        _initialized = false;
        _animationComments?.Clear();
    }
    
    /// <summary>
    /// Get comment for events
    /// </summary>
    private static string GetCommentForEvent(string animationName, string eventName, int seCount)
    {
        if (!_animationComments.ContainsKey(animationName))
        {
            return null;
        }
        
        Dictionary<string, List<string>> eventComments = _animationComments[animationName];
        
        // For events SE проверяем se_count
        if (eventName == "SE" && seCount > 0)
        {
            string seCountKey = $"se_count_{seCount}";
            if (eventComments.ContainsKey(seCountKey))
            {
                List<string> phrases = eventComments[seCountKey];
                if (phrases.Count > 0)
                {
                    return phrases[UnityEngine.Random.Range(0, phrases.Count)];
                }
            }
        }
        
        // Check direct event
        if (eventComments.ContainsKey(eventName))
        {
            List<string> phrases = eventComments[eventName];
            if (phrases.Count > 0)
            {
                return phrases[UnityEngine.Random.Range(0, phrases.Count)];
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Очистка expired comments
    /// </summary>
    private static void CleanupExpiredComments()
    {
        List<object> toRemove = new();
        foreach (object enemy in _activeComments)
        {
            if (!_lastCommentTime.ContainsKey(enemy) || 
                Time.time - _lastCommentTime[enemy] > _displayDuration + 1f)
            {
                toRemove.Add(enemy);
            }
        }
        
        foreach (object enemy in toRemove)
        {
            _activeComments.Remove(enemy);
        }
    }

    /// <summary>
    /// Parsing цвета from JSON настроек
    /// </summary>
// ColorParser используется for парсинга цветов

    /// <summary>
    /// Load Aradia data from AradiaTouzokuNormal.json
    /// </summary>
    private static void LoadAradiaData()
    {
        try
        {
            string dataPath = GetDataPath();
            string jsonPath = Path.Combine(dataPath, "AradiaTouzokuNormal.json");

            if (!File.Exists(jsonPath))
            {
                return;
            }

            string jsonText = File.ReadAllText(jsonPath);

            // Парсим секцию settings
            string settingsPattern = "\"settings\"\\s*:\\s*\\{([^}]*)\\}";
            Match settingsMatch = Regex.Match(jsonText, settingsPattern, RegexOptions.Singleline);

            if (settingsMatch.Success)
            {
                string settingsSection = settingsMatch.Groups[1].Value;
                // Aradia colors centralized in DialogueDisplay.
            }
        }
        catch (Exception ex)
        {
        }
    }
}

