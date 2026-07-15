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
/// Patch for VagrantMainERO - player handoff after 1 cycle
/// </summary>
class VagrantPassPatch : BaseEnemyPassPatch<VagrantMainERO>
{
    // Alternate handoff types: true = JIGO, false = 2JIGO
    private static bool alternatePassType = true;

    // Spawn limit - once per event per session
    private static bool hasSpawnedOnERO5 = false;
    private static bool hasSpawnedOn2ERO4 = false;

    protected override string EnemyName => "Vagrant";

    protected override int CyclesBeforePass => 1; // Handoff after 1 full cycle

    protected override string[] GetHAnimations()
    {
        return new[]
        {
            "START", "EROSTART",
            "ERO", "ERO1", "ERO1_2", "ERO2", "ERO3", "ERO3_2", "ERO4", "ERO5",
            "FIN", "JIGO", "JIGO2",
            "2ERO", "2ERO1", "2ERO2", "2ERO3", "2ERO4", "2EROSTART",
            "2FIN", "2JIGO", "2JIGO2"
        };
    }

    protected override bool IsCycleComplete(string animationName, string eventName, int seCount)
    {
        // Alternate handoff types: JIGO -> 2JIGO -> JIGO -> 2JIGO...
        if (alternatePassType && eventName.Contains("JIGO") && !eventName.Contains("JIGO2"))
        {
            // Handoff on JIGO (first type)
            alternatePassType = false; // Next will be on 2JIGO
            Plugin.Log?.LogDebug($"[Vagrant] Cycle complete: JIGO event detected, next will be 2JIGO");
            return true;
        }
        else if (!alternatePassType && eventName.Contains("2JIGO"))
        {
            // Handoff on 2JIGO (second type)
            alternatePassType = true; // Next will be on JIGO
            Plugin.Log?.LogDebug($"[Vagrant] Cycle complete: 2JIGO event detected, next will be JIGO");
            return true;
        }

        return false;
    }

    protected override string GetEnemyTypeName()
    {
        return "vagrant";
    }

    internal static void ResetAll()
    {
        BaseEnemyPassPatch<VagrantMainERO>.ResetAll();
        alternatePassType = true; // Reset alternation to start with JIGO
        hasSpawnedOnERO5 = false; // Reset spawn flags
        hasSpawnedOn2ERO4 = false;
    }

    // Spawn additional Vagrant on ERO5 and 2ERO4 events (like undead)
    [HarmonyPatch(typeof(VagrantMainERO), "OnEvent")]
    [HarmonyPostfix]
    private static void SpawnAdditionalVagrant(VagrantMainERO __instance, Spine.Event e, int ___se_count)
    {
        try
        {
            string eventName = e.Data.Name;

            // Get animation via reflection
            var spineField = typeof(VagrantMainERO).GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance);
            var spine = spineField?.GetValue(__instance) as Spine.Unity.SkeletonAnimation;
            string animationName = spine?.AnimationName ?? "";

            // Spawn an additional Vagrant on ERO5 and 2ERO4 events (once per event)
            // Only in gangbang (when a handoff already happened from any enemy)
            if (EnemyHandoffSystem.GlobalHandoffCount > 0)
            {
                if (animationName == "ERO5" && eventName == "SE" && !hasSpawnedOnERO5)
                {
                    SpawnVagrantNearPlayer(__instance);
                    hasSpawnedOnERO5 = true;
                    Plugin.Log?.LogDebug("[VagrantPassPatch] Spawned Vagrant on ERO5 event");
                }
                else if (animationName == "2ERO4" && eventName == "SE" && !hasSpawnedOn2ERO4)
                {
                    SpawnVagrantNearPlayer(__instance);
                    hasSpawnedOn2ERO4 = true;
                    Plugin.Log?.LogDebug("[VagrantPassPatch] Spawned Vagrant on 2ERO4 event");
                }
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[VagrantPassPatch] Error in SpawnAdditionalVagrant: {ex.Message}");
        }
    }


