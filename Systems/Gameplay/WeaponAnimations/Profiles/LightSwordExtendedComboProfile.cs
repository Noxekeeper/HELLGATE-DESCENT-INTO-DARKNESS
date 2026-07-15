using System;

namespace NoREroMod.Systems.Gameplay.WeaponAnimations.Profiles;

/// <summary>
/// Selection rules for extended ground combo support in <see cref="WitchExtendedGroundSwordComboPatch"/> (WeaponKind 1, <c>wp_witch*</c> / <c>wp_bigwitch*</c> / generic 3-hit rows).
/// </summary>
internal static class LightSwordExtendedComboProfile
{
    /// <summary>Returns true if the equipped weapon uses extended ground combo data.</summary>
    internal static bool IsMatch(PlayerStatus ps)
    {
        if (ps == null || ps.WeaponKind != 1) return false;
        if (WitchGreatswordBigProfile.IsMatch(ps)) return true;
        if (IsVanillaThreeHitLightSword(ps)) return true;
        return IsDupedNonBigwitchLightSword(ps);
    }

    /// <summary>Weapon key contains <c>wp_witch</c> but not <c>bigwitch</c> (GUID keys included).</summary>
    internal static bool IsWitchLightSwordLine(PlayerStatus ps)
    {
        if (ps == null) return false;
        string k = ps._weaponname;
        if (string.IsNullOrEmpty(k)) return false;
        if (k.IndexOf("bigwitch", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        return k.IndexOf("wp_witch", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Eligible before sequence extension: aligned motion/smash lists, at least 3 rows; <see cref="PlayerStatus.weaponcount"/> 2 or 3 on <c>wp_witch*</c>, else exactly 3 rows and weaponcount 2.
    /// </summary>
    internal static bool IsVanillaThreeHitLightSword(PlayerStatus ps)
    {
        if (ps == null || ps.WeaponKind != 1) return false;
        if (WitchGreatswordBigProfile.IsMatch(ps)) return false;
        if (ps._AtkMotion == null || ps._SmashKind == null) return false;
        if (ps._AtkMotion.Count != ps._SmashKind.Count) return false;
        if (ps._AtkMotion.Count < 3) return false;
        if (IsWitchLightSwordLine(ps))
            return ps.weaponcount == 2 || ps.weaponcount == 3;
        if (ps.weaponcount != 2) return false;
        return ps._AtkMotion.Count == 3;
    }

    /// <summary>True after <see cref="WitchGreatswordComboSequences"/> produced 9+ aligned rows (non-bigwitch).</summary>
    private static bool IsDupedNonBigwitchLightSword(PlayerStatus ps)
    {
        if (WitchGreatswordBigProfile.IsMatch(ps)) return false;
        if (ps._AtkMotion == null || ps._SmashKind == null) return false;
        if (ps._AtkMotion.Count != ps._SmashKind.Count) return false;
        return ps._AtkMotion.Count >= 9;
    }
}
