using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using BepInEx;
using NoREroMod;
using NoREroMod.Patches.UI.MindBroken;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// PC (Aradia) response system to TouzokuNormal lines during H-scenes - one response per enemy line after 2 seconds
/// </summary>
internal static class AradiaTouzokuNormalDialogues
{
    // Parsed chain data: animationKey -> ordered steps
    private static readonly Dictionary<string, List<ChainStep>> _chainsByAnimation = new();
    private static bool _initialized = false;

    // Coroutine runner for delayed responses
    private static MonoBehaviour _coroutineRunner = null;

    private sealed class ChainStep
    {
        public int StepNumber;
        public float DelayFromStart;
        public Dictionary<string, List<string>> ThoughtsByLevel = new();
    }

    // MindBroken thresholds (7 levels for more granularity)
    private static float[] _mindBrokenThresholds = { 0.0f, 0.15f, 0.3f, 0.5f, 0.7f, 0.85f, 1.0f };
    private static string[] _mindBrokenLevels = { "very_low", "low", "medium_low", "medium", "medium_high", "high", "very_high" };

    // Reference to DialogueDisplay
    private static DialogueDisplay _display = null;

    // Display settings
    private static float _displayDuration = 5.0f;

    /// <summary>
    /// Initialize the TouzokuNormal dialogue system
    /// </summary>
    internal static void Initialize()
    {
        if (_initialized) return;

        try
        {
            // Create coroutine runner for delayed responses
            GameObject runnerObj = new GameObject("AradiaTouzokuNormalCoroutineRunner_XUAIGNORE");
            UnityEngine.Object.DontDestroyOnLoad(runnerObj);
            _coroutineRunner = runnerObj.AddComponent<DialogueCoroutineRunner>();

            LoadAradiaHSceneData();
            LoadAradiaData();
            _initialized = true;
        }
        catch (Exception ex)
        {
            // Initialization failed silently
        }
    }

    /// <summary>
    /// Установка DialogueDisplay for отображения
    /// </summary>
    internal static void SetDisplay(DialogueDisplay display)
    {
        _display = display;
    }

    /// <summary>
    /// Process TouzokuNormal H-scene events - one response per enemy line after 2 seconds
    /// </summary>
    internal static void ProcessEnemyComment(object enemyInstance, string animationName, string eventName, int seCount)
    {
        if (!_initialized || _display == null || enemyInstance == null)
        {
            return;
        }

        try
        {
            // Skip SE events to prevent spam
            if (eventName == "SE")
            {
                return;
            }

            string phaseKey = MapAnimationToKey(animationName);
            if (string.IsNullOrEmpty(phaseKey) || !_chainsByAnimation.ContainsKey(phaseKey))
            {
                return;
            }

            // Get thought for this animation
            var steps = _chainsByAnimation[phaseKey];
            if (steps == null || steps.Count == 0)
            {
                return;
            }

            // Get first step for responses
            var step = steps[0];
            float mindBrokenPercent = NoREroMod.Patches.UI.MindBroken.MindBrokenSystem.Percent;
            string mindBrokenLevel = GetMindBrokenLevel(mindBrokenPercent);

            if (!step.ThoughtsByLevel.TryGetValue(mindBrokenLevel, out var list) || list == null || list.Count == 0)
            {
                return;
            }

            string response = list[UnityEngine.Random.Range(0, list.Count)];

            // Debug logging
            // Plugin.Log.LogInfo($"[AradiaTouzokuNormal] Enemy spoke, responding in 2 seconds: '{response}'");

            // Show response after 2 seconds delay
            _coroutineRunner.StartCoroutine(DelayedAradiaResponse(enemyInstance, response));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[AradiaTouzokuNormal] Exception in ProcessEnemyComment: {ex.Message}");
        }
    }

    /// <summary>
    /// Coroutine to delay Aradia response by 2 seconds
    /// </summary>
    private static System.Collections.IEnumerator DelayedAradiaResponse(object enemyInstance, string response)
    {
        yield return new WaitForSeconds(2.0f);
        ShowAradiaResponse(enemyInstance, response);
    }

    /// <summary>
    /// Map real H-scene animation names to Aradia JSON keys.
    /// </summary>
    private static string MapAnimationToKey(string animationName)
    {
        if (string.IsNullOrEmpty(animationName))
            return animationName;

        switch (animationName.ToUpperInvariant())
        {
            case "START":
            case "START2":
            case "START3":
            case "START4":
            case "START5":
                return "START";

            case "ERO":
            case "2ERO":
                return "ERO";

            case "ERO1":
                return "ERO1";

            case "ERO2":
            case "ERO3":
            case "ERO4":
            case "ERO5":
            case "2ERO2":
                return "ERO2";

            case "FIN":
            case "FIN2":
                return "FIN";

            case "JIGO":
            case "JIGO2":
                return "JIGO";
        }

        return animationName;
    }

