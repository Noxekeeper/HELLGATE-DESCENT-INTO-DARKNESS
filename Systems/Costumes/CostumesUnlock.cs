using System;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Systems.Costumes;

/// <summary>
/// Sets vanilla <c>_CosSkinflag[1]</c> / <c>[2]</c> so CostumeChange shows and applies alt skins.
/// Indices match Trade_menu costume rows (kind=3) and CostumeChange.skinflagcheck.
/// </summary>
internal static class CostumesUnlock
{
    /// <summary>Silver-haired gunner illusory costume.</summary>
    internal const int CosSkinGunner = 1;

    /// <summary>Vendetta / ageless witch illusory costume.</summary>
    internal const int CosSkinVendetta = 2;

    internal static void ApplyIfEnabled()
    {
        if (!CostumesConfig.IsEnabled)
            return;

        try
        {
            ApplyFlags(StaticMng.CosSkinflag);

            game_fragmng frag = UnifiedGameControllerCacheManager.GetGameFragMng();
            if (frag != null && frag._CosSkinflag != null)
                ApplyFlags(frag._CosSkinflag);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Costumes] Unlock apply failed: " + ex.Message);
        }
    }

    internal static void ApplyToFrag(game_fragmng frag)
    {
        if (!CostumesConfig.IsEnabled || frag == null)
            return;

        try
        {
            ApplyFlags(StaticMng.CosSkinflag);
            if (frag._CosSkinflag != null)
                ApplyFlags(frag._CosSkinflag);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Costumes] Unlock apply (frag) failed: " + ex.Message);
        }
    }

    private static void ApplyFlags(bool[] flags)
    {
        if (flags == null || flags.Length <= CosSkinVendetta)
            return;

        flags[CosSkinGunner] = true;
        flags[CosSkinVendetta] = true;
    }
}
