namespace NoREroMod.Patches.HellTraps;

/// <summary>Optional per-trap death PNG playback timing (null = magic trap defaults).</summary>
internal sealed class LethalDeathClipPlaybackProfile
{
    internal int FastPhaseFrameCountOneBased { get; private set; }
    internal float FastPhaseSpeedMultiplier { get; private set; }
    internal int SlowMoFlashAtFrameOneBased { get; private set; }
    internal bool DeferSlowMoUntilClipTransition { get; private set; }
    internal bool DeferFlashUntilClipTransition { get; private set; }
    internal float SlowMoScale { get; private set; }
    internal float SlowMoRealSeconds { get; private set; }

    /// <summary>Empty placeholder on sdw bone; PNG sequence renders at trap floor.</summary>
    internal bool UseBoneEmptyFrameWithTrapContent { get; private set; }

    internal float TrapContentOffsetY { get; private set; }

    internal float FastPhaseTrapContentYOffset { get; private set; }

    /// <summary>H-scene black screen during fast phase only (frames 1..FastPhaseFrameCountOneBased).</summary>
    internal bool UseBlackBackdropDuringClip { get; private set; }

    internal static readonly LethalDeathClipPlaybackProfile CocoonWebSpike =
        new LethalDeathClipPlaybackProfile
        {
            FastPhaseFrameCountOneBased = LethalCocoonTrapDeathTuning.FastPhaseFrameCountOneBased,
            FastPhaseSpeedMultiplier = LethalCocoonTrapDeathTuning.FastPhaseSpeedMultiplier,
            SlowMoFlashAtFrameOneBased = LethalCocoonTrapDeathTuning.SlowMoFlashAtFrameOneBased,
            DeferSlowMoUntilClipTransition = true,
            DeferFlashUntilClipTransition = true,
            SlowMoScale = LethalCocoonTrapDeathTuning.SlowMoScale,
            SlowMoRealSeconds = LethalCocoonTrapDeathTuning.SlowMoRealSeconds,
            UseBoneEmptyFrameWithTrapContent = true,
            TrapContentOffsetY = LethalCocoonTrapDeathTuning.TrapContentOffsetY,
            FastPhaseTrapContentYOffset = LethalCocoonTrapDeathTuning.FastPhaseTrapContentYOffset,
            UseBlackBackdropDuringClip = true,
        };
}
