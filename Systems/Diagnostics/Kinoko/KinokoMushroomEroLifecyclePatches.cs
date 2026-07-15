using System;
using HarmonyLib;
using Spine;
using Spine.Unity;

namespace NoREroMod.Systems.Diagnostics.Kinoko;

/// <summary>
/// Logs every MushroomERO / GAMushroomERO OnEvent (before/after) and catches exceptions
/// without changing gameplay. NoREroMod stays enabled.
/// </summary>
internal static class KinokoMushroomEroLifecyclePatches
{
    [HarmonyPatch(typeof(MushroomERO), "OnEvent")]
    [HarmonyPrefix]
    private static void Mushroom_OnEvent_Prefix(
        MushroomERO __instance,
        SkeletonAnimation ___myspine,
        Spine.Event e,
        int ___se_count,
        int ___count)
    {
        LogPrefix(__instance, ___myspine, e, ___se_count, ___count);
    }

    [HarmonyPatch(typeof(MushroomERO), "OnEvent")]
    [HarmonyPostfix]
    private static void Mushroom_OnEvent_Postfix(
        MushroomERO __instance,
        SkeletonAnimation ___myspine,
        Spine.Event e,
        int ___se_count,
        int ___count)
    {
        LogPostfix(__instance, ___myspine, e, ___se_count, ___count);
    }

    [HarmonyPatch(typeof(MushroomERO), "OnEvent")]
    [HarmonyFinalizer]
    private static Exception Mushroom_OnEvent_Finalizer(
        MushroomERO __instance,
        SkeletonAnimation ___myspine,
        Spine.Event e,
        int ___se_count,
        int ___count,
        Exception __exception)
    {
        return LogFinalizer(__instance, ___myspine, e, ___se_count, ___count, __exception);
    }

    [HarmonyPatch(typeof(GAMushroomERO), "OnEvent")]
    [HarmonyPrefix]
    private static void GA_OnEvent_Prefix(
        GAMushroomERO __instance,
        SkeletonAnimation ___myspine,
        Spine.Event e,
        int ___se_count,
        int ___count)
    {
        LogPrefix(__instance, ___myspine, e, ___se_count, ___count);
    }

    [HarmonyPatch(typeof(GAMushroomERO), "OnEvent")]
    [HarmonyPostfix]
    private static void GA_OnEvent_Postfix(
        GAMushroomERO __instance,
        SkeletonAnimation ___myspine,
        Spine.Event e,
        int ___se_count,
        int ___count)
    {
        LogPostfix(__instance, ___myspine, e, ___se_count, ___count);
    }

    [HarmonyPatch(typeof(GAMushroomERO), "OnEvent")]
    [HarmonyFinalizer]
    private static Exception GA_OnEvent_Finalizer(
        GAMushroomERO __instance,
        SkeletonAnimation ___myspine,
        Spine.Event e,
        int ___se_count,
        int ___count,
        Exception __exception)
    {
        return LogFinalizer(__instance, ___myspine, e, ___se_count, ___count, __exception);
    }

    private static bool ShouldLog(string ev, string anim)
    {
        if (KinokoMushroomEroDiagnosticsConfig.LogAllEvents)
            return true;
        return KinokoMushroomEroDiagnosticsConfig.IsInterestingEventOrAnim(ev, anim);
    }

    private static void LogPrefix(
        object instance,
        SkeletonAnimation spine,
        Spine.Event e,
        int seCount,
        int count)
    {
        if (!KinokoMushroomEroDiagnosticsConfig.Enable)
            return;

        string ev = KinokoMushroomEroSnapshot.EventName(e);
        string anim = spine != null ? spine.AnimationName : null;
        if (!ShouldLog(ev, anim))
            return;

        KinokoMushroomEroMonitor.NotifyEvent(instance, spine);
        KinokoMushroomEroDiagLog.Info(
            KinokoMushroomEroSnapshot.Describe("PRE", instance, spine, e, seCount, count));
    }

    private static void LogPostfix(
        object instance,
        SkeletonAnimation spine,
        Spine.Event e,
        int seCount,
        int count)
    {
        if (!KinokoMushroomEroDiagnosticsConfig.Enable)
            return;

        string ev = KinokoMushroomEroSnapshot.EventName(e);
        string anim = spine != null ? spine.AnimationName : null;
        if (!ShouldLog(ev, anim))
            return;

        KinokoMushroomEroMonitor.NotifyEvent(instance, spine);
        KinokoMushroomEroDiagLog.Info(
            KinokoMushroomEroSnapshot.Describe("POST", instance, spine, e, seCount, count));
    }

    private static Exception LogFinalizer(
        object instance,
        SkeletonAnimation spine,
        Spine.Event e,
        int seCount,
        int count,
        Exception exception)
    {
        if (exception != null && KinokoMushroomEroDiagnosticsConfig.Enable)
        {
            KinokoMushroomEroDiagLog.Warn(
                "EX " + KinokoMushroomEroSnapshot.Describe("FAIL", instance, spine, e, seCount, count)
                + " ex=" + exception.GetType().Name + ": " + exception.Message);
        }

        // Preserve original exception behavior — do not swallow.
        return exception;
    }
}
