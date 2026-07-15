using HarmonyLib;
using Spine;
using Spine.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using NoREroMod;
using NoREroMod.Patches.Enemy.Base;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Patch for UndeadERO - player handoff after a full cycle (on 3ERO_JIGO)
/// First Undead plays the full sequence; subsequent ones start at random points
/// Spawns additional Undead at certain stages
/// </summary>
class UndeadPassPatch : BaseEnemyPassPatch<UndeadERO>
{
    protected override string EnemyName => "Undead";

    protected override int CyclesBeforePass => 1; // Handoff after 1 full cycle

    protected override string[] GetHAnimations()
    {
        return new[]
        {
            // Startup animations
            "START",
            // First cycle
            "1ERO", "1ERO2", "1ERO3", "1EROFIN", "1EROFIN2",
            // Second cycle
            "2ERO_START", "2ERO1", "2ERO1_2", "2ERO2", "2ERO3", "2ERO4",
            // Third cycle
            "3ERO", "3ERO_2", "3ERO2", "3ERO3", "3ERO_FIN", "3ERO_FIN2",
            // JIGO animation
            "3ERO_JIGO", "3ERO_JIGO2"
        };
    }

    protected override bool IsCycleComplete(string animationName, string eventName, int seCount)
    {
        // Handoff on 3ERO_JIGO event (transition to final JIGO animation)
        return eventName == "3ERO_JIGO";
    }

    protected override void ForceAnimationToMiddle(SkeletonAnimation spine)
    {
        // For subsequent Undead pick a random start point from different cycles
        string[] startOptions = {
            "2ERO_START",  // Start of second cycle
            "1ERO",        // Start of first cycle
            "2ERO1",       // Midpoint of second cycle
            "3ERO3",       // Midpoint of third cycle
            "1ERO2"        // Midpoint of first cycle
        };

        string selectedStart = startOptions[UnityEngine.Random.Range(0, startOptions.Length)];

        Plugin.Log?.LogDebug($"[UndeadPassPatch] ForceAnimationToMiddle: Selected '{selectedStart}' for next undead");
        spine.state.SetAnimation(0, selectedStart, true);
        spine.timeScale = 1f;
    }

    protected override string GetEnemyTypeName()
    {
        return "undead";
    }

    internal static void ResetAll()
    {
        BaseEnemyPassPatch<UndeadERO>.ResetAll();
    }

