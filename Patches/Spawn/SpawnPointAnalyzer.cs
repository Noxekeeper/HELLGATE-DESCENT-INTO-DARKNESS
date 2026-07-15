using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Linq;
using System.Reflection;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.Spawn;

namespace NoREroMod;

/// <summary>
/// Spawn point analysis patch and F11 coordinate recorder for HellGate spawn authoring.
/// </summary>
class SpawnPointAnalyzer {

    private static int callCount = 0;
    private static bool hasLoggedFirstCall = false;
    private static readonly System.Collections.Generic.Dictionary<int, SpawnInfo> loggedSpawns = new();

    private static bool recordingMode = false;

    internal static bool IsRecordingModeActive => recordingMode;
    private static readonly string spawnPointLogFile = "BepInEx" + System.IO.Path.DirectorySeparatorChar + "spawnpoint.log";
    private static System.Collections.Generic.List<string> recordedPoints = new();

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
    /// F11 spawn coordinate recorder (invoked from PlayerConUpdateDispatcher).
    /// F11 — toggle recording (green screen overlay when on).
    /// LMB while recording — append mouse cursor world position to spawnpoint.log and clipboard.
    /// RMB while recording — altar-style hot reload (clear enemies, fun_SpawnRE, re-read HellGate JSON).
    /// Ctrl+Z — undo last recorded line.
    /// F12 — show recording statistics.
    /// </summary>
    internal static void Process()
    {
        if (Input.GetKeyDown(KeyCode.F11))
        {
            recordingMode = !recordingMode;
            string status = recordingMode ? "ON" : "OFF";
            Plugin.Log.LogInfo($"[SPAWN RECORDER] Mode: {status} ({recordedPoints.Count} points recorded)");

            ShowRecordingNotification($"Spawn Recording: {status}\nPoints: {recordedPoints.Count}");

            UpdateModeIndicator();
        }

        if (recordingMode && Input.GetMouseButtonDown(0))
        {
            RecordCurrentPosition();
        }

        if (recordingMode && Input.GetMouseButtonDown(1))
        {
            NoREroMod.Systems.Spawn.SpawnRespawnAfterAltarPatch.TriggerSpawnEditHotReload();
            ShowRecordingNotification("Spawn hot-reload (altar-style)\nEnemies cleared, respawned, HellGate re-read from disk");
        }

        if (recordingMode && Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Z))
        {
            UndoLastRecording();
        }

