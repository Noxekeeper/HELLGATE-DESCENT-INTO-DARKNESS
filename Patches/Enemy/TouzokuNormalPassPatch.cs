using System;
using HarmonyLib;
using UnityEngine;
using Spine.Unity;
using System.Collections.Generic;
using System.Reflection;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Cache;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace NoREroMod;

/// <summary>
/// Patch for swordsman (TouzokuNormal) - GG handoff after 1 cycle (on JIGO)
/// Optimized: uses UnifiedPlayerCacheManager instead of FindGameObjectWithTag
/// </summary>
class TouzokuNormalPassPatch {
    
    // Animation cycle tracking per enemy instance
    private static Dictionary<object, int> enemyAnimationCycles = new Dictionary<object, int>();
    
    // H-session start time per enemy
    private static Dictionary<object, float> enemySessionStartTime = new Dictionary<object, float>();
    
    // Last cycle time (prevents duplicates)
    private static Dictionary<object, float> lastCycleTime = new Dictionary<object, float>();
    
    // Flag that enemy already passed GG (avoid repeat)
    private static Dictionary<object, bool> enemyHasPassed = new Dictionary<object, bool>();
    
    // Flag that enemy should be disabled from patch
    private static Dictionary<object, bool> enemyDisabled = new Dictionary<object, bool>();
    
    
    // Last speech/thought bubble time
    private static Dictionary<object, float> lastSpeechTime = new Dictionary<object, float>();
    private static Dictionary<object, float> lastThoughtTime = new Dictionary<object, float>();
    
    // Global handoff session tracking
    private static int globalHandoffCount = 0;
    private static float globalSessionStartTime = 0f;
    
    
    // Legacy hardcoded phrases and UI removed - using JSON system via DialogueDisplay
    
    // Handoff UI (above enemy head, like grab phrase)
    private static GameObject handoffUI;
    private static UnityEngine.UI.Text handoffText;
    private static RectTransform handoffRect;
    private static Transform currentHandoffEnemyTransform;

    internal static void ResetAll()
    {
        enemyAnimationCycles.Clear();
        enemySessionStartTime.Clear();
        lastCycleTime.Clear();
        enemyHasPassed.Clear();
        enemyDisabled.Clear();
        lastSpeechTime.Clear();
        lastThoughtTime.Clear();
        globalHandoffCount = 0;
        globalSessionStartTime = 0f;

        // Legacy UI cleared - using JSON system via DialogueDisplay

        if (handoffUI != null)
        {
            Object.Destroy(handoffUI);
            handoffUI = null;
            handoffText = null;
            handoffRect = null;
        }

        currentHandoffEnemyTransform = null;
    }
    
