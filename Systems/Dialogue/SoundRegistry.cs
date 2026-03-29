using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// Sound registration and onomatopoeia mapping
/// Universal system for all enemies
/// </summary>
internal class SoundRegistry
{
    private static Dictionary<string, string[]> _soundToOnomatopoeia = new();
    private static bool _initialized = false;

    /// <summary>
    /// Initialize sound registry from JSON
    /// </summary>
    internal static void Initialize()
    {
        if (_initialized)
        {
            // Debug.Log("[SoundRegistry] Already initialized"); // Disabled for release
            return;
        }

        try
        {
            string dataPath = GetDataPath();
            string jsonPath = Path.Combine(dataPath, "OnomatopoeiaData.json");

                // Debug.Log($"[SoundRegistry] Looking for JSON at: {jsonPath}"); // Disabled for release

            if (File.Exists(jsonPath))
            {
                string jsonContent = File.ReadAllText(jsonPath);
                // Debug.Log($"[SoundRegistry] JSON file found, size: {jsonContent.Length} bytes"); // Disabled for release
                ParseSoundMapping(jsonContent);
                // Debug.Log($"[SoundRegistry] Total sounds registered: {_soundToOnomatopoeia.Count}"); // Disabled for release
            }
            else
            {
                // Debug.LogWarning($"[SoundRegistry] JSON file not found at: {jsonPath}"); // Disabled for release
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SoundRegistry] Error loading sound mapping: {ex.Message}\n{ex.StackTrace}");
        }

        _initialized = true;
    }

    /// <summary>
    /// Parse soundMapping section from JSON
    /// </summary>
    private static void ParseSoundMapping(string json)
    {
        try
        {
            // Find "soundMapping" section
            int soundMappingStart = json.IndexOf("\"soundMapping\"", StringComparison.OrdinalIgnoreCase);
            if (soundMappingStart < 0)
            {
                // Debug.LogWarning("[SoundRegistry] soundMapping section not found in JSON"); // Disabled for release
                return;
            }

            int braceStart = json.IndexOf('{', soundMappingStart);
            if (braceStart < 0)
            {
                // Debug.LogWarning("[SoundRegistry] Opening brace not found for soundMapping"); // Disabled for release
                return;
            }

            // Находим соответствующую закрывающую скобку
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

            if (braceEnd <= braceStart)
            {
                // Debug.LogWarning("[SoundRegistry] Closing brace not found for soundMapping"); // Disabled for release
                return;
            }

            string soundMappingSection = json.Substring(braceStart + 1, braceEnd - braceStart - 1);
            // Debug.Log($"[SoundRegistry] Found soundMapping section, length: {soundMappingSection.Length}"); // Disabled for release

            // Улучшенный парсинг: ищем каждую пару "soundName": [массив]
            // Use более надежный паттерн which учитывает многострочные массивы и вложенные скобки
            // Паттерн: "soundName": [ ... ] where ... может содержать переносы строк
            string pattern = @"\""([^\""]+)\""\s*:\s*\[(.*?)\]";
            MatchCollection soundMatches = Regex.Matches(soundMappingSection, pattern, RegexOptions.Singleline);
            
            // Debug.Log($"[SoundRegistry] Found {soundMatches.Count} sound mappings"); // Disabled for release
            
            int parsedCount = 0;
            foreach (Match soundMatch in soundMatches)
            {
                if (soundMatch.Groups.Count < 3)
                {
                    continue;
                }
                
                string soundName = soundMatch.Groups[1].Value;
                string arrayContent = soundMatch.Groups[2].Value;

                List<string> onomatopoeias = new();
                
                // Парсим элементы массива - ищем все строки in кавычках
                MatchCollection itemMatches = Regex.Matches(arrayContent, @"""([^""]+)""");
                foreach (Match itemMatch in itemMatches)
                {
                    if (itemMatch.Groups.Count >= 2)
                    {
                        string onomatopoeia = itemMatch.Groups[1].Value;
                        if (!string.IsNullOrEmpty(onomatopoeia))
                        {
                            onomatopoeias.Add(onomatopoeia);
                        }
                    }
                }

                if (onomatopoeias.Count > 0)
                {
                    _soundToOnomatopoeia[soundName] = onomatopoeias.ToArray();
                    parsedCount++;
                    // Debug.Log($"[SoundRegistry] Parsed sound: {soundName} -> {onomatopoeias.Count} onomatopoeias"); // Disabled for release
                }
                else
                {
                    // Debug.LogWarning($"[SoundRegistry] No onomatopoeias found for sound: {soundName}"); // Disabled for release
                }
            }
            
            // Debug.Log($"[SoundRegistry] Successfully parsed {parsedCount} sounds out of {soundMatches.Count} matches"); // Disabled for release
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SoundRegistry] Error parsing soundMapping: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Registration sounds (can вызывать manually or через intercept)
    /// </summary>
    internal static void RegisterSound(string soundName, string[] onomatopoeias)
    {
        if (string.IsNullOrEmpty(soundName) || onomatopoeias == null || onomatopoeias.Length == 0)
        {
            return;
        }

        _soundToOnomatopoeia[soundName] = onomatopoeias;
    }

    /// <summary>
    /// Получение ономатопей by name sounds
    /// </summary>
    internal static string[] GetOnomatopoeia(string soundName)
    {
        if (!_initialized)
        {
            Initialize();
        }

        if (string.IsNullOrEmpty(soundName))
        {
            return null;
        }

        if (_soundToOnomatopoeia == null)
        {
            return null;
        }

        if (_soundToOnomatopoeia.TryGetValue(soundName, out string[] onomatopoeias))
        {
            return onomatopoeias;
        }

        return null;
    }

    /// <summary>
    /// Check есть ли регистрация for sounds
    /// </summary>
    internal static bool HasSound(string soundName)
    {
        if (!_initialized)
        {
            Initialize();
        }

        if (string.IsNullOrEmpty(soundName) || _soundToOnomatopoeia == null)
        {
            return false;
        }

        return _soundToOnomatopoeia.ContainsKey(soundName);
    }

    /// <summary>
    /// Получение случайной ономатопеи for sounds
    /// </summary>
    internal static string GetRandomOnomatopoeia(string soundName)
    {
        if (!_initialized)
        {
            Initialize();
        }

        if (string.IsNullOrEmpty(soundName))
        {
            return string.Empty;
        }

        if (_soundToOnomatopoeia == null)
        {
            return string.Empty;
        }

        string[] onomatopoeias = GetOnomatopoeia(soundName);
        if (onomatopoeias == null || onomatopoeias.Length == 0)
        {
            int count = _soundToOnomatopoeia != null ? _soundToOnomatopoeia.Count : 0;
            // Debug.LogWarning($"[SoundRegistry] No onomatopoeias found for sound: {soundName} (Total registered: {count})"); // Disabled for release
            return string.Empty;
        }

        string selected = onomatopoeias[UnityEngine.Random.Range(0, onomatopoeias.Length)];
        // Debug.Log($"[SoundRegistry] Selected onomatopoeia for {soundName}: {selected}"); // Disabled for release
        return selected;
    }

    /// <summary>
    /// Get path to folder HellGateJson considering selected language
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
    /// Очистка реестра
    /// </summary>
    internal static void Clear()
    {
        _soundToOnomatopoeia.Clear();
        _initialized = false;
    }
}

