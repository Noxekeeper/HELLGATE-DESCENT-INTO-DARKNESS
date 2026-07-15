using UnityEngine;

namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// Universal trimester debuffs. Applies to every pregnancy regardless of source faction.
/// The penalty scales with the current trimester level: each level adds a flat stat drain
/// and the movement speed penalty is active from II trimester onward.
/// </summary>
internal static class TrimesterDebuffs
{
    public static bool IsActive =>
        PregnancyConfig.IsEnabled &&
        WitchPregnancyState.IsActive &&
        WitchPregnancyState.CurrentTrimester >= 1;

    public static int CurrentTrimester => WitchPregnancyState.IsActive ? WitchPregnancyState.CurrentTrimester : 0;

    private static int PenaltyPerLevel => Mathf.Max(0, PregnancyConfig.TrimesterStatPenaltyPerLevel?.Value ?? 3);

    public static int StrPenalty => -CurrentTrimester * PenaltyPerLevel;
    public static int IntPenalty => -CurrentTrimester * PenaltyPerLevel;
    public static int DexPenalty => -CurrentTrimester * PenaltyPerLevel;
    public static int LuckPenalty => -CurrentTrimester * PenaltyPerLevel;

    public static float MoveSpeedMultiplier
    {
        get
        {
            if (!IsActive || CurrentTrimester < 2)
                return 1f;

            float penalty = PregnancyConfig.TrimesterMoveSpeedPenalty?.Value ?? 0.30f;
            return Mathf.Clamp(1f - penalty, 0.01f, 1f);
        }
    }
}