    // Patch for swordsman (EroTouzoku)
    [HarmonyPatch(typeof(EroTouzoku), "OnEvent")]
    [HarmonyPostfix]
    static void TouzokuNormalPass(EroTouzoku __instance, Spine.Event e, int ___se_count) {
        try {
            if (enemyDisabled.ContainsKey(__instance) && enemyDisabled[__instance]) {
                return;
            }

            // Optimization: use cached playercon
            var player = UnifiedPlayerCacheManager.GetPlayer();
            if (player == null || !player.eroflag || player.erodown == 0) {
                return;
            }
            
            // Get spine via reflection (field name unknown)
            var spineField = __instance.GetType().GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance) 
                          ?? __instance.GetType().GetField("mySpine", BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (spineField == null) {
                return;
            }
            
            var spine = spineField.GetValue(__instance) as SkeletonAnimation;
            if (spine == null) {
                return;
            }
            
            string currentAnim = spine.AnimationName;
            
            // Check this is H-animation, not combat
            if (!IsHAnimation(currentAnim)) {
                return; // Ignore combat animations
            }
            
            // Processing dialogue system events
            try {
                string eventName = e?.Data?.Name ?? e?.ToString() ?? string.Empty;
                
                // Get count via reflection for processing FIN on count == 1 (before start of animation)
                int count = 0;
                var countField = typeof(EroTouzoku).GetField("count", BindingFlags.Public | BindingFlags.Instance);
                if (countField != null)
                {
                    count = (int)(countField.GetValue(__instance) ?? 0);
                }
                
                // Debug logging START событий
                if (currentAnim == "START" || currentAnim == "START2" || currentAnim == "START3")
                {
                    // Plugin.Log.LogInfo($"[TOUZOKU NORMAL] H-anim event: anim={currentAnim}, event={eventName}, se_count={___se_count}");
                }
                
                // SPECIAL HANDLING: Для events FIN with count == 1 (before start of animation)
                // Show phrases типа "I'm about to cum!" or "I'm going to cum in you!"
                // This happens before того, as animation starts FIN (count == 2)
                if (eventName == "FIN" && count == 1)
                {
                    // Use специальное event "FIN_pre" for phrases before start of FIN
                    // Фразы are in section "FIN" in JSON, so we use "FIN" as animationName
                    bool enemySpoke = NoREroMod.Systems.Dialogue.TouzokuNormalHSceneDialogues.ProcessHSceneEvent(
                        __instance,
                        "FIN", // Фразы FIN_pre are in section FIN in JSON
                        "FIN_pre", // Специальное event for moment before FIN
                        0
                    );
                    // Aradia responses are handled centrally by DialogueFramework/DialogueEventProcessor.
                }
                
                // Processing кастомных phrases TouzokuNormal during H-scene
                // IMPORTANT: Для START, START2, START3 force call обработку with именем animation
                // This ensures that phrases будут показаны even if event comes with different name
                if (currentAnim == "START" || currentAnim == "START2" || currentAnim == "START3")
                {
                    // Force обрабатываем event with именем animation for initial событий
                    bool enemySpoke = NoREroMod.Systems.Dialogue.TouzokuNormalHSceneDialogues.ProcessHSceneEvent(
                        __instance,
                        currentAnim,
                        currentAnim, // Use имя animation as имя events
                        0
                    );

                    // Schedule GG response ГГ ONLY if enemy actually spoke phrase
                    if (enemySpoke)
                    {
                        Plugin.Log.LogInfo($"[TouzokuNormalPassPatch] START animation, calling Aradia system: anim={currentAnim}");
                        NoREroMod.Systems.Dialogue.AradiaTouzokuNormalDialogues.ProcessEnemyComment(
                            __instance,
                            currentAnim,
                            currentAnim,
                            0
                        );
                    }
                }
                
                // Check if this is event event переключения animation (matches name of animation)
                bool isAnimationSwitchEvent = eventName == currentAnim || 
                    eventName == "START" || eventName == "START2" || eventName == "START3" ||
                    eventName == "ERO" || eventName == "ERO1" || eventName == "ERO2" || eventName == "ERO3" || 
                    eventName == "ERO4" || eventName == "ERO5" || 
                    eventName == "2ERO" || eventName == "2ERO2" ||
                    eventName == "FIN" || eventName == "FIN2" || eventName == "FIN3" ||
                    eventName == "JIGO" || eventName == "JIGO2" ||
                    eventName == "_2ERO2" || eventName == "_2ERO3" || eventName == "_2ERO4" || 
                    eventName == "_2ERO6" || eventName == "_2ERO7" ||
                    eventName == "_ERO5" || eventName == "_ERO7" || eventName == "_ERO8" || 
                    eventName == "_ERO9" || eventName == "_ERO10" || eventName == "_ERO11" || eventName == "_ERO12" ||
                    eventName == "_FIN3" ||
                    eventName == "_JIGO2" || eventName == "_JIGO4";
                
                // Process event переключения animation (ERO, FIN etc.)
                // For нtheir используем имя animation as имя events
                if (isAnimationSwitchEvent && (currentAnim != "START" && currentAnim != "START2" && currentAnim != "START3"))
                {
                    // Process event переключения animation with именем animation as event
                    // This ensures that for ERO, ERO1, FIN etc. will be shown text
                    bool enemySpoke = NoREroMod.Systems.Dialogue.TouzokuNormalHSceneDialogues.ProcessHSceneEvent(
                        __instance,
                        currentAnim,
                        currentAnim, // Use имя animation as имя events
                        0
                    );

                    // Schedule GG response ГГ ONLY if enemy actually spoke phrase
                    if (enemySpoke)
                    {
                        Plugin.Log.LogInfo($"[TouzokuNormalPassPatch] Animation switch, calling Aradia system: anim={currentAnim}, event={currentAnim}");
                        NoREroMod.Systems.Dialogue.AradiaTouzokuNormalDialogues.ProcessEnemyComment(
                            __instance,
                            currentAnim,
                            currentAnim,
                            0
                        );
                    }
                }
                
                // Then process все events (включая SE, SE1, SE2, SE3, SE8)
                // Call with actual se_count и actual name events
                // But only if this НЕ event переключения animation (to avoid duplicate)
                if (!isAnimationSwitchEvent || eventName != currentAnim)
                {
                    // First process phrase enemy
                    // ProcessHSceneEvent returns true only if phrase was shown
                    bool enemySpoke = NoREroMod.Systems.Dialogue.TouzokuNormalHSceneDialogues.ProcessHSceneEvent(
                        __instance,
                        currentAnim,
                        eventName,
                        ___se_count
                    );
                    
                    // Schedule GG response ГГ ONLY if enemy actually spoke phrase
                    if (enemySpoke)
                    {
                        Plugin.Log.LogInfo($"[TouzokuNormalPassPatch] Enemy spoke, calling Aradia system: anim={currentAnim}, event={eventName}");
                        NoREroMod.Systems.Dialogue.AradiaTouzokuNormalDialogues.ProcessEnemyComment(
                            __instance,
                            currentAnim,
                            eventName,
                            ___se_count
                        );
                    }
                }
                else
                {
                    // For animation switch events animation check if spoke enemy phrase
                    bool enemySpoke = NoREroMod.Systems.Dialogue.TouzokuNormalHSceneDialogues.ProcessHSceneEvent(
                        __instance,
                        currentAnim,
                        currentAnim, // Use имя animation as имя events
                        0
                    );
                    
                    if (enemySpoke)
                    {
                        Plugin.Log.LogInfo($"[TouzokuNormalPassPatch] Animation switch (else block), calling Aradia system: anim={currentAnim}");
                        NoREroMod.Systems.Dialogue.AradiaTouzokuNormalDialogues.ProcessEnemyComment(
                            __instance,
                            currentAnim,
                            currentAnim,
                            0
                        );
                    }
                }
                
                NoREroMod.Systems.Dialogue.DialogueFramework.ProcessAnimationEvent(
                    __instance, 
                    currentAnim, 
                    eventName, 
                    ___se_count
                );
            } catch (System.Exception ex) {
            }
            
            TrackCycles(__instance, spine, e, ___se_count);
        } catch (System.Exception ex) {
        }
    }
    
