using NoREroMod.Systems.CombatAi.Factions;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// Runtime state of the current/queued pregnancy. The womb meter queues a conception
/// (<see cref="PendingFaction"/>) when it fills; the conception is applied later, only when
/// the player is safely out of the H-scene (see <see cref="PregnancyConceptionApplier"/>).
/// <see cref="SourceFaction"/> is the faction of the pregnancy currently in gestation and
/// is the seed for trimester modifiers and offspring in later phases.
/// </summary>
internal static class WitchPregnancyState
{
    public static int SourceFaction = FactionIds.Neutral;
    public static int PendingFaction = FactionIds.Neutral;

    /// <summary>Real-time seconds elapsed since conception.</summary>
    public static float GestationElapsedSeconds = 0f;

    public static bool HasPending => PendingFaction != FactionIds.Neutral;
    public static bool IsActive => SourceFaction != FactionIds.Neutral;

    public static float GestationTotalSeconds
    {
        get
        {
            float v = PregnancyConfig.TrimesterTotalSeconds != null ? PregnancyConfig.TrimesterTotalSeconds.Value : 90f;
            return v < 1f ? 90f : v;
        }
    }

    public static float ProgressRatio => IsActive ? Mathf.Clamp01(GestationElapsedSeconds / GestationTotalSeconds) : 0f;

    public static int CurrentTrimester
    {
        get
        {
            if (!IsActive) return 0;
            float r = ProgressRatio;
            float t2 = PregnancyConfig.Trimester2Threshold != null ? PregnancyConfig.Trimester2Threshold.Value : 0.333f;
            float t3 = PregnancyConfig.Trimester3Threshold != null ? PregnancyConfig.Trimester3Threshold.Value : 0.666f;
            if (r < t2) return 1;
            if (r < t3) return 2;
            return 3;
        }
    }

    public static bool IsThirdTrimester => CurrentTrimester == 3;

    public static void QueueConception(int faction)
    {
        if (faction != FactionIds.Neutral)
            PendingFaction = faction;
    }

    public static void ResetGestation()
    {
        GestationElapsedSeconds = 0f;
    }

    public static void ClearAll()
    {
        SourceFaction = FactionIds.Neutral;
        PendingFaction = FactionIds.Neutral;
        GestationElapsedSeconds = 0f;
    }
}
