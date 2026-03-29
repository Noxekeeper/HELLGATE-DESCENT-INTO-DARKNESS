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
using NoREroMod.Systems.Cache;

namespace NoREroMod.Patches.Enemy.Six_hand
{
    /// <summary>
    /// Handoff logic and tracking cycles for демit «Six Hands» (Mutude).
    /// Optimized: Uses UnifiedPlayerCacheManager instead of FindGameObjectWithTag
    /// </summary>
    internal class MutudePassLogic : BaseEnemyPassPatch<Mutudeero>
    {
        protected override string EnemyName => "Mutude";

        /// <summary>
        /// Six Hands quickly transitions to intense phase, therefore hand off GG after first full cycle.
        /// </summary>
        protected override int CyclesBeforePass => 1;

        private const float CloudSpawnChancePerCycle = 0.35f;
        private static readonly Dictionary<object, bool> cloudSpawnedThisCycle = new();
        private static readonly Dictionary<object, bool> startEffectTriggered = new();
        private static readonly Dictionary<object, bool> languageEffectTriggered = new();
        private static readonly Dictionary<object, bool> fin1EffectTriggered = new();
        private static readonly Dictionary<object, bool> fin2EffectTriggered = new();
        private static readonly Dictionary<object, string> videoEffectAnimation = new();
        private static readonly Dictionary<object, float> lastSpeechTime = new();
        private static readonly Dictionary<object, float> soundEffectCooldown = new(); // Cooldown for soundsых эффектов
        private const float SpeechCooldown = 4f; // Cooldown for речи Mutude
        private static readonly FieldInfo OwnerField = typeof(Mutudeero).GetField("oya", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo CreateAtkField = typeof(Mutude).GetField("CreateATK", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly System.Random cloudRng = new();

        protected override string[] GetHAnimations()
        {
            return new[]
            {
                "START",
                "ERO1", "ERO1_2",
                "ERO2", "ERO2_2",
                "ERO3", "ERO4", "ERO5",
                "FIN", "FIN2",
                "START_JIGO",
                "DRINK", "DRINK_END"
            };
        }

        protected override bool IsCycleComplete(string animationName, string eventName, int seCount)
        {
            string anim = animationName?.ToUpperInvariant() ?? string.Empty;
            string evt = eventName?.ToUpperInvariant() ?? string.Empty;

            if (anim == "START_JIGO" && evt.Contains("START_JIGO"))
            {
                return true;
            }

            return false;
        }

        protected override string GetEnemyTypeName()
        {
            return "mutude";
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

                const string fallbackAnim = "ERO3";
                string[] candidates =
                {
                    "ERO1",
                    "ERO2",
                    "ERO3",
                    "ERO4",
                    "ERO5"
                };

                string chosen = candidates.Length > 0
                    ? candidates[UnityEngine.Random.Range(0, candidates.Length)]
                    : fallbackAnim;

                var track = spine.AnimationState.SetAnimation(0, chosen, true);
                if (track?.Animation != null)
                {
                    track.Time = track.Animation.Duration * 0.35f;
                }

                // Логи принудительной animation отключены
            }
            catch (Exception ex)
            {
            }
        }

        [HarmonyPatch(typeof(Mutudeero), "OnEvent")]
        [HarmonyPostfix]
        private static void MutudePass(Mutudeero __instance, Spine.AnimationState state, int trackIndex, Spine.Event e)
        {
            
            var instance = new MutudePassLogic();
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
                
                // Check that this spine Mutude, а not ГГ
                if (spine.transform.parent != null && spine.transform.parent.name.Contains("Player"))
                {
                    // Try найти правильный spine Mutude
                    var mutudeObj = GameObject.Find("Mutude(Clone)");
                    if (mutudeObj != null)
                    {
                        spine = mutudeObj.GetComponentInChildren<SkeletonAnimation>();
                    }
                }

                string currentAnim = spine.AnimationName ?? string.Empty;
                string eventName = e?.Data?.Name ?? e?.ToString() ?? string.Empty;
                
                if (!instance.IsHAnimation(currentAnim))
                {
                    return;
                }

                
                // Processing dialogue system events
                try {
                    NoREroMod.Systems.Dialogue.DialogueFramework.ProcessAnimationEvent(
                        __instance, 
                        currentAnim, 
                        eventName, 
                        __instance.se_count
                    );
                } catch (Exception ex) {
                }

                // eventName already объявлеon выше
                MindBrokenSystem.ProcessAnimationEvent(__instance, currentAnim, eventName);
                if (!string.IsNullOrEmpty(currentAnim) &&
                    (currentAnim.IndexOf("FIN", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     currentAnim.IndexOf("DRINK_END", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    MindBrokenSystem.RegisterClimaxEvent(__instance);
                }

                if (!cloudSpawnedThisCycle.ContainsKey(__instance))
                {
                    cloudSpawnedThisCycle[__instance] = false;
                }
                if (!startEffectTriggered.ContainsKey(__instance))
                {
                    startEffectTriggered[__instance] = false;
                }
                if (!languageEffectTriggered.ContainsKey(__instance))
                {
                    languageEffectTriggered[__instance] = false;
                }
                if (!fin1EffectTriggered.ContainsKey(__instance))
                {
                    fin1EffectTriggered[__instance] = false;
                }
                if (!fin2EffectTriggered.ContainsKey(__instance))
                {
                    fin2EffectTriggered[__instance] = false;
                }

                if (!cloudSpawnedThisCycle[__instance] &&
                    ShouldTriggerCloud(currentAnim, eventName, __instance.se_count))
                {
                    if (cloudRng.NextDouble() <= CloudSpawnChancePerCycle &&
                        TrySpawnCloud(__instance))
                    {
                        cloudSpawnedThisCycle[__instance] = true;
                    }
                }


                bool cycleFinished = instance.IsCycleComplete(currentAnim, eventName, __instance.se_count);
                instance.TrackCycles(__instance, spine, e, __instance.se_count);
                if (cycleFinished)
                {
                    cloudSpawnedThisCycle[__instance] = false;
                    startEffectTriggered[__instance] = false;
                    languageEffectTriggered[__instance] = false;
                    fin1EffectTriggered[__instance] = false;
                    fin2EffectTriggered[__instance] = false;
                    videoEffectAnimation.Remove(__instance);
                }

                if (!startEffectTriggered[__instance] &&
                    ShouldTriggerStartEffect(currentAnim, eventName, __instance.se_count))
                {
                    startEffectTriggered[__instance] = true;
                }
                else if (!languageEffectTriggered[__instance] &&
                    ShouldTriggerLanguageEffect(currentAnim, eventName, __instance.se_count))
                {
                    languageEffectTriggered[__instance] = true;
                }
                else if (!fin1EffectTriggered[__instance] &&
                         ShouldTriggerFin1Effect(currentAnim, eventName, __instance.se_count))
                {
                    fin1EffectTriggered[__instance] = true;
                    fin2EffectTriggered[__instance] = true;
                    videoEffectAnimation.Remove(__instance);
                }
                else if (!fin2EffectTriggered[__instance] &&
                         ShouldTriggerFin2Effect(currentAnim, eventName, __instance.se_count))
                {
                    fin2EffectTriggered[__instance] = true;
                    videoEffectAnimation.Remove(__instance);
                }
            }
            catch (Exception ex)
            {
            }
        }

        static MutudePassLogic()
        {
            var instance = new MutudePassLogic();
            SetInstance(instance);
        }

        internal static void ResetAll()
        {
            BaseEnemyPassPatch<Mutudeero>.ResetAll();
            cloudSpawnedThisCycle.Clear();
            startEffectTriggered.Clear();
            languageEffectTriggered.Clear();
            fin1EffectTriggered.Clear();
            fin2EffectTriggered.Clear();
            videoEffectAnimation.Clear();
            lastSpeechTime.Clear();
            soundEffectCooldown.Clear();
        }

        public static void ExecuteHandoff(object enemyInstance)
        {
            PushPlayerAwayFromEnemy(enemyInstance);
        }

        [HarmonyPatch(typeof(StruggleSystem), "startGrabInvul")]
        [HarmonyPostfix]
        private static void ClearOnStruggleEscape()
        {
            try
            {
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
                // Всегда очищаем state, even if enemy already отключен
                // This ensures that on следующем спаoutside Mutude will in correctlyм состоянии
                ClearStateData();
            }
            catch (Exception ex)
            {
            }
        }

        private static void ClearStateData()
        {
            int cyclesCount = enemyAnimationCycles.Count;
            int startTimes = enemySessionStartTime.Count;
            int hasPassedCount = enemyHasPassed.Count;


            enemyAnimationCycles.Clear();
            enemySessionStartTime.Clear();
            lastCycleTime.Clear();
            enemyHasPassed.Clear();
            enemyDisabled.Clear();

            cloudSpawnedThisCycle.Clear();
            startEffectTriggered.Clear();
            languageEffectTriggered.Clear();
            fin1EffectTriggered.Clear();
            fin2EffectTriggered.Clear();
            videoEffectAnimation.Clear();
            MutudeEffects.StopAll(true);

            int oldGlobal = globalHandoffCount;
            globalHandoffCount = 0;
            globalSessionStartTime = 0f;

        }

        private static void PushPlayerAwayFromEnemy(object enemyInstance)
        {
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

                    // Push ГГ from enemy (as у Touzoku)
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
                        }
                        
                    }
                }

                if (playerStatus != null)
                {
                    playerStatus.Sp = 0f;
                }

                var mutudeEro = enemyInstance as Mutudeero;
                if (mutudeEro != null)
                {
                    var ownerField = typeof(Mutudeero).GetField("oya", BindingFlags.NonPublic | BindingFlags.Instance);
                    var owner = ownerField?.GetValue(mutudeEro) as Mutude;
                    if (owner != null)
                    {
                        try
                        {
                            var erodataField = typeof(EnemyDate).GetField("erodata", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                            var erodata = erodataField?.GetValue(owner) as GameObject;
                            if (erodata != null)
                            {
                                erodata.SetActive(false);
                            }

                            var ownerSpineField = typeof(Mutude).GetField("erospine", BindingFlags.NonPublic | BindingFlags.Instance);
                            (ownerSpineField?.GetValue(owner) as SkeletonAnimation)?.AnimationState?.ClearTracks();

                            var eroflagFieldOwner = typeof(Mutude).GetField("eroflag", BindingFlags.NonPublic | BindingFlags.Instance);
                            eroflagFieldOwner?.SetValue(owner, false);

                            // DO NOT disable owner.gameObject - основной Mutude must остаться активным,
                            // so that он мог again захватить ГГ after освобождения (as у Touzoku)
                            // owner.gameObject.SetActive(false); // ❌ УДАЛЕНО
                        }
                        catch (Exception ex)
                        {
                        }
                    }

                    var eroFlagField = typeof(Mutudeero).GetField("eroflag", BindingFlags.NonPublic | BindingFlags.Instance);
                    eroFlagField?.SetValue(mutudeEro, false);
                }

                (enemyInstance as MonoBehaviour)?.gameObject.SetActive(false);
                cloudSpawnedThisCycle.Remove(enemyInstance);
                startEffectTriggered.Remove(enemyInstance);
                languageEffectTriggered.Remove(enemyInstance);
                fin1EffectTriggered.Remove(enemyInstance);
                fin2EffectTriggered.Remove(enemyInstance);
            }
            catch (Exception ex)
            {
            }
        }

