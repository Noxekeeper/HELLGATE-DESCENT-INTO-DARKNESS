using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using HarmonyLib;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod;
using NoREroMod.Systems.Audio;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// Dirty threats system when ready for grab
/// Integrates with CanEliteGrabPlayer events for contextual phrases
/// </summary>
internal static class GrabThreatDialogues
{
    // Threats data storage
    private static Dictionary<string, List<string>> _grabThreats = new();
    private static Dictionary<string, Dictionary<string, List<string>>> _perEnemyOverrides = new();
    private static bool _initialized = false;
    
    // Track cooldowns for each enemy
    private static readonly Dictionary<object, float> _lastThreatTime = new();
    
    // Global: last time ANY threat was shown (spam control with many enemies)
    private static float _lastGlobalThreatDisplayTime = -999f;
    
    // Track active threats to limit quantity
    private static readonly List<object> _activeThreats = new();

    /// <summary>Enemy keys that use only perEnemyOverrides — never the global grabThreats pool.</summary>
    private static readonly HashSet<string> ExclusivePerEnemyThreatKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "slavebigaxe",
        "otherslavebigaxe"
    };
    
    // Settings from JSON
    private static float _threatCooldown = 10.0f;
    private static int _maxSimultaneousThreats = 1;
    private static float _visibilityModifier = 0.5f;
    private static float _displayDuration = 3.0f;
    private static float _minThreatDistance = 2f;
    private static float _maxThreatDistance = 5f;
    private static float _threatCheckInterval = 0.4f;
    private static float[] _mindBrokenThresholds = { 0.0f, 0.3f, 0.7f };
    private static bool _onlyForElites = false; // If false, show for all enemies
    private static Color _threatColor = Color.red;
    private static Color _threatOutlineColor = Color.black;
    private static bool _colorFromJson = false;
    
    // ✨ УДАЛЕНО: Локальный кеш playercon - используем UnifiedPlayerCacheManager

    /// <summary>
    /// Initialize threats system
    /// </summary>
    internal static void Initialize()
    {
        if (_initialized) return;
        
        try
        {
            LoadGrabThreatsData();
            _initialized = true;
        }
        catch (Exception ex)
        {
        }
    }
    
    /// <summary>
    /// Load threat data from JSON (manual parsing)
    /// </summary>
    private static void LoadGrabThreatsData()
    {
        // Clear old data before load
        _grabThreats?.Clear();
        _perEnemyOverrides?.Clear();
        
        try
        {
            string dataPath = GetDataPath();
            string jsonPath = Path.Combine(dataPath, "GrabThreatsData.json");

            if (!File.Exists(jsonPath))
            {
                _colorFromJson = false;
                return;
            }

            string jsonText = File.ReadAllText(jsonPath);
            // Logging disabled for оптимизации
            ParseJsonManually(jsonText);
            EnsureSlaveBigAxeOverridesFromEnglishFallback();
        }
        catch (Exception ex)
        {
            _grabThreats?.Clear();
            _perEnemyOverrides?.Clear();
            _colorFromJson = false;
        }
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
    /// Manual parsing JSON (as in other файлах проекта)
    /// </summary>
    private static void ParseJsonManually(string json)
    {
        // Парсим settings
        ParseSettings(json);
        
        // Парсим grabThreats
        ParseGrabThreats(json);
        
        // Парсим perEnemyOverrides
        ParsePerEnemyOverrides(json);
    }
    
    /// <summary>
    /// Parsing секции settings
    /// </summary>
    private static void ParseSettings(string json)
    {
        int startIdx = json.IndexOf("\"settings\"", StringComparison.OrdinalIgnoreCase);
        if (startIdx < 0) return;
        int braceStart = json.IndexOf('{', startIdx);
        if (braceStart < 0) return;
        int depth = 1;
        int endIdx = braceStart + 1;
        while (endIdx < json.Length && depth > 0)
        {
            char c = json[endIdx];
            if (c == '{') depth++;
            else if (c == '}') depth--;
            endIdx++;
        }
        string settingsContent = depth == 0 && endIdx <= json.Length ? json.Substring(braceStart + 1, endIdx - braceStart - 2) : "";
        if (string.IsNullOrEmpty(settingsContent)) return;

        // Парсим threatCooldownSeconds
            var cooldownMatch = Regex.Match(settingsContent, "\"threatCooldownSeconds\"\\s*:\\s*([0-9.]+)");
            if (cooldownMatch.Success && float.TryParse(cooldownMatch.Groups[1].Value, out float cooldown))
            {
                _threatCooldown = cooldown;
            }
            
            // Парсим maxSimultaneousThreats
            var maxMatch = Regex.Match(settingsContent, "\"maxSimultaneousThreats\"\\s*:\\s*([0-9]+)");
            if (maxMatch.Success && int.TryParse(maxMatch.Groups[1].Value, out int maxThreats))
            {
                _maxSimultaneousThreats = maxThreats;
            }
            
            // Парсим visibilityDistanceModifier
            var visMatch = Regex.Match(settingsContent, "\"visibilityDistanceModifier\"\\s*:\\s*([0-9.]+)");
            if (visMatch.Success && float.TryParse(visMatch.Groups[1].Value, out float visMod))
            {
                _visibilityModifier = visMod;
            }
            
            // Парсим threatDisplayDuration
            var durMatch = Regex.Match(settingsContent, "\"threatDisplayDuration\"\\s*:\\s*([0-9.]+)");
            if (durMatch.Success && float.TryParse(durMatch.Groups[1].Value, out float duration))
            {
                _displayDuration = duration;
            }
            
            // Парсим onlyForElites
            var eliteMatch = Regex.Match(settingsContent, "\"onlyForElites\"\\s*:\\s*(true|false)");
            if (eliteMatch.Success)
            {
                _onlyForElites = eliteMatch.Groups[1].Value == "true";
            }
            
            var minDistMatch = Regex.Match(settingsContent, "\"minThreatDistance\"\\s*:\\s*([0-9.]+)");
            if (minDistMatch.Success && float.TryParse(minDistMatch.Groups[1].Value, out float minDist))
            {
                _minThreatDistance = minDist;
            }
            
            var maxDistMatch = Regex.Match(settingsContent, "\"maxThreatDistance\"\\s*:\\s*([0-9.]+)");
            if (maxDistMatch.Success && float.TryParse(maxDistMatch.Groups[1].Value, out float maxDist))
            {
                _maxThreatDistance = maxDist;
            }
            
            var checkIntervalMatch = Regex.Match(settingsContent, "\"threatCheckInterval\"\\s*:\\s*([0-9.]+)");
            if (checkIntervalMatch.Success && float.TryParse(checkIntervalMatch.Groups[1].Value, out float checkInterval))
            {
                _threatCheckInterval = checkInterval;
            }
            
            // Парсим mindBrokenThresholds
            var thresholdsMatch = Regex.Match(settingsContent, "\"mindBrokenThresholds\"\\s*:\\s*\\[([^\\]]+)\\]");
            if (thresholdsMatch.Success)
            {
                string thresholdsStr = thresholdsMatch.Groups[1].Value;
                var thresholdMatches = Regex.Matches(thresholdsStr, "([0-9.]+)");
                if (thresholdMatches.Count >= 3)
                {
                    _mindBrokenThresholds = new float[3];
                    for (int i = 0; i < 3 && i < thresholdMatches.Count; i++)
                    {
                        if (float.TryParse(thresholdMatches[i].Groups[1].Value, out float threshold))
                        {
                            _mindBrokenThresholds[i] = threshold;
                        }
                    }
                }
            }

            _threatColor = ParseColorFromSettings(settingsContent, "threatColor", Color.clear);
            if (_threatColor == Color.clear) _threatColor = ParseColorFromSettings(settingsContent, "goblinColor", Color.red);
            _threatOutlineColor = ParseColorFromSettings(settingsContent, "threatOutlineColor", Color.clear);
            if (_threatOutlineColor == Color.clear) _threatOutlineColor = ParseColorFromSettings(settingsContent, "goblinOutlineColor", Color.black);
            _colorFromJson = ParseColorFromSettings(settingsContent, "threatColor", Color.clear) != Color.clear || ParseColorFromSettings(settingsContent, "goblinColor", Color.clear) != Color.clear;
    }

    /// <summary>
    /// Parsing цвета from настроек
    /// </summary>
    private static Color ParseColorFromSettings(string settingsContent, string colorName, Color defaultColor)
    {
        string pattern = $"\"{colorName}\"\\s*:\\s*\\{{([^}}]+)\\}}";
        Match match = Regex.Match(settingsContent, pattern, RegexOptions.Singleline);

        if (match.Success)
        {
            string colorContent = match.Groups[1].Value;

            float r = defaultColor.r, g = defaultColor.g, b = defaultColor.b, a = defaultColor.a;

            var rMatch = Regex.Match(colorContent, "\"r\"\\s*:\\s*([0-9.]+)");
            if (rMatch.Success) float.TryParse(rMatch.Groups[1].Value, out r);

            var gMatch = Regex.Match(colorContent, "\"g\"\\s*:\\s*([0-9.]+)");
            if (gMatch.Success) float.TryParse(gMatch.Groups[1].Value, out g);

            var bMatch = Regex.Match(colorContent, "\"b\"\\s*:\\s*([0-9.]+)");
            if (bMatch.Success) float.TryParse(bMatch.Groups[1].Value, out b);

            var aMatch = Regex.Match(colorContent, "\"a\"\\s*:\\s*([0-9.]+)");
            if (aMatch.Success) float.TryParse(aMatch.Groups[1].Value, out a);

            return new Color(r, g, b, a);
        }

        return defaultColor;
    }
    
    /// <summary>
    /// Parsing секции grabThreats
    /// FIXED: Использует более надежный regex for многострочных массивов
    /// </summary>
    private static void ParseGrabThreats(string json)
    {
        string[] levels = { "mild", "medium", "extreme" };
        
        // Находим секцию grabThreats
        int grabThreatsStart = json.IndexOf("\"grabThreats\"", StringComparison.OrdinalIgnoreCase);
        if (grabThreatsStart == -1)
        {
            return;
        }
        
        
        // Находим начало объекта grabThreats
        int braceStart = json.IndexOf('{', grabThreatsStart);
        if (braceStart == -1)
        {
            return;
        }
        
        // Находим конец объекта grabThreats (ищем закрывающую скобку)
        int braceCount = 0;
        int braceEnd = -1;
        for (int i = braceStart; i < json.Length; i++)
        {
            if (json[i] == '{') braceCount++;
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
        
        if (braceEnd == -1)
        {
            return;
        }
        
        string grabThreatsSection = json.Substring(braceStart + 1, braceEnd - braceStart - 1);
        
        foreach (string level in levels)
        {
            // Ищем массиin for этого уровня - use simple поиск строки
            string searchPattern = $"\"{level}\"";
            int levelStart = grabThreatsSection.IndexOf(searchPattern, StringComparison.OrdinalIgnoreCase);
            
            if (levelStart == -1)
            {
                // Logging disabled for оптимизации
                continue;
            }
            
            // Находим начало массива (after двоеточия)
            int colonPos = grabThreatsSection.IndexOf(':', levelStart);
            if (colonPos == -1) continue;
            
            // Skip пробелы after двоеточия
            int arrayStart = colonPos + 1;
            while (arrayStart < grabThreatsSection.Length && char.IsWhiteSpace(grabThreatsSection[arrayStart]))
            {
                arrayStart++;
            }
            
            // Check that this действительbut массив
            if (arrayStart >= grabThreatsSection.Length || grabThreatsSection[arrayStart] != '[')
            {
            continue;
            }
            
            // Находим конец массива (ищем закрывающую скобку)
            int bracketCount = 0;
            int arrayEnd = -1;
            for (int i = arrayStart; i < grabThreatsSection.Length; i++)
            {
                if (grabThreatsSection[i] == '[') bracketCount++;
                else if (grabThreatsSection[i] == ']')
                {
                    bracketCount--;
                    if (bracketCount == 0)
                    {
                        arrayEnd = i;
                        break;
                    }
                }
            }
            
            if (arrayEnd == -1) continue;
            
            string arrayContent = grabThreatsSection.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
            List<string> threats = ParseStringArray(arrayContent);
            
            if (threats.Count > 0)
            {
                _grabThreats[level] = threats;
            }
            else
            {
                // Logging disabled for оптимизации
                // Plugin.Log.LogWarning($"[GrabThreats] No threats parsed for level '{level}'");
            }
        }
        
    }
    
    /// <summary>
    /// Index of closing brace matching json[openBraceIndex] == '{' (handles nested objects and strings).
    /// </summary>
    private static int FindMatchingClosingBrace(string json, int openBraceIndex)
    {
        if (openBraceIndex < 0 || openBraceIndex >= json.Length || json[openBraceIndex] != '{')
            return -1;
        int depth = 0;
        int i = openBraceIndex;
        while (i < json.Length)
        {
            char c = json[i];
            if (c == '"')
            {
                i++;
                while (i < json.Length)
                {
                    if (json[i] == '\\') { i += 2; continue; }
                    if (json[i] == '"') { i++; break; }
                    i++;
                }
                continue;
            }
            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
            i++;
        }
        return -1;
    }

    /// <summary>
    /// Parsing секции perEnemyOverrides (nested braces — старый regex [^}]+ ломался after первого врага).
    /// </summary>
    private static void ParsePerEnemyOverrides(string json)
    {
        ParsePerEnemyOverridesInto(json, _perEnemyOverrides);
    }

    /// <summary>
    /// RU/другие языки часто без slavebigaxe — подставляем фразы из EN (совпадают с WAV в BigSlaveAxe).
    /// </summary>
    private static void EnsureSlaveBigAxeOverridesFromEnglishFallback()
    {
        if (HasNonEmptySlaveBigAxeOverrides()) return;
        try
        {
            string basePath = Path.Combine(Application.dataPath, "..");
            string enPath = Path.Combine(
                Path.Combine(Path.Combine(Path.Combine(Path.Combine(basePath, "BepInEx"), "plugins"), "HellGateJson"), "EN"),
                "GrabThreatsData.json");
            if (!File.Exists(enPath)) return;
            string enJson = File.ReadAllText(enPath);
            var temp = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);
            ParsePerEnemyOverridesInto(enJson, temp);
            if (temp.TryGetValue("slavebigaxe", out var pack) && pack != null && pack.Count > 0)
                _perEnemyOverrides["slavebigaxe"] = pack;
        }
        catch { }
    }

    private static bool HasNonEmptySlaveBigAxeOverrides()
    {
        if (!_perEnemyOverrides.TryGetValue("slavebigaxe", out var d) || d == null) return false;
        foreach (var kv in d)
        {
            if (kv.Value != null && kv.Value.Count > 0) return true;
        }
        return false;
    }

    private static void ParsePerEnemyOverridesInto(string json, Dictionary<string, Dictionary<string, List<string>>> target)
    {
        int keyIdx = json.IndexOf("\"perEnemyOverrides\"", StringComparison.OrdinalIgnoreCase);
        if (keyIdx < 0) return;
        int colon = json.IndexOf(':', keyIdx);
        if (colon < 0) return;
        int braceStart = json.IndexOf('{', colon);
        if (braceStart < 0) return;
        int braceEnd = FindMatchingClosingBrace(json, braceStart);
        if (braceEnd < 0) return;
        string inner = json.Substring(braceStart + 1, braceEnd - braceStart - 1);

        int pos = 0;
        while (pos < inner.Length)
        {
            while (pos < inner.Length && char.IsWhiteSpace(inner[pos])) pos++;
            if (pos >= inner.Length) break;
            if (inner[pos] != '"') { pos++; continue; }
            int keyStart = pos + 1;
            int keyEnd = inner.IndexOf('"', keyStart);
            if (keyEnd < 0) break;
            string enemyType = inner.Substring(keyStart, keyEnd - keyStart);
            pos = keyEnd + 1;
            while (pos < inner.Length && char.IsWhiteSpace(inner[pos])) pos++;
            if (pos >= inner.Length || inner[pos] != ':') break;
            pos++;
            while (pos < inner.Length && char.IsWhiteSpace(inner[pos])) pos++;
            if (pos >= inner.Length || inner[pos] != '{') break;
            int objEnd = FindMatchingClosingBrace(inner, pos);
            if (objEnd < 0) break;
            string enemyContent = inner.Substring(pos + 1, objEnd - pos - 1);
            pos = objEnd + 1;

            Dictionary<string, List<string>> enemyThreats = new();
            string[] levels = { "mild", "medium", "extreme" };
            foreach (string level in levels)
            {
                string levelPattern = $"\"{level}\"\\s*:\\s*\\[([^\\]]+)\\]";
                Match levelMatch = Regex.Match(enemyContent, levelPattern, RegexOptions.Singleline);
                if (levelMatch.Success)
                    enemyThreats[level] = ParseStringArray(levelMatch.Groups[1].Value);
            }
            if (enemyThreats.Count > 0)
                target[enemyType] = enemyThreats;
        }
    }
    
    /// <summary>
    /// Parse string array from JSON
    /// </summary>
    private static List<string> ParseStringArray(string arrayContent)
    {
        List<string> result = new();
        
        // Find all строки in кавычках
        var matches = Regex.Matches(arrayContent, "\"([^\"]+)\"");
        
        foreach (Match match in matches)
        {
            string value = match.Groups[1].Value;
            // Декодируем escape-последовательности
            value = value.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\\\", "\\");
            result.Add(value);
        }
        
        return result;
    }
    
    /// <summary>
    /// Main entry: show threat when enemy transitions to IDLE (stops after attack).
    /// Called from GrabThreatIdlePatch when setanimation("IDLE") is invoked.
    /// 100% probability, 10s cooldown per enemy, max 1 simultaneous (spam control).
    /// </summary>
    internal static void TryShowThreatOnIdle(EnemyDate enemy)
    {
        try
        {
            if (!_initialized || enemy == null || enemy.Hp <= 0) return;
            if (Plugin.enableGrabThreats != null && !Plugin.enableGrabThreats.Value) return;
            if (enemy.eroflag) return;
            if (!AttackSoundRegistry.IsThreatEnabledPrefab(enemy)) return;
            playercon player = enemy.com_player ?? UnifiedPlayerCacheManager.GetPlayer();
            if (player == null || player.eroflag) return;

            // Distance check: only show when player is close (config: minThreatDistance to maxThreatDistance)
            float distance = Vector2.Distance(enemy.transform.position, player.transform.position);
            if (distance > _maxThreatDistance) return;
            if (_minThreatDistance > 0 && distance < _minThreatDistance) return;

            if (IsOnCooldown(enemy)) return;

            // Global cooldown: min interval between ANY threat (matches display duration)
            float now = Time.time;
            if ((now - _lastGlobalThreatDisplayTime) < _displayDuration) return;

            CleanupExpiredThreats();
            if (_activeThreats.Count >= _maxSimultaneousThreats) return;

            if (_onlyForElites)
            {
                string jpName = (Traverse.Create(enemy).Field("JPname").GetValue() as string) ?? "";
                if (!jpName.Contains("<SUPER>")) return;
            }

            string enemyType = GetEnemyType(enemy);
            string threatLevel = GetThreatLevel();
            string threat = SelectRandomThreat(threatLevel, enemyType);
            if (string.IsNullOrEmpty(threat)) return;

            if (Plugin.enableGrabThreatsText != null && Plugin.enableGrabThreatsText.Value)
                ShowThreat(enemy, threat, threatLevel);
            NoREroMod.Systems.Audio.AttackSoundSystem.TryPlayThreatSound(enemy, threat);

            _lastThreatTime[enemy] = now;
            _lastGlobalThreatDisplayTime = now;
            _activeThreats.Add(enemy);
        }
        catch { }
    }

    /// <summary>
    /// Legacy: Check и генерация угрозы (kept for API compatibility, prefer TryShowThreatOnIdle)
    /// </summary>
    internal static void ProcessGrabThreat(object enemyInstance, string enemyType)
    {
        try
        {
            if (!_initialized) return;
            if (Plugin.enableGrabThreats != null && !Plugin.enableGrabThreats.Value) return;
            
            if (enemyInstance == null)
            {
            return;
            }

            if (enemyInstance is EnemyDate edLegacy && !AttackSoundRegistry.IsThreatEnabledPrefab(edLegacy))
                return;
            
            // Check cooldown for этого enemy
            if (IsOnCooldown(enemyInstance))
            {
                return;
            }
            
            // Check visibility enemy
            if (!IsEnemyVisible(enemyInstance))
            {
                return;
            }
            
            // Check limit одновременных угроз
            CleanupExpiredThreats();
            if (_activeThreats.Count >= _maxSimultaneousThreats)
            {
                return;
            }
            
            // Определяем уровень угрозы on основе MindBroken%
            string threatLevel = GetThreatLevel();
            
            // Выбираем случайную phrase
            string threat = SelectRandomThreat(threatLevel, enemyType);
            if (string.IsNullOrEmpty(threat))
            {
                return;
            }
            
            
            if (Plugin.enableGrabThreatsText != null && Plugin.enableGrabThreatsText.Value)
                ShowThreat(enemyInstance, threat, threatLevel);

            EnemyDate enemyDate = enemyInstance as EnemyDate;
            if (enemyDate != null)
                NoREroMod.Systems.Audio.AttackSoundSystem.TryPlayThreatSound(enemyDate, threat);
            
            // Update данные отслеживания
            _lastThreatTime[enemyInstance] = Time.time;
            _activeThreats.Add(enemyInstance);
            
        }
        catch (Exception ex)
        {
        }
    }
    
    /// <summary>
    /// Check кулдауon for enemy
    /// </summary>
    private static bool IsOnCooldown(object enemyInstance)
    {
        if (!_lastThreatTime.ContainsKey(enemyInstance))
        {
            return false;
        }
        
        float timeSinceLastThreat = Time.time - _lastThreatTime[enemyInstance];
        return timeSinceLastThreat < _threatCooldown;
    }
    
    /// <summary>
    /// Visibility check enemy (on основе расстояния)
    /// Упрощено: проверяем only that enemy not слишком далеко (until 30 единиц)
    /// ОПТИМИЗИРОВАНО: Использует кешированный playercon и enemy.com_player
    /// </summary>
    private static bool IsEnemyVisible(object enemyInstance)
    {
        try
        {
            MonoBehaviour enemyMB = enemyInstance as MonoBehaviour;
            if (enemyMB == null) return false;
            
            playercon player = null;
            
            // PRIORITY 1: Используем enemy.com_player directly (самый быстрый способ)
            EnemyDate enemy = enemyMB as EnemyDate;
            if (enemy != null && enemy.com_player != null)
            {
                player = enemy.com_player;
            }
            else
            {
                // Optimized: Используем UnifiedPlayerCacheManager
                player = UnifiedPlayerCacheManager.GetPlayer();
            }
            
            if (player == null) return false;
            
            float distance = Vector2.Distance(enemyMB.transform.position, player.transform.position);
            return distance <= _maxThreatDistance;
        }
        catch
        {
            return true; // In case of error разрешаем показ
        }
    }
    
    /// <summary>
    /// Очистка expired угроз from activeго списка
    /// </summary>
    private static void CleanupExpiredThreats()
    {
        float currentTime = Time.time;
        _activeThreats.RemoveAll(threat => 
        {
            if (!_lastThreatTime.ContainsKey(threat))
            {
                return true;
            }
            return (currentTime - _lastThreatTime[threat]) > _displayDuration;
        });
    }
    
    /// <summary>
    /// Определение уровня угрозы on основе MindBroken процента
    /// </summary>
    private static string GetThreatLevel()
    {
        try
        {
            float mindBrokenPercent = MindBrokenSystem.Percent;
            
            if (mindBrokenPercent < _mindBrokenThresholds[1]) // < 30%
            {
                return "mild";
            }
            else if (mindBrokenPercent < _mindBrokenThresholds[2]) // 30-70%
            {
                return "medium";
            }
            else // 70-100%
            {
                return "extreme";
            }
        }
        catch
        {
            return "mild"; // Fallback
        }
    }
    
    /// <summary>
    /// Выбор случайной угрозы from соответствующей категории
    /// </summary>
    private static string SelectRandomThreat(string threatLevel, string enemyType)
    {
        try
        {
            // Big axe variants share the same JSON pack under "slavebigaxe"
            string overrideLookupKey = enemyType;
            if (string.Equals(enemyType, "otherslavebigaxe", StringComparison.OrdinalIgnoreCase))
                overrideLookupKey = "slavebigaxe";

            // Сначала пробуем специфичные for enemy phrases
            if (_perEnemyOverrides.ContainsKey(overrideLookupKey) &&
                _perEnemyOverrides[overrideLookupKey].ContainsKey(threatLevel))
            {
                var enemyThreats = _perEnemyOverrides[overrideLookupKey][threatLevel];
                if (enemyThreats != null && enemyThreats.Count > 0)
                {
                    return enemyThreats[UnityEngine.Random.Range(0, enemyThreats.Count)];
                }
            }

            if (ExclusivePerEnemyThreatKeys.Contains(enemyType))
                return string.Empty;
            
            // Fallback: используем общие phrases
            if (_grabThreats.ContainsKey(threatLevel))
            {
                var threats = _grabThreats[threatLevel];
                if (threats != null && threats.Count > 0)
                {
                    return threats[UnityEngine.Random.Range(0, threats.Count)];
                }
            }
            
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
    
    /// <summary>
    /// Display угрозу над головой enemy
    /// </summary>
    private static void ShowThreat(object enemyInstance, string threat, string threatLevel)
    {
        try
        {
            var fontStyle = Plugin.GetFontStyle(Plugin.threatFontStyle.Value);
            Color textColor = _colorFromJson ? _threatColor : (Plugin.threatColor != null ? Plugin.ParseColor(Plugin.threatColor.Value) : Color.red);
            Color outlineColor = _colorFromJson ? _threatOutlineColor : (Plugin.threatOutlineColor != null ? Plugin.ParseColor(Plugin.threatOutlineColor.Value) : Color.black);
            if (textColor == Color.white) textColor = new Color(1f, 0f, 0f, 1f);
            float verticalOffset = GetVerticalOffsetForEnemy(enemyInstance);
            var style = new DialogueStyle
            {
                Color = textColor,
                FontSize = Plugin.dialogueFontSize.Value,
                IsBold = (fontStyle & FontStyle.Bold) != 0,
                IsItalic = (fontStyle & FontStyle.Italic) != 0,
                VerticalOffset = verticalOffset,
                FollowBone = true,
                UseOutline = true,
                OutlineColor = outlineColor,
                OutlineDistance = new UnityEngine.Vector2(1f, -1f)
            };
            
            string boneName = GetBoneNameForEnemy(enemyInstance);
            if (string.IsNullOrEmpty(boneName)) return; // Нет кости — не показываем, без фоллбеков

            var bonePos = new BonePosition
            {
                BoneName = boneName,
                UseScreenCenter = false,
            };
            
            // Use статичный вывод bound to bone
            var display = DialogueFramework.GetDialogueDisplay();
            if (display != null)
            {
                // Вызываем статичную версию bound to bone, but without animation движения
                display.ShowStaticThreat(enemyInstance, threat, bonePos, style, _displayDuration);
            }
        }
        catch (Exception ex)
        {
        }
    }
    
    /// <summary>
    /// Per-enemy vertical offset (BlackMafia needs higher position, others 100px).
    /// </summary>
    private static float GetVerticalOffsetForEnemy(object enemyInstance)
    {
        if (enemyInstance == null) return 100f;
        string typeName = (enemyInstance as MonoBehaviour)?.GetType().Name ?? "";
        if (typeName == "BlackMafia") return 120f;
        return 100f;
    }

    /// <summary>
    /// Map EnemyDate type to JSON enemy key (touzoku, dorei, etc.)
    /// </summary>
    private static string GetEnemyType(EnemyDate enemy)
    {
        if (enemy == null) return "unknown";
        string typeName = enemy.GetType().Name.ToLowerInvariant();
        if (typeName.Contains("goblin")) return "goblin";
        if (typeName.Contains("mutude")) return "mutude";
        if (typeName.Contains("touzoku")) return "touzoku";
        if (typeName.Contains("kakash")) return "kakasi";
        if (typeName.Contains("otherslavebigaxe")) return "otherslavebigaxe";
        if (typeName.Contains("slavebigaxe")) return "slavebigaxe";
        if (typeName.Contains("sinnerslave") || typeName.Contains("dorei")) return "dorei";
        if (typeName.Contains("inquisition")) return "inquisition";
        if (typeName.Contains("bigoni")) return "bigoni";
        if (typeName.Contains("mafiamuscle") || typeName.Contains("blackmafia")) return "mafiamuscle";
        if (typeName.Contains("vagrant")) return "vagrant";
        return typeName;
    }

    /// <summary>
    /// Get name кости for specific enemy type. Returns null if type unknown — no fallbacks.
    /// </summary>
    private static string GetBoneNameForEnemy(object enemyInstance)
    {
        if (enemyInstance == null) return null;
        if (!(enemyInstance is MonoBehaviour mb)) return null;

        string enemyTypeName = mb.GetType().Name;

        if (enemyTypeName == "TouzokuNormal" || enemyTypeName == "TouzokuAxe")
            return "bone32";
        if (enemyTypeName == "SinnerslaveCrossbow" || enemyTypeName.Contains("Dorei") || enemyTypeName.Contains("Sinnerslave"))
            return "face";
        if (enemyTypeName == "Mafiamuscle" || enemyTypeName == "MafiaBossCustom" || enemyTypeName == "BlackMafia")
            return "bone4";
        if (enemyTypeName == "SlaveBigAxe" || enemyTypeName == "OtherSlavebigAxe")
            return "bone4";
        if (enemyTypeName == "Vagrant" || enemyTypeName == "VagrantGuard" || enemyTypeName == "VagrantThrow")
            return "bone3";

        return null;
    }
    
    /// <summary>
    /// Получение цвета угрозы depending on уровня
    /// </summary>
    private static Color GetThreatColor(string threatLevel)
    {
        return threatLevel switch
        {
            "mild" => new Color(1f, 0.8f, 0.8f, 1f),    // Светло-red
            "medium" => new Color(1f, 0.4f, 0.4f, 1f),  // Красный  
            "extreme" => new Color(0.8f, 0f, 0f, 1f),   // Темно-red
            _ => new Color(1f, 0.6f, 0.6f, 1f)
        };
    }
    
    /// <summary>
    /// Очистка данных on change сцены
    /// </summary>
    internal static void Clear()
    {
        _lastThreatTime.Clear();
        _activeThreats.Clear();
        _lastGlobalThreatDisplayTime = -999f;
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
        _grabThreats?.Clear();
        _perEnemyOverrides?.Clear();
        Clear();
    }
    
    /// <summary>
    /// Показывать угрозы only for элитов
    /// </summary>
    internal static bool OnlyForElites => _onlyForElites;
    internal static float MinThreatDistance => _minThreatDistance;
    internal static float MaxThreatDistance => _maxThreatDistance;
    internal static float ThreatCheckInterval => _threatCheckInterval;
}
