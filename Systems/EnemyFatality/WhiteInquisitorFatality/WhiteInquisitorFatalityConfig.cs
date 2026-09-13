using NoREroMod.Systems.EnemyFatality;

namespace NoREroMod.Systems.EnemyFatality.WhiteInquisitorFatality;

/// <summary>
/// White Inquisitor combat fatality profile.
/// Section: [EnemyFatality.WhiteInquisitor] — plugs into the shared EnemyFatality system.
/// Clip pool: random <c>lost_leg</c> / <c>lost_head</c> (same as shared bisect roster).
/// </summary>
internal static class WhiteInquisitorFatalityConfig
{
    internal const string Section = "EnemyFatality.WhiteInquisitor";
    internal const string ProfileId = "WhiteInquisitor";

    internal const string DefaultClipRelative =
        EnemyFatalityProfileCatalog.DefaultClipRelative;

    private static bool _initialized;

    internal static EnemyFatalityBoundProfile Profile { get; private set; }

    public static void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;

        Profile = EnemyFatalityBoundProfile.Bind(
            Plugin.Instance.Config,
            Section,
            ProfileId,
            "Enable White Inquisitor (InquisitionWhite) fatality (random lost_leg / lost_head) on any damaging attack. Requires [EnemyFatality] Enable and General.EnableGoreContent.",
            DefaultClipRelative,
            enemy => enemy is InquisitionWhite,
            defaultHpThresholdPercent: 0.2f,
            defaultChance: 0.5f,
            defaultClipPool: EnemyFatalityProfileCatalog.SharedBisectClipPool);

        EnemyFatalityRegistry.Register(Profile);
    }
}
