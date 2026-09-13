using BepInEx.Configuration;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>Master gate for enemy combat fatalities (per-enemy subsections bind separately).</summary>
internal static class EnemyFatalityConfig
{
    private const string Section = "EnemyFatality";

    public static ConfigEntry<bool> Enable;
    public static ConfigEntry<bool> DebugLogging;
    public static ConfigEntry<bool> TauntEnable;
    public static ConfigEntry<float> TauntDelaySeconds;

    private static bool _initialized;

    public static bool IsMasterEnabled =>
        Enable != null && Enable.Value && Plugin.IsGoreContentEnabled;

    public static void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;

        ConfigFile cfg = Plugin.Instance.Config;

        Enable = cfg.Bind(Section, "Enable", true,
            "Master switch for HellGate enemy combat fatalities. Also requires General.EnableGoreContent. Per-enemy sections (e.g. EnemyFatality.WhiteInquisitor) have their own Enable.");

        DebugLogging = cfg.Bind(Section, "DebugLogging", false,
            "Write detailed EnemyFatality logs to BepInEx. Keep off for normal play.");

        TauntEnable = cfg.Bind(Section, "TauntEnable", true,
            "After a combat fatality clip finishes advancing, show a random killer taunt (HellGateJson/EnemyFatality/<Lang>/phrases.json). Mute list: EnemyFatality/_shared/settings.json.");

        TauntDelaySeconds = cfg.Bind(Section, "TauntDelaySeconds", 2f,
            "Realtime seconds to wait after the fatality clip reaches its last frame before showing the taunt.");
    }

    internal static void LogDebug(string message)
    {
        if (DebugLogging != null && DebugLogging.Value)
            Plugin.Log?.LogInfo(message);
    }
}
