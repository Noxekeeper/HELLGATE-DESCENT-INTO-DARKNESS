using System;
using NoREroMod.Systems.Gameplay.WeaponAnimations;

namespace NoREroMod.Systems.Gameplay.WeaponAnimations.Profiles;

/// <summary>
/// Weapon family: <c>wp_bigwitch*</c> (Wise Witch's Fine Greatsword). Patches: <see cref="WitchFineGreatswordPatch"/>.
/// </summary>
internal static class WitchGreatswordBigProfile
{
    /// <summary>True when <see cref="PlayerStatus._weaponname"/> or <see cref="PlayerStatus.Exuip"/> matches <c>bigwitch</c>.</summary>
    internal static bool IsMatch(PlayerStatus ps)
    {
        if (ps == null) return false;
        try
        {
            string key = ps._weaponname;
            if (!string.IsNullOrEmpty(key) && key.IndexOf("bigwitch", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        catch { }

        string ex = ps.Exuip;
        return !string.IsNullOrEmpty(ex) && ex.IndexOf("bigwitch", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>Invoked after base <c>wp_witch</c> stats are applied to this weapon.</summary>
    internal static void AfterLightSwordStatsApplied(PlayerStatus status)
    {
        WitchGreatswordComboSequences.ApplyConfiguredSequence(status);
    }
}
