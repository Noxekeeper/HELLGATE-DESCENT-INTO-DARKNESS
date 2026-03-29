using System;
using HarmonyLib;
using UnityEngine;
using System.Collections;
using NoREroMod;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Effects;
using NoREroMod.Patches.Enemy.Base;

namespace NoREroMod.Patches.Enemy
{
    /// <summary>
    /// Patch for BigoniBrother mini-boss. HP reduced to 1000, damage to 70%.
    /// </summary>
    internal class BigoniBrotherPatch
    {
        private static System.Collections.Generic.HashSet<Bigoni> finishingBigoniBrothers = new System.Collections.Generic.HashSet<Bigoni>();
        private static System.Collections.Generic.Dictionary<Bigoni, int> finEventCounts = new System.Collections.Generic.Dictionary<Bigoni, int>();
        private static System.Collections.Generic.Dictionary<Bigoni, int> twoEroEventCounts = new System.Collections.Generic.Dictionary<Bigoni, int>();
        private static System.Collections.Generic.Dictionary<Bigoni, int> start2PlayCounts = new System.Collections.Generic.Dictionary<Bigoni, int>();
        private static System.Collections.Generic.HashSet<Bigoni> jigo3HandoffStarted = new System.Collections.Generic.HashSet<Bigoni>();

        internal static void ClearJigo3HandoffState() { jigo3HandoffStarted.Clear(); }
    