    /// <summary>
    /// Get MindBroken level name from percentage.
    /// </summary>
    private static string GetMindBrokenLevel(float percent)
    {
        for (int i = _mindBrokenThresholds.Length - 1; i >= 0; i--)
        {
            if (percent >= _mindBrokenThresholds[i])
            {
                return _mindBrokenLevels[i];
            }
        }
        return _mindBrokenLevels[0];
    }

    /// <summary>
    /// Render Aradia thought as floating UI text.
    /// </summary>
    private static void ShowAradiaResponse(object enemyInstance, string response)
    {
        try
        {
            if (_display == null || string.IsNullOrEmpty(response))
                return;

            // Get bone based on enemy type (single response system doesn't track animation changes)
            string boneName = DialogueDisplay.GetAradiaBoneForAnimation(enemyInstance, "");
            float verticalOffset = DialogueDisplay.GetDefaultAradiaVerticalOffset();

            var style = DialogueDisplay.BuildAradiaUnifiedStyle(verticalOffset, 0.0f, true);
            Plugin.Log.LogInfo($"[TouzokuNormal] Showing response with color: {style.Color}, outline: {style.OutlineColor}, bold: {style.IsBold}, italic: {style.IsItalic}");
            _display.ShowAradiaResponse(enemyInstance, response, boneName, style, _displayDuration);
        }
        catch (Exception ex)
        {
            // Response display failed silently
        }
    }

