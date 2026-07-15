using System;
using System.Collections.Generic;
using System.Reflection;
using DarkTonic.MasterAudio;
using HarmonyLib;
using UnityEngine;
using Spine.Unity;
using NoREroMod;
using NoREroMod.Patches.Enemy.Base;
using NoREroMod.Patches.Player;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Patches.Enemy;

/// <summary>
/// Patch for MummyDogERO — handoff of GG after 1 cycle (on JIGO2)
/// </summary>
class MummyDogPassPatch : BaseEnemyPassPatch<MummyDogERO>
{
    protected override string EnemyName => "MummyDog";

    protected override int CyclesBeforePass => 1; // Handoff after 1 full cycle

    protected override string[] GetHAnimations()
    {
        return new[]
        {
            "START", "1ERO", "1ERO2", "1ERO3", "1ERO4",
            "2ERO", "2ERO2", "2ERO3", "2EROFIN", "2EROFIN2",
            "JIGO", "JIGO2"
        };
    }

    protected override bool IsCycleComplete(string animationName, string eventName, int seCount)
    {
        // Handoff on start of JIGO animation (JIGO event in 2EROFIN2)
        return eventName == "JIGO";
    }

    protected override void ForceAnimationToMiddle(SkeletonAnimation spine)
    {
        // Subsequent dogs start from JIGO2
        spine.state.SetAnimation(0, "JIGO2", false);
        spine.timeScale = 1f;
    }

    protected override string GetEnemyTypeName()
    {
        return "mummy_dog";
    }

    internal static void ResetAll()
    {
        BaseEnemyPassPatch<MummyDogERO>.ResetAll();
    }

    private static int _playerErodownBeforeFunNowdamage;

    /// <summary>
    /// Vanilla struggle escape clears erodown in fun_nowdamage but not always eroflag/erodata.
    /// MummyDog cleanup normally runs in MummyDog.eroanime() when eroflag and erodown==0 align;
    /// handoff timing or Wolf skeleton swaps can leave erodata playing with stale flags.
    /// </summary>
    internal static void AbortActiveMummyDogHSceneOnPlayerEscape(playercon player, bool requireErodownClear)
    {
        if (player == null)
        {
            return;
        }

        if (requireErodownClear && player.erodown != 0)
        {
            return;
        }

        if (!IsAnyMummyDogHSceneVisualActive())
        {
            return;
        }

        bool abortedAny = false;
        foreach (var dog in UnityEngine.Object.FindObjectsOfType<MummyDog>())
        {
            if (dog != null && TryAbortSingleMummyDogHScene(dog))
            {
                abortedAny = true;
            }
        }

        if (!abortedAny)
        {
            return;
        }

        ClearPlayerHSceneFlags(player);
        CancelPendingHandoff();
        ResetAll();
        PlayerCombatControlRecovery.RestoreAfterStruggleEscape();
    }

