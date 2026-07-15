using NoREroMod.Systems.CombatAi.Factions;

namespace NoREroMod.Systems.Economy;

/// <summary>
/// Shared helpers for Economic ↔ Factions integration. Keeps the JSON-friendly
/// lower-case faction key in one place so <see cref="GoldDropTable"/> and
/// <see cref="GoldHSceneEarningsBridge"/> stay consistent.
/// </summary>
internal static class EconomicFactionUtil
{
    /// <summary>
    /// Convert a runtime <see cref="FactionIds"/> integer to the JSON key used by
    /// our config files (<c>"bandits"</c>, <c>"church"</c>, <c>"mafia"</c>, …).
    /// Unknown ids fall back to <c>"neutral"</c>.
    /// </summary>
    public static string FactionIdToKey(int factionId)
    {
        switch (factionId)
        {
            case FactionIds.Bandits:                 return "bandits";
            case FactionIds.BanditsInquisitionLoyal: return "bandits_inquisition";
            case FactionIds.BanditsMafiaLoyal:       return "bandits_mafia";
            case FactionIds.BanditsDemonsLoyal:      return "bandits_demons";
            case FactionIds.Church:                  return "church";
            case FactionIds.Demons:                  return "demons";
            case FactionIds.Mafia:                   return "mafia";
            case FactionIds.Undead:                  return "undead";
            case FactionIds.Monsters:                return "monsters";
            case FactionIds.Neutral:                 return "neutral";
            default:                                 return "neutral";
        }
    }
}
