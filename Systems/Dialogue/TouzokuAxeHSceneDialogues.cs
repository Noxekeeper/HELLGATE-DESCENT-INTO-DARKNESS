using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using NoREroMod;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// TouzokuAxe dirty phrases system during H-scenes
/// Bound to specific animations and events
/// </summary>
internal static class TouzokuAxeHSceneDialogues
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
    private static float _verticalOffset = 15.0f;
    private static float _streamingSpeed = 0.03f;
    private static Color _goblinColor = Color.white;
    private static Color _goblinOutlineColor = Color.black;
    // Aradia colors centralized in DialogueDisplay.

    // Reference to DialogueDisplay
    private static DialogueDisplay _display = null;

    // Position counters for Aradia (separate for words and thoughts)
    private static readonly Dictionary<object, int> _aradiaResponsePositionCounter = new();
    private static readonly Dictionary<object, int> _aradiaThoughtPositionCounter = new();
    private static int _aradiaPositionIndex = 0;

    // Aradia dialogue data storage
    private static Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>> _aradiaResponseDialogues = new();
    private static Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>> _aradiaThoughtDialogues = new();

    /// <summary>
    /// Initialize system
    /// </summary>
    internal static void Initialize()
    {
        if (_initialized) return;
        
        try
        {
            LoadTouzokuAxeHSceneData();
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
    /// Load data from JSON (ручной парсинг)
    /// </summary>
    private static void LoadTouzokuAxeHSceneData()
    {
        // Clear old data before load
        _animationComments?.Clear();
        
        try
        {
            string dataPath = GetDataPath();
            string jsonPath = Path.Combine(dataPath, "TouzokuAxeHSceneData.json");

            if (!File.Exists(jsonPath))
            {
                return;
            }

            string jsonText = File.ReadAllText(jsonPath);
            
            ParseJsonManually(jsonText);
            
            // Logging disabled by request
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

// Colors are now taken from config
        }
        catch (Exception ex)
        {
        }
    }
    
    /// <summary>
    /// Parsing comments by animations
    /// Анимации for TouzokuAxe: START, START2, START3, START4, START5, ERO, ERO2, 2ERO, 2ERO2, 2ERO3, 2ERO4, FIN, FIN2, FIN3, JIGO, JIGO2
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
            
            // List of animations for parsing (соответствует EroTouzokuAXE.cs)
            string[] animations = { 
                "START", "START2", "START3", "START4", "START5",
                "ERO", "ERO2",
                "2ERO", "2ERO2", "2ERO3", "2ERO4",
                "FIN", "FIN2", "FIN3",
                "JIGO", "JIGO2"
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
        
        // Parse events: SE, SE1, SE2, SE3, SE8, START, START2, START3, START4, START5, ERO, ERO2, 2ERO, 2ERO2, 2ERO3, 2ERO4, FIN, FIN2, FIN3, JIGO, JIGO2
        string[] eventNames = { 
            "SE", "SE1", "SE2", "SE3", "SE8",
            "START", "START2", "START3", "START4", "START5",
            "ERO", "ERO2",
            "2ERO", "2ERO2", "2ERO3", "2ERO4",
            "FIN", "FIN2", "FIN3",
            "JIGO", "JIGO2"
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
    /// Process H-scene events TouzokuAxe
    /// </summary>
    internal static void ProcessHSceneEvent(object enemyInstance, string animationName, string eventName, int seCount)
    {
        // TEMPORARY LOGGING FOR DIAGNOSTICS
        // try
// Debug logging disabled
        
        if (!_initialized || _display == null)
        {
            return;
        }

        // First check, whether this is ARADIA_RESPONSE or ARADIA_THOUGHT event
        if (eventName.StartsWith("ARADIA_RESPONSE") || eventName.StartsWith("ARADIA_THOUGHT"))
        {
            try
            {
            }
            catch { }
            ProcessAradiaEvent(enemyInstance, animationName, eventName, seCount);
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
            return;
        }
        
        // Определяем verticalOffset depending on phase animation
        float enemyVerticalOffset = _verticalOffset; // Base value 15px
        bool isStartPhase = !string.IsNullOrEmpty(animationName) &&
                           (animationName == "START" || animationName == "START2" || animationName == "START3" ||
                            animationName == "START4" || animationName == "START5");

        if (isStartPhase)
        {
            enemyVerticalOffset = 150f; // START-START5: 150px higher
        }
        else
        {
            enemyVerticalOffset = 300f; // Others: 300px higher (raised by 150px)
        }

        // Show comment bound to bone
        _display.ShowTouzokuHSceneComment(enemyInstance, comment, _displayDuration, Plugin.dialogueFontSize.Value, enemyVerticalOffset, 0f, Plugin.ParseColor(Plugin.enemyColor.Value), Plugin.ParseColor(Plugin.enemyOutlineColor.Value));

        // Schedule GG response in 2 seconds
        StartAradiaResponseCoroutine(enemyInstance, animationName, eventName);

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
    /// Load Aradia data from AradiaTouzokuNormal.json
    /// </summary>
    private static void LoadAradiaData()
    {
        try
        {
            string dataPath = GetDataPath();
            string jsonPath = Path.Combine(dataPath, "AradiaTouzokuAxe.json");

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

            // Парсим секцию animations for диаlogs
            LoadAradiaDialoguesFromJson(jsonText);
        }
        catch (Exception ex)
        {
        }
    }

// ColorParser используется for парсинга цветов

    /// <summary>
    /// Загрузка диаlogs Aradia from JSON
    /// </summary>
    private static void LoadAradiaDialoguesFromJson(string jsonText)
    {
        try
        {
            // Clear old data
            _aradiaResponseDialogues.Clear();
            _aradiaThoughtDialogues.Clear();

            // Парсим секцию animations
            int animStart = jsonText.IndexOf("\"animations\"");
            if (animStart == -1) return;

            int animEnd = FindMatchingBrace(jsonText, animStart + jsonText.Substring(animStart).IndexOf('{'));
            if (animEnd == -1) return;

            string animSection = jsonText.Substring(animStart, animEnd - animStart + 1);

            // Парсим каждую animation
            string[] animations = {
                "START", "START2", "START3",
                "ERO", "ERO1", "ERO2", "ERO3", "ERO4", "ERO5",
                "2ERO", "2ERO2", "2ERO3", "2ERO4", "2ERO5", "2ERO6", "2ERO7", "2ERO8",
                "JIGO", "JIGO2"
            };

            foreach (string anim in animations)
            {
                ParseAnimationDialogues(animSection, anim);
            }
        }
        catch (Exception ex)
        {
        }
    }

    /// <summary>
    /// Parsing диаlogs for specific animation
    /// </summary>
    private static void ParseAnimationDialogues(string animSection, string animationName)
    {
        try
        {
            string animPattern = $"\"{animationName}\"\\s*:\\s*\\{{";
            Match animMatch = Regex.Match(animSection, animPattern);

            if (!animMatch.Success) return;

            int animStart = animMatch.Index + animMatch.Length;
            int braceCount = 1;
            int animEnd = animStart;

            // Находим конец блока animation
            for (int i = animStart; i < animSection.Length; i++)
            {
                if (animSection[i] == '{') braceCount++;
                else if (animSection[i] == '}') braceCount--;

                if (braceCount == 0)
                {
                    animEnd = i;
                    break;
                }
            }

            if (animEnd <= animStart) return;

            string animBlock = animSection.Substring(animStart, animEnd - animStart);

            // Parse events
            string[] eventTypes = { "ARADIA_RESPONSE", "ARADIA_THOUGHT" };

            foreach (string eventType in eventTypes)
            {
                string eventPattern = $"\"{eventType}\"\\s*:\\s*\\{{";
                Match eventMatch = Regex.Match(animBlock, eventPattern);

                if (eventMatch.Success)
                {
                    int eventStart = eventMatch.Index + eventMatch.Length;
                    int eventBraceCount = 1;
                    int eventEnd = eventStart;

                    // Находим конец блока events
                    for (int i = eventStart; i < animBlock.Length; i++)
                    {
                        if (animBlock[i] == '{') eventBraceCount++;
                        else if (animBlock[i] == '}') eventBraceCount--;

                        if (eventBraceCount == 0)
                        {
                            eventEnd = i;
                            break;
                        }
                    }

                    if (eventEnd > eventStart)
                    {
                        string eventBlock = animBlock.Substring(eventStart, eventEnd - eventStart);
                        ParseMindBrokenLevels(eventBlock, animationName, eventType);
                    }
                }
            }
        }
        catch (Exception ex)
        {
        }
    }

    /// <summary>
    /// Parsing уровней mindBroken for events
    /// </summary>
    private static void ParseMindBrokenLevels(string eventBlock, string animationName, string eventType)
    {
        try
        {
            string[] levels = { "low", "medium", "high" };

            foreach (string level in levels)
            {
                string levelPattern = $"\"{level}\"\\s*:\\s*\\[";
                Match levelMatch = Regex.Match(eventBlock, levelPattern);

                if (levelMatch.Success)
                {
                    int levelStart = levelMatch.Index + levelMatch.Length;
                    int bracketCount = 1;
                    int levelEnd = levelStart;

                    // Находим конец массива
                    for (int i = levelStart; i < eventBlock.Length; i++)
                    {
                        if (eventBlock[i] == '[') bracketCount++;
                        else if (eventBlock[i] == ']') bracketCount--;

                        if (bracketCount == 0)
                        {
                            levelEnd = i;
                            break;
                        }
                    }

                    if (levelEnd > levelStart)
                    {
                        string levelBlock = eventBlock.Substring(levelStart, levelEnd - levelStart);

                        // Парсим строки from массива
                        MatchCollection stringMatches = Regex.Matches(levelBlock, "\"([^\"]+)\"");
                        List<string> dialogues = new List<string>();

                        foreach (Match strMatch in stringMatches)
                        {
                            dialogues.Add(strMatch.Groups[1].Value);
                        }

                        if (dialogues.Count > 0)
                        {
                            // Добавляем in соответствующую структуру
                            if (eventType == "ARADIA_RESPONSE")
                            {
                                if (!_aradiaResponseDialogues.ContainsKey(animationName))
                                    _aradiaResponseDialogues[animationName] = new Dictionary<string, Dictionary<string, List<string>>>();

                                if (!_aradiaResponseDialogues[animationName].ContainsKey(eventType))
                                    _aradiaResponseDialogues[animationName][eventType] = new Dictionary<string, List<string>>();

                                _aradiaResponseDialogues[animationName][eventType][level] = dialogues;
                            }
                            else if (eventType == "ARADIA_THOUGHT")
                            {
                                if (!_aradiaThoughtDialogues.ContainsKey(animationName))
                                    _aradiaThoughtDialogues[animationName] = new Dictionary<string, Dictionary<string, List<string>>>();

                                if (!_aradiaThoughtDialogues[animationName].ContainsKey(eventType))
                                    _aradiaThoughtDialogues[animationName][eventType] = new Dictionary<string, List<string>>();

                                _aradiaThoughtDialogues[animationName][eventType][level] = dialogues;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
        }
    }

    // Aradia positioning is now handled centrally in DialogueDisplay.GetAradiaBoneForAnimation()

    /// <summary>
    /// Process ARADIA_RESPONSE и ARADIA_THOUGHT events
    /// </summary>
    private static void ProcessAradiaEvent(object enemyInstance, string animationName, string eventName, int seCount)
    {
        try
        {
        }
        catch { }

        if (_display == null) return;

        try
        {
            // Get уровень MindBroken
            float mindBrokenPercent = NoREroMod.Patches.UI.MindBroken.MindBrokenSystem.Percent;
            string mindBrokenLevel = GetMindBrokenLevel(mindBrokenPercent);

            try
            {
            }
            catch { }

            string[] dialogues = null;

            if (eventName.StartsWith("ARADIA_RESPONSE"))
            {
                dialogues = GetAradiaResponseDialogues(animationName, eventName, mindBrokenLevel);
                try
                {
                }
                catch { }

                if (dialogues != null && dialogues.Length > 0)
                {
                    string selectedDialogue = dialogues[UnityEngine.Random.Range(0, dialogues.Length)];
                    try
                    {
                    }
                    catch { }
                    ShowAradiaResponse(enemyInstance, selectedDialogue, animationName, eventName);
                }
                else
                {
                    try
                    {
                    }
                    catch { }
                }
            }
            else if (eventName.StartsWith("ARADIA_THOUGHT"))
            {
                dialogues = GetAradiaThoughtDialogues(animationName, eventName, mindBrokenLevel);
                try
                {
                }
                catch { }

                if (dialogues != null && dialogues.Length > 0)
                {
                    string selectedDialogue = dialogues[UnityEngine.Random.Range(0, dialogues.Length)];
                    try
                    {
                    }
                    catch { }
                    ShowAradiaThought(enemyInstance, selectedDialogue, animationName, eventName);
                }
                else
                {
                    try
                    {
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            try
            {
            }
            catch { }
        }
    }

    /// <summary>
    /// Get MindBroken level based on percent
    /// </summary>
    private static string GetMindBrokenLevel(float percent)
    {
        if (percent < 0.3f) return "low";
        if (percent < 0.7f) return "medium";
        return "high";
    }

    /// <summary>
    /// Get dialogues ARADIA_RESPONSE
    /// </summary>
    private static string[] GetAradiaResponseDialogues(string animationName, string eventName, string mindBrokenLevel)
    {
        if (_aradiaResponseDialogues.ContainsKey(animationName) &&
            _aradiaResponseDialogues[animationName].ContainsKey(eventName) &&
            _aradiaResponseDialogues[animationName][eventName].ContainsKey(mindBrokenLevel))
        {
            return _aradiaResponseDialogues[animationName][eventName][mindBrokenLevel].ToArray();
        }
        return null;
    }

    /// <summary>
    /// Get dialogues ARADIA_THOUGHT
    /// </summary>
    private static string[] GetAradiaThoughtDialogues(string animationName, string eventName, string mindBrokenLevel)
    {
        if (_aradiaThoughtDialogues.ContainsKey(animationName) &&
            _aradiaThoughtDialogues[animationName].ContainsKey(eventName) &&
            _aradiaThoughtDialogues[animationName][eventName].ContainsKey(mindBrokenLevel))
        {
            return _aradiaThoughtDialogues[animationName][eventName][mindBrokenLevel].ToArray();
        }
        return null;
    }

    /// <summary>
    /// Display ARADIA_RESPONSE
    /// </summary>
    private static void ShowAradiaResponse(object enemyInstance, string response, string animationName, string eventName)
    {
        if (string.IsNullOrEmpty(response) || _display == null) return;

        try
        {
            // Get bone based on animation and enemy type, use centralized vertical offset
            string boneName = DialogueDisplay.GetAradiaBoneForAnimation(enemyInstance, animationName);
            float verticalOffset = DialogueDisplay.GetDefaultAradiaVerticalOffset();
            var style = DialogueDisplay.BuildAradiaUnifiedStyle(verticalOffset, 0.0f, true);
            _display.ShowAradiaResponse(enemyInstance, response, boneName, style, _displayDuration);
        }
        catch (Exception ex)
        {
            try
            {
            }
            catch { }
        }
    }

    /// <summary>
    /// Display ARADIA_THOUGHT
    /// </summary>
    private static void ShowAradiaThought(object enemyInstance, string thought, string animationName, string eventName)
    {
        if (string.IsNullOrEmpty(thought) || _display == null) return;

        try
        {
            // Get bone based on animation and enemy type, use centralized vertical offset
            string boneName = DialogueDisplay.GetAradiaBoneForAnimation(enemyInstance, animationName);
            float verticalOffset = DialogueDisplay.GetDefaultAradiaVerticalOffset();
            var style = DialogueDisplay.BuildAradiaUnifiedStyle(verticalOffset, 0.0f, true);
            _display.ShowAradiaThought(enemyInstance, thought, boneName, style, _displayDuration);
        }
        catch (Exception ex)
        {
            try
            {
            }
            catch { }
        }
    }

    /// <summary>
    /// Start coroutine for планирования ответа ГГ
    /// </summary>
    private static void StartAradiaResponseCoroutine(object enemyInstance, string animationName, string eventName)
    {
        if (enemyInstance == null) return;

        var monoBehaviour = enemyInstance as UnityEngine.MonoBehaviour;
        if (monoBehaviour != null)
        {
            monoBehaviour.StartCoroutine(DelayedAradiaResponse(enemyInstance, animationName, eventName));
        }
    }

    /// <summary>
    /// Coroutine for задержанного ответа ГГ
    /// </summary>
    private static System.Collections.IEnumerator DelayedAradiaResponse(object enemyInstance, string animationName, string eventName)
    {
        // Wait 2 seconds
        yield return new UnityEngine.WaitForSeconds(2.0f);

        // Определяем тип ответа (RESPONSE for первой фазы, THOUGHT for второй)
        string responseType = animationName.StartsWith("START") ? "ARADIA_RESPONSE" : "ARADIA_THOUGHT";

        try
        {
        }
        catch { }

        // Генерируем ответ ГГ
        ProcessAradiaEvent(enemyInstance, animationName, responseType, 0);
    }
}

