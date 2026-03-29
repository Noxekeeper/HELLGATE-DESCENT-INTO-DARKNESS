using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using NoREroMod;
using NoREroMod.Patches.UI.MindBroken;
using System.Linq;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// Dialogue system for goblins (goblinero) during H-scenes
/// Works similar to TouzokuNormalHSceneDialogues
/// </summary>
internal static class GoblinHSceneDialogues
{
    private static DialogueDisplay _display;
    private static Dictionary<string, Dictionary<string, string[]>> _dialogueData;
    private static Dictionary<string, Dictionary<string, Dictionary<string, string[]>>> _segmentedDialogues;
    private static Dictionary<string, Dictionary<string, Dictionary<string, string[]>>> _aradiaResponseData;
    private static Dictionary<string, Dictionary<string, Dictionary<string, string[]>>> _aradiaThoughtData;
    private static Dictionary<string, float> _settings;
    private static Dictionary<object, int> _aradiaResponsePositionCounter = new();
    private static Dictionary<string, float> _aradiaResponseSettings;
    private static Dictionary<string, float> _aradiaThoughtSettings;
    private static Color _goblinColor = Color.white;
    private static Color _goblinOutlineColor = Color.black;
    private static float _lowMindThreshold = 0.3f;
    private static float _mediumMindThreshold = 0.7f;
    private static bool _initialized = false;
    private static int _dialoguePositionCounter = 0;
    
    /// <summary>
    /// Sets the dialogue display system
    /// </summary>
    internal static void SetDisplay(DialogueDisplay display)
    {
        _display = display;
    }

    /// <summary>
    /// Initialize the system
    /// </summary>
    internal static void Initialize()
    {
        if (_initialized) return;

        try
        {
            LoadDialogueData();
            _initialized = true;

// Debug information removed

        }
        catch (Exception ex)
        {
            // Initialization failed silently
        }
    }
    
