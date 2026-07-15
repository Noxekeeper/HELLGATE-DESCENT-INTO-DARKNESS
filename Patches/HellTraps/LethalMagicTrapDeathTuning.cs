namespace NoREroMod.Patches.HellTraps;

/// <summary>Hardcoded lethal trap death clip / slow-mo tuning (not cfg-driven).</summary>
internal static class LethalMagicTrapDeathTuning
{
    internal const float SlowMoScale = 0.25f;
    internal const float SlowMoRealSeconds = 0.4f;

    internal const float FrameSeconds = 1f / 15f;
    internal const float DisplayScale = 1f;

    /// <summary>Bone capture Y offset (negative = lower). Tuned for neck/torso bones.</summary>
    internal const float StartOffsetY = -2.5f;

    /// <summary>Offset when start bone is player shadow (sdw), already at feet.</summary>
    internal const float ShadowBoneStartOffsetY = 0f;

    /// <summary>Trap floor landing Y offset.</summary>
    internal const float TrapFloorOffsetY = -1.5f;

    internal const float FallAcceleration = 90f;
    internal const float FallMaxSpeed = 56f;

    /// <summary>Frames 1..5 hold at hit; fall begins on frame 6.</summary>
    internal const int FallStartFrameOneBased = 6;

    /// <summary>1-based frame when meat_fall.wav starts.</summary>
    internal const int MeatFallSoundFrameOneBased = 8;

    internal const bool SuppressPlayerVoiceDuringDeath = true;

    internal const string DeathSoundFileName = "death.wav";
    internal const string MeatFallSoundFileName = "meat_fall.wav";
    internal const float DeathSoundVolume = 1f;
    internal const float MeatFallSoundVolume = 1f;
}
