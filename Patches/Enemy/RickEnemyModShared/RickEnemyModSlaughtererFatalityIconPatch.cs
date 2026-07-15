using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.RickEnemyModShared;

/// <summary>
/// Ensures Rick fatality enemies use the shared logo template at grab time (backup for serialized FatalityIcon field).
/// </summary>
[HarmonyPatch(typeof(Slaughterer), "OnTriggerStay2D")]
internal static class RickEnemyModSlaughtererFatalityIconPatch
{
    [HarmonyPrefix]
    static void EnsureRickFatalityIcon(Slaughterer __instance, ref GameObject ___FatalityIcon)
    {
        if (!RickEnemyModFatalityLogoLoader.UsesRickFatalityLogo(__instance.gameObject))
            return;

        GameObject template = RickEnemyModFatalityLogoLoader.GetLogoTemplate(__instance.gameObject);
        if (template != null)
            ___FatalityIcon = template;
    }
}
