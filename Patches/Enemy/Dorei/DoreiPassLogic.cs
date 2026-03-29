using System;
using HarmonyLib;
using UnityEngine;
using Spine.Unity;
using NoREroMod.Patches.Enemy.Base;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Cache;
using System.Reflection;
using Spine;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Optimized: Uses UnifiedPlayerCacheManager instead of FindGameObjectWithTag
/// </summary>
class DoreiPassLogic : BaseEnemyPassPatch<SinnerslaveCrossbowERO>
{
    protected override string EnemyName => "Dorei";

    protected override int CyclesBeforePass => 1;

    protected override string[] GetHAnimations()
    {
        return new[]
        {
            "START", "START2", "START3",
            "ERO0", "ERO", "ERO1", "ERO1_2", "ERO2", "ERO3",
            "FIN", "FIN2",
            "JIGO", "JIGO2"
        };
    }

    protected override bool IsCycleComplete(string animationName, string eventName, int seCount)
    {
        string anim = animationName?.ToUpperInvariant() ?? string.Empty;
        string evt = eventName?.ToUpperInvariant() ?? string.Empty;

        if (anim == "JIGO" && evt.Contains("JIGO"))
        {
            return true;
        }

        return false;
    }

    protected override string GetEnemyTypeName()
    {
        return "dorei";
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
            var track = spine.AnimationState.SetAnimation(0, "ERO0", true);
            if (track?.Animation != null)
            {
                track.Time = track.Animation.Duration * 0.5f;
            }
        }
        catch (Exception)
        {
        }
    }

    [HarmonyPatch(typeof(SinnerslaveCrossbowERO), "OnEvent")]
    [HarmonyPostfix]
    private static void DoreiPass(SinnerslaveCrossbowERO __instance, Spine.AnimationState state, int trackIndex, Spine.Event e)
    {
        var instance = new DoreiPassLogic();
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

            string eventName = e?.Data?.Name ?? e?.ToString() ?? string.Empty;
            
            try {
                NoREroMod.Systems.Dialogue.DialogueFramework.ProcessAnimationEvent(
                    __instance, 
                    currentAnim, 
                    eventName, 
                    __instance.se_count
                );
            } catch (Exception ex) {
            }
            
            MindBrokenSystem.ProcessAnimationEvent(__instance, currentAnim, eventName);
            if (!string.IsNullOrEmpty(currentAnim) && currentAnim.IndexOf("IKI", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                MindBrokenSystem.RegisterClimaxEvent(__instance);
            }

            // Logs disabled
            // Plugin.Log.LogInfo($"[DOREI] H-anim anim={currentAnim}, event={eventName}, se={__instance.se_count}");
            // Plugin.Log.LogInfo( $"[DOREI PASS] anim={currentAnim}, event={eventName}, se={__instance.se_count}");

            instance.TrackCycles(__instance, spine, e, __instance.se_count);
        }
        catch (Exception ex)
        {
            // Plugin.Log.LogInfo( $"[DOREI PASS] Error: {ex.Message}");
        }
    }

    static DoreiPassLogic()
    {
        var instance = new DoreiPassLogic();
        SetInstance(instance);
    }

    internal static void ResetAll()
    {
        BaseEnemyPassPatch<SinnerslaveCrossbowERO>.ResetAll();
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
            // Plugin.Log.LogInfo("[DOREI] === CLEAR ON STRUGGLE ESCAPE ===");
            ClearStateData();
        }
        catch (System.Exception ex)
        {
        }
    }

    [HarmonyPatch(typeof(playercon), "ImmediatelyERO")]
    [HarmonyPostfix]
    private static void ClearStateOnImmediatelyERO()
    {
        try
        {
            // Check if that текущий enemy — Dorei
            var currentEnemy = UnityEngine.Object.FindObjectOfType<SinnerslaveCrossbowERO>();
            if (currentEnemy == null)
            {
                return;
            }

            // Plugin.Log.LogInfo("[DOREI] === CLEAR ON IMMEDIATELYERO (GiveUp) ===");
            ClearStateData();
        }
        catch (System.Exception ex)
        {
        }
    }

    private static void ClearStateData()
    {
        int cyclesCount = enemyAnimationCycles.Count;
        int startTimes = enemySessionStartTime.Count;
        int hasPassedCount = enemyHasPassed.Count;

        // Plugin.Log.LogInfo($"[DOREI CLEAR] Before clear: globalHandoffCount={globalHandoffCount}, cycles={cyclesCount}, startTimes={startTimes}, hasPassed={hasPassedCount}");

        enemyAnimationCycles.Clear();
        enemySessionStartTime.Clear();
        lastCycleTime.Clear();
        enemyHasPassed.Clear();
        enemyDisabled.Clear();

        // REMOVED: Вызоin dialogue system

        int oldGlobal = globalHandoffCount;
        globalHandoffCount = 0;
        globalSessionStartTime = 0f;

        // Plugin.Log.LogInfo($"[DOREI CLEAR] After clear: globalHandoffCount={oldGlobal} -> {globalHandoffCount}");
    }

    private static void PushPlayerAwayFromEnemy(object enemyInstance)
    {
        // Plugin.Log.LogInfo("[DOREI] === Pushing GG away ===");
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
                    playerSpine.AnimationState.ClearTracks();
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

                if (playerCon.rigi2d != null)
                {
                    playerCon.rigi2d.velocity = new Vector2(playerCon.rigi2d.velocity.x, 0f);
                    playerCon.rigi2d.simulated = true;
                }

                Vector3 pushDir = Vector3.right;
                var enemyTransform = (enemyInstance as MonoBehaviour)?.transform;
                if (enemyTransform != null)
                {
                    pushDir = playerCon.transform.position - enemyTransform.position;
                    if (pushDir.sqrMagnitude < 0.01f)
                    {
                        pushDir = enemyTransform.right;
                    }
                    pushDir = new Vector3(Mathf.Sign(pushDir.x) == 0 ? 1f : Mathf.Sign(pushDir.x), 0f, 0f);
                }
                playerCon.transform.position += pushDir.normalized * 2.5f;
            }

            if (playerStatus != null)
            {
                playerStatus.Sp = 0f;
            }

            var doreiero = enemyInstance as SinnerslaveCrossbowERO;
            if (doreiero != null)
            {
                // Get родителя (SinnerslaveCrossbow) и сбрасываем its состяние
                var ownerField = typeof(SinnerslaveCrossbowERO).GetField("oya", BindingFlags.NonPublic | BindingFlags.Instance);
                if (ownerField != null)
                {
                    var owner = ownerField.GetValue(doreiero) as SinnerslaveCrossbow;
                    if (owner != null)
                    {
                        try
                        {
                            // Скрываем активную ero-animation
                            var erodataField = typeof(EnemyDate).GetField("erodata", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                            var erodata = erodataField?.GetValue(owner) as GameObject;
                            if (erodata != null)
                            {
                                erodata.SetActive(false);
                            }

                            var ownerSpineField = typeof(SinnerslaveCrossbow).GetField("erospine", BindingFlags.NonPublic | BindingFlags.Instance);
                            (ownerSpineField?.GetValue(owner) as SkeletonAnimation)?.AnimationState?.ClearTracks();

                            // Копируем приватный флаг eroflag и сбрасываем его
                            var eroflagFieldOwner = typeof(SinnerslaveCrossbow).GetField("eroflag", BindingFlags.NonPublic | BindingFlags.Instance);
                            eroflagFieldOwner?.SetValue(owner, false);

                            owner.gameObject.SetActive(false);
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                }

                // Reset flag on самой animation
                var eroFlagField = typeof(SinnerslaveCrossbowERO).GetField("eroflag", BindingFlags.NonPublic | BindingFlags.Instance);
                eroFlagField?.SetValue(doreiero, false);
            }

            // Отключаем текущую animation enemy
            (enemyInstance as MonoBehaviour)?.gameObject.SetActive(false);
        }
        catch (Exception ex)
        {
        }
    }
}

