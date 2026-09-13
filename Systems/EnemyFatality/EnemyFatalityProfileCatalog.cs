using System;
using BepInEx.Configuration;
using NoREroMod.Patches.Enemy;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>
/// Registers combat-fatality profiles for all supported enemies (except White,
/// which stays in <c>WhiteInquisitorFatalityConfig</c> as the reference profile).
/// Shared bisect roster randomly picks from <see cref="SharedBisectClipPool"/>
/// (<c>lost_leg</c> / <c>lost_head</c>; add more later). CrawlingCreatures keeps
/// <c>StillAlive</c> only. Empty cfg ClipPath uses the pool; set ClipPath to force one.
/// </summary>
internal static class EnemyFatalityProfileCatalog
{
    internal const string LostLegClipRelative =
        "sources/HellGate_sources/CustomDeath/Fatality/lost_leg";

    internal const string LostHeadClipRelative =
        "sources/HellGate_sources/CustomDeath/Fatality/lost_head";

    /// <summary>Primary label for docs / DefaultClipRelative (pool[0] is lost_leg).</summary>
    internal const string DefaultClipRelative = LostLegClipRelative;

    internal const string StillAliveClipRelative =
        "sources/HellGate_sources/CustomDeath/Fatality/StillAlive";

    internal const string HeavyCriticalClipRelative =
        "sources/HellGate_sources/CustomDeath/Fatality/HeavyCritical";

    /// <summary>
    /// Random pool for White + catalog bisect enemies + goblins.
    /// Append more clip folders here when new fatality art ships.
    /// </summary>
    internal static readonly string[] SharedBisectClipPool =
    {
        LostLegClipRelative,
        LostHeadClipRelative,
    };

    /// <summary>Magic-projectile-only fatality (HeavyCritical roster).</summary>
    internal static readonly string[] HeavyCriticalClipPool =
    {
        HeavyCriticalClipRelative,
    };

    /// <summary>Production defaults: below 20% HP, 50% trigger chance.</summary>
    private const float DefaultHpThresholdPercent = 0.2f;
    private const float DefaultChance = 0.5f;

    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;

        ConfigFile cfg = Plugin.Instance.Config;

        // CrawlingCreatures — StillAlive only (not in shared bisect pool).
        Register(
            cfg,
            "EnemyFatality.CrawlingCreatures",
            "CrawlingCreatures",
            "Enable CrawlingCreatures combat fatality on any damaging attack. Requires [EnemyFatality] Enable and General.EnableGoreContent. Default clip: StillAlive.",
            enemy => enemy is CrawlingCreatures,
            StillAliveClipRelative,
            defaultClipOffsetY: 1.5f,
            hideKillerDuringClip: true,
            defaultMidClipSfxFile: "bone-crack.wav",
            defaultMidClipSfxAfterFrame: 15,
            defaultMidClipSfxDelaySeconds: 0f);

        RegisterSharedBisect(
            cfg,
            "EnemyFatality.Bigoni",
            "Bigoni",
            "Enable Bigoni combat fatality (random lost_leg / lost_head) on any damaging attack. Requires [EnemyFatality] Enable and General.EnableGoreContent. Does not apply to BigoniBrother.",
            enemy => enemy is Bigoni bigoni && !BigoniBrotherIdentity.IsBrother(bigoni));

        RegisterSharedBisect(
            cfg,
            "EnemyFatality.BigoniBrother",
            "BigoniBrother",
            "Enable BigoniBrother combat fatality (random lost_leg / lost_head) on any damaging attack. Requires [EnemyFatality] Enable and General.EnableGoreContent.",
            enemy => enemy is Bigoni bigoni && BigoniBrotherIdentity.IsBrother(bigoni));

        RegisterSharedBisect(
            cfg,
            "EnemyFatality.BlackOoze",
            "BlackOoze",
            "Enable BlackOoze (BlackOoze_Monster) combat fatality (random lost_leg / lost_head) on any damaging attack. Requires [EnemyFatality] Enable and General.EnableGoreContent. Does not apply to BlackOoze traps.",
            enemy => enemy is BlackOoze_Monster);

        RegisterSharedBisect(
            cfg,
            "EnemyFatality.Cocoonman",
            "Cocoonman",
            "Enable Cocoonman combat fatality (random lost_leg / lost_head) on any damaging attack. Requires [EnemyFatality] Enable and General.EnableGoreContent.",
            enemy => enemy is Cocoonman);

        RegisterSharedBisect(
            cfg,
            "EnemyFatality.Gorotuki",
            "Gorotuki",
            "Enable Gorotuki combat fatality (random lost_leg / lost_head) on any damaging attack (includes Demon_gorotuki skin). Requires [EnemyFatality] Enable and General.EnableGoreContent.",
            enemy => enemy is Gorotuki);

        RegisterSharedBisect(
            cfg,
            "EnemyFatality.HighInquisitionFemale",
            "HighInquisitionFemale",
            "Enable High Inquisition Female (HighInquisition_famale) combat fatality (random lost_leg / lost_head) on any damaging attack. Requires [EnemyFatality] Enable and General.EnableGoreContent.",
            enemy => enemy is HighInquisition_famale);

        RegisterSharedBisect(
            cfg,
            "EnemyFatality.Minotaurosu",
            "Minotaurosu",
            "Enable Minotaurosu combat fatality (random lost_leg / lost_head) on any damaging attack. Requires [EnemyFatality] Enable and General.EnableGoreContent.",
            enemy => enemy is Minotaurosu);

        RegisterSharedBisect(
            cfg,
            "EnemyFatality.Slaughterer",
            "Slaughterer",
            "Enable Slaughterer combat fatality (random lost_leg / lost_head) on any damaging attack. Requires [EnemyFatality] Enable and General.EnableGoreContent.",
            enemy => enemy is Slaughterer);

