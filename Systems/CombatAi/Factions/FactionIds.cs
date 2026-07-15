using System;

namespace NoREroMod.Systems.CombatAi.Factions;

internal static class FactionIds
{
    public const int Neutral = 0;
    public const int EventCoreEncounter = 50;

    public const int Bandits = 100;
    public const int BanditsInquisitionLoyal = 101;
    public const int BanditsMafiaLoyal = 102;
    public const int BanditsDemonsLoyal = 103;

    public const int Church = 200;
    public const int Demons = 300;
    public const int Mafia = 400;
    public const int Undead = 500;
    public const int Monsters = 600;

    // Aradia's own faction. Used by the Pregnancy module for born offspring (companions
    // friendly to the player). Never a pregnancy source; allegiance only.
    public const int Witch = 700;

    public static bool TryParse(string raw, out int factionId)
    {
        factionId = Neutral;
        if (string.IsNullOrEmpty(raw))
            return false;

        string key = raw.Trim();
        if (int.TryParse(key, out int parsed))
        {
            factionId = parsed;
            return true;
        }

        key = key.Replace("-", "_").Replace(" ", "_");
        if (key.Equals("neutral", StringComparison.OrdinalIgnoreCase))
        {
            factionId = Neutral;
            return true;
        }
        if (key.Equals("eventcore_encounter", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("eventcore", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("encounter", StringComparison.OrdinalIgnoreCase))
        {
            factionId = EventCoreEncounter;
            return true;
        }
        if (key.Equals("bandits", StringComparison.OrdinalIgnoreCase))
        {
            factionId = Bandits;
            return true;
        }
        if (key.Equals("bandits_inquisition", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("bandits_inquisition_loyal", StringComparison.OrdinalIgnoreCase))
        {
            factionId = BanditsInquisitionLoyal;
            return true;
        }
        if (key.Equals("bandits_mafia", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("bandits_mafia_loyal", StringComparison.OrdinalIgnoreCase))
        {
            factionId = BanditsMafiaLoyal;
            return true;
        }
        if (key.Equals("bandits_demons", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("bandits_demons_loyal", StringComparison.OrdinalIgnoreCase))
        {
            factionId = BanditsDemonsLoyal;
            return true;
        }
        if (key.Equals("church", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("inquisition", StringComparison.OrdinalIgnoreCase))
        {
            factionId = Church;
            return true;
        }
        if (key.Equals("demons", StringComparison.OrdinalIgnoreCase))
        {
            factionId = Demons;
            return true;
        }
        if (key.Equals("mafia", StringComparison.OrdinalIgnoreCase))
        {
            factionId = Mafia;
            return true;
        }
        if (key.Equals("undead", StringComparison.OrdinalIgnoreCase))
        {
            factionId = Undead;
            return true;
        }
        if (key.Equals("monsters", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("monster", StringComparison.OrdinalIgnoreCase))
        {
            factionId = Monsters;
            return true;
        }
        if (key.Equals("witch", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("aradia", StringComparison.OrdinalIgnoreCase))
        {
            factionId = Witch;
            return true;
        }

        return false;
    }

    public static bool IsBanditFamily(int factionId)
    {
        return factionId == Bandits ||
               factionId == BanditsInquisitionLoyal ||
               factionId == BanditsMafiaLoyal ||
               factionId == BanditsDemonsLoyal;
    }

    public static bool IsPassiveNonCombat(int factionId)
    {
        return factionId == Neutral || factionId == EventCoreEncounter;
    }

    /// <summary>Aradia's home faction — always allied, not driven by reputation score.</summary>
    public static bool IsPlayerNativeFaction(int factionId) => factionId == Witch;
}