        if (Input.GetKeyDown(KeyCode.F12))
        {
            ShowRecordingStatistics();
        }
    }

    /// <summary>Appends the mouse cursor world position to spawnpoint.log and copies coords to the clipboard.</summary>
    private static void RecordCurrentPosition()
    {
        try
        {
            if (!TryGetMouseWorldPosition(out Vector3 position))
            {
                Plugin.Log.LogWarning("[SPAWN RECORDER] Could not resolve mouse world position (camera missing?)");
                return;
            }

            string coords = $"{position.x:F2},{position.y:F2}";
            string location = GetCurrentLocation();
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            int pointNumber = GetPointNumberForLocation(location);

            string logEntry = $"# Point {pointNumber} | {timestamp} | {location} | {coords}";

            File.AppendAllText(spawnPointLogFile, logEntry + "\n");

            recordedPoints.Add(logEntry);

            CopyCoordinatesToClipboard(coords);

            Plugin.Log.LogInfo($"[SPAWN RECORDER] Point #{recordedPoints.Count} recorded: ({coords}) in {location} [clipboard]");

            ShowRecordingNotification($"Point #{recordedPoints.Count} recorded\n{coords} (clipboard)\nLocation: {location}");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[SPAWN RECORDER] Error recording position: {ex.Message}");
        }
    }

    private static bool TryGetMouseWorldPosition(out Vector3 position)
    {
        position = default;

        GameObject camGo = UnifiedCameraCacheManager.GetMainCamera();
        Camera cam = camGo != null ? camGo.GetComponent<Camera>() : null;
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return false;

        float depthZ = 0f;
        var playerObj = UnifiedPlayerCacheManager.GetPlayerObject();
        if (playerObj != null)
            depthZ = playerObj.transform.position.z;

        Vector3 screen = Input.mousePosition;
        screen.z = cam.WorldToScreenPoint(new Vector3(0f, 0f, depthZ)).z;
        position = cam.ScreenToWorldPoint(screen);
        position.z = depthZ;
        return true;
    }

    private static void CopyCoordinatesToClipboard(string coords)
    {
        try
        {
            GUIUtility.systemCopyBuffer = coords;
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[SPAWN RECORDER] Clipboard copy failed: {ex.Message}");
        }
    }

    /// <summary>Removes the last line from spawnpoint.log and the in-memory list.</summary>
    private static void UndoLastRecording()
    {
        try
        {
            if (recordedPoints.Count == 0)
            {
                ShowRecordingNotification("No points to undo");
                return;
            }

            if (File.Exists(spawnPointLogFile))
            {
                var lines = File.ReadAllLines(spawnPointLogFile).ToList();
                if (lines.Count > 0)
                {
                    lines.RemoveAt(lines.Count - 1);
                    File.WriteAllLines(spawnPointLogFile, lines.ToArray());
                }
            }

            recordedPoints.RemoveAt(recordedPoints.Count - 1);

            Plugin.Log.LogInfo($"[SPAWN RECORDER] Last point undone. Total points: {recordedPoints.Count}");
            ShowRecordingNotification($"Point undone\nRemaining: {recordedPoints.Count}");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[SPAWN RECORDER] Error undoing recording: {ex.Message}");
        }
    }

    /// <summary>Logs and notifies point counts grouped by location label.</summary>
    private static void ShowRecordingStatistics()
    {
        try
        {
            if (recordedPoints.Count == 0)
            {
                ShowRecordingNotification("No points recorded yet");
                return;
            }

            var locationStats = recordedPoints
                .Select(line => {
                    var parts = line.Split('|');
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

    /// <summary>Counts existing log lines for the given location label.</summary>
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
                if (!string.IsNullOrEmpty(trimmed) && trimmed.Contains($"| {location} |"))
                {
                    count++;
                }
            }
            
            return count + 1;
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[SPAWN RECORDER] Error counting points: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Resolves a human-readable location label.
    /// Uses physical zone (<see cref="HellGateLocationSpawnRefresh.GetActiveGameplayZone"/> / Idea_Nowscene),
    /// not checkpoint <c>_re_Scenename</c> (last altar — stale after door transitions).
    /// </summary>
    private static string GetCurrentLocation()
    {
        try
        {
            string zone = HellGateLocationSpawnRefresh.GetActiveGameplayZone();
            if (!string.IsNullOrEmpty(zone))
                return MapToHumanReadableName(zone);
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[SPAWN RECORDER] Error getting active gameplay zone: {ex.Message}");
        }

        try
        {
            string levelScene = HellGateLocationSpawnRefresh.GetLoadedGameplayLevelScene();
            if (!string.IsNullOrEmpty(levelScene))
                return MapToHumanReadableName(levelScene);
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[SPAWN RECORDER] Error getting loaded level scene: {ex.Message}");
        }

        try
        {
            var fragMng = UnifiedGameControllerCacheManager.GetGameFragMng();
            if (fragMng != null && !string.IsNullOrEmpty(fragMng._re_Scenename))
                return MapToHumanReadableName(fragMng._re_Scenename);
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[SPAWN RECORDER] Error getting location from game_fragmng: {ex.Message}");
        }

        return "Unknown";
    }

    /// <summary>Maps internal scene tokens to recorder-friendly location names.</summary>
    private static string MapToHumanReadableName(string technicalName)
    {
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
                return MakeReadableName(technicalName);
        }
    }

    /// <summary>Fallback formatter for unmapped scene tokens.</summary>
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

    /// <summary>Shows or hides the full-screen green recording overlay.</summary>
    private static void UpdateModeIndicator()
    {
        try
        {
            if (recordingMode)
            {
                if (modeIndicatorCanvas == null)
                {
                    CreateModeIndicator();
                }

                if (modeIndicatorCanvas != null)
                {
                    modeIndicatorCanvas.SetActive(true);
                    modeIndicatorImage.color = new Color(0f, 1f, 0f, 0.1f);
                }
            }
            else
            {
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

    /// <summary>Creates a screen-space overlay used while recording mode is active.</summary>
    private static void CreateModeIndicator()
    {
        try
        {
            modeIndicatorCanvas = new GameObject("SpawnRecorderIndicator");
            var canvas = modeIndicatorCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            var imageObj = new GameObject("IndicatorImage");
            imageObj.transform.SetParent(modeIndicatorCanvas.transform);

            modeIndicatorImage = imageObj.AddComponent<Image>();
            modeIndicatorImage.color = new Color(0f, 1f, 0f, 0.1f);

            var rectTransform = imageObj.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

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


    /// <summary>Writes recorder messages to the BepInEx log.</summary>
    private static void ShowRecordingNotification(string message)
    {
        try
        {
            Plugin.Log.LogInfo($"[SPAWN RECORDER] {message}");
        }
        catch (System.Exception ex)
        {
            Debug.Log($"[SPAWN RECORDER] {message} ({ex.Message})");
        }
    }
}

/// <summary>
/// Blocks player attack input while F11 spawn recording mode is active so LMB only records points.
/// </summary>
[HarmonyPatch(typeof(playercon), "Getinput")]
internal static class SpawnRecorderAttackInputBlockPatch
{
    private static readonly FieldInfo KeyAtkField = AccessTools.Field(typeof(playercon), "key_atk");
    private static readonly FieldInfo KeyAtkPressField = AccessTools.Field(typeof(playercon), "key_atk_press");
    private static readonly FieldInfo KeyAtkUpField = AccessTools.Field(typeof(playercon), "key_atk_up");

    [HarmonyPostfix]
    private static void Postfix(playercon __instance)
    {
        if (__instance == null || !SpawnPointAnalyzer.IsRecordingModeActive)
            return;

        KeyAtkField?.SetValue(__instance, false);
        KeyAtkPressField?.SetValue(__instance, false);
        KeyAtkUpField?.SetValue(__instance, false);
    }
}