        RegisterSharedBisect(
            cfg,
            "EnemyFatality.SlaveBigAxe",
            "SlaveBigAxe",
            "Enable SlaveBigAxe combat fatality (random lost_leg / lost_head) on any damaging attack. Requires [EnemyFatality] Enable and General.EnableGoreContent.",
            enemy => enemy is SlaveBigAxe
                && !NoREroMod.Patches.Enemy.SlaveBigAxeIllusiveEventGate.ShouldSkipHellGateLogic());

        RegisterSharedBisect(
            cfg,
            "EnemyFatality.TouzokuNormal",
            "TouzokuNormal",
            "Enable TouzokuNormal combat fatality (random lost_leg / lost_head) on any damaging attack. Requires [EnemyFatality] Enable and General.EnableGoreContent.",
            enemy => enemy is TouzokuNormal);

        RegisterSharedBisect(
            cfg,
            "EnemyFatality.Goblin",
            "Goblin",
            "Enable Goblin (goblin) combat fatality (random lost_leg / lost_head) on any damaging attack. Requires [EnemyFatality] Enable and General.EnableGoreContent.",
            enemy => enemy is goblin);

        RegisterSharedBisect(
            cfg,
            "EnemyFatality.GobBigAlter",
            "GobBigAlter",
            "Enable GobBigAlter combat fatality (random lost_leg / lost_head) on any damaging attack. Requires [EnemyFatality] Enable and General.EnableGoreContent.",
            enemy => enemy is GobBigAlter);

        RegisterSharedBisect(
            cfg,
            "EnemyFatality.GobRider",
            "GobRider",
            "Enable GobRider combat fatality (random lost_leg / lost_head) on any damaging attack. Requires [EnemyFatality] Enable and General.EnableGoreContent.",
            enemy => enemy is GobRider);

        // --- HeavyCritical: magic projectile only (not body melee / gun) ---
        RegisterHeavyCritical(
            cfg,
            "EnemyFatality.CrawlingSisterKnight",
            "CrawlingSisterKnight",
            "Enable CrawlingSisterKnight HeavyCritical fatality on LightMagic projectile hits only.",
            enemy => enemy is CrawlingSisterKnight);

        RegisterHeavyCritical(
            cfg,
            "EnemyFatality.InquisitionRED",
            "InquisitionRED",
            "Enable InquisitionRED HeavyCritical fatality on Fireball (SPELL3/4) projectile hits only — not gun Arrow.",
            enemy => enemy is InquisitionRED);

        RegisterHeavyCritical(
            cfg,
            "EnemyFatality.Snailshell",
            "Snailshell",
            "Enable Snailshell / NormalSnailshell HeavyCritical fatality on WaterBall (ATK2) projectile hits only — not SlashDamage.",
            enemy => enemy is Snailshell);

        RegisterHeavyCritical(
            cfg,
            "EnemyFatality.Pilgrim",
            "Pilgrim",
            "Enable Pilgrim HeavyCritical fatality on HomingMissileConst (MAGIC) projectile hits only.",
            enemy => enemy is Pilgrim);

        RegisterHeavyCritical(
            cfg,
            "EnemyFatality.Sisterknight",
            "Sisterknight",
            "Enable Sisterknight HeavyCritical fatality on LightMagic projectile hits only.",
            enemy => enemy is Sisterknight);

        RegisterHeavyCritical(
            cfg,
            "EnemyFatality.SkeltonOoze",
            "SkeltonOoze",
            "Enable SkeltonOoze HeavyCritical fatality on WaterBall / BoundMoveMagic / TargetRotationEnemy projectile hits only.",
            enemy => enemy is SkeltonOoze);

        // Tyoukyoushi / TyoukyoushiRed excluded — HomingMissileConst path unreliable for fatality arming.
    }

    private static void RegisterHeavyCritical(
        ConfigFile cfg,
        string section,
        string id,
        string enableDescription,
        Func<EnemyDate, bool> match)
    {
        Register(
            cfg,
            section,
            id,
            enableDescription,
            match,
            HeavyCriticalClipRelative,
            defaultHpThresholdPercent: DefaultHpThresholdPercent,
            defaultChance: DefaultChance,
            defaultClipPool: HeavyCriticalClipPool,
            requiresMagicProjectile: true,
            defaultBoneName: "body",
            defaultClipOffsetAlongFacing: -1.5f);
    }

    private static void RegisterSharedBisect(
        ConfigFile cfg,
        string section,
        string id,
        string enableDescription,
        Func<EnemyDate, bool> match)
    {
        Register(
            cfg,
            section,
            id,
            enableDescription,
            match,
            DefaultClipRelative,
            defaultHpThresholdPercent: DefaultHpThresholdPercent,
            defaultChance: DefaultChance,
            defaultClipPool: SharedBisectClipPool);
    }

    private static void Register(
        ConfigFile cfg,
        string section,
        string id,
        string enableDescription,
        Func<EnemyDate, bool> match,
        string defaultClipRelative = null,
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
        EnemyFatalityBoundProfile profile = EnemyFatalityBoundProfile.Bind(
            cfg,
            section,
            id,
            enableDescription,
            string.IsNullOrEmpty(defaultClipRelative) ? DefaultClipRelative : defaultClipRelative,
            match,
            defaultClipOffsetY,
            hideKillerDuringClip,
            defaultMidClipSfxFile,
            defaultMidClipSfxAfterFrame,
            defaultMidClipSfxDelaySeconds,
            defaultHpThresholdPercent,
            defaultChance,
            defaultClipPool,
            requiresMagicProjectile,
            defaultBoneName,
            defaultClipOffsetAlongFacing);

        EnemyFatalityRegistry.Register(profile);
    }
}
