using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Spine;
using Spine.Unity;
using NoREroMod.Patches.Enemy.Kakash;
using NoREroMod.Patches.Enemy.Base;

namespace NoREroMod.Patches.Enemy.Kakash
{
    /// <summary>
    /// Patch for handling cross events (s_kakasiero_spine via EroAnimation)
    /// </summary>
    [HarmonyPatch(typeof(global::EroAnimation), "OnEvent")]
    internal static class KakasiCrossPatch
    {
        private static bool crossGrabTriggered = false;
        private static FieldInfo kakashField;
        private static FieldInfo mySpineField;
        private static FieldInfo myspine2Field;
        private static FieldInfo ero2Field;
        private static float lastCrossThoughtTime = 0f;
        private const float CrossThoughtCooldown = 8f; // Cooldown for player thoughts on the cross (8 seconds, same as on ground)

        static KakasiCrossPatch()
        {
            // Get fields via reflection
            kakashField = typeof(global::EroAnimation).GetField("kakash", BindingFlags.NonPublic | BindingFlags.Instance);
            mySpineField = typeof(global::EroAnimation).GetField("mySpine", BindingFlags.NonPublic | BindingFlags.Instance);
            myspine2Field = typeof(global::EroAnimation).GetField("myspine2", BindingFlags.NonPublic | BindingFlags.Instance);
            ero2Field = typeof(global::EroAnimation).GetField("ero2", BindingFlags.Public | BindingFlags.Instance);
            
            if (kakashField == null)
            {
            }
            if (mySpineField == null)
            {
            }
            if (myspine2Field == null)
            {
            }
            if (ero2Field == null)
            {
            }
        }

        internal static bool IsKakasiCross(global::EroAnimation instance)
        {
            // Check if this is the Kakasi cross via the kakash field
            if (kakashField == null) return false;
            var kakash = kakashField.GetValue(instance) as global::Kakash;
            return kakash != null;
        }

