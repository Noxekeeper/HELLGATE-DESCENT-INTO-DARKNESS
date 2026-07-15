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
/// Patch for PilgrimERO — handoff of GG after a full cycle (on 2EROJIGO)
/// The first pilgrim plays the full sequence; later ones pick a random start
/// </summary>
class PilgrimPassPatch : BaseEnemyPassPatch<PilgrimERO>
{
    protected override string EnemyName => "Pilgrim";

    protected override int CyclesBeforePass => 1; // Handoff after 1 full cycle

    protected override string[] GetHAnimations()
    {
        return new[]
        {
            // Starting animations
            "START", "START2",
            // Path A: oral
            "FERA", "FERA1", "FERA2", "FERAFIN", "FERAFIN2", "FERAFIN3", "FERAJIGO",
            // Path B: vaginal
            "EROSTART1", "ERO", "ERO1", "ERO2", "ERO3", "EROFIN", "EROFIN2", "EROFIN3", "EROJIGO",
            // Path C: second cycle
            "2ERO", "2ERO1", "2EROFIN", "2EROFIN2", "2EROJIGO"
        };
    }

    protected override bool IsCycleComplete(string animationName, string eventName, int seCount)
    {
        // Handoff after 2EROJIGO completes (end of full cycle)
        return eventName == "2EROJIGO";
    }

    protected override void ForceAnimationToMiddle(SkeletonAnimation spine)
    {
        // For subsequent pilgrims, randomly pick a start point:
        // A: FERA (oral), B: ERO (vaginal), C: 2ERO (second cycle)
        string[] startOptions = { "FERA", "ERO", "2ERO" };
        string selectedStart = startOptions[UnityEngine.Random.Range(0, startOptions.Length)];

        Plugin.Log?.LogDebug($"[PilgrimPassPatch] ForceAnimationToMiddle: Selected '{selectedStart}' for next pilgrim");
        spine.state.SetAnimation(0, selectedStart, true);
        spine.timeScale = 1f;

        // Extra logic hooks for different start points
        switch (selectedStart)
        {
            case "FERA":
                // Oral path
                break;
            case "ERO":
                // Vaginal path
                break;
            case "2ERO":
                // Second-cycle path
                break;
        }
    }

    protected override string GetEnemyTypeName()
    {
        return "pilgrim";
    }

    internal static void ResetAll()
    {
        BaseEnemyPassPatch<PilgrimERO>.ResetAll();
    }