    /// <summary>
    /// Get path к данным considering selected language
    /// </summary>
    private static string GetDataPath()
    {
        string languageCode = Plugin.hellGateLanguage?.Value ?? "EN";
        if (string.IsNullOrEmpty(languageCode))
        {
            languageCode = "EN"; // Fallback
        }
        return Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), languageCode);
    }

    /// <summary>
    /// Reset the TouzokuNormal dialogue system
    /// </summary>
    internal static void Reset()
    {
        _initialized = false;
        _chainsByAnimation?.Clear();
    }

    /// <summary>
    /// Load H-scene dialogue data from JSON
    /// </summary>
    private static void LoadAradiaHSceneData()
    {
        try
        {
            string dataPath = GetDataPath();
            string jsonPath = Path.Combine(dataPath, "TouzokuNormalHSceneData.json");

            if (!File.Exists(jsonPath))
            {
                return;
            }

            string jsonText = File.ReadAllText(jsonPath);
            ParseJsonManually(jsonText);
        }
        catch (Exception ex)
        {
            // H-scene data loading failed silently
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
            string jsonPath = Path.Combine(dataPath, "AradiaTouzokuNormal.json");

            if (!File.Exists(jsonPath))
            {
                return;
            }

            string jsonText = File.ReadAllText(jsonPath);
            LoadAradiaDialoguesFromJson(jsonText);
        }
        catch (Exception ex)
        {
            // Aradia data loading failed silently
        }
    }

    /// <summary>
    /// Manual JSON parsing for dialogues
    /// </summary>
    private static Dictionary<string, object> ParseJsonManually(string jsonText)
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

            return result;
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Parse settings section
    /// </summary>
    private static Dictionary<string, float> ParseSettingsSection(string settingsContent)
    {
        var settings = new Dictionary<string, float>();

        try
        {
            // Parse displayDuration
            var durationMatch = Regex.Match(settingsContent, "\"displayDuration\"\\s*:\\s*([0-9.]+)");
            if (durationMatch.Success && float.TryParse(durationMatch.Groups[1].Value, out float duration))
            {
                _displayDuration = duration;
            }

            // Parse other settings if needed
        }
        catch
        {
            // Settings parsing failed
        }

        return settings;
    }

    /// <summary>
    /// Parse animations section
    /// </summary>
    private static Dictionary<string, object> ParseAnimationsSection(string animationsContent)
    {
        var animations = new Dictionary<string, object>();

        try
        {
            // Parse each animation
            var animMatches = Regex.Matches(animationsContent, "\"([^\"]+)\"\\s*:\\s*\\{");
            foreach (Match animMatch in animMatches)
            {
                string animName = animMatch.Groups[1].Value;
                var animStart = animMatch.Index + animMatch.Length - 1;

                if (animStart >= animationsContent.Length) continue;

                var animEnd = FindMatchingBrace(animationsContent, animStart);
                if (animEnd == -1) continue;

                var animSection = animationsContent.Substring(animStart, animEnd - animStart + 1);
                var animationData = ParseAnimationSection(animSection);
                if (animationData != null)
                {
                    animations[animName] = animationData;
                }
            }
        }
        catch
        {
            // Animations parsing failed
        }

        return animations;
    }

    /// <summary>
    /// Parse single animation section
    /// </summary>
    private static Dictionary<string, object> ParseAnimationSection(string animSection)
    {
        var animationData = new Dictionary<string, object>();

        try
        {
            // Parse events
            var eventsStart = animSection.IndexOf("\"events\":");
            if (eventsStart >= 0)
            {
                var eventsBraceStart = animSection.IndexOf('{', eventsStart);
                if (eventsBraceStart >= 0)
                {
                    var eventsEnd = FindMatchingBrace(animSection, eventsBraceStart);
                    if (eventsEnd > eventsBraceStart)
                    {
                        var eventsSection = animSection.Substring(eventsBraceStart, eventsEnd - eventsBraceStart + 1);
                        var events = ParseEventsSection(eventsSection);
                        if (events != null && events.Count > 0)
                        {
                            animationData["events"] = events;
                        }
                    }
                }
            }
        }
        catch
        {
            // Animation parsing failed
        }

        return animationData;
    }

    /// <summary>
    /// Parse events section
    /// </summary>
    private static Dictionary<string, object> ParseEventsSection(string eventsSection)
    {
        var events = new Dictionary<string, object>();

        try
        {
            var eventMatches = Regex.Matches(eventsSection, "\"([^\"]+)\"\\s*:\\s*\\[");
            foreach (Match eventMatch in eventMatches)
            {
                string eventName = eventMatch.Groups[1].Value;
                var eventStart = eventMatch.Index + eventMatch.Length;

                if (eventStart >= eventsSection.Length) continue;

                var eventEnd = eventsSection.IndexOf("]", eventStart);
                if (eventEnd == -1) continue;

                var eventContent = eventsSection.Substring(eventStart, eventEnd - eventStart);
                var phrases = ParsePhrases(eventContent);
                if (phrases != null && phrases.Count > 0)
                {
                    events[eventName] = phrases;
                }
            }
        }
        catch
        {
            // Events parsing failed
        }

        return events;
    }

    /// <summary>
    /// Parse phrases array
    /// </summary>
    private static List<string> ParsePhrases(string phrasesContent)
    {
        var phrases = new List<string>();

        try
        {
            var phraseMatches = Regex.Matches(phrasesContent, "\"([^\"]+)\"");
            foreach (Match phraseMatch in phraseMatches)
            {
                phrases.Add(phraseMatch.Groups[1].Value);
            }
        }
        catch
        {
            // Phrases parsing failed
        }

        return phrases;
    }

    /// <summary>
    /// Load Aradia dialogues from JSON
    /// </summary>
    private static void LoadAradiaDialoguesFromJson(string jsonText)
    {
        try
        {
            // Parse settings
            string settingsPattern = "\"settings\"\\s*:\\s*\\{([^}]*)\\}";
            Match settingsMatch = Regex.Match(jsonText, settingsPattern, RegexOptions.Singleline);

            if (settingsMatch.Success)
            {
                string settingsSection = settingsMatch.Groups[1].Value;

                // Font size
                Match fontSizeMatch = Regex.Match(settingsSection, "\"fontSize\"\\s*:\\s*([0-9.]+)");
                if (fontSizeMatch.Success)
                {
                    // Font size is now centralized in DialogueDisplay
                }

                // Vertical offset and bone name are fixed by system (bone14 + 100px).
            }

            // Parse animations
            ParseAnimationResponses(jsonText);
        }
        catch (Exception ex)
        {
            // Dialogues loading failed silently
        }
    }

    /// <summary>
    /// Parsing секции animations
    /// </summary>
    private static void ParseAnimationResponses(string jsonText)
    {
        try
        {
            // Ищем начало секции animations
            string animationsPattern = "\"animations\"\\s*:\\s*\\{";
            Match animationsMatch = Regex.Match(jsonText, animationsPattern);

            if (!animationsMatch.Success)
            {
                return;
            }

            int animationsStart = animationsMatch.Index + animationsMatch.Length;
            string remainingJson = jsonText.Substring(animationsStart);

            // Парсим каждую animation
            var animMatches = Regex.Matches(remainingJson, "\"([^\"]+)\"\\s*:\\s*\\{");
            foreach (Match animMatch in animMatches)
            {
                string animationName = animMatch.Groups[1].Value;
                var animStart = animMatch.Index + animMatch.Length - 1;

                if (animStart >= remainingJson.Length) continue;

                var animEnd = FindMatchingBrace(remainingJson, animStart);
                if (animEnd == -1) continue;

                var animSection = remainingJson.Substring(animStart, animEnd - animStart + 1);
                ParseSingleAnimation(animationName, animSection);
            }
        }
        catch (Exception ex)
        {
            // Animation responses parsing failed silently
        }
    }

    /// <summary>
    /// Parse single animation section
    /// </summary>
    private static void ParseSingleAnimation(string animationName, string animSection)
    {
        try
        {
            // Check if this animation has a "chain" structure
            var chainStart = animSection.IndexOf("\"chain\":");
            if (chainStart != -1)
            {
                // Parse chain structure
                var chainBraceStart = animSection.IndexOf("[", chainStart);
                if (chainBraceStart != -1)
                {
                    var chainEnd = FindMatchingBracket(animSection, chainBraceStart);
                    if (chainEnd != -1)
                    {
                        var chainSection = animSection.Substring(chainBraceStart, chainEnd - chainBraceStart + 1);
                        ParseChainForAnimation(animationName, chainSection);
                    }
                }
                return;
            }

            // For non-chain animations, create a simple step
            var step = new ChainStep
            {
                StepNumber = 1,
                DelayFromStart = 0f,
                ThoughtsByLevel = new Dictionary<string, List<string>>()
            };

            // Add empty thoughts for all levels (will be filled from events if they exist)
            foreach (string level in _mindBrokenLevels)
            {
                step.ThoughtsByLevel[level] = new List<string> { "..." };
            }

            _chainsByAnimation[animationName] = new List<ChainStep> { step };
        }
        catch (Exception ex)
        {
            // Single animation parsing failed silently
        }
    }

    /// <summary>
    /// Parse chain for animation
    /// </summary>
    private static void ParseChainForAnimation(string animationName, string chainSection)
    {
        try
        {
            var steps = new List<ChainStep>();
            var stepMatches = Regex.Matches(chainSection, "\\{([^}]*)\\}");

            foreach (Match stepMatch in stepMatches)
            {
                var step = new ChainStep();
                string stepContent = stepMatch.Groups[1].Value;

                // Parse step number
                var stepMatch_ = Regex.Match(stepContent, "\"step\"\\s*:\\s*([0-9]+)");
                if (stepMatch_.Success)
                {
                    step.StepNumber = int.Parse(stepMatch_.Groups[1].Value);
                }

                // Parse delay
                var delayMatch = Regex.Match(stepContent, "\"delay\"\\s*:\\s*([0-9.]+)");
                if (delayMatch.Success)
                {
                    step.DelayFromStart = float.Parse(delayMatch.Groups[1].Value);
                }

                // Parse thoughts
                step.ThoughtsByLevel = ParseThoughtsByLevel(stepContent);
                steps.Add(step);
            }

            _chainsByAnimation[animationName] = steps;
        }
        catch (Exception ex)
        {
            // Chain parsing failed silently
        }
    }

    /// <summary>
    /// Parse thoughts by level from step content
    /// </summary>
    private static Dictionary<string, List<string>> ParseThoughtsByLevel(string stepContent)
    {
        var thoughtsByLevel = new Dictionary<string, List<string>>();

        try
        {
            // Parse each mind broken level
            foreach (string level in _mindBrokenLevels)
            {
                var levelPattern = $"\"{level}\"\\s*:\\s*\\[([^]]*?)\\]";
                var levelMatch = Regex.Match(stepContent, levelPattern, RegexOptions.Singleline);

                if (levelMatch.Success)
                {
                    var levelContent = levelMatch.Groups[1].Value;
                    var thoughtMatches = Regex.Matches(levelContent, "\"([^\"]+)\"");

                    var thoughts = new List<string>();
                    foreach (Match thoughtMatch in thoughtMatches)
                    {
                        thoughts.Add(thoughtMatch.Groups[1].Value);
                    }

                    thoughtsByLevel[level] = thoughts;
                }
                else
                {
                    thoughtsByLevel[level] = new List<string>();
                }
            }
        }
        catch (Exception ex)
        {
            // Thoughts parsing failed
            foreach (string level in _mindBrokenLevels)
            {
                thoughtsByLevel[level] = new List<string>();
            }
        }

        return thoughtsByLevel;
    }

    /// <summary>
    /// Find matching closing bracket for arrays
    /// </summary>
    private static int FindMatchingBracket(string text, int startIndex)
    {
        if (startIndex < 0 || startIndex >= text.Length || text[startIndex] != '[')
            return -1;

        int braceCount = 1;
        for (int i = startIndex + 1; i < text.Length; i++)
        {
            if (text[i] == '[')
                braceCount++;
            else if (text[i] == ']')
            {
                braceCount--;
                if (braceCount == 0)
                    return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Find matching closing brace for objects
    /// </summary>
    private static int FindMatchingBrace(string text, int startIndex)
    {
        if (startIndex < 0 || startIndex >= text.Length || text[startIndex] != '{')
            return -1;

        int braceCount = 1;
        for (int i = startIndex + 1; i < text.Length; i++)
        {
            if (text[i] == '{')
                braceCount++;
            else if (text[i] == '}')
            {
                braceCount--;
                if (braceCount == 0)
                    return i;
            }
        }
        return -1;
    }
}