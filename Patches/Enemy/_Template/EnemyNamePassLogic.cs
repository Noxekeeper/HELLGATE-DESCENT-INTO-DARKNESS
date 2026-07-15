// ============================================================================
// COPY TEMPLATE
// ============================================================================
// INSTRUCTIONS:
// 1. Copy this file into your enemy folder (e.g. goblin/)
// 2. Rename the file (e.g. GoblinPassLogic.cs)
// 3. Replace every "EnemyName" with your enemy name
// 4. Replace "EnemyEroType" with the enemy's class type (find it in the game code)
// 5. Configure GetHAnimations() and IsCycleComplete()
// 6. Register in EnemyHandoffSystem.cs and DelayedHandoffScript.cs
// 7. Add the file to the .csproj
// ============================================================================

using HarmonyLib;
using UnityEngine;
using Spine.Unity;
using NoREroMod.Patches.Enemy.Base;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Grab, animation, and handoff logic for EnemyName
/// </summary>
class EnemyNamePassLogic : BaseEnemyPassPatch<EnemyEroType>
{
    protected override string EnemyName => "EnemyName";

    /// <summary>
    /// Cycles before handing off the player (1 for scarecrow, 2 for others)
    /// </summary>
    protected override int CyclesBeforePass => 2;

    /// <summary>
    /// H-animation list for this enemy type
    /// IMPORTANT: Use the correct animation names for your enemy!
    /// </summary>
    protected override string[] GetHAnimations()
    {
        return new[]
        {
            "START", "START2", "START3",
            "ERO", "ERO1", "ERO2", "ERO3", "ERO4", "ERO5",
            "FIN", "FIN2", "FIN3",
            "JIGO", "JIGO2"
        };
    }

    /// <summary>
    /// Detects completion of a full animation cycle
    /// IMPORTANT: Tune this to your animation flow!
    /// </summary>
    protected override bool IsCycleComplete(string animationName, string eventName, int seCount)
    {
        // EXAMPLE 1: Cycle ends on JIGO2 (like TouzokuNormal)
        if (animationName == "JIGO2" && eventName == "JIGO2")
        {
            return true;
        }

        // EXAMPLE 2: Cycle ends on FIN (like TouzokuAxe)
        if (animationName == "FIN" && eventName == "FIN")
        {
            return true;
        }

        // EXAMPLE 3: Fallback on ERO (start of the next cycle)
        if (animationName == "ERO" && eventName == "ERO")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Enemy name for the speech system (used by EnemySpeechPhrases)
    /// Must match an entry in the enemy list (e.g. "goblin", "sraimu")
    /// </summary>
    protected override string GetEnemyTypeName()
    {
        return "enemyname"; // Replace with the real name (e.g. "goblin", "sraimu")
    }

    /// <summary>
    /// Optional: override when you need custom force-to-middle animation logic
    /// </summary>
    protected override void ForceAnimationToMiddle(SkeletonAnimation spine)
    {
        // Override here if you need custom logic
        // Otherwise use the base implementation
        base.ForceAnimationToMiddle(spine);
    }

    /// <summary>
    /// Resets tracked data for this enemy type
    /// </summary>
    internal static void ResetAll()
    {
        BaseEnemyPassPatch<EnemyEroType>.ResetAll();
    }
    
    /// <summary>
    /// OnEvent patch — registered automatically via Harmony
    /// </summary>
    [HarmonyPatch(typeof(EnemyEroType), "OnEvent")]
    [HarmonyPostfix]
    private static void EnemyNamePass(EnemyEroType __instance, Spine.Event e, int ___se_count)
    {
        // Create an instance to access instance methods
        var instance = new EnemyNamePassLogic();
        SetInstance(instance);

        try
        {
            // Skip if the enemy is already disabled
            var disabledField = typeof(BaseEnemyPassPatch<EnemyEroType>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);

            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                if (disabledDict != null && disabledDict.ContainsKey(__instance) && disabledDict[__instance])
                {
                    return;
                }
            }

            // Require an active H-scene
            var player = GameObject.FindWithTag("Player")?.GetComponent<playercon>();
            if (player == null || !player.eroflag || player.erodown == 0)
            {
                return; // H-scene is not active
            }

            var spine = GetSpineAnimation(__instance);
            if (spine == null)
            {
                return;
            }

            string currentAnim = spine.AnimationName;

            // Ignore non-H (combat) animations
            if (!instance.IsHAnimation(currentAnim))
            {
                return; // Ignore combat animations
            }

            // Dialogue system hook (if needed)
            try
            {
                string eventName = e?.Data?.Name ?? e?.ToString() ?? string.Empty;
                NoREroMod.Systems.Dialogue.DialogueFramework.ProcessAnimationEvent(
                    __instance,
                    currentAnim,
                    eventName,
                    ___se_count
                );
            }
            catch (Exception ex)
            {
                // Ignore dialogue system errors
            }

            // Track cycles and handoff
            instance.TrackCycles(__instance, spine, e, ___se_count);
        }
        catch (System.Exception ex)
        {
            // Log errors for diagnostics
            Plugin.Log.LogError($"[EnemyNamePassLogic] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Public handoff entry point (used by DelayedHandoffScript)
    /// </summary>
    public static void ExecuteHandoff(object enemyInstance)
    {
        try
        {
            // Find the player
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null)
            {
                return;
            }

            // Mark the enemy as disabled
            var disabledField = typeof(BaseEnemyPassPatch<EnemyEroType>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);
            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                disabledDict[enemyInstance] = true;
            }

            // Stop the enemy's H-animation
            var enemyComponent = enemyInstance as EnemyEroType;
            if (enemyComponent != null)
            {
                try
                {
                    var enemySpine = GetSpineAnimation(enemyComponent);
                    if (enemySpine != null)
                    {
                        enemySpine.AnimationState.ClearTracks();

                        // Try common idle animation names
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
                                // Try the next animation name
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

            // Clear the player's animation
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

            // Set the player animation to downed/idle
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

                // If the enemy is to the left of the player, push right
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

                // Clear vertical velocity
                var rigi2d = playerComponent.rigi2d;
                if (rigi2d != null)
                {
                    rigi2d.velocity = new Vector2(rigi2d.velocity.x, 0f);
                }
            }

            // Reset the struggle flag
            StruggleSystem.setStruggleLevel(-1);

            // Enable the sprite renderer
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
