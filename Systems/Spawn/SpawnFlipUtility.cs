using System;
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
        if (root == null)
            return;

        ApplyHorizontalFlip(root);

        SpawnFixedFacing facing = root.GetComponent<SpawnFixedFacing>();
        if (facing == null)
            facing = root.AddComponent<SpawnFixedFacing>();
        facing.FixedDir = -1;
    }

    internal static void ApplyHorizontalFlip(GameObject root)
    {
        if (root == null)
            return;

        Trapdata trapdata = root.GetComponentInChildren<Trapdata>(true);
        if (trapdata != null)
            ApplyTrapdataFacing(trapdata.gameObject, trapdata, -1);
        else if (TryGetEnemyDate(root, out EnemyDate enemyDate))
            ApplyEnemyDateFacing(enemyDate.gameObject, enemyDate, -1);
        else
            ApplySpineAndSpriteFlipLeft(root);
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

    /// <summary>Force face-left (-X scale) on root, Spine transforms, and sprites.</summary>
    internal static void ApplySpineAndSpriteFlipLeft(GameObject root)
    {
        if (root == null)
            return;

        SkeletonAnimation rootSpine = root.GetComponent<SkeletonAnimation>();
        if (rootSpine != null)
        {
            ForceTransformFaceLeft(root.transform);
        }
        else
        {
            SkeletonAnimation[] spines = root.GetComponentsInChildren<SkeletonAnimation>(true);
            for (int i = 0; i < spines.Length; i++)
            {
                SkeletonAnimation spine = spines[i];
                if (spine == null || HasSpineAnimationAncestor(spines, spine))
                    continue;

                ForceTransformFaceLeft(spine.transform);
            }

            if (spines.Length == 0)
                ForceTransformFaceLeft(root.transform);
        }

        SpriteRenderer[] sprites = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sprite = sprites[i];
            if (sprite != null)
                sprite.flipX = true;
        }
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
        if (target == null)
            return;

        Vector3 scale = target.localScale;
        float absX = Mathf.Abs(scale.x) > 0.001f ? Mathf.Abs(scale.x) : 1f;
        target.localScale = new Vector3(-absX, scale.y, scale.z);
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
