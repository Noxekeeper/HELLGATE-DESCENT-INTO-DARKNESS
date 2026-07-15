using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Spine.Unity;
using NoREroMod;
using NoREroMod.Patches.Enemy.Base;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// MummyManERO handoff: after one full cycle ending on Spine event <c>JIGO</c>.
/// First MummyMan plays the full sequence; subsequent MummyMan in the same gangbang
/// session start at <c>JIGO</c>. After handoff the owner stays hidden until the player stands
/// (<see cref="MummyManHandoffHide"/>).
/// </summary>
class MummyManPassPatch : BaseEnemyPassPatch<MummyManERO>
{
    protected override string EnemyName => "MummyMan";

    protected override int CyclesBeforePass => 1;

    protected override string[] GetHAnimations()
    {
        return new[]
        {
            "EROSTART100",
            "START", "START2", "START3", "START4", "START5",
            "START6", "START7", "START8", "START9", "START10", "START11",
            "ERO", "ERO2",
            "2ERO", "2ERO1", "2ERO2", "2ERO3",
            "FIN", "FIN2", "FIN3",
            "JIGO", "JIGO2"
        };
    }

    protected override bool IsCycleComplete(string animationName, string eventName, int seCount)
    {
        return eventName == "JIGO";
    }

    protected override void ForceAnimationToMiddle(SkeletonAnimation spine)
    {
        if (spine == null || spine.state == null)
            return;

        spine.state.SetAnimation(0, "JIGO", false);
        spine.timeScale = 1f;
    }

    protected override string GetEnemyTypeName()
    {
        return "mummy_man";
    }

    internal static void ResetAll()
    {
        BaseEnemyPassPatch<MummyManERO>.ResetAll();
        MummyManHandoffHide.RestoreAll();
    }

    [HarmonyPatch(typeof(MummyManERO), "OnEvent")]
    [HarmonyPostfix]
    private static void MummyManPass(MummyManERO __instance, Spine.Event e, int ___se_count)
    {
        var instance = new MummyManPassPatch();
        SetInstance(instance);

        try
        {
            var disabledField = typeof(BaseEnemyPassPatch<MummyManERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);

            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                if (disabledDict != null && disabledDict.ContainsKey(__instance) && disabledDict[__instance])
                    return;
            }

            var player = UnifiedPlayerCacheManager.GetPlayer();
            if (player == null || !player.eroflag || player.erodown == 0)
                return;

            var spine = GetSpineAnimation(__instance);
            if (spine == null)
                return;

            string currentAnim = spine.AnimationName;
            if (!instance.IsHAnimation(currentAnim))
                return;

            if (EnemyHandoffSystem.GlobalHandoffCount > 0
                && !string.IsNullOrEmpty(currentAnim)
                && currentAnim.StartsWith("START", StringComparison.Ordinal))
            {
                __instance.count = 0;
                __instance.se_count = 0;
                instance.ForceAnimationToMiddle(spine);
            }

            instance.TrackCycles(__instance, spine, e, ___se_count);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[MummyManPassPatch] OnEvent error: {ex.Message}");
        }
    }

    /// <summary>
    /// Called by <see cref="DelayedHandoffScript"/> after handoff delay.
    /// Hides the MummyMan owner (Kakasi-style) and puts the player prone for the next grab.
    /// </summary>
    public static void ExecuteHandoff(object enemyInstance)
    {
        try
        {
            GameObject playerObject = UnifiedPlayerCacheManager.GetPlayerObject();
            if (playerObject == null)
                return;

            var player = playerObject.GetComponent<playercon>();
            var playerStatus = playerObject.GetComponent<PlayerStatus>();
            if (player == null)
                return;

            var disabledField = typeof(BaseEnemyPassPatch<MummyManERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);
            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                if (disabledDict != null)
                    disabledDict[enemyInstance] = true;
            }

            var ero = enemyInstance as MummyManERO;
            Transform enemyTransform = ero != null ? ero.transform : null;

            if (ero != null)
            {
                try
                {
                    var enemySpine = GetSpineAnimation(ero);
                    enemySpine?.AnimationState?.ClearTracks();
                }
                catch
                {
                }

                MummyMan oya = AccessTools.Field(typeof(MummyManERO), "oya")?.GetValue(ero) as MummyMan;
                if (oya != null)
                {
                    // Do NOT RestoreEnemyDateParentAfterEro — that re-shows combat mesh and
                    // lets the same mummy walk up and re-grab from START.
                    MummyManHandoffHide.HideAfterHandoff(oya, oya.erodata);
                }
                else
                {
                    ero.gameObject.SetActive(false);
                }
            }

            EnemyHandoffPlayerHelper.ApplyStandardHandoffState(player, playerStatus, enemyTransform);

            // MummyMan-only: grab requires state=="DOWN"; neighbors need faction wake + EROWALK.
            // Do not put this in EnemyHandoffPlayerHelper — other Pass enemies already worked.
            player.state = "DOWN";
            MummyManHandoffHide.WakeNearbyForGrab(player);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[MummyManPassPatch] ExecuteHandoff error: {ex.Message}");
        }
    }
}
