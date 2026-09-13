using System;
using BepInEx.Configuration;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>
/// Standard cfg-backed fatality profile. Bind once per enemy section, then
/// <see cref="EnemyFatalityRegistry.Register"/>.
/// </summary>
internal sealed class EnemyFatalityBoundProfile : IEnemyFatalityProfile
{
    private readonly Func<EnemyDate, bool> _match;
    private readonly string[] _clipPool;

    public string Id { get; }
    public string DefaultClipRelative { get; }
    public string[] ClipPoolRelatives => _clipPool;
    public bool HideKillerDuringClip { get; private set; }
    public bool RequiresMagicProjectile { get; private set; }

    public ConfigEntry<bool> Enable;
    public ConfigEntry<float> HpThresholdPercentEntry;
    public ConfigEntry<float> ChanceEntry;
    public ConfigEntry<string> ClipPath;
    public ConfigEntry<string> BoneNameEntry;
    public ConfigEntry<float> ClipOffsetYEntry;
    public ConfigEntry<float> ClipOffsetAlongFacingEntry;
    public ConfigEntry<float> FallDistanceEntry;
    public ConfigEntry<float> FallSpeedMultiplierEntry;
    public ConfigEntry<float> FrameSecondsEntry;
    public ConfigEntry<int> SortingOrderEntry;
    public ConfigEntry<bool> SlowMoEnableEntry;
    public ConfigEntry<float> SlowMoTimeScaleEntry;
    public ConfigEntry<float> SlowMoDurationSecondsEntry;
    public ConfigEntry<int> SlowMoStartFrameEntry;
    public ConfigEntry<bool> WhiteFlashEnable;
    public ConfigEntry<float> SoundVolumeEntry;
    public ConfigEntry<string> MidClipSfxFileEntry;
    public ConfigEntry<int> MidClipSfxAfterFrameEntry;
    public ConfigEntry<float> MidClipSfxDelaySecondsEntry;

    private EnemyFatalityBoundProfile(
        string id,
        string defaultClipRelative,
        string[] clipPool,
        Func<EnemyDate, bool> match)
    {
        Id = id;
        DefaultClipRelative = defaultClipRelative;
        _clipPool = clipPool != null && clipPool.Length > 0
            ? clipPool
            : new[] { defaultClipRelative };
        _match = match ?? (_ => false);
    }

    public bool IsEnabled =>
        EnemyFatalityConfig.IsMasterEnabled &&
        Enable != null && Enable.Value;

    public bool IsModuleEnabled =>
        EnemyFatalityConfig.Enable != null && EnemyFatalityConfig.Enable.Value &&
        Enable != null && Enable.Value;

    public bool Matches(EnemyDate enemy) => _match(enemy);

    public bool HasClipPathOverride
    {
        get
        {
            string rel = ClipPath != null ? ClipPath.Value : null;
            return !string.IsNullOrEmpty(rel) && rel.Trim().Length > 0;
        }
    }

    public string ClipPathRelative
    {
        get
        {
            if (HasClipPathOverride)
                return ClipPath.Value.Trim();
            return DefaultClipRelative;
        }
    }

    public float HpThresholdPercent =>
        HpThresholdPercentEntry != null ? HpThresholdPercentEntry.Value : 0.2f;

    public float Chance =>
        ChanceEntry != null ? ChanceEntry.Value : 0.5f;

    public string BoneName
    {
        get
        {
            string name = BoneNameEntry != null ? BoneNameEntry.Value : "body";
            return string.IsNullOrEmpty(name) ? "body" : name;
        }
    }

    public float ClipOffsetY =>
        ClipOffsetYEntry != null ? ClipOffsetYEntry.Value : 0f;

    public float ClipOffsetAlongFacing =>
        ClipOffsetAlongFacingEntry != null ? ClipOffsetAlongFacingEntry.Value : 0f;

    public float FallDistance =>
        FallDistanceEntry != null ? FallDistanceEntry.Value : 0f;

    public float FallSpeedMultiplier =>
        FallSpeedMultiplierEntry != null ? FallSpeedMultiplierEntry.Value : 4.5f;

    public float FrameSeconds =>
        FrameSecondsEntry != null ? FrameSecondsEntry.Value : 0.0625f;

