using BepInEx.Configuration;

namespace NoREroMod.Systems.DeadArmor;

/// <summary>
/// BepInEx settings for DeadArmor: NikuArmor break clips and SlaveBigAxe armored grab-throw.
/// Isolated from HellTraps / other death systems.
/// </summary>
internal static class DeadArmorConfig
{
    private const string Section = "DeadArmor";

    public static ConfigEntry<bool> Enable;
    public static ConfigEntry<string> PhysicalClipPath;
    public static ConfigEntry<string> MagicClipPath;
    public static ConfigEntry<string> DeathSoundsPath;
    public static ConfigEntry<string> BoneName;
    public static ConfigEntry<string> FallDistances;
    public static ConfigEntry<float> FallSpeedMultiplier;
    public static ConfigEntry<float> FrameSeconds;
    public static ConfigEntry<float> HoldLastFrameSeconds;
    public static ConfigEntry<float> DisplayScale;
    public static ConfigEntry<float> SoundVolume;
    public static ConfigEntry<int> SortingOrder;
    public static ConfigEntry<bool> DebugLogging;

    public static ConfigEntry<bool> ArmoredGrabThrowEnable;
    public static ConfigEntry<float> ArmoredGrabThrowAwayVelocity;
    public static ConfigEntry<float> ArmoredGrabThrowUpVelocity;
    public static ConfigEntry<float> ArmoredGrabThrowHoldSeconds;
    public static ConfigEntry<float> ArmoredGrabThrowHoldPull;
    public static ConfigEntry<float> ArmoredGrabThrowHoldLift;
    public static ConfigEntry<bool> ArmoredGrabThrowSlowmo;
    public static ConfigEntry<float> ArmoredGrabThrowSlowmoTimeScale;
    public static ConfigEntry<float> ArmoredGrabThrowSlowmoDuration;

    private static bool _initialized;

    public static bool IsEnabled =>
        Enable != null && Enable.Value && Plugin.IsGoreContentEnabled;

    /// <summary>Module toggled on in cfg — used for asset preload even if splash Gore is later unchecked.</summary>
    public static bool IsModuleEnabled => Enable != null && Enable.Value;

    public static void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;

        ConfigFile cfg = Plugin.Instance.Config;

        Enable = cfg.Bind(Section, "Enable", true,
            "Enable DeadArmor. When girl armor (NikuArmor) breaks on SlaveBigAxe / OtherSlavebigAxe, play a PNG death clip + sound.");

        PhysicalClipPath = cfg.Bind(Section, "PhysicalClipPath", string.Empty,
            "Folder with physical/stab break frames named w1.png, w2.png, … Empty = sources/HellGate_sources/DeadArmor/PhisicDamage.");

        MagicClipPath = cfg.Bind(Section, "MagicClipPath", string.Empty,
            "Folder with magic break frames named w1.png, w2.png, … Empty = sources/HellGate_sources/DeadArmor/MagicDamage.");

        DeathSoundsPath = cfg.Bind(Section, "DeathSoundsPath", string.Empty,
            "Folder with .wav files; one is picked at random on armor break. Empty = sources/HellGate_sources/DeadArmor/DeathSounds.");

        BoneName = cfg.Bind(Section, "BoneName", "bone2",
            "Spine bone used as the clip spawn point on the enemy.");

        FallDistances = cfg.Bind(Section, "FallDistances", "0,0.1",
            "How far the clip drops (world units). Comma-separated list = random pick each time. One number = always that distance.");

        FallSpeedMultiplier = cfg.Bind(Section, "FallSpeedMultiplier", 4.5f,
            "Clip fall speed vs animation length. 1 = falls over the whole clip; 4.5 = reaches the bottom in ~1/4.5 of the clip.");

        FrameSeconds = cfg.Bind(Section, "FrameSeconds", 0.087f,
            "Seconds per PNG frame. 0.087 ≈ 11.5 FPS. Lower = faster playback.");

        HoldLastFrameSeconds = cfg.Bind(Section, "HoldLastFrameSeconds", 10f,
            "After the last frame, keep it on screen this many seconds, then remove the clip.");

        DisplayScale = cfg.Bind(Section, "DisplayScale", 1f,
            "Clip size (world scale). Increase if the overlay looks too small; decrease if too large.");

        SoundVolume = cfg.Bind(Section, "SoundVolume", 1f,
            "Armor-break sound volume (0 = mute, 1 = full).");

        SortingOrder = cfg.Bind(Section, "SortingOrder", 80,
            "Fallback draw order if the enemy has no MeshRenderer. Normally uses enemy mesh order + 20.");

        DebugLogging = cfg.Bind(Section, "DebugLogging", false,
            "Write detailed DeadArmor logs to BepInEx (spawn, load, throw). Keep off for normal play.");

        ArmoredGrabThrowEnable = cfg.Bind(Section, "ArmoredGrabThrowEnable", true,
            "SlaveBigAxe only, while NikuArmor is on: replace grab-via-attack (H snap) with a short hold then knockback. After armor breaks, normal grab returns.");

        ArmoredGrabThrowAwayVelocity = cfg.Bind(Section, "ArmoredGrabThrowAwayVelocity", 24f,
            "Horizontal throw strength (vanilla nockbackspeed). Higher = farther slide. Vanilla knockout is around 16.");

        ArmoredGrabThrowUpVelocity = cfg.Bind(Section, "ArmoredGrabThrowUpVelocity", 0f,
            "Upward throw impulse. 0 = flat horizontal throw (gravity briefly disabled during the slide).");

        ArmoredGrabThrowHoldSeconds = cfg.Bind(Section, "ArmoredGrabThrowHoldSeconds", 0.7f,
            "How long the failed-grab hold lasts before the throw (real seconds).");

        ArmoredGrabThrowHoldPull = cfg.Bind(Section, "ArmoredGrabThrowHoldPull", 0.85f,
            "During hold, pull the player this far beside the slave (world units). 0 = freeze without pulling.");

        ArmoredGrabThrowHoldLift = cfg.Bind(Section, "ArmoredGrabThrowHoldLift", 0.5f,
            "During hold, raise the player this many world units.");

        ArmoredGrabThrowSlowmo = cfg.Bind(Section, "ArmoredGrabThrowSlowmo", true,
            "Slow time during the armored throw. No camera zoom. Independent of GrabViaAttack / StartZoom slow-mo.");

        ArmoredGrabThrowSlowmoTimeScale = cfg.Bind(Section, "ArmoredGrabThrowSlowmoTimeScale", 0.7f,
            "Time scale during armored-throw slow-mo. 1 = normal speed, 0.7 = 70% speed.");

        ArmoredGrabThrowSlowmoDuration = cfg.Bind(Section, "ArmoredGrabThrowSlowmoDuration", 0.7f,
            "How long armored-throw slow-mo lasts (real seconds).");
    }

    /// <summary>Picks a fall distance from <see cref="FallDistances"/> (random if several values).</summary>
    public static float PickFallDistance()
    {
        const float fallback = 0.6f;
        string raw = FallDistances != null ? FallDistances.Value : null;
        if (string.IsNullOrEmpty(raw) || raw.Trim().Length == 0)
            return fallback;

        string[] parts = raw.Split(new[] { ',', ';', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        float chosen = fallback;
        int valid = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            if (!float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float v) || v < 0f)
                continue;
            valid++;
            if (UnityEngine.Random.Range(0, valid) == 0)
                chosen = v;
        }

        return valid > 0 ? chosen : fallback;
    }

    internal static void LogDebug(string message)
    {
        if (DebugLogging != null && DebugLogging.Value)
            Plugin.Log?.LogInfo(message);
    }
}