    private static bool IsAnyMummyDogHSceneVisualActive()
    {
        foreach (var dog in UnityEngine.Object.FindObjectsOfType<MummyDog>())
        {
            if (dog == null)
            {
                continue;
            }

            if (dog.eroflag || (dog.erodata != null && dog.erodata.activeSelf))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryAbortSingleMummyDogHScene(MummyDog dog)
    {
        if (dog == null)
        {
            return false;
        }

        bool eroActive = dog.erodata != null && dog.erodata.activeSelf;
        if (!dog.eroflag && !eroActive)
        {
            return false;
        }

        try
        {
            MasterAudio.StopBus("EroVoice");

            var eroSpine = dog.erodata != null ? dog.erodata.GetComponent<SkeletonAnimation>() : null;
            eroSpine?.AnimationState?.ClearTracks();

            var eroComponent = dog.erodata != null ? dog.erodata.GetComponent<MummyDogERO>() : null;
            if (eroComponent != null)
            {
                eroComponent.enabled = true;
                if (eroComponent.gameObject != null && dog.erodata != null && eroComponent.gameObject != dog.erodata && !eroComponent.gameObject.activeSelf)
                {
                    eroComponent.gameObject.SetActive(true);
                }
            }

            if (eroActive)
            {
                dog.erodata.SetActive(false);
            }

            dog.eroflag = false;

            var meshRenderer = AccessTools.Field(typeof(MummyDog), "myspinerennder")?.GetValue(dog) as MeshRenderer;
            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
            }

            var ui = AccessTools.Field(typeof(MummyDog), "UI")?.GetValue(dog) as GameObject;
            if (ui != null)
            {
                ui.SetActive(true);
            }

            var rigidBody = AccessTools.Field(typeof(EnemyDate), "rigi2D")?.GetValue(dog) as Rigidbody2D;
            if (rigidBody != null && !rigidBody.simulated)
            {
                rigidBody.simulated = true;
            }

            try
            {
                dog.ero_camerareset();
            }
            catch
            {
                // Camera reset is best-effort; H-scene teardown must still complete.
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[MummyDogPassPatch] Failed to abort H-scene: {ex.Message}");
            return false;
        }

        return true;
    }

    private static void ClearPlayerHSceneFlags(playercon player)
    {
        player.eroflag = false;

        try
        {
            Traverse.Create(player).Field("_eroflag2").SetValue(false);
        }
        catch
        {
            try
            {
                Traverse.Create(player).Field("eroflag2").SetValue(false);
            }
            catch
            {
                // Optional field name differs between builds.
            }
        }
    }

    private static void CancelPendingHandoff()
    {
        try
        {
            var playerObject = UnifiedPlayerCacheManager.GetPlayerObject();
            if (playerObject != null)
            {
                var script = playerObject.GetComponent<DelayedHandoffScript>();
                if (script != null)
                {
                    script.StopAllCoroutines();
                    UnityEngine.Object.Destroy(script);
                }
            }

            var temp = GameObject.Find("DelayedHandoffTemp");
            if (temp != null)
            {
                UnityEngine.Object.Destroy(temp);
            }
        }
        catch
        {
            // Handoff cancellation must not block escape cleanup.
        }
    }

    [HarmonyPatch(typeof(StruggleSystem), nameof(StruggleSystem.startGrabInvul))]
    [HarmonyPostfix]
    private static void OnStruggleEscapeCleanup()
    {
        try
        {
            var player = UnifiedPlayerCacheManager.GetPlayer();
            AbortActiveMummyDogHSceneOnPlayerEscape(player, requireErodownClear: false);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[MummyDogPassPatch] Struggle escape cleanup failed: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(playercon), "fun_nowdamage")]
    [HarmonyPrefix]
    private static void BeforeFunNowdamage(playercon __instance)
    {
        _playerErodownBeforeFunNowdamage = __instance != null ? __instance.erodown : 0;
    }

    [HarmonyPatch(typeof(playercon), "fun_nowdamage")]
    [HarmonyPostfix]
    private static void AfterFunNowdamage(playercon __instance)
    {
        try
        {
            if (__instance == null || _playerErodownBeforeFunNowdamage == 0 || __instance.erodown != 0)
            {
                return;
            }

            AbortActiveMummyDogHSceneOnPlayerEscape(__instance, requireErodownClear: true);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[MummyDogPassPatch] fun_nowdamage escape cleanup failed: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(playercon), nameof(playercon.ImmediatelyERO))]
    [HarmonyPostfix]
    private static void OnGiveUpCleanup(playercon __instance)
    {
        try
        {
            AbortActiveMummyDogHSceneOnPlayerEscape(__instance, requireErodownClear: false);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[MummyDogPassPatch] GiveUp cleanup failed: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(MummyDogERO), "OnEvent")]
    [HarmonyPostfix]
    private static void MummyDogPass(MummyDogERO __instance, Spine.Event e, int ___se_count)
    {
        var instance = new MummyDogPassPatch();
        SetInstance(instance);

        try
        {
            // Log patch call for debugging
            string eventStr = e?.ToString() ?? "NULL";
            Plugin.Log?.LogDebug($"[MummyDog PATCH] Called: event={eventStr}, se_count={___se_count}");

            // Skip if enemy is already disabled
            var disabledField = typeof(BaseEnemyPassPatch<MummyDogERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);

            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                if (disabledDict != null && disabledDict.ContainsKey(__instance) && disabledDict[__instance])
                {
                    Plugin.Log?.LogDebug($"[MummyDog PATCH] Enemy disabled, skipping");
                    return;
                }
            }

            // Optimization: use cached playercon
            var player = UnifiedPlayerCacheManager.GetPlayer();
            if (player == null)
            {
                Plugin.Log?.LogDebug($"[MummyDog PATCH] Player is null");
                return;
            }

            Plugin.Log?.LogDebug($"[MummyDog PATCH] Player state: eroflag={player.eroflag}, erodown={player.erodown}");

            if (!player.eroflag || player.erodown == 0)
            {
                Plugin.Log?.LogDebug($"[MummyDog PATCH] H-scene not active (eroflag={player.eroflag}, erodown={player.erodown})");
                return; // H-scene is not active
            }

            var spine = GetSpineAnimation(__instance);
            if (spine == null)
            {
                Plugin.Log?.LogDebug($"[MummyDog PATCH] Spine is null");
                return;
            }

            string currentAnim = spine.AnimationName;
            string eventName = e.Data.Name;

            // Verify current animation is an H-animation
            bool isHAnim = instance.IsHAnimation(currentAnim);
            Plugin.Log?.LogDebug($"[MummyDog PATCH] Is H-animation '{currentAnim}': {isHAnim}");

            if (!isHAnim)
            {
                Plugin.Log?.LogDebug($"[MummyDog PATCH] Not H-animation: '{currentAnim}'");
                return;
            }

            Plugin.Log?.LogDebug($"[MummyDog PATCH] Processing: anim='{currentAnim}', event='{eventName}', se_count={___se_count}");

            // Check if this is event cycle completion
            bool isCycleComplete = instance.IsCycleComplete(currentAnim, eventName, ___se_count);
            if (isCycleComplete)
            {
                Plugin.Log?.LogDebug($"[MummyDog PATCH] CYCLE COMPLETE DETECTED! (anim='{currentAnim}', event='{eventName}')");
            }

            // Call base cycle-tracking logic
            instance.TrackCycles(__instance, spine, e, ___se_count);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[MummyDogPassPatch] Error in OnEvent: {ex.Message}");
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
                UnityEngine.Debug.LogError("[MummyDogPassPatch] ExecuteHandoff: Player object not found!");
                return;
            }

            var player = playerObject.GetComponent<playercon>();
            if (player == null)
            {
                UnityEngine.Debug.LogError("[MummyDogPassPatch] ExecuteHandoff: Player component not found!");
                return;
            }

            // Mark enemy as disabled
            var disabledField = typeof(BaseEnemyPassPatch<MummyDogERO>)
                .GetField("enemyDisabled", BindingFlags.NonPublic | BindingFlags.Static);
            if (disabledField != null)
            {
                var disabledDict = disabledField.GetValue(null) as Dictionary<object, bool>;
                disabledDict[enemyInstance] = true;
            }

            // Stop H-animation enemy
            var enemyComponent = enemyInstance as MummyDogERO;
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
                    UnityEngine.Debug.LogError($"[MummyDogPassPatch] Error stopping enemy animation: {ex.Message}");
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