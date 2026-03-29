using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Linq;

namespace NoREroMod;

/// <summary>
/// Patch for анализа точек spawn enemies и activeй записи координат
/// </summary>
class SpawnPointAnalyzer {

    private static int callCount = 0;
    private static bool hasLoggedFirstCall = false;
    private static readonly System.Collections.Generic.Dictionary<int, SpawnInfo> loggedSpawns = new();

    // Активный режим записи координат
    private static bool recordingMode = false;
    private static readonly string spawnPointLogFile = "BepInEx" + System.IO.Path.DirectorySeparatorChar + "spawnpoint.log";
    private static System.Collections.Generic.List<string> recordedPoints = new();

    // Visual indication режима
    private static GameObject modeIndicatorCanvas = null;
    private static Image modeIndicatorImage = null;
    
    [HarmonyPatch(typeof(Spawnenemy), "Update")]
    [HarmonyPrefix]
    static bool AnalyzeSpawnPoint(Spawnenemy __instance, 
                                   GameObject ___enemy, 
                                   int ___SpawnNumber,
                                   SpawnParent ___Spawnparent) {
        callCount++;

        string enemyName = ___enemy != null ? ___enemy.name : "NULL";
        Vector3 position = __instance.transform.position;
        bool isSpawned = ___Spawnparent != null && ___Spawnparent._SpawnPoint[___SpawnNumber];

        if (isSpawned)
        {
            if (!loggedSpawns.TryGetValue(___SpawnNumber, out SpawnInfo info))
            {
                info = new SpawnInfo
                {
                    FirstEnemyName = enemyName,
                    Position = position,
                    ParentName = ___Spawnparent != null ? ___Spawnparent.name : "NULL"
                };
                loggedSpawns[___SpawnNumber] = info;

                if (enemyName.IndexOf("goblin", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    info.GoblinOrder = ++globalGoblinOrder;
                }

                LogSpawnInfo(___SpawnNumber, info);
            }
            else if (string.Equals(enemyName, "goblin", System.StringComparison.OrdinalIgnoreCase) && info.GoblinOrder == 0)
            {
                info.GoblinOrder = ++globalGoblinOrder;
                LogSpawnInfo(___SpawnNumber, info);
            }
        }

        return true;
    }

    internal static void Reset()
    {
        callCount = 0;
        hasLoggedFirstCall = false;
        loggedSpawns.Clear();
        globalGoblinOrder = 0;
        recordedPoints.Clear();

        // Clear визуальный индикатор
        if (modeIndicatorCanvas != null)
        {
            UnityEngine.Object.Destroy(modeIndicatorCanvas);
            modeIndicatorCanvas = null;
            modeIndicatorImage = null;
        }

        recordingMode = false;
    }

    private static int globalGoblinOrder = 0;

    private static void LogSpawnInfo(int spawnNumber, SpawnInfo info)
    {
        string orderPart = info.GoblinOrder > 0 ? $", GoblinOrder={info.GoblinOrder}" : string.Empty;
        // Plugin.Log.LogInfo($"[SPAWN MAP] #{spawnNumber + 1} enemy={info.FirstEnemyName}{orderPart} pos=({info.Position.x:F2}, {info.Position.y:F2}, {info.Position.z:F2}) parent={info.ParentName}"); // Disabled for release
    }

    private class SpawnInfo
    {
        public string FirstEnemyName;
        public Vector3 Position;
        public string ParentName;
        public int GoblinOrder;
    }

    /// <summary>
    /// Активный режим записи координат for spawn enemies
    /// F11 - включение/выключение режима записи (with визуальной индикацией)
    /// ЛКМ in режиме записи - запись текущей позиции (with эффектом тряски)
    /// Ctrl+Z - отмеon последней записи
    /// F12 - показать статистику записанных точек
    /// </summary>
    /// <summary>Вызывается from PlayerConUpdateDispatcher</summary>
    internal static void Process()
    {
        // F11 - переключение режима записи
        if (Input.GetKeyDown(KeyCode.F11))
        {
            recordingMode = !recordingMode;
            string status = recordingMode ? "ON" : "OFF";
            Plugin.Log.LogInfo($"[SPAWN RECORDER] Mode: {status} ({recordedPoints.Count} points recorded)");

            // Показать notification игроку
            ShowRecordingNotification($"Spawn Recording: {status}\nPoints: {recordedPoints.Count}");

            // Visual indication режима
            UpdateModeIndicator();
        }

        // ЛКМ in режиме записи - записать координату
        if (recordingMode && Input.GetMouseButtonDown(0))
        {
            RecordCurrentPosition();
        }

        // Ctrl+Z - отмеon последней записи
        if (recordingMode && Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Z))
        {
            UndoLastRecording();
        }

        // F12 - показать статистику
        if (Input.GetKeyDown(KeyCode.F12))
        {
            ShowRecordingStatistics();
        }
    }

