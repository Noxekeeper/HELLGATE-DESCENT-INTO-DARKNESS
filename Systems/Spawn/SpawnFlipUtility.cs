using System;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>Horizontal mirror for template spawns and registry enemies.</summary>
internal static class SpawnFlipUtility
{
    internal static bool TryParseFlipToken(string token, out bool flipX)
    {
        flipX = false;
        if (token == null)
            return false;

        token = token.Trim();
        if (token.Length == 0)
            return false;

        if (string.Equals(token, "flip", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "mirror", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "-1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "left", StringComparison.OrdinalIgnoreCase))
        {
            flipX = true;
            return true;
        }

        if (string.Equals(token, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "right", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "noflip", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "0", StringComparison.OrdinalIgnoreCase))
        {
            flipX = false;
            return true;
        }

        return false;
    }

    /// <summary>Apply flip once and keep it locked against vanilla DIR_fun / mob facing resets.</summary>
    internal static void LockHorizontalFlipLeft(GameObject root)
    {
        ApplyFixedFacing(root, -1);
    }

    /// <summary>Lock facing right (no pack flip / undo flip).</summary>
    internal static void LockHorizontalFlipRight(GameObject root)
    {
        ApplyFixedFacing(root, 1);
    }

    /// <summary>Authoring helper: pack ,flip → lock left; otherwise restore spawn facing and drop the lock.</summary>
    internal static void ApplyAuthoringFlip(GameObject root, bool flipLeft)
    {
        if (flipLeft)
            ApplyFixedFacing(root, -1);
        else
            ClearFixedFacing(root);
    }

    /// <summary>Remove the facing lock and put Trapdata / EnemyDate / Spine back to pre-flip values.</summary>
    internal static void ClearFixedFacing(GameObject root)
    {
        if (root == null)
            return;

        SpawnFixedFacing facing = root.GetComponent<SpawnFixedFacing>();
        if (facing == null)
            facing = root.GetComponentInChildren<SpawnFixedFacing>(true);
        if (facing == null)
            return;

        facing.RestoreOriginalFacing();
        UnityEngine.Object.DestroyImmediate(facing);
    }

    private static void ApplyFixedFacing(GameObject root, int fixedDir)
    {
        if (root == null)
            return;

        if (fixedDir >= 0)
            fixedDir = 1;
        else
            fixedDir = -1;

        SpawnFixedFacing facing = root.GetComponent<SpawnFixedFacing>();
        if (facing == null)
            facing = root.AddComponent<SpawnFixedFacing>();
        facing.CaptureRestoreIfNeeded();
        facing.Arm(fixedDir);
    }

    internal static void ApplyHorizontalFlip(GameObject root)
    {
        ApplyHorizontalFacing(root, -1);
    }

    internal static void ApplyHorizontalFacing(GameObject root, int fixedDir)
    {
        if (root == null)
            return;

        if (fixedDir >= 0)
            fixedDir = 1;
        else
            fixedDir = -1;

        Trapdata trapdata = root.GetComponentInChildren<Trapdata>(true);
        if (trapdata != null)
        {
            ApplyTrapdataFacing(trapdata.gameObject, trapdata, fixedDir);
            return;
        }

        if (TryGetEnemyDate(root, out EnemyDate enemyDate))
            ApplyEnemyDateFacing(enemyDate.gameObject, enemyDate, fixedDir);

        ApplySpineAndSpriteFacing(root, fixedDir);
    }

    internal static bool TryGetEnemyDate(GameObject root, out EnemyDate enemyDate)
    {
        enemyDate = root != null ? root.GetComponentInChildren<EnemyDate>(true) : null;
        return enemyDate != null;
    }

    internal static void ApplyEnemyDateFacing(GameObject root, EnemyDate enemyDate, int fixedDir)
    {
        if (root == null || enemyDate == null)
            return;

        Vector3 s = enemyDate.scale;
        if (Mathf.Abs(s.x) < 0.001f && Mathf.Abs(s.y) < 0.001f && Mathf.Abs(s.z) < 0.001f)
            s = root.transform.localScale;

        float absX = Mathf.Abs(s.x) > 0.001f ? Mathf.Abs(s.x) : 1f;
        float absY = Mathf.Abs(s.y) > 0.001f ? Mathf.Abs(s.y) : 1f;
        float absZ = Mathf.Abs(s.z) > 0.001f ? Mathf.Abs(s.z) : 1f;
        s = new Vector3(fixedDir * absX, absY, absZ);
        enemyDate.scale = s;
        enemyDate.DIR = fixedDir;
        root.transform.localScale = s;
    }

    /// <summary>Force face-left on Spine (negative localScale.x) or SpriteRenderer.flipX — never both.</summary>
    internal static void ApplySpineAndSpriteFlipLeft(GameObject root)
    {
        ApplySpineAndSpriteFacing(root, -1);
    }

    internal static void ApplySpineAndSpriteFacing(GameObject root, int fixedDir)
    {
        if (root == null)
            return;

        if (fixedDir >= 0)
            fixedDir = 1;
        else
            fixedDir = -1;

        SkeletonAnimation rootSpine = root.GetComponent<SkeletonAnimation>();
        if (rootSpine != null)
        {
            ForceSpineFace(rootSpine, fixedDir);
            return;
        }

        SkeletonAnimation[] spines = root.GetComponentsInChildren<SkeletonAnimation>(true);
        if (spines != null && spines.Length > 0)
        {
            for (int i = 0; i < spines.Length; i++)
            {
                SkeletonAnimation spine = spines[i];
                if (spine == null || HasSpineAnimationAncestor(spines, spine))
                    continue;

                ForceSpineFace(spine, fixedDir);
            }
            return;
        }

        SpriteRenderer[] sprites = root.GetComponentsInChildren<SpriteRenderer>(true);
        bool flippedSprite = false;
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sprite = sprites[i];
            if (sprite == null)
                continue;
            sprite.flipX = fixedDir < 0;
            flippedSprite = true;
        }

        if (!flippedSprite)
            ForceTransformFace(root.transform, fixedDir);
    }

    private static void ForceSpineFace(SkeletonAnimation spine, int fixedDir)
    {
        if (spine == null)
            return;

        if (fixedDir >= 0)
            fixedDir = 1;
        else
            fixedDir = -1;

        // Mirror with transform.scale.x so SpawnSlave SetActive / Spine Initialize
        // cannot drop facing. Do not use skeleton.FlipX here — a new Skeleton
        // is created on re-enable with FlipX=false.
        ForceTransformFace(spine.transform, fixedDir);
    }

    private static bool HasSpineAnimationAncestor(SkeletonAnimation[] spines, SkeletonAnimation spine)
    {
        Transform transform = spine.transform;
        for (int i = 0; i < spines.Length; i++)
        {
            SkeletonAnimation other = spines[i];
            if (other == null || other == spine)
                continue;

            if (transform.IsChildOf(other.transform))
                return true;
        }

        return false;
    }

    private static void ForceTransformFaceLeft(Transform target)
    {
        ForceTransformFace(target, -1);
    }

    private static void ForceTransformFace(Transform target, int fixedDir)
    {
        if (target == null)
            return;

        if (fixedDir >= 0)
            fixedDir = 1;
        else
            fixedDir = -1;

        Vector3 scale = target.localScale;
        float absX = Mathf.Abs(scale.x) > 0.001f ? Mathf.Abs(scale.x) : 1f;
        target.localScale = new Vector3(fixedDir * absX, scale.y, scale.z);
    }

    internal static void ApplyTrapdataFacing(GameObject root, Trapdata trapdata, int fixedDir)
    {
        if (root == null || trapdata == null)
            return;

        Vector3 s = root.transform.localScale;
        float absX = Mathf.Abs(s.x) > 0.001f ? Mathf.Abs(s.x) : 1f;
        float absY = Mathf.Abs(s.y) > 0.001f ? Mathf.Abs(s.y) : 1f;
        float absZ = Mathf.Abs(s.z) > 0.001f ? Mathf.Abs(s.z) : 1f;
        s = new Vector3(fixedDir * absX, absY, absZ);
        root.transform.localScale = s;
        trapdata.scale = s;
        trapdata.DIR = fixedDir;
    }
}
