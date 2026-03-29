using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using NoREroMod;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// Kakasi dirty phrases system during H-scenes
/// Bound to specific animations and events (cross and ground)
/// </summary>
internal static class KakasiHSceneDialogues
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
    private static float _fontSize = 30.0f;
    private static float _verticalOffset = 15.0f;
    private static float _streamingSpeed = 0.03f;
    private static Color _goblinColor = Color.white;
    private static Color _goblinOutlineColor = Color.black;
    // Aradia colors centralized in DialogueDisplay.

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
            LoadKakasiHSceneData();
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
    private static void LoadKakasiHSceneData()
    {
        // Clear old data before loading
        _animationComments?.Clear();
        
        try
        {
            string dataPath = GetDataPath();
            string jsonPath = Path.Combine(dataPath, "KakasiHSceneData.json");

            if (!File.Exists(jsonPath))
            {
                return;
            }

            string jsonText = File.ReadAllText(jsonPath);
            
            ParseJsonManually(jsonText);
        }
        catch (Exception ex)
        {
            // Ensure data cleared on error
            _animationComments?.Clear();
        }
    }
    
    /// <summary>
    /// Get path to folder with data
    /// </summary>
    private static string GetDataPath()
    {
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
            int settingsStart = jsonText.IndexOf("\"settings\"");
            if (settingsStart == -1) return;
            
            int settingsEnd = FindMatchingBrace(jsonText, settingsStart + jsonText.Substring(settingsStart).IndexOf('{'));
            if (settingsEnd == -1) return;
            
            string settingsSection = jsonText.Substring(settingsStart, settingsEnd - settingsStart + 1);
            
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
    /// </summary>
    private static void ParseAnimationComments(string jsonText)
    {
        try
        {
            int animStart = jsonText.IndexOf("\"animations\"");
            if (animStart == -1) return;
            
            int animEnd = FindMatchingBrace(jsonText, animStart + jsonText.Substring(animStart).IndexOf('{'));
            if (animEnd == -1) return;
            
            string animSection = jsonText.Substring(animStart, animEnd - animStart + 1);
            
            // Анимации креста (EroAnimation) - lowercase
            // Анимации земли (kakashi_ero2) - UPPERCASE
            string[] animations = { 
                // Крест
                "start", "start2", "ero1", "ero2", "ero3", "finish1", "finish2", "finish_end", "finish_end2",
                // Земля
                "START", "START2", "ERO2", "ERO3", "ERO4", "ERO5", "FIN", "JIGO1", "JIGO2"
            };
            
            foreach (string animName in animations)
            {
                Dictionary<string, List<string>> eventComments = new();
                
                string animPattern = $"\"{animName}\"\\s*:\\s*\\{{";
                Match animMatch = Regex.Match(animSection, animPattern);
                if (!animMatch.Success) continue;
                
                int animBlockStart = animMatch.Index + animMatch.Length - 1;
                int animBlockEnd = FindMatchingBrace(animSection, animBlockStart);
                if (animBlockEnd == -1) continue;
                
                string animBlock = animSection.Substring(animBlockStart, animBlockEnd - animBlockStart + 1);
                
                ParseEventsForAnimation(animBlock, eventComments);
                
                if (eventComments.Count > 0)
                {
                    _animationComments[animName] = eventComments;
                }
            }
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
        int eventsStart = animBlock.IndexOf("\"events\"");
        if (eventsStart == -1) return;
        
        int eventsBraceStart = eventsStart + animBlock.Substring(eventsStart).IndexOf('{');
        int eventsEnd = FindMatchingBrace(animBlock, eventsBraceStart);
        if (eventsEnd == -1) return;
        
        string eventsSection = animBlock.Substring(eventsBraceStart, eventsEnd - eventsBraceStart + 1);
        
        // События for креста и земли
        string[] eventNames = { 
            "SE", "SE1", "SE2", "SE3", "SE8",
            "start", "start2", "ero1", "ero2", "ero3", "finish1", "finish2", "finish_end", "finish_end2",
            "START", "START2", "ERO2", "ERO3", "ERO4", "ERO5", "FIN", "JIGO1", "JIGO2"
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
            string pattern = $"\"{key}\"\\s*:\\s*\\[";
            Match match = Regex.Match(jsonSection, pattern);
            if (!match.Success) return result;
            
            int arrayStart = match.Index + match.Length - 1;
            int arrayEnd = FindMatchingBracket(jsonSection, arrayStart);
            if (arrayEnd == -1) return result;
            
            string arrayContent = jsonSection.Substring(arrayStart, arrayEnd - arrayStart + 1);
            
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
    /// Process animation events for Kakasi
    /// </summary>
    internal static void ProcessHSceneEvent(object enemyInstance, string animationName, string eventName, int seCount)
    {
        if (!_initialized || _display == null)
        {
            return;
        }
        
        try
        {
            string typeName = enemyInstance?.GetType().Name ?? "NULL";
            
            // Проверяем cooldown
            if (_lastCommentTime.ContainsKey(enemyInstance))
            {
                float timeSinceLast = Time.time - _lastCommentTime[enemyInstance];
                if (timeSinceLast < _commentCooldown)
                {
                    // Cooldown активен - skip (логи отключены)
                    return;
                }
            }
            
            // Проверяем максимальное количество simultaneous comments
            CleanupExpiredComments();
            if (_activeComments.Count >= _maxSimultaneousComments)
            {
                // Maximum comments reached - skip (логи отключены)
                return;
            }
            
            // Нормализуем имеon анимаций (крест - lowercase, земля - UPPERCASE)
            string animKey = animationName;
            string eventKey = eventName;
            
            // For креста (EroAnimation) имеon анимаций in lowercase
            // For земли (kakashi_ero2) имеon анимаций in UPPERCASE
            if (typeName == "EroAnimation")
            {
                // Крест - нормализуем к lowercase
                animKey = animationName?.ToLowerInvariant() ?? "";
            }
            else
            {
                // Земля - нормализуем к UPPERCASE
                animKey = animationName?.ToUpperInvariant() ?? "";
            }
            
            // For событий SE with se_count формируем ключ se_count_N
            if (eventName.Equals("SE", StringComparison.OrdinalIgnoreCase) && seCount > 0)
            {
                eventKey = $"se_count_{seCount}";
            }
            
            // Ищем комментарии for этой animation и events
            if (!_animationComments.ContainsKey(animKey))
            {
                return;
            }
            
            Dictionary<string, List<string>> events = _animationComments[animKey];
            
            // Сначала пробуем найти by eventKey (se_count_N or конкретное event)
            List<string> phrases = null;
            if (events.ContainsKey(eventKey))
            {
                phrases = events[eventKey];
            }
            // If not found, пробуем SE1, SE2 etc.
            else if (eventName.Equals("SE", StringComparison.OrdinalIgnoreCase))
            {
                string seKey = $"SE{seCount}";
                if (events.ContainsKey(seKey))
                {
                    phrases = events[seKey];
                }
                else if (events.ContainsKey("SE1"))
                {
                    phrases = events["SE1"];
                }
            }
            // For креста: if event SE, but not found by se_count, пробуем найти by name animation as событию
            // Например, for animation "start" и events SE ищем event "start" in JSON
            else if (typeName == "EroAnimation" && eventName.Equals("SE", StringComparison.OrdinalIgnoreCase))
            {
                // Try найти event with именем animation (start, start2, ero1 etc.)
                if (events.ContainsKey(animKey))
                {
                    phrases = events[animKey];
                }
            }
            // If event - this имя animation (e.g., START, ERO2 for земли)
            else if (events.ContainsKey(eventName))
            {
                phrases = events[eventName];
            }
            
            if (phrases == null || phrases.Count == 0)
            {
                // Logging disabled by request
                return;
            }
            
            // Выбираем случайную phrase
            string selectedPhrase = phrases[UnityEngine.Random.Range(0, phrases.Count)];
            
            // Фраза выбраon и показывается (логи отключены)
            
            // Update cooldown
            _lastCommentTime[enemyInstance] = Time.time;
            if (!_activeComments.Contains(enemyInstance))
            {
                _activeComments.Add(enemyInstance);
            }
            
            // Отображаем комментарий
            _display.ShowTouzokuHSceneComment(enemyInstance, selectedPhrase, _displayDuration, Plugin.dialogueFontSize.Value, _verticalOffset, 0f, Plugin.ParseColor(Plugin.enemyColor.Value), Plugin.ParseColor(Plugin.enemyOutlineColor.Value));
            
            // Remove from active after delay
            MonoBehaviour mb = enemyInstance as MonoBehaviour;
            if (mb != null)
            {
                mb.StartCoroutine(RemoveFromActiveAfterDelay(enemyInstance, _displayDuration));
            }
        }
        catch (Exception ex)
        {
        }
    }
    
    /// <summary>
    /// Remove from active comments after delay
    /// </summary>
    private static System.Collections.IEnumerator RemoveFromActiveAfterDelay(object enemyInstance, float delay)
    {
        yield return new WaitForSeconds(delay);
        _activeComments.Remove(enemyInstance);
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
    /// Reset system for reload with new language
    /// </summary>
    internal static void Reset()
    {
        _initialized = false;
        _animationComments?.Clear();
    }

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