    private static void SpawnVagrantNearPlayer(VagrantMainERO currentVagrant)
    {
        try
        {
            // Get Vagrant prefab
            GameObject vagrantPrefab = NoREroMod.Systems.Spawn.EnemyPrefabRegistry.GetPrefab("Vagrant");
            if (vagrantPrefab == null)
            {
                UnityEngine.Debug.LogError("[VagrantPassPatch] Vagrant prefab not found!");
                return;
            }

            // Optimization: use cached playercon
            GameObject playerObj = UnifiedPlayerCacheManager.GetPlayerObject();
            if (playerObj == null) return;

            Vector2 playerPos = playerObj.transform.position;

            // Spawn Vagrant ~150px away (Unity units) to the left or right
            float spawnDistance = 1.5f; // ~150px in Unity (at standard scale)
            bool spawnLeft = UnityEngine.Random.Range(0, 2) == 0;

            Vector2 spawnPos = spawnLeft
                ? new Vector2(playerPos.x - spawnDistance, playerPos.y)
                : new Vector2(playerPos.x + spawnDistance, playerPos.y);

            // Create Vagrant
            GameObject newVagrant = UnityEngine.Object.Instantiate(vagrantPrefab, spawnPos, Quaternion.identity);
            if (newVagrant != null)
            {
                newVagrant.SetActive(true);
                Plugin.Log?.LogDebug($"[VagrantPassPatch] Spawned additional Vagrant at ({spawnPos.x:F2}, {spawnPos.y:F2})");
                // Spawned Vagrant will start at mid via shared EnemyHandoffSystem.GlobalHandoffCount
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[VagrantPassPatch] Error spawning additional Vagrant: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(VagrantMainERO), "OnEvent")]
    [HarmonyPostfix]
    private static void VagrantPass(VagrantMainERO __instance, Spine.Event e, int ___se_count)
    {
        var instance = new VagrantPassPatch();
        SetInstance(instance);

        try
        {
            // Check whether the enemy is disabled
            var disabledField = typeof(BaseEnemyPassPatch<VagrantMainERO>)
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
                return; // H-scene not active
            }

            var spine = GetSpineAnimation(__instance);
            if (spine == null)
            {
                var spineField = typeof(VagrantMainERO).GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance);
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
            // (same as InquisitionRed). The game manages the animation chain itself.

            // Track cycles and handoff
            instance.TrackCycles(__instance, spine, e, ___se_count);
        }
        catch (System.Exception ex)
        {
            // Log errors for diagnostics
            UnityEngine.Debug.LogError($"[VagrantPassPatch] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Force mid-cycle animation for subsequent enemies
    /// </summary>
    protected override void ForceAnimationToMiddle(SkeletonAnimation spine)
    {
        try
        {
            if (spine == null) return;

            // Varied start points for subsequent Vagrant (without problematic ERO4 and 2ERO2)
            string[] startOptions = {
                "EROSTART",    // Start of first cycle
                "JIGO2",       // After JIGO of first cycle
                "START",       // From the very beginning
                "2EROSTART"    // Start of second cycle
            };

            // Pick a random start point
            string selectedStart = startOptions[UnityEngine.Random.Range(0, startOptions.Length)];

            spine.AnimationState.ClearTracks();
            spine.AnimationState.AddAnimation(0, selectedStart, false, 0f);
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[Vagrant] ForceAnimationToMiddle error: {ex.Message}");
        }
    }

    /// <summary>
    /// Public method for invoking handoff (used by DelayedHandoffScript)
    /// </summary>
    public static void ExecuteHandoff(object enemyInstance)
    {
        try
        {
            // Reset spawn flags for the next Vagrant
            hasSpawnedOnERO5 = false;
            hasSpawnedOn2ERO4 = false;

            // Optimization: use cached playercon
            GameObject playerObject = UnifiedPlayerCacheManager.GetPlayerObject();
            if (playerObject == null)
            {
                return;
            }

            // Mark enemy as disabled
            var disabledField = typeof(BaseEnemyPassPatch<VagrantMainERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);
            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                disabledDict[enemyInstance] = true;
            }

            // Stop H-animation enemy
            var enemyComponent = enemyInstance as VagrantMainERO;
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
                        enemyMonoBehaviour.gameObject.SetActive(false);
                    }
                }
                catch (System.Exception ex)
                {
                    // Ignore errors
                }
            }

            // Clear player animation
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

                // Fix: if enemy is left of the player, push right
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

            // Reset struggle flag
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