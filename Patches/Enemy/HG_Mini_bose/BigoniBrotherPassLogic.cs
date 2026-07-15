using System;
using System.Linq;
using System.Collections;
using UnityEngine;
using HarmonyLib;
using NoREroMod;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Handoff logic for the player (GG) for BigoniBrother
/// </summary>
class BigoniBrotherPassLogic
{
    // Dictionary for tracking disabled BigoniBrother enemies (analogous to TouzokuNormalPassPatch)
    private static System.Collections.Generic.Dictionary<object, bool> enemyDisabled = new System.Collections.Generic.Dictionary<object, bool>();
    /// <summary>
    /// Reset gangbang-cycle state on escape from BigoniBrother
    /// Since BigoniBrother may be part of a chain with goblins,
    /// reset state for all enemies
    /// </summary>
    internal static void ResetAll()
    {
        try
        {
            // Plugin.Log.LogInfo("[BIGONI BROTHER] === RESET GANGBANG STATE ===");
            
            // Clear disabled-enemies dictionary (they are already not disabled after reset)
            enemyDisabled.Clear();
            
            // Reset JIGO3 guard (prevents duplicate goblin H-animations)
            BigoniBrotherPatch.ClearJigo3HandoffState();
            
            // Reset state for all enemies (for gangbang chain cases)
            // TouzokuNormalPassPatch.ResetAll();
            // GoblinPassPatch.ResetAll();
            
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[BIGONI BROTHER] Error during ResetAll: {ex.Message}");
        }
    }

    /// <summary>
    /// Disable BigoniBrother enemy
    /// </summary>
    /// <param name="enemyInstance">BigoniBrother enemy instance</param>
    internal static void DisableEnemy(object enemyInstance)
    {
        try
        {
            if (enemyInstance == null)
            {
                Plugin.Log.LogWarning("[BIGONI BROTHER] DisableEnemy called with null enemyInstance");
                return;
            }

            // Mark enemy as disabled
            enemyDisabled[enemyInstance] = true;
            
            Plugin.Log.LogInfo($"[BIGONI BROTHER] Enemy {enemyInstance.GetType().Name} disabled");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[BIGONI BROTHER] Error in DisableEnemy: {ex.Message}");
        }
    }

