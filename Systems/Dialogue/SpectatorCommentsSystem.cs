using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using Spine.Unity;
using NoREroMod;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Systems.Dialogue;

/// <summary>
/// Spectator comments system during H-scenes
/// Enemies in frame speak lines depending on H-scene stage
/// </summary>
internal static class SpectatorCommentsSystem
{
    // Comments data storage
    private static Dictionary<string, Dictionary<string, List<string>>> _spectatorComments = new();
    private static bool _initialized = false;
    
    // Track cooldowns for each enemy
    private static readonly Dictionary<EnemyDate, float> _lastCommentTime = new();
    
    // Track active comments to limit quantity
    private static readonly List<EnemyDate> _activeComments = new();
    
    // Settings from JSON
    private static float _commentCooldown = 4.0f;
    private static int _maxSimultaneousComments = 3;
    private static float _checkInterval = 2.0f;
    private static float _displayDuration = 3.0f; // Reduced from 4.0 to 3.0 seconds to prevent phrase overlap
    private static float _fontSize = 22.0f; // Size like enemies, loaded from JSON
    private static Color _commentColor = Color.yellow;
    
    // Viewport bounds for visibility check
    private static float _minViewportX = 0.0f;
    private static float _maxViewportX = 1.0f;
    private static float _minViewportY = 0.0f;
    private static float _maxViewportY = 1.0f;
    
    // ✨ УДАЛЕНО: Локальный кеш playercon - используем UnifiedPlayerCacheManager
    
    // Coroutine for monitoring
    private static MonoBehaviour _coroutineRunner = null;
    
    /// <summary>
    /// Initialize spectator comments system
    /// </summary>
    internal static void Initialize()
    {
        if (_initialized) return;
        
        try
        {
            LoadSpectatorCommentsData();
            
            // Create object for coroutines
            GameObject runnerObj = new GameObject("SpectatorCommentsCoroutineRunner");
            UnityEngine.Object.DontDestroyOnLoad(runnerObj);
            _coroutineRunner = runnerObj.AddComponent<SpectatorCommentsCoroutineRunner>();
            
            // Start monitoring coroutine
            _coroutineRunner.StartCoroutine(MonitorHSceneAndShowComments());
            
            _initialized = true;
        }
        catch (Exception ex)
        {
        }
    }
    