    /// <summary>
    /// Записывает текущую позицию игрока in лог-файл
    /// </summary>
    private static void RecordCurrentPosition()
    {
        try
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Plugin.Log.LogWarning("[SPAWN RECORDER] Player not found!");
                return;
            }

            var position = player.transform.position;
            string location = GetCurrentLocation();
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            // Get point number for current location
            int pointNumber = GetPointNumberForLocation(location);

            string logEntry = $"# Point {pointNumber} | {timestamp} | {location} | {position.x:F2},{position.y:F2}";

            // Записать in файл
            File.AppendAllText(spawnPointLogFile, logEntry + "\n");

            // Добавить in список for отмены
            recordedPoints.Add(logEntry);

            // Показать in консоли
            Plugin.Log.LogInfo($"[SPAWN RECORDER] Point #{recordedPoints.Count} recorded: ({position.x:F2}, {position.y:F2}) in {location}");

            // Эффект тряски экраon on записи - удален by просьбе пользователя

            // Показать notification игроку
            ShowRecordingNotification($"Point #{recordedPoints.Count} recorded\nLocation: {location}");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[SPAWN RECORDER] Error recording position: {ex.Message}");
        }
    }

    /// <summary>
    /// Отменяет последнюю запись координаты
    /// </summary>
    private static void UndoLastRecording()
    {
        try
        {
            if (recordedPoints.Count == 0)
            {
                ShowRecordingNotification("No points to undo");
                return;
            }

            // Удаляем последнюю запись from файла
            if (File.Exists(spawnPointLogFile))
            {
                var lines = File.ReadAllLines(spawnPointLogFile).ToList();
                if (lines.Count > 0)
                {
                    lines.RemoveAt(lines.Count - 1); // Удаляем последнюю строку
                    File.WriteAllLines(spawnPointLogFile, lines.ToArray());
                }
            }

            // Remove from списка
            recordedPoints.RemoveAt(recordedPoints.Count - 1);

            Plugin.Log.LogInfo($"[SPAWN RECORDER] Last point undone. Total points: {recordedPoints.Count}");
            ShowRecordingNotification($"Point undone\nRemaining: {recordedPoints.Count}");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[SPAWN RECORDER] Error undoing recording: {ex.Message}");
        }
    }

    /// <summary>
    /// Показывает статистику записанных точек
    /// </summary>
    private static void ShowRecordingStatistics()
    {
        try
        {
            if (recordedPoints.Count == 0)
            {
                ShowRecordingNotification("No points recorded yet");
                return;
            }

            // Группируем точки by локациям
            var locationStats = recordedPoints
                .Select(line => {
                    var parts = line.Split('|');
                    // New format: # Point N | timestamp | location | coordinates
                    return parts.Length >= 3 ? parts[2].Trim() : "Unknown";
                })
                .GroupBy(location => location)
                .Select(group => $"{group.Key}: {group.Count()}")
                .ToList();

            string statsMessage = $"Recording Statistics:\nTotal Points: {recordedPoints.Count}\n\nBy Location:\n{string.Join("\n", locationStats.ToArray())}";

            Plugin.Log.LogInfo($"[SPAWN RECORDER] {statsMessage.Replace("\n", " | ")}");
            ShowRecordingNotification(statsMessage);
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[SPAWN RECORDER] Error showing statistics: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets point number for current location by counting existing points in log file
    /// </summary>
    private static int GetPointNumberForLocation(string location)
    {
        try
        {
            if (!File.Exists(spawnPointLogFile))
            {
                return 1;
            }

            string[] lines = File.ReadAllLines(spawnPointLogFile);
            int count = 0;
            
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                // Count lines that contain this location (new format: # Point N | timestamp | location | coordinates)
                if (!string.IsNullOrEmpty(trimmed) && trimmed.Contains($"| {location} |"))
                {
                    count++;
                }
            }
            
            return count + 1; // Next point number
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[SPAWN RECORDER] Error counting points: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Determines текущую локацию через FlagMng (использует игровые названия сцен)
    /// </summary>
    private static string GetCurrentLocation()
    {
        try
        {
            // Сначала пытаемся получить через game_fragmng (игровая логика)
            var fragMng = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetGameFragMng();
            if (fragMng != null && !string.IsNullOrEmpty(fragMng._re_Scenename))
            {
                return MapToHumanReadableName(fragMng._re_Scenename);
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[SPAWN RECORDER] Error getting location from game_fragmng: {ex.Message}");
        }

        // Fallback on Unity SceneManager
        try
        {
            string unitySceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            return MapToHumanReadableName(unitySceneName);
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <summary>
    /// Преобразует техническое название сцены in человеко-читаемое
    /// </summary>
    private static string MapToHumanReadableName(string technicalName)
    {
        // Маппинг основных локаций from игры
        switch (technicalName)
        {
            case "Parishchurch": return "parish church";
            case "village_main": return "abadoned vilage area";
            case "scapegoatEntrance": return "scapegoat entrance";
            case "UndergroundChurch": return "underground church";
            case "InundergroundChurch": return "inunderground church";
            case "InsomniaTown": return "nightless city (ragdum) b";
            case "Shop": return "shop";
            case "InsomniaTownC": return "nightless city C";
            case "InsomniaTownUnderRoad": return "nightless city under road";
            case "InsomniaTownUnder": return "nightless city under";
            case "Valley": return "valley";
            case "ForestOfRequiem": return "hidden Forest area";
            case "BridgeBlockArea": return "bridge block area";
            case "WitchHideout": return "witch's hideout";
            case "Ranch": return "Ranch";
            case "RisingPassage": return "Rising passage";
            case "SynkingCanyon": return "synking canyon area";
            case "WhiteCathedral": return "white cathedral";
            case "WhiteCathedralGarden": return "white cathedral garden";
            case "WhiteCathedralRooftop": return "white cathedral rooftop";
            default:
                // Fallback: делаем название читаемым
                return MakeReadableName(technicalName);
        }
    }

    /// <summary>
    /// Преобразует техническое название in читаемый вид
    /// </summary>
    private static string MakeReadableName(string technicalName)
    {
        if (string.IsNullOrEmpty(technicalName))
            return "unknown area";

        return technicalName
            .Replace("_", " ")
            .Replace("main", "area")
            .Replace("level", "area")
            .Replace("scene", "")
            .Trim();
    }

    /// <summary>
    /// Создает or обновляет visual indication режима записи
    /// </summary>
    private static void UpdateModeIndicator()
    {
        try
        {
            if (recordingMode)
            {
                // Создаем индикатор if its нет
                if (modeIndicatorCanvas == null)
                {
                    CreateModeIndicator();
                }

                // Show индикатор
                if (modeIndicatorCanvas != null)
                {
                    modeIndicatorCanvas.SetActive(true);
                    // Зеленый цвет for режима записи
                    modeIndicatorImage.color = new Color(0f, 1f, 0f, 0.1f);
                }
            }
            else
            {
                // Скрываем индикатор in режиме просмотра
                if (modeIndicatorCanvas != null)
                {
                    modeIndicatorCanvas.SetActive(false);
                }
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[SPAWN RECORDER] Error updating mode indicator: {ex.Message}");
        }
    }

    /// <summary>
    /// Создает UI индикатор режима записи (red бордер by краям экрана)
    /// </summary>
    private static void CreateModeIndicator()
    {
        try
        {
            // Create Canvas
            modeIndicatorCanvas = new GameObject("SpawnRecorderIndicator");
            var canvas = modeIndicatorCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // Поверх всего

            // Создаем полноэкранный Image
            var imageObj = new GameObject("IndicatorImage");
            imageObj.transform.SetParent(modeIndicatorCanvas.transform);

            modeIndicatorImage = imageObj.AddComponent<Image>();
            modeIndicatorImage.color = new Color(0f, 1f, 0f, 0.1f); // Почти прозрачный зеленый

            // Stretch to full screen
            var rectTransform = imageObj.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            // Добавляем бордер эффект
            var outline = imageObj.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Color.green;
            outline.effectDistance = new Vector2(3, 3);

            UnityEngine.Object.DontDestroyOnLoad(modeIndicatorCanvas);
            modeIndicatorCanvas.SetActive(false);
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[SPAWN RECORDER] Error creating mode indicator: {ex.Message}");
        }
    }


    /// <summary>
    /// Показывает notification игроку
    /// </summary>
    private static void ShowRecordingNotification(string message)
    {
        try
        {
            // Show in лог with префиксом
            Plugin.Log.LogInfo($"[SPAWN RECORDER] {message}");

            // In будущем can добавить UI notification
        }
        catch (System.Exception ex)
        {
            // Fallback on простой лог
            Debug.Log($"[SPAWN RECORDER] {message}");
        }
    }
}