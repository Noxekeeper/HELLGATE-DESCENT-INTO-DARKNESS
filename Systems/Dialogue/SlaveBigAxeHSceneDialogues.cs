using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using NoREroMod;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// SlaveBigAxe H-scene phrases + Aradia responses (MindBroken low/medium/high).
/// JSON: SlaveBigAxeHSceneData.json, AradiaSlaveBigAxeResponses.json
/// </summary>
internal static class SlaveBigAxeHSceneDialogues
{
    private static readonly string[] HAnimations =
    {
        "START",
        "ERO", "ERO1", "ERO2", "ERO3", "ERO4",
        "FIN", "FIN2",
        "JIGO", "JIGO2",
        "JIGOERO", "JIGOERO2", "JIGOERO3", "JIGOERO4",
        "JIGOFIN", "JIGOFIN2",
        "JIGOPOST3", "JIGOPOST4",
        "JIGOPOSTERO", "JIGOPOSTERO2", "JIGOPOSTERO3",
        "JIGOPOSTFIN", "JIGOPOSTFIN2",
        "JIGOTOFADE",
        "ZGAMEOVER", "ZGAMEOVER2LOOP",
        "4ERO"
    };

    private static readonly string[] EventNames =
    {
        "SE", "SE1", "SE2", "SE3", "SE4", "SE5", "SE6", "SE7", "SE8",
        "START",
        "ERO", "ERO1", "ERO2", "ERO3", "ERO4",
        "FIN", "FIN2",
        "JIGO", "JIGO2",
        "JIGOERO", "JIGOERO2", "JIGOERO3", "JIGOERO4",
        "JIGOFIN", "JIGOFIN2",
        "JIGOPOST3", "JIGOPOST4",
        "JIGOPOSTERO", "JIGOPOSTERO2", "JIGOPOSTERO3",
        "JIGOPOSTFIN", "JIGOPOSTFIN2",
        "JIGOTOFADE",
        "ZGAMEOVER", "ZGAMEOVER2LOOP",
        "4ERO"
    };

    private static Dictionary<string, Dictionary<string, List<string>>> _animationComments = new();
    private static bool _initialized = false;

    private static readonly Dictionary<object, float> _lastCommentTime = new();
    private static readonly Dictionary<object, float> _lastAradiaCommentTime = new();
    private static readonly List<object> _activeComments = new();

    private static float _commentCooldown = 2.0f;
    private const float PostFadeEnemyCooldown = 5.0f;
    private const float PostFadeAradiaCooldown = 7.0f;

    private static readonly string[] PostFadeAnimations =
    {
        "JIGOTOFADE",
        "ZGAMEOVER",
        "ZGAMEOVER2LOOP",
        "4ERO"
    };