    /// <summary>
    /// Load data comments from JSON
    /// </summary>
    private static void LoadSpectatorCommentsData()
    {
        // Clear old data before load
        _spectatorComments?.Clear();
        
        try
        {
            string dataPath = GetDataPath();
            string jsonPath = Path.Combine(dataPath, "SpectatorCommentsData.json");

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
            _spectatorComments?.Clear();
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
    /// Manual parsing JSON
    /// </summary>
    private static void ParseJsonManually(string json)
    {
        ParseSettings(json);
        ParseSpectatorComments(json);
    }
    
    /// <summary>
    /// Parse settings
    /// </summary>
    private static void ParseSettings(string json)
    {
        int settingsStart = json.IndexOf("\"settings\"", StringComparison.OrdinalIgnoreCase);
        if (settingsStart == -1) return;
        
        int braceStart = json.IndexOf('{', settingsStart);
        if (braceStart == -1) return;
        
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
        
        if (braceEnd == -1) return;
        
        string settingsContent = json.Substring(braceStart + 1, braceEnd - braceStart - 1);
        
        // Parse settings
        var cooldownMatch = Regex.Match(settingsContent, "\"commentCooldownSeconds\"\\s*:\\s*([0-9.]+)");
        if (cooldownMatch.Success && float.TryParse(cooldownMatch.Groups[1].Value, out float cooldown))
        {
            _commentCooldown = cooldown;
        }
        
        var maxMatch = Regex.Match(settingsContent, "\"maxSimultaneousComments\"\\s*:\\s*([0-9]+)");
        if (maxMatch.Success && int.TryParse(maxMatch.Groups[1].Value, out int max))
        {
            _maxSimultaneousComments = max;
        }
        
        var intervalMatch = Regex.Match(settingsContent, "\"checkIntervalSeconds\"\\s*:\\s*([0-9.]+)");
        if (intervalMatch.Success && float.TryParse(intervalMatch.Groups[1].Value, out float interval))
        {
            _checkInterval = interval;
        }
        
        var durationMatch = Regex.Match(settingsContent, "\"commentDisplayDuration\"\\s*:\\s*([0-9.]+)");
        if (durationMatch.Success && float.TryParse(durationMatch.Groups[1].Value, out float duration))
        {
            _displayDuration = duration;
        }
        
        // fontSize now taken from config Plugin.dialogueFontSize.Value
        
        // Парсим цвет
        var colorMatch = Regex.Match(settingsContent, "\"commentColor\"\\s*:\\s*\\{([^}]+)\\}");
        if (colorMatch.Success)
        {
            var rMatch = Regex.Match(colorMatch.Groups[1].Value, "\"r\"\\s*:\\s*([0-9.]+)");
            var gMatch = Regex.Match(colorMatch.Groups[1].Value, "\"g\"\\s*:\\s*([0-9.]+)");
            var bMatch = Regex.Match(colorMatch.Groups[1].Value, "\"b\"\\s*:\\s*([0-9.]+)");
            
            if (rMatch.Success && gMatch.Success && bMatch.Success)
            {
                if (float.TryParse(rMatch.Groups[1].Value, out float r) &&
                    float.TryParse(gMatch.Groups[1].Value, out float g) &&
                    float.TryParse(bMatch.Groups[1].Value, out float b))
                {
                    _commentColor = new Color(r, g, b, 1.0f);
                }
            }
        }
    }
    
    /// <summary>
    /// Parsing comments spectators
    /// </summary>
    private static void ParseSpectatorComments(string json)
    {
        int commentsStart = json.IndexOf("\"spectatorComments\"", StringComparison.OrdinalIgnoreCase);
        if (commentsStart == -1) return;
        
        int braceStart = json.IndexOf('{', commentsStart);
        if (braceStart == -1) return;
        
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
        
        if (braceEnd == -1) return;
        
        string commentsSection = json.Substring(braceStart + 1, braceEnd - braceStart - 1);
        
        // Парсим for каждого enemy type
        string[] enemyTypes = { "touzoku", "dorei" };
        string[] stages = { "start", "ero", "fin" };
        
        foreach (string enemyType in enemyTypes)
        {
            Dictionary<string, List<string>> enemyComments = new();
            
            foreach (string stage in stages)
            {
                string searchPattern = $"\"{enemyType}\"";
                int enemyStart = commentsSection.IndexOf(searchPattern, StringComparison.OrdinalIgnoreCase);
                if (enemyStart == -1) continue;
                
                int enemyBraceStart = commentsSection.IndexOf('{', enemyStart);
                if (enemyBraceStart == -1) continue;
                
                int enemyBraceCount = 0;
                int enemyBraceEnd = -1;
                for (int i = enemyBraceStart; i < commentsSection.Length; i++)
                {
                    if (commentsSection[i] == '{') enemyBraceCount++;
                    else if (commentsSection[i] == '}')
                    {
                        enemyBraceCount--;
                        if (enemyBraceCount == 0)
                        {
                            enemyBraceEnd = i;
                            break;
                        }
                    }
                }
                
                if (enemyBraceEnd == -1) continue;
                
                string enemySection = commentsSection.Substring(enemyBraceStart + 1, enemyBraceEnd - enemyBraceStart - 1);
                
                // Ищем этап
                string stagePattern = $"\"{stage}\"";
                int stageStart = enemySection.IndexOf(stagePattern, StringComparison.OrdinalIgnoreCase);
                if (stageStart == -1) continue;
                
                int arrayStart = enemySection.IndexOf('[', stageStart);
                if (arrayStart == -1) continue;
                
                int bracketCount = 0;
                int arrayEnd = -1;
                for (int i = arrayStart; i < enemySection.Length; i++)
                {
                    if (enemySection[i] == '[') bracketCount++;
                    else if (enemySection[i] == ']')
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
                
                string arrayContent = enemySection.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                List<string> comments = ParseStringArray(arrayContent);
                
                if (comments.Count > 0)
                {
                    enemyComments[stage] = comments;
                    // Logs disabled
                    // Plugin.Log.LogInfo($"[SpectatorComments] Loaded {comments.Count} {stage} comments for {enemyType}");
                }
            }
            
            if (enemyComments.Count > 0)
            {
                _spectatorComments[enemyType] = enemyComments;
            }
        }
        
        // Logs disabled
        // Plugin.Log.LogInfo($"[SpectatorComments] Total enemy types loaded: {_spectatorComments.Count}");
    }
    
    /// <summary>
    /// Parse string array from JSON
    /// </summary>
    private static List<string> ParseStringArray(string arrayContent)
    {
        List<string> result = new();
        var matches = Regex.Matches(arrayContent, "\"([^\"]+)\"");
        
        foreach (Match match in matches)
        {
            string value = match.Groups[1].Value;
            value = value.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\\\", "\\");
            result.Add(value);
        }
        
        return result;
    }
    
    /// <summary>
    /// Coroutine for мониторинга H-scene и показа comments
    /// </summary>
    private static IEnumerator MonitorHSceneAndShowComments()
    {
        while (true)
        {
            yield return new WaitForSeconds(_checkInterval);
            
            if (!_initialized) continue;
            
            // Check that идет H-сцена
            if (!IsHSceneActive())
            {
                CleanupExpiredComments();
                continue;
            }
            
            // Get visibleых enemies in кадре
            List<EnemyDate> visibleEnemies = GetVisibleEnemiesInFrame();
            
            if (visibleEnemies.Count == 0) continue;
            
            // Clear истекшие комментарии
            CleanupExpiredComments();
            
            // Check limit simultaneous comments
            if (_activeComments.Count >= _maxSimultaneousComments) continue;
            
            // Определяем этап H-scene
            string stage = GetHSceneStage();
            
            // Выбираем случайного enemy for комментария
            List<EnemyDate> availableEnemies = new();
            foreach (EnemyDate enemy in visibleEnemies)
            {
                if (!_lastCommentTime.ContainsKey(enemy) || 
                    (Time.time - _lastCommentTime[enemy]) >= _commentCooldown)
                {
                    availableEnemies.Add(enemy);
                }
            }
            
            if (availableEnemies.Count == 0) continue;
            
            EnemyDate selectedEnemy = availableEnemies[UnityEngine.Random.Range(0, availableEnemies.Count)];
            string enemyType = GetEnemyType(selectedEnemy);
            
            // Выбираем комментарий
            string comment = SelectRandomComment(enemyType, stage);
            if (string.IsNullOrEmpty(comment)) continue;
            
            // Show comment
            ShowSpectatorComment(selectedEnemy, comment);
            
            // Update данные отслеживания
            _lastCommentTime[selectedEnemy] = Time.time;
            _activeComments.Add(selectedEnemy);
        }
    }
    
    /// <summary>
    /// Check that идет H-сцена
    /// </summary>
    private static bool IsHSceneActive()
    {
        try
        {
            playercon player = GetPlayer();
            return player != null && player.eroflag;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Optimized: Используем UnifiedPlayerCacheManager
    /// </summary>
    private static playercon GetPlayer()
    {
        return UnifiedPlayerCacheManager.GetPlayer();
    }
    
    /// <summary>
    /// Получение visibleых enemies in кадре
    /// </summary>
    private static List<EnemyDate> GetVisibleEnemiesInFrame()
    {
        List<EnemyDate> visibleEnemies = new();
        
        try
        {
            if (UnityEngine.Camera.main == null) return visibleEnemies;
            
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            
            EnemyDate[] allEnemies = UnityEngine.Object.FindObjectsOfType<EnemyDate>();
            
            foreach (EnemyDate enemy in allEnemies)
            {
                // Skip enemy which in H-сцене
                if (enemy.eroflag) continue;
                
                // Skip if enemy мертв
                if (enemy.Hp <= 0) continue;
                
                // Check visibility через viewport
                Vector3 viewportPos = mainCamera.WorldToViewportPoint(enemy.transform.position);
                
                if (viewportPos.x >= _minViewportX && viewportPos.x <= _maxViewportX &&
                    viewportPos.y >= _minViewportY && viewportPos.y <= _maxViewportY &&
                    viewportPos.z > 0)
                {
                    visibleEnemies.Add(enemy);
                }
            }
        }
        catch (Exception ex)
        {
        }
        
        return visibleEnemies;
    }
    
    /// <summary>
    /// Определение этапа H-scene (start/ero/fin)
    /// </summary>
    private static string GetHSceneStage()
    {
        try
        {
            // Ищем enemy which in H-сцене
            EnemyDate[] allEnemies = UnityEngine.Object.FindObjectsOfType<EnemyDate>();
            
            foreach (EnemyDate enemy in allEnemies)
            {
                if (enemy.eroflag)
                {
                    // Get current animation через Spine
                    SkeletonAnimation spine = enemy.GetComponentInChildren<SkeletonAnimation>();
                    if (spine != null && spine.skeleton != null && !string.IsNullOrEmpty(spine.AnimationName))
                    {
                        string animName = spine.AnimationName.ToUpperInvariant();
                        
                        if (animName.StartsWith("FIN") || animName == "FIN2")
                        {
                            return "fin";
                        }
                        else if (animName.StartsWith("ERO") || animName == "ERO0" || animName == "ERO")
                        {
                            return "ero";
                        }
                        else if (animName.StartsWith("START") || animName == "START2" || animName == "START3")
                        {
                            return "start";
                        }
                    }
                }
            }
        }
        catch { }
        
        // Fallback: возвращаем "ero" as наиболее вероятный этап
        return "ero";
    }
    
    /// <summary>
    /// Определение enemy type
    /// </summary>
    private static string GetEnemyType(EnemyDate enemy)
    {
        if (enemy == null) return "unknown";
        
        string typeName = enemy.GetType().Name.ToLowerInvariant();
        
        if (typeName.Contains("touzoku")) return "touzoku";
        if (typeName.Contains("dorei") || typeName.Contains("sinnerslave")) return "dorei";
        
        return "unknown";
    }
    
    /// <summary>
    /// Выбор случайного комментария
    /// </summary>
    private static string SelectRandomComment(string enemyType, string stage)
    {
        try
        {
            if (!_spectatorComments.ContainsKey(enemyType))
            {
                return string.Empty;
            }
            
            var enemyComments = _spectatorComments[enemyType];
            if (!enemyComments.ContainsKey(stage))
            {
                return string.Empty;
            }
            
            var comments = enemyComments[stage];
            if (comments == null || comments.Count == 0)
            {
                return string.Empty;
            }
            
            return comments[UnityEngine.Random.Range(0, comments.Count)];
        }
        catch
        {
            return string.Empty;
        }
    }
    
    /// <summary>
    /// Показ комментария зрителя
    /// </summary>
    private static void ShowSpectatorComment(EnemyDate enemy, string comment)
    {
        try
        {
            var fontStyle = Plugin.GetFontStyle(Plugin.spectatorFontStyle.Value);
            var style = new DialogueStyle
            {
                Color = Plugin.ParseColor(Plugin.spectatorColor.Value),
                FontSize = Plugin.dialogueFontSize.Value,
                IsBold = (fontStyle & FontStyle.Bold) != 0,
                IsItalic = (fontStyle & FontStyle.Italic) != 0,
                UseOutline = true,
                OutlineColor = Plugin.ParseColor(Plugin.spectatorOutlineColor.Value),
                OutlineDistance = new Vector2(1f, -1f)
            };
            
            // Определяем кость for enemy
            string boneName = GetBoneNameForEnemy(enemy);
            
            var bonePos = new BonePosition
            {
                BoneName = boneName,
                UseScreenCenter = false,
            };
            
            // Use существующую system отображения
            var display = DialogueFramework.GetDialogueDisplay();
            if (display != null)
            {
                display.ShowThreatOnomatopoeia(enemy, comment, bonePos, style, _displayDuration);
            }
        }
        catch (Exception ex)
        {
        }
    }
    
    /// <summary>
    /// Get name кости for enemy
    /// </summary>
    private static string GetBoneNameForEnemy(EnemyDate enemy)
    {
        if (enemy == null) return "bone13";
        
        string enemyTypeName = enemy.GetType().Name;
        
        if (enemyTypeName == "TouzokuNormal" || enemyTypeName == "TouzokuAxe")
        {
            return "bone33";
        }
        
        if (enemyTypeName == "SinnerslaveCrossbow" || 
            enemyTypeName.Contains("Dorei") || 
            enemyTypeName.Contains("Sinnerslave"))
        {
            return "face";
        }
        
        return "bone13";
    }
    
    /// <summary>
    /// Очистка expired comments
    /// </summary>
    private static void CleanupExpiredComments()
    {
        float currentTime = Time.time;
        _activeComments.RemoveAll(enemy =>
        {
            if (!_lastCommentTime.ContainsKey(enemy))
            {
                return true;
            }
            return (currentTime - _lastCommentTime[enemy]) > _displayDuration;
        });
    }
    
    /// <summary>
    /// Очистка системы
    /// </summary>
    internal static void Clear()
    {
        _lastCommentTime.Clear();
        _activeComments.Clear();
    }
    
    /// <summary>
    /// Reset system for reload with new language
    /// </summary>
    internal static void Reset()
    {
        _initialized = false;
        _spectatorComments?.Clear();
        Clear();
    }
}

/// <summary>
/// Компонент for запуска корутин
/// </summary>
internal class SpectatorCommentsCoroutineRunner : MonoBehaviour
{
}

