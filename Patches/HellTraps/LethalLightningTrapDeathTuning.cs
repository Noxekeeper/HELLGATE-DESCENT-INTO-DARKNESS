namespace NoREroMod.Patches.HellTraps;

/// <summary>
/// Lightning fatal death-clip timing — same profile as lethal cocoon (black screen + bone start).
/// </summary>
internal static class LethalLightningTrapDeathTuning
{
    internal const int FastPhaseFrameCountOneBased =
        LethalCocoonTrapDeathTuning.FastPhaseFrameCountOneBased;

    internal const float FastPhaseSpeedMultiplier =
        LethalCocoonTrapDeathTuning.FastPhaseSpeedMultiplier;

    internal const float TrapContentOffsetY =
        LethalCocoonTrapDeathTuning.TrapContentOffsetY;

    internal const float FastPhaseTrapContentYOffset =
        LethalCocoonTrapDeathTuning.FastPhaseTrapContentYOffset;

    internal const int SlowMoFlashAtFrameOneBased =
        LethalCocoonTrapDeathTuning.SlowMoFlashAtFrameOneBased;

    internal const float SlowMoScale = LethalCocoonTrapDeathTuning.SlowMoScale;

    internal const float SlowMoRealSeconds = LethalCocoonTrapDeathTuning.SlowMoRealSeconds;

    internal const int BlackScreenEndAtFrameOneBased =
        LethalCocoonTrapDeathTuning.BlackScreenEndAtFrameOneBased;

    internal const float DefaultWarningDelaySeconds = 1.2f;

    /// <summary>PNG feet pivot at trap world; negative lowers the whole clip.</summary>
    internal const float ClipOffsetY = -2.3f;

    /// <summary>
    /// Bolt spawn Y above trap world position.
    /// Vanilla uses +3.5 from cast origin; we fire from trap (often below player root),
    /// so this is higher than 3.5. Was 4.0 after switching base player→trap (looked lower).
    /// </summary>
    internal const float StrikeOffsetY = 6.5f;

    internal const float DefaultCooldownSeconds = 3f;
}
