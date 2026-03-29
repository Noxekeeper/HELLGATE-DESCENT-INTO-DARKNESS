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
/// Patch for axe wielder (TouzokuAxe) - GG handoff after 2 cycles
/// Optimized: uses UnifiedPlayerCacheManager instead of FindGameObjectWithTag
/// </summary>
class TouzokuAxePassPatch {
    
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
    
    // Legacy message timing removed - using JSON system via DialogueDisplay
    
    // Global handoff session tracking
    private static int globalHandoffCount = 0;
    private static float globalSessionStartTime = 0f;
    
    // Legacy hardcoded phrases and UI removed - using JSON system via DialogueDisplay

    internal static void ResetAll()
    {
        enemyAnimationCycles.Clear();
        enemySessionStartTime.Clear();
        lastCycleTime.Clear();
        enemyHasPassed.Clear();
        enemyDisabled.Clear();
        globalHandoffCount = 0;
        globalSessionStartTime = 0f;
    }
    
    // Patch for axe wielder (EroTouzokuAXE)
    [HarmonyPatch(typeof(EroTouzokuAXE), "OnEvent")]
    [HarmonyPostfix]
    static void TouzokuAxePass(EroTouzokuAXE __instance, Spine.Event e, int ___se_count) {
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
                
                // Processing кастомных phrases TouzokuAxe during H-scene
                // IMPORTANT: Для START, START2, START3, START4, START5 force call обработку with именем animation
                // This ensures that phrases будут показаны even if event comes with different name
                if (currentAnim == "START" || currentAnim == "START2" || currentAnim == "START3" || 
                    currentAnim == "START4" || currentAnim == "START5")
                {
                    // Force обрабатываем event with именем animation for initial событий
                    NoREroMod.Systems.Dialogue.TouzokuAxeHSceneDialogues.ProcessHSceneEvent(
                        __instance,
                        currentAnim,
                        currentAnim, // Use имя animation as имя events
                        0
                    );
                }
                
                // Check if this is event event переключения animation (matches name of animation)
                bool isAnimationSwitchEvent = eventName == currentAnim || 
                    eventName == "START" || eventName == "START2" || eventName == "START3" || 
                    eventName == "START4" || eventName == "START5" ||
                    eventName == "ERO" || eventName == "ERO2" ||
                    eventName == "2ERO" || eventName == "2ERO2" || eventName == "2ERO3" || eventName == "2ERO4" ||
                    eventName == "FIN" || eventName == "FIN2" || eventName == "FIN3" ||
                    eventName == "JIGO" || eventName == "JIGO2";
                
                // Process event переключения animation (ERO, FIN etc.)
                // For нtheir используем имя animation as имя events
                if (isAnimationSwitchEvent && (currentAnim != "START" && currentAnim != "START2" && 
                    currentAnim != "START3" && currentAnim != "START4" && currentAnim != "START5"))
                {
                    // Process event переключения animation with именем animation as event
                    // This ensures that for ERO, ERO2, FIN etc. will be shown text
                    NoREroMod.Systems.Dialogue.TouzokuAxeHSceneDialogues.ProcessHSceneEvent(
                        __instance,
                        currentAnim,
                        currentAnim, // Use имя animation as имя events
                        0
                    );
                }
                
                // Then process все events (включая SE, SE1, SE2, SE3, SE8)
                // Call with actual se_count и actual name events
                // But only if this НЕ event переключения animation (to avoid duplicate)
                if (!isAnimationSwitchEvent || eventName != currentAnim)
                {
                    NoREroMod.Systems.Dialogue.TouzokuAxeHSceneDialogues.ProcessHSceneEvent(
                        __instance,
                        currentAnim,
                        eventName,
                        ___se_count
                    );
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
    /// Список соответствует реальным анимациям from EroTouzokuAXE.cs
    /// </summary>
    private static bool IsHAnimation(string animationName) {
        if (string.IsNullOrEmpty(animationName)) return false;
        
        // Список H-анимаций for TouzokuAxe (from исходного кода EroTouzokuAXE.cs)
        string[] hAnimations = {
            "START", "START2", "START3", "START4", "START5",
            "ERO", "ERO2",
            "2ERO", "2ERO2", "2ERO3", "2ERO4",
            "FIN", "FIN2", "FIN3",
            "JIGO", "JIGO2"
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
        // Plugin.Log.LogInfo($"[TRACK CYCLES] ENTRY: anim={spine?.AnimationName}, event={e?.ToString()}");
        
        try {
            if (Plugin.enableEnemyPass != null && !Plugin.enableEnemyPass.Value) {
                // Plugin.Log.LogInfo("[TRACK CYCLES] EXIT: enabled=false");
                return;
            }
        } catch {
            // If Plugin not инициализирован, продолжаем работу
        }
        
        // Check that enemy еще not передал ГГ
        if (enemyHasPassed.ContainsKey(enemyInstance) && enemyHasPassed[enemyInstance]) {
            // Plugin.Log.LogInfo("[TRACK CYCLES] EXIT: enemy already passed");
            return;
        }
        
        string currentAnim = spine.AnimationName;
        string eventName = e.ToString();

        // Processing MindBroken системы
        MindBrokenSystem.ProcessAnimationEvent(enemyInstance, currentAnim, eventName);
        
        // Initialize сессию if this первый вызов
        if (!enemySessionStartTime.ContainsKey(enemyInstance)) {
            enemySessionStartTime[enemyInstance] = Time.time;
            
            // Initialize глобальную сессию if this первый enemy
            if (EnemyHandoffSystem.GlobalHandoffCount == 0) {
                globalSessionStartTime = Time.time;
                // Plugin.Log.LogInfo($"[TOUZOKU AXE] Start global session at {Time.time:F3}s (globalHandoffCount={globalHandoffCount})");
            }
            
            // Check if this первый enemy or последующий (shared — любой тип)
            if (EnemyHandoffSystem.GlobalHandoffCount > 0) {
                // Plugin.Log.LogInfo($"[TOUZOKU AXE] Subsequent enemy #{globalHandoffCount + 1} at {Time.time:F3}s - force to middle (globalHandoffCount={globalHandoffCount})");
                // Force переводим к center animation
                ForceAnimationToMiddle(spine);
            } else {
                // Plugin.Log.LogInfo($"[TOUZOKU AXE] First enemy at {Time.time:F3}s - full animation {currentAnim}");
            }
        }
        
        // СТАРАЯ СИСТЕМА ОТКЛЮЧЕНА - используется EnemySpeechDisplayPatch
        
        // Рассчитываем время with начала сессии for этого enemy
        float enemyElapsedTime = Time.time - enemySessionStartTime[enemyInstance];
        float globalElapsedTime = globalSessionStartTime > 0 ? Time.time - globalSessionStartTime : 0f;
        
        // Проверяем завершение цикла animation
        // Log only важные events раз in second
        if ((currentAnim.Contains("START") || currentAnim.Contains("ERO") || currentAnim.Contains("FIN") || currentAnim.Contains("JIGO")) 
            && (enemyElapsedTime % 1.0f < 0.1f)) {
            // Plugin.Log.LogInfo($"[TIME] TouzokuNormal | Global={globalElapsedTime:F1}s | Local={enemyElapsedTime:F1}s | Anim={currentAnim} | Event={eventName} | SE={seCount}");
        }
        
        if (IsCycleComplete(currentAnim, eventName, seCount)) {
            if (!enemyAnimationCycles.ContainsKey(enemyInstance)) {
                enemyAnimationCycles[enemyInstance] = 0;
            }
            enemyAnimationCycles[enemyInstance]++;
            
            // Plugin.Log.LogInfo($"[CYCLE] TouzokuNormal: Completed cycle #{enemyAnimationCycles[enemyInstance]} in {enemyElapsedTime:F2}s (anim={currentAnim}, event={eventName})");
            
            // Pass ГГ after двух полных циклов
            if (enemyAnimationCycles[enemyInstance] >= 2) {
                globalHandoffCount++;
                EnemyHandoffSystem.GlobalHandoffCount++;
                // Logs disabled
                // Plugin.Log.LogInfo($"[TOUZOKU AXE] Passing GG after {enemyAnimationCycles[enemyInstance]} cycles! (Global handoff #{globalHandoffCount})");
                // Plugin.Log.LogInfo("[DEBUG] About to call ShowHandoffMessage and PushPlayerAwayFromEnemy");
                
                enemyHasPassed[enemyInstance] = true;
                
                // СТАРАЯ СИСТЕМА ОТКЛЮЧЕНА - phrases через EnemySpeechDisplayPatch!
                // if (Plugin.enableHandoffMessages.Value) {
                //     ShowHandoffMessage();
                // }
                
                // Pass ГГ with задержкой from config
                StartDelayedHandoff(enemyInstance);
                
                // Plugin.Log.LogInfo("[DEBUG] After PushPlayerAwayFromEnemy, about to return");
                return;
            }
        }
    }
    
    /// <summary>
    /// Determines завершение полного цикла animation
    /// </summary>
    private static bool IsCycleComplete(string animationName, string eventName, int seCount) {
        // TouzokuAxe: FIN - кульминация with se_count==1, затем FIN2, FIN3, JIGO, JIGO2
        // Идеальное место передачи - after FIN (кульминация)
        if (animationName == "FIN" && eventName == "FIN") {
            // Plugin.Log.LogInfo("[CYCLE DETECTION] TouzokuAxe: CYCLE COMPLETE! FIN + FIN - After climax");
            return true;
        }
        // Альтернатива: FIN2
        else if (animationName == "FIN2" && eventName == "FIN2") {
            // Plugin.Log.LogInfo("[CYCLE DETECTION] TouzokuAxe: CYCLE COMPLETE! FIN2 + FIN2 - Fallback");
            return true;
        }
        // Last fallback: FIN3
        else if (animationName == "FIN3" && eventName == "FIN3") {
            // Plugin.Log.LogInfo("[CYCLE DETECTION] TouzokuAxe: CYCLE COMPLETE! FIN3 + FIN3 - Final fallback");
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Force перевести animation к center (случайная точка входа)
    /// For TouzokuAxe: используем конец цикла (FIN2/FIN3/JIGO) for корректного idle
    /// </summary>
    private static void ForceAnimationToMiddle(SkeletonAnimation spine) {
        try {
            if (spine == null) return;
            
            // For TouzokuAxe: начало with конца цикла so that idle correctly грузился
            // DO NOT используем ERO2, only финальные animation after кульминации
            const string endAnim = "JIGO";
            const bool isLoop = false; // Анимация JIGO not looped
            
            // Plugin.Log.LogInfo($"[ANIMATION] Force TouzokuAxe to end cycle: {endAnim} (loop={isLoop})");
            
            // Clear текущую animation и устанавливаем animation with конца цикла
            spine.AnimationState.ClearTracks();
            spine.AnimationState.AddAnimation(0, endAnim, isLoop, 0f);
        } catch (System.Exception ex) {
        }
    }
    
    /// <summary>
    /// Отталкивает ГГ from enemy for передачи другому enemyу
    /// </summary>
    private static void PushPlayerAwayFromEnemy(object enemyInstance) {
        // Logs disabled
        // Plugin.Log.LogInfo("[DEBUG] PushPlayerAwayFromEnemy called!");
        try {
            // Plugin.Log.LogInfo("[TOUZOKU AXE] === Pushing GG away ===");
            
            // Находим ГГ
            // Optimization: use cached playercon
            GameObject playerObject = UnifiedPlayerCacheManager.GetPlayerObject();
            if (playerObject == null) {
                return;
            }
            
            // Plugin.Log.LogInfo($"[DEBUG] Player found, name={playerObject?.name ?? "NULL"}");
            
            // Mark enemy as disabled и скрываем его
            enemyDisabled[enemyInstance] = true;
            // Plugin.Log.LogInfo($"[TOUZOKU AXE] Enemy marked as disabled. Type: {enemyInstance?.GetType()?.Name}");
            
            // Stop H-animation enemy
            var enemyComponent = enemyInstance as EroTouzokuAXE;
            // Plugin.Log.LogInfo($"[DEBUG] enemyComponent after cast: {enemyComponent != null}");
            if (enemyComponent != null) {
                try {
                    // Get spine enemy via reflection
                    var enemySpineField = enemyComponent.GetType().GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance) 
                                        ?? enemyComponent.GetType().GetField("mySpine", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (enemySpineField != null) {
                        var enemySpine = enemySpineField.GetValue(enemyComponent) as SkeletonAnimation;
                        if (enemySpine != null) {
                            // Plugin.Log.LogInfo("[TOUZOKU AXE] Stopping enemy H-animation... looking for idle animation");
                            enemySpine.AnimationState.ClearTracks();
                            
                            // Try разные варианты idle анима образ for TouzokuAxe
                            string[] idleAnimations = { "IDLE", "idle", "Idle", "WAIT", "wait", "Wait" };
                            bool animationSet = false;
                            foreach (string animName in idleAnimations) {
                                try {
                                    enemySpine.AnimationState.SetAnimation(0, animName, true);
                                    // Plugin.Log.LogInfo($"[TOUZOKU AXE] Set enemy animation to '{animName}'");
                                    animationSet = true;
                                    break;
                                } catch {
                                    // Try next animation
                                }
                            }
                            
                            if (!animationSet) {
                                // Последняя попытка - финальные animation
                                try {
                                    enemySpine.AnimationState.SetAnimation(0, "FIN2", false);
                                    animationSet = true;
                                }
                                catch (System.Exception ex1)
                                {
                                    try
                                    {
                                        enemySpine.AnimationState.SetAnimation(0, "JIGO", false);
                                        animationSet = true;
                                    }
                                    catch (System.Exception ex2)
                                    {
                                    }
                                }
                            }
                        }
                    }
                    
                    // Нужbut восстановить основную модель enemy (вместо erodata)
                    // erodata - this дочерний объект, основная модель - родитель
                    var enemyMonoBehaviour = enemyComponent as MonoBehaviour;
                    if (enemyMonoBehaviour != null) {
                        // Скрываем erodata
                        var erodataObject = enemyMonoBehaviour.gameObject;
                        // Plugin.Log.LogInfo($"[DEBUG] erodata name: {erodataObject?.name ?? "NULL"}");
                        
                        // Ищем родителя (основная модель enemy)
                        var parentObject = erodataObject.transform.parent?.gameObject;
                        if (parentObject != null) {
                            // Проверяем есть ли component TouzokuAxe
                            var touzokuAxeComponent = parentObject.GetComponent<TouzokuAxe>();
                            if (touzokuAxeComponent != null) {
                                // Plugin.Log.LogInfo("[TOUZOKU AXE] Restoring main enemy model...");
                                
                                // Reset eroflag via reflection
                                var axeEroFlagField = typeof(TouzokuAxe).GetField("eroflag", BindingFlags.NonPublic | BindingFlags.Instance);
                                if (axeEroFlagField != null) {
                                    axeEroFlagField.SetValue(touzokuAxeComponent, false);
                                    // Plugin.Log.LogInfo("[TOUZOKU AXE] eroflag set to false");
                                }
                                
                                // Включаем MeshRenderer
                                var axeMeshRendererField = typeof(TouzokuAxe).GetField("myspinerennder", BindingFlags.NonPublic | BindingFlags.Instance);
                                if (axeMeshRendererField != null) {
                                    var axeMeshRenderer = axeMeshRendererField.GetValue(touzokuAxeComponent) as MeshRenderer;
                                    if (axeMeshRenderer != null) {
                                        axeMeshRenderer.enabled = true;
                                        // Plugin.Log.LogInfo("[TOUZOKU AXE] MeshRenderer enabled");
                                    }
                                }
                                
                                // Скрываем erodata
                                erodataObject.SetActive(false);
                                // Plugin.Log.LogInfo("[TOUZOKU AXE] erodata hidden");
                            } else {
                                // Fallback: просто скрываем erodata
                                erodataObject.SetActive(false);
                            }
                        } else {
                            erodataObject.SetActive(false);
                        }
                    }
                } catch (System.Exception ex) {
                }
            }
            
            // Clear animation ГГ
            // Plugin.Log.LogInfo("[DEBUG] Getting SkeletonAnimation...");
            var playerSpine = playerObject.GetComponentInChildren<SkeletonAnimation>();
            // Plugin.Log.LogInfo($"[DEBUG] SkeletonAnimation: {playerSpine?.name ?? "NULL"}");
            if (playerSpine != null) {
                try {
                    // Plugin.Log.LogInfo("[DEBUG] Calling ClearTracks...");
                    playerSpine.AnimationState.ClearTracks();
                    // Plugin.Log.LogInfo("[DEBUG] ClearTracks completed!");
                    // Plugin.Log.LogInfo("[TOUZOKU AXE] Player spine cleared");
                } catch (System.Exception ex) {
                }
            }
            
            // Get playercon via reflection
            // Plugin.Log.LogInfo("[DEBUG] Getting playercon...");
            var playerComponent = playerObject.GetComponent<playercon>();
            // Plugin.Log.LogInfo($"[DEBUG] playercon: {playerComponent != null}");
            if (playerComponent == null) {
                return;
            }
            
            // Clear eroflag via reflection
            // Plugin.Log.LogInfo("[DEBUG] Getting eroflag field...");
            var eroFlagField = typeof(playercon).GetField("eroflag", BindingFlags.Public | BindingFlags.Instance);
            // Plugin.Log.LogInfo($"[DEBUG] eroFlagField: {eroFlagField != null}");
            if (eroFlagField != null) {
                try {
                    // Plugin.Log.LogInfo("[DEBUG] Setting eroflag to false...");
                    eroFlagField.SetValue(playerComponent, false);
                    // Plugin.Log.LogInfo("[TOUZOKU AXE] eroflag set to false (exit H-scene)");
                } catch (System.Exception ex) {
                }
            }
            
            // Set GG animation to lying
            string[] downAnims = { "DOWN", "down", "Idle", "idle" };
            foreach (string animName in downAnims) {
                if (playerSpine != null) {
                    try {
                        playerSpine.AnimationState.SetAnimation(0, animName, true);
                        // Plugin.Log.LogInfo($"[TOUZOKU AXE] GG animation set to '{animName}'");
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
                // Plugin.Log.LogInfo("[TOUZOKU AXE] erodown set to 1 (prone)");
            }
            
            // Reset SP via PlayerStatus
            var playerStatus = playerObject.GetComponent<PlayerStatus>();
            if (playerStatus != null) {
                playerStatus.Sp = 0f;
                // Plugin.Log.LogInfo("[TOUZOKU AXE] SP reset to 0");
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
                    // Plugin.Log.LogInfo("[TOUZOKU AXE] Vertical velocity reset to prevent bounce");
                }
                
                // Plugin.Log.LogInfo($"[TOUZOKU AXE] GG pushed to: {newPosition}");
                // Plugin.Log.LogInfo($"[TOUZOKU AXE] Direction: {direction}");
            }
            
            // Reset flag борьбы
            StruggleSystem.setStruggleLevel(-1);
            
            // Enable sprite renderer
            var spriteRenderer = playerObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) {
                spriteRenderer.enabled = true;
                // Plugin.Log.LogInfo("[TOUZOKU AXE] Sprite renderer enabled");
            }
            
            // Plugin.Log.LogInfo("[TOUZOKU AXE] === Push completed ===");
            
        } catch (System.Exception ex) {
        }
    }
    
    // DisplayMessage function removed - using JSON system via DialogueDisplay
    
    // ShowDirtyMessage removed - using JSON system via DialogueDisplay
    
    // ShowHandoffMessage removed - using JSON system via DialogueDisplay
    
    // Patch on ImmediatelyERO for cleanup on escape через GiveUp
    [HarmonyPatch(typeof(playercon), "ImmediatelyERO")]
    [HarmonyPostfix]
    static void ClearStateOnImmediatelyERO() {
        try {
            // Проверяем тип enemy - очищаем only for TOUZOKU AXE
            EroTouzokuAXE currentEnemy = Object.FindObjectOfType<EroTouzokuAXE>();
            if (currentEnemy == null)
            {
                // Do not TOUZOKU AXE enemy - not очищаем
                return;
            }
            
            // Plugin.Log.LogInfo("[TOUZOKU AXE] === CLEAR ON IMMEDIATELYERO (GiveUp) ===");
            ClearStateData();
        } catch (System.Exception ex) {
        }
    }
    
    // Patch on StruggleSystem.startGrabInvul for cleanup on ручной борьбе
    [HarmonyPatch(typeof(StruggleSystem), "startGrabInvul")]
    [HarmonyPostfix]
    static void ClearStateCuStruggleEscape() {
        try {
            // Plugin.Log.LogInfo("[TOUZOKU AXE] === CLEAR ON STRUGGLE ESCAPE ===");
            ClearStateData();
        } catch (System.Exception ex) {
        }
    }
    
    // Common function очистки состояния
    private static void ClearStateData() {
        // Plugin.Log.LogInfo($"[CLEAR STATE] Before clear: globalHandoffCount={globalHandoffCount}, dictCounts=[cycles={enemyAnimationCycles.Count}, startTimes={enemySessionStartTime.Count}, hasPassed={enemyHasPassed.Count}]");
        
        // Clear все словари
        enemyAnimationCycles.Clear();
        enemySessionStartTime.Clear();
        lastCycleTime.Clear();
        enemyHasPassed.Clear();
        enemyDisabled.Clear();
        
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

// MessageDisplayScriptAxe class removed - using JSON system via DialogueDisplay