    /// <summary>
    /// Checks that анимация является H-анимацией
    /// </summary>
    private static bool IsHAnimation(string animationName) {
        if (string.IsNullOrEmpty(animationName)) return false;
        
        // Список H-анимаций for мечника (including all with underscore)
        string[] hAnimations = {
            "START", "START2", "START3",
            "ERO", "ERO1", "ERO2", "ERO3", "ERO4", "ERO5",
            "2ERO", "2ERO2",
            "FIN", "FIN2", "FIN3",
            "JIGO", "JIGO2",
            "_2ERO2", "_2ERO3", "_2ERO4", "_2ERO6", "_2ERO7",
            "_ERO5", "_ERO7", "_ERO8", "_ERO9", "_ERO10", "_ERO11", "_ERO12",
            "_FIN3",
            "_JIGO2", "_JIGO4"
        };
        
        foreach (string hAnim in hAnimations) {
            if (animationName.StartsWith(hAnim)) {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Tracks циклы animation и hands off GG after 2 циклов
    /// </summary>
    private static void TrackCycles(object enemyInstance, SkeletonAnimation spine, Spine.Event e, int seCount) {
        try {
            if (Plugin.enableEnemyPass != null && !Plugin.enableEnemyPass.Value) {
                return;
            }
        } catch {
            // If Plugin not инициализирован, продолжаем работу
        }
        
        // Check that enemy еще not передал ГГ
        if (enemyHasPassed.ContainsKey(enemyInstance) && enemyHasPassed[enemyInstance]) {
            return;
        }
        
        string currentAnim = spine.AnimationName;
        string eventName = e.ToString();

        // Processing dialogue system events
        try {
            int dialogueSeCount = 0;
            if (enemyInstance is EroTouzoku touzoku) {
                // Get se_count via reflection
                var seCountField = typeof(EroTouzoku).GetField("se_count", BindingFlags.Public | BindingFlags.Instance);
                if (seCountField != null) {
                    dialogueSeCount = (int)(seCountField.GetValue(touzoku) ?? 0);
                }
            }
            NoREroMod.Systems.Dialogue.DialogueFramework.ProcessAnimationEvent(
                enemyInstance, 
                currentAnim, 
                eventName, 
                dialogueSeCount
            );
        } catch (Exception ex) {
        }

        MindBrokenSystem.ProcessAnimationEvent(enemyInstance, currentAnim, eventName);
        
        // Initialize сессию if this первый вызов
        if (!enemySessionStartTime.ContainsKey(enemyInstance)) {
            enemySessionStartTime[enemyInstance] = Time.time;
            
            // Initialize глобальную сессию if this первый enemy
            if (EnemyHandoffSystem.GlobalHandoffCount == 0) {
                globalSessionStartTime = Time.time;
            }
            
            // Check if this первый enemy or последующий (shared — любой тип)
            if (EnemyHandoffSystem.GlobalHandoffCount > 0) {
                // Force переводим к center animation
                ForceAnimationToMiddle(spine);
            }
        }
        
        // РЕЧЕВАЯ СИСТЕМА УБРАНА ОТСЮДА!
        // Теперь it in separatelyм патче EnemySpeechDisplayPatch
        // This НЕ влияет on handoff logic!
        
        if (IsCycleComplete(currentAnim, eventName, seCount)) {
            if (!enemyAnimationCycles.ContainsKey(enemyInstance)) {
                enemyAnimationCycles[enemyInstance] = 0;
            }
            enemyAnimationCycles[enemyInstance]++;
            
            // Pass ГГ after 1 полного цикла (on JIGO)
            if (enemyAnimationCycles[enemyInstance] >= 1) {
                globalHandoffCount++;
                EnemyHandoffSystem.GlobalHandoffCount++;
                enemyHasPassed[enemyInstance] = true;
                
                // Pass ГГ with задержкой from config
                StartDelayedHandoff(enemyInstance);
                return;
            }
        }
    }
    
    /// <summary>
    /// Determines завершение полного цикла animation.
    /// JIGO — when сам прошёл полный цикл (кульминация).
    /// JIGO2 — when получил ГГ и начал with JIGO (доиграл until JIGO2).
    /// </summary>
    private static bool IsCycleComplete(string animationName, string eventName, int seCount) {
        if (eventName == "JIGO") return true;   // Полный цикл: передача on кульминации
        if (eventName == "JIGO2") return true;  // Получил with JIGO: передача after JIGO2
        return false;
    }
    
    /// <summary>
    /// Force перевести animation on передаче ГГ.
    /// TouzokuNormal on передаче always начинает with JIGO2.
    /// </summary>
    private static void ForceAnimationToMiddle(SkeletonAnimation spine) {
        try {
            if (spine == null) return;
            
            spine.AnimationState.ClearTracks();
            spine.AnimationState.AddAnimation(0, "JIGO2", false, 0f);
        } catch (System.Exception ex) {
        }
    }
    
    /// <summary>
    /// Отталкивает ГГ from enemy for передачи другому enemyу
    /// </summary>
    private static void PushPlayerAwayFromEnemy(object enemyInstance) {
        try {
            // Optimization: use cached playercon
            GameObject playerObject = UnifiedPlayerCacheManager.GetPlayerObject();
            if (playerObject == null) {
                return;
            }
            
            // Mark enemy as disabled и скрываем его
            enemyDisabled[enemyInstance] = true;
            
            // Stop H-animation enemy
            var enemyComponent = enemyInstance as EroTouzoku;
            if (enemyComponent != null) {
                try {
                    // Get spine enemy via reflection
                    var enemySpineField = enemyComponent.GetType().GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance) 
                                        ?? enemyComponent.GetType().GetField("mySpine", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (enemySpineField != null) {
                        var enemySpine = enemySpineField.GetValue(enemyComponent) as SkeletonAnimation;
                        if (enemySpine != null) {
                            enemySpine.AnimationState.ClearTracks();
                            
                            // Try разные варианты idle animations
                            string[] idleAnimations = { "idle", "Idle", "IDLE", "wait", "Wait", "WAIT" };
                            bool animationSet = false;
                            foreach (string animName in idleAnimations) {
                                try {
                                    enemySpine.AnimationState.SetAnimation(0, animName, true);
                                    animationSet = true;
                                    break;
                                } catch {
                                    // Try next animation
                                }
                            }
                            
                            if (!animationSet) {
                            }
                        }
                    }
                    
                    // Make enemy неvisibleым (as было in старом решении)
                    var enemyMonoBehaviour = enemyComponent as MonoBehaviour;
                    // Logs disabled
                    // Plugin.Log.LogInfo($"[DEBUG] enemyMonoBehaviour: {enemyMonoBehaviour != null}");
                    if (enemyMonoBehaviour != null) {
                        var enemyGameObject = enemyMonoBehaviour.gameObject;
                        // Plugin.Log.LogInfo($"[DEBUG] enemyGameObject name: {enemyGameObject?.name ?? "NULL"}");
                        if (enemyGameObject != null) {
                            enemyGameObject.SetActive(false);
                        }
                    }
                } catch (System.Exception ex) {
                }
            }
            
            // Clear animation ГГ
            var playerSpine = playerObject.GetComponentInChildren<SkeletonAnimation>();
            if (playerSpine != null) {
                try {
                    playerSpine.AnimationState.ClearTracks();
                } catch (System.Exception ex) {
                }
            }
            
            // Get playercon via reflection
            var playerComponent = playerObject.GetComponent<playercon>();
            if (playerComponent == null) {
                return;
            }
            
            // Clear eroflag via reflection
            var eroFlagField = typeof(playercon).GetField("eroflag", BindingFlags.Public | BindingFlags.Instance);
            if (eroFlagField != null) {
                try {
                    eroFlagField.SetValue(playerComponent, false);
                } catch (System.Exception ex) {
                }
            }
            
            // Set GG animation to lying
            string[] downAnims = { "DOWN", "down", "Idle", "idle" };
            foreach (string animName in downAnims) {
                if (playerSpine != null) {
                    try {
                        playerSpine.AnimationState.SetAnimation(0, animName, true);
                        break;
                    }
                    catch (System.Exception ex)
                    {
                    }
                }
            }
            
            // Set erodown via reflection
            var eroDownField = typeof(playercon).GetField("erodown", BindingFlags.Public | BindingFlags.Instance);
            if (eroDownField != null) {
                eroDownField.SetValue(playerComponent, 1);
            }
            
            // Reset SP via PlayerStatus
            var playerStatus = playerObject.GetComponent<PlayerStatus>();
            if (playerStatus != null) {
                playerStatus.Sp = 0f;
            }
            
            // Push ГГ from enemy
            var enemyTransform = (enemyInstance as MonoBehaviour)?.transform;
            if (enemyTransform != null) {
                Vector3 enemyPos = enemyTransform.position;
                Vector3 playerPos = playerComponent.transform.position;
                Vector3 direction = playerPos - enemyPos;
                direction.Normalize();
                
                // Fix: if enemy is left from ГГ, push right
                if (direction.x < 0) {
                    direction = Vector3.right;
                } else {
                    direction = Vector3.left;
                }
                
                float pushDistance = 2f;
                Vector3 newPosition = playerComponent.transform.position + (direction * pushDistance);
                playerComponent.transform.position = newPosition;
                
                // Force сбрасываем vertical velocity so that избежать подбрасывания
                var rigi2d = playerComponent.rigi2d;
                if (rigi2d != null) {
                    rigi2d.velocity = new Vector2(rigi2d.velocity.x, 0f);
                }
            }
            
            // Reset flag борьбы
            StruggleSystem.setStruggleLevel(-1);
            
            // Enable sprite renderer
            var spriteRenderer = playerObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) {
                spriteRenderer.enabled = true;
            }
            
        } catch (System.Exception ex) {
        }
    }
    
    
    
    /// <summary>
    /// Get enemy Transform from instanceа
    /// </summary>
    static Transform GetEnemyTransform(object enemyInstance) {
        try {
            if (enemyInstance is MonoBehaviour mono) {
                return mono.transform;
            }
            
            // Try via reflection
            var transformField = enemyInstance.GetType().GetField("transform", 
                BindingFlags.Public | BindingFlags.Instance);
            if (transformField != null) {
                return transformField.GetValue(enemyInstance) as Transform;
            }
            
            return null;
            
        } catch (System.Exception ex) {
            return null;
        }
    }
    
    /// <summary>
    /// Get current animation enemy
    /// </summary>
    static string GetCurrentAnimation(object enemyInstance) {
        try {
            // Get spine via reflection
            var spineField = enemyInstance.GetType().GetField("myspine", 
                BindingFlags.NonPublic | BindingFlags.Instance) 
                          ?? enemyInstance.GetType().GetField("mySpine", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (spineField != null) {
                var spine = spineField.GetValue(enemyInstance) as SkeletonAnimation;
                if (spine != null) {
                    return spine.AnimationName ?? "";
                }
            }
            
            return "";
            
        } catch {
            return "";
        }
    }
    
    static Color GetColorForBubbleType(object type) {
        // REMOVED: Весь код dialogue system
        return Color.white;
    }
    
    // Patch on ImmediatelyERO for cleanup on escape через GiveUp
    [HarmonyPatch(typeof(playercon), "ImmediatelyERO")]
    [HarmonyPostfix]
    static void ClearStateOnImmediatelyERO() {
        try {
            // Проверяем тип enemy - очищаем only for TOUZOKU
            EroTouzoku currentEnemy = Object.FindObjectOfType<EroTouzoku>();
            if (currentEnemy == null)
            {
                // Do not TOUZOKU enemy - not очищаем
                return;
            }
            
            ClearStateData();
        } catch (System.Exception ex) {
        }
    }
    
    // Patch on StruggleSystem.startGrabInvul for cleanup on ручной борьбе
    [HarmonyPatch(typeof(StruggleSystem), "startGrabInvul")]
    [HarmonyPostfix]
    static void ClearStateCuStruggleEscape() {
        try {
            ClearStateData();
        } catch (System.Exception ex) {
        }
    }
    
    // Common function очистки состояния
    private static void ClearStateData() {
        // Logs disabled
        // Plugin.Log.LogInfo($"[CLEAR STATE] Before clear: globalHandoffCount={globalHandoffCount}, dictCounts=[cycles={enemyAnimationCycles.Count}, startTimes={enemySessionStartTime.Count}, hasPassed={enemyHasPassed.Count}]");
        
        // Clear все словари
        enemyAnimationCycles.Clear();
        enemySessionStartTime.Clear();
        lastCycleTime.Clear();
        enemyHasPassed.Clear();
        enemyDisabled.Clear();
        lastSpeechTime.Clear();
        lastThoughtTime.Clear();
        
        // Clear cooldown'ы облачкоin in глобальной системе (V2)
        // REMOVED: Вызоin dialogue system
        
        // Reset глобальные counters
        int oldGlobalCount = globalHandoffCount;
        globalHandoffCount = 0;
        globalSessionStartTime = 0f;
        
        // Plugin.Log.LogInfo($"[CLEAR STATE] After clear: globalHandoffCount={oldGlobalCount} -> {globalHandoffCount}, state fully cleared!");
    }
    
    /// <summary>
    /// Start задержку before передачей ГГ
    /// </summary>
    private static void StartDelayedHandoff(object enemyInstance) {
        try {
            // Optimization: use cached playercon
            var playerObj = UnifiedPlayerCacheManager.GetPlayerObject();
            if (playerObj == null) {
                // If нет Player, используем temp GameObject
                GameObject temp = new GameObject("DelayedHandoffTemp");
                var script = temp.AddComponent<DelayedHandoffScript>();
                script.StartDelayedHandoff(enemyInstance);
            } else {
                var script = playerObj.GetComponent<DelayedHandoffScript>();
                if (script == null) {
                    script = playerObj.AddComponent<DelayedHandoffScript>();
                }
                script.StartDelayedHandoff(enemyInstance);
            }
        } catch (System.Exception ex) {
            // Fallback: pass immediately without delay
            PushPlayerAwayFromEnemy(enemyInstance);
        }
    }
    
    /// <summary>
    /// Public method for invoking handoff (used by DelayedHandoffScript)
    /// </summary>
    public static void ExecuteHandoff(object enemyInstance) {
        PushPlayerAwayFromEnemy(enemyInstance);
    }
}

// MessageDisplayScript class removed - using JSON system via DialogueDisplay

/// <summary>
/// MonoBehaviour for скрытия сообщения передачи
/// </summary>
public class HandoffMessageHideScript : MonoBehaviour {
    private Coroutine hideCoroutine;
    
    public void StartHide(float delay) {
        if (hideCoroutine != null) {
            StopCoroutine(hideCoroutine);
        }
        hideCoroutine = StartCoroutine(HideCoroutine(delay));
    }
    
    private System.Collections.IEnumerator HideCoroutine(float delay) {
        yield return new WaitForSeconds(delay);
        
        var textField = typeof(TouzokuNormalPassPatch).GetField("handoffText", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (textField != null) {
            var text = textField.GetValue(null) as UnityEngine.UI.Text;
            if (text != null) {
                text.text = "";
            }
        }
    }
}

/// <summary>
/// MonoBehaviour for tracking enemy position - HANDOFF (red, BELOW speech)
/// </summary>
public class HandoffPositionTracker : MonoBehaviour {
    private RectTransform handoffRect;
    private Transform enemyTransform;
    
    private const float yOffset = 120f; // НИЖЕ чем речь
    private const float xOffset = 220f; // Same offset right
    
    void Start() {
        var handoffRectField = typeof(TouzokuNormalPassPatch).GetField("handoffRect",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (handoffRectField != null) {
            handoffRect = handoffRectField.GetValue(null) as RectTransform;
        }
    }
    
    void Update() {
        if (handoffRect == null) return;
        
        // Get current enemy Transform
        var enemyTransformField = typeof(TouzokuNormalPassPatch).GetField("currentHandoffEnemyTransform",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (enemyTransformField != null) {
            enemyTransform = enemyTransformField.GetValue(null) as Transform;
        }
        
        if (enemyTransform == null || Camera.main == null) return;
        
        // Convert world position enemy to screen position
        Vector3 worldPos = enemyTransform.position;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        
        if (screenPos.z < 0) {
            if (handoffRect.gameObject.activeSelf) {
                handoffRect.gameObject.SetActive(false);
            }
            return;
        }
        
        if (!handoffRect.gameObject.activeSelf) {
            handoffRect.gameObject.SetActive(true);
        }
        
        // Apply offset
        screenPos.y += yOffset;
        screenPos.x += xOffset;
        
        // Convert to local position
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

