using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Spine;
using Spine.Unity;
using NoREroMod;
using NoREroMod.Patches.Enemy.Base;
using NoREroMod.Systems.Cache;
using NoREroMod.Patches.UI.MindBroken;

namespace NoREroMod.Patches.Enemy.CrowInquisition;

/// <summary>
/// Handoff logic and tracking cycles for Crow Inquisitor.
/// Передает контроль after completion секции времени (END animation).
/// </summary>
internal class CrowInquisitionPassLogic : BaseEnemyPassPatch<CrowInquisitionERO>
{
    protected override string EnemyName => "CrowInquisition";

/// <summary>
/// Crow Inquisitor получает ГГ after передачи и начинает with START.
/// Do not имеет замкнутого цикла - always завершается END/END2.
/// </summary>
    protected override int CyclesBeforePass => 1;

    private static readonly Dictionary<object, bool> timeStopActive = new();
    private static readonly Dictionary<object, float> lastSpeechTime = new();
    private const float SpeechCooldown = 5f; // Cooldown for речей Crow
    private static readonly FieldInfo OwnerField = typeof(CrowInquisitionERO).GetField("oya", BindingFlags.NonPublic | BindingFlags.Instance);

    protected override string[] GetHAnimations()
    {
        return new[]
        {
            "START",
            "IKISTART",
            "IKI",
            "IKI2",
            "END",
            "END2"
        };
    }

    protected override bool IsCycleComplete(string animationName, string eventName, int seCount)
    {
        string anim = animationName?.ToUpperInvariant() ?? string.Empty;
        string evt = eventName?.ToUpperInvariant() ?? string.Empty;

        // CrowInquisition not имеет замкнутого цикла - завершается on END анимациях
        if ((anim == "END" || anim == "END2") && evt == "SE" && seCount == 1)
        {
            return true;
        }

        return false;
    }

    protected override string GetEnemyTypeName()
    {
        return "crowinquisition";
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

            // For Crow Inquisitor - возвращаем к START (начало сцены)
            var track = spine.AnimationState.SetAnimation(0, "START", false);
            if (track?.Animation != null)
            {
                track.Time = 0f; // Start with начала
            }
        }
        catch (Exception ex)
        {
        }
    }

    [HarmonyPatch(typeof(CrowInquisitionERO), "OnEvent")]
    [HarmonyPostfix]
    private static void CrowInquisitionPass(CrowInquisitionERO __instance, Spine.AnimationState state, int trackIndex, Spine.Event e)
    {
        var instance = new CrowInquisitionPassLogic();
        SetInstance(instance);

        try
        {
            if (enemyDisabled.ContainsKey(__instance) && enemyDisabled[__instance])
            {
                return;
            }

            // Use кэшированный playercon
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

            // Log начало работы системы
            Plugin.Log?.LogInfo($"[CrowInquisition] System active - Player eroflag: {player.eroflag}, Current animation: {currentAnim}, se_count: {__instance.se_count}");
            string eventName = e?.Data?.Name ?? string.Empty;

            if (!instance.IsHAnimation(currentAnim))
            {
                return;
            }

            // Processing dialogue system
            try
            {
                NoREroMod.Systems.Dialogue.DialogueFramework.ProcessAnimationEvent(
                    __instance,
                    currentAnim,
                    eventName,
                    __instance.se_count
                );
            }
            catch (Exception ex)
            {
            }

            MindBrokenSystem.ProcessAnimationEvent(__instance, currentAnim, eventName);

            bool cycleFinished = instance.IsCycleComplete(currentAnim, eventName, __instance.se_count);
            instance.TrackCycles(__instance, spine, e, __instance.se_count);

            if (cycleFinished)
            {
                timeStopActive.Remove(__instance);
            }
        }
        catch (Exception ex)
        {
        }
    }

    static CrowInquisitionPassLogic()
    {
        var instance = new CrowInquisitionPassLogic();
        SetInstance(instance);
    }

    internal static void ResetAll()
    {
        BaseEnemyPassPatch<CrowInquisitionERO>.ResetAll();
        timeStopActive.Clear();
        lastSpeechTime.Clear();
    }

    public static void ExecuteHandoff(object enemyInstance)
    {
        Plugin.Log?.LogInfo($"[CrowInquisition] ExecuteHandoff called - transferring player to next enemy");
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
            ClearStateData();
        }
        catch (Exception ex)
        {
        }
    }

    private static void ClearStateData()
    {
        enemyAnimationCycles.Clear();
        enemySessionStartTime.Clear();
        lastCycleTime.Clear();
        enemyHasPassed.Clear();
        enemyDisabled.Clear();

        timeStopActive.Clear();
        lastSpeechTime.Clear();
    }

    private static void PushPlayerAwayFromEnemy(object enemyInstance)
    {
        try
        {
            Plugin.Log?.LogInfo($"[CrowInquisition] PushPlayerAwayFromEnemy - starting transfer");
            enemyDisabled[enemyInstance] = true;

            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null)
            {
                Plugin.Log?.LogWarning($"[CrowInquisition] Player object not found!");
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

                // Push from Crow Inquisitor
                var enemyTransform = (enemyInstance as MonoBehaviour)?.transform;
                if (enemyTransform != null)
                {
                    Vector3 enemyPos = enemyTransform.position;
                    Vector3 playerPos = playerCon.transform.position;
                    Vector3 direction = playerPos - enemyPos;
                    direction.Normalize();

                    if (direction.x < 0)
                    {
                        direction = Vector3.right;
                    }
                    else
                    {
                        direction = Vector3.left;
                    }

                    float pushDistance = 3f;
                    Vector3 newPosition = playerCon.transform.position + (direction * pushDistance);
                    playerCon.transform.position = newPosition;

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

            var crowEro = enemyInstance as CrowInquisitionERO;
            if (crowEro != null)
            {
                var ownerField = typeof(CrowInquisitionERO).GetField("oya", BindingFlags.NonPublic | BindingFlags.Instance);
                var owner = ownerField?.GetValue(crowEro) as global::CrowInquisition;
                if (owner != null)
                {
                    try
                    {
                        var erodataField = typeof(global::CrowInquisition).GetField("erodata", BindingFlags.NonPublic | BindingFlags.Instance);
                        var erodata = erodataField?.GetValue(owner) as GameObject;
                        if (erodata != null)
                        {
                            erodata.SetActive(false);
                        }

                        var eroflagFieldOwner = typeof(global::CrowInquisition).GetField("eroflag", BindingFlags.NonPublic | BindingFlags.Instance);
                        eroflagFieldOwner?.SetValue(owner, false);
                    }
                    catch (Exception ex)
                    {
                    }
                }

                var eroflagField = typeof(CrowInquisitionERO).GetField("eroflag", BindingFlags.NonPublic | BindingFlags.Instance);
                eroflagField?.SetValue(crowEro, false);
            }

            (enemyInstance as MonoBehaviour)?.gameObject.SetActive(false);
            timeStopActive.Remove(enemyInstance);
        }
        catch (Exception ex)
        {
        }
    }
}