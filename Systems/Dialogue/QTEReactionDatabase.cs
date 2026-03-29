using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// Loading and storing QTE reactions from JSON
/// </summary>
internal class QTEReactionDatabase
{
    private Dictionary<string, string[]> _correctPress = new();
    private Dictionary<string, string[]> _wrongPress = new();
    private Dictionary<string, string[]> _comboMilestone = new();
    private Dictionary<string, Dictionary<string, string[]>> _enemySpecific = new();
    
    private float _correctPressChance = 0.15f;
    private float _wrongPressChance = 0.25f;
    private float _comboMilestoneChance = 1.0f;
    private float _cooldownSeconds = 3.0f;
    private int _minComboForReaction = 3;

    internal float CorrectPressChance => _correctPressChance;
    internal float WrongPressChance => _wrongPressChance;
    internal float ComboMilestoneChance => _comboMilestoneChance;
    internal float CooldownSeconds => _cooldownSeconds;
    internal int MinComboForReaction => _minComboForReaction;

    internal void LoadAll()
    {
        // Clear old data before loading
        _correctPress?.Clear();
        _wrongPress?.Clear();
        _comboMilestone?.Clear();
        _enemySpecific?.Clear();
        
        try
        {
            string dataPath = GetDataPath();
            string jsonPath = Path.Combine(dataPath, "QTEReactionData.json");

            if (!File.Exists(jsonPath))
            {
                LoadDefaultData();
                return;
            }

            string jsonContent = File.ReadAllText(jsonPath);
            ParseJsonManually(jsonContent);
        }
        catch (Exception ex)
        {
            LoadDefaultData();
        }
    }

    private void ParseJsonManually(string json)
    {
        // Parse settings
        ParseSettings(json);
        
        // Parse correctPress
        ParseSection(json, "correctPress", _correctPress);
        
        // Parse wrongPress
        ParseSection(json, "wrongPress", _wrongPress);
        
        // Parse comboMilestone
        ParseSection(json, "comboMilestone", _comboMilestone);
        
        // Parse enemySpecific
        ParseEnemySpecific(json);
    }

    private void ParseSettings(string json)
    {
        string pattern = "\"settings\"\\s*:\\s*\\{([^}]+)\\}";
        Match match = Regex.Match(json, pattern, RegexOptions.Singleline);
        
        if (match.Success)
        {
            string settingsContent = match.Groups[1].Value;
            
            // Parse correctPressChance
            var chanceMatch = Regex.Match(settingsContent, "\"correctPressChance\"\\s*:\\s*([0-9.]+)");
            if (chanceMatch.Success && float.TryParse(chanceMatch.Groups[1].Value, out float correctChance))
            {
                _correctPressChance = correctChance;
            }
            
            // Parse wrongPressChance
            chanceMatch = Regex.Match(settingsContent, "\"wrongPressChance\"\\s*:\\s*([0-9.]+)");
            if (chanceMatch.Success && float.TryParse(chanceMatch.Groups[1].Value, out float wrongChance))
            {
                _wrongPressChance = wrongChance;
            }
            
            // Parse comboMilestoneChance
            chanceMatch = Regex.Match(settingsContent, "\"comboMilestoneChance\"\\s*:\\s*([0-9.]+)");
            if (chanceMatch.Success && float.TryParse(chanceMatch.Groups[1].Value, out float comboChance))
            {
                _comboMilestoneChance = comboChance;
            }
            
            // Парсим cooldownSeconds
            chanceMatch = Regex.Match(settingsContent, "\"cooldownSeconds\"\\s*:\\s*([0-9.]+)");
            if (chanceMatch.Success && float.TryParse(chanceMatch.Groups[1].Value, out float cooldown))
            {
                _cooldownSeconds = cooldown;
            }
            
            // Парсим minComboForReaction
            chanceMatch = Regex.Match(settingsContent, "\"minComboForReaction\"\\s*:\\s*([0-9]+)");
            if (chanceMatch.Success && int.TryParse(chanceMatch.Groups[1].Value, out int minCombo))
            {
                _minComboForReaction = minCombo;
            }
        }
    }

    private void ParseSection(string json, string sectionName, Dictionary<string, string[]> target)
    {
        string pattern = $"\"{sectionName}\"\\s*:\\s*\\{{([^}}]+)\\}}";
        Match match = Regex.Match(json, pattern, RegexOptions.Singleline);
        
        if (match.Success)
        {
            string sectionContent = match.Groups[1].Value;
            
            // Парсим подкатегории (taunting, dominant, sexual etc.)
            string subCategoryPattern = $"\"([^\"]+)\"\\s*:\\s*\\[([^\\]]+)\\]";
            var subMatches = Regex.Matches(sectionContent, subCategoryPattern, RegexOptions.Singleline);
            
            foreach (Match subMatch in subMatches)
            {
                string categoryName = subMatch.Groups[1].Value;
                string arrayContent = subMatch.Groups[2].Value;
                
                List<string> phrases = new List<string>();
                var phraseMatches = Regex.Matches(arrayContent, "\"([^\"]+)\"");
                
                foreach (Match phraseMatch in phraseMatches)
                {
                    phrases.Add(phraseMatch.Groups[1].Value);
                }
                
                if (phrases.Count > 0)
                {
                    target[categoryName] = phrases.ToArray();
                }
            }
        }
    }

