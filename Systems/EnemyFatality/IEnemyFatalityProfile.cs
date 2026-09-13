namespace NoREroMod.Systems.EnemyFatality;

/// <summary>
/// Per-enemy combat fatality definition. Shared patches/playback read this;
/// each enemy registers one profile (config + match predicate + clip root).
/// </summary>
internal interface IEnemyFatalityProfile
{
    /// <summary>Stable id used in logs and audio/sprite cache keys (e.g. WhiteInquisitor).</summary>
    string Id { get; }

    /// <summary>Master + per-enemy Enable + Gore Content.</summary>
    bool IsEnabled { get; }

    /// <summary>Master + per-enemy Enable (preload / patch registration; ignores gore).</summary>
    bool IsModuleEnabled { get; }

    bool Matches(EnemyDate enemy);

    /// <summary>
    /// When true, only magic-projectile hits may arm this profile
    /// (body <c>OndamageSend</c> never arms it). Used by HeavyCritical.
    /// </summary>
    bool RequiresMagicProjectile { get; }

    /// <summary>Default clip folder relative to game root when ClipPath is empty.</summary>
    string DefaultClipRelative { get; }

    /// <summary>
    /// Clip folders to pick from when ClipPath is empty (e.g. lost_leg + lost_head).
    /// Single-entry pool = fixed default. Empty / null falls back to
    /// <see cref="DefaultClipRelative"/>. ClipPath override bypasses the pool.
    /// </summary>
    string[] ClipPoolRelatives { get; }

    /// <summary>Resolved relative clip path (override or default; not the random pick).</summary>
    string ClipPathRelative { get; }

    float HpThresholdPercent { get; }
    float Chance { get; }
    string BoneName { get; }
    /// <summary>World Y added to bone spawn (e.g. StillAlive sits higher).</summary>
    float ClipOffsetY { get; }
    /// <summary>
    /// World X offset along Aradia facing (+ = forward in look direction,
    /// − = backward). Mirrored when she faces left.
    /// </summary>
    float ClipOffsetAlongFacing { get; }
    float FallDistance { get; }
    float FallSpeedMultiplier { get; }
    float FrameSeconds { get; }
    int SortingOrder { get; }
    bool SlowMoEnable { get; }
    float SlowMoTimeScale { get; }
    float SlowMoDurationSeconds { get; }
    int SlowMoStartFrame { get; }
    bool ScarletFlashEnable { get; }
    float SoundVolume { get; }

    /// <summary>
    /// When true, hide the killer enemy visuals for the fatality session
    /// (same window as the PNG clip / player hide).
    /// </summary>
    bool HideKillerDuringClip { get; }

    /// <summary>
    /// Optional mid-clip WAV in the clip folder (empty = none).
    /// Played <see cref="MidClipSfxDelaySeconds"/> after 1-based
    /// <see cref="MidClipSfxAfterFrame"/> first appears.
    /// </summary>
    string MidClipSfxFileName { get; }

    /// <summary>1-based PNG frame that arms the mid-clip SFX (0 = disabled).</summary>
    int MidClipSfxAfterFrame { get; }

    /// <summary>Realtime seconds to wait after the arm frame before playing.</summary>
    float MidClipSfxDelaySeconds { get; }
}
