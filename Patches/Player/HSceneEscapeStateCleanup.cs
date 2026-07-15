using NoREroMod.Patches.HellTraps;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.Dialogue;
using NoREroMod.Systems.Effects;
using NoREroMod.Systems.EventCore.Core;
using NoREroMod.Systems.Gameplay;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Restores visuals and stops H-scene overlay systems after Bad End Vengeance or struggle escape.
/// Fixes stuck black-background MindBroken ticks and invisible spine state after MindBreak Bad End.
/// </summary>
internal static class HSceneEscapeStateCleanup
{
    internal static void RestoreAfterBadEndVengeance(playercon player = null)
    {
        try
        {
            StopHSceneOverlaySystems();
            player ??= UnifiedPlayerCacheManager.GetPlayer();
            RestoreCombatantVisuals(player);
            LethalTrapDeathCleanup.EnsurePlayerVisuallyRestored(player);
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[HSceneEscapeStateCleanup] RestoreAfterBadEndVengeance: " + ex.Message);
        }
    }

    internal static void RestoreAfterStruggleEscape(playercon player = null)
    {
        try
        {
            StopHSceneOverlaySystems();
            player ??= UnifiedPlayerCacheManager.GetPlayer();
            ClearStaleHSceneDownState(player);
            RestoreCombatantVisuals(player);
            LethalTrapDeathCleanup.EnsurePlayerVisuallyRestored(player);

            if (player != null && Time.timeScale == 0f && !EventCorePause.IsFrozen && !MindBrokenBadEndSystem.IsBadEndActive)
                Time.timeScale = 1f;
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[HSceneEscapeStateCleanup] RestoreAfterStruggleEscape: " + ex.Message);
        }
    }

    /// <summary>
    /// After gangbang handoff: keep erodown prone state, only fix overlays / invisible spine.
    /// </summary>
    internal static void RestoreVisualsOnly(playercon player = null)
    {
        try
        {
            StopHSceneOverlaySystems();
            player ??= UnifiedPlayerCacheManager.GetPlayer();
            RestoreCombatantVisuals(player);
            LethalTrapDeathCleanup.EnsurePlayerVisuallyRestored(player);

            if (player != null && Time.timeScale == 0f && !EventCorePause.IsFrozen && !MindBrokenBadEndSystem.IsBadEndActive)
                Time.timeScale = 1f;
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[HSceneEscapeStateCleanup] RestoreVisualsOnly: " + ex.Message);
        }
    }

    /// <summary>
    /// If the FIN black screen stayed active outside an H-scene, stop it (stops passive MindBroken tick).
    /// </summary>
    internal static void ProcessStuckBlackBackgroundSafetyNet(playercon player)
    {
        if (player == null || player._Death)
            return;

        if (MindBrokenBadEndSystem.IsBadEndActive || EventCorePause.IsFrozen)
            return;

        if (!HSceneBlackBackgroundSystem.IsActive)
            return;

        if (player.eroflag)
            return;

        Plugin.Log?.LogInfo("[HSceneEscapeStateCleanup] Forcing H-scene black background off (stuck outside H-scene).");
        StopHSceneOverlaySystems();
    }

    private static void StopHSceneOverlaySystems()
    {
        DialogueFramework.DismissAllVisible();
        MindBrokenUIPatch.ForceShowLabelDuringBlackBackground = false;
        if (HSceneBlackBackgroundSystem.IsActive)
            HSceneBlackBackgroundSystem.Deactivate();
    }

    private static void ClearStaleHSceneDownState(playercon player)
    {
        if (player == null)
            return;

        if (!player.eroflag && player.erodown != 0)
            VanillaKnockdownRecoveryUtility.ApplyStandUpFromKnockdown(player);
    }

    private static void RestoreCombatantVisuals(playercon player)
    {
        if (player == null || !PlayerEroContextUtility.ShouldPreserveBadstatusBirthVisuals(player))
        {
            RestoreSkeletonHierarchy(player != null ? player.gameObject : null);

            GameObject playerObj = player != null ? player.gameObject : UnifiedPlayerCacheManager.GetPlayerObject();
            if (playerObj != null)
                RestoreSkeletonHierarchy(playerObj);
        }

        RestoreNearbyEnemyVisuals(player);
    }

    private static void RestoreNearbyEnemyVisuals(playercon player)
    {
        try
        {
            GameObject playerObj = player != null ? player.gameObject : UnifiedPlayerCacheManager.GetPlayerObject();
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            for (int i = 0; i < enemies.Length; i++)
            {
                GameObject enemy = enemies[i];
                if (enemy == null)
                    continue;

                if (playerObj != null)
                {
                    float dist = Vector3.Distance(playerObj.transform.position, enemy.transform.position);
                    if (dist > 24f)
                        continue;
                }

                RestoreSkeletonHierarchy(enemy);
            }
        }
        catch (System.Exception)
        {
        }
    }

    private static void RestoreSkeletonHierarchy(GameObject root)
    {
        if (root == null)
            return;

        SkeletonAnimation[] spines = root.GetComponentsInChildren<SkeletonAnimation>(true);
        for (int i = 0; i < spines.Length; i++)
            RestoreSkeleton(spines[i]);
    }

    internal static void RestoreSkeleton(SkeletonAnimation spine)
    {
        if (spine == null)
            return;

        if (!spine.enabled)
            spine.enabled = true;

        spine.timeScale = Mathf.Approximately(spine.timeScale, 0f) ? 1f : spine.timeScale;
        EnemyConstantVisibilityPatch.RestoreFullAlpha(spine);

        MeshRenderer meshRenderer = spine.GetComponent<MeshRenderer>();
        if (meshRenderer != null && !meshRenderer.enabled)
            meshRenderer.enabled = true;
    }
}
