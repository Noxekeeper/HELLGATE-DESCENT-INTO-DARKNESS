namespace NoREroMod.Patches.HellTraps;

/// <summary>Hardcoded timings for Take Vengeance shock after lethal trap death.</summary>
internal static class LethalTrapVengeanceShockTuning
{
    internal const float RiseToMaxSeconds = 2f;
    internal const float HoldAtMaxSeconds = 3f;
    internal const float DecayToFloorSeconds = 3f;
    internal const float FloorPercent = 0.15f;
    internal const float MaxPercent = 1f;

    internal static float DecayPercentPerSecond =>
        DecayToFloorSeconds > 0.001f
            ? (MaxPercent - FloorPercent) / DecayToFloorSeconds
            : 0f;

    internal const float PeakFlashSeconds = 1.4f;
    internal const float FloorFlashSeconds = 1.4f;

    internal const float PinkGlowMinAlpha = 0.035f;
    internal const float PinkGlowMaxAlpha = 0.11f;
    internal const float PinkGlowPulseHz = 0.38f;

    internal const float PinkColorR = 1f;
    internal const float PinkColorG = 0.64f;
    internal const float PinkColorB = 0.78f;

    /// <summary><c>sources/HellGate_sources/CustomDeath/MindShock.wav</c></summary>
    internal const string ShockSoundFileName = "MindShock.wav";

    /// <summary><c>sources/HellGate_sources/CustomDeath/HeartBeat.wav</c></summary>
    internal const string HeartBeatSoundFileName = "HeartBeat.wav";
}
