using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using NoREroMod;

namespace NoREroMod.Systems.Camera;

/// <summary>
/// Camera settings for H-scenes with per-enemy customization support.
/// Loads from JSON file: BepInEx/plugins/HellGateJson/[LANG]/CameraSettings.json
/// Currently only PanSpeed is actively used; other settings are legacy.
/// </summary>
internal class CameraSettings
{
    private static CameraSettings _instance;
    private Dictionary<string, EnemyCameraSettings> _enemySettings = new Dictionary<string, EnemyCameraSettings>();
    private EnemyCameraSettings _defaultSettings;
    
    internal class EnemyCameraSettings
    {
        // REMOVED: ZoomLevel and CenterOffset - unused, may break camera
        // public float ZoomLevel = 6f;
        // public Vector2 CenterOffset = Vector2.zero;
        public float FollowSmoothness = 0.1f;
        public float CenterSmoothness = 0.1f;
        public float PanSpeed = 0.8f; // Camera pan speed with arrow keys
        
        // Initial effects
        public float StartSlowmoScale = 0.3f;
        public float StartSlowmoDuration = 3f;
        
        // QTE effects
        public float QTECorrectShakeIntensity = 0.5f;
        public float QTECorrectShakeDuration = 0.2f;
        
        public float QTEWrongShakeIntensity = 1.0f;
        public float QTEWrongShakeDuration = 0.3f;
        public bool QTEWrongSlowmo = true;
        public float QTEWrongSlowmoScale = 0.5f;
        public float QTEWrongSlowmoDuration = 0.5f;
        
        // Combo effects
        public float ComboShakeIntensity = 0.3f;
        public float ComboShakeDuration = 0.1f;
        
        // MindBroken effects
        public float MindBrokenShakeIntensity = 0.3f;
        
        // Climax effects
        public bool ClimaxSlowmo = true;
        public float ClimaxSlowmoScale = 0.3f;
        public float ClimaxSlowmoDuration = 2f;
        public float ClimaxZoom = 2f;
        public bool ClimaxPulse = true;
    }
    
    internal static CameraSettings Load()
    {
        if (_instance == null)
        {
            _instance = new CameraSettings();
            _instance._LoadFromFile();
        }
        return _instance;
    }
    
    /// <summary>
    /// Reload settings with new language. Called after language selection on splash screen.
    /// </summary>
    internal static void Reload()
    {
        if (_instance != null)
        {
            _instance._LoadFromFile();
        }
    }
    
    private void _LoadFromFile()
    {
        string filePath = GetDataPath();
        
        if (!File.Exists(filePath))
        {
            _defaultSettings = new EnemyCameraSettings();
            return;
        }
        
        try
        {
            string json = File.ReadAllText(filePath);
            _ParseJson(json);
        }
        catch (Exception ex)
        {
            _defaultSettings = new EnemyCameraSettings();
        }
    }
    
    private void _ParseJson(string json)
    {
        // Parse default settings
        _defaultSettings = _ParseEnemySettings(json, "default");
        
        // Parse enemy-specific settings
        string enemySpecificPattern = "\"enemySpecific\"\\s*:\\s*\\{([^}]+)\\}";
        Match enemyMatch = Regex.Match(json, enemySpecificPattern, RegexOptions.Singleline);
        
        if (enemyMatch.Success)
        {
            string enemyContent = enemyMatch.Groups[1].Value;
            string enemyPattern = "\"([^\"]+)\"\\s*:\\s*\\{([^}]+)\\}";
            var enemyMatches = Regex.Matches(enemyContent, enemyPattern, RegexOptions.Singleline);
            
            foreach (Match match in enemyMatches)
            {
                string enemyName = match.Groups[1].Value;
                string enemyData = match.Groups[2].Value;
                _enemySettings[enemyName] = _ParseEnemySettings("{" + enemyData + "}", enemyName);
            }
        }
        
    }
    