    /// <summary>
    /// Loads dialogue data from JSON files
    /// </summary>
    /// <summary>
    /// Manual JSON parsing for dialogues (adapted from DialogueDatabase)
    /// </summary>
    internal static Dictionary<string, object> ParseJsonManually(string jsonText)
    {
        try
        {
            var result = new Dictionary<string, object>();

            // Parse settings section
            int settingsStart = jsonText.IndexOf("\"settings\"", StringComparison.OrdinalIgnoreCase);
            if (settingsStart >= 0)
            {
                int braceStart = jsonText.IndexOf('{', settingsStart);
                if (braceStart >= 0)
                {
                    int braceCount = 0;
                    int braceEnd = braceStart;
                    for (int i = braceStart; i < jsonText.Length; i++)
                    {
                        if (jsonText[i] == '{')
                        {
                            braceCount++;
                        }
                        else if (jsonText[i] == '}')
                        {
                            braceCount--;
                            if (braceCount == 0)
                            {
                                braceEnd = i;
                                break;
                            }
                        }
                    }

                    if (braceEnd > braceStart)
                    {
                        string settingsContent = jsonText.Substring(braceStart + 1, braceEnd - braceStart - 1);
                        var settings = ParseSettingsSection(settingsContent);
                        if (settings != null && settings.Count > 0)
                        {
                            result["settings"] = settings;
                        }
                    }
                }
            }

            // Parse animations section
            int animationsStart = jsonText.IndexOf("\"animations\"", StringComparison.OrdinalIgnoreCase);
            if (animationsStart >= 0)
            {
                int braceStart = jsonText.IndexOf('{', animationsStart);
                if (braceStart >= 0)
                {
                    int braceCount = 0;
                    int braceEnd = braceStart;
                    for (int i = braceStart; i < jsonText.Length; i++)
                    {
                        if (jsonText[i] == '{')
                        {
                            braceCount++;
                        }
                        else if (jsonText[i] == '}')
                        {
                            braceCount--;
                            if (braceCount == 0)
                            {
                                braceEnd = i;
                                break;
                            }
                        }
                    }

                    if (braceEnd > braceStart)
                    {
                        string animationsContent = jsonText.Substring(braceStart + 1, braceEnd - braceStart - 1);
                        var animations = ParseAnimationsSection(animationsContent);
                        if (animations != null && animations.Count > 0)
                        {
                            result["animations"] = animations;
                        }
                    }
                }
            }

            // Parse mindBrokenThresholds (for Aradia files)
            int thresholdsStart = jsonText.IndexOf("\"mindBrokenThresholds\"", StringComparison.OrdinalIgnoreCase);
            if (thresholdsStart >= 0)
            {
                int bracketStart = jsonText.IndexOf('[', thresholdsStart);
                if (bracketStart >= 0)
                {
                    int bracketEnd = jsonText.IndexOf(']', bracketStart);
                    if (bracketEnd > bracketStart)
                    {
                        string thresholdsContent = jsonText.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
                        var thresholds = ParseFloatArray(thresholdsContent);
                        if (thresholds != null && thresholds.Length > 0)
                        {
                            result["mindBrokenThresholds"] = thresholds;
                        }
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    /// <summary>
    /// Parse settings section
    /// </summary>
    private static Dictionary<string, object> ParseSettingsSection(string content)
    {
        var settings = new Dictionary<string, object>();

        // Parse simple values
        var matches = System.Text.RegularExpressions.Regex.Matches(content,
            "\"([^\"]+)\"\\s*:\\s*([^,}\\n]+)", System.Text.RegularExpressions.RegexOptions.Singleline);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            string key = match.Groups[1].Value;
            string value = match.Groups[2].Value.Trim();

            // Skip nested objects (color, etc.)
            if (value.Contains("{") || value.Contains("[")) continue;

            // Remove quotes if present
            if (value.StartsWith("\"") && value.EndsWith("\""))
            {
                value = value.Substring(1, value.Length - 2);
            }

            settings[key] = value;
        }

        return settings;
    }

    /// <summary>
    /// Parse animations section
    /// </summary>
    private static Dictionary<string, object> ParseAnimationsSection(string content)
    {
        var animations = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        // Find animations of type "ANIMATION_NAME": {
        var animMatches = System.Text.RegularExpressions.Regex.Matches(content,
            "\"([^\"]+)\"\\s*:\\s*\\{", System.Text.RegularExpressions.RegexOptions.Singleline);

        foreach (System.Text.RegularExpressions.Match animMatch in animMatches)
        {
            string animName = animMatch.Groups[1].Value;

            // Find corresponding closing brace
            int startPos = animMatch.Index + animMatch.Length;
            int braceCount = 1;
            int endPos = startPos;

            for (int i = startPos; i < content.Length; i++)
            {
                if (content[i] == '{')
                {
                    braceCount++;
                }
                else if (content[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        endPos = i;
                        break;
                    }
                }
            }

            if (endPos > startPos)
            {
                string animContent = content.Substring(startPos, endPos - startPos);
                var animationData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                string description = ExtractStringProperty(animContent, "description");
                if (!string.IsNullOrEmpty(description))
                {
                    animationData["description"] = description;
                }

                var events = ParseEventsSection(animContent);
                if (events != null && events.Count > 0)
                {
                    animationData["events"] = events;
                    animations[animName] = animationData;
                }
            }
        }

        return animations;
    }

    private static string ExtractStringProperty(string content, string propertyName)
    {
        int keyIndex = content.IndexOf($"\"{propertyName}\"", StringComparison.OrdinalIgnoreCase);
        if (keyIndex < 0) return null;

        int colonIndex = content.IndexOf(':', keyIndex);
        if (colonIndex < 0) return null;

        int valueStart = content.IndexOf('"', colonIndex + 1);
        if (valueStart < 0) return null;

        int valueEnd = content.IndexOf('"', valueStart + 1);
        if (valueEnd < 0) return null;

        return content.Substring(valueStart + 1, valueEnd - valueStart - 1);
    }

    /// <summary>
    /// Parse events section within animation
    /// </summary>
    private static Dictionary<string, object> ParseEventsSection(string content)
    {
        var events = new Dictionary<string, object>();

        // Find events of type "EVENT_NAME": [
        var eventMatches = System.Text.RegularExpressions.Regex.Matches(content,
            "\"([^\"]+)\"\\s*:\\s*\\[", System.Text.RegularExpressions.RegexOptions.Singleline);

        foreach (System.Text.RegularExpressions.Match eventMatch in eventMatches)
        {
            string eventName = eventMatch.Groups[1].Value;

            // Find corresponding closing brace
            int startPos = eventMatch.Index + eventMatch.Length;
            int bracketCount = 1;
            int endPos = startPos;

            for (int i = startPos; i < content.Length; i++)
            {
                if (content[i] == '[') bracketCount++;
                else if (content[i] == ']')
                {
                    bracketCount--;
                    if (bracketCount == 0)
                    {
                        endPos = i;
                        break;
                    }
                }
            }

            if (endPos > startPos)
            {
                string arrayContent = content.Substring(startPos, endPos - startPos);
                var strings = ParseStringArray(arrayContent);
                if (strings != null && strings.Length > 0)
                {
                    events[eventName] = strings;
                }
            }
        }

        // Also find events of type "EVENT_NAME": { (for nested objects)
        var nestedEventMatches = System.Text.RegularExpressions.Regex.Matches(content,
            "\"([^\"]+)\"\\s*:\\s*\\{", System.Text.RegularExpressions.RegexOptions.Singleline);

        foreach (System.Text.RegularExpressions.Match eventMatch in nestedEventMatches)
        {
            string eventName = eventMatch.Groups[1].Value;

            // Skip if already processed as array
            if (events.ContainsKey(eventName)) continue;

            // Find corresponding closing brace
            int startPos = eventMatch.Index + eventMatch.Length;
            int braceCount = 1;
            int endPos = startPos;

            for (int i = startPos; i < content.Length; i++)
            {
                if (content[i] == '{') braceCount++;
                else if (content[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        endPos = i;
                        break;
                    }
                }
            }

            if (endPos > startPos)
            {
                string nestedContent = content.Substring(startPos, endPos - startPos);
                var nestedEvents = ParseNestedEvents(nestedContent);
                if (nestedEvents != null && nestedEvents.Count > 0)
                {
                    events[eventName] = nestedEvents;
                }
            }
        }

        return events;
    }

    /// <summary>
    /// Parse nested events (for mindBroken levels)
    /// </summary>
    private static Dictionary<string, object> ParseNestedEvents(string content)
    {
        var nestedEvents = new Dictionary<string, object>();

        // Find levels mindBroken
        var levelMatches = System.Text.RegularExpressions.Regex.Matches(content,
            "\"([^\"]+)\"\\s*:\\s*\\[", System.Text.RegularExpressions.RegexOptions.Singleline);

        foreach (System.Text.RegularExpressions.Match levelMatch in levelMatches)
        {
            string levelName = levelMatch.Groups[1].Value;

            // Find corresponding closing brace
            int startPos = levelMatch.Index + levelMatch.Length;
            int bracketCount = 1;
            int endPos = startPos;

            for (int i = startPos; i < content.Length; i++)
            {
                if (content[i] == '[') bracketCount++;
                else if (content[i] == ']')
                {
                    bracketCount--;
                    if (bracketCount == 0)
                    {
                        endPos = i;
                        break;
                    }
                }
            }

            if (endPos > startPos)
            {
                string arrayContent = content.Substring(startPos, endPos - startPos);
                var strings = ParseStringArray(arrayContent);
                if (strings != null && strings.Length > 0)
                {
                    nestedEvents[levelName] = strings;
                }
            }
        }

        return nestedEvents;
    }

    /// <summary>
    /// Parse string array
    /// </summary>
    private static string[] ParseStringArray(string content)
    {
        var items = new System.Collections.Generic.List<string>();

        // Regex for search strings in quotes
        var stringMatches = System.Text.RegularExpressions.Regex.Matches(content, "\"([^\"]*)\"");

        foreach (System.Text.RegularExpressions.Match stringMatch in stringMatches)
        {
            items.Add(stringMatch.Groups[1].Value);
        }

        return items.ToArray();
    }

    /// <summary>
    /// Parse array float значений
    /// </summary>
    private static float[] ParseFloatArray(System.Collections.IEnumerable content)
    {
        var items = new System.Collections.Generic.List<float>();

        if (content != null)
        {
            foreach (var item in content)
            {
                if (item != null && float.TryParse(item.ToString().Trim(), out float value))
                {
                    items.Add(value);
                }
            }
        }

        return items.ToArray();
    }

    /// </summary>
    private static void LoadDialogueData()
    {
        _dialogueData = new Dictionary<string, Dictionary<string, string[]>>(StringComparer.OrdinalIgnoreCase);
        _segmentedDialogues = new Dictionary<string, Dictionary<string, Dictionary<string, string[]>>>(StringComparer.OrdinalIgnoreCase);
        _aradiaResponseData = new Dictionary<string, Dictionary<string, Dictionary<string, string[]>>>(StringComparer.OrdinalIgnoreCase);
        _aradiaThoughtData = new Dictionary<string, Dictionary<string, Dictionary<string, string[]>>>(StringComparer.OrdinalIgnoreCase);
        _settings = new Dictionary<string, float>();
        _aradiaResponseSettings = new Dictionary<string, float>();
        _aradiaThoughtSettings = new Dictionary<string, float>();
        
        try
        {
            string dataPath = GetDataPath();
            string jsonPath = System.IO.Path.Combine(dataPath, "GoblinHSceneData.json");

            if (!System.IO.File.Exists(jsonPath))
            {
                return;
            }

            string jsonText = System.IO.File.ReadAllText(jsonPath);
            var data = ParseJsonManually(jsonText);
            if (data == null)
            {
                return;
            }
            
            // Load settings
            if (data.ContainsKey("settings"))
            {
                var settings = data["settings"] as Dictionary<string, object>;
                if (settings != null)
                {
                    foreach (var setting in settings)
                    {
                        if (setting.Value != null)
                        {
                            // Load regular float settings
                            if (float.TryParse(setting.Value.ToString(), out float value))
                            {
                                _settings[setting.Key] = value;
                            }
// Goblin colors are now taken from config
                        }
                    }
                }
            }

            // Use single bone37 for all goblin dialogues

            // Load animations
            if (data.ContainsKey("animations"))
            {
                var animations = data["animations"] as Dictionary<string, object>;
                if (animations != null)
                {
                    LoadAnimations(animations);
                }
            }

            // Load unified ARADIA_RESPONSE and ARADIA_THOUGHT file
            string aradiaGoblinPath = System.IO.Path.Combine(dataPath, "AradiaGoblinResponses.json");
            if (System.IO.File.Exists(aradiaGoblinPath))
            {
            string aradiaGoblinJson = System.IO.File.ReadAllText(aradiaGoblinPath);
            LoadAradiaGoblinDataFromJson(aradiaGoblinJson);
            }

            // Logging disabled by request
        }
        catch (Exception ex)
        {
            // Dialogue data loading failed silently
        }
    }
    
    /// <summary>
    /// Load animations from JSON data
    /// </summary>
    private static void LoadAnimations(Dictionary<string, object> animations)
    {
        foreach (var animKvp in animations)
        {
            string animationName = animKvp.Key;
            var animationData = animKvp.Value as Dictionary<string, object>;
            
            if (animationData?.ContainsKey("events") == true)
            {
                var events = animationData["events"] as Dictionary<string, object>;
                if (events != null)
                {
                    LoadAnimationEvents(animationName, events);
                }
            }
        }
    }

    // Counter for rotating dialogue positions around bone37
// Variable no longer used

    /// <summary>
    /// Display dialogue with random position around bone37
    /// </summary>
    private static void ShowDialogueWithRandomPosition(object goblinInstance, string dialogue, string animationName,
        string eventName, int seCount)
    {
        if (string.IsNullOrEmpty(dialogue))
        {
            return;
        }

        try
        {
            // Get display settings
            float fontSize = Plugin.dialogueFontSize.Value;
            float displayDuration = _settings.ContainsKey("commentDisplayDuration") ? _settings["commentDisplayDuration"] : 4f;

            // Rotate position to simulate different goblins (heights only)
            _dialoguePositionCounter = (_dialoguePositionCounter + 1) % 4;

            float verticalOffset, horizontalOffset;

            switch (_dialoguePositionCounter)
            {
                case 0:
                    verticalOffset = 50f;
                    break;
                case 1:
                    verticalOffset = 100f;
                    break;
                case 2:
                    verticalOffset = 150f;
                    break;
                default:
                    verticalOffset = 50f;
                    break;
            }
            horizontalOffset = 0f;


            // Use standard ShowStaticThreat with colors from JSON
            var fontStyle = Plugin.GetFontStyle(Plugin.enemyFontStyle.Value);
            var goblinStyle = new DialogueStyle
            {
                FontSize = Plugin.dialogueFontSize.Value,
                Color = Plugin.ParseColor(Plugin.enemyColor.Value),
                VerticalOffset = verticalOffset,
                HorizontalOffset = horizontalOffset,
                FollowBone = true, // Bone attachment - dialogue follows movement
                IsBold = (fontStyle & FontStyle.Bold) != 0,
                IsItalic = (fontStyle & FontStyle.Italic) != 0,
                UseOutline = true,
                OutlineColor = Plugin.ParseColor(Plugin.enemyOutlineColor.Value),
                OutlineDistance = new UnityEngine.Vector2(1f, -1f)
            };

            var bonePos = new BonePosition
            {
                BoneName = "bone11", // Single bone for all dialogues
                UseScreenCenter = false
            };

            _display.ShowStaticThreat(goblinInstance, dialogue, bonePos, goblinStyle, displayDuration);
        }
        catch (Exception ex)
        {
            // Dialogue display failed silently
        }
    }


    /// <summary>
    /// Load events for animation
    /// </summary>
    private static void LoadAnimationEvents(string animationName, Dictionary<string, object> events)
    {
        var animDialogues = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var animSegmented = new Dictionary<string, Dictionary<string, string[]>>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var eventKvp in events)
        {
            string eventName = eventKvp.Key;
            
            if (eventName.Equals("SE", StringComparison.OrdinalIgnoreCase))
            {
                // Segmented dialogues (se_count_1, se_count_2, etc.)
                var seEvents = eventKvp.Value as Dictionary<string, object>;
                if (seEvents != null)
                {
                    LoadSegmentedEvents(animationName, seEvents, animSegmented);
                }
            }
            else
            {
                // Standard dialogues
                var dialogues = ConvertToStringArray(eventKvp.Value);
                if (dialogues != null && dialogues.Length > 0)
                {
                    animDialogues[eventName] = dialogues;
                }
            }
        }
        
        if (animDialogues.Count > 0)
        {
            _dialogueData[animationName] = animDialogues;
        }
        
        if (animSegmented.Count > 0)
        {
            _segmentedDialogues[animationName] = animSegmented;
        }
    }
    
    /// <summary>
    /// Load segmented events
    /// </summary>
    private static void LoadSegmentedEvents(string animationName, Dictionary<string, object> seEvents, 
        Dictionary<string, Dictionary<string, string[]>> animSegmented)
    {
        foreach (var seKvp in seEvents)
        {
            string seCountKey = seKvp.Key; // e.g. "se_count_1"
            var dialogues = ConvertToStringArray(seKvp.Value);
            
            if (dialogues != null && dialogues.Length > 0)
            {
                if (!animSegmented.ContainsKey("SE"))
                {
                    animSegmented["SE"] = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                }
                animSegmented["SE"][seCountKey] = dialogues;
            }
        }
    }
    
    /// <summary>
    /// Convert object to string array
    /// </summary>
    internal static string[] ConvertToStringArray(object value)
    {
        if (value == null) return null;
        
        if (value is string singleString)
        {
            return new[] { singleString };
        }
        
        if (value is System.Collections.IEnumerable enumerable)
        {
            var result = new List<string>();
            foreach (var item in enumerable)
            {
                if (item != null)
                {
                    result.Add(item.ToString());
                }
            }
            return result.ToArray();
        }
        
        return new[] { value.ToString() };
    }
    
    /// <summary>
    /// Process animation events goblin
    /// </summary>
    internal static void ProcessEvent(object goblinInstance, string animationName, string eventName, int seCount)
    {
        if (!_initialized || _display == null || goblinInstance == null)
        {
            return;
        }

        try
        {
            // Special handling for 2ERO_IKI events

            // First check, whether this is ARADIA_RESPONSE or ARADIA_THOUGHT event
            if (eventName.StartsWith("ARADIA_RESPONSE") || eventName.StartsWith("ARADIA_THOUGHT"))
            {
                ProcessAradiaEvent(goblinInstance, animationName, eventName, seCount);
                return;
            }

            // Process dialogues goblin
            string[] dialogues = GetDialogues(animationName, eventName, seCount);
            if (dialogues != null && dialogues.Length > 0)
            {
                string selectedDialogue = dialogues[UnityEngine.Random.Range(0, dialogues.Length)];

                // Use single bone37 with different offsets to simulate different goblins
                ShowDialogueWithRandomPosition(goblinInstance, selectedDialogue, animationName, eventName, seCount);

                // If this диалог goblin, schedule response ГГ in 2 seconds
                StartAradiaResponseCoroutine(goblinInstance, animationName, eventName);
            }
            else
            {
                // No dialogues found for this event
            }
        }
        catch (Exception ex)
        {
            // Event processing failed silently
        }
    }

    /// <summary>
    /// Process ARADIA_RESPONSE и ARADIA_THOUGHT events
    /// </summary>
    private static void ProcessAradiaEvent(object goblinInstance, string animationName, string eventName, int seCount)
    {
        if (_display == null) return;

        try
        {
            // Get actual MindBroken level
            float mindBrokenPercent = NoREroMod.Patches.UI.MindBroken.MindBrokenSystem.Percent;
            string mindBrokenLevel = GetMindBrokenLevel(mindBrokenPercent);

            string[] dialogues = null;

            if (eventName.StartsWith("ARADIA_RESPONSE"))
            {
                dialogues = GetAradiaResponseDialogues(animationName, eventName, mindBrokenLevel);
                if (dialogues != null && dialogues.Length > 0)
                {
                    string selectedDialogue = dialogues[UnityEngine.Random.Range(0, dialogues.Length)];
                    ShowAradiaResponse(goblinInstance, selectedDialogue, animationName, eventName);
                }
            }
            else if (eventName.StartsWith("ARADIA_THOUGHT"))
            {
                dialogues = GetAradiaThoughtDialogues(animationName, eventName, mindBrokenLevel);
                if (dialogues != null && dialogues.Length > 0)
                {
                    string selectedDialogue = dialogues[UnityEngine.Random.Range(0, dialogues.Length)];
                    ShowAradiaThought(goblinInstance, selectedDialogue, animationName, eventName);
                }
            }
        }
        catch (Exception ex)
        {
            // Aradia event processing failed silently
        }
    }

    /// <summary>
    /// Get MindBroken level based on percent
    /// </summary>
    private static string GetMindBrokenLevel(float percent)
    {
        // Use thresholds from JSON or fallback
        float lowThreshold = 0.3f;
        float mediumThreshold = 0.7f;

        if (percent < lowThreshold) return "low";
        if (percent < mediumThreshold) return "medium";
        return "high";
    }

    /// <summary>
    /// Get dialogues ARADIA_RESPONSE
    /// </summary>
    private static string[] GetAradiaResponseDialogues(string animationName, string eventName, string mindBrokenLevel)
    {
        if (_aradiaResponseData.ContainsKey(animationName))
        {
            var animData = _aradiaResponseData[animationName];
            if (animData.ContainsKey(eventName) && animData[eventName].ContainsKey(mindBrokenLevel))
            {
                return animData[eventName][mindBrokenLevel];
            }
        }
        return null;
    }

    /// <summary>
    /// Get dialogues ARADIA_THOUGHT
    /// </summary>
    private static string[] GetAradiaThoughtDialogues(string animationName, string eventName, string mindBrokenLevel)
    {
        if (_aradiaThoughtData.ContainsKey(animationName))
        {
            var animData = _aradiaThoughtData[animationName];
            if (animData.ContainsKey(eventName) && animData[eventName].ContainsKey(mindBrokenLevel))
            {
                return animData[eventName][mindBrokenLevel];
            }
        }
        return null;
    }

    /// <summary>
    /// Display ARADIA_RESPONSE
    /// </summary>
    private static void ShowAradiaResponse(object goblinInstance, string response, string animationName, string eventName)
    {
        if (string.IsNullOrEmpty(response) || _display == null) return;

        try
        {
            // Get display settings
            float displayDuration = _settings.TryGetValue("commentDisplayDuration", out float duration) ? duration : 5f;

            // Get bone based on animation and enemy type, use centralized vertical offset
            string boneName = DialogueDisplay.GetAradiaBoneForAnimation(goblinInstance, animationName);
            float verticalOffset = DialogueDisplay.GetDefaultAradiaVerticalOffset();
            var style = DialogueDisplay.BuildAradiaUnifiedStyle(verticalOffset, 0f, true);
            _display.ShowAradiaResponse(goblinInstance, response, boneName, style, displayDuration);
        }
        catch (Exception ex)
        {
            // ARADIA_RESPONSE display failed silently
        }
    }

    /// <summary>
    /// Display ARADIA_THOUGHT
    /// </summary>
    private static void ShowAradiaThought(object goblinInstance, string thought, string animationName, string eventName)
    {
        if (string.IsNullOrEmpty(thought) || _display == null) return;

        try
        {
            // Get display settings
            float displayDuration = _settings.TryGetValue("commentDisplayDuration", out float duration) ? duration : 5f;

            // Get bone based on animation and enemy type, use centralized vertical offset
            string boneName = DialogueDisplay.GetAradiaBoneForAnimation(goblinInstance, animationName);
            float verticalOffset = DialogueDisplay.GetDefaultAradiaVerticalOffset();
            var style = DialogueDisplay.BuildAradiaUnifiedStyle(verticalOffset, 0f, true);
            _display.ShowAradiaThought(goblinInstance, thought, boneName, style, displayDuration);
        }
        catch (Exception ex)
        {
            // ARADIA_THOUGHT display failed silently
        }
    }

    
    /// <summary>
    /// Get dialogues for animation и events
    /// </summary>
    private static string[] GetDialogues(string animationName, string eventName, int seCount)
    {
        if (string.IsNullOrEmpty(animationName) || string.IsNullOrEmpty(eventName))
        {
            return null;
        }

        string animKey = animationName.ToUpperInvariant();
        string eventKey = eventName.ToUpperInvariant();

        // Special logging for 2ERO_iki
        // Handle 2ERO_IKI special case

        // First check сегментированные диалоги for SE events
        if (eventKey == "SE" && _segmentedDialogues.ContainsKey(animKey))
        {
            var segmentedData = _segmentedDialogues[animKey];
            if (segmentedData.ContainsKey("SE"))
            {
                string seCountKey = $"se_count_{seCount}";
                if (segmentedData["SE"].ContainsKey(seCountKey))
                {
                    return segmentedData["SE"][seCountKey];
                }
            }
        }

        // Then check standard dialogues
        if (_dialogueData.ContainsKey(animKey))
        {
            var animData = _dialogueData[animKey];

            // First look for exact match (for backward compatibility)
            if (animData.ContainsKey(eventKey))
            {
                return animData[eventKey];
            }

            // Обычный поиск диаlogs (теперь without префиксоin Goblin*)
        }
        else
        {
            // Animation not found
        }

        return null;
    }
    


    
    /// <summary>
    /// Get path to folder with data considering selected language
    /// </summary>
    internal static string GetDataPath()
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
        
        // Fallback: use old method
        try
        {
            string baseFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string hellGateJson = Path.Combine(baseFolder, "HellGateJson");
            
            // Try to add language folder
            string languageCode = Plugin.hellGateLanguage?.Value ?? "EN";
            if (string.IsNullOrEmpty(languageCode))
            {
                languageCode = "EN";
            }
            string langPath = Path.Combine(hellGateJson, languageCode);
            if (Directory.Exists(langPath))
            {
                return langPath;
            }
            string enPath = Path.Combine(hellGateJson, "EN");
            if (Directory.Exists(enPath))
            {
                return enPath;
            }
            
            return hellGateJson;
        }
        catch { }
        
        // Last fallback
        string basePathFallback = Path.Combine(Application.dataPath, "..");
        string bepInExFallback = Path.Combine(basePathFallback, "BepInEx");
        string pluginsFallback = Path.Combine(bepInExFallback, "plugins");
        return Path.Combine(pluginsFallback, "HellGateJson");
    }
    
    /// <summary>
    /// Запуск корутины for ответа ГГ in 2 seconds
    /// </summary>
    private static void StartAradiaResponseCoroutine(object goblinInstance, string animationName, string eventName)
    {
        if (goblinInstance == null) return;

        var monoBehaviour = goblinInstance as UnityEngine.MonoBehaviour;
        if (monoBehaviour != null)
        {
            monoBehaviour.StartCoroutine(DelayedAradiaResponse(goblinInstance, animationName, eventName));
        }
    }

    /// <summary>
    /// Coroutine for задержанного ответа ГГ
    /// </summary>
    private static System.Collections.IEnumerator DelayedAradiaResponse(object goblinInstance, string animationName, string coroutineKey)
    {
        // Wait 2 seconds
        yield return new UnityEngine.WaitForSeconds(2.0f);

        // Определяем тип ответа (RESPONSE for первой фазы, THOUGHT for второй)
        string responseType = animationName.StartsWith("2ERO") ? "ARADIA_THOUGHT" : "ARADIA_RESPONSE";

        // Генерируем ответ ГГ
        ProcessAradiaEvent(goblinInstance, animationName, responseType, 0);
    }

    private static void LoadAradiaGoblinDataFromJson(string json)
    {
        try
        {
            var data = ParseJsonManually(json);
            if (data == null)
            {
                return;
            }

            LoadAradiaGoblinData(data);
        }
        catch (Exception ex)
        {
            // AradiaGoblin data loading failed silently
        }
    }

    private static void LoadAradiaGoblinData(Dictionary<string, object> data)
    {
        try
        {
            if (data.ContainsKey("settings"))
            {
                if (data["settings"] is Dictionary<string, object> settings)
                {
                    foreach (var setting in settings)
                    {
                        if (setting.Value != null)
                        {
                            // Load regular float settings
                            if (float.TryParse(setting.Value.ToString(), out float value))
                            {
                                _settings[setting.Key] = value;
                            }
                        }
                    }
                }
            }

            float[] thresholds = new float[] { 0.3f, 0.7f };
            if (data.ContainsKey("mindBrokenThresholds"))
            {
                if (data["mindBrokenThresholds"] is System.Collections.IEnumerable enumerable)
                {
                    var parsed = ParseFloatArray(enumerable);
                    if (parsed != null && parsed.Length >= 2)
                    {
                        thresholds = parsed;
                    }
                }
            }

            if (data.ContainsKey("animations"))
            {
                if (data["animations"] is Dictionary<string, object> animations)
                {
                    LoadAradiaGoblinAnimations(animations, thresholds);
                }
            }
        }
        catch (Exception ex)
        {
            // AradiaGoblin data processing failed silently
        }
    }

    /// <summary>
    /// Загрузка анимаций AradiaGoblin
    /// </summary>
    private static void LoadAradiaGoblinAnimations(Dictionary<string, object> animations, float[] thresholds)
    {
        foreach (var animKvp in animations)
        {
            string animationName = animKvp.Key;
            var animationData = animKvp.Value as Dictionary<string, object>;

            if (animationData?.ContainsKey("events") == true)
            {
                var events = animationData["events"] as Dictionary<string, object>;
                if (events != null)
                {
                    foreach (var eventKvp in events)
                    {
                        string eventName = eventKvp.Key;
                        var eventData = eventKvp.Value as Dictionary<string, object>;

                        if (eventData != null)
                        {
                            // Process уровни mindBroken (low, medium, high)
                            var mindBrokenLevels = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

                            foreach (var levelKvp in eventData)
                            {
                                string levelName = levelKvp.Key;
                                var levelDialogues = ConvertToStringArray(levelKvp.Value);
                                if (levelDialogues != null && levelDialogues.Length > 0)
                                {
                                    mindBrokenLevels[levelName] = levelDialogues;
                                }
                            }

                            if (mindBrokenLevels.Count > 0)
                            {
                                // Определяем, куда сохранять (responses or thoughts)
                                if (eventName == "ARADIA_RESPONSE")
                                {
                                    if (!_aradiaResponseData.ContainsKey(animationName))
                                        _aradiaResponseData[animationName] = new Dictionary<string, Dictionary<string, string[]>>(StringComparer.OrdinalIgnoreCase);
                                    _aradiaResponseData[animationName][eventName] = mindBrokenLevels;
                                }
                                else if (eventName == "ARADIA_THOUGHT")
                                {
                                    if (!_aradiaThoughtData.ContainsKey(animationName))
                                        _aradiaThoughtData[animationName] = new Dictionary<string, Dictionary<string, string[]>>(StringComparer.OrdinalIgnoreCase);
                                    _aradiaThoughtData[animationName][eventName] = mindBrokenLevels;
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Load data ARADIA_RESPONSE
    /// </summary>
    private static void LoadAradiaResponseData(Dictionary<string, object> data)
    {
        try
        {
            // Load settings ARADIA_RESPONSE
            if (data.ContainsKey("settings"))
            {
                var settings = data["settings"] as Dictionary<string, object>;
                if (settings != null)
                {
                    foreach (var setting in settings)
                    {
                        if (setting.Value != null && float.TryParse(setting.Value.ToString(), out float value))
                        {
                            _aradiaResponseSettings[setting.Key] = value;
                        }
                    }
                }
            }

            // Load animations ARADIA_RESPONSE
            if (data.ContainsKey("animations"))
            {
                var animations = data["animations"] as Dictionary<string, object>;
                if (animations != null)
                {
                    LoadAradiaResponseAnimations(animations);
                }
            }
        }
        catch (Exception ex)
        {
            // ARADIA_RESPONSE data loading failed silently
        }
    }

    /// <summary>
    /// Load data ARADIA_THOUGHT
    /// </summary>
    private static void LoadAradiaThoughtData(Dictionary<string, object> data)
    {
        try
        {
            // Load settings ARADIA_THOUGHT
            if (data.ContainsKey("settings"))
            {
                var settings = data["settings"] as Dictionary<string, object>;
                if (settings != null)
                {
                    foreach (var setting in settings)
                    {
                        if (setting.Value != null && float.TryParse(setting.Value.ToString(), out float value))
                        {
                            _aradiaThoughtSettings[setting.Key] = value;
                        }
                    }
                }
            }

            // Load animations ARADIA_THOUGHT
            if (data.ContainsKey("animations"))
            {
                var animations = data["animations"] as Dictionary<string, object>;
                if (animations != null)
                {
                    LoadAradiaThoughtAnimations(animations);
                }
            }
        }
        catch (Exception ex)
        {
            // ARADIA_THOUGHT data loading failed silently
        }
    }

    /// <summary>
    /// Загрузка анимаций ARADIA_RESPONSE
    /// </summary>
    private static void LoadAradiaResponseAnimations(Dictionary<string, object> animations)
    {
        foreach (var animKvp in animations)
        {
            string animationName = animKvp.Key;
            var animationData = animKvp.Value as Dictionary<string, object>;

            if (animationData?.ContainsKey("events") == true)
            {
                var events = animationData["events"] as Dictionary<string, object>;
                if (events != null)
                {
                    if (!_aradiaResponseData.ContainsKey(animationName))
                    {
                        _aradiaResponseData[animationName] = new Dictionary<string, Dictionary<string, string[]>>(StringComparer.OrdinalIgnoreCase);
                    }

                    LoadAradiaAnimationEvents(animationName, events, _aradiaResponseData[animationName]);
                }
            }
        }
    }

    /// <summary>
    /// Загрузка анимаций ARADIA_THOUGHT
    /// </summary>
    private static void LoadAradiaThoughtAnimations(Dictionary<string, object> animations)
    {
        foreach (var animKvp in animations)
        {
            string animationName = animKvp.Key;
            var animationData = animKvp.Value as Dictionary<string, object>;

            if (animationData?.ContainsKey("events") == true)
            {
                var events = animationData["events"] as Dictionary<string, object>;
                if (events != null)
                {
                    if (!_aradiaThoughtData.ContainsKey(animationName))
                    {
                        _aradiaThoughtData[animationName] = new Dictionary<string, Dictionary<string, string[]>>(StringComparer.OrdinalIgnoreCase);
                    }

                    LoadAradiaAnimationEvents(animationName, events, _aradiaThoughtData[animationName]);
                }
            }
        }
    }

    /// <summary>
    /// Загрузка событий for GG анимаций
    /// </summary>
    private static void LoadAradiaAnimationEvents(string animationName, Dictionary<string, object> events, Dictionary<string, Dictionary<string, string[]>> targetData)
    {
        foreach (var eventKvp in events)
        {
            string eventName = eventKvp.Key;
            var eventData = eventKvp.Value as Dictionary<string, object>;

            if (eventData != null)
            {
                // Process уровни mindBroken (low, medium, high)
                var mindBrokenLevels = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

                foreach (var levelKvp in eventData)
                {
                    string levelName = levelKvp.Key;
                    var levelDialogues = ConvertToStringArray(levelKvp.Value);
                    if (levelDialogues != null && levelDialogues.Length > 0)
                    {
                        mindBrokenLevels[levelName] = levelDialogues;
                    }
                }

                if (mindBrokenLevels.Count > 0)
                {
                    targetData[eventName] = mindBrokenLevels;
                }
            }
        }
    }

    /// <summary>
    /// Parsing цвета from словаря JSON
    /// </summary>
// ColorParser используется for парсинга цветов

    /// <summary>
    /// Processing ономатопей for goblins

    /// <summary>
    /// Получение кости for onomatopoeia goblin




    /// <summary>
    /// Get default dialogue for events (fallback)
    /// </summary>
    private static string GetDefaultDialogue(string eventName)
    {
        return eventName switch
        {
            "START" => "Ке-ке-ке! Попала к гоблинам!",
            "2ERO_iki" => "Ке-ке-ке! Кончаем! Принимай!",
            "2ERO_iki2" => "Еще больше спермы!",
            _ => "Ке-ке-ке!"
        };
    }
    
    /// <summary>
    /// Check initialization
    /// </summary>
    internal static bool IsInitialized => _initialized;
    
    /// <summary>
    /// Reset system for reload with new language
    /// </summary>
    internal static void Reset()
    {
        _initialized = false;
        _dialogueData?.Clear();
        _segmentedDialogues?.Clear();
    }
}