    private void ParseEnemySpecific(string json)
    {
        string pattern = "\"enemySpecific\"\\s*:\\s*\\{([^}]+)\\}";
        Match match = Regex.Match(json, pattern, RegexOptions.Singleline);
        
        if (match.Success)
        {
            string enemyContent = match.Groups[1].Value;
            
            // Парсим каждого enemy
            string enemyPattern = "\"([^\"]+)\"\\s*:\\s*\\{([^}]+)\\}";
            var enemyMatches = Regex.Matches(enemyContent, enemyPattern, RegexOptions.Singleline);
            
            foreach (Match enemyMatch in enemyMatches)
            {
                string enemyName = enemyMatch.Groups[1].Value;
                string enemyData = enemyMatch.Groups[2].Value;
                
                Dictionary<string, string[]> enemyPhrasesDict = new Dictionary<string, string[]>();
                
                // Parse correctPress и wrongPress for enemy
                ParseSection("{" + enemyData + "}", "correctPress", enemyPhrasesDict);
                ParseSection("{" + enemyData + "}", "wrongPress", enemyPhrasesDict);
                
                if (enemyPhrasesDict.Count > 0)
                {
                    _enemySpecific[enemyName] = enemyPhrasesDict;
                }
            }
        }
    }

    internal string[] GetCorrectPressPhrases(string category = null, string enemyName = null)
    {
        // First check enemy-специфичные phrases
        if (!string.IsNullOrEmpty(enemyName))
        {
            // Try основное имя
            if (_enemySpecific.TryGetValue(enemyName, out var enemyPhrases))
            {
                if (enemyPhrases.TryGetValue("correctPress", out var enemyCorrectPhrases))
                {
                    return enemyCorrectPhrases;
                }
            }
            
            // Fallback: for Dorei пробуем альтернативное имя
            if (enemyName == "dorei" && _enemySpecific.TryGetValue("SinnerslaveCrossbow", out var altPhrases))
            {
                if (altPhrases.TryGetValue("correctPress", out var altCorrectPhrases))
                {
                    return altCorrectPhrases;
                }
            }
            else if (enemyName == "SinnerslaveCrossbow" && _enemySpecific.TryGetValue("dorei", out var altPhrases2))
            {
                if (altPhrases2.TryGetValue("correctPress", out var altCorrectPhrases2))
                {
                    return altCorrectPhrases2;
                }
            }
        }
        
        // Затем общие phrases
        if (string.IsNullOrEmpty(category))
        {
            List<string> allPhrases = new List<string>();
            foreach (var phrasesList in _correctPress.Values)
            {
                allPhrases.AddRange(phrasesList);
            }
            return allPhrases.ToArray();
        }
        
        return _correctPress.TryGetValue(category, out var categoryPhrases) ? categoryPhrases : new string[0];
    }

    internal string[] GetWrongPressPhrases(string category = null, string enemyName = null)
    {
        // First check enemy-специфичные phrases
        if (!string.IsNullOrEmpty(enemyName))
        {
            // Try основное имя
            if (_enemySpecific.TryGetValue(enemyName, out var enemyPhrases))
            {
                if (enemyPhrases.TryGetValue("wrongPress", out var enemyWrongPhrases))
                {
                    return enemyWrongPhrases;
                }
            }
            
            // Fallback: for Dorei пробуем альтернативное имя
            if (enemyName == "dorei" && _enemySpecific.TryGetValue("SinnerslaveCrossbow", out var altPhrases))
            {
                if (altPhrases.TryGetValue("wrongPress", out var altWrongPhrases))
                {
                    return altWrongPhrases;
                }
            }
            else if (enemyName == "SinnerslaveCrossbow" && _enemySpecific.TryGetValue("dorei", out var altPhrases2))
            {
                if (altPhrases2.TryGetValue("wrongPress", out var altWrongPhrases2))
                {
                    return altWrongPhrases2;
                }
            }
        }
        
        // Затем общие phrases
        if (string.IsNullOrEmpty(category))
        {
            List<string> allPhrases = new List<string>();
            foreach (var phrasesList in _wrongPress.Values)
            {
                allPhrases.AddRange(phrasesList);
            }
            return allPhrases.ToArray();
        }
        
        return _wrongPress.TryGetValue(category, out var categoryPhrases) ? categoryPhrases : new string[0];
    }

    internal string[] GetComboMilestonePhrases(int milestone)
    {
        string key = $"x{milestone}";
        return _comboMilestone.TryGetValue(key, out var phrases) ? phrases : new string[0];
    }

    private void LoadDefaultData()
    {
        _correctPress["taunting"] = new[] { "Ты слабеешь...", "Сдавайся уже..." };
        _wrongPress["punishment"] = new[] { "Ошибка...", "Неверно..." };
    }

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

        // Fallback
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

