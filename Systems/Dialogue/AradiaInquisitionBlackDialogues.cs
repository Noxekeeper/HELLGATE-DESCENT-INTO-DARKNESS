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
/// Aradia's thoughts system for InquisitionBlack H-scenes - one response per enemy line after 2 seconds
/// </summary>
internal static class AradiaInquisitionBlackDialogues
{
    // InquisitionBlack thoughts data (PC cannot speak during oral violence)
    private static Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>> _inquisitionBlackThoughts = new();

    // Chain dialogue tracking for sequential thoughts
    private static Dictionary<object, ChainState> _activeChains = new();

    // Cooldown system for thoughts to prevent spam
    private static Dictionary<object, float> _lastThoughtTime = new();
    private static float _thoughtCooldown = 1.5f; // 1.5 seconds between thoughts

    private static DialogueDisplay _display;
    private static bool _initialized = false;

    // MindBroken thresholds (7 levels for more granularity)
    private static float[] _mindBrokenThresholds = { 0.0f, 0.15f, 0.3f, 0.5f, 0.7f, 0.85f, 1.0f };
    private static string[] _mindBrokenLevels = { "very_low", "low", "medium_low", "medium", "medium_high", "high", "very_high" };

    // Display settings
    private static float _displayDuration = 5.0f;
    private static float _verticalOffset = 100.0f;