        [HarmonyPrefix]
        private static void OnEvent_Prefix(global::EroAnimation __instance, Spine.AnimationState state, int trackIndex, Spine.Event e, ref int ___count)
        {
            try
            {
                // Check if this is the Kakasi cross
                if (!IsKakasiCross(__instance))
                {
                    return;
                }

                // Get mySpine via reflection
                if (mySpineField == null) return;
                var mySpine = mySpineField.GetValue(__instance) as SkeletonAnimation;
                if (mySpine == null) return;

                string eventName = e.Data.Name;
                string animName = mySpine.AnimationName ?? string.Empty;
                string animNameUpper = animName.ToUpperInvariant();
                string eventNameUpper = eventName.ToUpperInvariant();

                // Logs disabled
                // Plugin.Log.LogInfo($"[KAKASI CROSS] ===== EroAnimation event: {eventName}, anim: {animName}, count: {___count} =====");

                // Process SE event for all cross animations
                if (eventNameUpper.Equals("SE", StringComparison.OrdinalIgnoreCase))
                {
                    // Process dialogue system events for all cross animations
                    try {
                        NoREroMod.Systems.Dialogue.DialogueFramework.ProcessAnimationEvent(
                            __instance, 
                            animName, 
                            eventName, 
                            ___count
                        );
                    } catch (Exception ex) {
                    }
                    
                    // Special handling for start (SE, count == 0) - first grab event
                    if (animNameUpper.Equals("START", StringComparison.OrdinalIgnoreCase) && 
                        ___count == 0 && 
                        !crossGrabTriggered)
                    {
                        crossGrabTriggered = true;
                        
                    // Effects are now handled via Camera Framework
                    // REMOVED: KakasiEffects.TriggerCrossGrabEffect
                    }
                }

                // Process animation-switch events (next1-next7, COUNT, END)
                // These events switch animation, so treat them as switch events
                bool isAnimationSwitchEvent = eventNameUpper == "NEXT1" || eventNameUpper == "NEXT2" || 
                                             eventNameUpper == "NEXT3" || eventNameUpper == "NEXT4" || 
                                             eventNameUpper == "NEXT5" || eventNameUpper == "NEXT6" || 
                                             eventNameUpper == "NEXT7" || eventNameUpper == "COUNT" || 
                                             eventNameUpper == "END";

                if (isAnimationSwitchEvent)
                {
                    // Determine target animation based on the event
                    string targetAnim = animName; // Default to current animation
                    
                    switch (eventNameUpper)
                    {
                        case "NEXT1":
                            targetAnim = "start2";
                            break;
                        case "NEXT2":
                            targetAnim = "ero1";
                            break;
                        case "NEXT3":
                            targetAnim = "ero2";
                            break;
                        case "NEXT4":
                            targetAnim = "ero3";
                            break;
                        case "NEXT5":
                            targetAnim = "finish1";
                            break;
                        case "NEXT6":
                            targetAnim = "finish2";
                            break;
                        case "NEXT7":
                            targetAnim = "finish_end";
                            break;
                        case "COUNT":
                            // COUNT switches to finish_end2 only if current animation is finish_end and count >= 1
                            if (animNameUpper.Equals("FINISH_END", StringComparison.OrdinalIgnoreCase) && ___count >= 1)
                            {
                                targetAnim = "finish_end2";
                            }
                            break;
                        case "END":
                            // END switches to ground (START in kakashi_ero2), but that is handled in the original code
                            // Here we can process the event before the transition
                            if (animNameUpper.Equals("FINISH_END2", StringComparison.OrdinalIgnoreCase))
                            {
                                // Process events before transitioning to ground
                                try {
                                    NoREroMod.Systems.Dialogue.DialogueFramework.ProcessAnimationEvent(
                                        __instance, 
                                        "finish_end2", 
                                        eventName, 
                                        ___count
                                    );
                                } catch (Exception ex) {
                                }
                            }
                            break;
                    }
                    
                    // For next5 use a coroutine to process after the animation switches
                    if (eventNameUpper == "NEXT5")
                    {
                        var runner = __instance.GetComponent<MonoBehaviour>();
                        if (runner != null)
                        {
                            runner.StartCoroutine(DelayedFinish1Effect(runner, mySpine));
                        }
                    }
                    else if (!string.IsNullOrEmpty(targetAnim) && targetAnim != animName)
                    {
                        // For other switch events process immediately with the target animation
                        // Use coroutine for processing after the switch
                        var runner = __instance.GetComponent<MonoBehaviour>();
                        if (runner != null)
                        {
                            runner.StartCoroutine(DelayedAnimationSwitchEffect(runner, mySpine, targetAnim, eventName));
                        }
                    }
                }

                // Show player thoughts on the cross (same as Kakasi on ground)
                ShowCrossPlayerThought(mySpine, animName);
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// Postfix for END events - intercept the transition from cross to ground
        /// and set START2 instead of START for subsequent Kakasi in gangbang
        /// </summary>
        [HarmonyPostfix]
        private static void OnEvent_Postfix_END(global::EroAnimation __instance, Spine.AnimationState state, int trackIndex, Spine.Event e)
        {
            try
            {
                // Check if this is an END event and this is the Kakasi cross
                if (!IsKakasiCross(__instance))
                {
                    return;
                }

                string eventName = e?.Data?.Name ?? string.Empty;
                if (!eventName.Equals("END", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // Check if this is the transition from cross to ground (finish_end2)
                if (mySpineField == null) return;
                var mySpine = mySpineField.GetValue(__instance) as SkeletonAnimation;
                if (mySpine == null) return;

                string animName = mySpine.AnimationName ?? string.Empty;
                if (!animName.Equals("finish_end2", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // Get myspine2 (SkeletonAnimation for kakashi_ero2) via reflection
                if (myspine2Field == null) return;
                var myspine2 = myspine2Field.GetValue(__instance) as SkeletonAnimation;
                if (myspine2 == null) return;

                // CRITICAL: Check shared GlobalHandoffCount for subsequent enemies in gangbang
                // If this is a subsequent enemy, set START2 instead of START
                if (EnemyHandoffSystem.GlobalHandoffCount > 0)
                {
                    // Plugin.Log.LogInfo($"[KAKASI CROSS] END event detected, globalHandoffCount={globalHandoffCount}, forcing START -> START2");
                    
                    // Original code already set "START", but we overwrite it to "START2"
                    // Use coroutine delay so the original code has time to finish
                    var runner = __instance.GetComponent<MonoBehaviour>();
                    if (runner != null)
                    {
                        runner.StartCoroutine(DelayedForceSTART2(runner, myspine2));
                    }
                }
            }
            catch (Exception ex)
            {
                // Plugin.Log.LogInfo($"[KAKASI CROSS] OnEvent_Postfix_END error: {ex.Message}");
            }
        }

        /// <summary>
        /// Coroutine to force-set START2 after the transition from cross to ground
        /// </summary>
        private static System.Collections.IEnumerator DelayedForceSTART2(MonoBehaviour runner, SkeletonAnimation myspine2)
        {
            // Wait one frame so the original code has time to set START
            yield return null;
            
            if (myspine2 != null && myspine2.AnimationState != null)
            {
                string currentAnim = myspine2.AnimationName ?? string.Empty;
                
                // If animation is still "START" (any case), overwrite to "START2"
                if (currentAnim.Equals("START", StringComparison.OrdinalIgnoreCase) || 
                    currentAnim.Equals("start", StringComparison.OrdinalIgnoreCase))
                {
                    // Plugin.Log.LogInfo($"[KAKASI CROSS] Forcing START -> START2 (current={currentAnim})");
                    
                    myspine2.AnimationState.ClearTracks();
                    var track = myspine2.AnimationState.SetAnimation(0, "START2", true); // loop=true
                    if (track?.Animation != null)
                    {
                        // Start from middle of animation (GG already in position)
                        track.Time = track.Animation.Duration * 0.35f;
                    }
                }
            }
        }
        
        /// <summary>
        /// Process events after animation switch (for next1-next7, COUNT)
        /// </summary>
        private static System.Collections.IEnumerator DelayedAnimationSwitchEffect(MonoBehaviour runner, SkeletonAnimation spine, string targetAnim, string eventName)
        {
            // Wait one frame so the animation has time to switch
            yield return null;
            
            if (spine != null && spine.AnimationName != null)
            {
                string currentAnim = spine.AnimationName;
                string currentAnimLower = currentAnim.ToLowerInvariant();
                string targetAnimLower = targetAnim.ToLowerInvariant();
                
                // Check that the animation actually switched
                if (currentAnimLower.Equals(targetAnimLower, StringComparison.OrdinalIgnoreCase))
                {
                    // Process dialogue system events for the switched animation
                    // Use animation name as event for searching phrases in JSON
                    try {
                        // Call ProcessAnimationEvent with event = animation name (for the cross, JSON events are named like animations)
                        NoREroMod.Systems.Dialogue.DialogueFramework.ProcessAnimationEvent(
                            runner, 
                            currentAnim, 
                            currentAnim,  // Use animation name as event for JSON lookup
                            0
                        );
                    } catch (Exception ex) {
                    }
                }
            }
        }

        private static System.Collections.IEnumerator DelayedFinish1Effect(MonoBehaviour runner, SkeletonAnimation spine)
        {
            // Wait one frame so the animation has time to switch to finish1
            yield return null;
            
            if (spine != null && spine.AnimationName != null && spine.AnimationName.Equals("finish1", StringComparison.OrdinalIgnoreCase))
            {
                // Plugin.Log.LogInfo("[KAKASI CROSS] ===== Cross 'finish1' animation confirmed! =====");
                
                // CRITICAL: RED finish phrase is ALWAYS shown (use ShowCrossPhrase for red color)
                // REMOVED: dialogue system call
                
                // Effects are now handled via Camera Framework
                // REMOVED: KakasiEffects.TriggerCrossFinish2Effect
            }
        }

        /// <summary>
        /// Display player thoughts on the cross (same as Kakasi on ground)
        /// </summary>
        private static void ShowCrossPlayerThought(SkeletonAnimation spine, string currentAnim)
        {
            try
            {
                // Check cooldown for player thoughts on the cross
                float currentTime = Time.time;
                if (currentTime - lastCrossThoughtTime < CrossThoughtCooldown)
                {
                    return; // Cooldown active
                }

                // REMOVED: dialogue system call
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// Reset flag when clearing state
        /// </summary>
        internal static void ResetCrossState()
        {
            crossGrabTriggered = false;
            lastCrossThoughtTime = 0f;
            // Plugin.Log.LogInfo("[KAKASI CROSS] Cross state reset");
        }

        /// <summary>
        /// Patch for EroAnimation.Start - intercept cross activation for subsequent Kakasi
        /// </summary>
        [HarmonyPatch(typeof(global::EroAnimation), "Start")]
        internal static class EroAnimationStartPatch
        {
            [HarmonyPostfix]
            private static void Start_Postfix(global::EroAnimation __instance)
            {
                try
                {
                    // Check if this is the Kakasi cross
                    if (!IsKakasiCross(__instance))
                    {
                        return;
                    }

                    // CRITICAL: Check shared GlobalHandoffCount for subsequent enemies in gangbang
                    // If this is a subsequent enemy, go straight to ground
                    if (EnemyHandoffSystem.GlobalHandoffCount > 0)
                    {
                        // Plugin.Log.LogInfo($"[KAKASI CROSS START] Subsequent Kakasi detected (globalHandoffCount={globalHandoffCount}), skipping cross, going directly to ground START2");

                        // Deactivate the cross
                        __instance.enabled = false;
                        if (mySpineField != null)
                        {
                            var mySpine = mySpineField.GetValue(__instance) as SkeletonAnimation;
                            if (mySpine != null)
                            {
                                mySpine.enabled = false;
                            }
                        }

                        // Activate ground (kakashi_ero2)
                        if (ero2Field != null)
                        {
                            var ero2 = ero2Field.GetValue(__instance) as GameObject;
                            if (ero2 != null)
                            {
                                ero2.SetActive(true);

                                // Get kakashi_ero2 component
                                var kakashiEro2 = ero2.GetComponent<kakashi_ero2>();
                                if (kakashiEro2 != null)
                                {
                                    // Get myspine via reflection
                                    var myspineField = typeof(kakashi_ero2).GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (myspineField != null)
                                    {
                                        var myspine = myspineField.GetValue(kakashiEro2) as SkeletonAnimation;
                                        if (myspine != null)
                                        {
                                            // Enable spine
                                            myspine.enabled = true;
                                            
                                            // Reset counters
                                            var countField = typeof(kakashi_ero2).GetField("count", BindingFlags.Public | BindingFlags.Instance);
                                            var seCountField = typeof(kakashi_ero2).GetField("se_count", BindingFlags.Public | BindingFlags.Instance);
                                            if (countField != null) countField.SetValue(kakashiEro2, 0);
                                            if (seCountField != null) seCountField.SetValue(kakashiEro2, 0);

                                            // Set START2 immediately
                                            myspine.state.ClearTracks();
                                            var track = myspine.state.SetAnimation(0, "START2", true);
                                            
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
                }
                catch (Exception ex)
                {
                    // Plugin.Log.LogInfo($"[KAKASI CROSS START] Error: {ex.Message}");
                }
            }
        }
    }
}

