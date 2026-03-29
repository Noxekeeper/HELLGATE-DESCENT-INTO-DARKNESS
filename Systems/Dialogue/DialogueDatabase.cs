using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// Loading and storing dialogue data from JSON
/// </summary>
internal class DialogueDatabase
{
    private Dictionary<string, string[]> _categories = new();
    private Dictionary<string, Dictionary<string, string[]>> _mutudeSpecific = new();
    private Dictionary<string, Dictionary<string, Dictionary<string, string[]>>> _mutudeSegments = new();

    /// <summary>
    /// Load all data from JSON files
    /// </summary>
    internal void LoadAll()
    {
        // Clear old data before loading
        _categories?.Clear();
        _mutudeSpecific?.Clear();
        _mutudeSegments?.Clear();
        
        try
        {
            string dataPath = GetDataPath();
            string jsonPath = Path.Combine(dataPath, "OnomatopoeiaData.json");

            if (!File.Exists(jsonPath))
            {
                LoadDefaultData();
                return;
            }

            string jsonContent = File.ReadAllText(jsonPath);
            ParseJsonManually(jsonContent);
        }
        catch
        {
            LoadDefaultData();
        }
    }

    /// <summary>
    /// Manual JSON parsing (JsonUtility doesn't support Dictionary)
    /// </summary>
    private void ParseJsonManually(string json)
    {
        // Parse categories
        Match categoriesMatch = Regex.Match(json, @"""categories""\s*:\s*\{([^}]+)\}", RegexOptions.Singleline);
        if (categoriesMatch.Success)
        {
            ParseCategories(categoriesMatch.Groups[1].Value);
        }

        // Parse Mutude specific data - use more complex pattern for nested objects
        // Find start of "mutudeSpecific": { then find corresponding closing brace
        int mutudeStart = json.IndexOf("\"mutudeSpecific\"", StringComparison.OrdinalIgnoreCase);
        if (mutudeStart >= 0)
        {
            int braceStart = json.IndexOf('{', mutudeStart);
            if (braceStart >= 0)
            {
                int braceCount = 0;
                int braceEnd = braceStart;
                for (int i = braceStart; i < json.Length; i++)
                {
                    if (json[i] == '{')
                    {
                        braceCount++;
                    }
                    else if (json[i] == '}')
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
                    string mutudeSection = json.Substring(braceStart + 1, braceEnd - braceStart - 1);
                    ParseMutudeSpecific(mutudeSection);
                }
            }
        }
    }

    /// <summary>
    /// Parse categories
    /// </summary>
    private void ParseCategories(string jsonSection)
    {
        // Ищем каждую категорию
        string[] categoryNames = { "StaminaEffort", "ThrustSFX", "SlimeWet", "Impact", "ClimaxBurst" };
        
        foreach (string categoryName in categoryNames)
        {
            string pattern = $"\"{categoryName}\"\\s*:\\s*\\[([^\\]]+)\\]";
            Match match = Regex.Match(jsonSection, pattern);
            if (match.Success)
            {
                List<string> items = new();
                MatchCollection itemMatches = Regex.Matches(match.Groups[1].Value, "\"([^\"]+)\"");
                foreach (Match itemMatch in itemMatches)
                {
                    items.Add(itemMatch.Groups[1].Value);
                }
                _categories[categoryName] = items.ToArray();
            }
        }
    }

    /// <summary>
    /// Parsing Mutude специфичных данных
    /// </summary>
    private void ParseMutudeSpecific(string jsonSection)
    {
        // Ищем каждую animation
        string[] animationNames = { "START", "ERO1", "ERO1_2", "ERO2", "ERO2_2", "ERO3", "ERO4", "ERO5", "FIN", "FIN2", "START_JIGO", "DRINK", "DRINK_END" };
        
        foreach (string animName in animationNames)
        {
            // Ищем начало animation
            string searchPattern = "\"" + animName + "\"";
            int animStart = jsonSection.IndexOf(searchPattern, StringComparison.OrdinalIgnoreCase);
            if (animStart >= 0)
            {
                // Ищем открывающую скобку after имени animation
                int braceStart = jsonSection.IndexOf('{', animStart);
                if (braceStart >= 0)
                {
                    // Находим соответствующую закрывающую скобку
                    int braceCount = 0;
                    int braceEnd = braceStart;
                    for (int i = braceStart; i < jsonSection.Length; i++)
                    {
                        if (jsonSection[i] == '{')
                        {
                            braceCount++;
                        }
                        else if (jsonSection[i] == '}')
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
                        string animContent = jsonSection.Substring(braceStart + 1, braceEnd - braceStart - 1);
                        Dictionary<string, string[]> animData = new();
                        Dictionary<string, Dictionary<string, string[]>> animSegments = new();
                        
                        // Ищем se_count_X or "any"
                        MatchCollection seMatches = Regex.Matches(animContent, "\"(se_count_\\d+|any)\"\\s*:\\s*\\[([^\\]]+)\\]");
                        foreach (Match seMatch in seMatches)
                        {
                            string key = seMatch.Groups[1].Value;
                            List<string> items = new();
                            MatchCollection itemMatches = Regex.Matches(seMatch.Groups[2].Value, "\"([^\"]+)\"");
                            foreach (Match itemMatch in itemMatches)
                            {
                                items.Add(itemMatch.Groups[1].Value);
                            }
                            animData[key] = items.ToArray();
                        }
                        
                        // Ищем segments (se_count_X_segments or any_segments)
                        MatchCollection segmentMatches = Regex.Matches(animContent, "\"(se_count_\\d+|any)_segments\"\\s*:\\s*\\{([^}]+)\\}", RegexOptions.Singleline);
                        foreach (Match segmentMatch in segmentMatches)
                        {
                            string baseKey = segmentMatch.Groups[1].Value; // se_count_1 or any
                            string segmentsContent = segmentMatch.Groups[2].Value;
                            
                            Dictionary<string, string[]> segments = new();
                            
                            // Парсим каждый сегмент "1": ["item1", "item2"]
                            MatchCollection segmentItemMatches = Regex.Matches(segmentsContent, "\"(\\d+)\"\\s*:\\s*\\[([^\\]]+)\\]");
                            foreach (Match segmentItemMatch in segmentItemMatches)
                            {
                                string segmentNumber = segmentItemMatch.Groups[1].Value;
                                List<string> segmentItems = new();
                                MatchCollection segmentItemArrayMatches = Regex.Matches(segmentItemMatch.Groups[2].Value, "\"([^\"]+)\"");
                                foreach (Match segmentItemArrayMatch in segmentItemArrayMatches)
                                {
                                    segmentItems.Add(segmentItemArrayMatch.Groups[1].Value);
                                }
                                if (segmentItems.Count > 0)
                                {
                                    segments[segmentNumber] = segmentItems.ToArray();
                                }
                            }
                            
                            if (segments.Count > 0)
                            {
                                animSegments[baseKey] = segments;
                            }
                        }
                        
                        if (animData.Count > 0)
                        {
                            _mutudeSpecific[animName] = animData;
                        }
                        
                        if (animSegments.Count > 0)
                        {
                            _mutudeSegments[animName] = animSegments;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Загрузка дефолтных данных if JSON not found
    /// </summary>
    private void LoadDefaultData()
    {
        _categories["StaminaEffort"] = new[] { "nnnh...!", "kgghh!", "ghhh...!", "Ияяя!", "Н-нет!", "Нет!" };
        _categories["ThrustSFX"] = new[] { "Глк...", "Глкхх...", "Chmok!", "Gulp!", "Глп!", "Глпх!" };
        _categories["SlimeWet"] = new[] { "schlk...", "splrch...", "plop...", "ММ..", "ммм.мм" };
        _categories["Impact"] = new[] { "thud!", "tsk!", "tap!" };
        _categories["ClimaxBurst"] = new[] { "AAAaaah!!!", "АААаах!", "Ааааххх!!", "Khh—!", "уууааах", "ааах" };
    }

    /// <summary>
    /// Получение ономатопей by категории
    /// </summary>
    internal string[] GetOnomatopoeiaByCategory(string category)
    {
        if (_categories.TryGetValue(category, out string[] array))
        {
            return array;
        }
        return null;
    }

    /// <summary>
    /// Получение специфичных ономатопей Mutude for animation и se_count
    /// </summary>
    internal string[] GetMutudeOnomatopoeia(string animationName, int seCount)
    {
        if (!_mutudeSpecific.TryGetValue(animationName, out var animData))
        {
            return null;
        }

        if (seCount == 0)
        {
            if (animData.TryGetValue("any", out string[] anyArray))
            {
                return anyArray;
            }
        }
        else
        {
            string key = $"se_count_{seCount}";
            if (animData.TryGetValue(key, out string[] array))
            {
                return array;
            }
        }

        if (animData.TryGetValue("any", out string[] anyArrayFallback))
        {
            return anyArrayFallback;
        }

        return null;
    }

    /// <summary>
    /// Получение ономатопеи by сегменту for animation и se_count
    /// </summary>
    internal string[] GetSegmentOnomatopoeia(string animationName, int seCount, int segmentNumber)
    {
        if (!_mutudeSegments.TryGetValue(animationName, out var animSegments))
        {
            return null;
        }

        string baseKey = seCount == 0 ? "any" : $"se_count_{seCount}";
        
        if (!animSegments.TryGetValue(baseKey, out var segments))
        {
            // Try "any" as fallback
            if (!animSegments.TryGetValue("any", out segments))
            {
                return null;
            }
        }

        string segmentKey = segmentNumber.ToString();
        if (segments.TryGetValue(segmentKey, out string[] segmentArray))
        {
            return segmentArray;
        }

        return null;
    }

    /// <summary>
    /// Check есть ли segments for animation и se_count
    /// </summary>
    internal bool HasSegments(string animationName, int seCount)
    {
        if (!_mutudeSegments.TryGetValue(animationName, out var animSegments))
        {
            return false;
        }

        string baseKey = seCount == 0 ? "any" : $"se_count_{seCount}";
        return animSegments.ContainsKey(baseKey) || animSegments.ContainsKey("any");
    }

    /// <summary>
    /// Получение количества сегментоin for animation и se_count
    /// </summary>
    internal int GetSegmentCount(string animationName, int seCount)
    {
        if (!_mutudeSegments.TryGetValue(animationName, out var animSegments))
        {
            return 0;
        }

        string baseKey = seCount == 0 ? "any" : $"se_count_{seCount}";
        
        if (!animSegments.TryGetValue(baseKey, out var segments))
        {
            if (!animSegments.TryGetValue("any", out segments))
            {
                return 0;
            }
        }

        return segments.Count;
    }

    /// <summary>
    /// Get path to folder HellGateJson considering selected language
    /// </summary>
    private string GetDataPath()
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
}