    /// <summary>
    /// Check whether BigoniBrother enemy is disabled
    /// </summary>
    /// <param name="enemyInstance">BigoniBrother enemy instance</param>
    /// <returns>true if the enemy is disabled</returns>
    internal static bool IsEnemyDisabled(object enemyInstance)
    {
        try
        {
            if (enemyInstance == null)
                return false;
                
            return enemyDisabled.ContainsKey(enemyInstance) && enemyDisabled[enemyInstance];
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[BIGONI BROTHER] Error in IsEnemyDisabled: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Public method for invoking handoff (used by DelayedHandoffScript)
    /// </summary>
    public static void ExecuteHandoff(object enemyInstance)
    {
        EnemyHandoffSystem.GlobalHandoffCount++;
        PushPlayerAwayFromEnemy(enemyInstance);
    }

    /// <summary>
    /// Remove BigoniBrother enemy from the disabled list
    /// (on death or H-scene end)
    /// </summary>
    /// <param name="enemyInstance">BigoniBrother enemy instance</param>
    internal static void RemoveDisabledEnemy(object enemyInstance)
    {
        try
        {
            if (enemyInstance != null && enemyDisabled.ContainsKey(enemyInstance))
            {
                enemyDisabled.Remove(enemyInstance);
                Plugin.Log.LogInfo($"[BIGONI BROTHER] Removed disabled enemy {enemyInstance.GetType().Name}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[BIGONI BROTHER] Error in RemoveDisabledEnemy: {ex.Message}");
        }
    }

    /// <summary>
    /// Push the player away from the enemy and hide BigoniBrother (analogous to GoblinPassLogic)
    /// </summary>
    private static void PushPlayerAwayFromEnemy(object enemyInstance)
    {
        // Plugin.Log.LogInfo( "[BIGONI BROTHER] === Pushing GG away ===");

        try
        {
            // Find the player
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null)
            {
                return;
            }

            // Get component enemy
            var bigoni = enemyInstance as Bigoni;
            if (bigoni == null)
            {
                return;
            }

            // Reset enemy eroflag BEFORE stopping animation
            try
            {
                bigoni.eroflag = false;
            }
            catch (System.Exception ex)
            {
            }

            // Stop enemy H-animation before hiding (important! otherwise the animation will hang)
            try
            {
                // Stop main enemy animation (erospine)
                var erospineField = typeof(Bigoni).GetField("erospine",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (erospineField != null)
                {
                    var erospine = erospineField.GetValue(bigoni) as Spine.Unity.SkeletonAnimation;
                    if (erospine != null)
                    {
                        erospine.AnimationState.ClearTracks();
                    }
                }

                // Get erodata via reflection
                var erodataField = typeof(Bigoni).GetField("erodata",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (erodataField != null)
                {
                    GameObject erodata = erodataField.GetValue(bigoni) as GameObject;
                    if (erodata != null)
                    {
                        // Deactivate erodata (important! stops H-animation)
                        erodata.SetActive(false);

                        // Get StartBigoniERO component
                        var startBigoniERO = erodata.GetComponent<StartBigoniERO>();
                        if (startBigoniERO != null)
                        {
                            // Get myspine via reflection
                            var myspineField = typeof(StartBigoniERO).GetField("myspine",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (myspineField != null)
                            {
                                var enemySpine = myspineField.GetValue(startBigoniERO) as Spine.Unity.SkeletonAnimation;
                                if (enemySpine != null)
                                {
                                    enemySpine.AnimationState.ClearTracks();
                                }
                            }
                        }

                        // Also check BigoniERO (if used)
                        var bigoniERO = erodata.GetComponent<BigoniERO>();
                        if (bigoniERO != null)
                        {
                            var myspineField = typeof(BigoniERO).GetField("myspine",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (myspineField != null)
                            {
                                var enemySpine = myspineField.GetValue(bigoniERO) as Spine.Unity.SkeletonAnimation;
                                if (enemySpine != null)
                                {
                                    enemySpine.AnimationState.ClearTracks();
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                // Ignore errors - not critical
            }

            // Hide the enemy completely (SetActive(false) - as before)
            try
            {
                bigoni.gameObject.SetActive(false);
                // Plugin.Log.LogInfo( "[BIGONI BROTHER] Enemy GameObject hidden (SetActive(false))");
            }
            catch (System.Exception ex)
            {
            }

            // CLEAR PLAYER ANIMATION (important! without this the player stays in the H-scene)
            var playerSpine = playerObject.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
            if (playerSpine != null)
            {
                try
                {
                    playerSpine.AnimationState.ClearTracks();
                    // Plugin.Log.LogInfo( "[BIGONI BROTHER] Player spine cleared");
                }
                catch (System.Exception ex)
                {
                }
            }

            // Get playercon and reset state
            var playerComponent = playerObject.GetComponent<playercon>();
            if (playerComponent != null)
            {
                playerComponent.eroflag = false;
                playerComponent._eroflag2 = false;

                // CRITICAL: set player.state = "DOWN" for correct goblin behavior
                // Goblins check this.com_player.state == "DOWN" before grab
                var stateField = typeof(playercon).GetField("state",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (stateField != null)
                {
                    try
                    {
                        stateField.SetValue(playerComponent, "DOWN");
                        // Plugin.Log.LogInfo("[BIGONI BROTHER] Player state set to 'DOWN' for goblin compatibility");
                    }
                    catch (System.Exception ex)
                    {
                        // Ignore errors
                    }
                }

                // Set player animation to lying (if not already set)
                if (playerSpine != null)
                {
                    string[] downAnims = { "DOWN", "down", "Idle", "idle" };
                    foreach (string animName in downAnims)
                    {
                        try
                        {
                            playerSpine.AnimationState.SetAnimation(0, animName, true);
                            // Plugin.Log.LogInfo( $"[BIGONI BROTHER] GG animation set to '{animName}'");
                            break;
                        }
                        catch (System.Exception ex)
                        {
                        }
                    }
                }

                // Set erodown via reflection (ALWAYS set to 1 for DOWN state)
                var eroDownField = typeof(playercon).GetField("erodown", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (eroDownField != null)
                {
                    try
                    {
                        eroDownField.SetValue(playerComponent, 1);
                        // Plugin.Log.LogInfo( "[BIGONI BROTHER] erodown set to 1 (DOWN state)");
                    }
                    catch (System.Exception ex)
                    {
                    }
                }

                // Reset blocking combat and defense flags
                playerComponent.Attacknow = false;
                playerComponent.Actstate = false;
                playerComponent.stepfrag = false;
                playerComponent.magicnow = false;
                playerComponent.guard = false;

                var parryField = typeof(playercon).GetField("Parry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                parryField?.SetValue(playerComponent, false);

                var itemUseField = typeof(playercon).GetField("Itemuse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                itemUseField?.SetValue(playerComponent, false);

                var stabNowField = typeof(playercon).GetField("stabnow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                stabNowField?.SetValue(playerComponent, false);

                playerComponent._easyESC = false;
                playerComponent.nowdamage = playerComponent.erodown != 0;
                StruggleSystem.setStruggleLevel(-1f);
                Time.timeScale = 1f;
                
                // CRITICAL: restore player physics (otherwise the player gets stuck: no movement, attacks pass through)
                try
                {
                    if (playerComponent.rigi2d != null)
                    {
                        playerComponent.rigi2d.simulated = true;
                        playerComponent.rigi2d.velocity = Vector2.zero; // Reset velocity
                        playerComponent.rigi2d.angularVelocity = 0f; // Reset angular velocity
                        // Plugin.Log.LogInfo( "[BIGONI BROTHER] Restored player physics: simulated=true, velocity=zero");
                    }
                }
                catch (System.Exception ex)
                {
                    // Plugin.Log.LogError($"[BIGONI BROTHER] Failed to restore player physics: {ex.Message}");
                }
            }
            else
            {
            }

            // Plugin.Log.LogInfo( "[BIGONI BROTHER] Player should be free now");
        }
        catch (System.Exception ex)
        {
        }
    }

    // Patch on ImmediatelyERO for cleanup on escape via GiveUp
    // By analogy with TouzokuNormalPassPatch, TouzokuAxePassPatch and GoblinPassLogic
    [HarmonyPatch(typeof(playercon), "ImmediatelyERO")]
    [HarmonyPostfix]
    static void ClearStateOnImmediatelyERO()
    {
        try
        {
            // Check enemy type - clear only for BigoniBrother
            Bigoni currentEnemy = UnityEngine.Object.FindObjectOfType<Bigoni>();
            if (!BigoniBrotherIdentity.IsBrother(currentEnemy))
            {
                // Not BigoniBrother - do not clear
                return;
            }

            // Plugin.Log.LogInfo( "[BIGONI BROTHER] === CLEAR ON IMMEDIATELYERO (GiveUp) ===");
            ResetAll();
        }
        catch (System.Exception ex)
        {
        }
    }

    // Patch on StruggleSystem.startGrabInvul for cleanup on manual struggle
    // By analogy with TouzokuNormalPassPatch, TouzokuAxePassPatch and GoblinPassLogic
    [HarmonyPatch(typeof(StruggleSystem), "startGrabInvul")]
    [HarmonyPostfix]
    static void ClearStateOnStruggleEscape()
    {
        try
        {
            // Plugin.Log.LogInfo( "[BIGONI BROTHER] === CLEAR ON STRUGGLE ESCAPE ===");
            ResetAll();
        }
        catch (System.Exception ex)
        {
        }
    }
}