    private EnemyCameraSettings _ParseEnemySettings(string json, string enemyName)
    {
        EnemyCameraSettings settings = new EnemyCameraSettings();
        
        // REMOVED: ZoomLevel and CenterOffset parsing - unused, may break camera
        
        Match match;
        
        // FollowSmoothness
        match = Regex.Match(json, "\"followSmoothness\"\\s*:\\s*([0-9.]+)");
        if (match.Success && float.TryParse(match.Groups[1].Value, out float smoothness))
        {
            settings.FollowSmoothness = smoothness;
        }
        
        // CenterSmoothness
        match = Regex.Match(json, "\"centerSmoothness\"\\s*:\\s*([0-9.]+)");
        if (match.Success && float.TryParse(match.Groups[1].Value, out float centerSmoothness))
        {
            settings.CenterSmoothness = centerSmoothness;
        }
        
        // PanSpeed
        match = Regex.Match(json, "\"panSpeed\"\\s*:\\s*([0-9.]+)");
        if (match.Success && float.TryParse(match.Groups[1].Value, out float panSpeed))
        {
            settings.PanSpeed = panSpeed;
        }
        
        // StartSlowmo
        match = Regex.Match(json, "\"startSlowmoScale\"\\s*:\\s*([0-9.]+)");
        if (match.Success && float.TryParse(match.Groups[1].Value, out float slowmoScale))
        {
            settings.StartSlowmoScale = slowmoScale;
        }
        
        match = Regex.Match(json, "\"startSlowmoDuration\"\\s*:\\s*([0-9.]+)");
        if (match.Success && float.TryParse(match.Groups[1].Value, out float slowmoDuration))
        {
            settings.StartSlowmoDuration = slowmoDuration;
        }
        
        // QTE Correct (nested object)
        string qteCorrectSection = _ExtractNestedObject(json, "qteCorrect");
        if (!string.IsNullOrEmpty(qteCorrectSection))
        {
            match = Regex.Match(qteCorrectSection, "\"shakeIntensity\"\\s*:\\s*([0-9.]+)");
            if (match.Success && float.TryParse(match.Groups[1].Value, out float intensity))
            {
                settings.QTECorrectShakeIntensity = intensity;
            }
            
            match = Regex.Match(qteCorrectSection, "\"shakeDuration\"\\s*:\\s*([0-9.]+)");
            if (match.Success && float.TryParse(match.Groups[1].Value, out float duration))
            {
                settings.QTECorrectShakeDuration = duration;
            }
        }
        
        // QTE Wrong (nested object)
        string qteWrongSection = _ExtractNestedObject(json, "qteWrong");
        if (!string.IsNullOrEmpty(qteWrongSection))
        {
            match = Regex.Match(qteWrongSection, "\"shakeIntensity\"\\s*:\\s*([0-9.]+)");
            if (match.Success && float.TryParse(match.Groups[1].Value, out float intensity))
            {
                settings.QTEWrongShakeIntensity = intensity;
            }
            
            match = Regex.Match(qteWrongSection, "\"shakeDuration\"\\s*:\\s*([0-9.]+)");
            if (match.Success && float.TryParse(match.Groups[1].Value, out float duration))
            {
                settings.QTEWrongShakeDuration = duration;
            }
            
            match = Regex.Match(qteWrongSection, "\"slowmo\"\\s*:\\s*(true|false)");
            if (match.Success)
            {
                settings.QTEWrongSlowmo = match.Groups[1].Value == "true";
            }
            
            match = Regex.Match(qteWrongSection, "\"slowmoScale\"\\s*:\\s*([0-9.]+)");
            if (match.Success && float.TryParse(match.Groups[1].Value, out float qteWrongSlowmoScale))
            {
                settings.QTEWrongSlowmoScale = qteWrongSlowmoScale;
            }
            
            match = Regex.Match(qteWrongSection, "\"slowmoDuration\"\\s*:\\s*([0-9.]+)");
            if (match.Success && float.TryParse(match.Groups[1].Value, out float qteWrongSlowmoDuration))
            {
                settings.QTEWrongSlowmoDuration = qteWrongSlowmoDuration;
            }
        }
        
        // Combo (nested object)
        string comboSection = _ExtractNestedObject(json, "combo");
        if (!string.IsNullOrEmpty(comboSection))
        {
            match = Regex.Match(comboSection, "\"shakeIntensity\"\\s*:\\s*([0-9.]+)");
            if (match.Success && float.TryParse(match.Groups[1].Value, out float intensity))
            {
                settings.ComboShakeIntensity = intensity;
            }
            
            match = Regex.Match(comboSection, "\"shakeDuration\"\\s*:\\s*([0-9.]+)");
            if (match.Success && float.TryParse(match.Groups[1].Value, out float duration))
            {
                settings.ComboShakeDuration = duration;
            }
        }
        
        // MindBroken (nested object)
        string mindBrokenSection = _ExtractNestedObject(json, "mindBroken");
        if (!string.IsNullOrEmpty(mindBrokenSection))
        {
            match = Regex.Match(mindBrokenSection, "\"shakeIntensity\"\\s*:\\s*([0-9.]+)");
            if (match.Success && float.TryParse(match.Groups[1].Value, out float intensity))
            {
                settings.MindBrokenShakeIntensity = intensity;
            }
        }
        
        // Climax (nested object)
        string climaxSection = _ExtractNestedObject(json, "climax");
        if (!string.IsNullOrEmpty(climaxSection))
        {
            match = Regex.Match(climaxSection, "\"slowmo\"\\s*:\\s*(true|false)");
            if (match.Success)
            {
                settings.ClimaxSlowmo = match.Groups[1].Value == "true";
            }
            
            match = Regex.Match(climaxSection, "\"slowmoScale\"\\s*:\\s*([0-9.]+)");
            if (match.Success && float.TryParse(match.Groups[1].Value, out float climaxSlowmoScale))
            {
                settings.ClimaxSlowmoScale = climaxSlowmoScale;
            }
            
            match = Regex.Match(climaxSection, "\"slowmoDuration\"\\s*:\\s*([0-9.]+)");
            if (match.Success && float.TryParse(match.Groups[1].Value, out float climaxSlowmoDuration))
            {
                settings.ClimaxSlowmoDuration = climaxSlowmoDuration;
            }
            
            match = Regex.Match(climaxSection, "\"zoom\"\\s*:\\s*([0-9.]+)");
            if (match.Success && float.TryParse(match.Groups[1].Value, out float climaxZoom))
            {
                settings.ClimaxZoom = climaxZoom;
            }
            
            match = Regex.Match(climaxSection, "\"pulse\"\\s*:\\s*(true|false)");
            if (match.Success)
            {
                settings.ClimaxPulse = match.Groups[1].Value == "true";
            }
        }
        
        return settings;
    }
    
