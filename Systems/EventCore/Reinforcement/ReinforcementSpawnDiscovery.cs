using System.Collections.Generic;
using NoREroMod.Systems.Spawn;

namespace NoREroMod.Systems.EventCore.Reinforcement;

/// <summary>
/// Discovers <c>REINFORCEMENT</c> anchors from <c>HellGateSpawn_*.txt</c> spawn files.
/// </summary>
internal static class ReinforcementSpawnDiscovery
{
    internal static List<ReinforcementRegistryEntry> Discover(string spawnPointDirectory, ReinforcementRegistryFile registry)
    {
        string[] allowed = registry != null && registry.eventFoldersAllowed != null ? registry.eventFoldersAllowed : null;
        string sceneExtra = registry != null ? registry.eventSceneContains : null;

        return HellGateSpawnAnchorDiscovery.Scan(
            spawnPointDirectory,
            "REINFORCEMENT",
            "[Reinforcement]",
            allowed,
            sceneExtra,
            ReinforcementRegistryEntry.FromSpawnBinding);
    }
}