    private static bool IsPostFadeAnimation(string animationName)
    {
        if (string.IsNullOrEmpty(animationName))
        {
            return false;
        }

        foreach (string anim in PostFadeAnimations)
        {
            if (animationName.Equals(anim, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
    private static int _maxSimultaneousComments = 1;
    private static float _displayDuration = 4.0f;
    private static float _verticalOffset = 15.0f;
    private static float _streamingSpeed = 0.03f;

    private static DialogueDisplay _display = null;

    private static Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>> _aradiaResponseDialogues = new();
    private static float _aradiaDisplayDuration = 5.0f;

    internal static void Initialize()
    {
        if (_initialized) return;

        try
        {
            LoadSlaveBigAxeHSceneData();
            LoadAradiaData();
            _initialized = true;
        }
        catch
        {
        }
    }

    internal static void SetDisplay(DialogueDisplay display)
    {
        _display = display;
    }

    internal static void Reset()
    {
        _initialized = false;
        _animationComments?.Clear();
        _aradiaResponseDialogues?.Clear();
        _lastCommentTime.Clear();
        _lastAradiaCommentTime.Clear();
        _activeComments.Clear();
    }

    private static void LoadSlaveBigAxeHSceneData()
    {
        _animationComments?.Clear();

        try
        {
            string jsonPath = ResolveJsonPath("SlaveBigAxeHSceneData.json");
            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
            {
                return;
            }

            ParseEnemyJson(File.ReadAllText(jsonPath));
        }
        catch
        {
            _animationComments?.Clear();
        }
    }

    private static void LoadAradiaData()
    {
        _aradiaResponseDialogues?.Clear();

        try
        {
            string jsonPath = ResolveJsonPath("AradiaSlaveBigAxeResponses.json");
            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
            {
                return;
            }

            LoadAradiaDialoguesFromJson(File.ReadAllText(jsonPath));
        }
        catch
        {
            _aradiaResponseDialogues?.Clear();
        }
    }

    /// <summary>
    /// Prefer selected language folder; if the file is missing there, fall back to EN.
    /// Keeps untranslated languages working until translations land.
    /// </summary>
    private static string ResolveJsonPath(string fileName)
    {
        try
        {
            string hellGateJson = Path.Combine(
                Path.Combine(
                    Path.Combine(Path.Combine(Application.dataPath, ".."), "BepInEx"),
                    "plugins"),
                "HellGateJson");

            if (!Directory.Exists(hellGateJson))
            {
                return null;
            }

            string languageCode = Plugin.hellGateLanguage?.Value ?? "EN";
            if (string.IsNullOrEmpty(languageCode))
            {
                languageCode = "EN";
            }

            string langFile = Path.Combine(Path.Combine(hellGateJson, languageCode), fileName);
            if (File.Exists(langFile))
            {
                return langFile;
            }

            string enFile = Path.Combine(Path.Combine(hellGateJson, "EN"), fileName);
            if (File.Exists(enFile))
            {
                return enFile;
            }
        }
        catch
        {
        }

        return null;
    }

    private static void ParseEnemyJson(string jsonText)
    {
        _animationComments.Clear();
        ParseSettings(jsonText);
        ParseAnimationComments(jsonText);
    }

    private static void ParseSettings(string jsonText)
    {
        try
        {
            int settingsStart = jsonText.IndexOf("\"settings\"");
            if (settingsStart == -1) return;

            int braceStart = settingsStart + jsonText.Substring(settingsStart).IndexOf('{');
            int settingsEnd = FindMatchingBrace(jsonText, braceStart);
            if (settingsEnd == -1) return;

            string settingsSection = jsonText.Substring(settingsStart, settingsEnd - settingsStart + 1);

            Match match = Regex.Match(settingsSection, "\"commentCooldownSeconds\"\\s*:\\s*([0-9.]+)");
            if (match.Success) _commentCooldown = float.Parse(match.Groups[1].Value);

            match = Regex.Match(settingsSection, "\"maxSimultaneousComments\"\\s*:\\s*([0-9]+)");
            if (match.Success) _maxSimultaneousComments = int.Parse(match.Groups[1].Value);

            match = Regex.Match(settingsSection, "\"commentDisplayDuration\"\\s*:\\s*([0-9.]+)");
            if (match.Success) _displayDuration = float.Parse(match.Groups[1].Value);

            match = Regex.Match(settingsSection, "\"verticalOffset\"\\s*:\\s*([0-9.]+)");
            if (match.Success) _verticalOffset = float.Parse(match.Groups[1].Value);

            match = Regex.Match(settingsSection, "\"streamingSpeed\"\\s*:\\s*([0-9.]+)");
            if (match.Success) _streamingSpeed = float.Parse(match.Groups[1].Value);
        }
        catch
        {
        }
    }

    private static void ParseAnimationComments(string jsonText)
    {
        try
        {
            int animStart = jsonText.IndexOf("\"animations\"");
            if (animStart == -1) return;

            int braceStart = animStart + jsonText.Substring(animStart).IndexOf('{');
            int animEnd = FindMatchingBrace(jsonText, braceStart);
            if (animEnd == -1) return;

            string animSection = jsonText.Substring(animStart, animEnd - animStart + 1);

            foreach (string animName in HAnimations)
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
        catch
        {
        }
    }

    private static void ParseEventsForAnimation(string animBlock, Dictionary<string, List<string>> eventComments)
    {
        int eventsStart = animBlock.IndexOf("\"events\"");
        if (eventsStart == -1) return;

        int eventsBraceStart = eventsStart + animBlock.Substring(eventsStart).IndexOf('{');
        int eventsEnd = FindMatchingBrace(animBlock, eventsBraceStart);
        if (eventsEnd == -1) return;

        string eventsSection = animBlock.Substring(eventsBraceStart, eventsEnd - eventsBraceStart + 1);

        foreach (string eventName in EventNames)
        {
            List<string> phrases = ParseStringArray(eventsSection, eventName);
            if (phrases.Count > 0)
            {
                eventComments[eventName] = phrases;
            }

            if (eventName == "SE")
            {
                for (int i = 1; i <= 8; i++)
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

    private static List<string> ParseStringArray(string jsonSection, string key)
    {
        List<string> result = new();

        try
        {
            string pattern = $"\"{Regex.Escape(key)}\"\\s*:\\s*\\[";
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
        catch
        {
        }

        return result;
    }

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

    internal static void ProcessHSceneEvent(object enemyInstance, string animationName, string eventName, int seCount)
    {
        if (!_initialized || _display == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(eventName) &&
            (eventName.StartsWith("ARADIA_RESPONSE") || eventName.StartsWith("ARADIA_THOUGHT")))
        {
            ProcessAradiaEvent(enemyInstance, animationName, eventName, seCount);
            return;
        }

        bool postFade = IsPostFadeAnimation(animationName);
        float enemyCooldown = postFade ? PostFadeEnemyCooldown : _commentCooldown;

        if (_lastCommentTime.ContainsKey(enemyInstance))
        {
            if (Time.time - _lastCommentTime[enemyInstance] < enemyCooldown)
            {
                return;
            }
        }

        CleanupExpiredComments();
        if (_activeComments.Count >= _maxSimultaneousComments)
        {
            return;
        }

        string comment = GetCommentForEvent(animationName, eventName, seCount);
        if (string.IsNullOrEmpty(comment))
        {
            return;
        }

        // START: bone7 +50px / post-fade (JIGOTOFADE+): bone4 +25px / others: bone42 +75px
        string enemyBone;
        float enemyVerticalOffset;
        float worldOffsetY;
        if (animationName == "START")
        {
            enemyBone = "bone7";
            enemyVerticalOffset = 50f;
            worldOffsetY = 0.25f;
        }
        else if (postFade)
        {
            enemyBone = "bone4";
            enemyVerticalOffset = 25f;
            worldOffsetY = 0.15f;
        }
        else
        {
            enemyBone = "bone42";
            enemyVerticalOffset = 75f;
            worldOffsetY = 0.4f;
        }

        var bonePos = new BonePosition
        {
            BoneName = enemyBone,
            UseScreenCenter = false,
            WorldOffsetY = worldOffsetY
        };

        _display.ShowTouzokuHSceneComment(
            enemyInstance,
            comment,
            _displayDuration,
            Plugin.dialogueFontSize.Value,
            enemyVerticalOffset,
            0f,
            Plugin.ParseColor(Plugin.enemyColor.Value),
            Plugin.ParseColor(Plugin.enemyOutlineColor.Value),
            bonePos);

        StartAradiaResponseCoroutine(enemyInstance, animationName, eventName);

        _lastCommentTime[enemyInstance] = Time.time;
        if (!_activeComments.Contains(enemyInstance))
        {
            _activeComments.Add(enemyInstance);
        }
    }

    private static string GetCommentForEvent(string animationName, string eventName, int seCount)
    {
        if (!_animationComments.ContainsKey(animationName))
        {
            return null;
        }

        Dictionary<string, List<string>> eventComments = _animationComments[animationName];

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

    private static void LoadAradiaDialoguesFromJson(string jsonText)
    {
        _aradiaResponseDialogues.Clear();

        Match durationMatch = Regex.Match(jsonText, "\"commentDisplayDuration\"\\s*:\\s*([0-9.]+)");
        if (!durationMatch.Success)
        {
            durationMatch = Regex.Match(jsonText, "\"displayDuration\"\\s*:\\s*([0-9.]+)");
        }
        if (durationMatch.Success)
        {
            float.TryParse(durationMatch.Groups[1].Value, out _aradiaDisplayDuration);
        }

        int animStart = jsonText.IndexOf("\"animations\"");
        if (animStart == -1) return;

        int braceStart = animStart + jsonText.Substring(animStart).IndexOf('{');
        int animEnd = FindMatchingBrace(jsonText, braceStart);
        if (animEnd == -1) return;

        string animSection = jsonText.Substring(animStart, animEnd - animStart + 1);

        foreach (string anim in HAnimations)
        {
            ParseAnimationDialogues(animSection, anim);
        }
    }

    private static void ParseAnimationDialogues(string animSection, string animationName)
    {
        string animPattern = $"\"{animationName}\"\\s*:\\s*\\{{";
        Match animMatch = Regex.Match(animSection, animPattern);
        if (!animMatch.Success) return;

        int animStart = animMatch.Index + animMatch.Length - 1;
        int animEnd = FindMatchingBrace(animSection, animStart);
        if (animEnd == -1) return;

        string animBlock = animSection.Substring(animStart, animEnd - animStart + 1);

        string eventPattern = "\"ARADIA_RESPONSE\"\\s*:\\s*\\{";
        Match eventMatch = Regex.Match(animBlock, eventPattern);
        if (!eventMatch.Success) return;

        int eventStart = eventMatch.Index + eventMatch.Length - 1;
        int eventEnd = FindMatchingBrace(animBlock, eventStart);
        if (eventEnd == -1) return;

        string eventBlock = animBlock.Substring(eventStart, eventEnd - eventStart + 1);
        ParseMindBrokenLevels(eventBlock, animationName);
    }

    private static void ParseMindBrokenLevels(string eventBlock, string animationName)
    {
        string[] levels = { "low", "medium", "high" };

        foreach (string level in levels)
        {
            List<string> dialogues = ParseStringArray(eventBlock, level);
            if (dialogues.Count == 0) continue;

            if (!_aradiaResponseDialogues.ContainsKey(animationName))
            {
                _aradiaResponseDialogues[animationName] = new Dictionary<string, Dictionary<string, List<string>>>();
            }

            if (!_aradiaResponseDialogues[animationName].ContainsKey("ARADIA_RESPONSE"))
            {
                _aradiaResponseDialogues[animationName]["ARADIA_RESPONSE"] = new Dictionary<string, List<string>>();
            }

            _aradiaResponseDialogues[animationName]["ARADIA_RESPONSE"][level] = dialogues;
        }
    }

    private static void ProcessAradiaEvent(object enemyInstance, string animationName, string eventName, int seCount)
    {
        if (_display == null) return;

        try
        {
            if (!eventName.StartsWith("ARADIA_RESPONSE"))
            {
                return;
            }

            // Post-fade: Aradia phrases on their own 7s cooldown
            if (IsPostFadeAnimation(animationName) &&
                _lastAradiaCommentTime.ContainsKey(enemyInstance) &&
                Time.time - _lastAradiaCommentTime[enemyInstance] < PostFadeAradiaCooldown)
            {
                return;
            }

            float mindBrokenPercent = NoREroMod.Patches.UI.MindBroken.MindBrokenSystem.Percent;
            string mindBrokenLevel = GetMindBrokenLevel(mindBrokenPercent);

            string[] dialogues = GetAradiaResponseDialogues(animationName, "ARADIA_RESPONSE", mindBrokenLevel);
            if (dialogues == null || dialogues.Length == 0)
            {
                return;
            }

            string selected = dialogues[UnityEngine.Random.Range(0, dialogues.Length)];
            ShowAradiaResponse(enemyInstance, selected, animationName);
            _lastAradiaCommentTime[enemyInstance] = Time.time;
        }
        catch
        {
        }
    }

    private static string GetMindBrokenLevel(float percent)
    {
        if (percent < 0.3f) return "low";
        if (percent < 0.7f) return "medium";
        return "high";
    }

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

    private static void ShowAradiaResponse(object enemyInstance, string response, string animationName)
    {
        if (string.IsNullOrEmpty(response) || _display == null) return;

        try
        {
            string boneName = DialogueDisplay.GetAradiaBoneForAnimation(enemyInstance, animationName);
            // START: bone10 +25px / post-fade: bone9 / others: bone62 +50px
            float verticalOffset;
            float worldOffsetY;
            if (animationName == "START")
            {
                verticalOffset = 25f;
                worldOffsetY = 0.15f;
            }
            else if (IsPostFadeAnimation(animationName))
            {
                verticalOffset = 0f;
                worldOffsetY = 0f;
            }
            else
            {
                verticalOffset = 50f;
                worldOffsetY = 0.3f;
            }

            var style = DialogueDisplay.BuildAradiaUnifiedStyle(verticalOffset, 0.0f, true);
            _display.ShowAradiaResponse(enemyInstance, response, boneName, style, _aradiaDisplayDuration, worldOffsetY);
        }
        catch
        {
        }
    }

    private static void StartAradiaResponseCoroutine(object enemyInstance, string animationName, string eventName)
    {
        if (enemyInstance == null) return;
        if (!string.IsNullOrEmpty(eventName) && eventName.StartsWith("SE"))
        {
            return;
        }

        var monoBehaviour = enemyInstance as MonoBehaviour;
        if (monoBehaviour != null)
        {
            monoBehaviour.StartCoroutine(DelayedAradiaResponse(enemyInstance, animationName));
        }
    }

    private static System.Collections.IEnumerator DelayedAradiaResponse(object enemyInstance, string animationName)
    {
        yield return new WaitForSeconds(2.0f);
        ProcessAradiaEvent(enemyInstance, animationName, "ARADIA_RESPONSE", 0);
    }
}
