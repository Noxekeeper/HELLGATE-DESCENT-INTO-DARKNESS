using NoREroMod.Systems.EventCore.Host;
using NoREroMod.Systems.Spawn;

namespace NoREroMod.Systems.CombatAi.Factions;

/// <summary>
/// EventCore strangers and broker ambush extras keep faction AI/rep but hide bone emblems.
/// </summary>
internal static class FactionMarkerVisibility
{
    internal static bool ShouldSuppress(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null)
            return false;

        if (enemy.GetComponent<EventCoreHost>() != null)
            return true;

        SpawnManagedInstance managed = enemy.GetComponent<SpawnManagedInstance>();
        return managed != null && managed.SuppressFactionMarker;
    }
}
