using HarmonyLib;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>
/// Replaces the vanilla creampie scale. When the Pregnancy module is enabled we drive
/// conception from the milliliter womb meter instead of the vanilla <c>BadstatusVal[2]</c>
/// "着床" scale, so both vanilla auto-conception triggers are neutralized:
///
///  * <c>PlayerStatus.CreampieVal_UI</c> — sets <c>BadstatusVal[2]=10</c> / <c>_Creampie=true</c>
///    and shows the creampie bar; skipped so the vanilla bar/flag never appear.
///  * <c>Buff.CreampieTime</c> — accumulates <c>BadstatusVal[2]</c> to 100 and invokes
///    <c>Pregnancystart</c>; skipped so vanilla never auto-conceives.
///
/// HELLGATE's <c>PregnancyClipTrigger</c> postfix on <c>CreampieVal_UI</c> still runs.
/// </summary>
[HarmonyPatch(typeof(PlayerStatus), "CreampieVal_UI")]
internal static class SuppressVanillaCreampieValUiPatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        // Returning false skips the vanilla body when the module owns the creampie scale.
        return !(PregnancyConfig.Enable != null && PregnancyConfig.Enable.Value);
    }
}

[HarmonyPatch(typeof(Buff), "CreampieTime")]
internal static class SuppressVanillaCreampieTimePatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        return !(PregnancyConfig.Enable != null && PregnancyConfig.Enable.Value);
    }
}

/// <summary>
/// Suppresses vanilla gestation-to-birth timing. HellGate drives pregnancy progress through its own
/// real-time trimester timer and calls <see cref="PlayerStatus.BirthAction"/> when the configured
/// duration elapses. This keeps the vanilla <c>_BadstatusVal[3]</c> bar as a visual progress indicator
/// without letting it trigger birth at the vanilla ~40 second mark.
/// </summary>
[HarmonyPatch(typeof(Buff), "PregnancyTime")]
internal static class SuppressVanillaPregnancyTimePatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        return !(PregnancyConfig.Enable != null && PregnancyConfig.Enable.Value);
    }
}
