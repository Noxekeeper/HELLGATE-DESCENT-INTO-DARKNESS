using System;
using NoREroMod.Systems.CombatAi.Factions;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.ShelterAttack;

internal enum ShelterAttackPhase
{
    Idle,
    Armed,
    Alerting,
    WaveBreak,
    Spawning,
    Combat,
    Victory,
    Defeat
}

internal static class ShelterAttackState
{
    public static ShelterAttackPhase Phase = ShelterAttackPhase.Idle;

    public static int AttackingFaction = FactionIds.Neutral;

    public static double UtcDeadlineSeconds = 0;

    public static int CurrentWave = 0;

    /// <summary>How many enemies of the current wave were already spawned (persisted for resume).</summary>
    public static int SpawnIndexInWave = 0;

    /// <summary>Real-time deadline for the current wave-break pause (<see cref="ShelterAttackPhase.WaveBreak"/>).</summary>
    public static float WaveBreakUntilUnscaled = 0f;

    public static int TotalWaves;

    /// <summary>
    /// Wave-roster tier locked when the attack is armed (Low/Mid/High by hideout child count).
    /// </summary>
    public static ShelterAttackWaves.ThreatTier ThreatTier = ShelterAttackWaves.ThreatTier.Low;

    public static bool ThreatTierLocked;

    public static bool IsEventActive => Phase == ShelterAttackPhase.Armed
                                     || Phase == ShelterAttackPhase.Alerting
                                     || Phase == ShelterAttackPhase.WaveBreak
                                     || Phase == ShelterAttackPhase.Spawning
                                     || Phase == ShelterAttackPhase.Combat;

    public static bool IsResolved => Phase == ShelterAttackPhase.Victory || Phase == ShelterAttackPhase.Defeat;

    public static void Reset()
    {
        Phase = ShelterAttackPhase.Idle;
        AttackingFaction = FactionIds.Neutral;
        UtcDeadlineSeconds = 0;
        CurrentWave = 0;
        SpawnIndexInWave = 0;
        WaveBreakUntilUnscaled = 0f;
        ThreatTier = ShelterAttackWaves.ThreatTier.Low;
        ThreatTierLocked = false;
    }

    public static bool IsAssaultPhase => Phase == ShelterAttackPhase.WaveBreak
                                      || Phase == ShelterAttackPhase.Spawning
                                      || Phase == ShelterAttackPhase.Combat;

    public static float GetWaveBreakRemainingSeconds()
    {
        float remaining = WaveBreakUntilUnscaled - Time.unscaledTime;
        return remaining > 0f ? remaining : 0f;
    }

    public static double GetRemainingSeconds()
    {
        double now = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        double remaining = UtcDeadlineSeconds - now;
        return remaining > 0 ? remaining : 0;
    }
}