    /// <summary>
    /// Initialize the InquisitionBlack dialogue system
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        try
        {
            LoadInquisitionBlackThoughtsData();
            _initialized = true;
        }
        catch (Exception ex)
        {
            // InquisitionBlack initialization failed silently
        }
    }

    /// <summary>
    /// Set the display reference for InquisitionBlack dialogue system
    /// </summary>
    public static void SetDisplay(DialogueDisplay display)
    {
        _display = display;
    }

    /// <summary>
    /// Reset the InquisitionBlack dialogue system
    /// </summary>
    public static void Reset()
    {
        _initialized = false;
        _inquisitionBlackThoughts?.Clear();
    }

    /// <summary>
    /// Process InquisitionBlack events with Aradia thoughts - one response per enemy line after 2 seconds
    /// </summary>
    public static void ProcessInquisitionBlackAradiaEvent(object enemyInstance, string animationName, string eventName, int seCount)
    {
        if (_display == null || enemyInstance == null)
        {
            return;
        }

        try
        {
            // Only show thoughts for main animation events, not for SE spam events
            if (eventName == "SE")
            {
                return; // Skip SE events to prevent spam
            }

            // Determine which animation data to use based on animation and event
            string dataKey = GetDataKeyForEvent(animationName, eventName, seCount);

            if (string.IsNullOrEmpty(dataKey) || !_inquisitionBlackThoughts.ContainsKey(dataKey))
            {
                return;
            }

            // Handle chain dialogues
            if (ShouldStartChain(dataKey, eventName))
            {
                StartOrContinueChain(enemyInstance, dataKey, eventName, seCount);
                return;
            }

            // Handle regular events
            float mindBrokenPercent = NoREroMod.Patches.UI.MindBroken.MindBrokenSystem.Percent;
            string mindBrokenLevel = GetMindBrokenLevel(mindBrokenPercent);

            // For InquisitionBlack, use animationName as both keys since data is stored as animationName.animationName
            string thought = GetInquisitionBlackThought(dataKey, dataKey, mindBrokenLevel, seCount);

            if (!string.IsNullOrEmpty(thought))
            {
                ShowAradiaThought(enemyInstance, thought);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[AradiaInquisitionBlack] Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Coroutine to delay Aradia response by 2 seconds
    /// </summary>
    private static System.Collections.IEnumerator DelayedAradiaResponse(object enemyInstance, string thought)
    {
        // Plugin.Log.LogInfo($"[AradiaInquisitionBlack] Waiting 2 seconds before showing thought...");
        yield return new WaitForSeconds(2.0f);
        // Plugin.Log.LogInfo($"[AradiaInquisitionBlack] Showing thought after 2s delay: '{thought?.Substring(0, Math.Min(50, thought.Length)) ?? "null"}...'");
        ShowAradiaThought(enemyInstance, thought);
    }


    /// <summary>
    /// Get data key for event
    /// </summary>
    private static string GetDataKeyForEvent(string animationName, string eventName, int seCount)
    {
        // Map animation + event combinations to data keys
        switch (animationName)
        {
            case "START":
                return "START";
            case "START2":
                if (eventName == "START2")
                    return "START2";
                return "START2";
            case "ERO":
                if (eventName == "ERO")
                    return "ERO";
                if (eventName == "ERO1")
                    return "ERO1";
                return "ERO";
            case "ERO1":
                if (eventName == "ERO1")
                    return "ERO1";
                return "ERO1";
            case "ERO2":
                if (eventName == "ERO2")
                    return "ERO2";
                if (eventName == "ERO3")
                    return "ERO3";
                return "ERO2";
            case "ERO3":
                return "ERO3";
            case "ERO4":
                return "ERO4";
            case "FIN":
                return "FIN";
            case "FIN2":
                return "FIN2";
            case "JIGO":
                return "JIGO";
            case "JIGO2":
                return "JIGO2";
            case "JIGOFIN":
                return "JIGOFIN";
            case "JIGOFIN2":
                return "JIGOFIN2";
            case "STRUGGLE":
                return "STRUGGLE";
            default:
                return animationName;
        }
    }

    /// <summary>
    /// Get MindBroken level based on percentage
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
    /// Load InquisitionBlack thoughts data from JSON
    /// </summary>
    private static void LoadInquisitionBlackThoughtsData()
    {
        try
        {
            string languageCode = Plugin.hellGateLanguage?.Value ?? "EN";
            if (string.IsNullOrEmpty(languageCode))
            {
                languageCode = "EN"; // Fallback
            }
            string dataPath = Path.Combine(Path.Combine(Paths.PluginPath, "HellGateJson"), languageCode);
            string jsonPath = Path.Combine(dataPath, "AradiaInquisitionBlack.json");

            // Plugin.Log.LogInfo($"[AradiaInquisitionBlack] Loading data from: {jsonPath}");

            if (!File.Exists(jsonPath))
            {
                Plugin.Log.LogError($"[AradiaInquisitionBlack] JSON file not found: {jsonPath}");
                return;
            }

            // Plugin.Log.LogInfo($"[AradiaInquisitionBlack] JSON file exists, reading...");

            string jsonText = File.ReadAllText(jsonPath);

            // Parse settings (vertical offset, display duration)
            Match settingsMatch = Regex.Match(jsonText, "\"settings\"\\s*:\\s*\\{([^}]*)\\}", RegexOptions.Singleline);
            if (settingsMatch.Success)
            {
                string settingsSection = settingsMatch.Groups[1].Value;

                Match verticalOffsetMatch = Regex.Match(settingsSection, "\"verticalOffset\"\\s*:\\s*([0-9.]+)");
                if (verticalOffsetMatch.Success && float.TryParse(verticalOffsetMatch.Groups[1].Value, out float offsetValue))
                {
                    _verticalOffset = offsetValue;
                }

                Match displayDurationMatch = Regex.Match(settingsSection, "\"displayDuration\"\\s*:\\s*([0-9.]+)");
                if (displayDurationMatch.Success && float.TryParse(displayDurationMatch.Groups[1].Value, out float durationValue))
                {
                    _displayDuration = durationValue;
                }
            }

            LoadInquisitionBlackThoughtsFromJson(jsonText);
        }
        catch (Exception ex)
        {
            // InquisitionBlack thoughts loading failed silently
        }
    }

    /// <summary>
    /// Load InquisitionBlack thoughts from JSON text
    /// </summary>
    private static void LoadInquisitionBlackThoughtsFromJson(string jsonText)
    {
        try
        {
            // Simple JSON parsing for animations section
            var animationsStart = jsonText.IndexOf("\"animations\":");
            if (animationsStart == -1) return;

            var animationsEnd = FindMatchingBrace(jsonText, jsonText.IndexOf("{", animationsStart));
            if (animationsEnd == -1) return;

            var animationsSection = jsonText.Substring(animationsStart, animationsEnd - animationsStart + 1);

            // Parse each animation
            var animMatches = Regex.Matches(animationsSection, "\"([^\"]+)\"\\s*:\\s*\\{");

            foreach (Match animMatch in animMatches)
            {
                string animationName = animMatch.Groups[1].Value;
                var animStart = animMatch.Index + animMatch.Length - 1;

                if (animStart >= animationsSection.Length) continue;

                var animEnd = FindMatchingBrace(animationsSection, animStart);
                if (animEnd == -1) continue;

                var animSection = animationsSection.Substring(animStart, animEnd - animStart + 1);
                ParseInquisitionBlackAnimation(animationName, animSection);
            }
        }
        catch (Exception ex)
        {
            // InquisitionBlack thoughts JSON parsing failed silently
        }
    }

    /// <summary>
    /// Parse single animation section
    /// </summary>
    private static void ParseInquisitionBlackAnimation(string animationName, string animSection)
    {
        try
        {
            // Check if this animation has a "chain" structure (ERO1, ERO2)
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
                        // For now, treat chain as regular event - can be expanded later
                        ParseInquisitionBlackEvent(animationName, animationName, "{\"chain_data\":" + chainSection + "}");
                    }
                }
                return;
            }

            // Parse regular events structure
            var eventsStart = animSection.IndexOf("\"events\":");
            if (eventsStart == -1) return;

            var eventsBraceStart = animSection.IndexOf("{", eventsStart);
            if (eventsBraceStart == -1) return;

            var eventsEnd = FindMatchingBrace(animSection, eventsBraceStart);
            if (eventsEnd == -1) return;

            var eventsSection = animSection.Substring(eventsBraceStart, eventsEnd - eventsBraceStart + 1);

            // Parse each event
            var eventMatches = Regex.Matches(eventsSection, "\"([^\"]+)\"\\s*:\\s*\\{");

            foreach (Match eventMatch in eventMatches)
            {
                string eventName = eventMatch.Groups[1].Value;
                var eventStart = eventMatch.Index + eventMatch.Length - 1;

                if (eventStart >= eventsSection.Length) continue;

                var eventEnd = FindMatchingBrace(eventsSection, eventStart);
                if (eventEnd == -1) continue;

                var eventSection = eventsSection.Substring(eventStart, eventEnd - eventStart + 1);
                ParseInquisitionBlackEvent(animationName, eventName, eventSection);
            }
        }
        catch (Exception ex)
        {
            // Animation parsing failed silently
        }
    }

    /// <summary>
    /// Parse single event section
    /// </summary>
    private static void ParseInquisitionBlackEvent(string animationName, string eventName, string eventSection)
    {
        try
        {
            // Plugin.Log.LogInfo($"[AradiaInquisitionBlack] Parsing event '{eventName}' in animation '{animationName}'");

            // Parse each mind broken level
            var levelMatches = Regex.Matches(eventSection, "\"(very_low|low|medium_low|medium|medium_high|high|very_high)\"\\s*:\\s*\\[");
            // Plugin.Log.LogInfo($"[AradiaInquisitionBlack] Found {levelMatches.Count} mind broken levels");

            foreach (Match levelMatch in levelMatches)
            {
                string levelName = levelMatch.Groups[1].Value;
                var levelStart = levelMatch.Index + levelMatch.Length;

                if (levelStart >= eventSection.Length) continue;

                var levelEnd = eventSection.IndexOf("]", levelStart);
                if (levelEnd == -1) continue;

                var levelSection = eventSection.Substring(levelStart, levelEnd - levelStart);

                // Parse thoughts array
                var thoughts = new List<string>();
                var thoughtMatches = Regex.Matches(levelSection, "\"([^\"]+)\"");

                foreach (Match thoughtMatch in thoughtMatches)
                {
                    thoughts.Add(thoughtMatch.Groups[1].Value);
                }

                // Store in data structure
                if (!_inquisitionBlackThoughts.ContainsKey(animationName))
                    _inquisitionBlackThoughts[animationName] = new Dictionary<string, Dictionary<string, List<string>>>();

                if (!_inquisitionBlackThoughts[animationName].ContainsKey(eventName))
                    _inquisitionBlackThoughts[animationName][eventName] = new Dictionary<string, List<string>>();

                _inquisitionBlackThoughts[animationName][eventName][levelName] = thoughts;

                // Plugin.Log.LogInfo($"[AradiaInquisitionBlack] Added {thoughts.Count} thoughts for {animationName}.{eventName}.{levelName}");
            }
        }
        catch (Exception ex)
        {
            // Event parsing failed silently
        }
    }

    /// <summary>
    /// Get thought for InquisitionBlack event
    /// </summary>
    private static string GetInquisitionBlackThought(string animationName, string eventName, string mindBrokenLevel, int seCount)
    {
        try
        {
            if (!_inquisitionBlackThoughts.ContainsKey(animationName))
                return null;

            var animData = _inquisitionBlackThoughts[animationName];
            if (!animData.ContainsKey(eventName))
                return null;

            var levelData = animData[eventName];
            if (!levelData.ContainsKey(mindBrokenLevel))
                return null;

            var thoughts = levelData[mindBrokenLevel];
            if (thoughts == null || thoughts.Count == 0)
                return null;

            return thoughts[UnityEngine.Random.Range(0, thoughts.Count)];
        }
        catch
        {
            return null;
        }
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

    /// <summary>
    /// Chain state for sequential dialogue steps
    /// </summary>
    internal class ChainState
    {
        public string AnimationName { get; set; }
        public string EventName { get; set; }
        public int CurrentStep { get; set; }
        public float LastStepTime { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Check if we should start a chain for this animation+event
    /// </summary>
    private static bool ShouldStartChain(string animationName, string eventName)
    {
        // Start chains for specific animation+event combinations
        return (animationName == "ERO1" && eventName == "ERO1") ||
               (animationName == "ERO2" && eventName == "ERO2");
    }

    /// <summary>
    /// Start or continue a dialogue chain
    /// </summary>
    private static void StartOrContinueChain(object enemyInstance, string animationName, string eventName, int seCount)
    {
        try
        {
            // Check if chain already exists for this enemy
            if (_activeChains.ContainsKey(enemyInstance))
            {
                var existingChain = _activeChains[enemyInstance];
                if (existingChain.AnimationName == animationName && existingChain.IsActive)
                {
                    // Continue existing chain
                    ContinueChain(enemyInstance, existingChain);
                    return;
                }
                else
                {
                    // Clean up old chain
                    _activeChains.Remove(enemyInstance);
                }
            }

            // Start new chain
            var chainState = new ChainState
            {
                AnimationName = animationName,
                EventName = eventName,
                CurrentStep = 1,
                LastStepTime = UnityEngine.Time.time,
                IsActive = true
            };

            _activeChains[enemyInstance] = chainState;

            // Show first step
            ShowChainStep(enemyInstance, chainState, 1);
        }
        catch (Exception ex)
        {
            // Chain start failed silently
        }
    }

    /// <summary>
    /// Continue existing chain to next step
    /// </summary>
    private static void ContinueChain(object enemyInstance, ChainState chainState)
    {
        try
        {
            float currentTime = UnityEngine.Time.time;
            float timeSinceLastStep = currentTime - chainState.LastStepTime;

            // Check if enough time has passed for next step
            float stepDelay = GetChainStepDelay(chainState.AnimationName, chainState.CurrentStep + 1);

            if (timeSinceLastStep >= stepDelay)
            {
                chainState.CurrentStep++;
                chainState.LastStepTime = currentTime;

                ShowChainStep(enemyInstance, chainState, chainState.CurrentStep);
            }
        }
        catch (Exception ex)
        {
            // Chain continue failed silently
        }
    }

    /// <summary>
    /// Show specific step of dialogue chain
    /// </summary>
    private static void ShowChainStep(object enemyInstance, ChainState chainState, int step)
    {
        try
        {
            // Get current MindBroken level
            float mindBrokenPercent = NoREroMod.Patches.UI.MindBroken.MindBrokenSystem.Percent;
            string mindBrokenLevel = GetMindBrokenLevel(mindBrokenPercent);

            // Get thought for this chain step
            string thought = GetChainThought(chainState.AnimationName, step, mindBrokenLevel);

            if (!string.IsNullOrEmpty(thought))
            {
                ShowAradiaThought(enemyInstance, thought);
            }
            else
            {
                // No more steps, end chain
                if (_activeChains.ContainsKey(enemyInstance))
                {
                    _activeChains.Remove(enemyInstance);
                }
            }
        }
        catch (Exception ex)
        {
            // Chain step show failed silently
        }
    }

    /// <summary>
    /// Get delay for chain step
    /// </summary>
    private static float GetChainStepDelay(string animationName, int step)
    {
        // Default delays based on animation
        switch (animationName)
        {
            case "ERO1":
                return step * 0.8f; // 0.8s, 1.6s, 2.4s
            case "ERO2":
                return step * 0.7f; // 0.7s, 1.4s, 2.1s
            default:
                return step * 1.0f;
        }
    }

    /// <summary>
    /// Get thought for chain step
    /// </summary>
    private static string GetChainThought(string animationName, int step, string mindBrokenLevel)
    {
        try
        {
            // For now, use regular thought system - can be expanded to use chain-specific data
            return GetInquisitionBlackThought(animationName, animationName, mindBrokenLevel, 0);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Show Aradia thought for InquisitionBlack
    /// </summary>
    private static void ShowAradiaThought(object enemyInstance, string thought)
    {
        try
        {
            if (_display == null || string.IsNullOrEmpty(thought))
                return;

            // Check cooldown to prevent spam
            float currentTime = UnityEngine.Time.time;
            if (_lastThoughtTime.TryGetValue(enemyInstance, out float lastTime))
            {
                if (currentTime - lastTime < _thoughtCooldown)
                    return; // Too soon, skip this thought
            }
            _lastThoughtTime[enemyInstance] = currentTime;

            // Fixed vertical offset of 100px from bone
            float verticalOffset = 100.0f;

            // Use centralized Aradia thought style (blue text, white outline, italic)
            var style = DialogueDisplay.BuildAradiaThoughtStyle(verticalOffset, 0.0f, true);
            Plugin.Log.LogInfo($"[InquisitionBlack] Showing thought with color: {style.Color}, outline: {style.OutlineColor}, bold: {style.IsBold}, italic: {style.IsItalic}");

            _display.ShowAradiaThought(enemyInstance, thought, "bone34", style, _displayDuration);
        }
        catch (Exception ex)
        {
            // Thought display failed silently
        }
    }
}