        private static bool ShouldTriggerCloud(string animationName, string eventName, int seCount)
        {
            if (string.IsNullOrEmpty(animationName) || string.IsNullOrEmpty(eventName))
            {
                return false;
            }

            string animUpper = animationName.ToUpperInvariant();
            string evtUpper = eventName.ToUpperInvariant();

            if (evtUpper != "SE")
            {
                return false;
            }

            if ((animUpper == "ERO3" || animUpper == "ERO4" || animUpper == "ERO5") && seCount == 1)
            {
                return true;
            }

            return false;
        }

        private static bool ShouldTriggerVideoEffect(string animationName, string eventName, int seCount)
        {
            return false;
        }

        private static bool ShouldTriggerStartEffect(string animationName, string eventName, int seCount)
        {
            if (string.IsNullOrEmpty(animationName) || string.IsNullOrEmpty(eventName))
            {
                return false;
            }

            string animUpper = animationName.ToUpperInvariant();
            string evtUpper = eventName.ToUpperInvariant();

            return evtUpper == "SE" && animUpper == "START" && seCount == 1;
        }

        private static bool ShouldTriggerLanguageEffect(string animationName, string eventName, int seCount)
        {
            if (string.IsNullOrEmpty(animationName) || string.IsNullOrEmpty(eventName))
            {
                return false;
            }

            string animUpper = animationName.ToUpperInvariant();
            string evtUpper = eventName.ToUpperInvariant();

            return evtUpper == "SE" && animUpper == "ERO3" && seCount == 1;
        }

