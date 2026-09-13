using HarmonyLib;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.DeadArmor;

internal enum DeadArmorBreakKind
{
    Physical,
    Magic
}

/// <summary>Spawns DeadArmor death overlays from bone position + enemy facing.</summary>
internal static class DeadArmorPlayback
{
    private static FieldInfoCache _fields;

    private sealed class FieldInfoCache
    {
        internal readonly System.Reflection.FieldInfo Dir;
        internal readonly System.Reflection.FieldInfo MySpine;

        internal FieldInfoCache()
        {
            Dir = AccessTools.Field(typeof(EnemyDate), "DIR");
            MySpine = AccessTools.Field(typeof(SlaveBigAxe), "mySpine")
                ?? AccessTools.Field(typeof(EnemyDate), "mySpine");
        }
    }

    private static FieldInfoCache Fields
    {
        get
        {
            if (_fields == null)
                _fields = new FieldInfoCache();
            return _fields;
        }
    }

    internal static void Play(EnemyDate enemy, DeadArmorBreakKind kind)
    {
        if (!DeadArmorConfig.IsEnabled || enemy == null)
            return;

        Sprite[] frames = kind == DeadArmorBreakKind.Magic
            ? DeadArmorSpriteCache.GetMagicFrames()
            : DeadArmorSpriteCache.GetPhysicalFrames();

        if (frames == null || frames.Length == 0)
        {
            Plugin.Log?.LogWarning("[DeadArmor] No frames for " + kind);
            return;
        }

        Vector3 start = ResolveBoneWorld(enemy);
        start.z = enemy.transform.position.z;
        bool faceLeft = ResolveFaceLeft(enemy);
        string layer;
        int order;
        ResolveSorting(enemy, out layer, out order);

        float scale = DeadArmorConfig.DisplayScale != null ? DeadArmorConfig.DisplayScale.Value : 1f;
        float frameSec = DeadArmorConfig.FrameSeconds != null ? DeadArmorConfig.FrameSeconds.Value : 0.087f;
        float fall = DeadArmorConfig.PickFallDistance();
        float fallSpeed = DeadArmorConfig.FallSpeedMultiplier != null ? DeadArmorConfig.FallSpeedMultiplier.Value : 4.5f;
        float hold = DeadArmorConfig.HoldLastFrameSeconds != null ? DeadArmorConfig.HoldLastFrameSeconds.Value : 10f;

        DeadArmorClipPlayer spawned = DeadArmorClipPlayer.Spawn(
            start, frames, faceLeft, scale, frameSec, fall, order, layer, hold, fallSpeed);
        DeadArmorAudio.PlayRandom();

        DeadArmorConfig.LogDebug(
            "[DeadArmor] PLAY " + kind
            + " frames=" + frames.Length
            + " pos=" + start
            + " faceLeft=" + faceLeft
            + " scale=" + scale
            + " frameSec=" + frameSec
            + " layer=" + layer
            + " order=" + order
            + " ok=" + (spawned != null));
    }

    private static void ResolveSorting(EnemyDate enemy, out string layerName, out int order)
    {
        layerName = "Default";
        order = DeadArmorConfig.SortingOrder != null ? DeadArmorConfig.SortingOrder.Value : 80;

        try
        {
            MeshRenderer mesh = enemy.GetComponent<MeshRenderer>();
            if (mesh == null)
                mesh = enemy.GetComponentInChildren<MeshRenderer>();
            if (mesh != null)
            {
                if (!string.IsNullOrEmpty(mesh.sortingLayerName))
                    layerName = mesh.sortingLayerName;
                order = mesh.sortingOrder + 20;
            }
        }
        catch
        {
        }
    }

    private static bool ResolveFaceLeft(EnemyDate enemy)
    {
        try
        {
            if (Fields.Dir != null)
            {
                object raw = Fields.Dir.GetValue(enemy);
                if (raw is int dir)
                    return dir < 0;
            }
        }
        catch
        {
        }

        return enemy.transform.localScale.x < 0f;
    }

    private static Vector3 ResolveBoneWorld(EnemyDate enemy)
    {
        Vector3 fallback = enemy.transform.position;
        try
        {
            SkeletonAnimation spine = null;
            if (Fields.MySpine != null)
                spine = Fields.MySpine.GetValue(enemy) as SkeletonAnimation;
            if (spine == null)
                spine = enemy.GetComponent<SkeletonAnimation>();
            if (spine == null || spine.skeleton == null)
                return fallback;

            spine.skeleton.UpdateWorldTransform();

            string boneName = DeadArmorConfig.BoneName != null ? DeadArmorConfig.BoneName.Value : "bone2";
            if (string.IsNullOrEmpty(boneName))
                boneName = "bone2";

            Bone bone = spine.skeleton.FindBone(boneName);
            if (bone == null)
            {
                Plugin.Log?.LogWarning("[DeadArmor] Bone not found: " + boneName + " — using enemy root");
                return fallback;
            }

            return spine.transform.TransformPoint(bone.WorldX, bone.WorldY, 0f);
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[DeadArmor] Bone resolve failed: " + ex.Message);
            return fallback;
        }
    }
}
