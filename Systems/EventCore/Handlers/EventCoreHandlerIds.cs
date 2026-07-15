namespace NoREroMod.Systems.EventCore.Handlers;

/// <summary>
/// Declares handler ids referenced by JSON event definitions (routing layer; broker toll is first consumer).
/// </summary>
internal static class EventCoreHandlerIds
{
    internal const string BrokerToll = "broker_toll";
    internal const string FactionSocial = "faction_social";
}
