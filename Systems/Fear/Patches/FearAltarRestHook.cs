using System;
using HarmonyLib;

namespace NoREroMod.Systems.Fear.Patches;

/// <summary>Altar use (<see cref="Savepoint_on"/> full restore) resets Morale to 0.</summary>
internal static class FearAltarRestHook
{
    [HarmonyPatch(typeof(Savepoint_on), "fun_ALLreset")]
    [HarmonyPostfix]
    private static void AllReset_Postfix()
    {
        try
        {
            if (!FearConfig.IsEnabled)
                return;
            FearSystem.ResetOnAltarRest();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Fear] Altar rest reset failed: " + ex.Message);
        }
    }
}
