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
/// Patch for PrisonOfficerERO — handoff of GG after 1 cycle (on JIGO)
/// Optimized: Uses UnifiedPlayerCacheManager instead of FindGameObjectWithTag
/// </summary>
class PrisonOfficerPassPatch : BaseEnemyPassPatch<PrisonOfficerERO>
{
    protected override string EnemyName => "PrisonOfficer";

    protected override int CyclesBeforePass => 1; // Handoff after 1 full cycle

    protected override string[] GetHAnimations()
    {
        return new[]
        {
            "START", "START2", "START3", "START4",
            "ERO", "ERO1", "ERO2", "ERO3", "ERO4", "ERO5", "ERO6", "ERO7", "ERO8", "ERO9", "ERO10",
            "ERO11", "ERO12", "ERO13", "ERO14", "ERO15", "ERO16", "ERO17", "ERO18",
            "JIGO"
        };
    }

    protected override bool IsCycleComplete(string animationName, string eventName, int seCount)
    {
        // Prison officer cycle ends on the JIGO event (pull-out)
        if (eventName.Contains("JIGO"))
        {
            Plugin.Log?.LogDebug($"[PrisonOfficer] Cycle complete: JIGO event detected");
            return true;
        }

        return false;
    }

    protected override string GetEnemyTypeName()
    {
        return "prison_officer";
    }

    internal static void ResetAll()
    {
        BaseEnemyPassPatch<PrisonOfficerERO>.ResetAll();
    }

    [HarmonyPatch(typeof(PrisonOfficerERO), "OnEvent")]
    [HarmonyPostfix]
    private static void PrisonOfficerPass(PrisonOfficerERO __instance, Spine.Event e, int ___se_count)
    {
        var instance = new PrisonOfficerPassPatch();
        SetInstance(instance);

        try
        {
            // Skip if enemy is already disabled
            var disabledField = typeof(BaseEnemyPassPatch<PrisonOfficerERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);

            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                if (disabledDict != null && disabledDict.ContainsKey(__instance) && disabledDict[__instance])
                {
                    return;
                }
            }

            // Optimization: use cached playercon
            var player = UnifiedPlayerCacheManager.GetPlayer();
            if (player == null)
            {
                return;
            }

            if (!player.eroflag || player.erodown == 0)
            {
                return; // H-scene is not active
            }

            var spine = GetSpineAnimation(__instance);
            if (spine == null)
            {
                var spineField = typeof(PrisonOfficerERO).GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance);
                if (spineField != null)
                {
                    spine = spineField.GetValue(__instance) as Spine.Unity.SkeletonAnimation;
                }

                if (spine == null)
                {
                    return;
                }
            }

            string currentAnim = spine.AnimationName;

            if (!instance.IsHAnimation(currentAnim))
            {
                return; // Ignore combat animations
            }

            // REMOVED: "first capture" block — reset animation to START and blocked progress

            // Track cycles and handoff
            instance.TrackCycles(__instance, spine, e, ___se_count);
        }
        catch (System.Exception ex)
        {
            // Log errors for diagnostics
            UnityEngine.Debug.LogError($"[PrisonOfficerPassPatch] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Force animation to the mid-sequence start for subsequent enemies
    /// </summary>
    protected override void ForceAnimationToMiddle(SkeletonAnimation spine)
    {
        try
        {
            if (spine == null) return;

            // Prison officer subsequent grabs start at ERO3 (penetration begins)
            spine.AnimationState.ClearTracks();
            spine.AnimationState.AddAnimation(0, "ERO3", false, 0f);
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[PrisonOfficer] ForceAnimationToMiddle error: {ex.Message}");
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
                return;
            }

            // Mark enemy as disabled
            var disabledField = typeof(BaseEnemyPassPatch<PrisonOfficerERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);
            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                disabledDict[enemyInstance] = true;
            }

            // Stop H-animation enemy
            var enemyComponent = enemyInstance as PrisonOfficerERO;
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
                        enemyMonoBehaviour.gameObject.SetActive(false);
                    }
                }
                catch (System.Exception ex)
                {
                    // Ignore errors
                }
            }

            // Clear the player animation
            var playerSpine = playerObject.GetComponentInChildren<SkeletonAnimation>();
            if (playerSpine != null)
            {
                try
                {
                    playerSpine.AnimationState.ClearTracks();
                }
                catch (System.Exception ex)
                {
                    // Ignore errors
                }
            }

            // Get playercon
            var playerComponent = playerObject.GetComponent<playercon>();
            if (playerComponent == null)
            {
                return;
            }

            // Clear eroflag
            var eroFlagField = typeof(playercon).GetField("eroflag", BindingFlags.Public | BindingFlags.Instance);
            if (eroFlagField != null)
            {
                eroFlagField.SetValue(playerComponent, false);
            }

            // Set GG animation to lying
            string[] downAnims = { "DOWN", "down", "Idle", "idle" };
            foreach (string animName in downAnims)
            {
                if (playerSpine != null)
                {
                    try
                    {
                        playerSpine.AnimationState.SetAnimation(0, animName, true);
                        break;
                    }
                    catch (System.Exception ex)
                    {
                        // Ignore errors
                    }
                }
            }

            // Set erodown
            var eroDownField = typeof(playercon).GetField("erodown", BindingFlags.Public | BindingFlags.Instance);
            if (eroDownField != null)
            {
                eroDownField.SetValue(playerComponent, 1);
            }

            // Reset SP
            var playerStatus = playerObject.GetComponent<PlayerStatus>();
            if (playerStatus != null)
            {
                playerStatus.Sp = 0f;
            }

            // Push the player away from the enemy
            var enemyTransform = (enemyInstance as MonoBehaviour)?.transform;
            if (enemyTransform != null)
            {
                Vector3 enemyPos = enemyTransform.position;
                Vector3 playerPos = playerComponent.transform.position;
                Vector3 direction = playerPos - enemyPos;
                direction.Normalize();

                // Fix: if the enemy is to the left of the player, push right
                if (direction.x < 0)
                {
                    direction = Vector3.right;
                }
                else
                {
                    direction = Vector3.left;
                }

                float pushDistance = 2f;
                Vector3 newPosition = playerComponent.transform.position + (direction * pushDistance);
                playerComponent.transform.position = newPosition;

                // Reset vertical velocity
                var rigi2d = playerComponent.rigi2d;
                if (rigi2d != null)
                {
                    rigi2d.velocity = new Vector2(rigi2d.velocity.x, 0f);
                }
            }

            // Reset the struggle flag
            StruggleSystem.setStruggleLevel(-1);

            // Enable sprite renderer
            var spriteRenderer = playerObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
            }
        }
        catch (System.Exception ex)
        {
            // Ignore errors
        }
    }
}