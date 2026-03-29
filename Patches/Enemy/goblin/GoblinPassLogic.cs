using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Spine.Unity;
using NoREroMod.Patches.Enemy.Base;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Optimized: Uses UnifiedPlayerCacheManager instead of FindGameObjectWithTag
/// </summary>
class GoblinPassLogic : BaseEnemyPassPatch<goblinero>
{
    protected override string EnemyName => "Goblin";
    
    
    protected override int CyclesBeforePass => 2;
    
    protected override string[] GetHAnimations()
    {
        return new[]
        {
            "START",
            "ERO1", "ERO2", "ERO3", "ERO4", "ERO5",
            "ERO_iki", "ERO_iki2", "ERO_jigo",
            "2ERO_START", "2ERO_START2",
            "2ERO_1", "2ERO_2", "2ERO_3", "2ERO_4",
            "2ERO_iki", "2ERO_iki2",
            "2ERO_JIGO", "2ERO_JIGO2"
        };
    }
    
    protected override bool IsCycleComplete(string animationName, string eventName, int seCount)
    {
        string animUpper = animationName?.ToUpperInvariant() ?? string.Empty;

        // Передача only after второй фазы (2ERO_JIGO)
        if (animUpper == "2ERO_JIGO")
        {
            return true;
        }

        if (animUpper == "2ERO_JIGO2")
        {
            return true;
        }

        return false;
    }
    
    protected override string GetEnemyTypeName()
    {
        return "goblin";
    }
    
