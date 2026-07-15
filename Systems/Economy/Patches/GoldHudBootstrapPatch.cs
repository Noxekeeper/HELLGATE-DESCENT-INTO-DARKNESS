using HarmonyLib;

namespace NoREroMod.Systems.Economy.Patches;

/// <summary>
/// Creates / refreshes the gold HUD when the vanilla UI manager starts.
/// Pattern follows <c>FactionReputationHudBootstrapPatch</c>: idempotent <c>Ensure</c>,
/// safe to call repeatedly across scene transitions.
/// </summary>
[HarmonyPatch(typeof(UImng), "Start")]
internal static class GoldHudBootstrapPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        try
        {
            if (!EconomicConfig.Enable || !EconomicConfig.Hud.Enable)
            {
                GoldHud.Destroy();
                return;
            }
            GoldHud.Ensure();
        }
        catch { }
    }
}

/// <summary>
/// Secondary bootstrap: bad-status canvas re-Start on scene change recreates the HUD
/// if it was destroyed during scene unload. Same trick the Faction HUD uses.
/// </summary>
[HarmonyPatch(typeof(CanvasBadstatusinfo), "Start")]
internal static class GoldHudBadstatusBootstrapPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        try
        {
            if (!EconomicConfig.Enable || !EconomicConfig.Hud.Enable) return;
            GoldHud.Ensure();
        }
        catch { }
    }
}
