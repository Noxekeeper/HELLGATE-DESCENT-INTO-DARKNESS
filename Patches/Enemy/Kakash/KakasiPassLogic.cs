using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Spine;
using Spine.Unity;
using NoREroMod;
using NoREroMod.Patches.Enemy.Base;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Patches.Enemy.Kakash;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Patches.Enemy.Kakash
{
    /// <summary>
    /// Handoff logic and tracking cycles for Kakasi (Scarecrow).
    /// Optimized: Uses UnifiedPlayerCacheManager instead of FindGameObjectWithTag
    /// </summary>
    internal class KakasiPassLogic : BaseEnemyPassPatch<kakashi_ero2>
    {
        protected override string EnemyName => "Kakasi";

        /// <summary>
        /// Kakasi hands off GG after one cycle.
        /// </summary>
        protected override int CyclesBeforePass => 1;

        private static readonly Dictionary<object, bool> startEffectTriggered = new();
        private static readonly Dictionary<object, bool> aphroEffectTriggered = new();
        private static readonly Dictionary<object, bool> floorEffectTriggered = new();
        private static readonly Dictionary<object, bool> finEffectTriggered = new();
        private static readonly Dictionary<object, string> previousAnimation = new(); // Track previous animation for determining transitions
        private static readonly FieldInfo KakashField = typeof(kakashi_ero2).GetField("kakash", BindingFlags.NonPublic | BindingFlags.Instance);
        
        // Global tracking: whether effect was already for this type enemy in current gangbang cycle
        // If the same type enemy picks up GG again, effects should not trigger
        internal static bool kakasiTypeEffectTriggered = false;

        protected override string[] GetHAnimations()
        {
            return new[]
            {
                "START",
                "START2",
                "ERO2", "ERO3", "ERO4", "ERO5",
                "FIN",
                "JIGO1", "JIGO2"
            };
        }

        protected override bool IsCycleComplete(string animationName, string eventName, int seCount)
        {
            string anim = animationName?.ToUpperInvariant() ?? string.Empty;
            string evt = eventName?.ToUpperInvariant() ?? string.Empty;

            // JIGO2 = main point cycle completion (as START_JIGO у Mutude)
            // Check both by name animation, так и by event for reliability
            if (anim == "JIGO2" || evt == "JIGO2")
            {
                // Logs disabled
                // Plugin.Log.LogInfo("[CYCLE DETECTION] Kakasi: cycle complete on JIGO2 (post-finish hold).");
                // Plugin.Log.LogInfo( "[KAKASI CYCLE] Complete on JIGO2");
                return true;
            }

            return false;
        }

        protected override string GetEnemyTypeName()
        {
            return "kakasi";
        }

        /// <summary>
        /// Override GetSpineAnimation for Kakasi - ALL animations используют s_kakasi_ero2 (myspine)
        /// </summary>
        protected static new SkeletonAnimation GetSpineAnimation(object enemyInstance)
        {
            try
            {
                // Get myspine (s_kakasi_ero2) for ALL animations, including START
                var spineField = enemyInstance.GetType().GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance)
                              ?? enemyInstance.GetType().GetField("mySpine", BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (spineField != null)
                {
                    return spineField.GetValue(enemyInstance) as SkeletonAnimation;
                }
            }
            catch (Exception ex)
            {
            }
            return null;
        }

        protected override void ForceAnimationToMiddle(SkeletonAnimation spine)
        {
            if (spine == null || spine.AnimationState == null)
            {
                return;
            }

            try
            {
                spine.AnimationState.ClearTracks();

                // After handoff always start with START2 (only 1 вариант)
                // Use loop=true for continuous animation, as with other enemies
                const string chosen = "START2";
                const bool isLoop = true;

                var track = spine.AnimationState.SetAnimation(0, chosen, isLoop);
                if (track?.Animation != null)
                {
                    // Start from middle of animation (GG already in position)
                    track.Time = track.Animation.Duration * 0.35f;
                }

                // Plugin.Log.LogInfo($"[KAKASI] Forced subsequent enemy to resume from {chosen} (loop={isLoop}, fixed - only 1 variant).");
            }
            catch (Exception ex)
            {
            }
        }

        [HarmonyPatch(typeof(kakashi_ero2), "OnEvent")]
        [HarmonyPrefix]
        private static void KakasiPassPrefix(kakashi_ero2 __instance, Spine.AnimationState state, int trackIndex, Spine.Event e)
        {
            try
            {
                string eventName = e?.Data?.Name ?? e?.ToString() ?? string.Empty;
                if (!eventName.Equals("SE", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // Get spine for checking current animation
                var spine = GetSpineAnimation(__instance);
                if (spine == null)
                {
                    return;
                }

                string currentAnim = spine.AnimationName ?? string.Empty;
                
                // CRITICAL: Check se_count BEFORE increment in оригинальном коде
                // If se_count == 4 and animation START, то after increment will 5
                // This moment when we need to trigger effect BEFORE se_count resets
                // Slow-mo и zoom only if this first time for Kakasi type
                if (currentAnim.Equals("START", StringComparison.OrdinalIgnoreCase) && __instance.se_count == 4)
                {
                    // Plugin.Log.LogInfo($"[KAKASI] ===== PREFIX: START se_count will be 5! (current={__instance.se_count}) ===== anim={currentAnim}, event={eventName}");
                    
                    // Эффекты теперь handled via Camera Framework
                    // REMOVED: KakasiEffects.TriggerStartSe5Effect
                }
            }
            catch (Exception ex)
            {
            }
        }

        [HarmonyPatch(typeof(kakashi_ero2), "OnEvent")]
        [HarmonyPostfix]
        private static void KakasiPass(kakashi_ero2 __instance, Spine.AnimationState state, int trackIndex, Spine.Event e)
        {
            var instance = new KakasiPassLogic();
            SetInstance(instance);

            try
            {
                if (enemyDisabled.ContainsKey(__instance) && enemyDisabled[__instance])
                {
                    return;
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
                    return;
                }

                string currentAnim = spine.AnimationName ?? string.Empty;
                if (!instance.IsHAnimation(currentAnim))
                {
                    return;
                }

                // CRITICAL: For subsequent enemies in gangbang (any type) force switch on START2
                // Use shared EnemyHandoffSystem.GlobalHandoffCount
                // If this subsequent enemy (GlobalHandoffCount > 0) and current animation - "start" (any case),
                // force switch on "START2" (with правильным регистром - заглавные буквы)
                string currentAnimUpper = currentAnim.ToUpperInvariant();
                if (EnemyHandoffSystem.GlobalHandoffCount > 0 && currentAnimUpper == "START" && currentAnim != "START2")
                {
                    // Plugin.Log.LogInfo($"[KAKASI] Subsequent enemy detected (globalHandoffCount={globalHandoffCount}), forcing {currentAnim} -> START2");
                    
                    if (spine.AnimationState != null)
                    {
                        spine.AnimationState.ClearTracks();
                        // Use "START2" with capital letters (as in GetHAnimations)
                        var track = spine.AnimationState.SetAnimation(0, "START2", true); // loop=true for continuous animation
                        if (track?.Animation != null)
                        {
                            // Start from middle of animation (GG already in position)
                            track.Time = track.Animation.Duration * 0.35f;
                        }
                        currentAnim = "START2"; // Update for further processing
                        
                        // Update previousAnimation, to avoid false triggers on animation change
                        previousAnimation[__instance] = "START2";
                    }
                }

                string eventName = e?.Data?.Name ?? e?.ToString() ?? string.Empty;

                // Additional logging for debugging START se_count==5
                if (currentAnim.Equals("START", StringComparison.OrdinalIgnoreCase) && __instance.se_count == 5)
                {
                    // Plugin.Log.LogInfo($"[KAKASI DEBUG] START se_count==5 DETECTED in OnEvent! anim={currentAnim}, event={eventName}, se_count={__instance.se_count}");
                }

                // Track transitions between animations
                string prevAnim = previousAnimation.ContainsKey(__instance) ? previousAnimation[__instance] : string.Empty;
                bool animationChanged = !string.IsNullOrEmpty(prevAnim) && !prevAnim.Equals(currentAnim, StringComparison.OrdinalIgnoreCase);
                
                // Update previous animation
                previousAnimation[__instance] = currentAnim;

                // IMPORTANT: se_count incremented in original OnEvent BEFORE our Postfix
                // Но мы вызываем HandleAnimationPhases AFTER increment, so that se_count already updated
                // Plugin.Log.LogInfo($"[KAKASI] OnEvent: anim={currentAnim}, event={eventName}, se_count={__instance.se_count}, prevAnim={prevAnim}, changed={animationChanged}");

                // Initialize flags эффектов
                if (!startEffectTriggered.ContainsKey(__instance))
                {
                    startEffectTriggered[__instance] = false;
                }
                if (!aphroEffectTriggered.ContainsKey(__instance))
                {
                    aphroEffectTriggered[__instance] = false;
                }
                if (!floorEffectTriggered.ContainsKey(__instance))
                {
                    floorEffectTriggered[__instance] = false;
                }
                if (!finEffectTriggered.ContainsKey(__instance))
                {
                    finEffectTriggered[__instance] = false;
                }

                // CRITICAL: If this new grab (START se_count==1), проверяем whether effect was already for this type enemy
                // FIXED: Если the same type enemy picks up GG again in цикле гангбанг, effects should not trigger
                if (currentAnim.Equals("START", StringComparison.OrdinalIgnoreCase) && 
                    eventName.Equals("SE", StringComparison.OrdinalIgnoreCase) && 
                    __instance.se_count == 1)
                {
                    // Check whether effect was already for Kakasi type in current gangbang cycle
                    if (kakasiTypeEffectTriggered)
                    {
                        // Plugin.Log.LogInfo("[KAKASI] ===== Same enemy type (Kakasi) picking up player again - skipping slow-mo and zoom effects! =====");
                        
                        // Эффекты camera now handled via Camera Framework
                        // REMOVED: KakasiEffects.ClearZoomTarget
                        
                        // CRITICAL: Also clear cross state, so that cross effects would not trigger on втором цикле
                        try
                        {
                            var resetCrossMethod = typeof(KakasiCrossPatch)
                                .GetMethod("ResetCrossState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                            if (resetCrossMethod != null)
                            {
                                resetCrossMethod.Invoke(null, null);
                                // Plugin.Log.LogInfo("[KAKASI] Reset cross state for second cycle");
                            }
                        }
                        catch (System.Exception ex)
                        {
                        }
                        
                        // Do not reset флаги - this повторный захват того же типа
                        // Но продолжаем обработку for баннероin и phrases
                    }
                    else
                    {
                        // Plugin.Log.LogInfo("[KAKASI] ===== New grab detected (START se_count==1) - first time for Kakasi type, triggering effects! =====");
                        // Эффекты теперь handled via Camera Framework
                        // REMOVED: KakasiEffects.StopAll
                        
                        // Reset all flags effects for этого instanceа
                        if (startEffectTriggered.ContainsKey(__instance))
                            startEffectTriggered[__instance] = false;
                        if (floorEffectTriggered.ContainsKey(__instance))
                            floorEffectTriggered[__instance] = false;
                        if (finEffectTriggered.ContainsKey(__instance))
                            finEffectTriggered[__instance] = false;
                        
                        // Reset tracking предыдущей animation
                        if (previousAnimation.ContainsKey(__instance))
                            previousAnimation.Remove(__instance);
                    }
                }

                // Processing phases animation и slow-mo эффектов
                HandleAnimationPhases(__instance, currentAnim, __instance.se_count, spine, eventName, animationChanged, prevAnim);

                // Show speech Kakasi in комикс-канвасе (привязанном к кости bone24)
                ShowKakasiSpeech(spine, currentAnim, __instance.se_count);

                // Show GG thoughts
                ShowKakasiThought(spine, currentAnim);

                // Processing dialogue system events
                // Process as SE events, так и events переключения анимаций (ERO2, ERO3, ERO4, ERO5, FIN, JIGO1, JIGO2, START2)
                try {
                    // For SE events используем стандартную обработку
                    if (eventName.Equals("SE", StringComparison.OrdinalIgnoreCase))
                    {
                        NoREroMod.Systems.Dialogue.DialogueFramework.ProcessAnimationEvent(
                            __instance, 
                            currentAnim, 
                            eventName, 
                            __instance.se_count
                        );
                    }
                    // For animation switch events анимаций вызываем directly KakasiHSceneDialogues
                    else if (eventName.Equals("ERO2", StringComparison.OrdinalIgnoreCase) ||
                             eventName.Equals("ERO3", StringComparison.OrdinalIgnoreCase) ||
                             eventName.Equals("ERO4", StringComparison.OrdinalIgnoreCase) ||
                             eventName.Equals("ERO5", StringComparison.OrdinalIgnoreCase) ||
                             eventName.Equals("FIN", StringComparison.OrdinalIgnoreCase) ||
                             eventName.Equals("JIGO1", StringComparison.OrdinalIgnoreCase) ||
                             eventName.Equals("JIGO2", StringComparison.OrdinalIgnoreCase) ||
                             eventName.Equals("START2", StringComparison.OrdinalIgnoreCase))
                    {
                        // Вызываем directly KakasiHSceneDialogues for animation switch events анимаций
                        NoREroMod.Systems.Dialogue.KakasiHSceneDialogues.ProcessHSceneEvent(
                            __instance,
                            currentAnim,
                            eventName,
                            0  // se_count not используется for animation switch events
                        );
                    }
                } catch (Exception ex) {
                }
                
                // MindBroken система
                MindBrokenSystem.ProcessAnimationEvent(__instance, currentAnim, eventName);
                if (!string.IsNullOrEmpty(currentAnim) && currentAnim.IndexOf("FIN", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MindBrokenSystem.RegisterClimaxEvent(__instance);
                }

                // Logs disabled
                // Plugin.Log.LogInfo($"[KAKASI] H-anim anim={currentAnim}, event={eventName}, se={__instance.se_count}");
                // Plugin.Log.LogInfo($"[KAKASI PASS] anim={currentAnim}, event={eventName}, se={__instance.se_count}");

                // CRITICAL: TrackCycles must вызываться ДО проверки cycleFinished
                // TrackCycles сам проверяет IsCycleComplete inside себя
                // Pass event in формате, which ожидает BaseEnemyPassPatch (e.ToString())
                instance.TrackCycles(__instance, spine, e, __instance.se_count);
                
                // Проверяем завершение цикла for сброса flags эффектов
                bool cycleFinished = instance.IsCycleComplete(currentAnim, eventName, __instance.se_count);
                if (cycleFinished)
                {
                    startEffectTriggered[__instance] = false;
                    aphroEffectTriggered[__instance] = false;
                    floorEffectTriggered[__instance] = false;
                    finEffectTriggered[__instance] = false;
                }
            }
            catch (Exception ex)
            {
                // Plugin.Log.LogInfo( $"[KAKASI PASS] Error: {ex.Message}");
            }
        }

        static KakasiPassLogic()
        {
            var instance = new KakasiPassLogic();
            SetInstance(instance);
        }

        internal static void ResetAll()
        {
            BaseEnemyPassPatch<kakashi_ero2>.ResetAll();
            startEffectTriggered.Clear();
            aphroEffectTriggered.Clear();
            floorEffectTriggered.Clear();
            finEffectTriggered.Clear();
            previousAnimation.Clear();
            // Reset глобальный флаг enemy type on полном сбросе
            kakasiTypeEffectTriggered = false;
        }

        public static void ExecuteHandoff(object enemyInstance)
        {
            try
            {
                // Plugin.Log.LogInfo("[KAKASI] ===== ExecuteHandoff called - stopping all effects! =====");
                
                // Эффекты camera now handled via Camera Framework
                // REMOVED: KakasiEffects.StopAll и KakasiEffects.ClearZoomTarget
                
                // CRITICAL: Устанавливаем флаг only on передаче in gangbang, а not on первом захвате!
                // This означает that следующий захват того же типа not will иметь slow-mo и zoom
                kakasiTypeEffectTriggered = true;
                // Plugin.Log.LogInfo("[KAKASI] ===== Handoff complete - kakasiTypeEffectTriggered set to TRUE =====");
                
                // Push игрока и отключаем enemy
                PushPlayerAwayFromEnemy(enemyInstance);
                
                // CRITICAL: Get enemy spine for показа phrases передачи
                var kakasiEro = enemyInstance as kakashi_ero2;
                if (kakasiEro != null)
                {
                    // Get spine via reflection
                    var spineField = typeof(kakashi_ero2).GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (spineField != null)
                    {
                        var spine = spineField.GetValue(kakasiEro) as SkeletonAnimation;
                        if (spine != null)
                        {
                            // Stop animation enemy before отключением
                            try
                            {
                                spine.AnimationState?.ClearTracks();
                            }
                            catch (Exception ex)
                            {
                            }
                            
                            // Plugin.Log.LogInfo("[KAKASI] Showing handoff phrase");
                            // REMOVED: Вызоin dialogue system
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Plugin.Log.LogInfo($"[KAKASI] ExecuteHandoff error: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(StruggleSystem), "startGrabInvul")]
        [HarmonyPostfix]
        private static void ClearOnStruggleEscape()
        {
            try
            {
                // Plugin.Log.LogInfo("[KAKASI] === CLEAR ON STRUGGLE ESCAPE ===");
                ClearStateData();
            }
            catch (Exception ex)
            {
            }
        }

        [HarmonyPatch(typeof(playercon), "ImmediatelyERO")]
        [HarmonyPostfix]
        private static void ClearStateOnImmediatelyERO()
        {
            try
            {
                var currentEnemy = UnityEngine.Object.FindObjectOfType<kakashi_ero2>();
                if (currentEnemy == null)
                {
                    return;
                }

                // Plugin.Log.LogInfo("[KAKASI] === CLEAR ON IMMEDIATELYERO (GiveUp) ===");
                ClearStateData();
            }
            catch (Exception ex)
            {
            }
        }

        private static void ClearStateData()
        {
            // Plugin.Log.LogInfo($"[KAKASI CLEAR] Before clear: globalHandoffCount={globalHandoffCount}");

            enemyAnimationCycles.Clear();
            enemySessionStartTime.Clear();
            lastCycleTime.Clear();
            enemyHasPassed.Clear();
            enemyDisabled.Clear();

            // REMOVED: Вызоin dialogue system
            startEffectTriggered.Clear();
            aphroEffectTriggered.Clear();
            floorEffectTriggered.Clear();
            finEffectTriggered.Clear();
            // Эффекты camera now handled via Camera Framework
            // REMOVED: KakasiEffects.StopAll
            
            // CRITICAL: Сбрасываем глобальный флаг enemy type on очистке состояния
            // This означает that следующий захват will with полными эффектами (slow-mo, zoom etc.)
            kakasiTypeEffectTriggered = false;
            
            // Reset state креста
            KakasiCrossPatch.ResetCrossState();

            int oldGlobal = globalHandoffCount;
            globalHandoffCount = 0;
            globalSessionStartTime = 0f;

            // Plugin.Log.LogInfo($"[KAKASI CLEAR] After clear: globalHandoffCount={oldGlobal} -> {globalHandoffCount}, kakasiTypeEffectTriggered=false (NEXT GRAB WILL HAVE FULL EFFECTS!)");
        }

        private static void PushPlayerAwayFromEnemy(object enemyInstance)
        {
            // Plugin.Log.LogInfo("[KAKASI] === Pushing GG away ===");
            try
            {
                enemyDisabled[enemyInstance] = true;

                var playerObject = GameObject.FindWithTag("Player");
                if (playerObject == null)
                {
                    return;
                }

                var playerCon = playerObject.GetComponent<playercon>();
                var playerStatus = playerObject.GetComponent<PlayerStatus>();
                var playerSpine = playerObject.GetComponentInChildren<SkeletonAnimation>();

                if (playerSpine != null)
                {
                    try
                    {
                        playerSpine.AnimationState?.ClearTracks();
                    }
                    catch (Exception ex)
                    {
                    }
                }

                if (playerCon != null)
                {
                    playerCon.eroflag = false;
                    playerCon.erodown = 1;
                    StruggleSystem.setStruggleLevel(-1);

                    // Push ГГ from enemy
                    var enemyTransform = (enemyInstance as MonoBehaviour)?.transform;
                    if (enemyTransform != null)
                    {
                        Vector3 enemyPos = enemyTransform.position;
                        Vector3 playerPos = playerCon.transform.position;
                        Vector3 direction = playerPos - enemyPos;
                        direction.Normalize();
                        
                        // Fix: if enemy is left from ГГ, push right
                        if (direction.x < 0)
                        {
                            direction = Vector3.right;
                        }
                        else
                        {
                            direction = Vector3.left;
                        }
                        
                        float pushDistance = 2f;
                        Vector3 newPosition = playerCon.transform.position + (direction * pushDistance);
                        playerCon.transform.position = newPosition;
                        
                        // Force сбрасываем vertical velocity so that избежать подбрасывания
                        if (playerCon.rigi2d != null)
                        {
                            playerCon.rigi2d.velocity = new Vector2(playerCon.rigi2d.velocity.x, 0f);
                            // Plugin.Log.LogInfo("[KAKASI] Vertical velocity reset to prevent bounce");
                        }
                        
                        // Logs disabled
                        // Plugin.Log.LogInfo($"[KAKASI] GG pushed to: {newPosition}");
                        // Plugin.Log.LogInfo($"[KAKASI] Direction: {direction}");
                    }
                }

                if (playerStatus != null)
                {
                    playerStatus.Sp = 0f;
                }

                var kakasiEro = enemyInstance as kakashi_ero2;
                if (kakasiEro != null)
                {
                    // Get Kakash via reflection
                    global::Kakash enemy = null;
                    if (KakashField != null)
                    {
                        enemy = KakashField.GetValue(kakasiEro) as global::Kakash;
                    }

                    if (enemy != null)
                    {
                        try
                        {
                            var erodataField = typeof(EnemyDate).GetField("erodata", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                            var erodata = erodataField?.GetValue(enemy) as GameObject;
                            if (erodata != null)
                            {
                                erodata.SetActive(false);
                                // Plugin.Log.LogInfo("[KAKASI] erodata disabled");
                            }

                            var eroflagField = typeof(EnemyDate).GetField("eroflag", BindingFlags.NonPublic | BindingFlags.Instance);
                            eroflagField?.SetValue(enemy, false);
                            // Plugin.Log.LogInfo("[KAKASI] enemy eroflag set to false");

                            // FIXED: Отключаем only kakashi_ero2.gameObject, а not enemy.gameObject
                            // enemy.gameObject может быть родительским объектом, which содержит всех enemies типа Kakasi
                            // Мы должны отключать only конкретного enemy, which передал ГГ
                            var enemyMonoBehaviour = kakasiEro as MonoBehaviour;
                            if (enemyMonoBehaviour != null)
                            {
                                var enemyGameObject = enemyMonoBehaviour.gameObject;
                                // Plugin.Log.LogInfo($"[KAKASI] Disabling enemy GameObject: {enemyGameObject.name}");
                                enemyGameObject.SetActive(false);
                            }
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                    else
                    {
                        // If not удалось получить enemy via reflection, отключаем directly kakashi_ero2
                        var enemyMonoBehaviour = kakasiEro as MonoBehaviour;
                        if (enemyMonoBehaviour != null)
                        {
                            var enemyGameObject = enemyMonoBehaviour.gameObject;
                            // Plugin.Log.LogInfo($"[KAKASI] Disabling enemy GameObject (fallback): {enemyGameObject.name}");
                            enemyGameObject.SetActive(false);
                        }
                    }
                }
                else
                {
                    // Fallback: отключаем directly if not kakashi_ero2
                    (enemyInstance as MonoBehaviour)?.gameObject.SetActive(false);
                }
                startEffectTriggered.Remove(enemyInstance);
                aphroEffectTriggered.Remove(enemyInstance);
                floorEffectTriggered.Remove(enemyInstance);
                finEffectTriggered.Remove(enemyInstance);
                previousAnimation.Remove(enemyInstance);
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// Processing phases animation и slow-mo эффектов
        /// </summary>
        private static void HandleAnimationPhases(kakashi_ero2 instance, string animName, int seCount, SkeletonAnimation spine, string eventName, bool animationChanged, string prevAnim)
        {
            string animUpper = animName?.ToUpperInvariant() ?? string.Empty;
            string evtUpper = eventName?.ToUpperInvariant() ?? string.Empty;
            
            // CRITICAL: Если эффект for Kakasi type already был вызван, блокируем only slow-mo и zoom
            // Баннеры и phrases должны показываться всегда!
            bool skipSlowMoAndZoom = kakasiTypeEffectTriggered;

            // START фаза - se_count from 1 until 5
            // ВСЕ events START используют s_kakasi_ero2 (myspine), НЕ крест!
            if (animUpper == "START")
            {
                // Plugin.Log.LogInfo($"[KAKASI] START phase detected! anim={animUpper}, event={evtUpper}, se_count={seCount}, triggered={startEffectTriggered.ContainsKey(instance) && startEffectTriggered[instance]}");
                
                // START se_count==1 - slow-mo 5 сек + зум 5.5x
                if ((evtUpper == "SE" && seCount == 1) || (evtUpper == "START" && seCount == 1) || (evtUpper == "" && seCount == 1))
                {
                    // Plugin.Log.LogInfo($"[KAKASI] ===== START se_count==1: Showing grab phrase! spine={(spine != null ? spine.name : "NULL")} =====");
                    // Фраза показывается always - КРИТИЧНО: передаем spine явно
                    // REMOVED: Вызоin dialogue system
                    
                    // Slow-mo и zoom only if this first time for Kakasi type
                    if (!skipSlowMoAndZoom)
                    {
                        if (!startEffectTriggered.ContainsKey(instance) || !startEffectTriggered[instance])
                        {
                            // Plugin.Log.LogInfo($"[KAKASI] START se_count=1 TRIGGERED! anim={animUpper}, event={evtUpper}, se_count={seCount} - slow-mo 5 sec + zoom 5.5x");
                            if (!startEffectTriggered.ContainsKey(instance))
                                startEffectTriggered[instance] = false;
                            startEffectTriggered[instance] = true;
                            // Эффекты теперь handled via Camera Framework
                            // REMOVED: KakasiEffects.TriggerStartGrabEffect
                        }
                    }
                    else
                    {
                        // Plugin.Log.LogInfo("[KAKASI] START se_count=1: Same enemy type - skipping slow-mo and zoom, but showing phrase");
                    }
                }
                // START se_count==2 - добавить эффект
                else if (evtUpper == "SE" && seCount == 2)
                {
                    // Plugin.Log.LogInfo($"[KAKASI] START se_count=2 TRIGGERED! anim={animUpper}, event={evtUpper}, se_count={seCount}");
                }
                // START se_count==4
                else if (evtUpper == "SE" && seCount == 4)
                {
                    // Plugin.Log.LogInfo($"[KAKASI] START se_count=4 TRIGGERED! anim={animUpper}, event={evtUpper}, se_count={seCount}");
                }
                // START se_count==5 - rapecount(1), Sexcount(1): slow-mo + zoom 5x
                // IMPORTANT: Check se_count ДО того, as он resets in оригинальном коде
                else if (evtUpper == "SE" && seCount == 5)
                {
                    // Plugin.Log.LogInfo($"[KAKASI] ===== START se_count=5 TRIGGERED! ===== anim={animUpper}, event={evtUpper}, se_count={seCount} - rapecount/Sexcount: slow-mo + zoom 5x");
                    
                    // Эффекты теперь handled via Camera Framework
                    // REMOVED: KakasiEffects.TriggerStartSe5Effect
                }
            }
            // START2 фаза (пол) - se_count 1-2
            // Используется s_kakasi_ero2 (обычный Spine Какаси, not крест)
            if (animUpper == "START2" || (animUpper == "JIGO2" && evtUpper == "START2"))
            {
                // START2 se_count==1 - фиксация on полу
                if ((evtUpper == "START2" && seCount == 1) || (evtUpper == "SE" && animUpper == "START2" && seCount == 1))
                {
                    if (!floorEffectTriggered.ContainsKey(instance) || !floorEffectTriggered[instance])
                    {
                        // Only phrasesа фиксации on полу, without slow-mo и zoom
                        // Plugin.Log.LogInfo($"[KAKASI] START2 se_count=1: anim={animUpper}, event={evtUpper}, se_count={seCount} - floor fixation phrase only");
                        if (!floorEffectTriggered.ContainsKey(instance))
                            floorEffectTriggered[instance] = false;
                        floorEffectTriggered[instance] = true;
                        // Фраза фиксации on полу (if exists такой триггер)
                    }
                }
                // START2 se_count==2 - transition to ERO2 (without slow-mo и zoom)
                else if (evtUpper == "SE" && animUpper == "START2" && seCount == 2)
                {
                    // Plugin.Log.LogInfo($"[KAKASI] START2 se_count=2 TRIGGERED! anim={animUpper}, event={evtUpper}, se_count={seCount} - transition to ERO2 (no effects)");
                }
            }
            // Переходы между ERO фазами - slow-mo 2 сек + прогрессивный зум
            // FIXED: Отслеживаем РЕАЛЬНОЕ animation change, а not event
            // Прогрессивный зум: start with 5x/3x и увеличиваем on 0.5x каждый переход
            // Всегда привязаbut к кости R_momo игрока
            // Slow-mo и zoom only if this first time for Kakasi type
            // ERO2 → ERO3: зум 5x, возврат к 3x
            // Переходы между ERO фазами теперь handled via Camera Framework
            // REMOVED: KakasiEffects.TriggerEroTransitionSlowMo for всех переходов
            if (!skipSlowMoAndZoom && animationChanged && prevAnim.Equals("ERO2", StringComparison.OrdinalIgnoreCase) && animUpper == "ERO3")
            {
                // Plugin.Log.LogInfo($"[KAKASI] ERO2 → ERO3 REAL transition detected! prevAnim={prevAnim}, currentAnim={animUpper}");
            }
            // ERO3 → ERO4
            else if (!skipSlowMoAndZoom && animationChanged && prevAnim.Equals("ERO3", StringComparison.OrdinalIgnoreCase) && animUpper == "ERO4")
            {
                // Plugin.Log.LogInfo($"[KAKASI] ERO3 → ERO4 REAL transition detected! prevAnim={prevAnim}, currentAnim={animUpper}");
            }
            // ERO4 → ERO5
            else if (!skipSlowMoAndZoom && animationChanged && prevAnim.Equals("ERO4", StringComparison.OrdinalIgnoreCase) && animUpper == "ERO5")
            {
                // Plugin.Log.LogInfo($"[KAKASI] ERO4 → ERO5 REAL transition detected! prevAnim={prevAnim}, currentAnim={animUpper}");
            }
            // ERO5 → FIN: переход without slow-mo и zoom (only FIN se_count==1 имеет эффекты)
            else if (animationChanged && prevAnim.Equals("ERO5", StringComparison.OrdinalIgnoreCase) && animUpper == "FIN")
            {
                // Plugin.Log.LogInfo($"[KAKASI] ERO5 → FIN REAL transition detected! prevAnim={prevAnim}, currentAnim={animUpper} - transition to FIN (no effects)");
            }
            // FIN = финиш/кончание
            // FIXED: Триггерим on transition to FIN (when анимация меняется on FIN) or on первое event SE in FIN
            // se_count может быть большим on переходе к FIN, therefore we check переход
            // Фраза OnCreampie показывается ВСЕГДА, slow-mo и zoom only if this first time for Kakasi type
            if ((animationChanged && animUpper == "FIN") || (animUpper == "FIN" && evtUpper == "SE" && !finEffectTriggered[instance]))
            {
                if (!finEffectTriggered[instance])
                {
                    // Plugin.Log.LogInfo($"[KAKASI] FIN detected! anim={animUpper}, event={evtUpper}, se_count={seCount}, prevAnim={prevAnim}, changed={animationChanged} - climax trigger!");
                    finEffectTriggered[instance] = true;
                    
                    // Фраза оргазма показывается всегда
                    // REMOVED: Вызоin dialogue system
                    
                    // Эффекты кульминации теперь handled via Camera Framework (OnClimaxEvent)
                    // REMOVED: KakasiEffects.TriggerFinClimaxEffect
                }
            }
        }

        /// <summary>
        /// Get SkeletonAnimation креста (s_kakasiero_spine.spine - дочерний объект spine inside s_kakasiero_spine)
        /// </summary>
        private static SkeletonAnimation GetCrossSpine()
        {
            try
            {
                // FPS: FindObjectOfType<kakashi_ero2> first - O(components), avoid FindObjectsOfType<GameObject> which is O(all objects)
                GameObject crossParent = null;
                var kakasiEro = UnityEngine.Object.FindObjectOfType<kakashi_ero2>();
                if (kakasiEro != null)
                {
                    Transform parent = kakasiEro.transform.parent;
                    if (parent != null)
                    {
                        foreach (Transform child in parent)
                        {
                            if (child.name.Equals("s_kakasiero_spine", StringComparison.OrdinalIgnoreCase) || 
                                child.name.Contains("kakasiero_spine"))
                            {
                                crossParent = child.gameObject;
                                break;
                            }
                        }
                    }
                }
                
                if (crossParent == null)
                {
                    return null;
                }
                
                // Ищем дочерний объект "spine" inside s_kakasiero_spine
                foreach (Transform child in crossParent.transform)
                {
                    if (child.name.Equals("spine", StringComparison.OrdinalIgnoreCase))
                    {
                        var spine = child.GetComponent<SkeletonAnimation>();
                        if (spine != null)
                        {
                            // Plugin.Log.LogInfo($"[KAKASI] Found cross spine child: {child.name} in {crossParent.name}");
                            return spine;
                        }
                    }
                }
                
                // If дочерний объект "spine" not found, пробуем получить SkeletonAnimation directly with родителя
                var parentSpine = crossParent.GetComponent<SkeletonAnimation>();
                if (parentSpine != null)
                {
                    // Plugin.Log.LogInfo($"[KAKASI] Found cross spine directly on parent: {crossParent.name}");
                    return parentSpine;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        // Локальные переменные for cooldown речи Kakasi (вместо KakasiEffects)
        private static readonly System.Collections.Generic.Dictionary<object, float> lastSpeechTime = new();
        private const float SpeechCooldown = 4f; // Cooldown for речи Kakasi
        
        /// <summary>
        /// Display речь Kakasi in комикс-канвасе, привязанном к кости bone24
        /// </summary>
        private static void ShowKakasiSpeech(SkeletonAnimation spine, string currentAnim, int seCount)
        {
            try
            {
                // Проверяем cooldown
                object spineKey = spine;
                if (lastSpeechTime.ContainsKey(spineKey))
                {
                    float timeSinceLast = Time.time - lastSpeechTime[spineKey];
                    if (timeSinceLast < SpeechCooldown)
                    {
                        return; // Cooldown активен
                    }
                }

                string anim = string.IsNullOrEmpty(currentAnim) ? string.Empty : currentAnim.ToUpperInvariant();
                string phrase = null;

                // REMOVED: Весь код dialogue system
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// Display мысль ГГ for Kakasi
        /// </summary>
        private static float lastThoughtTime = 0f;
        private const float ThoughtCooldown = 8f; // Cooldown for мыслей ГГ (as у Mutude)

        private static void ShowKakasiThought(SkeletonAnimation spine, string currentAnim)
        {
            try
            {
                if (!Plugin.enableDirtyTalkMessages?.Value ?? true)
                {
                    return;
                }

                // FIXED: Добавлен cooldown for мыслей ГГ (as у Mutude)
                // Show мысли only раз in 8 секунд, so that избежать спама
                float currentTime = Time.time;
                if (currentTime - lastThoughtTime < ThoughtCooldown)
                {
                    return;
                }

                // REMOVED: Вызоin dialogue system
            }
            catch (Exception ex)
            {
            }
        }
    }

    /// <summary>
    /// Patch for пропуска креста for последующtheir Kakasi in gangbang
    /// Перехватывает момент установки animation "start" on кресте
    /// </summary>
    [HarmonyPatch(typeof(global::Kakash), "OnTriggerStay2D")]
    internal static class KakashGrabPatch
    {
        /// <summary>
        /// Prefix for блокировки оригинального кода for последующtheir Kakasi
        /// </summary>
        [HarmonyPrefix]
        private static bool OnTriggerStay2D_Prefix(global::Kakash __instance, Collider2D collision)
        {
            try
            {
                // Проверяем стандартные условия захвата
                if (__instance.com_player.eroflag || __instance.eroflag || 
                    __instance.state != global::Kakash.enemystate.EROWALK || 
                    __instance.com_player.state != "DOWN" || 
                    collision.gameObject.tag != "playerDAMAGEcol")
                {
                    return true; // Continue original logic
                }

                // CRITICAL: Проверяем shared GlobalHandoffCount for последующtheir enemies in gangbang
                // Проверяем флаг kakasiTypeEffectTriggered (is set in ExecuteHandoff)
                bool kakasiTypeEffectTriggered = KakasiPassLogic.kakasiTypeEffectTriggered;

                // If this subsequent enemy, skip крест и immediately переходим on землю
                if (EnemyHandoffSystem.GlobalHandoffCount > 0 || kakasiTypeEffectTriggered)
                {
                    // Set flagи (as in оригинальном коде)
                    __instance.com_player.eroflag = true;
                    __instance.com_player.rigi2d.velocity = Vector2.zero;
                    __instance.eroflag = true;

                    // DO NOT активируем крест - immediately переходим on землю
                    var erodata2Field = typeof(global::Kakash).GetField("erodata2", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (erodata2Field != null)
                    {
                        var erodata2 = erodata2Field.GetValue(__instance) as GameObject;
                        if (erodata2 != null)
                        {
                            erodata2.SetActive(true);

                            var kakashiEro2 = erodata2.GetComponent<kakashi_ero2>();
                            if (kakashiEro2 != null)
                            {
                                var myspineField = typeof(kakashi_ero2).GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance);
                                if (myspineField != null)
                                {
                                    var myspine = myspineField.GetValue(kakashiEro2) as SkeletonAnimation;
                                    if (myspine != null)
                                    {
                                        myspine.enabled = true;
                                        
                                        // Reset counters
                                        var countField = typeof(kakashi_ero2).GetField("count", BindingFlags.Public | BindingFlags.Instance);
                                        var seCountField = typeof(kakashi_ero2).GetField("se_count", BindingFlags.Public | BindingFlags.Instance);
                                        if (countField != null) countField.SetValue(kakashiEro2, 0);
                                        if (seCountField != null) seCountField.SetValue(kakashiEro2, 0);

                                        // Set START2 immediately (loop=true for continuous animation)
                                        myspine.state.ClearTracks();
                                        var track = myspine.state.SetAnimation(0, "START2", true);
                                        
                                        // Set время animation on 35% (GG already in position)
                                        if (track?.Animation != null)
                                        {
                                            track.Time = track.Animation.Duration * 0.35f;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Вызываем камеру
                    __instance.ero_camera_2();

                    // Return false, so that НЕ выполнять оригинальный код (skip крест)
                    return false;
                }

                // For первого Kakasi продолжаем original logic (with крестом)
                return true;
            }
            catch (Exception ex)
            {
                // In case of error продолжаем original logic
                return true;
            }
        }

        /// <summary>
        /// Postfix for interceptа after установки animation (резервный вариант, if Prefix not сработал)
        /// </summary>
        [HarmonyPostfix]
        private static void OnTriggerStay2D_Postfix(global::Kakash __instance, Collider2D collision)
        {
            try
            {
                // If this захват ГГ (eroflag установлен), проверяем, need to ли переключить on землю
                if (__instance.eroflag && __instance.com_player.eroflag)
                {
                    // CRITICAL: Проверяем shared GlobalHandoffCount for последующtheir enemies in gangbang
                    // Проверяем флаг kakasiTypeEffectTriggered
                    bool kakasiTypeEffectTriggered = KakasiPassLogic.kakasiTypeEffectTriggered;

                    // If this subsequent enemy, переключаем on землю
                    if (EnemyHandoffSystem.GlobalHandoffCount > 0 || kakasiTypeEffectTriggered)
                    {
                        // Use coroutine for delay, so that оригинальный код успел выполниться
                        var runner = __instance.GetComponent<MonoBehaviour>();
                        if (runner != null)
                        {
                            runner.StartCoroutine(SwitchToGroundAfterDelay(runner, __instance));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Игнорируем ошибки in Postfix
            }
        }

        /// <summary>
        /// Coroutine for переключения on землю after delay
        /// </summary>
        private static System.Collections.IEnumerator SwitchToGroundAfterDelay(MonoBehaviour runner, global::Kakash kakash)
        {
            // Wait один кадр, so that оригинальный код успел выполниться
            yield return null;
            
            try
            {
                // Get erokakash и erokakashspine via reflection
                var erokakashField = typeof(global::Kakash).GetField("erokakash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var erokakashspineField = typeof(global::Kakash).GetField("erokakashspine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var erodataField = typeof(global::Kakash).GetField("erodata", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                // Деактивируем крест (if он был активирован)
                if (erokakashField != null)
                {
                    var erokakash = erokakashField.GetValue(kakash) as EroAnimation;
                    if (erokakash != null && erokakash.enabled)
                    {
                        erokakash.enabled = false;
                    }
                }
                
                if (erokakashspineField != null)
                {
                    var erokakashspine = erokakashspineField.GetValue(kakash) as SkeletonAnimation;
                    if (erokakashspine != null && erokakashspine.enabled)
                    {
                        erokakashspine.enabled = false;
                    }
                }
                
                if (erodataField != null)
                {
                    var erodata = erodataField.GetValue(kakash) as GameObject;
                    if (erodata != null && erodata.activeSelf)
                    {
                        erodata.SetActive(false);
                    }
                }

                // Активируем землю (kakashi_ero2) instead of креста
                var erodata2Field = typeof(global::Kakash).GetField("erodata2", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (erodata2Field != null)
                {
                    var erodata2 = erodata2Field.GetValue(kakash) as GameObject;
                    if (erodata2 != null)
                    {
                        erodata2.SetActive(true);

                        var kakashiEro2 = erodata2.GetComponent<kakashi_ero2>();
                        if (kakashiEro2 != null)
                        {
                            var myspineField = typeof(kakashi_ero2).GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (myspineField != null)
                            {
                                var myspine = myspineField.GetValue(kakashiEro2) as SkeletonAnimation;
                                if (myspine != null)
                                {
                                    myspine.enabled = true;
                                    
                                    // Reset counters
                                    var countField = typeof(kakashi_ero2).GetField("count", BindingFlags.Public | BindingFlags.Instance);
                                    var seCountField = typeof(kakashi_ero2).GetField("se_count", BindingFlags.Public | BindingFlags.Instance);
                                    if (countField != null) countField.SetValue(kakashiEro2, 0);
                                    if (seCountField != null) seCountField.SetValue(kakashiEro2, 0);

                                    // Set START2 immediately (loop=true for continuous animation)
                                    myspine.state.ClearTracks();
                                    var track = myspine.state.SetAnimation(0, "START2", true);
                                    
                                    // Set время animation on 35% (GG already in position)
                                    if (track?.Animation != null)
                                    {
                                        track.Time = track.Animation.Duration * 0.35f;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Игнорируем ошибки in корутине
            }
        }
    }
}