    [HarmonyPatch(typeof(goblinero), "OnEvent")]
    [HarmonyPostfix]
    private static void GoblinPass(goblinero __instance, Spine.Event e, int ___se_count)
    {
        var instance = new GoblinPassLogic();
        SetInstance(instance);

        try
        {
            var disabledField = typeof(BaseEnemyPassPatch<goblinero>)
                .GetField("enemyDisabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as System.Collections.Generic.Dictionary<object, bool>;
                if (disabledDict != null && disabledDict.ContainsKey(__instance) && disabledDict[__instance])
                {
                    return;
                }
            }
            
                // Optimization: use cached playercon
                var player = UnifiedPlayerCacheManager.GetPlayer();
                if (player == null || !player.eroflag || player.erodown == 0)
                {
                    return;
                }
            
            var spine = GetSpineAnimation(__instance);
            if (spine == null)
            {
                // Plugin.Log.LogInfo( "[GOBLIN PASS] spine is null");
                return;
            }
            
            string currentAnim = spine.AnimationName;
            
            // Check that this H-анимация
            if (!instance.IsHAnimation(currentAnim))
            {
                return; // Ignore combat animations
            }
            
            // Goblin использует Base.TrackCycles, which проверяет EnemyHandoffSystem.GlobalHandoffCount
            string eventName = e?.Data?.Name ?? e?.ToString() ?? string.Empty;
            
            // Skip dialogue processing for ERO_iki and ERO_iki2 events to prevent freezing
            // These animations require count >= 3 to transition, and dialogue processing may interfere
            // BUT allow dialogue processing for 2ERO_iki since user wants climax dialogues
            string eventUpper = eventName.ToUpperInvariant();
            string animUpper = currentAnim.ToUpperInvariant();
            bool isIkiEvent = eventUpper == "ERO_IKI" || eventUpper == "ERO_IKI2" ||
                             eventUpper == "2ERO_IKI2"; // Exclude 2ERO_IKI from blocking
            bool isIkiAnim = animUpper == "ERO_IKI" || animUpper == "ERO_IKI2" ||
                            animUpper == "2ERO_IKI2"; // Exclude 2ERO_IKI from blocking

            // Only process dialogue for non-iki events/animations to prevent interference with count logic
            // But allow 2ERO_IKI for climax dialogues
            if (!isIkiEvent && !isIkiAnim || animUpper == "2ERO_IKI")
            {
                try {
                    NoREroMod.Systems.Dialogue.DialogueFramework.ProcessAnimationEvent(
                        __instance,
                        currentAnim,
                        eventName,
                        ___se_count
                    );
                } catch (Exception ex) {
                }
            }

            // Processing ономатопей on основе sounds from анимационных событий
            try {
                ProcessOnomatopoeiaForEvent(__instance, currentAnim, eventName, ___se_count);
            } catch (Exception ex) {
                Plugin.Log?.LogError($"[GoblinPassLogic] Error processing onomatopoeia: {ex.Message}");
            }
            
            string logMessage = $"[GOBLIN PASS] H-anim anim={currentAnim}, event={eventName}, se={___se_count}";
            // Logs disabled
            // Plugin.Log.LogInfo(logMessage);
            // Plugin.Log.LogInfo( logMessage);
            instance.TrackCycles(__instance, spine, e, ___se_count);
        }
        catch (System.Exception ex)
        {
            // Plugin.Log.LogInfo( $"[GOBLIN PASS] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Processing ономатопей for анимационных событий goblin
    /// </summary>
    private static void ProcessOnomatopoeiaForEvent(goblinero goblinInstance, string animationName, string eventName, int seCount)
    {
        string soundKey = GetSoundKeyForEvent(animationName, eventName, seCount);
        if (string.IsNullOrEmpty(soundKey))
        {
            return;
        }

        // Check if there are ономатопея for этого sounds
        bool hasSound = NoREroMod.Systems.Dialogue.SoundRegistry.HasSound(soundKey);
        Plugin.Log?.LogInfo($"[GoblinPassLogic] Checking sound '{soundKey}': HasSound={hasSound}");

        if (!hasSound)
        {
            return;
        }

        string onomatopoeia = NoREroMod.Systems.Dialogue.SoundRegistry.GetRandomOnomatopoeia(soundKey);
        Plugin.Log?.LogInfo($"[GoblinPassLogic] Got onomatopoeia for '{soundKey}': '{onomatopoeia}' (empty: {string.IsNullOrEmpty(onomatopoeia)})");

        if (string.IsNullOrEmpty(onomatopoeia))
        {
            return;
        }

        Plugin.Log?.LogInfo($"[GoblinPassLogic] Calling ShowSoundOnomatopoeia for '{onomatopoeia}' on {goblinInstance?.GetType().Name}");

        try {
            // Show ономатопею (передаем сам component, а not GameObject)
            NoREroMod.Systems.Dialogue.DialogueFramework.ShowSoundOnomatopoeia(goblinInstance, soundKey, onomatopoeia);
            Plugin.Log?.LogInfo($"[GoblinPassLogic] ShowSoundOnomatopoeia completed successfully");
        } catch (System.Exception ex) {
            Plugin.Log?.LogError($"[GoblinPassLogic] Error calling ShowSoundOnomatopoeia: {ex.Message}");
        }
    }

    /// <summary>
    /// Determines ключ sounds for анимационного events
    /// </summary>
    private static string GetSoundKeyForEvent(string animationName, string eventName, int seCount)
    {
        // Use данные from GOBLIN_KAKASI_SOUNDS.md
        switch (animationName)
        {
            case "START":
                if (eventName == "SE" && seCount == 1) return "dame_kuhuu";
                break;

            case "ERO1":
                if (eventName == "SE" && seCount == 2) return "dame_kuhuu";
                break;

            case "ERO2":
                if (eventName == "SE" && seCount == 2) return "ero_nameru";
                break;

            case "ERO3":
                if (eventName == "SE" && seCount == 2) return "ero_nameru";
                break;

            case "ERO4":
                if (eventName == "SE" && seCount == 2) return "ero_nameru";
                if (eventName == "SE" && seCount == 4) return "ero_nameru";
                break;

            case "ERO5":
                if (eventName == "SE" && seCount == 3) return "ero_nameru";
                if (eventName == "SE" && seCount == 5) return "ero_nameru";
                break;

            case "ERO_iki":
                if (eventName == "SE" && (seCount >= 1 && seCount <= 7)) return "ero_nameru";
                break;

            case "ERO_iki2":
                if (eventName == "SE" && (seCount >= 1 && seCount <= 7)) return "ero_nameru";
                break;

            case "2ERO_START":
                if (eventName == "SE" && seCount == 1) return "dame_kuhuu";
                if (eventName == "SE" && seCount == 6) return "ero_piston1";
                break;

            case "2ERO_START2":
                if (eventName == "SE" && seCount == 1) return "dame_kuhuu";
                if (eventName == "SE" && seCount == 6) return "ero_piston1";
                break;

            case "2ERO_1":
            case "2ERO_2":
            case "2ERO_3":
                if (eventName == "SE" && seCount == 1) return "ero_piston5";
                break;

            case "2ERO_4":
                if (eventName == "SE" && seCount == 1) return "ero_piston1";
                break;

            case "2ERO_iki":
                if (eventName == "SE" && seCount == 1) return "ero_piston1";
                if (eventName == "SE" && seCount == 2) return "ero_enemy_syasei1";
                break;

            case "2ERO_iki2":
                if (eventName == "SE" && seCount == 1) return "ero_piston1";
                if (eventName == "SE" && seCount == 2) return "ero_enemy_syasei1";
                break;

            case "2ERO_JIGO":
                // Событие окончания - добавим подходящий sound
                if (eventName == "SE" && seCount == 1) return "ero_enemy_syasei1";
                break;
        }

        return null; // Нет соответствующits sounds
    }
    
    // Инициализация статического instanceа
    static GoblinPassLogic()
    {
        var instance = new GoblinPassLogic();
        SetInstance(instance);
    }
    
    /// <summary>
    /// Public method for invoking handoff (used by DelayedHandoffScript)
    /// </summary>
    public static void ExecuteHandoff(object enemyInstance)
    {
        PushPlayerAwayFromEnemy(enemyInstance);
    }
    
    /// <summary>
    /// Оттолкнуть ГГ from enemy (analogичbut TouzokuNormalPassPatch)
    /// Гоблины НЕ наследуются from EnemyDate, so we use прямой подход
    /// </summary>
    private static void PushPlayerAwayFromEnemy(object enemyInstance)
    {

        // Plugin.Log.LogInfo("[GOBLIN] === Pushing GG away ===");
        
        try
        {
            // Mark enemy as disabled
            var disabledField = typeof(BaseEnemyPassPatch<goblinero>)
                .GetField("enemyDisabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as System.Collections.Generic.Dictionary<object, bool>;
                if (disabledDict != null)
                {
                    disabledDict[enemyInstance] = true;
                    // Logs disabled
                    // Plugin.Log.LogInfo("[GOBLIN] Enemy marked as disabled");
                    // Plugin.Log.LogInfo( "[GOBLIN] Enemy marked as disabled");
                }
            }
            
            // Находим ГГ
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null)
            {
                // Plugin.Log.LogInfo( "[GOBLIN] Player not found!");
                return;
            }
            
            // Get component enemy
            var goblin = enemyInstance as goblinero;
            if (goblin == null)
            {
                // Plugin.Log.LogInfo( "[GOBLIN] Enemy instance is not goblinero!");
                return;
            }
            
            // Stop H-animation enemy (analogичbut Touzoku)
            try
            {
                // Get spine enemy via reflection (in goblinero поле называется myspine)
                var enemySpineField = typeof(goblinero).GetField("myspine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (enemySpineField != null)
                {
                    var enemySpine = enemySpineField.GetValue(goblin) as SkeletonAnimation;
                    if (enemySpine != null)
                    {
                        // Plugin.Log.LogInfo("[GOBLIN] Stopping enemy H-animation...");
                        enemySpine.AnimationState.ClearTracks();
                        
                        // Try установить idle animation
                        string[] idleAnimations = { "idle", "Idle", "IDLE", "wait", "Wait", "WAIT" };
                        bool animationSet = false;
                        foreach (string animName in idleAnimations)
                        {
                            try
                            {
                                enemySpine.AnimationState.SetAnimation(0, animName, true);
                                // Plugin.Log.LogInfo($"[GOBLIN] Set enemy animation to '{animName}'");
                                animationSet = true;
                                break;
                            }
                            catch
                            {
                                // Try next animation
                            }
                        }
                        
                        if (!animationSet)
                        {
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
            }
            
            // Скрываем enemy (analogичbut Touzoku)
            try
            {
                goblin.gameObject.SetActive(false);
                // Logs disabled
                // Plugin.Log.LogInfo("[GOBLIN] Enemy GameObject hidden");
                // Plugin.Log.LogInfo( "[GOBLIN] Enemy GameObject hidden");
            }
            catch (System.Exception ex)
            {
            }
            
            // ОЧИЩАЕМ АНИМАЦИЮ ГГ (важно! without этого ГГ останется in H-сцене)
            var playerSpine = playerObject.GetComponentInChildren<SkeletonAnimation>();
            if (playerSpine != null)
            {
                try
                {
                    playerSpine.AnimationState.ClearTracks();
                    // Logs disabled
                    // Plugin.Log.LogInfo("[GOBLIN] Player spine cleared");
                    // Plugin.Log.LogInfo( "[GOBLIN] Player spine cleared");
                }
                catch (System.Exception ex)
                {
                }
            }
            
            // Get playercon и устанавливаем eroflag = false (выход from H-scene)
            var playerComponent = playerObject.GetComponent<playercon>();
            if (playerComponent != null)
            {
                var eroFlagField = typeof(playercon).GetField("eroflag", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (eroFlagField != null)
                {
                    try
                    {
                        eroFlagField.SetValue(playerComponent, false);
                        // Logs disabled
                        // Plugin.Log.LogInfo("[GOBLIN] eroflag set to false (exit H-scene)");
                        // Plugin.Log.LogInfo( "[GOBLIN] eroflag set to false");
                    }
                    catch (System.Exception ex)
                    {
                    }
                }
                
                // Set GG animation to lying
                string[] downAnims = { "DOWN", "down", "Idle", "idle" };
                foreach (string animName in downAnims)
                {
                    if (playerSpine != null)
                    {
                        try
                        {
                            playerSpine.AnimationState.SetAnimation(0, animName, true);
                            // Plugin.Log.LogInfo($"[GOBLIN] GG animation set to '{animName}'");
                            break;
                        }
                        catch (System.Exception ex)
                        {
                        }
                    }
                }
                
                // Set erodown via reflection
                var eroDownField = typeof(playercon).GetField("erodown", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (eroDownField != null)
                {
                    try
                    {
                        eroDownField.SetValue(playerComponent, 1);
                        // Plugin.Log.LogInfo("[GOBLIN] erodown set to 1 (prone)");
                    }
                    catch (System.Exception ex)
                    {
                    }
                }
            }
            else
            {
                // Plugin.Log.LogInfo( "[GOBLIN] playercon not found!");
            }
            
            // Reset SP via PlayerStatus
            var playerStatus = playerObject.GetComponent<PlayerStatus>();
            if (playerStatus != null)
            {
                try
                {
                    playerStatus.Sp = 0f;
                    // Plugin.Log.LogInfo("[GOBLIN] SP reset to 0");
                }
                catch (System.Exception ex)
                {
                }
            }
            
            // Logs disabled
            // Plugin.Log.LogInfo("[GOBLIN] Player should be free now");
            // Plugin.Log.LogInfo( "[GOBLIN] Player should be free now");
        }
        catch (System.Exception ex)
        {
            // Plugin.Log.LogInfo( $"[GOBLIN] Error in PushPlayerAwayFromEnemy: {ex.Message}");
        }
    }
    
    /// <summary>
    
    /// <summary>
    /// Статические поля for UI phrases передачи
    /// </summary>
    private static GameObject handoffUI;
    private static UnityEngine.UI.Text handoffText;
    private static RectTransform handoffRect;
    private static Transform currentHandoffEnemyTransform;
    
    /// <summary>
    /// Public methods for access to приватным полям from внешнtheir классов
    /// </summary>
    public static GameObject GetHandoffUI() => handoffUI;
    public static UnityEngine.UI.Text GetHandoffText() => handoffText;
    public static RectTransform GetHandoffRect() => handoffRect;
    public static Transform GetCurrentHandoffEnemyTransform() => currentHandoffEnemyTransform;
    
    /// <summary>
    /// Display сообщение о передаче ГГ for goblins (над головой enemy)
    /// Invoked from BaseEnemyPassPatch via reflection
    /// </summary>
    protected static void DisplayHandoffMessage(string message, float displayTime, object enemyInstance)
    {
        try
        {
            // Сохраняем enemy Transform
            var mb = enemyInstance as MonoBehaviour;
            if (mb != null)
            {
                currentHandoffEnemyTransform = mb.transform;
            }
            
            // Create Canvas if its нет
            if (handoffUI == null)
            {
                GameObject canvasObj = new GameObject("GoblinHandoffMessageCanvas");
                handoffUI = canvasObj;
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 999; // As граб-phrasesа
                
                UnityEngine.UI.CanvasScaler scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                
                // Panel
                GameObject panelObj = new GameObject("HandoffPanel");
                panelObj.transform.SetParent(canvasObj.transform, false);
                RectTransform panelRect = panelObj.AddComponent<RectTransform>();
                handoffRect = panelRect;
                
                panelRect.sizeDelta = new Vector2(600, 80);
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0f);
                
                // Text
                GameObject textObj = new GameObject("HandoffText");
                textObj.transform.SetParent(panelObj.transform, false);
                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(10, 10);
                textRect.offsetMax = new Vector2(-10, -10);
                
                handoffText = textObj.AddComponent<UnityEngine.UI.Text>();
                handoffText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                handoffText.fontSize = 44;
                handoffText.alignment = TextAnchor.MiddleCenter;
                handoffText.color = new Color(1f, 0.2f, 0.2f); // Красный
                handoffText.fontStyle = FontStyle.Bold;
                
                // Компонент for position tracking
                handoffUI.AddComponent<GoblinHandoffPositionTracker>();
            }
            
            // Update text и активируем UI
            if (handoffText != null)
            {
                handoffText.text = message;
                handoffText.enabled = true;
            }
            
            // Активируем Canvas и Panel
            if (handoffUI != null)
            {
                handoffUI.SetActive(true);
            }
            if (handoffRect != null && handoffRect.gameObject != null)
            {
                handoffRect.gameObject.SetActive(true);
            }
            
            // Скрытие через delay
            var hideScript = handoffUI.GetComponent<GoblinHandoffMessageHideScript>();
            if (hideScript == null)
            {
                hideScript = handoffUI.AddComponent<GoblinHandoffMessageHideScript>();
            }
            hideScript.StartHide(displayTime);
            
            // Logs disabled
            // Plugin.Log.LogInfo($"[GOBLIN HANDOFF] Displaying message: {message}");
            // Plugin.Log.LogInfo( $"[GOBLIN HANDOFF] Displaying: {message}");
        }
        catch (System.Exception ex)
        {
        }
    }

    /// <summary>
    /// For goblins on передаче используем 2ERO_START instead of ERO2
    /// </summary>
    protected override void ForceAnimationToMiddle(Spine.Unity.SkeletonAnimation spine)
    {
        try
        {
            if (spine == null) return;

            // For goblins on передаче start with 2ERO_START
            const string middleAnim = "2ERO_START";
            const bool isLoop = false; // START animation обычbut not зациклены

            if (spine.AnimationState != null)
            {
                var track = spine.AnimationState.SetAnimation(0, middleAnim, isLoop);
                if (track != null && track.Animation != null)
                {
                    spine.timeScale = 1f; // Reset timeScale
                }
                else
                {
                    Plugin.Log.LogError($"[GOBLIN FORCE] Failed to set animation {middleAnim} - track or animation is null");
                }
            }
            else
            {
                Plugin.Log.LogError($"[GOBLIN FORCE] AnimationState is null");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[GOBLIN FORCE] Error forcing animation to middle: {ex.Message}");
        }
    }
}

/// <summary>
/// MonoBehaviour for скрытия phrases передачи через задержку
/// </summary>
public class GoblinHandoffMessageHideScript : MonoBehaviour
{
    private Coroutine hideCoroutine;
    
    public void StartHide(float delay)
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }
        hideCoroutine = StartCoroutine(HideAfterDelay(delay));
    }
    
    private System.Collections.IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        var handoffText = GoblinPassLogic.GetHandoffText();
        if (handoffText != null)
        {
            handoffText.text = "";
        }
        var handoffUI = GoblinPassLogic.GetHandoffUI();
        if (handoffUI != null && handoffUI.activeSelf)
        {
            handoffUI.SetActive(false);
        }
    }
}

/// <summary>
/// MonoBehaviour for tracking enemy position и обновления позиции phrases передачи
/// </summary>
public class GoblinHandoffPositionTracker : MonoBehaviour
{
    void Update()
    {
        var currentHandoffEnemyTransform = GoblinPassLogic.GetCurrentHandoffEnemyTransform();
        var handoffUI = GoblinPassLogic.GetHandoffUI();
        var handoffRect = GoblinPassLogic.GetHandoffRect();
        var handoffText = GoblinPassLogic.GetHandoffText();
        
        if (currentHandoffEnemyTransform == null || Camera.main == null)
        {
            if (handoffUI != null && handoffUI.activeSelf)
            {
                handoffUI.SetActive(false);
            }
            return;
        }
        
        Vector3 worldPos = currentHandoffEnemyTransform.position;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        
        if (screenPos.z < 0)
        {
            if (handoffUI != null && handoffUI.activeSelf)
            {
                handoffUI.SetActive(false);
            }
            return;
        }
        
        if (handoffRect != null && handoffUI != null)
        {
            if (!handoffUI.activeSelf && !string.IsNullOrEmpty(handoffText?.text))
            {
                handoffUI.SetActive(true);
            }
            
            screenPos.y += 120f; // Upward offset над головой enemy
            
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                handoffRect.parent as RectTransform,
                screenPos,
                null,
                out localPoint
            );
            handoffRect.anchoredPosition = localPoint;
        }
    }
    

    /// <summary>
    /// Reset всех данных for goblins (is called on побеге from гангбанг цикла)
    /// By analogии with TouzokuNormalPassPatch
    /// </summary>
    internal static void ResetAll()
    {
        try
        {
            // Вызываем ResetAll from базового класса via reflection
            var baseType = typeof(BaseEnemyPassPatch<goblinero>);
            var resetMethod = baseType.GetMethod("ResetAll", BindingFlags.NonPublic | BindingFlags.Static);
            if (resetMethod != null)
            {
                resetMethod.Invoke(null, null);
                // Logs disabled
                // Plugin.Log.LogInfo("[GOBLIN] ResetAll called - base class state cleared (globalHandoffCount reset to 0)");
                // Plugin.Log.LogInfo( "[GOBLIN] ResetAll - base class cleared");
            }
            else
            {
            }
            
            // Clear UI elements for phrases передачи via reflection
            var uiField = typeof(GoblinPassLogic).GetField("handoffUI", BindingFlags.NonPublic | BindingFlags.Static);
            if (uiField != null)
            {
                var ui = uiField.GetValue(null) as GameObject;
                if (ui != null)
                {
                    UnityEngine.Object.Destroy(ui);
                }
                
                // Reset все поля
                var textField = typeof(GoblinPassLogic).GetField("handoffText", BindingFlags.NonPublic | BindingFlags.Static);
                var rectField = typeof(GoblinPassLogic).GetField("handoffRect", BindingFlags.NonPublic | BindingFlags.Static);
                var transformField = typeof(GoblinPassLogic).GetField("currentHandoffEnemyTransform", BindingFlags.NonPublic | BindingFlags.Static);
                
                uiField.SetValue(null, null);
                if (textField != null) textField.SetValue(null, null);
                if (rectField != null) rectField.SetValue(null, null);
                if (transformField != null) transformField.SetValue(null, null);
                
                // Plugin.Log.LogInfo("[GOBLIN] Handoff UI cleared");
            }
            
            // Logs disabled
            // Plugin.Log.LogInfo("[GOBLIN] ResetAll completed - all state cleared (ready for fresh start after escape)");
            // Plugin.Log.LogInfo( "[GOBLIN] ResetAll completed");
        }
        catch (System.Exception ex)
        {
        }
    }
}
