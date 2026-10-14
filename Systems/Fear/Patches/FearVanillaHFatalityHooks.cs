using System;
using HarmonyLib;
using NoREroMod.Patches.HellTraps;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Fear.Patches;

/// <summary>
/// Vanilla H-scene fatalities (SlaughterFatality, HiInquisition_famale_Ftality, CandoreFatality, …)
/// kill via <see cref="playercon.SpDeath"/>. Arm the same Take Vengeance trauma latch as
/// lethal traps / combat EnemyFatality so MB shock + Fear −100→floor run after respawn.
/// </summary>
internal static class FearVanillaHFatalityHooks
{
    /// <summary>
    /// Fatality H drivers that call SpDeath but contain neither "Fatality" nor "Ftality" in the type name.
    /// </summary>
    private static readonly string[] ExtraFatalityTypeNames =
    {
        "ERODemonRequiemKnight",
    };

    [HarmonyPatch(typeof(playercon), "SpDeath")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void SpDeath_Prefix()
    {
        if (!FearConfig.IsEnabled)
            return;

        // Combat EnemyFatality already calls MarkFatalityDeath and does not use SpDeath.
        if (!IsVanillaFatalityHActive())
            return;

        LethalTrapVengeanceShockSession.MarkFatalityDeath();
        Plugin.Log?.LogInfo("[Fear] Vanilla H-fatality SpDeath — armed vengeance trauma latch.");
    }

    internal static bool IsVanillaFatalityHActive()
    {
        try
        {
            MonoBehaviour[] monos = Object.FindObjectsOfType<MonoBehaviour>();
            if (monos == null || monos.Length == 0)
                return false;

            for (int i = 0; i < monos.Length; i++)
            {
                MonoBehaviour mb = monos[i];
                if (mb == null || !mb.isActiveAndEnabled)
                    continue;

                string name = mb.GetType().Name;
                if (string.IsNullOrEmpty(name))
                    continue;

                if (name.IndexOf("Fatality", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                // Vanilla High Inquisition female fatality script (typo: Ftality).
                if (name.IndexOf("Ftality", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                for (int e = 0; e < ExtraFatalityTypeNames.Length; e++)
                {
                    if (string.Equals(name, ExtraFatalityTypeNames[e], StringComparison.Ordinal))
                        return true;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Fear] Vanilla H-fatality detect failed: " + ex.Message);
        }

        return false;
    }
}
