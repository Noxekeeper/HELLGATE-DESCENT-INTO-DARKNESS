using System;
using System.Reflection;
using HarmonyLib;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.UI.MindBroken;

internal static class PilgrimMindbrokenControl
{
    private static readonly FieldInfo MySpineField = typeof(PilgrimERO).GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance);

    [HarmonyPatch(typeof(PilgrimERO), "Start")]
    [HarmonyPostfix]
    private static void AttachTracker(PilgrimERO __instance)
    {
        if (__instance == null) return;
        try
        {
            var tracker = __instance.gameObject.GetComponent<PilgrimMindbrokenTracker>();
            if (tracker == null)
                tracker = __instance.gameObject.AddComponent<PilgrimMindbrokenTracker>();
            tracker.Init(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[PilgrimMindbrokenControl] Attach failed: {ex.Message}");
        }
    }

    internal static SkeletonAnimation GetMySpine(PilgrimERO instance)
    {
        if (instance == null) return null;
        try { return MySpineField?.GetValue(instance) as SkeletonAnimation; }
        catch { return null; }
    }
}

internal class PilgrimMindbrokenTracker : MonoBehaviour
{
    private PilgrimERO host;
    private SkeletonAnimation spine;

    internal void Init(PilgrimERO h)
    {
        host = h;
        spine = PilgrimMindbrokenControl.GetMySpine(h);
    }

    private void Update()
    {
        if (host == null || spine == null || spine.AnimationState == null) return;

        string anim = spine.AnimationName;
        if (string.IsNullOrEmpty(anim)) return;

        if (anim == "START2" || anim == "FERA1" || anim == "2EROFIN")
        {
            float rate = Plugin.pilgrimMindBrokenPerSecondBell?.Value ?? 2f;
            MindBrokenSystem.AddPercent((rate / 100f) * Time.deltaTime, "pilgrim-bell-hypnosis");
        }
    }
}