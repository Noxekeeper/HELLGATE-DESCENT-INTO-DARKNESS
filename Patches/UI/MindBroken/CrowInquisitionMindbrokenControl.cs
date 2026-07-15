using System;
using System.Reflection;
using HarmonyLib;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.UI.MindBroken;

/// <summary>
/// Special MindBroken control for CrowInquisition - time-stop orgasm sequences.
/// Adds configurable MindBroken per second during IKI and IKI2 animations.
/// </summary>
internal static class CrowInquisitionMindbrokenControl
{
    internal static readonly string[] TargetAnimations = { "IKI", "IKI2" };

    // Cache reflection fields for performance
    private static readonly FieldInfo MySpineField = typeof(CrowInquisitionERO).GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance);

    [HarmonyPatch(typeof(CrowInquisitionERO), "Start")]
    [HarmonyPostfix]
    private static void AttachTracker(CrowInquisitionERO __instance)
    {
        try
        {
            if (__instance == null) return;
            var tracker = __instance.gameObject.GetComponent<CrowInquisitionMindbrokenTracker>();
            if (tracker == null)
            {
                tracker = __instance.gameObject.AddComponent<CrowInquisitionMindbrokenTracker>();
            }
            tracker.Init(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[CrowInquisitionMindbrokenControl] Error attaching tracker: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the private myspine field from CrowInquisitionERO instance.
    /// </summary>
    internal static SkeletonAnimation GetMySpine(CrowInquisitionERO instance)
    {
        if (instance == null) return null;
        try
        {
            return MySpineField?.GetValue(instance) as SkeletonAnimation;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Tracker for CrowInquisition time-stop orgasm sequences - adds MindBroken during IKI and IKI2 animations.
/// IKI: +6% per second, IKI2: +3% per second (configurable).
/// </summary>
internal class CrowInquisitionMindbrokenTracker : MonoBehaviour
{
    private CrowInquisitionERO host;
    private SkeletonAnimation spine;

    internal void Init(CrowInquisitionERO h)
    {
        host = h;
        spine = CrowInquisitionMindbrokenControl.GetMySpine(h);
    }

    private void Update()
    {
        if (host == null || spine == null || spine.AnimationState == null) return;

        string anim = spine.AnimationName ?? string.Empty;
        if (string.IsNullOrEmpty(anim)) return;

        // Check if we're in time-stop orgasm animations
        bool isIKIAnimation = anim == "IKI";
        bool isIKI2Animation = anim == "IKI2";

        // Add MindBroken during IKI (+6% per second)
        if (isIKIAnimation)
        {
            float mbPerSecondIKI = Plugin.crowInquisitionMindBrokenPerSecondIKI?.Value ?? 6f;
            MindBrokenSystem.AddPercent((mbPerSecondIKI / 100f) * Time.deltaTime, "crow-inquisition-iki");
        }

        // Add MindBroken during IKI2 (+3% per second)
        if (isIKI2Animation)
        {
            float mbPerSecondIKI2 = Plugin.crowInquisitionMindBrokenPerSecondIKI2?.Value ?? 3f;
            MindBrokenSystem.AddPercent((mbPerSecondIKI2 / 100f) * Time.deltaTime, "crow-inquisition-iki2");
        }
    }
}