    [HarmonyPatch(typeof(PilgrimERO), "OnEvent")]
    [HarmonyPostfix]
    private static void PilgrimPass(PilgrimERO __instance, Spine.Event e, int ___se_count)
    {
        var instance = new PilgrimPassPatch();
        SetInstance(instance);

        try
        {
            // Log patch call for debugging
            string eventStr = e?.ToString() ?? "NULL";
            Plugin.Log?.LogDebug($"[Pilgrim PATCH] Called: event={eventStr}, se_count={___se_count}");

            // Skip if enemy is already disabled
            var disabledField = typeof(BaseEnemyPassPatch<PilgrimERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);

            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                if (disabledDict != null && disabledDict.ContainsKey(__instance) && disabledDict[__instance])
                {
                    Plugin.Log?.LogDebug($"[Pilgrim PATCH] Enemy disabled, skipping");
                    return;
                }
            }

            // Optimization: use cached playercon
            var player = UnifiedPlayerCacheManager.GetPlayer();
            if (player == null)
            {
                Plugin.Log?.LogDebug($"[Pilgrim PATCH] Player is null");
                return;
            }

            Plugin.Log?.LogDebug($"[Pilgrim PATCH] Player state: eroflag={player.eroflag}, erodown={player.erodown}");

            if (!player.eroflag || player.erodown == 0)
            {
                Plugin.Log?.LogDebug($"[Pilgrim PATCH] H-scene not active (eroflag={player.eroflag}, erodown={player.erodown})");
                return; // H-scene is not active
            }

            var spine = GetSpineAnimation(__instance);
            if (spine == null)
            {
                Plugin.Log?.LogDebug($"[Pilgrim PATCH] Spine is null");
                return;
            }

            string currentAnim = spine.AnimationName;
            string eventName = e.Data.Name;

            // Verify current animation is an H-animation
            bool isHAnim = instance.IsHAnimation(currentAnim);
            Plugin.Log?.LogDebug($"[Pilgrim PATCH] Is H-animation '{currentAnim}': {isHAnim}");

            if (!isHAnim)
            {
                Plugin.Log?.LogDebug($"[Pilgrim PATCH] Not H-animation: '{currentAnim}'");
                return;
            }

            Plugin.Log?.LogDebug($"[Pilgrim PATCH] Processing: anim='{currentAnim}', event='{eventName}', se_count={___se_count}");

            // Call base cycle-tracking logic
            instance.TrackCycles(__instance, spine, e, ___se_count);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[PilgrimPassPatch] Error in OnEvent: {ex.Message}");
        }
    }

    /// <summary>
    /// Public method for invoking handoff (used by DelayedHandoffScript)
    /// </summary>
    public static void ExecuteHandoff(object enemyInstance)
    {
        try
        {
            // Optimization: use cached playercon
            GameObject playerObject = UnifiedPlayerCacheManager.GetPlayerObject();
            if (playerObject == null)
            {
                UnityEngine.Debug.LogError("[PilgrimPassPatch] ExecuteHandoff: Player object not found!");
                return;
            }

            var player = playerObject.GetComponent<playercon>();
            if (player == null)
            {
                UnityEngine.Debug.LogError("[PilgrimPassPatch] ExecuteHandoff: Player component not found!");
                return;
            }

            // Mark enemy as disabled
            var disabledField = typeof(BaseEnemyPassPatch<PilgrimERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);
            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                disabledDict[enemyInstance] = true;
            }

            // Stop H-animation enemy
            var enemyComponent = enemyInstance as PilgrimERO;
            if (enemyComponent != null)
            {
                try
                {
                    var enemySpine = GetSpineAnimation(enemyComponent);
                    if (enemySpine != null)
                    {
                        enemySpine.AnimationState.ClearTracks();

                        // Try different idle animation names
                        string[] idleAnimations = { "idle", "Idle", "IDLE", "wait", "Wait", "WAIT" };
                        foreach (string animName in idleAnimations)
                        {
                            try
                            {
                                enemySpine.AnimationState.SetAnimation(0, animName, true);
                                break;
                            }
                            catch
                            {
                                // Try next animation
                            }
                        }
                    }

                    // Hide the enemy
                    var enemyMonoBehaviour = enemyComponent as MonoBehaviour;
                    if (enemyMonoBehaviour != null)
                    {
                        var meshRenderer = enemyMonoBehaviour.GetComponent<MeshRenderer>();
                        if (meshRenderer != null)
                        {
                            meshRenderer.enabled = false;
                        }

                        var spriteRenderer = enemyMonoBehaviour.GetComponent<SpriteRenderer>();
                        if (spriteRenderer != null)
                        {
                            spriteRenderer.enabled = false;
                        }

                        // Deactivate the enemy GameObject
                        enemyMonoBehaviour.gameObject.SetActive(false);
                    }
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[PilgrimPassPatch] Error stopping enemy animation: {ex.Message}");
                }
            }

            // Clear eroflag to interrupt the current H-scene
            player.eroflag = false;

            // Enable the player's sprite renderer
            var playerSpriteRenderer = playerObject.GetComponent<SpriteRenderer>();
            if (playerSpriteRenderer != null)
            {
                playerSpriteRenderer.enabled = true;
            }
        }
        catch (System.Exception ex)
        {
            // Ignore errors
        }
    }
}