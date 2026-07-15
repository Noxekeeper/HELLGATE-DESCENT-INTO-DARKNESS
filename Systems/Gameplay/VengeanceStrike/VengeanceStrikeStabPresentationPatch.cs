using HarmonyLib;
using NoREroMod.Systems.Rage;
using UnityEngine;

namespace NoREroMod.Systems.Gameplay;

/// <summary>Vengeance Strike: <see cref="playercon.Stab_fun"/> prefix/postfix. Rage gate and spend; slow-mo via <see cref="VengeanceStrikeRuntime"/>.</summary>
[HarmonyPatch(typeof(playercon), "Stab_fun")]
internal static class VengeanceStrikeStabPresentationPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(playercon __instance, ref bool enestabnow)
    {
        if (__instance == null) return true;
        if (!(Plugin.enableVengeanceStrikeRageCost?.Value ?? true)) return true;
        if (!RageSystem.Enabled) return true;

        float cost = Mathf.Clamp(Plugin.vengeanceStrikeRageCostPercent?.Value ?? 15f, 0f, 100f);
        if (cost <= 0f) return true;

        if (RageSystem.Percent < cost)
            return false;

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(playercon __instance, ref bool enestabnow)
    {
        // Vanilla sets enestabnow=true only when THIS call actually starts a new stab.
        // Do not key off player._stabnow — other enemies can call Stab_fun while already stabbing
        // and would re-charge Rage every call (can drain the bar to 0 mid-animation).
        if (__instance == null || !enestabnow) return;

        if ((Plugin.enableVengeanceStrikeRageCost?.Value ?? true) && RageSystem.Enabled)
        {
            float cost = Mathf.Clamp(Plugin.vengeanceStrikeRageCostPercent?.Value ?? 15f, 0f, 100f);
            if (cost > 0f)
                RageSystem.AddRage(-cost, "vengeance_strike");
        }

        VengeanceStrikeRuntime.TryBeginStabPresentation(__instance);
    }
}