    // Spawn additional Undead at certain stages
    [HarmonyPatch(typeof(UndeadERO), "OnEvent")]
    [HarmonyPostfix]
    private static void SpawnAdditionalUndead(UndeadERO __instance, Spine.Event e, int ___se_count)
    {
        try
        {
            string eventName = e.Data.Name;
            var spine = GetSpineAnimation(__instance);
            if (spine == null) return;
            string animationName = spine.AnimationName;

            // Spawn an additional Undead at certain stages
            if (ShouldSpawnAdditionalUndead(eventName, animationName))
            {
                SpawnUndeadNearPlayer(__instance);
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[UndeadPassPatch] Error in SpawnAdditionalUndead: {ex.Message}");
        }
    }

    private static bool ShouldSpawnAdditionalUndead(string eventName, string animationName)
    {
        // Spawn on transitions between cycles
        return eventName == "2ERO_START" ||  // Transition to second cycle
               eventName == "3ERO" ||        // Transition to third cycle
               eventName == "3ERO_JIGO";     // Transition to finale
    }

    private static void SpawnUndeadNearPlayer(UndeadERO currentUndead)
    {
        try
        {
            // Get Undead prefab
            GameObject undeadPrefab = NoREroMod.Systems.Spawn.EnemyPrefabRegistry.GetPrefab("Undead");
            if (undeadPrefab == null)
            {
                UnityEngine.Debug.LogError("[UndeadPassPatch] Undead prefab not found!");
                return;
            }

            // Optimization: use cached playercon
            GameObject playerObj = UnifiedPlayerCacheManager.GetPlayerObject();
            if (playerObj == null) return;

            Vector2 playerPos = playerObj.transform.position;

            // Spawn Undead 3 Unity units to the left or right
            float spawnDistance = 3f;
            bool spawnLeft = UnityEngine.Random.Range(0, 2) == 0;

            Vector2 spawnPos = spawnLeft
                ? new Vector2(playerPos.x - spawnDistance, playerPos.y)
                : new Vector2(playerPos.x + spawnDistance, playerPos.y);

            // Create Undead
            GameObject newUndead = UnityEngine.Object.Instantiate(undeadPrefab, spawnPos, Quaternion.identity);
            if (newUndead != null)
            {
                newUndead.SetActive(true);
                Plugin.Log?.LogDebug($"[UndeadPassPatch] Spawned additional Undead at ({spawnPos.x:F2}, {spawnPos.y:F2})");
                // Spawned Undead will start at mid via shared EnemyHandoffSystem.GlobalHandoffCount
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[UndeadPassPatch] Error spawning additional Undead: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(UndeadERO), "OnEvent")]
    [HarmonyPostfix]
    private static void UndeadPass(UndeadERO __instance, Spine.Event e, int ___se_count)
    {
        var instance = new UndeadPassPatch();
        SetInstance(instance);

        try
        {
            // Log patch call for debugging
            string eventStr = e?.ToString() ?? "NULL";
            Plugin.Log?.LogDebug($"[Undead PATCH] Called: event={eventStr}, se_count={___se_count}");

            // Check whether the enemy is disabled
            var disabledField = typeof(BaseEnemyPassPatch<UndeadERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);

            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                if (disabledDict != null && disabledDict.ContainsKey(__instance) && disabledDict[__instance])
                {
                    Plugin.Log?.LogDebug($"[Undead PATCH] Enemy disabled, skipping");
                    return;
                }
            }

            // Optimization: use cached playercon
            var player = UnifiedPlayerCacheManager.GetPlayer();
            if (player == null)
            {
                Plugin.Log?.LogDebug($"[Undead PATCH] Player is null");
                return;
            }

            Plugin.Log?.LogDebug($"[Undead PATCH] Player state: eroflag={player.eroflag}, erodown={player.erodown}");

            if (!player.eroflag || player.erodown == 0)
            {
                Plugin.Log?.LogDebug($"[Undead PATCH] H-scene not active (eroflag={player.eroflag}, erodown={player.erodown})");
                return; // H-scene not active
            }

            var spine = GetSpineAnimation(__instance);
            if (spine == null)
            {
                Plugin.Log?.LogDebug($"[Undead PATCH] Spine is null");
                return;
            }

            string currentAnim = spine.AnimationName;
            string eventName = e.Data.Name;

            // Check whether the current animation is an H-animation
            bool isHAnim = instance.IsHAnimation(currentAnim);
            Plugin.Log?.LogDebug($"[Undead PATCH] Is H-animation '{currentAnim}': {isHAnim}");

            if (!isHAnim)
            {
                Plugin.Log?.LogDebug($"[Undead PATCH] Not H-animation: '{currentAnim}'");
                return;
            }

            Plugin.Log?.LogDebug($"[Undead PATCH] Processing: anim='{currentAnim}', event='{eventName}', se_count={___se_count}");

            // Check if this is event cycle completion
            bool isCycleComplete = instance.IsCycleComplete(currentAnim, eventName, ___se_count);
            if (isCycleComplete)
            {
                Plugin.Log?.LogDebug($"[Undead PATCH] CYCLE COMPLETE DETECTED! (anim='{currentAnim}', event='{eventName}')");
            }

            // Invoke base cycle-tracking logic
            instance.TrackCycles(__instance, spine, e, ___se_count);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[UndeadPassPatch] Error in OnEvent: {ex.Message}");
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
                UnityEngine.Debug.LogError("[UndeadPassPatch] ExecuteHandoff: Player object not found!");
                return;
            }

            var player = playerObject.GetComponent<playercon>();
            if (player == null)
            {
                UnityEngine.Debug.LogError("[UndeadPassPatch] ExecuteHandoff: Player component not found!");
                return;
            }

            // Mark enemy as disabled
            var disabledField = typeof(BaseEnemyPassPatch<UndeadERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);
            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                disabledDict[enemyInstance] = true;
            }

            // Stop H-animation enemy
            var enemyComponent = enemyInstance as UndeadERO;
            if (enemyComponent != null)
            {
                try
                {
                    var enemySpine = GetSpineAnimation(enemyComponent);
                    if (enemySpine != null)
                    {
                        enemySpine.AnimationState.ClearTracks();

                        // Try different idle animation variants
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

                    // Make enemy invisible
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

                        // Deactivate the enemy object
                        enemyMonoBehaviour.gameObject.SetActive(false);
                    }
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[UndeadPassPatch] Error stopping enemy animation: {ex.Message}");
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