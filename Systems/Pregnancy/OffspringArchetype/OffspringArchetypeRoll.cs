namespace NoREroMod.Systems.Pregnancy.OffspringArchetype;

/// <summary>Assigns a persisted spawn archetype when a child is born.</summary>
internal static class OffspringArchetypeRoll
{
    internal static void AssignToChild(ChildData child)
    {
        if (child == null)
            return;

        if (!PregnancyConfig.IsEnabled)
            return;

        if (PregnancyConfig.OffspringArchetypeEnable != null && !PregnancyConfig.OffspringArchetypeEnable.Value)
            return;

        child.SpawnArchetype = OffspringArchetypeCatalog.RollArchetype(child.FactionSource);

        bool logRoll = PregnancyConfig.OffspringArchetypeLogRolls != null && PregnancyConfig.OffspringArchetypeLogRolls.Value;
        bool debug = PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value;
        if (logRoll || debug)
        {
            Plugin.Log?.LogInfo(
                $"[Pregnancy.Archetype] Child {child.Guid} faction={child.FactionSource} archetype={child.SpawnArchetype}");
        }
    }
}
