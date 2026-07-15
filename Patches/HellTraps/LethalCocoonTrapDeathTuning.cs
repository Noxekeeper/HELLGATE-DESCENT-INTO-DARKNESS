namespace NoREroMod.Patches.HellTraps;

/// <summary>WebSpike lethal cocoon death-clip timing (magic trap keeps LethalMagicTrapDeathTuning).</summary>
internal static class LethalCocoonTrapDeathTuning
{
    /// <summary>Frames 1..N play at FastPhaseSpeedMultiplier (no slow-mo during this phase).</summary>
    internal const int FastPhaseFrameCountOneBased = 11;

    internal const float FastPhaseSpeedMultiplier = 3f;

    /// <summary>Trap-anchored PNG Y offset vs spawn point (negative = lower).</summary>
    internal const float TrapContentOffsetY = -0.5f;

    /// <summary>Extra Y for frames 1..11 only (world units; negative = lower).</summary>
    internal const float FastPhaseTrapContentYOffset = -1f;

    /// <summary>White flash + slow-mo when frame 12 begins (gap after frame 11).</summary>
    internal const int SlowMoFlashAtFrameOneBased = 12;

    internal const float SlowMoScale = 0.25f;
    internal const float SlowMoRealSeconds = 0.5f;

    /// <summary>Black screen ends when frame 12 begins (same as slow-mo / flash).</summary>
    internal const int BlackScreenEndAtFrameOneBased = 12;
}
