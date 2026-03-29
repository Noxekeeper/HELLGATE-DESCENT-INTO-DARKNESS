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
    /// Patch for обработки событий креста (s_kakasiero_spine через EroAnimation)
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
        private const float CrossThoughtCooldown = 8f; // Cooldown for мыслей ГГ on кресте (8 секунд, as on земле)

        static KakasiCrossPatch()
        {
            // Get поля через рефлексию
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
            // Check if this is крест Какаси через поле kakash
            if (kakashField == null) return false;
            var kakash = kakashField.GetValue(instance) as global::Kakash;
            return kakash != null;
        }

        [HarmonyPrefix]
        private static void OnEvent_Prefix(global::EroAnimation __instance, Spine.AnimationState state, int trackIndex, Spine.Event e, ref int ___count)
        {
            try
            {
                // Check if this is крест Какаси
                if (!IsKakasiCross(__instance))
                {
                    return;
                }

                // Get mySpine через рефлексию
                if (mySpineField == null) return;
                var mySpine = mySpineField.GetValue(__instance) as SkeletonAnimation;
                if (mySpine == null) return;

                string eventName = e.Data.Name;
                string animName = mySpine.AnimationName ?? string.Empty;
                string animNameUpper = animName.ToUpperInvariant();
                string eventNameUpper = eventName.ToUpperInvariant();

                // Logs disabled
                // Plugin.Log.LogInfo($"[KAKASI CROSS] ===== EroAnimation event: {eventName}, anim: {animName}, count: {___count} =====");

                // Process event SE for всех анимаций креста
                if (eventNameUpper.Equals("SE", StringComparison.OrdinalIgnoreCase))
                {
                    // Processing dialogue system events for всех анимаций креста
                    try {
                        NoREroMod.Systems.Dialogue.DialogueFramework.ProcessAnimationEvent(
                            __instance, 
                            animName, 
                            eventName, 
                            ___count
                        );
                    } catch (Exception ex) {
                    }
                    
                    // Special handling for start (SE, count == 0) - первое event захвата
                    if (animNameUpper.Equals("START", StringComparison.OrdinalIgnoreCase) && 
                        ___count == 0 && 
                        !crossGrabTriggered)
                    {
                        crossGrabTriggered = true;
                        
                    // Эффекты теперь handled via Camera Framework
                    // REMOVED: KakasiEffects.TriggerCrossGrabEffect
                    }
                }

                // Process events переключения анимаций (next1-next7, COUNT, END)
                // Эти events переключают animation, поэтому обрабатываем their as events переключения
                bool isAnimationSwitchEvent = eventNameUpper == "NEXT1" || eventNameUpper == "NEXT2" || 
                                             eventNameUpper == "NEXT3" || eventNameUpper == "NEXT4" || 
                                             eventNameUpper == "NEXT5" || eventNameUpper == "NEXT6" || 
                                             eventNameUpper == "NEXT7" || eventNameUpper == "COUNT" || 
                                             eventNameUpper == "END";

                if (isAnimationSwitchEvent)
                {
                    // Определяем целевую animation on основе events
                    string targetAnim = animName; // By умолчанию текущая анимация
                    
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
                            // COUNT переключает on finish_end2 only if текущая анимация finish_end и count >= 1
                            if (animNameUpper.Equals("FINISH_END", StringComparison.OrdinalIgnoreCase) && ___count >= 1)
                            {
                                targetAnim = "finish_end2";
                            }
                            break;
                        case "END":
                            // END переключает on землю (START in kakashi_ero2), but this handled in оригинальном коде
                            // Здесь мы можем обработать event before переходом
                            if (animNameUpper.Equals("FINISH_END2", StringComparison.OrdinalIgnoreCase))
                            {
                                // Processing events before переходом on землю
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
                    
                    // For next5 используем корутину for processing after переключения animation
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
                        // For other событий переключения обрабатываем immediately with целевой анимацией
                        // Use coroutine for processing after переключения
                        var runner = __instance.GetComponent<MonoBehaviour>();
                        if (runner != null)
                        {
                            runner.StartCoroutine(DelayedAnimationSwitchEffect(runner, mySpine, targetAnim, eventName));
                        }
                    }
                }

                // Show GG thoughts on кресте (as у какаси on земле)
                ShowCrossPlayerThought(mySpine, animName);
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// Postfix for events END - interceptываем переход with креста on землю
        /// и устанавливаем START2 instead of START for последующtheir Kakasi in gangbang
        /// </summary>
        [HarmonyPostfix]
        private static void OnEvent_Postfix_END(global::EroAnimation __instance, Spine.AnimationState state, int trackIndex, Spine.Event e)
        {
            try
            {
                // Check if this is event END и this крест Какаси
                if (!IsKakasiCross(__instance))
                {
                    return;
                }

                string eventName = e?.Data?.Name ?? string.Empty;
                if (!eventName.Equals("END", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // Check if this is переход with креста on землю (finish_end2)
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

                // CRITICAL: Проверяем shared GlobalHandoffCount for последующtheir enemies in gangbang
                // If this subsequent enemy, устанавливаем START2 instead of START
                if (EnemyHandoffSystem.GlobalHandoffCount > 0)
                {
                    // Plugin.Log.LogInfo($"[KAKASI CROSS] END event detected, globalHandoffCount={globalHandoffCount}, forcing START -> START2");
                    
                    // Оригинальный код already установил "START", but мы перезаписываем on "START2"
                    // Use coroutine for delay, so that оригинальный код успел выполниться
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
        /// Coroutine for принудительной установки START2 after перехода with креста on землю
        /// </summary>
        private static System.Collections.IEnumerator DelayedForceSTART2(MonoBehaviour runner, SkeletonAnimation myspine2)
        {
            // Wait один кадр, so that оригинальный код успел установить START
            yield return null;
            
            if (myspine2 != null && myspine2.AnimationState != null)
            {
                string currentAnim = myspine2.AnimationName ?? string.Empty;
                
                // If анимация все еще "START" (any case), перезаписываем on "START2"
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
        /// Processing events after переключения animation (for next1-next7, COUNT)
        /// </summary>
        private static System.Collections.IEnumerator DelayedAnimationSwitchEffect(MonoBehaviour runner, SkeletonAnimation spine, string targetAnim, string eventName)
        {
            // Wait один кадр, so that анимация успела переключиться
            yield return null;
            
            if (spine != null && spine.AnimationName != null)
            {
                string currentAnim = spine.AnimationName;
                string currentAnimLower = currentAnim.ToLowerInvariant();
                string targetAnimLower = targetAnim.ToLowerInvariant();
                
                // Check if that анимация действительbut переключилась
                if (currentAnimLower.Equals(targetAnimLower, StringComparison.OrdinalIgnoreCase))
                {
                    // Processing dialogue system events for переключенной animation
                    // Use имя animation as event for search phrases in JSON
                    try {
                        // Вызываем ProcessAnimationEvent with event = имя animation (for креста in JSON events называются as имеon анимаций)
                        NoREroMod.Systems.Dialogue.DialogueFramework.ProcessAnimationEvent(
                            runner, 
                            currentAnim, 
                            currentAnim,  // Use имя animation as event for search in JSON
                            0
                        );
                    } catch (Exception ex) {
                    }
                }
            }
        }

        private static System.Collections.IEnumerator DelayedFinish1Effect(MonoBehaviour runner, SkeletonAnimation spine)
        {
            // Wait один кадр, so that анимация успела переключиться on finish1
            yield return null;
            
            if (spine != null && spine.AnimationName != null && spine.AnimationName.Equals("finish1", StringComparison.OrdinalIgnoreCase))
            {
                // Plugin.Log.LogInfo("[KAKASI CROSS] ===== Cross 'finish1' animation confirmed! =====");
                
                // CRITICAL: КРАСНАЯ phrasesа финиша показывается ВСЕГДА (используем ShowCrossPhrase for красного цвета)
                // REMOVED: Вызоin dialogue system
                
                // Эффекты теперь handled via Camera Framework
                // REMOVED: KakasiEffects.TriggerCrossFinish2Effect
            }
        }

        /// <summary>
        /// Display мысли ГГ on кресте (as у какаси on земле)
        /// </summary>
        private static void ShowCrossPlayerThought(SkeletonAnimation spine, string currentAnim)
        {
            try
            {
                // Проверяем cooldown for мыслей ГГ on кресте
                float currentTime = Time.time;
                if (currentTime - lastCrossThoughtTime < CrossThoughtCooldown)
                {
                    return; // Cooldown активен
                }

                // REMOVED: Вызоin dialogue system
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// Reset flagа on очистке состояния
        /// </summary>
        internal static void ResetCrossState()
        {
            crossGrabTriggered = false;
            lastCrossThoughtTime = 0f;
            // Plugin.Log.LogInfo("[KAKASI CROSS] Cross state reset");
        }

        /// <summary>
        /// Patch for EroAnimation.Start - interceptываем активацию креста for последующtheir Kakasi
        /// </summary>
        [HarmonyPatch(typeof(global::EroAnimation), "Start")]
        internal static class EroAnimationStartPatch
        {
            [HarmonyPostfix]
            private static void Start_Postfix(global::EroAnimation __instance)
            {
                try
                {
                    // Check if this is крест Какаси
                    if (!IsKakasiCross(__instance))
                    {
                        return;
                    }

                    // CRITICAL: Проверяем shared GlobalHandoffCount for последующtheir enemies in gangbang
                    // If this subsequent enemy, immediately переходим on землю
                    if (EnemyHandoffSystem.GlobalHandoffCount > 0)
                    {
                        // Plugin.Log.LogInfo($"[KAKASI CROSS START] Subsequent Kakasi detected (globalHandoffCount={globalHandoffCount}), skipping cross, going directly to ground START2");

                        // Деактивируем крест
                        __instance.enabled = false;
                        if (mySpineField != null)
                        {
                            var mySpine = mySpineField.GetValue(__instance) as SkeletonAnimation;
                            if (mySpine != null)
                            {
                                mySpine.enabled = false;
                            }
                        }

                        // Активируем землю (kakashi_ero2)
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
                                            // Включаем spine
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

