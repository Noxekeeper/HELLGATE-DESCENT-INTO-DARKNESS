using System.Collections.Generic;
using NoREroMod.Systems.Gameplay.WeaponAnimations.Profiles;
using UnityEngine;

namespace NoREroMod.Systems.Gameplay.WeaponAnimations;

/// <summary>
/// Builds extended ground-attack motion lists by appending pairs of strikes (see config <see cref="Plugin.witchGreatswordDuplicateLastTwoRounds"/>).
/// </summary>
internal static class WitchGreatswordComboSequences
{
    /// <summary>
    /// Duplicates the last two ground strikes for <paramref name="rounds"/> iterations, or auto-calculates rounds to reach 9 <see cref="PlayerStatus._AtkMotion"/> rows when config is 0.
    /// </summary>
    internal static void ApplyConfiguredSequence(PlayerStatus status)
    {
        if (status == null) return;

        int dupRounds = Mathf.Clamp(Plugin.witchGreatswordDuplicateLastTwoRounds?.Value ?? 0, 0, 16);

        if (dupRounds == 0 && status._AtkMotion != null
            && (WitchGreatswordBigProfile.IsMatch(status) || LightSwordExtendedComboProfile.IsVanillaThreeHitLightSword(status)))
        {
            int n = status._AtkMotion.Count;
            if (n > 0 && n < 9)
            {
                int need = 9 - n;
                dupRounds = (need + 1) / 2;
                if (dupRounds < 1) dupRounds = 1;
                if (dupRounds > 16) dupRounds = 16;
            }
        }

        if (dupRounds == 0) return;

        DuplicateLastTwoGroundStrikes(status, dupRounds);
    }

    private static void DuplicateLastTwoGroundStrikes(PlayerStatus status, int rounds)
    {
        if (rounds <= 0 || status._AtkMotion == null || status._AtkMotion.Count < 3) return;

        for (int r = 0; r < rounds; r++)
        {
            var motion = new List<string>(status._AtkMotion);
            int n = motion.Count;
            if (status._SmashKind == null || status._SmashKind.Count != n)
            {
                Plugin.Log?.LogWarning("[LightSwordExtendedCombo] DuplicateLastTwo: AtkMotion / SmashKind length mismatch; skip.");
                return;
            }

            int iA = n - 2;
            int iB = n - 1;
            if (r == 0 && LightSwordExtendedComboProfile.IsWitchLightSwordLine(status)
                && (status.weaponcount == 2 || status.weaponcount == 3) && n > 3)
            {
                iA = 1;
                iB = 2;
            }

            motion.Add(motion[iA]);
            motion.Add(motion[iB]);
            status._AtkMotion = motion;

            var smash = new List<int>(status._SmashKind);
            smash.Add(smash[iA]);
            smash.Add(smash[iB]);
            status._SmashKind = smash;

            status.weaponcount = motion.Count;
        }
    }
}
