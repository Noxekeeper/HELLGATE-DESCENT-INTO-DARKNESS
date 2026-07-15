using HarmonyLib;

namespace NoREroMod.Systems.CombatAi.Factions.Patches;

/// <summary>
/// Creates the reputation HUD when the vanilla UI manager starts up.
/// Safe to call repeatedly — <see cref="FactionReputationHud.Ensure"/> is idempotent.
/// </summary>
[HarmonyPatch(typeof(UImng), "Start")]
internal static class FactionReputationHudBootstrapPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        try
        {
            if (!EnemyFactionsConfig.Enable)
            {
                FactionReputationHud.Destroy();
                return;
            }

            FactionReputationHud.Ensure();
        }
        catch { }
    }
}

/// <summary>
/// Also re-create after the bad status canvas starts — matches the pattern the Rage HUD uses
/// so we survive scene transitions.
/// </summary>
[HarmonyPatch(typeof(CanvasBadstatusinfo), "Start")]
internal static class FactionReputationHudBadstatusBootstrapPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        try
        {
            if (!EnemyFactionsConfig.Enable) return;
            FactionReputationHud.Ensure();
        }
        catch { }
    }
}