    public int SortingOrder =>
        SortingOrderEntry != null ? SortingOrderEntry.Value : 80;

    public bool SlowMoEnable =>
        SlowMoEnableEntry != null && SlowMoEnableEntry.Value;

    public float SlowMoTimeScale =>
        SlowMoTimeScaleEntry != null ? SlowMoTimeScaleEntry.Value : 0.1f;

    public float SlowMoDurationSeconds =>
        SlowMoDurationSecondsEntry != null ? SlowMoDurationSecondsEntry.Value : 0.5f;

    public int SlowMoStartFrame =>
        SlowMoStartFrameEntry != null ? SlowMoStartFrameEntry.Value : 1;

    public bool ScarletFlashEnable =>
        WhiteFlashEnable == null || WhiteFlashEnable.Value;

    public float SoundVolume =>
        SoundVolumeEntry != null ? SoundVolumeEntry.Value : 1f;

    public string MidClipSfxFileName
    {
        get
        {
            string name = MidClipSfxFileEntry != null ? MidClipSfxFileEntry.Value : null;
            return string.IsNullOrEmpty(name) ? string.Empty : name.Trim();
        }
    }

    public int MidClipSfxAfterFrame =>
        MidClipSfxAfterFrameEntry != null ? MidClipSfxAfterFrameEntry.Value : 0;

    public float MidClipSfxDelaySeconds =>
        MidClipSfxDelaySecondsEntry != null ? MidClipSfxDelaySecondsEntry.Value : 0f;

