using System.Collections.Generic;
using NoREroMod.Systems.Spawn;

namespace NoREroMod.Systems.EventCore.EventTrap;

/// <summary>
/// Discovers <c>EVENTTRAP</c> anchors from <c>HellGateSpawn_*.txt</c> spawn files.
/// </summary>
internal static class EventTrapSpawnDiscovery
{
    internal static List<EventTrapRegistryEntry> Discover(string spawnPointDirectory, EventTrapRegistryFile registry)
    {
        string[] allowed = registry != null && registry.eventFoldersAllowed != null ? registry.eventFoldersAllowed : null;
        string sceneExtra = registry != null ? registry.eventSceneContains : null;

        return HellGateSpawnAnchorDiscovery.Scan(
            spawnPointDirectory,
            "EVENTTRAP",
            "[EventTrap]",
            allowed,
            sceneExtra,
            EventTrapRegistryEntry.FromSpawnBinding);
    }
}