    /// <summary>
    /// Extract nested JSON object by name with proper brace counting.
    /// </summary>
    private string _ExtractNestedObject(string json, string objectName)
    {
        string searchPattern = "\"" + objectName + "\"";
        int startIndex = json.IndexOf(searchPattern, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            return null;
        }
        
        // Find opening brace after object name
        int braceStart = json.IndexOf('{', startIndex);
        if (braceStart < 0)
        {
            return null;
        }
        
        // Count braces for proper nested object extraction
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
            return json.Substring(braceStart + 1, braceEnd - braceStart - 1);
        }
        
        return null;
    }
    
    internal EnemyCameraSettings GetEnemySettings(string enemyName)
    {
        if (!string.IsNullOrEmpty(enemyName) && _enemySettings.TryGetValue(enemyName, out var settings))
        {
            return settings;
        }
        return _defaultSettings;
    }
    
    private static string GetDataPath()
    {
        // Primary path: BepInEx/plugins/HellGateJson/
        try
        {
            string basePath = Path.Combine(Application.dataPath, "..");
            string bepInEx = Path.Combine(basePath, "BepInEx");
            string plugins = Path.Combine(bepInEx, "plugins");
            string hellGateJson = Path.Combine(plugins, "HellGateJson");
            
            if (Directory.Exists(hellGateJson))
            {
                // Get language from config, fallback to "EN" if not set
                string languageCode = Plugin.hellGateLanguage?.Value ?? "EN";
                if (string.IsNullOrEmpty(languageCode))
                {
                    languageCode = "EN"; // Fallback
                }
                
                // Add language folder
                string langPath = Path.Combine(hellGateJson, languageCode);
                
                // Check existence, fallback to EN if not found
                if (Directory.Exists(langPath))
                {
                    return Path.Combine(langPath, "CameraSettings.json");
                }
                
                // Fallback to EN if selected language not found
                string enPath = Path.Combine(hellGateJson, "EN");
                if (Directory.Exists(enPath))
                {
                    return Path.Combine(enPath, "CameraSettings.json");
                }
                
                // If even EN doesn't exist, return from root folder (for backward compatibility)
                return Path.Combine(hellGateJson, "CameraSettings.json");
            }
        }
        catch { }

        // Fallback
        string basePathFallback = Path.Combine(Application.dataPath, "..");
        string bepInExFallback = Path.Combine(basePathFallback, "BepInEx");
        string pluginsFallback = Path.Combine(bepInExFallback, "plugins");
        string hellGateJsonFallback = Path.Combine(pluginsFallback, "HellGateJson");
        
        // Try adding language folder in fallback path
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
                return Path.Combine(langPathFallback, "CameraSettings.json");
            }
            string enPathFallback = Path.Combine(hellGateJsonFallback, "EN");
            if (Directory.Exists(enPathFallback))
            {
                return Path.Combine(enPathFallback, "CameraSettings.json");
            }
        }
        catch { }
        
        return Path.Combine(hellGateJsonFallback, "CameraSettings.json");
    }
}

