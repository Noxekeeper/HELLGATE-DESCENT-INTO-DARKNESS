using NoREroMod.Systems.Spawn;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.OffspringArchetype;

/// <summary>Maps saved child data to an enemy prefab with Mafiamuscle fallback.</summary>
internal static class OffspringPrefabResolver
{
    internal static string GetArchetypeKey(ChildData child)
    {
        if (child != null && !string.IsNullOrEmpty(child.SpawnArchetype))
            return child.SpawnArchetype;

        return OffspringArchetypeCatalog.FallbackArchetype;
    }

    internal static string BuildObjectName(string archetypeKey)
    {
        if (string.IsNullOrEmpty(archetypeKey))
            archetypeKey = OffspringArchetypeCatalog.FallbackArchetype;

        return "WitchOffspring_" + archetypeKey;
    }

    internal static bool TryResolvePrefab(ChildData child, out GameObject prefab, out string archetypeKey)
    {
        prefab = null;
        archetypeKey = GetArchetypeKey(child);

        EnemyPrefabRegistry.Initialize();
        if (TryGetPrefab(archetypeKey, out prefab))
            return true;

        Plugin.Log?.LogWarning(
            $"[Pregnancy.Archetype] Prefab lookup failed for '{archetypeKey}' (child={child?.Guid}); using {OffspringArchetypeCatalog.FallbackArchetype}");

        archetypeKey = OffspringArchetypeCatalog.FallbackArchetype;
        return TryGetPrefab(archetypeKey, out prefab);
    }

    private static bool TryGetPrefab(string archetypeKey, out GameObject prefab)
    {
        prefab = null;
        if (string.IsNullOrEmpty(archetypeKey))
            return false;

        return EnemyPrefabRegistry.TryGetPrefab(archetypeKey, out prefab) && prefab != null;
    }
}
