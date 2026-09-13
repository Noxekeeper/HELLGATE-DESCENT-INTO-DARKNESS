using BepInEx.Configuration;

namespace NoREroMod.Systems.LostSounds;

/// <summary>
/// BepInEx settings for LostSounds — scaffold for replacing / adding player (and later other) SFX.
/// Assets under sources/HellGate_sources/LostSounds. Isolated from Systems/Audio (AttackSounds).
/// </summary>
internal static class LostSoundsConfig
{
    private const string Section = "LostSounds";

    public static ConfigEntry<bool> Enable;
    public static ConfigEntry<string> AssetsPath;
    public static ConfigEntry<float> MasterVolume;
    public static ConfigEntry<bool> DebugLogging;

    private static bool _initialized;

    public static bool IsEnabled => Enable != null && Enable.Value;

    public static void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;

        ConfigFile cfg = Plugin.Instance.Config;

        // Default off: folder may be empty until cues are authored.
        Enable = cfg.Bind(Section, "Enable", false,
            "Enable LostSounds module (loads WAVs from HellGate_sources/LostSounds). Off by default until cues are added.");

        AssetsPath = cfg.Bind(Section, "AssetsPath", string.Empty,
            "Folder with LostSounds WAVs (relative to game root). Empty = sources/HellGate_sources/LostSounds.");

        MasterVolume = cfg.Bind(Section, "MasterVolume", 1f,
            "Global volume multiplier for LostSounds cues (0 = mute, 1 = full).");

        DebugLogging = cfg.Bind(Section, "DebugLogging", false,
            "Write detailed LostSounds logs to BepInEx. Keep off for normal play.");
    }

    internal static void LogDebug(string message)
    {
        if (DebugLogging != null && DebugLogging.Value)
            Plugin.Log?.LogInfo(message);
    }
}
