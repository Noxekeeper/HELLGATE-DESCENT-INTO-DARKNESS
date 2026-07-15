using System;

namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// Represents a child born to Aradia via the pregnancy system.
/// Serialized to JSON per save slot.
/// </summary>
[Serializable]
public sealed class ChildData
{
    /// <summary>Unique identifier for this child (GUID).</summary>
    public string Guid;

    /// <summary>Faction of the father (determines buffs and emblem). See FactionIds.</summary>
    public int FactionSource;

    /// <summary>Default scale for a newborn offspring (50% of full MafiaMuscle).</summary>
    public const float InfantBirthScale = 0.50f;

    /// <summary>+13% of base scale per growth stage (3 stages → +39% total).</summary>
    public const float GrowthStepFraction = 0.13f;

    /// <summary>
    /// Growth stage:
    /// 0 = Infant (scale 0.50)
    /// 1 = Child (+13% → 0.565)
    /// 2 = Teen (+26% → 0.63)
    /// 3 = Grown (+39% → 0.695)
    /// </summary>
    public int GrowthStage;

    /// <summary>
    /// Current state:
    /// 0 = InHideout (safe in ParishChurch)
    /// 1 = Kidnapped (missing, buff disabled)
    /// 2 = WorldBoss (grown up, became boss - Phase 3)
    /// 3 = Renegade (hostile - Phase 3)
    /// 4 = Dead
    /// </summary>
    public int State;

    /// <summary>Index 0-8 of assigned node in ParishChurch hideout. -1 if not assigned.</summary>
    public int HideoutNodeIndex;

    /// <summary>Whether the child is alive.</summary>
    public bool IsAlive;

    /// <summary>Real-world timestamp when child was born (for age calculations).</summary>
    public double BirthTimestamp;

    /// <summary>Display name (optional, generated or custom).</summary>
    public string Name;

    /// <summary>EnemyPrefabRegistry key chosen at birth (e.g. TouzokuAxe). Empty = Mafiamuscle fallback.</summary>
    public string SpawnArchetype;

    /// <summary>Saved HP for hideout respawn. -1 = full health.</summary>
    public float CurrentHp = -1f;

    // Cached runtime state (not serialized)
    [NonSerialized] public bool IsSpawned;

    public ChildData()
    {
        Guid = System.Guid.NewGuid().ToString("N");
        FactionSource = 0;
        GrowthStage = 0;
        State = 0;
        HideoutNodeIndex = -1;
        IsAlive = true;
        BirthTimestamp = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        Name = null;
    }

    public bool IsInHideout => IsAlive && State == 0;
    public bool IsKidnapped => State == 1;
    public bool IsWorldBoss => State == 2;
    public bool IsRenegade => State == 3;

    public float GetScaleForGrowthStage()
    {
        int stage = GrowthStage;
        if (stage < 0) stage = 0;
        if (stage > 3) stage = 3;
        return InfantBirthScale * (1f + stage * GrowthStepFraction);
    }

    public void AdvanceGrowthStage()
    {
        if (GrowthStage < 3)
            GrowthStage++;
    }
}

public enum ChildGrowthStage
{
    Infant = 0,
    Child = 1,
    Teen = 2,
    Grown = 3
}

public enum ChildState
{
    InHideout = 0,
    Kidnapped = 1,
    WorldBoss = 2,
    Renegade = 3,
    Dead = 4
}
