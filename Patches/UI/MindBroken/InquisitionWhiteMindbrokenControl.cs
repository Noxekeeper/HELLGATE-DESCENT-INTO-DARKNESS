using System;
using System.Reflection;
using HarmonyLib;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.UI.MindBroken;

/// <summary>
/// Special MindBroken control for InquisitionWhite - syringe injection effects.
/// Adds 8% MindBroken per second during ERO_START2 animation (syringe injection phase).
/// Triggers visual wave during ERO_START3 animation.
/// </summary>
internal static class InquisitionWhiteMindbrokenControl
{
    internal static readonly string[] TargetAnimations = { "ERO_START2", "ERO_START3" };

    // Cache reflection fields for performance
    private static readonly FieldInfo MySpineField = typeof(InquisitionWhiteERO).GetField("myspine", BindingFlags.NonPublic | BindingFlags.Instance);

    [HarmonyPatch(typeof(InquisitionWhiteERO), "Start")]
    [HarmonyPostfix]
    private static void AttachTracker(InquisitionWhiteERO __instance)
    {
        try
        {
            if (__instance == null) return;
            var tracker = __instance.gameObject.GetComponent<InquisitionWhiteMindbrokenTracker>();
            if (tracker == null)
            {
                tracker = __instance.gameObject.AddComponent<InquisitionWhiteMindbrokenTracker>();
            }
            tracker.Init(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[InquisitionWhiteMindbrokenControl] Error attaching tracker: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the private myspine field from InquisitionWhiteERO instance.
    /// </summary>
    internal static SkeletonAnimation GetMySpine(InquisitionWhiteERO instance)
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
/// Tracker for InquisitionWhite syringe injection - adds MindBroken during ERO_START2 animation.
/// Triggers visual wave during ERO_START3 animation.
/// Similar to Mutude system: +8% per second while ERO_START2 animation plays.
/// </summary>
internal class InquisitionWhiteMindbrokenTracker : MonoBehaviour
{
    private InquisitionWhiteERO host;
    private SkeletonAnimation spine;
    private bool isInjectionActive = false;
    private bool hasTriggeredWave = false;

    internal void Init(InquisitionWhiteERO h)
    {
        host = h;
        spine = InquisitionWhiteMindbrokenControl.GetMySpine(h);
    }

    private void Update()
    {
        if (host == null || spine == null || spine.AnimationState == null) return;

        string anim = spine.AnimationName ?? string.Empty;
        if (string.IsNullOrEmpty(anim)) return;

        // Check if we're in syringe injection animation (ERO_START2)
        bool isInjectionAnimation = anim == "ERO_START2";
        bool isWaveAnimation = anim == "ERO_START3";

        // Handle injection phase (ERO_START2)
        if (isInjectionAnimation && !isInjectionActive)
        {
            // Started syringe injection
            isInjectionActive = true;
        }
        else if (!isInjectionAnimation && isInjectionActive)
        {
            // Ended syringe injection
            isInjectionActive = false;
        }

        // Add MindBroken while injection is active (configurable rate)
        if (isInjectionAnimation)
        {
            // SYRINGE INJECTION: Add configurable MindBroken per second
            float mbPerSecond = Plugin.inquisitionWhiteMindBrokenPerSecond?.Value ?? 8f;
            MindBrokenSystem.AddPercent((mbPerSecond / 100f) * Time.deltaTime, "inquisition-white-syringe-injection");
        }

        // Handle wave effect during ERO_START3 (trigger once, if enabled)
        bool waveEnabled = Plugin.inquisitionWhiteEnableWaveEffect?.Value ?? true;
        if (waveEnabled)
        {
            if (isWaveAnimation && !hasTriggeredWave)
            {
                hasTriggeredWave = true;
                TriggerMindBrokenWave();
            }

            // Reset wave trigger when injection starts again (new cycle)
            if (isInjectionAnimation && hasTriggeredWave)
            {
                hasTriggeredWave = false;
            }

            // Also trigger wave at 100% MindBroken (legacy behavior)
            if (MindBrokenSystem.Percent >= 1.0f && !hasTriggeredWave)
            {
                hasTriggeredWave = true;
                TriggerMindBrokenWave();
            }
        }
    }

    /// <summary>
    /// Triggers the visual wave effect at 100% MindBroken (Dream distortion effect).
    /// </summary>
    private static void TriggerMindBrokenWave()
    {
        try
        {
            // Use existing MindBroken visual effects system to trigger dream wave
            MindBrokenVisualEffectsSystem.TriggerDreamEffectForced(5f); // 5 second duration
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[InquisitionWhiteMindbrokenTracker] Error triggering wave: {ex.Message}");
        }
    }
}