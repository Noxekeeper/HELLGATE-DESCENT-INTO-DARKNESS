using HarmonyLib;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.Gameplay;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod;

/// <summary>
/// Shared gangbang handoff: player prone + push, and parent <see cref="EnemyDate"/> restore after hiding erodata child.
/// Same contract as <see cref="TouzokuNormalPassPatch"/> / <see cref="TouzokuAxePassPatch"/>.
/// </summary>
internal static class EnemyHandoffPlayerHelper
{
    internal static void ApplyStandardHandoffState(playercon player, PlayerStatus status, Transform enemyTransform)
    {
        if (player == null)
            return;

        GameObject playerObject = player.gameObject;
        SkeletonAnimation playerSpine = playerObject.GetComponentInChildren<SkeletonAnimation>();
        if (playerSpine != null)
        {
            try
            {
                playerSpine.AnimationState?.ClearTracks();
            }
            catch
            {
            }
        }

        player.eroflag = false;
        player._eroflag2 = false;
        player.erodown = 1;
        player._easyESC = false;
        player.nowdamage = player.erodown != 0;

        AccessTools.Field(typeof(playercon), "downup")?.SetValue(player, 0);

        string[] downAnims = { "DOWN", "down", "Idle", "idle" };
        for (int i = 0; i < downAnims.Length; i++)
        {
            if (playerSpine == null)
                break;
            try
            {
                playerSpine.AnimationState.SetAnimation(0, downAnims[i], true);
                break;
            }
            catch
            {
            }
        }

        if (AccessTools.Field(typeof(playercon), "uiface")?.GetValue(player) is SkeletonGraphic uiface)
        {
            string faceDown = status != null && status.CostumeBreak == 1 ? "DOWN" : "DOWN2";
            try
            {
                uiface.AnimationState.SetAnimation(0, faceDown, true);
            }
            catch
            {
                try { uiface.AnimationState.SetAnimation(0, "DOWN", true); }
                catch { }
            }
        }

        if (status != null)
            status.Sp = 0f;

        StruggleSystem.setStruggleLevel(-1);

        if (enemyTransform != null)
            PushPlayerFromTransform(player, enemyTransform);

        SpriteRenderer spriteRenderer = playerObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            spriteRenderer.enabled = true;
    }

    internal static void PushPlayerFromTransform(playercon player, Transform enemyTransform)
    {
        if (player == null || enemyTransform == null)
            return;

        Vector3 enemyPos = enemyTransform.position;
        Vector3 playerPos = player.transform.position;
        Vector3 direction = playerPos - enemyPos;
        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();
        else
            direction = Vector3.right;

        if (direction.x < 0f)
            direction = Vector3.right;
        else
            direction = Vector3.left;

        player.transform.position = playerPos + (direction * 2f);

        if (player.rigi2d != null)
            player.rigi2d.velocity = new Vector2(player.rigi2d.velocity.x, 0f);
    }

    /// <summary>
    /// erodata lives on a child; combat body is the parent <see cref="EnemyDate"/> (TouzokuAxe / Kakash pattern).
    /// </summary>
    internal static void RestoreEnemyDateParentAfterEro(EnemyDate parent, GameObject erodataObject)
    {
        if (parent == null)
            return;

        parent.eroflag = false;

        MeshRenderer meshRenderer = parent.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.enabled = true;

        SkeletonAnimation combatSpine = parent.GetComponent<SkeletonAnimation>();
        if (combatSpine != null)
        {
            if (!combatSpine.enabled)
                combatSpine.enabled = true;
            EnemyConstantVisibilityPatch.RestoreFullAlpha(combatSpine);
        }

        Transform ui = parent.transform.Find("Canvas");
        if (ui != null)
            ui.gameObject.SetActive(true);

        Rigidbody2D body = parent.GetComponent<Rigidbody2D>();
        if (body != null && !body.simulated)
            body.simulated = true;

        if (erodataObject != null)
            erodataObject.SetActive(false);
    }
}