    /// <summary>
    /// Binds the standard [EnemyFatality.*] keys used by White Inquisitor (and future enemies).
    /// </summary>
    internal static EnemyFatalityBoundProfile Bind(
        ConfigFile cfg,
        string section,
        string id,
        string enableDescription,
        string defaultClipRelative,
        Func<EnemyDate, bool> match,
        float defaultClipOffsetY = 0f,
        bool hideKillerDuringClip = false,
        string defaultMidClipSfxFile = "",
        int defaultMidClipSfxAfterFrame = 0,
        float defaultMidClipSfxDelaySeconds = 0f,
        float defaultHpThresholdPercent = 0.2f,
        float defaultChance = 0.5f,
        string[] defaultClipPool = null,
        bool requiresMagicProjectile = false,
        string defaultBoneName = "body",
        float defaultClipOffsetAlongFacing = 0f)
    {
        string[] pool = NormalizePool(defaultClipRelative, defaultClipPool);
        string primary = !string.IsNullOrEmpty(defaultClipRelative)
            ? defaultClipRelative
            : pool[0];

        var profile = new EnemyFatalityBoundProfile(id, primary, pool, match);
        profile.HideKillerDuringClip = hideKillerDuringClip;
        profile.RequiresMagicProjectile = requiresMagicProjectile;

        profile.Enable = cfg.Bind(section, "Enable", true, enableDescription);

        profile.HpThresholdPercentEntry = cfg.Bind(section, "HpThresholdPercent", defaultHpThresholdPercent,
            "Legacy fallback HP gate when ClipPath points at an unregistered folder (0.2 = below 20%). Registered clips use [EnemyFatality.Clip.*] HpThresholdPercent.");

        profile.ChanceEntry = cfg.Bind(section, "Chance", defaultChance,
            "Fatality trigger chance 0–1 after a clip passes Enable/HP/weight (0.5 = 50%). Also used as legacy HP-gate companion for unregistered ClipPath folders.");

        profile.ClipPath = cfg.Bind(section, "ClipPath", string.Empty,
            "Force one clip folder (relative to game root). Empty = pool pick among enabled clips (lost_leg / lost_head / …) using [EnemyFatality.Clip.*] Enable/Chance/HP.");

        string boneDefault = string.IsNullOrEmpty(defaultBoneName) ? "body" : defaultBoneName;
        profile.BoneNameEntry = cfg.Bind(section, "BoneName", boneDefault,
            "Aradia Spine bone used as the clip spawn point (world position sampled once; clip does not follow the bone).");

        profile.ClipOffsetYEntry = cfg.Bind(section, "ClipOffsetY", defaultClipOffsetY,
            "World Y offset added to the bone spawn position (positive = higher). Default 0 for most clips; StillAlive uses 1.5.");

        profile.ClipOffsetAlongFacingEntry = cfg.Bind(section, "ClipOffsetAlongFacing", defaultClipOffsetAlongFacing,
            "World X offset along Aradia facing (+ = forward in look direction, − = backward). Mirrored when facing left. HeavyCritical uses −1.5.");

        profile.FallDistanceEntry = cfg.Bind(section, "FallDistance", 0f,
            "How far the clip drops downward in world units. 0 = stay at spawn (bone) position.");

        profile.FallSpeedMultiplierEntry = cfg.Bind(section, "FallSpeedMultiplier", 4.5f,
            "Clip fall speed vs animation length. 1 = falls over the whole clip; 4.5 = reaches bottom in ~1/4.5 of the clip.");

        profile.FrameSecondsEntry = cfg.Bind(section, "FrameSeconds", 0.0625f,
            "Seconds per PNG frame (scaled time). Default 0.0625 = 16 FPS. Last frame holds until Take Vengeance / respawn.");

        profile.SortingOrderEntry = cfg.Bind(section, "SortingOrder", 80,
            "Fallback draw order if the player has no MeshRenderer. Normally uses player mesh order + 20.");

        profile.SlowMoEnableEntry = cfg.Bind(section, "SlowMoEnable", false,
            "Slow world time during the fatality clip. Off by default.");

        profile.SlowMoTimeScaleEntry = cfg.Bind(section, "SlowMoTimeScale", 0.1f,
            "Time.timeScale while slowed (0.1 = 90% slowdown). Clamped to 0.05–1.");

        profile.SlowMoDurationSecondsEntry = cfg.Bind(section, "SlowMoDurationSeconds", 0.5f,
            "How long slow-mo lasts in real (unscaled) seconds after SlowMoStartFrame.");

        profile.SlowMoStartFrameEntry = cfg.Bind(section, "SlowMoStartFrame", 1,
            "1-based PNG frame index when slow-mo begins (1 = immediately with the clip). Slow-mo is re-asserted every frame so vanilla death timescale cannot cancel it.");

        profile.WhiteFlashEnable = cfg.Bind(section, "WhiteFlashEnable", true,
            "Play a scarlet UI triple-blink at fatality start (module overlay).");

        profile.SoundVolumeEntry = cfg.Bind(section, "SoundVolume", 1f,
            "Volume for Hit.wav / death sound.wav / Final.wav / mid-clip SFX (0 = mute, 1 = full).");

        profile.MidClipSfxFileEntry = cfg.Bind(section, "MidClipSfxFile",
            defaultMidClipSfxFile ?? string.Empty,
            "Optional WAV file name inside the clip folder for a mid-clip cue (e.g. bone-crack.wav). Empty = none.");

        profile.MidClipSfxAfterFrameEntry = cfg.Bind(section, "MidClipSfxAfterFrame",
            defaultMidClipSfxAfterFrame,
            "1-based PNG frame that arms MidClipSfxFile (0 = disabled). Delay starts when this frame first shows.");

        profile.MidClipSfxDelaySecondsEntry = cfg.Bind(section, "MidClipSfxDelaySeconds",
            defaultMidClipSfxDelaySeconds,
            "Realtime seconds to wait after MidClipSfxAfterFrame before playing the mid-clip WAV.");

        return profile;
    }

    private static string[] NormalizePool(string defaultClipRelative, string[] defaultClipPool)
    {
        if (defaultClipPool != null && defaultClipPool.Length > 0)
        {
            var list = new System.Collections.Generic.List<string>(defaultClipPool.Length);
            for (int i = 0; i < defaultClipPool.Length; i++)
            {
                string rel = defaultClipPool[i];
                if (string.IsNullOrEmpty(rel))
                    continue;
                rel = rel.Trim();
                bool dup = false;
                for (int j = 0; j < list.Count; j++)
                {
                    if (string.Equals(list[j], rel, StringComparison.OrdinalIgnoreCase))
                    {
                        dup = true;
                        break;
                    }
                }

                if (!dup)
                    list.Add(rel);
            }

            if (list.Count > 0)
                return list.ToArray();
        }

        return new[]
        {
            string.IsNullOrEmpty(defaultClipRelative)
                ? EnemyFatalityProfileCatalog.LostLegClipRelative
                : defaultClipRelative
        };
    }
}
