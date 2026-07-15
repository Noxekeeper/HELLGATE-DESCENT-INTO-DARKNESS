using HarmonyLib;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>
/// Creates / refreshes the womb meter HUD when the vanilla UI manager starts.
/// Pattern follows <c>GoldHudBootstrapPatch</c>: idempotent <c>Ensure</c>, safe to call
/// repeatedly across scene transitions.
/// </summary>
[HarmonyPatch(typeof(UImng), "Start")]
internal static class WombMeterHudBootstrapPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        try { WombMeterHud.Ensure(); }
        catch { }
    }
}

/// <summary>
/// Secondary bootstrap: bad-status canvas re-Start on scene change recreates the HUD
/// if it was destroyed during scene unload.
/// </summary>
[HarmonyPatch(typeof(CanvasBadstatusinfo), "Start")]
internal static class WombMeterHudBadstatusBootstrapPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        try { WombMeterHud.Ensure(); }
        catch { }
    }
}