    /// <summary>
    /// Patch for logging events and sounds original Bigoni
    /// </summary>
    [HarmonyPatch(typeof(StartBigoniERO), "OnEvent")]
    [HarmonyPrefix]
    private static void LogOriginalBigoniEvents(StartBigoniERO __instance, Spine.AnimationState state, int trackIndex, Spine.Event e)
    {
        try
        {
            // Get event name
            string eventName = null;
            var nameProperty = e.Data.GetType().GetProperty("name") ?? e.Data.GetType().GetProperty("Name");
            if (nameProperty != null)
            {
                eventName = nameProperty.GetValue(e.Data, null) as string;
            }
            
            // Get oya (Bigoni)
            var oyaField = typeof(StartBigoniERO).GetField("oya", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (oyaField == null)
            {
                return;
            }
            
            Bigoni oya = oyaField.GetValue(__instance) as Bigoni;
            
            // Check if this is NOT BigoniBrother (i.e. original Bigoni)
            if (oya == null || oya.gameObject == null || oya.gameObject.name == null)
            {
                return;
            }
            
            // If this is BigoniBrother, skip (log it separately)
            if (oya.gameObject.name.Contains("BigoniBrother"))
            {
                return;
            }
            
            // Get myspine
            var myspineField = typeof(StartBigoniERO).GetField("myspine", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (myspineField == null)
            {
                return;
            }
            
            Spine.Unity.SkeletonAnimation myspine = myspineField.GetValue(__instance) as Spine.Unity.SkeletonAnimation;
            if (myspine == null)
            {
                return;
            }
            
            string currentAnim = myspine.AnimationName;
            float timeScale = myspine.timeScale;
            
            // Get se_count for logging
            var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int seCount = 0;
            if (seCountField != null)
            {
                seCount = (int)seCountField.GetValue(__instance);
            }
            
            // Get count for logging
            var countField = typeof(StartBigoniERO).GetField("count", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int count = 0;
            if (countField != null)
            {
                count = (int)countField.GetValue(__instance);
            }
            
            // Get current track for tracking animation time
            var track = state.GetCurrent(0);
            string trackInfo = "";
            if (track != null && track.Animation != null)
            {
                trackInfo = $", trackTime: {track.Time:F2}/{track.Animation.Duration:F2}, Loop: {track.Loop}, cycles: {(int)(track.Time / track.Animation.Duration)}";
            }
            
            // Log all events with details
            // Plugin.Log.LogInfo( $"[ORIGINAL BIGONI] Event: {eventName}, Anim: {currentAnim}, timeScale: {timeScale:F2}, se_count: {seCount}, count: {count}{trackInfo}");
            
            // Log transitions animations with details
            if (eventName == "START2" || eventName == "START3" || eventName == "4EROJIGO" || eventName == "FIN" || eventName == "FIN2" || eventName == "JIGO" || eventName == "ERO" || eventName == "2ERO")
            {
                // Plugin.Log.LogInfo( $"[ORIGINAL BIGONI] *** ANIMATION TRANSITION EVENT: {eventName} from {currentAnim} (timeScale: {timeScale:F2}, count: {count}, se_count: {seCount}) ***");
            }
            
            // Detailed logging for events ERO and 2ERO
            if (eventName == "ERO" || eventName == "2ERO")
            {
                // Plugin.Log.LogInfo( $"[ORIGINAL BIGONI] *** {eventName} EVENT DETAILS: currentAnim={currentAnim}, count={count}, timeScale={timeScale:F2}{trackInfo} ***");
            }
            
            // Log sounds, that will be played
            if (eventName == "SE" || eventName == "SE5" || eventName == "SE6" || eventName == "START" || eventName == "START2" || eventName == "START3" || eventName == "4EROJIGO" || eventName == "FADE")
            {
                // Log in more detail for sound events
                if (eventName == "SE")
                {
                    if (currentAnim == "START")
                    {
                        if (seCount == 1)
                        {
                            // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] SOUND: dame_ugu, snd_trapstart (SE count=1, START anim)");
                        }
                    }
                    else if (currentAnim == "START2")
                    {
                        if (seCount == 1)
                        {
                            // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] SOUND: snd_trapwood, dame_ahaa (SE count=1, START2 anim)");
                        }
                    }
                    else if (currentAnim == "START3")
                    {
                        if (seCount == 1)
                        {
                            // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] SOUND: ero_Unconscious (SE count=1, START3 anim)");
                        }
                    }
                    else if (currentAnim == "4EROJIGO")
                    {
                        if (seCount == 1)
                        {
                            // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] SOUND: ero_gutyugutyu2, ero_nameru (SE count=1, 4EROJIGO anim)");
                        }
                    }
                }
                else if (eventName == "SE5")
                {
                    // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] SOUND: snd_step (SE5 event)");
                }
                else if (eventName == "SE6")
                {
                    // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] SOUND: SE[0] AudioClip (SE6 event)");
                }
                else if (eventName == "START2")
                {
                    // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] SOUND: ero_Unconscious, switching to START2 anim");
                }
                else if (eventName == "START3")
                {
                    // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] SOUND: ero_Unconscious, switching to START3 anim (looped)");
                }
                else if (eventName == "4EROJIGO")
                {
                    // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] Switching to 4EROJIGO anim (looped), stopping EroVoice");
                }
                else if (eventName == "FADE")
                {
                    // Plugin.Log.LogInfo( $"[ORIGINAL BIGONI] FADE event, count={count}, triggering fadeevent() or place_GO()");
                }
            }
            
            // Log ALL events, including those, that may come after FADE
            if (eventName == "4EROJIGO" || eventName == "FIN" || eventName == "FIN2" || eventName == "FIN3" || 
                eventName == "JIGO" || eventName == "JIGO2" || eventName == "JIGO3" || eventName == "JIGO4" ||
                eventName == "2ERO" || eventName == "2EROJIGO" || eventName == "ERO2" || eventName == "ERO3" || eventName == "ERO4" ||
                eventName == "ERO")
            {
                // Plugin.Log.LogInfo( $"[ORIGINAL BIGONI] *** IMPORTANT EVENT: {eventName} *** Anim: {currentAnim}, count: {count}, se_count: {seCount}, timeScale: {timeScale:F2}{trackInfo}");
                
                if (eventName == "4EROJIGO")
                {
                    // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] *** 4EROJIGO EVENT - CREAMPIЕ START ***");
                }
                else if (eventName == "FIN" || eventName == "FIN2" || eventName == "FIN3")
                {
                    // Plugin.Log.LogInfo( $"[ORIGINAL BIGONI] *** FIN EVENT: {eventName} - CREAMPIЕ PHASE (count={count}, timeScale={timeScale:F2}) ***");
                }
                else if (eventName == "JIGO" || eventName == "JIGO2" || eventName == "JIGO3" || eventName == "JIGO4")
                {
                    // Plugin.Log.LogInfo( $"[ORIGINAL BIGONI] *** JIGO EVENT: {eventName} - PULLING OUT GG (count={count}) ***");
                }
                else if (eventName == "ERO")
                {
                    // Plugin.Log.LogInfo( $"[ORIGINAL BIGONI] *** ERO EVENT - TRANSITION TO ERO ANIMATION (count={count}) ***");
                }
                else if (eventName == "2ERO")
                {
                    // Plugin.Log.LogInfo( $"[ORIGINAL BIGONI] *** 2ERO EVENT - TRANSITION TO 2ERO ANIMATION (count={count}, will transition if count==2) ***");
                }
            }
            
            // Log state count after each FADE events
            if (eventName == "FADE")
            {
                // Plugin.Log.LogInfo( $"[ORIGINAL BIGONI] FADE DETAILS: count={count} (will trigger fadeevent() if count==1, place_GO() if count==2)");
            }
        }
        catch (System.Exception ex)
        {
        }
    }
    
    /// <summary>
    /// Patch for logging BigoniERO events (if separate class)
    /// </summary>
    [HarmonyPatch(typeof(BigoniERO), "OnEvent")]
    [HarmonyPrefix]
    private static void LogBigoniEROEvents(BigoniERO __instance, Spine.AnimationState state, int trackIndex, Spine.Event e)
    {
        try
        {
            // Get event name
            string eventName = null;
            var nameProperty = e.Data.GetType().GetProperty("name") ?? e.Data.GetType().GetProperty("Name");
            if (nameProperty != null)
            {
                eventName = nameProperty.GetValue(e.Data, null) as string;
            }
            
            // Get myspine
            var myspineField = typeof(BigoniERO).GetField("myspine", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (myspineField == null)
            {
                return;
            }
            
            Spine.Unity.SkeletonAnimation myspine = myspineField.GetValue(__instance) as Spine.Unity.SkeletonAnimation;
            if (myspine == null)
            {
                return;
            }
            
            string currentAnim = myspine.AnimationName;
            float timeScale = myspine.timeScale;
            
            // Get current track for tracking animation time
            var track = state.GetCurrent(0);
            string trackInfo = "";
            if (track != null && track.Animation != null)
            {
                trackInfo = $", trackTime: {track.Time:F2}/{track.Animation.Duration:F2}, Loop: {track.Loop}, cycles: {(int)(track.Time / track.Animation.Duration)}";
            }
            
            // Get count
            var countField = typeof(BigoniERO).GetField("count", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int count = 0;
            if (countField != null)
            {
                count = (int)countField.GetValue(__instance);
            }
            
            // Get se_count
            var seCountField = typeof(BigoniERO).GetField("se_count", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int seCount = 0;
            if (seCountField != null)
            {
                seCount = (int)seCountField.GetValue(__instance);
            }
            
            // Log BigoniERO events with details
            // Plugin.Log.LogInfo( $"[ORIGINAL BIGONI] BigoniERO Event: {eventName}, Anim: {currentAnim}, timeScale: {timeScale:F2}, count: {count}, se_count: {seCount}{trackInfo}");
            
            // Log sounds for SE events on different animations (as in BigoniERO.cs:223-301)
            if (eventName == "SE")
            {
                if (currentAnim == "ERO" && seCount == 1)
                {
                    // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] *** SOUND: ero_now12 (SE on ERO, se_count==1) ***");
                }
                else if (currentAnim == "2ERO" && seCount == 1)
                {
                    // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] *** SOUND: ero_now11 - CREAMPIE SOUND (SE on 2ERO, se_count==1) ***");
                }
                else if (currentAnim == "FIN")
                {
                    if (seCount == 1)
                    {
                        // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] *** SOUND: ero_now11 (SE on FIN, se_count==1) ***");
                    }
                    else if (seCount == 2)
                    {
                        // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] *** SOUND: ero_enemy_syasei1 (SE on FIN, se_count==2) ***");
                    }
                }
                else if (currentAnim == "FIN2")
                {
                    if (seCount == 1)
                    {
                        // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] *** SOUND: ero_now11 (SE on FIN2, se_count==1) ***");
                    }
                    else if (seCount == 2)
                    {
                        // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] *** SOUND: randomSE() + ero_enemy_syasei1 (SE on FIN2, se_count==2) ***");
                    }
                }
                else if (currentAnim == "JIGO" && seCount == 1)
                {
                    // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] *** SOUND: ero_Unconscious (SE on JIGO, se_count==1) ***");
                }
                else if (currentAnim == "JIGO2")
                {
                    if (seCount == 1)
                    {
                        // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] *** SOUND: dame_kuu (SE on JIGO2, se_count==1) ***");
                    }
                    else if (seCount == 2)
                    {
                        // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] *** SOUND: snd_down (SE on JIGO2, se_count==2) ***");
                    }
                }
                else if ((currentAnim == "JIGO3" || currentAnim == "JIGO4") && seCount == 1)
                {
                    // Plugin.Log.LogInfo( $"[ORIGINAL BIGONI] *** SOUND: ero_Unconscious (SE on {currentAnim}, se_count==1) ***");
                }
            }
            // Log sounds on animation transitions (count will be incremented inside handler)
            // In BigoniERO.cs:324-334 on event "2ERO": count++, then if count==2, sound is played
            // So in Prefix we see count BEFORE increment, and sound is played AFTER increment
            if (eventName == "2ERO")
            {
                // Log that event arrived, count will be incremented inside
                // Plugin.Log.LogInfo( $"[ORIGINAL BIGONI] *** 2ERO event received, count BEFORE increment: {count} (will increment to {count+1}, sound plays if count becomes 2) ***");
            }
            else if (eventName == "JIGO")
            {
                // Log that event arrived, count will be incremented inside
                // Plugin.Log.LogInfo( $"[ORIGINAL BIGONI] *** JIGO event received, count BEFORE increment: {count} (will increment to {count+1}, sound plays if count >= 1) ***");
            }
            
            // Detailed logging for important events
            if (eventName == "ERO" || eventName == "2ERO" || eventName == "FIN" || eventName == "FIN2" || 
                eventName == "JIGO" || eventName == "JIGO2" || eventName == "JIGO3" || eventName == "JIGO4")
            {
                // Plugin.Log.LogInfo( $"[ORIGINAL BIGONI] *** BigoniERO IMPORTANT: {eventName} *** Anim: {currentAnim}, count: {count}, se_count: {seCount}, timeScale: {timeScale:F2}{trackInfo}");
            }
        }
        catch (System.Exception ex)
        {
        }
    }
    
    /// <summary>
    /// Postfix patch for logging sounds AFTER processing events (when count already incremented)
    /// </summary>
    [HarmonyPatch(typeof(BigoniERO), "OnEvent")]
    [HarmonyPostfix]
    private static void LogBigoniEROSoundsAfterEvent(BigoniERO __instance, Spine.AnimationState state, int trackIndex, Spine.Event e)
    {
        try
        {
            // Get event name
            string eventName = null;
            var nameProperty = e.Data.GetType().GetProperty("name") ?? e.Data.GetType().GetProperty("Name");
            if (nameProperty != null)
            {
                eventName = nameProperty.GetValue(e.Data, null) as string;
            }
            
            // Get myspine
            var myspineField = typeof(BigoniERO).GetField("myspine", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (myspineField == null)
            {
                return;
            }
            
            Spine.Unity.SkeletonAnimation myspine = myspineField.GetValue(__instance) as Spine.Unity.SkeletonAnimation;
            if (myspine == null)
            {
                return;
            }
            
            string currentAnim = myspine.AnimationName;
            
            // Get count AFTER processing events
            var countField = typeof(BigoniERO).GetField("count", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int countAfter = 0;
            if (countField != null)
            {
                countAfter = (int)countField.GetValue(__instance);
            }
            
            // Log sounds AFTER processing events "2ERO"
            if (eventName == "2ERO" && countAfter == 0 && currentAnim == "2ERO")
            {
                // If count became 0 after processing, means it was 2 and sound was played
                // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] *** SOUND: StopBus(EroVoice) + ero_now11 - CREAMPIE SOUND (2ERO event, count was 2, transitioned to 2ERO) ***");
            }
            
            // Log sounds AFTER processing events "JIGO"
            if (eventName == "JIGO" && countAfter == 0 && currentAnim == "JIGO")
            {
                // If count became 0 after processing, means it was >= 1 and sound was played
                // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] *** SOUND: StopBus(EroVoice) + ero_Unconscious (JIGO event, count was >= 1, transitioned to JIGO) ***");
            }
            
            // Get se_count AFTER processing events SE
            if (eventName == "SE")
            {
                var seCountField = typeof(BigoniERO).GetField("se_count", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                int seCountAfter = 0;
                if (seCountField != null)
                {
                    seCountAfter = (int)seCountField.GetValue(__instance);
                }
                
                // Log sounds based on se_count AFTER processing
                if (currentAnim == "2ERO" && seCountAfter == 0)
                {
                    // se_count became 0 after processing - means it was 1 and sound was played
                    // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] *** SOUND: ero_now11 - CREAMPIE SOUND (SE on 2ERO, se_count was 1, now reset to 0) ***");
                }
                else if (currentAnim == "JIGO" && seCountAfter == 0)
                {
                    // se_count became 0 after processing - means it was 1 and sound was played
                    // Plugin.Log.LogInfo( "[ORIGINAL BIGONI] *** SOUND: ero_Unconscious (SE on JIGO, se_count was 1, now reset to 0) ***");
                }
            }
        }
        catch (System.Exception ex)
        {
        }
    }
    /// <summary>
    /// Patch on Bigoni.OnTriggerStay2D - sets timeScale on H-animation start
    /// </summary>
    [HarmonyPatch(typeof(Bigoni), "OnTriggerStay2D")]
    [HarmonyPostfix]
    private static void SetTimeScaleOnHAnimationStart(Bigoni __instance, Collider2D collision)
    {
        try
        {
            // Only for BigoniBrother
            if (__instance.gameObject == null || __instance.gameObject.name == null || 
                !__instance.gameObject.name.Contains("BigoniBrother"))
            {
                return;
            }

            // Check if this is H-animation start
            if (__instance.eroflag && collision != null && collision.gameObject.tag == "playerDAMAGEcol")
            {
                // Get erodata and StartBigoniERO for access to myspine
                var erodataField = typeof(Bigoni).GetField("erodata", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (erodataField != null)
                {
                    GameObject erodata = erodataField.GetValue(__instance) as GameObject;
                    if (erodata != null)
                    {
                        StartBigoniERO ero = erodata.GetComponent<StartBigoniERO>();
                        if (ero != null)
                        {
                            // Get myspine via reflection
                            var myspineField = typeof(StartBigoniERO).GetField("myspine", 
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            
                            if (myspineField != null)
                            {
                                Spine.Unity.SkeletonAnimation myspine = myspineField.GetValue(ero) as Spine.Unity.SkeletonAnimation;
                                if (myspine != null)
                                {
                                    // Set normal speed immediately on H-animation start
                                    myspine.timeScale = 1f;
                                    // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Set timeScale = 1f on H-animation start (OnTriggerStay2D Postfix), anim={myspine.AnimationName}");
                                    
                                    // Start coroutine to maintain timeScale
                                    __instance.StartCoroutine(MaintainTimeScaleOnBigoni(myspine, __instance));
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
        }
    }


    /// <summary>
    /// Patch on Bigoni.Start() to reduce HP to 1000
    /// </summary>
    [HarmonyPatch(typeof(Bigoni), "Start")]
    [HarmonyPostfix]
    private static void ReduceBigoniHP(Bigoni __instance)
    {
        try
        {
            // Only for our clone named BigoniBrother
            if (__instance.gameObject == null || __instance.gameObject.name == null || !__instance.gameObject.name.Contains("BigoniBrother"))
            {
                return;
            }

            // Get MaxHp via reflection
            var maxHpField = typeof(EnemyDate).GetField("MaxHp", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (maxHpField != null)
            {
                float currentMaxHp = (float)maxHpField.GetValue(__instance);
                
                // Reduce HP only our clone
                if (currentMaxHp >= 500f)
                {
                    maxHpField.SetValue(__instance, 1000f);
                    
                    // Also reduce current HP
                    var hpField = typeof(EnemyDate).GetField("Hp", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (hpField != null)
                    {
                        hpField.SetValue(__instance, 1000f);
                    }
                    
                    // Reduce damage by 30%
                    var atkField = typeof(EnemyDate).GetField("enmATK", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (atkField != null)
                    {
                        float currentAtk = (float)atkField.GetValue(__instance);
                        float newAtk = currentAtk * 0.7f; // 70% of original damage
                        atkField.SetValue(__instance, newAtk);
                    }
                    
                    // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Tuned: HP 1000, ATK 70% (was HP {currentMaxHp})");
                }
            }
        }
        catch (System.Exception ex)
        {
        }
    }

    /// <summary>
    /// Patch on StartBigoniERO.OnEvent - blocks event "FADE" (Bad End) for BigoniBrother
    /// and switches to normal H-animation via standard EnemyEro component
    /// </summary>
    [HarmonyPatch(typeof(StartBigoniERO), "OnEvent")]
    [HarmonyPrefix]
    private static bool BlockFadeEventForBigoniBrother(StartBigoniERO __instance, Spine.AnimationState state, int trackIndex, Spine.Event e)
    {
        try
        {
            // Check if this is event "FADE"
            if (e == null || e.Data == null)
            {
                return true; // Skip if no event data
            }

            // Get event name via reflection (may be name or Name)
            string eventName = null;
            var nameProperty = e.Data.GetType().GetProperty("name") ?? e.Data.GetType().GetProperty("Name");
            if (nameProperty != null)
            {
                eventName = nameProperty.GetValue(e.Data, null) as string;
            }

            if (eventName != "FADE")
            {
                return true; // Skip other events
            }

            // Get field oya (Bigoni) via reflection
            var oyaField = typeof(StartBigoniERO).GetField("oya", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (oyaField != null)
            {
                Bigoni oya = oyaField.GetValue(__instance) as Bigoni;
                
                // Check if this is BigoniBrother
                if (oya != null && oya.gameObject != null && oya.gameObject.name != null && 
                    oya.gameObject.name.Contains("BigoniBrother"))
                {
                    // Block event "FADE" for BigoniBrother
                    // Plugin.Log.LogInfo("[BIGONI BROTHER] Blocked FADE event - Bad End prevented");
                    
                    // Switch to normal H-animation - get erospine and switch to cycle
                    var erospineField = typeof(Bigoni).GetField("erospine", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (erospineField != null)
                    {
                        Spine.Unity.SkeletonAnimation erospine = erospineField.GetValue(oya) as Spine.Unity.SkeletonAnimation;
                        if (erospine != null && erospine.AnimationState != null)
                        {
                            // Get current animation
                            string currentAnim = erospine.AnimationName;
                            
                            // Try switch to normal H-animation (if they exist)
                            string[] normalAnimations = { "4ERO", "4EROJIGO", "ERO", "ERO1", "2ERO" };
                            bool animationSet = false;
                            
                            foreach (string animName in normalAnimations)
                            {
                                try
                                {
                                    var anim = erospine.skeleton.Data.FindAnimation(animName);
                                    if (anim != null)
                                    {
                                        // Reset se_count on switch animation, so that sounds work correctly
                                        var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                        if (seCountField != null)
                                        {
                                            seCountField.SetValue(__instance, 0);
                                        }
                                        
                                        erospine.AnimationState.SetAnimation(0, animName, true);
                                        // Plugin.Log.LogInfo($"[BIGONI BROTHER] Switched to normal H-animation: {animName}, se_count reset");
                                        animationSet = true;
                                        break;
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                }
                            }
                            
                            // If normal not found animation, just continue current in loop
                            if (!animationSet && !string.IsNullOrEmpty(currentAnim) && currentAnim != "FADE")
                            {
                                erospine.AnimationState.SetAnimation(0, currentAnim, true);
                                // Plugin.Log.LogInfo($"[BIGONI BROTHER] Continuing current animation in loop: {currentAnim}");
                            }
                        }
                    }

                    // CRITICAL: Activate system struggle for BigoniBrother
                    // In StartBigoniERO.Start() is set pl._SOUSA = false, which disables struggle
                    // Need to set _SOUSA = true, so that player could escape
                    var plField = typeof(StartBigoniERO).GetField("pl", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (plField != null)
                    {
                        PlayerStatus pl = plField.GetValue(__instance) as PlayerStatus;
                        if (pl != null)
                        {
                            pl._SOUSA = true; // Activate struggle system
                            pl._SOUSAMNG = true; // Activate system struggle in menu
                            // Plugin.Log.LogInfo("[BIGONI BROTHER] Activated struggle system (_SOUSA = true)");
                        }
                    }

                    return false; // Block processing events "FADE"
                }
            }
        }
        catch (System.Exception ex)
        {
        }
        
        return true; // Continue execution for normal Bigoni
    }

    /// <summary>
    /// Patch on StartBigoniERO.Start() - activates system struggle for BigoniBrother
    /// In original Start() is set pl._SOUSA = false, which disables struggle
    /// </summary>
    [HarmonyPatch(typeof(StartBigoniERO), "Start")]
    [HarmonyPostfix]
    private static void EnableStruggleForBigoniBrother(StartBigoniERO __instance)
    {
        try
        {
            // Get field oya (Bigoni) via reflection
            var oyaField = typeof(StartBigoniERO).GetField("oya", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (oyaField != null)
            {
                Bigoni oya = oyaField.GetValue(__instance) as Bigoni;
                
                // Check if this is BigoniBrother
                if (oya != null && oya.gameObject != null && oya.gameObject.name != null && 
                    oya.gameObject.name.Contains("BigoniBrother"))
                {
                    // Activate struggle system (in original Start() sets _SOUSA = false)
                    var plField = typeof(StartBigoniERO).GetField("pl", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (plField != null)
                    {
                        PlayerStatus pl = plField.GetValue(__instance) as PlayerStatus;
                        if (pl != null)
                        {
                            pl._SOUSA = true; // Activate struggle system
                            pl._SOUSAMNG = true; // Activate system struggle in menu
                            // Plugin.Log.LogInfo( "[BIGONI BROTHER] Activated struggle system in Start() (_SOUSA = true)");
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
        }
    }

    /// <summary>
    /// Prefix patch on StartBigoniERO.OnEvent - handles sound events and end animation for BigoniBrother
    /// </summary>
    [HarmonyPatch(typeof(StartBigoniERO), "OnEvent")]
    [HarmonyPrefix]
    private static bool HandleBigoniBrotherEvents(StartBigoniERO __instance, Spine.AnimationState state, int trackIndex, Spine.Event e)
    {
        try
        {
            if (e == null || e.Data == null)
            {
                return true; // Skip if no event data
            }

            // Get event name via reflection
            string eventName = null;
            var nameProperty = e.Data.GetType().GetProperty("name") ?? e.Data.GetType().GetProperty("Name");
            if (nameProperty != null)
            {
                eventName = nameProperty.GetValue(e.Data, null) as string;
            }

            // Get field oya (Bigoni) via reflection
            var oyaField = typeof(StartBigoniERO).GetField("oya", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (oyaField == null)
            {
                return true; // Skip if cannot get oya
            }

            Bigoni oya = oyaField.GetValue(__instance) as Bigoni;
            
            // Check if this is BigoniBrother
            if (oya == null || oya.gameObject == null || oya.gameObject.name == null || 
                !oya.gameObject.name.Contains("BigoniBrother"))
            {
                return true; // Skip for normal Bigoni
            }

            // Get myspine and current animation
            var myspineField = typeof(StartBigoniERO).GetField("myspine", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (myspineField == null)
            {
                return true;
            }

            Spine.Unity.SkeletonAnimation myspine = myspineField.GetValue(__instance) as Spine.Unity.SkeletonAnimation;
            if (myspine == null)
            {
                return true;
            }

            string currentAnim = myspine.AnimationName;
            
            // Process event "START" - animation speed handled in Postfix patch
            if (eventName == "START")
            {
                // Continue execution original method (it will set timeScale = 1f, but Postfix will override)
                return true;
            }
            
            // Handle START2 event: start coroutine to track animation completion
            if (eventName == "START2")
            {
                // Initialize counter for this BigoniBrother instance
                if (!start2PlayCounts.ContainsKey(oya))
                {
                    start2PlayCounts[oya] = 0;
                }
                
                int requiredCount = Plugin.bigoniBrotherStart2RepeatCount?.Value ?? 3;

                // Start coroutine to track START2 completion
                // Use oya (Bigoni) GameObject to run coroutine, as it persists
                if (oya != null)
                {
                    oya.StartCoroutine(TrackStart2Completion(oya, myspine, __instance));
                }
                
                // Continue original method execution (it will set START2)
                return true;
            }

            // Process event "FADE" - block Bad End и switch to ERO
            if (eventName == "FADE")
            {
                // Switch to normal H-animation via myspine
                string[] possibleAnims = { "ERO", "4ERO", "ERO1", "2ERO" };
                string animName = null;

                foreach (string anim in possibleAnims)
                {
                    var animData = myspine.skeleton.Data.FindAnimation(anim);
                    if (animData != null)
                    {
                        animName = anim;
                        break;
                    }
                }
                
                if (animName != null)
                {
                    // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Switched to normal H-animation: {animName}, se_count reset");
                    
                    // Switch to normal H-animation (looped)
                    myspine.state.SetAnimation(0, animName, true);
                    myspine.timeScale = 1f; // Normal speed
                    
                    // Start EroVoice for GG moans (if not yet started)
                    // In original EroVoice is started automatically, but for BigoniBrother may not start
                    try
                    {
                        // Check if not stopped EroVoice (if not playing, start it)
                        // MasterAudio has no direct method to check Bus state, 
                        // so just try start sounds from EroVoice group
                        // In original EroVoice is started automatically on H-animation start
                        // For BigoniBrother we rely on automatic start via sound system
                        // Plugin.Log.LogInfo( "[BIGONI BROTHER] EroVoice should start automatically with H-animation");
                    }
                    catch (System.Exception ex)
                    {
                    }
                    
                    // Reset se_count
                    var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (seCountField != null)
                    {
                        seCountField.SetValue(__instance, 0);
                    }
                    
                    // START coroutine tracking cycles ERO
                    if (oya != null && !finishingBigoniBrothers.Contains(oya))
                    {
                        // Plugin.Log.LogInfo( "[BIGONI BROTHER] Starting MaintainTimeScaleOnBigoni coroutine after FADE→ERO transition");
                        oya.StartCoroutine(MaintainTimeScaleOnBigoni(myspine, oya));
                    }
                }
                else
                {
                    // Plugin.Log.LogInfo( "[BIGONI BROTHER] No suitable H-animation found (ERO, 4ERO, ERO1, 2ERO)");
                }
                
                // Activate struggle system
                var plField = typeof(StartBigoniERO).GetField("pl", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (plField != null)
                {
                    PlayerStatus pl = plField.GetValue(__instance) as PlayerStatus;
                    if (pl != null)
                    {
                        pl._SOUSA = true;
                        pl._SOUSAMNG = true;
                        // Plugin.Log.LogInfo( "[BIGONI BROTHER] Activated struggle system (_SOUSA = true)");
                    }
                }
                
                // Block processing original events (which triggers Bad End)
                return false;
            }

            // Process event "2ERO" - transition to increased pace (as in original BigoniERO.cs:324-334)
            if (eventName == "2ERO")
            {
                // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Handling 2ERO event from {currentAnim} - checking count for transition");
                
                // Use our own counter twoEroEventCounts for BigoniBrother
                // (as original count may reset)
                if (!twoEroEventCounts.ContainsKey(oya))
                {
                    twoEroEventCounts[oya] = 0;
                }
                
                twoEroEventCounts[oya]++;
                int count = twoEroEventCounts[oya];
                
                // Plugin.Log.LogInfo( $"[BIGONI BROTHER] 2ERO event: count={count} (need count==2 to transition)");
                
                // If count == 2, switch to 2ERO (as in original BigoniERO.cs:324-334)
                if (count == 2)
                {
                    myspine.state.SetAnimation(0, "2ERO", true); // true = looped (as in original)
                    myspine.timeScale = 1f; // As in original: timeScale = 1f
                    // Plugin.Log.LogInfo( $"[BIGONI BROTHER] 2ERO count==2: Set 2ERO animation (Loop=true, timeScale=1f)");
                    
                    // In original BigoniERO.cs:332-333 on switch to 2ERO:
                    // - EroVoice stops
                    // - Plays ero_now11 (creampie sound)
                    // But for BigoniBrother we do NOT stop EroVoice, so that moans of GG continue playing
                    // Creampie sound play on switch to 2ERO (as in original)
                    DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_now11", 1f, null, 0f, null, false, false);
                    // Plugin.Log.LogInfo( "[BIGONI BROTHER] Transition to 2ERO: Played ero_now11 (creampie sound), EroVoice continues");
                    
                    // Reset counters (as in original BigoniERO.cs:330-331)
                    // In StartBigoniERO.cs:271,274 fields count and se_count declared as public
                    var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var countField = typeof(StartBigoniERO).GetField("count", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (seCountField != null)
                    {
                        seCountField.SetValue(__instance, 0);
                    }
                    if (countField != null)
                    {
                        countField.SetValue(__instance, 0);
                    }
                    
                    // Reset our counters
                    twoEroEventCounts[oya] = 0;
                    finEventCounts.Remove(oya); // Remove if was created
                    start2PlayCounts.Remove(oya); // Remove if was created
                }
                
                // Continue execution original method
                return true;
            }
            
            // Process event "FIN" - increment counter (as in original BigoniERO.cs: count >= 15 for transition on FIN)
            if (eventName == "FIN")
            {
                // Use our own counter finEventCounts for BigoniBrother
                if (!finEventCounts.ContainsKey(oya))
                {
                    finEventCounts[oya] = 0;
                }
                
                finEventCounts[oya]++;
                int count = finEventCounts[oya];
                
                // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Handling FIN event from {currentAnim}, count={count}");
                
                // As in original BigoniERO.cs:
                // count == 4: timeScale = 1.2f
                // count == 8: timeScale = 1.4f
                // count >= 15: switch on FIN with Loop = false
                if (count == 4)
                {
                    myspine.timeScale = 1.2f;
                    // Plugin.Log.LogInfo( "[BIGONI BROTHER] FIN count=4, setting timeScale=1.2f");
                }
                else if (count == 8)
                {
                    myspine.timeScale = 1.4f;
                    // Plugin.Log.LogInfo( "[BIGONI BROTHER] FIN count=8, setting timeScale=1.4f");
                }
                else if (count >= 15)
                {
                    // Switch on FIN with Loop = false (as in original BigoniERO.cs:346-352)
                    myspine.state.SetAnimation(0, "FIN", false);
                    myspine.timeScale = 1f; // As in original: timeScale = 1f
                    // Plugin.Log.LogInfo( "[BIGONI BROTHER] FIN count>=15, switching to FIN animation (Loop=false, timeScale=1f)");
                    
                    // Reset counters (as in original BigoniERO.cs:350-351)
                    var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (seCountField != null)
                    {
                        seCountField.SetValue(__instance, 0);
                    }
                    
                    // Reset our counter
                    finEventCounts[oya] = 0;
                    
                    // IMPORTANT: do NOT stop EroVoice here - sounds must continue playing on FIN animation
                    // Stop EroVoice happens only on JIGO (as in original BigoniERO.cs:368)
                }
                
                // Continue execution original method
                return true;
            }
            
            // Process event "FIN2" - transition to FIN2 (as in original BigoniERO.cs:354-359)
            if (eventName == "FIN2")
            {
                // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Handling FIN2 event from {currentAnim} - switching to FIN2");
                
                

                MindBrokenSystem.RegisterClimaxEvent(oya);
                
                // As in original BigoniERO.cs:354-359: immediately switch on FIN2 with Loop = false
                myspine.state.SetAnimation(0, "FIN2", false);
                myspine.timeScale = 1f; // As in original: timeScale = 1f
                // Plugin.Log.LogInfo( "[BIGONI BROTHER] Set FIN2 animation (Loop=false, timeScale=1f)");
                
                // Reset counters (as in original BigoniERO.cs:357-358)
                var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var countField = typeof(StartBigoniERO).GetField("count", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (seCountField != null)
                {
                    seCountField.SetValue(__instance, 0);
                }
                if (countField != null)
                {
                    countField.SetValue(__instance, 0);
                }

                // IMPORTANT: do NOT stop EroVoice here - sounds must continue playing until JIGO

                // Continue execution original method
                return true;
            }
            
            // Process event "JIGO" - pulling out GG (as in original BigoniERO.cs)
            if (eventName == "JIGO")
            {
                // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Handling JIGO event from {currentAnim} - pulling out GG");
                
                // As in original BigoniERO.cs: count++, if count >= 1, switch on JIGO (Loop = false)
                var countField = typeof(StartBigoniERO).GetField("count", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                int count = 0;
                if (countField != null)
                {
                    count = (int)countField.GetValue(__instance);
                    // If count was not initialized or less than 0, set to 0
                    if (count < 0) count = 0;
                    count++;
                    countField.SetValue(__instance, count);
                }
                else
                {
                    // If field not found, set count = 1 directly
                    count = 1;
                }
                
                // Log AFTER increment
                // Plugin.Log.LogInfo( $"[BIGONI BROTHER] JIGO event: count={count} (after increment)");
                
                // If count >= 1, switch on JIGO (as in original BigoniERO.cs:360-370)
                if (count >= 1)
                {
                    myspine.state.SetAnimation(0, "JIGO", false);
                    myspine.timeScale = 1f;
                    // Plugin.Log.LogInfo( "[BIGONI BROTHER] Set JIGO animation (Loop=false)");
                    
                    // Stop EroVoice and play sound (as in original BigoniERO.cs:368-369)
                    DarkTonic.MasterAudio.MasterAudio.StopBus("EroVoice");
                    // In original sound is played on event JIGO (count >= 1)
                    // А also on SE event on JIGO (se_count == 1)
                    // Therefore play sound here, but also need to play on SE event
                    DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_Unconscious", 1f, null, 0f, null, false, false);
                    // Plugin.Log.LogInfo( "[BIGONI BROTHER] JIGO: Stopped EroVoice and played ero_Unconscious");
                    
                    // Reset counters (as in original BigoniERO.cs:366-367)
                    // In StartBigoniERO.cs:274 field se_count declared as public
                    var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (seCountField != null)
                    {
                        // IMPORTANT: do NOT reset se_count here - it must be 0, so that on first SE event on JIGO it becomes 1
                        // and sound was played pullout (as in original BigoniERO.cs:266-271)
                        seCountField.SetValue(__instance, 0);
                    }
                    if (countField != null)
                    {
                        countField.SetValue(__instance, 0);
                    }
                    
                    // Start coroutine for completion after playing JIGO (or JIGO4, if we reach it)
                    if (!finishingBigoniBrothers.Contains(oya))
                    {
                        var playerField = typeof(StartBigoniERO).GetField("player", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (playerField != null)
                        {
                            playercon player = playerField.GetValue(__instance) as playercon;
                            if (player != null && oya != null)
                            {
                                finishingBigoniBrothers.Add(oya);
                                // Plugin.Log.LogInfo( "[BIGONI BROTHER] Starting WaitForFinishAnimationAndEnd for JIGO (will wait for JIGO4 or END)");
                                oya.StartCoroutine(WaitForFinishAnimationAndEnd(myspine, oya, player, __instance, "JIGO"));
                            }
                            else
                            {
                            }
                        }
                        else
                        {
                        }
                    }
                    else
                    {
                        // Plugin.Log.LogInfo( "[BIGONI BROTHER] WaitForFinishAnimationAndEnd already running for this BigoniBrother");
                    }
                }
                
                // Continue execution original method
                return true;
            }
            
            // Process event "JIGO2" - продолжение pullout (as in original BigoniERO.cs)
            if (eventName == "JIGO2")
            {
                // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Handling JIGO2 event from {currentAnim}");
                
                // As in original BigoniERO.cs: switch on JIGO2 (Loop = false)
                myspine.state.SetAnimation(0, "JIGO2", false);
                myspine.timeScale = 1f;
                DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_Unconscious", 1f, null, 0f, null, false, false);
                
                // Reset counters
                var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var countField = typeof(StartBigoniERO).GetField("count", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (seCountField != null)
                {
                    seCountField.SetValue(__instance, 0);
                }
                if (countField != null)
                {
                    countField.SetValue(__instance, 0);
                }
                
                return true;
            }
            
            // Process event "JIGO3" - wait for completion JIGO3, then handoff and spawn goblins (without darkening)
            if (eventName == "JIGO3")
            {
                // GUARD: prevent duplication (two copies ero-animation goblins on repeated call JIGO3)
                if (jigo3HandoffStarted.Contains(oya))
                {
                    return true;
                }
                jigo3HandoffStarted.Add(oya);
                
                // As in original BigoniERO.cs: switch on JIGO3 (Loop = false)
                myspine.state.SetAnimation(0, "JIGO3", false);
                myspine.timeScale = 1f;
                DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_Unconscious", 1f, null, 0f, null, false, false);
                
                // Reset counters
                var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var countField = typeof(StartBigoniERO).GetField("count", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (seCountField != null)
                {
                    seCountField.SetValue(__instance, 0);
                }
                if (countField != null)
                {
                    countField.SetValue(__instance, 0);
                }
                
                // Start coroutine: wait for completion JIGO3 (full ero-animation), then handoff and spawn without darkening
                if (oya != null)
                {
                    var playerField = typeof(StartBigoniERO).GetField("player", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (playerField != null)
                    {
                        playercon player = playerField.GetValue(__instance) as playercon;
                        if (player != null)
                        {
                            player.StartCoroutine(WaitForJIGO3CompleteThenHandoff(oya, player, __instance, myspine));
                        }
                        else
                        {
                            jigo3HandoffStarted.Remove(oya);
                        }
                    }
                    else
                    {
                        jigo3HandoffStarted.Remove(oya);
                    }
                }
                
                return true;
            }
            
            // Process event "JIGO4" - final animation, завершение (as in original BigoniERO.cs)
            if (eventName == "JIGO4")
            {
                // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Handling JIGO4 event from {currentAnim} - final animation, ending H-scene");
                
                // As in original BigoniERO.cs: switch on JIGO4 (Loop = false), ENDflag = true
                myspine.state.SetAnimation(0, "JIGO4", false);
                myspine.timeScale = 1f;
                DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_Unconscious", 1f, null, 0f, null, false, false);
                
                // Reset counters
                var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var countField = typeof(StartBigoniERO).GetField("count", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (seCountField != null)
                {
                    seCountField.SetValue(__instance, 0);
                }
                if (countField != null)
                {
                    countField.SetValue(__instance, 0);
                }
                
                // JIGO4 now simply final animation, handoff is performed in WaitForJIGO3CompleteThenHandoff
                
                // Clear counters
                finEventCounts.Remove(oya);
                start2PlayCounts.Remove(oya);
                jigo3HandoffStarted.Remove(oya);
                
                // Continue execution original method
                return true;
            }
            
            // Process event "END" - end of animation (as in original BigoniERO.cs)
            if (eventName == "END")
            {
                // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Handling END event from {currentAnim} - animation completed");
                
                // Start coroutine for completion, if not yet started
                if (!finishingBigoniBrothers.Contains(oya))
                {
                    var playerField = typeof(StartBigoniERO).GetField("player", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (playerField != null)
                    {
                        playercon player = playerField.GetValue(__instance) as playercon;
                        if (player != null && oya != null)
                        {
                            finishingBigoniBrothers.Add(oya);
                            // Plugin.Log.LogInfo( "[BIGONI BROTHER] Starting WaitForFinishAnimationAndEnd for END event");
                            oya.StartCoroutine(WaitForFinishAnimationAndEnd(myspine, oya, player, __instance, "JIGO4"));
                        }
                    }
                }
                
                // Clear counter
                finEventCounts.Remove(oya);
                
                return true;
            }
            
            // Process event "FIN3" - if it exists (fallback)
            if (eventName == "FIN3")
            {
                // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Handling FIN3 event from {currentAnim} - final animation, ending H-scene");
                
                // Switch on FIN3 with Loop = false
                var fin3Anim = myspine.skeleton.Data.FindAnimation("FIN3");
                if (fin3Anim != null)
                {
                    myspine.state.SetAnimation(0, "FIN3", false);
                    myspine.timeScale = 1f;
                    // Plugin.Log.LogInfo( "[BIGONI BROTHER] Set FIN3 animation (Loop=false)");
                }
                
                // Stop EroVoice
                DarkTonic.MasterAudio.MasterAudio.StopBus("EroVoice");
                
                // Start coroutine for completion
                if (!finishingBigoniBrothers.Contains(oya))
                {
                    var playerField = typeof(StartBigoniERO).GetField("player", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (playerField != null)
                    {
                        playercon player = playerField.GetValue(__instance) as playercon;
                        if (player != null && oya != null)
                        {
                            finishingBigoniBrothers.Add(oya);
                            string animToWait = fin3Anim != null ? "FIN3" : "FIN2";
                            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Starting WaitForFinishAnimationAndEnd for {animToWait}");
                            oya.StartCoroutine(WaitForFinishAnimationAndEnd(myspine, oya, player, __instance, animToWait));
                        }
                    }
                }
                
                // Clear counter
                finEventCounts.Remove(oya);
                
                // Continue execution original method
                return true;
            }



            // Process sound events SE1, SE2, SE3 (as in original BigoniERO.cs:200-222)
            // These sounds are processed in BigoniERO, but for BigoniBrother we use StartBigoniERO,
            // therefore need to add processing here
            // IMPORTANT: Return false, so that block original method and avoid double processing
            if (eventName == "SE1")
            {
                if (currentAnim == "ERO" || currentAnim == "2ERO" || currentAnim == "4ERO" || currentAnim == "ERO1")
                {
                    // SE1: randomSE() - plays ero_piston5 or ero_piston3 (as in BigoniERO.cs:111-124)
                    int num = UnityEngine.Random.Range(0, 10);
                    if (num <= 5)
                    {
                        DarkTonic.MasterAudio.MasterAudio.StopAllOfSound("ero_piston5");
                        DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_piston5", 1f, null, 0f, null, false, false);
                    }
                    else
                    {
                        DarkTonic.MasterAudio.MasterAudio.StopAllOfSound("ero_piston3");
                        DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_piston3", 1f, null, 0f, null, false, false);
                    }
                    // Plugin.Log.LogInfo( $"[BIGONI BROTHER] SE1 event: Playing random piston sound for {currentAnim}");
                    return false; // Block original method, so that avoid double processing
                }
            }
            else if (eventName == "SE2")
            {
                if (currentAnim == "ERO" || currentAnim == "2ERO" || currentAnim == "4ERO" || currentAnim == "ERO1")
                {
                    // SE2: GutyuSE() - plays ero_gutyugutyu2 or mouth_gutyu2 (as in BigoniERO.cs:143-156)
                    int num = UnityEngine.Random.Range(0, 4);
                    if (num <= 1)
                    {
                        DarkTonic.MasterAudio.MasterAudio.StopAllOfSound("ero_gutyugutyu2");
                        DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_gutyugutyu2", 1f, null, 0f, null, false, false);
                    }
                    else
                    {
                        DarkTonic.MasterAudio.MasterAudio.StopAllOfSound("mouth_gutyu2");
                        DarkTonic.MasterAudio.MasterAudio.PlaySound("mouth_gutyu2", 1f, null, 0f, null, false, false);
                    }
                    // Plugin.Log.LogInfo( $"[BIGONI BROTHER] SE2 event: Playing gutyu sound for {currentAnim}");
                    return false; // Block original method, so that avoid double processing
                }
            }
            else if (eventName == "SE3")
            {
                if (currentAnim == "ERO" || currentAnim == "2ERO" || currentAnim == "4ERO" || currentAnim == "ERO1")
                {
                    // SE3: ChainSE() - plays snd_kusari_5 or snd_kusari_4 (as in BigoniERO.cs:159-176)
                    int num = UnityEngine.Random.Range(0, 4);
                    if (num <= 1)
                    {
                        DarkTonic.MasterAudio.MasterAudio.StopAllOfSound("snd_kusari_5");
                        DarkTonic.MasterAudio.MasterAudio.PlaySound("snd_kusari_5", 1f, null, 0f, null, false, false);
                    }
                    else if (num == 2)
                    {
                        // Skip (as in original)
                    }
                    else
                    {
                        DarkTonic.MasterAudio.MasterAudio.StopAllOfSound("snd_kusari_4");
                        DarkTonic.MasterAudio.MasterAudio.PlaySound("snd_kusari_4", 1f, null, 0f, null, false, false);
                    }
                    // Plugin.Log.LogInfo( $"[BIGONI BROTHER] SE3 event: Playing chain sound for {currentAnim}");
                    return false; // Block original method, so that avoid double processing
                }
            }

            // Process sound events "SE" for обычных H-анимаций (as in original BigoniERO.cs:223-301)
            // IMPORTANT: do NOT set timeScale here - let original code controls speed
            // (timeScale may change on 1.2f and 1.4f on FIN events)
            if (eventName == "SE")
            {
                // Show phrase OnGrab on first SE event on START animation
                if (currentAnim == "START")
                {
                    var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (seCountField != null)
                    {
                        int seCount = (int)seCountField.GetValue(__instance);
                        if (seCount == 0) // After increment will be 1 - show phrase
                        {
                            // Set enemy Transform
                            if (oya != null && oya.transform != null)
                            {
                                // Dialogues removed
                            }
                        }
                    }
                }
                
                // Show phrase OnCreampie on first SE event on FIN/FIN2 animation
                if (currentAnim == "FIN" || currentAnim == "FIN2")
                {
                    var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (seCountField != null)
                    {
                        int seCount = (int)seCountField.GetValue(__instance);
                        if (seCount == 0 || seCount == 1) // Show on first or second SE event
                        {
                            MindBrokenSystem.RegisterClimaxEvent(oya);
                        }
                    }
                }
                
                // Process sounds for JIGO, JIGO2, JIGO3, JIGO4 (as in original BigoniERO.cs:266-299)
                if (currentAnim == "JIGO" || currentAnim == "JIGO2" || currentAnim == "JIGO3" || currentAnim == "JIGO4")
                {
                    // In StartBigoniERO.cs:274 field se_count declared as public
                    var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                    int seCount = 0;
                    if (seCountField != null)
                    {
                        seCount = (int)seCountField.GetValue(__instance);
                        seCount++; // Increment
                        seCountField.SetValue(__instance, seCount);
                    }
                    
                    if (currentAnim == "JIGO")
                    {
                        // In original BigoniERO.cs:266-271 sound ero_Unconscious is played on se_count == 1
                        // Sound is played both on event JIGO, and on SE event on JIGO (as in original)
                        if (seCount == 1)
                        {
                            DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_Unconscious", 1f, null, 0f, null, false, false);
                            seCountField.SetValue(__instance, 0); // Reset after playing (as in original)
                            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] SE event on JIGO: Played ero_Unconscious (se_count was {seCount-1})");
                        }
                        else
                        {
                            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] SE event on JIGO: se_count={seCount} (no sound, waiting for se_count==1)");
                        }
                    }
                    else if (currentAnim == "JIGO2")
                    {
                        if (seCount == 1)
                        {
                            DarkTonic.MasterAudio.MasterAudio.StopAllOfSound("dame_kuu");
                            DarkTonic.MasterAudio.MasterAudio.PlaySound("dame_kuu", 1f, null, 0f, null, false, false);
                            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] SE event on JIGO2: Played dame_kuu");
                        }
                        else if (seCount == 2)
                        {
                            DarkTonic.MasterAudio.MasterAudio.StopAllOfSound("snd_down");
                            DarkTonic.MasterAudio.MasterAudio.PlaySound("snd_down", 1f, null, 0f, null, false, false);
                            seCountField.SetValue(__instance, 0);
                            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] SE event on JIGO2: Played snd_down");
                        }
                    }
                    else if (currentAnim == "JIGO3" || currentAnim == "JIGO4")
                    {
                        if (seCount == 1)
                        {
                            DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_Unconscious", 1f, null, 0f, null, false, false);
                            seCountField.SetValue(__instance, 0);
                            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] SE event on {currentAnim}: Played ero_Unconscious");
                        }
                    }
                    
                    return false; // Block original method for JIGO animations
                }
                else if (currentAnim == "ERO" || currentAnim == "2ERO")
                {
                    // In original BigoniERO.cs:223-301 SE processing depends on animation:
                    // - ERO: se_count == 1 → "ero_now12" (BigoniERO.cs:230-234)
                    // - 2ERO: se_count == 1 → "ero_now11" (BigoniERO.cs:236-241) - this creampie sound!
                    // For BigoniBrother need to process sounds here, since it uses StartBigoniERO, and not BigoniERO
                    
                    // Get se_count BEFORE increment (original method also increments)
                    // In StartBigoniERO.cs:274 field se_count declared as public
                    var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                    if (seCountField == null)
                    {
                        return true; // Continue execution original method
                    }
                    
                    int seCount = 0;
                    try
                    {
                        seCount = (int)seCountField.GetValue(__instance);
                    }
                    catch (System.Exception ex)
                    {
                        return true; // Continue execution original method
                    }
                    
                    // Process sounds depending on animation (as in BigoniERO.cs:223-301)
                    // seCount will be incremented by original method, therefore we check seCount == 0 (until increment)
                    if (currentAnim == "ERO")
                    {
                        if (seCount == 0) // After increment in original will 1
                        {
                            // Allow original method to increment se_count, then process sound
                            // But since we return true, original method will execute and increments
                            // Therefore need to use Postfix or return false and handle ourselves
                            // Better return false and handle ourselves, to avoid double increment
                            seCountField.SetValue(__instance, 1); // Set to 1 (as after increment)
                            DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_now12", 1f, null, 0f, null, false, false);
                            seCountField.SetValue(__instance, 0); // Reset after playing
                            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] SE event on ERO: Played ero_now12 (se_count was {seCount})");
                            return false; // Block original method for ERO
                        }
                    }
                    else if (currentAnim == "2ERO")
                    {
                        // In original BigoniERO.cs:236-241 on event SE on 2ERO is played ero_now11 on se_count == 1
                        // From original Bigoni logs it is clear, that ero_now11 is played on EVERY SE event on 2ERO, when se_count==1
                        // Increment se_count
                        if (seCountField == null)
                        {
                            return true; // Continue execution original method
                        }
                        
                        int newSeCount = seCount + 1;
                        seCountField.SetValue(__instance, newSeCount);
                        
                        // If se_count == 1 (after increment), play ero_now11 (as in original BigoniERO.cs:236-241)
                        if (newSeCount == 1)
                        {
                            DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_now11", 1f, null, 0f, null, false, false);
                            seCountField.SetValue(__instance, 0); // Reset after playing (as in original)
                            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] SE event on 2ERO: Played ero_now11 (creampie sound, se_count was {seCount})");
                        }
                        else
                        {
                            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] SE event on 2ERO: se_count={newSeCount} (no sound, waiting for se_count==1)");
                        }
                        return false; // Block original method for 2ERO
                    }
                    
                    // For other animations continue execution original method
                    return true;
                }
                else if (currentAnim == "FIN" || currentAnim == "FIN2")
                {
                    // Process sounds for FIN and FIN2 (as in original BigoniERO.cs:243-265)
                    // In StartBigoniERO.cs:274 field se_count declared as public
                    var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                    if (seCountField == null)
                    {
                        return true; // Continue execution original method
                    }
                    
                    int seCount = 0;
                    seCount = (int)seCountField.GetValue(__instance);
                    seCount++; // Increment
                    seCountField.SetValue(__instance, seCount);
                    
                    if (currentAnim == "FIN")
                    {
                        // In original BigoniERO.cs:243-252:
                        // se_count == 1 → ero_now11
                        // se_count == 2 → ero_enemy_syasei1 (via Seset)
                        if (seCount == 1)
                        {
                            DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_now11", 1f, null, 0f, null, false, false);
                            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] SE event on FIN: Played ero_now11 (se_count was {seCount-1})");
                        }
                        else if (seCount == 2)
                        {
                            // In original Seset is called("ero_enemy_syasei1"), but for simplicity just play sound
                            DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_enemy_syasei1", 1f, null, 0f, null, false, false);
                            seCountField.SetValue(__instance, 0); // Reset after playing
                            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] SE event on FIN: Played ero_enemy_syasei1 (se_count was {seCount-1})");
                        }
                    }
                    else if (currentAnim == "FIN2")
                    {
                        // In original BigoniERO.cs:254-264:
                        // se_count == 1 → ero_now11
                        // se_count == 2 → randomSE() + ero_enemy_syasei1 (via Seset)
                        if (seCount == 1)
                        {
                            DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_now11", 1f, null, 0f, null, false, false);
                            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] SE event on FIN2: Played ero_now11 (se_count was {seCount-1})");
                        }
                        else if (seCount == 2)
                        {
                            // In original is called randomSE() + Seset("ero_enemy_syasei1")
                            // randomSE() plays random sound from ero_piston5/ero_piston3
                            int num = UnityEngine.Random.Range(0, 10);
                            if (num <= 5)
                            {
                                DarkTonic.MasterAudio.MasterAudio.StopAllOfSound("ero_piston5");
                                DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_piston5", 1f, null, 0f, null, false, false);
                            }
                            else
                            {
                                DarkTonic.MasterAudio.MasterAudio.StopAllOfSound("ero_piston3");
                                DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_piston3", 1f, null, 0f, null, false, false);
                            }
                            DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_enemy_syasei1", 1f, null, 0f, null, false, false);
                            seCountField.SetValue(__instance, 0); // Reset after playing
                            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] SE event on FIN2: Played randomSE() + ero_enemy_syasei1 (se_count was {seCount-1})");
                        }
                    }
                    
                    return false; // Block original method for FIN and FIN2
                }
                else if (currentAnim == "4ERO" || currentAnim == "ERO1")
                {
                    // For 4ERO and ERO1 continue execution original method (StartBigoniERO handles them)
                    return true;
                }
            }

            // For all other events continue execution original method
            return true;
        }
        catch (System.Exception ex)
        {
            return true; // In case of error continue execution original method
        }
    }

    /// <summary>
    /// Coroutine for waiting completion final animation и then completion of H-scene
    /// For 4EROJIGO wait several cycles (creampie), then release GG
    /// </summary>
    private static IEnumerator WaitForFinishAnimationAndEnd(Spine.Unity.SkeletonAnimation myspine, Bigoni oya, playercon player, StartBigoniERO ero, string finishAnimName)
    {
        // Plugin.Log.LogInfo( $"[BIGONI BROTHER] WaitForFinishAnimationAndEnd: Waiting for {finishAnimName} animation to complete");
        
        // For JIGO4, JIGO3, JIGO2, JIGO, FIN3 or FIN2 wait for completion animation (not looped)
        if (finishAnimName == "JIGO4" || finishAnimName == "JIGO3" || finishAnimName == "JIGO2" || finishAnimName == "JIGO" || finishAnimName == "FIN3" || finishAnimName == "FIN2")
        {
            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] WaitForFinishAnimationAndEnd: {finishAnimName} is not looped, waiting for animation sequence to complete");
            float maxWaitTime = 30f; // Increase total wait time for entire sequence JIGO → JIGO2 → JIGO3 → JIGO4 → END
            float elapsedTime = 0f;
            string lastAnim = finishAnimName;
            bool reachedTargetAnim = false;
            
            // For JIGO4 wait for completion JIGO4
            string targetAnim = "JIGO4";
            
            // Ждем, until we reach target animation and it does not complete, or max time elapses
            while (myspine != null && myspine.AnimationState != null && elapsedTime < maxWaitTime)
            {
                var track = myspine.AnimationState.GetCurrent(0);
                if (track != null && track.Animation != null)
                {
                    string currentAnim = track.Animation.Name;
                    
                    // If animation changed, log transition
                    if (currentAnim != lastAnim)
                    {
                        // Plugin.Log.LogInfo( $"[BIGONI BROTHER] WaitForFinishAnimationAndEnd: Animation transitioned from {lastAnim} to {currentAnim} (Time={track.Time:F2}/{track.Animation.Duration:F2}, Loop={track.Loop})");
                        lastAnim = currentAnim;
                    }
                    
                    // If reached target animation (JIGO3 or JIGO4), mark this
                    if (currentAnim == targetAnim)
                    {
                        if (!reachedTargetAnim)
                        {
                            reachedTargetAnim = true;
                            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] WaitForFinishAnimationAndEnd: Reached {targetAnim} animation, waiting for completion");
                        }
                        
                        // If target animation finished (not looped и reached end)
                        if (!track.Loop && track.Time >= track.Animation.Duration - 0.1f)
                        {
                            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] WaitForFinishAnimationAndEnd: {targetAnim} animation completed (Time={track.Time:F2}, Duration={track.Animation.Duration:F2})");
                            break;
                        }
                    }
                    
                    // If animation ended and we are not in target animation, but it was part of sequence
                    if ((currentAnim == "JIGO" || currentAnim == "JIGO2" || (currentAnim == "JIGO3" && targetAnim != "JIGO3")) && !track.Loop && track.Time >= track.Animation.Duration - 0.1f)
                    {
                        // Plugin.Log.LogInfo( $"[BIGONI BROTHER] WaitForFinishAnimationAndEnd: {currentAnim} completed, waiting for next in sequence (Time={track.Time:F2}, Duration={track.Animation.Duration:F2})");
                        // Continue waiting next animation
                    }
                }
                
                elapsedTime += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
            
            if (elapsedTime >= maxWaitTime)
            {
                // Plugin.Log.LogInfo( $"[BIGONI BROTHER] WaitForFinishAnimationAndEnd: Timeout waiting for animation sequence to complete (reachedTargetAnim={reachedTargetAnim}), proceeding with cleanup");
            }
        }
        else
        {
            // For other animations wait for completion
            float maxWaitTime = 10f; // Maximum wait time (10 seconds)
            float elapsedTime = 0f;
            
            // Wait until animation completes or max time elapses
            while (myspine != null && myspine.AnimationState != null && elapsedTime < maxWaitTime)
            {
                var track = myspine.AnimationState.GetCurrent(0);
                if (track != null && track.Animation != null)
                {
                    string currentAnim = track.Animation.Name;
                    
                    // If animation changed (final animation finished)
                    if (currentAnim != finishAnimName)
                    {
                        // Plugin.Log.LogInfo( $"[BIGONI BROTHER] WaitForFinishAnimationAndEnd: {finishAnimName} animation completed, currentAnim={currentAnim}");
                        break;
                    }
                    
                    // If animation finished (track.Time >= duration and not looped)
                    if (!track.Loop && track.Time >= track.Animation.Duration - 0.1f)
                    {
                        // Plugin.Log.LogInfo( $"[BIGONI BROTHER] WaitForFinishAnimationAndEnd: {finishAnimName} animation reached end (Time={track.Time:F2}, Duration={track.Animation.Duration:F2})");
                        break;
                    }
                }
                
                elapsedTime += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
            
            if (elapsedTime >= maxWaitTime)
            {
                // Plugin.Log.LogInfo( $"[BIGONI BROTHER] WaitForFinishAnimationAndEnd: Timeout waiting for {finishAnimName} animation, ending anyway");
            }
        }
        
        // Start completion H-scene
        // Plugin.Log.LogInfo( $"[BIGONI BROTHER] WaitForFinishAnimationAndEnd: Starting EndBigoniBrotherAnimation (finishAnimName={finishAnimName})");
        finishingBigoniBrothers.Remove(oya); // Remove from HashSet before completion
        finEventCounts.Remove(oya); // Clear counter FIN events
        start2PlayCounts.Remove(oya); // Clear counter START2
        
        if (oya != null && player != null && ero != null)
        {
            // Plugin.Log.LogInfo( "[BIGONI BROTHER] WaitForFinishAnimationAndEnd: All objects valid, starting EndBigoniBrotherAnimation coroutine");
            oya.StartCoroutine(EndBigoniBrotherAnimation(oya, player, ero));
        }
        else
        {
        }
    }

    /// <summary>
    /// Coroutine for completion of H-animation BigoniBrother
    /// </summary>
    private static IEnumerator EndBigoniBrotherAnimation(Bigoni oya, playercon player, StartBigoniERO ero)
    {
        // Wait minimum time for completion animation (0.1 seconds - almost immediately)
        // Speed up even more: was 0.5 + 0.5 = 1 second, now 0.1 + 0.1 = 0.2 seconds
        yield return new WaitForSeconds(0.1f);

        try
        {
            // First hide erodata (H-animation), so that see release ГГ
            var erodataField = typeof(Bigoni).GetField("erodata", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (erodataField != null)
            {
                GameObject erodata = erodataField.GetValue(oya) as GameObject;
                if (erodata != null)
                {
                    erodata.SetActive(false);
                    // Plugin.Log.LogInfo( "[BIGONI BROTHER] Hidden erodata (H-animation)");
                }
            }
            
            // Show enemy back
            var myspinerennderField = typeof(Bigoni).GetField("myspinerennder", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (myspinerennderField != null)
            {
                MeshRenderer myspinerennder = myspinerennderField.GetValue(oya) as MeshRenderer;
                if (myspinerennder != null)
                {
                    myspinerennder.enabled = true;
                }
            }

            // Show UI enemy
            var uIField = typeof(Bigoni).GetField("UI", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (uIField != null)
            {
                GameObject UI = uIField.GetValue(oya) as GameObject;
                if (UI != null)
                {
                    UI.SetActive(true);
                }
            }
            
            // Reset camera
            oya.ero_camerareset();
            
            // Push GG away дальше и возвращаем in normal state
            // Get position enemy and player
            Vector2 enemyPos = oya.transform.position;
            Vector2 playerPos = player.transform.position;
            Vector2 direction = (playerPos - enemyPos).normalized;
            
            // If GG too close to enemy, push away up and right
            if (Vector2.Distance(enemyPos, playerPos) < 2f)
            {
                direction = new Vector2(1f, 1f).normalized; // Откидываем right и up
            }
            
            // Push GG away дальше (используем rigi2d if exists)
            var rigi2dField = typeof(playercon).GetField("rigi2d", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (rigi2dField != null)
            {
                UnityEngine.Rigidbody2D rigi2d = rigi2dField.GetValue(player) as UnityEngine.Rigidbody2D;
                if (rigi2d != null)
                {
                    // Откидываем ГГ дальше with force
                    rigi2d.velocity = direction * 8f; // Push force
                    // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Threw player away with velocity {rigi2d.velocity}");
                }
            }
            
            // Transition GG in DOWN state for передачи (используем logic from GoblinPassLogic)
            // Set flags as in handoff logic goblin
            player.eroflag = false;
            player._eroflag2 = false;
            
            // Use handoff logic: set erodown = 1 and state = "DOWN"
            // (as in GoblinPassLogic.PushPlayerAwayFromEnemy, lines 354-360)
            var eroDownField = typeof(playercon).GetField("erodown", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (eroDownField != null)
            {
                eroDownField.SetValue(player, 1);
            }
            
            var stateField = typeof(playercon).GetField("state", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (stateField != null)
            {
                stateField.SetValue(player, "DOWN");
            }
            
            // Set GG animation on DOWN (as in GoblinPassLogic, lines 338-351)
            var playerSpine = player.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
            if (playerSpine != null)
            {
                try
                {
                    playerSpine.AnimationState.ClearTracks();
                    string[] downAnims = { "DOWN", "down", "Idle", "idle" };
                    foreach (string animName in downAnims)
                    {
                        try
                        {
                            playerSpine.AnimationState.SetAnimation(0, animName, true);
                            break;
                        }
                        catch (System.Exception ex)
                        {
                        }
                    }
                }
                catch (System.Exception ex)
                {
                }
            }
            
            // Plugin.Log.LogInfo( "[BIGONI BROTHER] Player set to DOWN state (erodown=1, state=DOWN) for transfer");
            
            // Reset state enemy
            oya.eroflag = false;

            // Reset camera
            oya.ero_camerareset();
            
            // Reset counters
            var countField = typeof(StartBigoniERO).GetField("count", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (countField != null)
            {
                countField.SetValue(ero, 0);
            }

            var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (seCountField != null)
            {
                seCountField.SetValue(ero, 0);
            }

            // Set ГГ in DOWN state СРАЗУ (until handoff)
            // Use already existing fields from method above
            if (eroDownField != null)
            {
                eroDownField.SetValue(player, 1);
                // Plugin.Log.LogInfo( "[BIGONI BROTHER] erodown set to 1 (DOWN state) - immediate");
            }
            
            if (stateField != null)
            {
                stateField.SetValue(player, "DOWN");
                // Plugin.Log.LogInfo( "[BIGONI BROTHER] state set to 'DOWN' - immediate");
            }
            
            // Set animation DOWN
            var playerSpineDown = player.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
            if (playerSpineDown != null)
            {
                playerSpineDown.AnimationState.SetAnimation(0, "DOWN", true);
                // Plugin.Log.LogInfo( "[BIGONI BROTHER] Player animation set to 'DOWN' - immediate");
            }
            
            // Use DelayedHandoffScript for передачи without delay (immediately after JIGO4)
            // Start handoff via coroutine on Bigoni (MonoBehaviour)
            if (oya != null)
            {
                oya.StartCoroutine(DelayedHandoffCoroutine(oya, 0f));
                // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Started immediate handoff with 0s delay (right after JIGO4)");
            }
            else
            {
                // Fallback: create temp GameObject
                GameObject temp = new GameObject("DelayedHandoffTemp");
                var script = temp.AddComponent<NoREroMod.DelayedHandoffScript>();
                script.StartDelayedHandoff(oya);
                // Plugin.Log.LogInfo( "[BIGONI BROTHER] Using temp GameObject for DelayedHandoffScript");
            }
            
            // Plugin.Log.LogInfo( "[BIGONI BROTHER] H-animation ended, player set to DOWN state, handoff initiated");
            
            // Clear HashSet and counters событий after completion
            finishingBigoniBrothers.Remove(oya);
            finEventCounts.Remove(oya);
            twoEroEventCounts.Remove(oya);
            start2PlayCounts.Remove(oya);
               }
               catch (System.Exception ex)
               {
                  // Clear HashSet и counter FIN events even on error
                  finishingBigoniBrothers.Remove(oya);
                  finEventCounts.Remove(oya);
                  twoEroEventCounts.Remove(oya);
                  start2PlayCounts.Remove(oya);
               }
           }
    
    /// <summary>
    /// Coroutine for delayed handoff with минимальной задержкой
    /// </summary>
    private static IEnumerator DelayedHandoffCoroutine(Bigoni oya, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        BigoniBrotherPassLogic.ExecuteHandoff(oya);
    }
    
    /// <summary>
    /// Public method for invoking handoff (used by DelayedHandoffScript)
    /// Delegates call in BigoniBrotherPassLogic
    /// </summary>
    public static void ExecuteHandoff(object enemyInstance)
    {
        BigoniBrotherPassLogic.ExecuteHandoff(enemyInstance);
    }
    
    /// <summary>
    /// Waits for completion animation JIGO3, then performs handoff and spawn goblins without darkening.
    /// Handoff happens only after full completion ero-animation BigoniBrother.
    /// </summary>
    private static System.Collections.IEnumerator WaitForJIGO3CompleteThenHandoff(Bigoni oya, playercon player, StartBigoniERO ero, Spine.Unity.SkeletonAnimation myspine)
    {
        Plugin.Log.LogInfo("[BIGONI BROTHER] WaitForJIGO3CompleteThenHandoff started - waiting for JIGO3 animation to finish");
        
        // Wait for completion animation JIGO3 (Loop = false)
        float jigo3Duration = 3f;
        if (myspine != null && myspine.skeleton != null && myspine.skeleton.Data != null)
        {
            var jigo3Anim = myspine.skeleton.Data.FindAnimation("JIGO3");
            if (jigo3Anim != null)
            {
                jigo3Duration = jigo3Anim.Duration;
                Plugin.Log.LogInfo($"[BIGONI BROTHER] JIGO3 animation duration: {jigo3Duration:F2}s");
            }
        }
        
        yield return new WaitForSeconds(jigo3Duration);
        
        Plugin.Log.LogInfo("[BIGONI BROTHER] JIGO3 completed - executing handoff and spawning goblins (no fade)");
        
        // Execute handoff immediately without darkening
        if (oya != null && player != null && ero != null)
        {
            oya.StartCoroutine(EndBigoniBrotherAnimation(oya, player, ero));
        }
        
        yield return new WaitForSeconds(0.3f);
        
        // Reset state игрока before spawn goblin
        if (player != null)
        {
            player.eroflag = false;
            player._eroflag2 = false;
            
            // Восстанавливаем физику (иначе ГГ застревает: not двигается, атаки проходят сквозь)
            if (player.rigi2d != null && !player.rigi2d.simulated)
                player.rigi2d.simulated = true;
            
            var eroDownField = typeof(playercon).GetField("erodown", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (eroDownField != null) eroDownField.SetValue(player, 1);
            
            var stateField = typeof(playercon).GetField("state", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (stateField != null)
            {
                stateField.SetValue(player, "DOWN");
                Plugin.Log.LogInfo("[BIGONI BROTHER] Player state set to 'DOWN' for goblin H-scene");
            }
            
            player.Attacknow = false;
            player.Actstate = false;
            player.stepfrag = false;
            player.magicnow = false;
            player.guard = false;
            
            var playerSpine = player.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
            if (playerSpine != null)
            {
                try
                {
                    playerSpine.AnimationState.ClearTracks();
                    playerSpine.AnimationState.SetAnimation(0, "DOWN", true);
                }
                catch { }
            }
        }
        
        // Деактивируем черный фон before spawn
        try
        {
            if (NoREroMod.Systems.Effects.HSceneBlackBackgroundSystem.IsActive)
            {
                NoREroMod.Systems.Effects.HSceneBlackBackgroundSystem.Deactivate();
            }
        }
        catch { }
        
        yield return new WaitForSeconds(0.2f);
        
        SpawnGoblinsForHandoff(player);
        Plugin.Log.LogInfo("[BIGONI BROTHER] Handoff and goblin spawn completed (no fade)");
        
        // Clear guard
        if (oya != null) jigo3HandoffStarted.Remove(oya);
    }
    
    /// <summary>
    /// [DEPRECATED] Старая coroutine with затемнением - left for compatibility. Используется WaitForJIGO3CompleteThenHandoff.
    /// </summary>
    private static System.Collections.IEnumerator FadeAndSpawnGoblinsOnJIGO4(Bigoni oya, playercon player, StartBigoniERO ero)
    {
        Plugin.Log.LogInfo("[BIGONI BROTHER] FadeAndSpawnGoblinsOnJIGO4 coroutine started (5 seconds total)");
        
        // Находим fade объект
        GameObject fadeObj = GameObject.Find("UIeffect");
        GameObject canvasObj = GameObject.Find("Canvas");
        
        if (fadeObj == null)
        {
            Plugin.Log.LogWarning("[BIGONI BROTHER] UIeffect not found, trying Canvas");
            fadeObj = canvasObj;
        }
        
        if (fadeObj == null)
        {
            Plugin.Log.LogWarning("[BIGONI BROTHER] UIeffect and Canvas not found, trying alternative search");
            fadeObj = GameObject.FindGameObjectWithTag("Fade");
        }
        
        if (fadeObj == null)
        {
            Plugin.Log.LogError("[BIGONI BROTHER] Fade object not found! Cannot start fade effect.");
            yield break;
        }
        
        Plugin.Log.LogInfo($"[BIGONI BROTHER] Found fade object: {fadeObj.name}");
        
        // Проверяем все fade components on объекте и дочернtheir объектах
        FadeINandOUT fadeComponent = null;
        fadein_out fadeinOutComponent = null;
        
        // Проверяем on основном объекте
        if (fadeObj != null)
        {
            fadeComponent = fadeObj.GetComponent<FadeINandOUT>();
            fadeinOutComponent = fadeObj.GetComponent<fadein_out>();
            
            // If not found, проверяем дочерние объекты
            if (fadeComponent == null && fadeinOutComponent == null)
            {
                fadeComponent = fadeObj.GetComponentInChildren<FadeINandOUT>();
                fadeinOutComponent = fadeObj.GetComponentInChildren<fadein_out>();
            }
        }
        
        // Также проверяем Canvas
        if ((fadeComponent == null && fadeinOutComponent == null) && canvasObj != null)
        {
            fadein_out canvasFade = canvasObj.GetComponent<fadein_out>();
            if (canvasFade != null)
            {
                fadeinOutComponent = canvasFade;
                fadeObj = canvasObj;
                Plugin.Log.LogInfo("[BIGONI BROTHER] Found fadein_out component on Canvas");
            }
        }
        
        if (fadeComponent == null && fadeinOutComponent == null)
        {
            Plugin.Log.LogError("[BIGONI BROTHER] No fade component found on UIeffect or Canvas!");
            yield break;
        }
        
        if (fadeinOutComponent != null)
        {
            Plugin.Log.LogInfo("[BIGONI BROTHER] Found fadein_out component");
        }
        if (fadeComponent != null)
        {
            Plugin.Log.LogInfo("[BIGONI BROTHER] Found FadeINandOUT component");
        }
        
        // Запускаем затемнение (fade in - плавное затемнение за 4 сек)
        if (fadeComponent != null)
        {
            try
            {
                // Активируем imageobj if нужно
                var imageobjField = typeof(FadeINandOUT).GetField("imageobj", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (imageobjField != null)
                {
                    GameObject imageobj = imageobjField.GetValue(fadeComponent) as GameObject;
                    if (imageobj != null && !imageobj.activeSelf)
                    {
                        imageobj.SetActive(true);
                        Plugin.Log.LogInfo("[BIGONI BROTHER] Activated fade imageobj");
                    }
                }
                
                fadeComponent._fade_now = true;
                fadeComponent._enable = false;
                fadeComponent._alphacount = 0f;
                var valueField = typeof(FadeINandOUT).GetField("value", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (valueField != null)
                {
                    // Плавное затемнение за 2.5 сек: 1.0 / 2.5 = 0.4 (быстрее, but плавное)
                    valueField.SetValue(fadeComponent, 0.4f);
                    Plugin.Log.LogInfo("[BIGONI BROTHER] Started fade in (smooth darkening over 2.5 seconds)");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[BIGONI BROTHER] Error setting fade in: {ex.Message}\n{ex.StackTrace}");
            }
        }
        else if (fadeinOutComponent != null)
        {
            try
            {
                fadeinOutComponent.on();
                Plugin.Log.LogInfo("[BIGONI BROTHER] Started fadein_out.on()");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[BIGONI BROTHER] Error setting fadein_out on: {ex.Message}\n{ex.StackTrace}");
            }
        }
        else
        {
            Plugin.Log.LogError("[BIGONI BROTHER] No fade component found! Cannot start fade effect.");
            yield break;
        }
        
        // Wait 2.5 seconds (полное плавное затемнение)
        Plugin.Log.LogInfo("[BIGONI BROTHER] Waiting 2.5 seconds for full smooth darkening...");
        yield return new WaitForSeconds(2.5f);
        
        // Теперь экран полностью темный - выполняем передачу во время темного экрана
        Plugin.Log.LogInfo("[BIGONI BROTHER] Screen is fully dark, executing handoff now...");
        if (oya != null && player != null && ero != null)
        {
            // Execute handoff (handoff) во время темного экрана
            oya.StartCoroutine(EndBigoniBrotherAnimation(oya, player, ero));
        }
        
        // Wait немного, so that передача finished
        yield return new WaitForSeconds(0.3f);
        
        // Ensure state игрока полностью сброшеbut before spawn goblin
        // This критичbut for предотвращения зависаний on ERO_iki
        if (player != null)
        {
            // Дополнительная проверка и reset состояния игрока
            player.eroflag = false;
            player._eroflag2 = false;
            
            
            // Ensure erodown = 1 (DOWN state)
            var eroDownField = typeof(playercon).GetField("erodown", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (eroDownField != null)
            {
                eroDownField.SetValue(player, 1);
            }
            
            // CRITICAL: Устанавливаем player.state = "DOWN" for correctlyй инициализации H-scene goblinми
            // Гоблины проверяют this.com_player.state == "DOWN" in OnTriggerStay2D before установкой eroflag = true
            var stateField = typeof(playercon).GetField("state", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (stateField != null)
            {
                stateField.SetValue(player, "DOWN");
                Plugin.Log.LogInfo("[BIGONI BROTHER] Player state set to 'DOWN' for goblin H-scene initialization");
            }
            
            // Reset блокирующие флаги боевых действий (as in BigoniBrotherPassLogic)
            player.Attacknow = false;
            player.Actstate = false;
            player.stepfrag = false;
            player.magicnow = false;
            player.guard = false;
            
            // Clear animation игрока еще раз for reliability
            var playerSpine = player.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
            if (playerSpine != null)
            {
                try
                {
                    playerSpine.AnimationState.ClearTracks();
                    // Set animation DOWN for визуального соответствия состоянию
                    playerSpine.AnimationState.SetAnimation(0, "DOWN", true);
                }
                catch { }
            }
        }
        
        // CRITICAL: Принудительbut деактивируем черный фон before spawn goblin
        // Active black background может block events animation goblins (ERO_iki/ERO_iki2)
        try
        {
            // Use прямой call for reliability
            if (NoREroMod.Systems.Effects.HSceneBlackBackgroundSystem.IsActive)
            {
                NoREroMod.Systems.Effects.HSceneBlackBackgroundSystem.Deactivate();
                Plugin.Log.LogInfo("[BIGONI BROTHER] Deactivated black background before spawning goblins (direct call)");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[BIGONI BROTHER] Error deactivating black background: {ex.Message}");
        }
        
        // CRITICAL: Принудительbut деактивируем fade-component before spawn goblin
        // Активный fade может block events animation goblin
        try
        {
            if (fadeComponent != null)
            {
                fadeComponent._fade_now = false;
                fadeComponent._enable = false;
                fadeComponent._alphacount = 0f;
                
                // Деактивируем imageobj if он есть
                var imageobjField = typeof(FadeINandOUT).GetField("imageobj", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (imageobjField != null)
                {
                    GameObject imageobj = imageobjField.GetValue(fadeComponent) as GameObject;
                    if (imageobj != null && imageobj.activeSelf)
                    {
                        imageobj.SetActive(false);
                        Plugin.Log.LogInfo("[BIGONI BROTHER] Deactivated fade imageobj before spawning goblins");
                    }
                }
                Plugin.Log.LogInfo("[BIGONI BROTHER] Fade component deactivated before spawning goblins");
            }
            
            if (fadeinOutComponent != null && fadeObj != null)
            {
                // Деактивируем imageobj on fadein_out componentе
                var imageobjField = typeof(fadein_out).GetField("imageobj", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (imageobjField != null)
                {
                    GameObject imageobj = imageobjField.GetValue(fadeinOutComponent) as GameObject;
                    if (imageobj != null && imageobj.activeSelf)
                    {
                        imageobj.SetActive(false);
                        Plugin.Log.LogInfo("[BIGONI BROTHER] Deactivated fadein_out imageobj before spawning goblins");
                    }
                }
                
                // If fadeObj this UIeffect (not Canvas), деактивируем its полностью
                if (fadeObj.name == "UIeffect" && fadeObj.activeSelf)
                {
                    fadeObj.SetActive(false);
                    Plugin.Log.LogInfo("[BIGONI BROTHER] Deactivated UIeffect GameObject before spawning goblins");
                }
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[BIGONI BROTHER] Error deactivating fade before spawning: {ex.Message}");
        }
        
        // CRITICAL: Ensure глобальный Time.timeScale = 1f before spawn goblin
        // Fade-эффект or другие системы могли изменить его, that замедляет animation
        if (Time.timeScale != 1f)
        {
            Time.timeScale = 1f;
            Plugin.Log.LogInfo($"[BIGONI BROTHER] Reset Time.timeScale to 1f (was {Time.timeScale}) before spawning goblins");
        }
        
        // Wait еще немного, so that state игрока полностью установилось
        yield return new WaitForSeconds(0.2f);
        

        // Финальная проверка состояния игрока before spawn goblin
        if (player != null)
        {
            // Ensure все флаги сброшены и state корректное
            player.eroflag = false;
            player._eroflag2 = false;

            // Проверяем и устанавливаем state = "DOWN" еще раз
            var stateField = typeof(playercon).GetField("state", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (stateField != null)
            {
                stateField.SetValue(player, "DOWN");
            }

            // Финальное восстановление физики
            if (player.rigi2d != null)
            {
                player.rigi2d.simulated = true;
                player.rigi2d.velocity = Vector2.zero;
                player.rigi2d.angularVelocity = 0f;
            }

            var eroDownField = typeof(playercon).GetField("erodown", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (eroDownField != null)
            {
                eroDownField.SetValue(player, 1);
            }

            Plugin.Log.LogInfo("[BIGONI BROTHER] Final player state check - ready for goblin spawn");
        }

        // Спавним 3 goblins after передачи
        Plugin.Log.LogInfo("[BIGONI BROTHER] Handoff completed in darkness. Spawning 3 goblins...");
        SpawnGoblinsForHandoff(player);
        
        // Wait немного before осветлением
        yield return new WaitForSeconds(0.5f);
        
        // Сразу начинаем осветление (экран был темным достаточbut долго)
        Plugin.Log.LogInfo("[BIGONI BROTHER] Starting fade out (smooth lightening over 2.5 seconds)...");
        
        // Проверяем fadeComponent и fadeinOutComponent еще раз (on случай if они были потеряны)
        if (fadeComponent == null && fadeObj != null)
        {
            fadeComponent = fadeObj.GetComponent<FadeINandOUT>();
            Plugin.Log.LogInfo("[BIGONI BROTHER] Re-checked fadeComponent for fade out");
        }
        
        if (fadeinOutComponent == null && fadeObj != null)
        {
            fadeinOutComponent = fadeObj.GetComponent<fadein_out>();
            Plugin.Log.LogInfo("[BIGONI BROTHER] Re-checked fadeinOutComponent for fade out");
        }
        
        // Возвращаем свет (fade out - плавное осветление за 2.5 сек)
        if (fadeComponent != null)
        {
            try
            {
                fadeComponent._fade_now = true;
                fadeComponent._enable = true;
                fadeComponent._alphacount = 1f;
                var valueField = typeof(FadeINandOUT).GetField("value", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (valueField != null)
                {
                    // Плавное осветление за 2.5 сек: 1.0 / 2.5 = 0.4 (быстрее, but плавное)
                    valueField.SetValue(fadeComponent, 0.4f);
                    Plugin.Log.LogInfo("[BIGONI BROTHER] Started fade out (smooth lightening over 2.5 seconds)");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[BIGONI BROTHER] Error setting fade out: {ex.Message}\n{ex.StackTrace}");
            }
            
            // Wait 2.5 seconds for плавного осветления (outside try-catch)
            yield return new WaitForSeconds(2.5f);
            
            // Force убеждаемся, that осветление завершено
            try
            {
                fadeComponent._alphacount = 0f;
                fadeComponent._fade_now = false;
                fadeComponent._enable = false;
                Plugin.Log.LogInfo("[BIGONI BROTHER] Fade out completed, screen should be light now");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[BIGONI BROTHER] Error completing fade out: {ex.Message}\n{ex.StackTrace}");
            }
        }
        else if (fadeinOutComponent != null)
        {
            // ПРОСТОЕ SOLUTION: деактивируем сам GameObject with fade componentом (as in Unity Explorer)
            // This most reliable способ - просто убрать галочку with объекта
            try
            {
                Plugin.Log.LogInfo("[BIGONI BROTHER] Deactivating fade GameObject to lighten screen");
                
                // Деактивируем сам fadeObj (UIeffect or Canvas)
                if (fadeObj != null && fadeObj.activeSelf)
                {
                    fadeObj.SetActive(false);
                    Plugin.Log.LogInfo($"[BIGONI BROTHER] Deactivated fade GameObject: {fadeObj.name}");
                }
                
                // Также деактивируем Canvas, if он отдельный
                if (canvasObj != null && canvasObj != fadeObj && canvasObj.activeSelf)
                {
                    // Но Canvas лучше not деактивировать полностью, only fade component on нем
                    fadein_out canvasFade = canvasObj.GetComponent<fadein_out>();
                    if (canvasFade != null)
                    {
                        // Деактивируем only imageobj on Canvas
                        var imageobjField = typeof(fadein_out).GetField("imageobj", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (imageobjField != null)
                        {
                            GameObject imageobj = imageobjField.GetValue(canvasFade) as GameObject;
                            if (imageobj != null && imageobj.activeSelf)
                            {
                                imageobj.SetActive(false);
                                Plugin.Log.LogInfo("[BIGONI BROTHER] Deactivated fade imageobj on Canvas");
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[BIGONI BROTHER] Error deactivating fade GameObject: {ex.Message}\n{ex.StackTrace}");
            }
        }
        else
        {
            Plugin.Log.LogError("[BIGONI BROTHER] No fade component found for fade out! Cannot lighten screen.");
        }
        
        Plugin.Log.LogInfo("[BIGONI BROTHER] FadeAndSpawnGoblinsOnJIGO4 coroutine completed (5 seconds total)");
    }
    
    /// <summary>
    /// Отключает preexisting goblins within radius from ГГ. Prevents double ero-animation
    /// (when already standing goblin grabs ГГ simultaneously со spawned by us).
    /// </summary>
    private static void DisableNearbyGoblins(playercon player, float radius = 12f)
    {
        try
        {
            if (player == null) return;
            Vector2 playerPos = player.transform.position;
            goblin[] allGoblins = UnityEngine.Object.FindObjectsOfType<goblin>();
            int disabled = 0;
            foreach (var g in allGoblins)
            {
                if (g == null || g.gameObject == null || !g.gameObject.activeInHierarchy) continue;
                float dist = Vector2.Distance(playerPos, g.transform.position);
                if (dist <= radius)
                {
                    g.gameObject.SetActive(false);
                    disabled++;
                }
            }
            if (disabled > 0)
                Plugin.Log.LogInfo($"[BIGONI BROTHER] Disabled {disabled} nearby goblin(s) to prevent double H-scene");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning($"[BIGONI BROTHER] DisableNearbyGoblins: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Spawns 3 goblins near GG (by center и by краям)
    /// </summary>
    private static void SpawnGoblinsForHandoff(playercon player)
    {
        try
        {
            // Проверяем state игрока before spawn goblin
            if (player == null)
            {
                Plugin.Log.LogError("[BIGONI BROTHER] Player is null during goblin spawn!");
                return;
            }

            // Дополнительная проверка состояния игрока
            var stateField = typeof(playercon).GetField("state", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            string playerState = stateField != null ? (string)stateField.GetValue(player) ?? "UNKNOWN" : "UNKNOWN";

            Plugin.Log.LogInfo($"[BIGONI BROTHER] Spawning goblins - Player state: {playerState}, eroflag: {player.eroflag}, erodown: {player.erodown}, physics: {player.rigi2d?.simulated ?? false}");

            // CRITICAL: отключаем preexisting goblins in радиусе, иначе они хватают ГГ
            // simultaneously со спавненными → двойная эро-анимация
            DisableNearbyGoblins(player, 12f);

            // EnemyHandoffSystem.GlobalHandoffCount already incremented in BigoniBrotherPassLogic.ExecuteHandoff
            // Спавненные гоблины начнут with 2ERO_START (force mid) через shared count

            // Get префаб goblin
            GameObject goblinPrefab = NoREroMod.Systems.Spawn.EnemyPrefabRegistry.GetPrefab("Goblin");
            if (goblinPrefab == null)
            {
                Plugin.Log.LogError("[BIGONI BROTHER] Goblin prefab not found!");
                return;
            }

            Vector2 playerPos = player.transform.position;
            float spawnOffset = 3f;

            // Спавним гоблиon слева
            Vector2 leftPos = new Vector2(playerPos.x - spawnOffset, playerPos.y);
            GameObject leftGoblin = UnityEngine.Object.Instantiate(goblinPrefab, leftPos, Quaternion.identity);
            if (leftGoblin != null) leftGoblin.SetActive(true);

            // Спавним гоблиon by середине
            Vector2 centerPos = new Vector2(playerPos.x, playerPos.y - spawnOffset * 0.5f);
            GameObject centerGoblin = UnityEngine.Object.Instantiate(goblinPrefab, centerPos, Quaternion.identity);
            if (centerGoblin != null) centerGoblin.SetActive(true);

            // Спавним гоблиon справа
            Vector2 rightPos = new Vector2(playerPos.x + spawnOffset, playerPos.y);
            GameObject rightGoblin = UnityEngine.Object.Instantiate(goblinPrefab, rightPos, Quaternion.identity);
            if (rightGoblin != null) rightGoblin.SetActive(true);

            Plugin.Log.LogInfo("[BIGONI BROTHER] Spawned 3 goblins");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"[BIGONI BROTHER] Error spawning goblins: {ex.Message}");
        }
    }
    

    /// <summary>
    /// Postfix patch on StartBigoniERO.OnEvent - sets timeScale = 4f after events START
    /// </summary>
    [HarmonyPatch(typeof(StartBigoniERO), "OnEvent")]
    [HarmonyPostfix]
    private static void SetTimeScaleAfterStartEvent(StartBigoniERO __instance, Spine.AnimationState state, int trackIndex, Spine.Event e)
    {
        try
        {
            if (e == null || e.Data == null)
            {
                return;
            }

            // Get event name via reflection
            string eventName = null;
            var nameProperty = e.Data.GetType().GetProperty("name") ?? e.Data.GetType().GetProperty("Name");
            if (nameProperty != null)
            {
                eventName = nameProperty.GetValue(e.Data, null) as string;
            }

            // Process все events for BigoniBrother
            var oyaField = typeof(StartBigoniERO).GetField("oya", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (oyaField == null)
            {
                // Plugin.Log.LogInfo( "[BIGONI BROTHER] Postfix OnEvent: oyaField is null");
                return;
            }

            Bigoni oya = oyaField.GetValue(__instance) as Bigoni;
            
            // Check if this is BigoniBrother
            if (oya == null || oya.gameObject == null || oya.gameObject.name == null)
            {
                return; // Do not логируем, to avoid спамить for normal Bigoni
            }
            
            bool isBigoniBrother = oya.gameObject.name.Contains("BigoniBrother");
            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Postfix OnEvent: eventName={eventName}, oyaName={oya.gameObject.name}, isBigoniBrother={isBigoniBrother}");
            
            if (!isBigoniBrother)
            {
                return;
            }

            // Get myspine
            var myspineField = typeof(StartBigoniERO).GetField("myspine", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (myspineField == null)
            {
                return;
            }

            Spine.Unity.SkeletonAnimation myspine = myspineField.GetValue(__instance) as Spine.Unity.SkeletonAnimation;
            if (myspine == null)
            {
                return;
            }

            string currentAnim = myspine.AnimationName;
            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Postfix OnEvent: eventName={eventName}, currentAnim={currentAnim}, timeScale={myspine.timeScale}");

            // IMPORTANT: do NOT set timeScale force here - let original code controls speed
            // (timeScale may change on 1.2f and 1.4f on FIN events on 2ERO)
            // Set timeScale = 1f only for START, so that предотвратить ускорение
            if (eventName == "START")
            {
                myspine.timeScale = 1f;
                // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Postfix OnEvent: Set timeScale = 1f after {eventName} event");
            }
            // For START2, set configured time scale
            else if (eventName == "START2")
            {
                float targetTimeScale = Plugin.bigoniBrotherStart2TimeScale?.Value ?? 1.0f;
                myspine.timeScale = targetTimeScale;
                // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Postfix OnEvent: Set timeScale = {targetTimeScale} after {eventName} event");
            }
            // For other событий просто логируем текущий timeScale
            else if (eventName == "SE" || eventName == "SE1" || eventName == "SE2" || eventName == "SE3" || eventName == "2ERO" || eventName == "FIN")
            {
                // Plugin.Log.LogInfo( $"[BIGONI BROTHER] Postfix OnEvent: {eventName} event, currentAnim={currentAnim}, timeScale={myspine.timeScale:F2} (original code controls it)");
            }
        }
        catch (System.Exception ex)
        {
        }
    }

    /// <summary>
    /// Coroutine to maintain timeScale = 1f (normal speed)
    /// </summary>
    private static IEnumerator MaintainTimeScale(Spine.Unity.SkeletonAnimation myspine, Bigoni oya)
    {
        // Plugin.Log.LogInfo( "[BIGONI BROTHER] MaintainTimeScale coroutine started");
        int checkCount = 0;
        
        while (myspine != null && myspine.AnimationState != null)
        {
            checkCount++;
            var track = myspine.AnimationState.GetCurrent(0);
            if (track != null && track.Animation != null)
            {
                string animName = track.Animation.Name;
                float currentTimeScale = myspine.timeScale;
                
                // Log каждые 10 проверок (1 second)
                if (checkCount % 10 == 0)
                {
                    // Plugin.Log.LogInfo( $"[BIGONI BROTHER] MaintainTimeScale: animName={animName}, timeScale={currentTimeScale}, trackTime={track.Time}/{track.Animation.Duration}");
                }
                
                // IMPORTANT: do NOT set timeScale = 1f force for 2ERO - оригинальный код может менять its on 1.2f and 1.4f on FIN events
                // Set timeScale = 1f only for ERO и START анимаций (кроме START2, which должon быть 2f)
                if (animName == "4ERO" || animName == "ERO" || animName == "ERO1" || 
                    animName == "START" || animName == "START3" || animName == "START5")
                {
                    if (currentTimeScale != 1f)
                    {
                        myspine.timeScale = 1f;
                        // Plugin.Log.LogInfo( $"[BIGONI BROTHER] MaintainTimeScale: Reset timeScale to 1f (was {currentTimeScale}) for {animName}");
                    }
                }
                // For START2, set configured time scale
                else if (animName == "START2")
                {
                    float targetTimeScale = Plugin.bigoniBrotherStart2TimeScale?.Value ?? 1.0f;
                    if (currentTimeScale != targetTimeScale)
                    {
                        myspine.timeScale = targetTimeScale;
                        // Plugin.Log.LogInfo( $"[BIGONI BROTHER] MaintainTimeScale: Set timeScale to {targetTimeScale} (was {currentTimeScale}) for START2");
                    }
                }
                // For 2ERO НЕ трогаем timeScale - let original код управляет им (может быть 1.0, 1.2, 1.4)
                else if (animName == "2ERO")
                {
                    // Only логируем, not меняем timeScale
                    if (checkCount % 10 == 0)
                    {
                        // Plugin.Log.LogInfo( $"[BIGONI BROTHER] MaintainTimeScale: 2ERO anim, timeScale={currentTimeScale:F2} (original code controls it)");
                    }
                }
                else if (animName == "4EROJIGO" || animName == "FIN")
                {
                    // Финальные animation - normal speed
                    myspine.timeScale = 1f;
                    // Plugin.Log.LogInfo( $"[BIGONI BROTHER] MaintainTimeScale: Final animation {animName} detected, setting timeScale = 1f and stopping");
                    break; // Выходим from цикла on final animation
                }
            }
            
            yield return new WaitForSeconds(0.1f); // Проверяем каждые 0.1 seconds
        }
        
        // Plugin.Log.LogInfo( "[BIGONI BROTHER] MaintainTimeScale coroutine ended");
    }

    /// <summary>
    /// Coroutine to maintain timeScale = 1f (normal speed)
    /// Также отслеживает завершение animation ERO и force переключает to 4EROJIGO
    /// Использует myspine from StartBigoniERO, and not erospine from Bigoni
    /// </summary>
    private static IEnumerator MaintainTimeScaleOnBigoni(Spine.Unity.SkeletonAnimation myspine, Bigoni oya)
    {
        // Plugin.Log.LogInfo( "[BIGONI BROTHER] MaintainTimeScaleOnBigoni coroutine started");
        
        // Verify this is BigoniBrother to prevent affecting other enemies (e.g., goblins)
        if (oya == null || oya.gameObject == null || oya.gameObject.name == null || !oya.gameObject.name.Contains("BigoniBrother"))
        {
            yield break;
        }
        
        int checkCount = 0;
        int eroCycleCount = 0; // ERO animation cycle counter
        
        while (myspine != null && myspine.AnimationState != null && oya != null && oya.eroflag && oya.gameObject != null && oya.gameObject.name != null && oya.gameObject.name.Contains("BigoniBrother"))
        {
            checkCount++;
            var track = myspine.AnimationState.GetCurrent(0);
            if (track != null && track.Animation != null)
            {
                string animName = track.Animation.Name;
                float currentTimeScale = myspine.timeScale;
                
                // Отслеживаем завершение cycles ERO animation
                if (animName == "ERO")
                {
                    // Анимация looped (Loop = true), поэтому track.Time not сбрасывается, а продолжает расти
                    // Считаем циклы by количеству прохождений через Duration
                    float duration = track.Animation.Duration;
                    int currentCycle = (int)(track.Time / duration);
                    
                    // If цикл изменился (перешли on новый цикл)
                    if (currentCycle > eroCycleCount)
                    {
                        eroCycleCount = currentCycle;
                        // Plugin.Log.LogInfo( $"[BIGONI BROTHER] MaintainTimeScaleOnBigoni: ERO cycle #{eroCycleCount} completed (trackTime={track.Time:F2}, duration={duration:F2})");
                        
                        // In original BigoniERO переход on 2ERO происходит ТОЛЬКО через event "2ERO" from animation,
                        // которое handled in OnEvent with count++ и проверкой count == 2.
                        // Мы НЕ force switch here - пусть events работают естественным образом.
                        // This позволяет animation ERO играть полный цикл with правильными таймингами.
                        // Plugin.Log.LogInfo( $"[BIGONI BROTHER] MaintainTimeScaleOnBigoni: ERO cycle #{eroCycleCount} completed, waiting for 2ERO event from animation (count==2)");
                    }
                }
                else
                {
                    // Reset counter on change animation
                    if (animName != "ERO")
                    {
                        eroCycleCount = 0;
                    }
                }
                
                // Log каждые 10 проверок (1 second)
                if (checkCount % 10 == 0)
                {
                    // Plugin.Log.LogInfo( $"[BIGONI BROTHER] MaintainTimeScaleOnBigoni: animName={animName}, timeScale={currentTimeScale}, trackTime={track.Time}/{track.Animation.Duration}, cycles={eroCycleCount}");
                }
                
                // Normal speed for всех анимаций (кроме START2, which должon быть 2f)
                if (animName == "4ERO" || animName == "ERO" || animName == "ERO1" || animName == "2ERO" || 
                    animName == "START" || animName == "START3" || animName == "START5")
                {
                    if (currentTimeScale != 1f)
                    {
                        myspine.timeScale = 1f;
                        // Plugin.Log.LogInfo( $"[BIGONI BROTHER] MaintainTimeScaleOnBigoni: Reset timeScale to 1f (was {currentTimeScale}) for {animName}");
                    }
                }
                // For START2, set configured time scale
                else if (animName == "START2")
                {
                    float targetTimeScale = Plugin.bigoniBrotherStart2TimeScale?.Value ?? 1.0f;
                    if (currentTimeScale != targetTimeScale)
                    {
                        myspine.timeScale = targetTimeScale;
                        // Plugin.Log.LogInfo( $"[BIGONI BROTHER] MaintainTimeScaleOnBigoni: Set timeScale to {targetTimeScale} (was {currentTimeScale}) for START2");
                    }
                }
                else if (animName == "4EROJIGO" || animName == "FIN")
                {
                    // Финальные animation - normal speed
                    myspine.timeScale = 1f;
                    // Plugin.Log.LogInfo( $"[BIGONI BROTHER] MaintainTimeScaleOnBigoni: Final animation {animName} detected, setting timeScale = 1f");
                    
                    // Get StartBigoniERO via reflection for completion animation
                    var erodataField = typeof(Bigoni).GetField("erodata", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (erodataField != null)
                    {
                        GameObject erodata = erodataField.GetValue(oya) as GameObject;
                        if (erodata != null)
                        {
                            StartBigoniERO ero = erodata.GetComponent<StartBigoniERO>();
                            if (ero != null)
                            {
                                var playerField = typeof(StartBigoniERO).GetField("player", 
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (playerField != null)
                                {
                                    playercon player = playerField.GetValue(ero) as playercon;
                                    if (player != null)
                                    {
                                        // Start coroutine for waiting completion final animation
                                        // Check if not запущеon ли already coroutine завершения (избегаем дублирования)
                                        if (!finishingBigoniBrothers.Contains(oya))
                                        {
                                            finishingBigoniBrothers.Add(oya);
                                            oya.StartCoroutine(WaitForFinishAnimationAndEnd(myspine, oya, player, ero, animName));
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    // Do not exit from цикла - продолжаем следить, пока final animation completes
                    // (WaitForFinishAnimationAndEnd сам обworks завершение)
                }
            }
            
            yield return new WaitForSeconds(0.1f); // Проверяем каждые 0.1 seconds
        }
        
        // Plugin.Log.LogInfo( "[BIGONI BROTHER] MaintainTimeScaleOnBigoni coroutine ended");
    }
    
    /// <summary>
    /// Coroutine for tracking completion animation START2 и its повторения 5 раз
    /// </summary>
    private static IEnumerator TrackStart2Completion(Bigoni oya, Spine.Unity.SkeletonAnimation myspine, StartBigoniERO ero)
    {
        // Ждем, пока трек START2 will установлен
        yield return new WaitForSeconds(0.1f);
        
        Spine.TrackEntry start2Track = null;
        int attempts = 0;
        const int maxAttempts = 50; // 5 секунд максимум
        
        // Ждем, пока трек START2 появится
        while (attempts < maxAttempts && myspine != null && myspine.AnimationState != null)
        {
            var currentTrack = myspine.AnimationState.GetCurrent(0);
            if (currentTrack != null && currentTrack.Animation != null && currentTrack.Animation.Name == "START2")
            {
                start2Track = currentTrack;
                break;
            }
            attempts++;
            yield return new WaitForSeconds(0.1f);
        }
        
        if (start2Track == null)
        {
            Plugin.Log.LogWarning("[BIGONI BROTHER] TrackStart2Completion: START2 track not found after waiting");
            yield break;
        }
        
        Plugin.Log.LogInfo($"[BIGONI BROTHER] TrackStart2Completion: Found START2 track, subscribing to Complete");
        
        // Subscribe to завершение animation START2
        bool completed = false;
        start2Track.Complete += (Spine.AnimationState state, int trackIndex, int loopCount) =>
        {
            completed = true;
            OnStart2Complete(oya, myspine, ero);
        };
        
        // Wait завершения animation
        while (!completed && myspine != null && myspine.AnimationState != null)
        {
            var currentTrack = myspine.AnimationState.GetCurrent(0);
            if (currentTrack == null || currentTrack.Animation == null || currentTrack.Animation.Name != "START2")
            {
                // Animation changed, возcan already finished
                break;
            }
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    /// <summary>
    /// Completion handler animation START2
    /// </summary>
    private static void OnStart2Complete(Bigoni oya, Spine.Unity.SkeletonAnimation myspine, StartBigoniERO ero)
    {
        if (oya == null || myspine == null || ero == null)
        {
            return;
        }
        
        // Initialize counter
        if (!start2PlayCounts.ContainsKey(oya))
        {
            start2PlayCounts[oya] = 0;
        }
        
        start2PlayCounts[oya]++;
        int playCount = start2PlayCounts[oya];
        
        int requiredCount = Plugin.bigoniBrotherStart2RepeatCount?.Value ?? 3;
        float timeScale = Plugin.bigoniBrotherStart2TimeScale?.Value ?? 1.0f;
        
        Plugin.Log.LogInfo($"[BIGONI BROTHER] OnStart2Complete: playCount={playCount}/{requiredCount}");
        
        // If played less than required count, loop back to START2
        if (playCount < requiredCount)
        {
            // Loop back to START2 with configured time scale (as in original StartBigoniERO.cs:111-117)
            myspine.state.SetAnimation(0, "START2", false);
            myspine.timeScale = timeScale;
            DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_Unconscious", 1f, null, 0f, null, false, false);
            
            // Reset counters (as in original)
            var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var countField = typeof(StartBigoniERO).GetField("count", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (seCountField != null)
            {
                seCountField.SetValue(ero, 0);
            }
            if (countField != null)
            {
                countField.SetValue(ero, 0);
            }
            
            Plugin.Log.LogInfo($"[BIGONI BROTHER] OnStart2Complete: Looped back START2 (playCount={playCount}/{requiredCount})");
            
            // Start coroutine again to track next completion
            if (oya != null)
            {
                oya.StartCoroutine(TrackStart2Completion(oya, myspine, ero));
            }
        }
        else
        {
            // Completed required count - reset counter and allow transition to START3
            Plugin.Log.LogInfo($"[BIGONI BROTHER] OnStart2Complete: Completed {requiredCount} times, allowing transition to START3");
            start2PlayCounts[oya] = 0; // Reset for next time
            
            // Transition to START3 (as in original StartBigoniERO.cs:118-125)
            var playerField = typeof(StartBigoniERO).GetField("player", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (playerField != null)
            {
                playercon player = playerField.GetValue(ero) as playercon;
                if (player != null)
                {
                    player._easyESC = true;
                }
            }
            
            myspine.state.SetAnimation(0, "START3", true);
            myspine.timeScale = 1f;
            DarkTonic.MasterAudio.MasterAudio.PlaySound("ero_Unconscious", 1f, null, 0f, null, false, false);
            
            // Reset counters
            var seCountField = typeof(StartBigoniERO).GetField("se_count", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var countField = typeof(StartBigoniERO).GetField("count", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (seCountField != null)
            {
                seCountField.SetValue(ero, 0);
            }
            if (countField != null)
            {
                countField.SetValue(ero, 0);
            }
        }
    }
}
}
