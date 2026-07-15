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

            // JIGO2 = main cycle-completion point (same role as START_JIGO for Mutude)
            // Check both by animation name and by event for reliability
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
        /// Override GetSpineAnimation for Kakasi - ALL animations use s_kakasi_ero2 (myspine)
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

                // After handoff always start with START2 (only 1 variant)
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
                
                // CRITICAL: Check se_count BEFORE increment in the original code
                // If se_count == 4 and animation is START, then after increment it will be 5
                // This moment when we need to trigger effect BEFORE se_count resets
                // Slow-mo and zoom only if this is the first time for Kakasi type
                if (currentAnim.Equals("START", StringComparison.OrdinalIgnoreCase) && __instance.se_count == 4)
                {
                    // Plugin.Log.LogInfo($"[KAKASI] ===== PREFIX: START se_count will be 5! (current={__instance.se_count}) ===== anim={currentAnim}, event={eventName}");
                    
                    // Effects are now handled via Camera Framework
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
                // force switch to "START2" (with correct casing - uppercase letters)
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
                // But we call HandleAnimationPhases AFTER increment, so that se_count is already updated
                // Plugin.Log.LogInfo($"[KAKASI] OnEvent: anim={currentAnim}, event={eventName}, se_count={__instance.se_count}, prevAnim={prevAnim}, changed={animationChanged}");

                // Initialize effect flags
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

                // CRITICAL: If this is a new grab (START se_count==1), check whether effects already ran for this enemy type
                // FIXED: If the same enemy type picks up the player again in the gangbang cycle, effects should not trigger
                if (currentAnim.Equals("START", StringComparison.OrdinalIgnoreCase) && 
                    eventName.Equals("SE", StringComparison.OrdinalIgnoreCase) && 
                    __instance.se_count == 1)
                {
                    // Check whether effect was already for Kakasi type in current gangbang cycle
                    if (kakasiTypeEffectTriggered)
                    {
                        // Plugin.Log.LogInfo("[KAKASI] ===== Same enemy type (Kakasi) picking up player again - skipping slow-mo and zoom effects! =====");
                        
                        // Camera effects are now handled via Camera Framework
                        // REMOVED: KakasiEffects.ClearZoomTarget
                        
                        // CRITICAL: Also clear cross state so cross effects do not trigger on the second cycle
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
                        
                        // Do not reset flags - this is a repeat grab of the same type
                        // But continue processing for banners and phrases
                    }
                    else
                    {
                        // Plugin.Log.LogInfo("[KAKASI] ===== New grab detected (START se_count==1) - first time for Kakasi type, triggering effects! =====");
                        // Effects are now handled via Camera Framework
                        // REMOVED: KakasiEffects.StopAll
                        
                        // Reset all effect flags for this instance
                        if (startEffectTriggered.ContainsKey(__instance))
                            startEffectTriggered[__instance] = false;
                        if (floorEffectTriggered.ContainsKey(__instance))
                            floorEffectTriggered[__instance] = false;
                        if (finEffectTriggered.ContainsKey(__instance))
                            finEffectTriggered[__instance] = false;
                        
                        // Reset previous-animation tracking
                        if (previousAnimation.ContainsKey(__instance))
                            previousAnimation.Remove(__instance);
                    }
                }

                // Process animation phases and slow-mo effects
                HandleAnimationPhases(__instance, currentAnim, __instance.se_count, spine, eventName, animationChanged, prevAnim);

                // Show Kakasi speech in the comic canvas (bound to bone24)
                ShowKakasiSpeech(spine, currentAnim, __instance.se_count);

                // Show GG thoughts
                ShowKakasiThought(spine, currentAnim);

                // Processing dialogue system events
                // Process both SE events and animation-switch events (ERO2, ERO3, ERO4, ERO5, FIN, JIGO1, JIGO2, START2)
                try {
                    // For SE events use standard processing
                    if (eventName.Equals("SE", StringComparison.OrdinalIgnoreCase))
                    {
                        NoREroMod.Systems.Dialogue.DialogueFramework.ProcessAnimationEvent(
                            __instance, 
                            currentAnim, 
                            eventName, 
                            __instance.se_count
                        );
                    }
                    // For animation-switch events call KakasiHSceneDialogues directly
                    else if (eventName.Equals("ERO2", StringComparison.OrdinalIgnoreCase) ||
                             eventName.Equals("ERO3", StringComparison.OrdinalIgnoreCase) ||
                             eventName.Equals("ERO4", StringComparison.OrdinalIgnoreCase) ||
                             eventName.Equals("ERO5", StringComparison.OrdinalIgnoreCase) ||
                             eventName.Equals("FIN", StringComparison.OrdinalIgnoreCase) ||
                             eventName.Equals("JIGO1", StringComparison.OrdinalIgnoreCase) ||
                             eventName.Equals("JIGO2", StringComparison.OrdinalIgnoreCase) ||
                             eventName.Equals("START2", StringComparison.OrdinalIgnoreCase))
                    {
                        // Call KakasiHSceneDialogues directly for animation-switch events
                        NoREroMod.Systems.Dialogue.KakasiHSceneDialogues.ProcessHSceneEvent(
                            __instance,
                            currentAnim,
                            eventName,
                            0  // se_count is not used for animation-switch events
                        );
                    }
                } catch (Exception ex) {
                }
                
                // MindBroken system
                MindBrokenSystem.ProcessAnimationEvent(__instance, currentAnim, eventName);
                if (!string.IsNullOrEmpty(currentAnim) && currentAnim.IndexOf("FIN", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MindBrokenSystem.RegisterClimaxEvent(__instance);
                }

                // Logs disabled
                // Plugin.Log.LogInfo($"[KAKASI] H-anim anim={currentAnim}, event={eventName}, se={__instance.se_count}");
                // Plugin.Log.LogInfo($"[KAKASI PASS] anim={currentAnim}, event={eventName}, se={__instance.se_count}");

                // CRITICAL: TrackCycles must be called BEFORE checking cycleFinished
                // TrackCycles itself checks IsCycleComplete inside
                // Pass event in the format expected by BaseEnemyPassPatch (e.ToString())
                instance.TrackCycles(__instance, spine, e, __instance.se_count);
                
                // Check cycle completion to reset effect flags
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
            // Reset global enemy-type flag on full reset
            kakasiTypeEffectTriggered = false;
        }

        public static void ExecuteHandoff(object enemyInstance)
        {
            try
            {
                // Plugin.Log.LogInfo("[KAKASI] ===== ExecuteHandoff called - stopping all effects! =====");
                
                // Camera effects are now handled via Camera Framework
                // REMOVED: KakasiEffects.StopAll and KakasiEffects.ClearZoomTarget
                
                // CRITICAL: Set the flag only on gangbang handoff, not on the first grab!
                // This means the next grab of the same type will not have slow-mo and zoom
                kakasiTypeEffectTriggered = true;
                // Plugin.Log.LogInfo("[KAKASI] ===== Handoff complete - kakasiTypeEffectTriggered set to TRUE =====");
                
                // Push the player away and disable the enemy
                PushPlayerAwayFromEnemy(enemyInstance);
                
                // CRITICAL: Get enemy spine for showing handoff phrases
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
                            // Stop enemy animation before disabling
                            try
                            {
                                spine.AnimationState?.ClearTracks();
                            }
                            catch (Exception ex)
                            {
                            }
                            
                            // Plugin.Log.LogInfo("[KAKASI] Showing handoff phrase");
                            // REMOVED: dialogue system call
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

            // REMOVED: dialogue system call
            startEffectTriggered.Clear();
            aphroEffectTriggered.Clear();
            floorEffectTriggered.Clear();
            finEffectTriggered.Clear();
            // Camera effects are now handled via Camera Framework
            // REMOVED: KakasiEffects.StopAll
            
            // CRITICAL: Reset global enemy-type flag when clearing state
            // This means the next grab will have full effects (slow-mo, zoom, etc.)
            KakasiHandoffHide.RestoreAll();
            kakasiTypeEffectTriggered = false;
            
            // Reset cross state
            KakasiCrossPatch.ResetCrossState();

            int oldGlobal = globalHandoffCount;
            globalHandoffCount = 0;
            globalSessionStartTime = 0f;

            // Plugin.Log.LogInfo($"[KAKASI CLEAR] After clear: globalHandoffCount={oldGlobal} -> {globalHandoffCount}, kakasiTypeEffectTriggered=false (NEXT GRAB WILL HAVE FULL EFFECTS!)");
        }

        private static void PushPlayerAwayFromEnemy(object enemyInstance)
        {
            try
            {
                enemyDisabled[enemyInstance] = true;

                GameObject playerObject = UnifiedPlayerCacheManager.GetPlayerObject();
                if (playerObject == null)
                    return;

                var playerCon = playerObject.GetComponent<playercon>();
                var playerStatus = playerObject.GetComponent<PlayerStatus>();
                var kakasiEro = enemyInstance as kakashi_ero2;
                Transform enemyTransform = (enemyInstance as MonoBehaviour)?.transform;

                if (kakasiEro != null)
                {
                    try
                    {
                        SkeletonAnimation eroSpine = GetSpineAnimation(kakasiEro);
                        eroSpine?.AnimationState?.ClearTracks();
                    }
                    catch
                    {
                    }

                    global::Kakash kakashOwner = KakashField?.GetValue(kakasiEro) as global::Kakash;
                    if (kakashOwner != null)
                    {
                        var erodataField = typeof(EnemyDate).GetField("erodata", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                        GameObject erodataCross = erodataField?.GetValue(kakashOwner) as GameObject;
                        KakasiHandoffHide.HideAfterHandoff(kakashOwner, erodataCross, kakasiEro.gameObject);
                    }
                    else
                    {
                        kakasiEro.gameObject.SetActive(false);
                    }
                }
                else
                {
                    (enemyInstance as MonoBehaviour)?.gameObject.SetActive(false);
                }

                if (playerCon != null)
                    EnemyHandoffPlayerHelper.ApplyStandardHandoffState(playerCon, playerStatus, enemyTransform);

                startEffectTriggered.Remove(enemyInstance);
                aphroEffectTriggered.Remove(enemyInstance);
                floorEffectTriggered.Remove(enemyInstance);
                finEffectTriggered.Remove(enemyInstance);
                previousAnimation.Remove(enemyInstance);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Process animation phases and slow-mo effects
        /// </summary>
        private static void HandleAnimationPhases(kakashi_ero2 instance, string animName, int seCount, SkeletonAnimation spine, string eventName, bool animationChanged, string prevAnim)
        {
            string animUpper = animName?.ToUpperInvariant() ?? string.Empty;
            string evtUpper = eventName?.ToUpperInvariant() ?? string.Empty;
            
            // CRITICAL: If the effect for Kakasi type was already triggered, block only slow-mo and zoom
            // Banners and phrases must still be shown every time!
            bool skipSlowMoAndZoom = kakasiTypeEffectTriggered;

            // START phase - se_count from 1 until 5
            // ALL START events use s_kakasi_ero2 (myspine), NOT the cross!
            if (animUpper == "START")
            {
                // Plugin.Log.LogInfo($"[KAKASI] START phase detected! anim={animUpper}, event={evtUpper}, se_count={seCount}, triggered={startEffectTriggered.ContainsKey(instance) && startEffectTriggered[instance]}");
                
                // START se_count==1 - slow-mo 5 sec + zoom 5.5x
                if ((evtUpper == "SE" && seCount == 1) || (evtUpper == "START" && seCount == 1) || (evtUpper == "" && seCount == 1))
                {
                    // Plugin.Log.LogInfo($"[KAKASI] ===== START se_count==1: Showing grab phrase! spine={(spine != null ? spine.name : "NULL")} =====");
                    // Phrase is always shown - CRITICAL: pass spine explicitly
                    // REMOVED: dialogue system call
                    
                    // Slow-mo and zoom only if this is the first time for Kakasi type
                    if (!skipSlowMoAndZoom)
                    {
                        if (!startEffectTriggered.ContainsKey(instance) || !startEffectTriggered[instance])
                        {
                            // Plugin.Log.LogInfo($"[KAKASI] START se_count=1 TRIGGERED! anim={animUpper}, event={evtUpper}, se_count={seCount} - slow-mo 5 sec + zoom 5.5x");
                            if (!startEffectTriggered.ContainsKey(instance))
                                startEffectTriggered[instance] = false;
                            startEffectTriggered[instance] = true;
                            // Effects are now handled via Camera Framework
                            // REMOVED: KakasiEffects.TriggerStartGrabEffect
                        }
                    }
                    else
                    {
                        // Plugin.Log.LogInfo("[KAKASI] START se_count=1: Same enemy type - skipping slow-mo and zoom, but showing phrase");
                    }
                }
                // START se_count==2 - add effect
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
                // IMPORTANT: Check se_count BEFORE it resets in the original code
                else if (evtUpper == "SE" && seCount == 5)
                {
                    // Plugin.Log.LogInfo($"[KAKASI] ===== START se_count=5 TRIGGERED! ===== anim={animUpper}, event={evtUpper}, se_count={seCount} - rapecount/Sexcount: slow-mo + zoom 5x");
                    
                    // Effects are now handled via Camera Framework
                    // REMOVED: KakasiEffects.TriggerStartSe5Effect
                }
            }
            // START2 phase (floor) - se_count 1-2
            // Uses s_kakasi_ero2 (regular Kakasi Spine, not the cross)
            if (animUpper == "START2" || (animUpper == "JIGO2" && evtUpper == "START2"))
            {
                // START2 se_count==1 - floor fixation
                if ((evtUpper == "START2" && seCount == 1) || (evtUpper == "SE" && animUpper == "START2" && seCount == 1))
                {
                    if (!floorEffectTriggered.ContainsKey(instance) || !floorEffectTriggered[instance])
                    {
                        // Only floor-fixation phrase, without slow-mo and zoom
                        // Plugin.Log.LogInfo($"[KAKASI] START2 se_count=1: anim={animUpper}, event={evtUpper}, se_count={seCount} - floor fixation phrase only");
                        if (!floorEffectTriggered.ContainsKey(instance))
                            floorEffectTriggered[instance] = false;
                        floorEffectTriggered[instance] = true;
                        // Floor-fixation phrase (if such a trigger exists)
                    }
                }
                // START2 se_count==2 - transition to ERO2 (without slow-mo and zoom)
                else if (evtUpper == "SE" && animUpper == "START2" && seCount == 2)
                {
                    // Plugin.Log.LogInfo($"[KAKASI] START2 se_count=2 TRIGGERED! anim={animUpper}, event={evtUpper}, se_count={seCount} - transition to ERO2 (no effects)");
                }
            }
            // Transitions between ERO phases - slow-mo 2 sec + progressive zoom
            // FIXED: Track the REAL animation change, not the event
            // Progressive zoom: start at 5x/3x and increase by 0.5x each transition
            // Always bound to the player's R_momo bone
            // Slow-mo and zoom only if this is the first time for Kakasi type
            // ERO2 -> ERO3: zoom 5x, return to 3x
            // Transitions between ERO phases are now handled via Camera Framework
            // REMOVED: KakasiEffects.TriggerEroTransitionSlowMo for all transitions
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
            // ERO5 -> FIN: transition without slow-mo and zoom (only FIN se_count==1 has effects)
            else if (animationChanged && prevAnim.Equals("ERO5", StringComparison.OrdinalIgnoreCase) && animUpper == "FIN")
            {
                // Plugin.Log.LogInfo($"[KAKASI] ERO5 → FIN REAL transition detected! prevAnim={prevAnim}, currentAnim={animUpper} - transition to FIN (no effects)");
            }
            // FIN = finish/climax
            // FIXED: Trigger on transition to FIN (when animation changes to FIN) or on the first SE event in FIN
            // se_count may be large on the transition to FIN, so we check the transition
            // OnCreampie phrase is ALWAYS shown; slow-mo and zoom only if first time for Kakasi type
            if ((animationChanged && animUpper == "FIN") || (animUpper == "FIN" && evtUpper == "SE" && !finEffectTriggered[instance]))
            {
                if (!finEffectTriggered[instance])
                {
                    // Plugin.Log.LogInfo($"[KAKASI] FIN detected! anim={animUpper}, event={evtUpper}, se_count={seCount}, prevAnim={prevAnim}, changed={animationChanged} - climax trigger!");
                    finEffectTriggered[instance] = true;
                    
                    // Orgasm phrase is always shown
                    // REMOVED: dialogue system call
                    
                    // Climax effects are now handled via Camera Framework (OnClimaxEvent)
                    // REMOVED: KakasiEffects.TriggerFinClimaxEffect
                }
            }
        }

        /// <summary>
        /// Get cross SkeletonAnimation (s_kakasiero_spine.spine - child "spine" object inside s_kakasiero_spine)
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
                
                // Look for child object "spine" inside s_kakasiero_spine
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
                
                // If child object "spine" not found, try getting SkeletonAnimation directly from the parent
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

        // Local variables for Kakasi speech cooldown (instead of KakasiEffects)
        private static readonly System.Collections.Generic.Dictionary<object, float> lastSpeechTime = new();
        private const float SpeechCooldown = 4f; // Cooldown for Kakasi speech
        
        /// <summary>
        /// Display Kakasi speech in the comic canvas bound to bone24
        /// </summary>
        private static void ShowKakasiSpeech(SkeletonAnimation spine, string currentAnim, int seCount)
        {
            try
            {
                // Check cooldown
                object spineKey = spine;
                if (lastSpeechTime.ContainsKey(spineKey))
                {
                    float timeSinceLast = Time.time - lastSpeechTime[spineKey];
                    if (timeSinceLast < SpeechCooldown)
                    {
                        return; // Cooldown active
                    }
                }

                string anim = string.IsNullOrEmpty(currentAnim) ? string.Empty : currentAnim.ToUpperInvariant();
                string phrase = null;

                // REMOVED: entire dialogue system code
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// Display player thought for Kakasi
        /// </summary>
        private static float lastThoughtTime = 0f;
        private const float ThoughtCooldown = 8f; // Cooldown for player thoughts (same as Mutude)

        private static void ShowKakasiThought(SkeletonAnimation spine, string currentAnim)
        {
            try
            {
                if (!Plugin.enableDirtyTalkMessages?.Value ?? true)
                {
                    return;
                }

                // FIXED: Added cooldown for player thoughts (same as Mutude)
                // Show thoughts only once every 8 seconds to avoid spam
                float currentTime = Time.time;
                if (currentTime - lastThoughtTime < ThoughtCooldown)
                {
                    return;
                }

                // REMOVED: dialogue system call
            }
            catch (Exception ex)
            {
            }
        }
    }

    /// <summary>
    /// Patch to skip the cross for subsequent Kakasi in gangbang
    /// Intercepts the moment animation "start" is set on the cross
    /// </summary>
    [HarmonyPatch(typeof(global::Kakash), "OnTriggerStay2D")]
    internal static class KakashGrabPatch
    {
        /// <summary>
        /// Prefix to block the original code for subsequent Kakasi
        /// </summary>
        [HarmonyPrefix]
        private static bool OnTriggerStay2D_Prefix(global::Kakash __instance, Collider2D collision)
        {
            try
            {
                // Check standard grab conditions
                if (__instance.com_player.eroflag || __instance.eroflag || 
                    __instance.state != global::Kakash.enemystate.EROWALK || 
                    __instance.com_player.state != "DOWN" || 
                    collision.gameObject.tag != "playerDAMAGEcol")
                {
                    return true; // Continue original logic
                }

                // CRITICAL: Check shared GlobalHandoffCount for subsequent enemies in gangbang
                // Check kakasiTypeEffectTriggered flag (set in ExecuteHandoff)
                bool kakasiTypeEffectTriggered = KakasiPassLogic.kakasiTypeEffectTriggered;

                // If this is a subsequent enemy, skip the cross and go straight to ground
                if (EnemyHandoffSystem.GlobalHandoffCount > 0 || kakasiTypeEffectTriggered)
                {
                    // Set flags (as in the original code)
                    __instance.com_player.eroflag = true;
                    __instance.com_player.rigi2d.velocity = Vector2.zero;
                    __instance.eroflag = true;

                    // DO NOT activate the cross - go straight to ground
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
                                        
                                        // Set animation time to 35% (player already in position)
                                        if (track?.Animation != null)
                                        {
                                            track.Time = track.Animation.Duration * 0.35f;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Call the camera
                    __instance.ero_camera_2();

                    // Return false so the original code does NOT run (skip the cross)
                    return false;
                }

                // For the first Kakasi continue original logic (with the cross)
                return true;
            }
            catch (Exception ex)
            {
                // On error continue original logic
                return true;
            }
        }

        /// <summary>
        /// Postfix as a fallback after animation is set (if Prefix did not run)
        /// </summary>
        [HarmonyPostfix]
        private static void OnTriggerStay2D_Postfix(global::Kakash __instance, Collider2D collision)
        {
            try
            {
                // If this is a player grab (eroflag set), check whether to switch to ground
                if (__instance.eroflag && __instance.com_player.eroflag)
                {
                    // CRITICAL: Check shared GlobalHandoffCount for subsequent enemies in gangbang
                    // Check kakasiTypeEffectTriggered flag
                    bool kakasiTypeEffectTriggered = KakasiPassLogic.kakasiTypeEffectTriggered;

                    // If this is a subsequent enemy, switch to ground
                    if (EnemyHandoffSystem.GlobalHandoffCount > 0 || kakasiTypeEffectTriggered)
                    {
                        // Use coroutine delay so the original code has time to run
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
                // Ignore errors in Postfix
            }
        }

        /// <summary>
        /// Coroutine to switch to ground after a delay
        /// </summary>
        private static System.Collections.IEnumerator SwitchToGroundAfterDelay(MonoBehaviour runner, global::Kakash kakash)
        {
            // Wait one frame so the original code has time to run
            yield return null;
            
            try
            {
                // Get erokakash and erokakashspine via reflection
                var erokakashField = typeof(global::Kakash).GetField("erokakash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var erokakashspineField = typeof(global::Kakash).GetField("erokakashspine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var erodataField = typeof(global::Kakash).GetField("erodata", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                // Deactivate the cross (if it was activated)
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

                // Activate ground (kakashi_ero2) instead of the cross
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
                                    
                                    // Set animation time to 35% (player already in position)
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
                // Ignore errors in the coroutine
            }
        }
    }
}

