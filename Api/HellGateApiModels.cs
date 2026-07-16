namespace NoREroMod.HellGate.Api;

/// <summary>Stable faction identifiers exposed by the HellGate API.</summary>
public enum HellGateFaction
{
    Neutral = 0,
    EventCoreEncounter = 50,
    Bandits = 100,
    BanditsInquisitionLoyal = 101,
    BanditsMafiaLoyal = 102,
    BanditsDemonsLoyal = 103,
    Church = 200,
    Demons = 300,
    Mafia = 400,
    Undead = 500,
    Monsters = 600,
    Witch = 700,
}

/// <summary>Active Rage tier.</summary>
public enum HellGateRageTier
{
    None = 0,
    Tier1 = 1,
    Tier2 = 2,
    Tier3 = 3,
}

/// <summary>Immutable snapshot of the Rage subsystem.</summary>
public sealed class RageStateSnapshot
{
    internal RageStateSnapshot(bool enabled, float percent, bool isActive, HellGateRageTier tier, bool isTier3Ready)
    {
        Enabled = enabled;
        Percent = percent;
        IsActive = isActive;
        Tier = tier;
        IsTier3Ready = isTier3Ready;
    }

    public bool Enabled { get; private set; }
    public float Percent { get; private set; }
    public bool IsActive { get; private set; }
    public HellGateRageTier Tier { get; private set; }
    public bool IsTier3Ready { get; private set; }
}

/// <summary>Immutable snapshot of the MindBroken subsystem.</summary>
public sealed class MindBrokenStateSnapshot
{
    internal MindBrokenStateSnapshot(
        bool enabled,
        float fraction,
        bool isCountdownActive,
        float countdownSecondsRemaining,
        bool isScriptedSequenceActive)
    {
        Enabled = enabled;
        Fraction = fraction;
        IsCountdownActive = isCountdownActive;
        CountdownSecondsRemaining = countdownSecondsRemaining;
        IsScriptedSequenceActive = isScriptedSequenceActive;
    }

    public bool Enabled { get; private set; }
    public float Fraction { get; private set; }
    public bool IsCountdownActive { get; private set; }
    public float CountdownSecondsRemaining { get; private set; }
    public bool IsScriptedSequenceActive { get; private set; }
}

/// <summary>Immutable snapshot of one faction's player reputation.</summary>
public sealed class FactionReputationSnapshot
{
    internal FactionReputationSnapshot(int factionId, float score, string relation)
    {
        FactionId = factionId;
        Score = score;
        Relation = relation;
    }

    public int FactionId { get; private set; }
    public float Score { get; private set; }
    public string Relation { get; private set; }
}

/// <summary>Immutable snapshot of the gold wallet.</summary>
public sealed class GoldStateSnapshot
{
    internal GoldStateSnapshot(bool enabled, long balance, int activeSlot)
    {
        Enabled = enabled;
        Balance = balance;
        ActiveSlot = activeSlot;
    }

    public bool Enabled { get; private set; }
    public long Balance { get; private set; }

    /// <summary>One-based save slot, or zero while no slot is bound.</summary>
    public int ActiveSlot { get; private set; }
}

/// <summary>Immutable snapshot of the current or queued pregnancy.</summary>
public sealed class PregnancyStateSnapshot
{
    internal PregnancyStateSnapshot(
        bool enabled,
        bool isActive,
        bool hasPendingConception,
        int sourceFactionId,
        int pendingFactionId,
        float elapsedSeconds,
        float totalSeconds,
        float progress,
        int trimester)
    {
        Enabled = enabled;
        IsActive = isActive;
        HasPendingConception = hasPendingConception;
        SourceFactionId = sourceFactionId;
        PendingFactionId = pendingFactionId;
        ElapsedSeconds = elapsedSeconds;
        TotalSeconds = totalSeconds;
        Progress = progress;
        Trimester = trimester;
    }

    public bool Enabled { get; private set; }
    public bool IsActive { get; private set; }
    public bool HasPendingConception { get; private set; }
    public int SourceFactionId { get; private set; }
    public int PendingFactionId { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public float TotalSeconds { get; private set; }
    public float Progress { get; private set; }
    public int Trimester { get; private set; }
}
