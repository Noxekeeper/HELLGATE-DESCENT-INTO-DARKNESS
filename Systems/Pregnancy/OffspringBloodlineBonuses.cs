using System.Collections.Generic;
using NoREroMod.Systems.CombatAi.Factions;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// Permanent additive bonuses from living children currently stored in the ParishChurch hideout.
/// A child must be in the hideout (IsInHideout) to contribute. Caps prevent infinite stacking.
/// </summary>
internal static class OffspringBloodlineBonuses
{
    public static int StrBonus => GetCappedInt(GetRawStrBonus(), PregnancyConfig.MaxBloodlineStrBonus?.Value ?? 20);
    public static int IntBonus => GetCappedInt(GetRawIntBonus(), PregnancyConfig.MaxBloodlineIntBonus?.Value ?? 20);
    public static int DexBonus => GetCappedInt(GetRawDexBonus(), PregnancyConfig.MaxBloodlineDexBonus?.Value ?? 20);
    public static int StaBonus => GetCappedInt(GetRawStaBonus(), PregnancyConfig.MaxBloodlineStaBonus?.Value ?? 20);
    public static int LuckBonus => GetCappedInt(GetRawLuckBonus(), PregnancyConfig.MaxBloodlineLuckBonus?.Value ?? 20);
    public static float RagePerSecond => GetCapped(GetRawRagePerSecond(), PregnancyConfig.MaxBloodlineRagePerSecond?.Value ?? 1.0f);

    private static int GetRawStrBonus()
    {
        if (!PregnancyConfig.IsEnabled) return 0;
        int sum = 0;
        foreach (var c in GetChildren())
        {
            sum += PregnancyConfig.NormalizeSourceFaction(c.FactionSource) switch
            {
                FactionIds.Demons => PregnancyConfig.DemonsStrBonusPerChild?.Value ?? 0,
                FactionIds.Monsters => PregnancyConfig.MonstersStrBonusPerChild?.Value ?? 0,
                FactionIds.Undead => PregnancyConfig.UndeadStrBonusPerChild?.Value ?? 0,
                _ => 0
            };
        }
        return sum;
    }

    private static int GetRawIntBonus()
    {
        if (!PregnancyConfig.IsEnabled) return 0;
        int sum = 0;
        foreach (var c in GetChildren())
        {
            if (PregnancyConfig.NormalizeSourceFaction(c.FactionSource) == FactionIds.Demons)
                sum += PregnancyConfig.DemonsIntBonusPerChild?.Value ?? 0;
        }
        return sum;
    }

    private static int GetRawDexBonus()
    {
        if (!PregnancyConfig.IsEnabled) return 0;
        int sum = 0;
        foreach (var c in GetChildren())
        {
            sum += PregnancyConfig.NormalizeSourceFaction(c.FactionSource) switch
            {
                FactionIds.Bandits => PregnancyConfig.BanditsDexBonusPerChild?.Value ?? 0,
                FactionIds.Mafia => PregnancyConfig.MafiaDexBonusPerChild?.Value ?? 0,
                _ => 0
            };
        }
        return sum;
    }

    private static int GetRawStaBonus()
    {
        if (!PregnancyConfig.IsEnabled) return 0;
        int sum = 0;
        foreach (var c in GetChildren())
        {
            sum += PregnancyConfig.NormalizeSourceFaction(c.FactionSource) switch
            {
                FactionIds.Church => PregnancyConfig.ChurchStaBonusPerChild?.Value ?? 0,
                FactionIds.Monsters => PregnancyConfig.MonstersStaBonusPerChild?.Value ?? 0,
                FactionIds.Undead => PregnancyConfig.UndeadStaBonusPerChild?.Value ?? 0,
                _ => 0
            };
        }
        return sum;
    }

    private static int GetRawLuckBonus()
    {
        if (!PregnancyConfig.IsEnabled) return 0;
        int sum = 0;
        foreach (var c in GetChildren())
        {
            sum += PregnancyConfig.NormalizeSourceFaction(c.FactionSource) switch
            {
                FactionIds.Church => PregnancyConfig.ChurchLuckBonusPerChild?.Value ?? 0,
                FactionIds.Undead => PregnancyConfig.UndeadLuckBonusPerChild?.Value ?? 0,
                FactionIds.Bandits => PregnancyConfig.BanditsLuckBonusPerChild?.Value ?? 0,
                FactionIds.Mafia => PregnancyConfig.MafiaLuckBonusPerChild?.Value ?? 0,
                _ => 0
            };
        }
        return sum;
    }

    private static float GetRawRagePerSecond()
    {
        if (!PregnancyConfig.IsEnabled) return 0f;
        float sum = 0f;
        foreach (var c in GetChildren())
        {
            sum += PregnancyConfig.NormalizeSourceFaction(c.FactionSource) switch
            {
                FactionIds.Demons => PregnancyConfig.DemonsRagePerSecondPerChild?.Value ?? 0f,
                FactionIds.Church => PregnancyConfig.ChurchRagePerSecondPerChild?.Value ?? 0f,
                FactionIds.Monsters => PregnancyConfig.MonstersRagePerSecondPerChild?.Value ?? 0f,
                FactionIds.Undead => PregnancyConfig.UndeadRagePerSecondPerChild?.Value ?? 0f,
                FactionIds.Bandits => PregnancyConfig.BanditsRagePerSecondPerChild?.Value ?? 0f,
                FactionIds.Mafia => PregnancyConfig.MafiaRagePerSecondPerChild?.Value ?? 0f,
                _ => 0f
            };
        }
        return sum;
    }

    private static List<ChildData> GetChildren()
    {
        try
        {
            return PregnancySlotStore.GetAliveChildrenInHideout();
        }
        catch
        {
            return new List<ChildData>();
        }
    }

    private static int GetCappedInt(int raw, int cap) => Mathf.Clamp(raw, 0, Mathf.Max(0, cap));
    private static float GetCapped(float raw, float cap) => Mathf.Clamp(raw, 0f, Mathf.Max(0f, cap));
}