        private static bool ShouldTriggerFin1Effect(string animationName, string eventName, int seCount)
        {
            if (string.IsNullOrEmpty(animationName) || string.IsNullOrEmpty(eventName))
            {
                return false;
            }

            string animUpper = animationName.ToUpperInvariant();
            string evtUpper = eventName.ToUpperInvariant();

            return evtUpper == "SE" && animUpper == "FIN" && seCount == 1;
        }

        private static bool ShouldTriggerFin2Effect(string animationName, string eventName, int seCount)
        {
            if (string.IsNullOrEmpty(animationName) || string.IsNullOrEmpty(eventName))
            {
                return false;
            }

            string animUpper = animationName.ToUpperInvariant();
            string evtUpper = eventName.ToUpperInvariant();

            return evtUpper == "SE" && animUpper == "FIN2" && seCount == 1;
        }

        // Новая модульная диаlogsая система will создаon with нуля

        private static bool TrySpawnCloud(Mutudeero ero)
        {
            try
            {
                if (ero == null)
                {
                    return false;
                }

                var owner = OwnerField?.GetValue(ero) as Mutude;
                if (owner == null)
                {
                    return false;
                }

                var prefab = CreateAtkField?.GetValue(owner) as GameObject;
                if (prefab == null)
                {
                    return false;
                }

                Vector3 position = owner.transform.position + new Vector3(0f, 0.25f, 0f);
                Quaternion rotation = owner.transform.rotation;

                GameObject cloud = UnityEngine.Object.Instantiate(prefab, position, rotation);
                if (cloud == null)
                {
                    return false;
                }

                var playerObj = GameObject.FindWithTag("Player");
                var playerCon = playerObj != null ? playerObj.GetComponent<playercon>() : null;
                var playerStatus = playerObj != null ? playerObj.GetComponent<PlayerStatus>() : null;

                var badstatus = cloud.GetComponent<MultipleAtkBadstatus>();
                if (badstatus != null)
                {
                    badstatus.Set(playerCon, playerStatus, 0, 0f);
                }

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}


