using BepInEx.Configuration;
using NoREroMod.Systems.CombatAi.Factions;

namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// Configuration for the extended Pregnancy module. Lives in the standard BepInEx config
/// file (<c>BepInEx/config/NoREroMod_HellGate.cfg</c>) under the <c>Pregnancy*</c> sections,
/// per the design decision to drive the module from <c>.cfg</c> rather than an in-game menu.
///
/// Milestone 1 wires only the foundation knobs (womb capacity, meter visibility, per-faction
/// blocking). Trimester timings, modifiers and late-game toggles are reserved for later phases.
/// </summary>
internal static class PregnancyConfig
{
    private const string SectionGeneral = "Pregnancy";
    private const string SectionBlock = "Pregnancy.Blocking";
    private const string SectionTrimester = "Pregnancy.Trimester";
    private const string SectionPhysics = "Pregnancy.Physics";
    private const string SectionTrimesterModifiers = "Pregnancy.TrimesterModifiers";
    private const string SectionBloodline = "Pregnancy.Bloodline";
    private const string SectionVisuals = "Pregnancy.TrimesterVisuals";
    private const string SectionSemenValue = "Pregnancy.SemenValue";
    private const string SectionOffspringCombat = "Pregnancy.OffspringCombat";
    private const string SectionAltar = "Pregnancy.Altar";
    private const string SectionShelter = "Pregnancy.ShelterAttack";
    private const string SectionOffspringArchetype = "Pregnancy.OffspringArchetype";

    public static ConfigEntry<bool> Enable;
    public static ConfigEntry<float> WombCapacityMl;
    public static ConfigEntry<float> MlPerContactOverride;
    public static ConfigEntry<bool> ShowWombMeter;
    public static ConfigEntry<bool> DebugLogging;
    public static ConfigEntry<float> BirthTransformDelaySeconds;
    public static ConfigEntry<float> BirthSlimeDisplayScale;
    public static ConfigEntry<float> OffspringDisplaySeconds;

    public static ConfigEntry<bool> OffspringArchetypeEnable;
    public static ConfigEntry<bool> OffspringArchetypeLogRolls;

    public static ConfigEntry<float> TrimesterTotalSeconds;
    public static ConfigEntry<float> Trimester2Threshold;
    public static ConfigEntry<float> Trimester3Threshold;

    public static ConfigEntry<bool> BlockDashInThirdTrimester;
    public static ConfigEntry<float> ThirdTrimesterJumpMultiplier;
    public static ConfigEntry<float> ThirdTrimesterMoveSpeedMultiplier;

    // Universal trimester debuffs (applied to all pregnancies by trimester level)
    public static ConfigEntry<int> TrimesterStatPenaltyPerLevel;
    public static ConfigEntry<float> TrimesterMoveSpeedPenalty;

    // Bloodline permanent bonuses (per living child in the hideout)
    public static ConfigEntry<int> DemonsIntBonusPerChild;
    public static ConfigEntry<int> DemonsStrBonusPerChild;
    public static ConfigEntry<float> DemonsRagePerSecondPerChild;

    public static ConfigEntry<int> ChurchStaBonusPerChild;
    public static ConfigEntry<int> ChurchLuckBonusPerChild;
    public static ConfigEntry<float> ChurchRagePerSecondPerChild;

    public static ConfigEntry<int> MonstersStrBonusPerChild;
    public static ConfigEntry<int> MonstersStaBonusPerChild;
    public static ConfigEntry<float> MonstersRagePerSecondPerChild;

    public static ConfigEntry<int> UndeadStrBonusPerChild;
    public static ConfigEntry<int> UndeadLuckBonusPerChild;
    public static ConfigEntry<int> UndeadStaBonusPerChild;
    public static ConfigEntry<float> UndeadRagePerSecondPerChild;

    public static ConfigEntry<int> BanditsDexBonusPerChild;
    public static ConfigEntry<int> BanditsLuckBonusPerChild;
    public static ConfigEntry<float> BanditsRagePerSecondPerChild;

    public static ConfigEntry<int> MafiaLuckBonusPerChild;
    public static ConfigEntry<int> MafiaDexBonusPerChild;
    public static ConfigEntry<float> MafiaRagePerSecondPerChild;

    // Bloodline caps
    public static ConfigEntry<int> MaxBloodlineStrBonus;
    public static ConfigEntry<int> MaxBloodlineIntBonus;
    public static ConfigEntry<int> MaxBloodlineDexBonus;
    public static ConfigEntry<int> MaxBloodlineStaBonus;
    public static ConfigEntry<int> MaxBloodlineLuckBonus;
    public static ConfigEntry<float> MaxBloodlineRagePerSecond;

    // Trimester visual effects
    public static ConfigEntry<float> TrimesterVisualIntervalSeconds;
    public static ConfigEntry<float> TrimesterVisualDurationSeconds;
    public static ConfigEntry<float> TrimesterVisualOffsetY;
    public static ConfigEntry<int> DemonsVisualEffectIndex;
    public static ConfigEntry<int> MonstersVisualEffectIndex;
    public static ConfigEntry<int> ChurchVisualEffectIndex;
    public static ConfigEntry<int> BanditsVisualEffectIndex;
    public static ConfigEntry<int> MafiaVisualEffectIndex;
    public static ConfigEntry<int> UndeadVisualEffectIndex;

    // Semen value multiplier (pregnancy fill speed)
    public static ConfigEntry<bool> EnableSemenValueMultiplier;
    public static ConfigEntry<float> MinimalCategoryMultiplier;
    public static ConfigEntry<float> StandardCategoryMultiplier;
    public static ConfigEntry<int> MaxSemenValueCap;

    public static ConfigEntry<bool> BlockAllPregnancy;
    public static ConfigEntry<bool> AllowFromDemons;
    public static ConfigEntry<bool> AllowFromMonsters;
    public static ConfigEntry<bool> AllowFromChurch;
    public static ConfigEntry<bool> AllowFromBandits;
    public static ConfigEntry<bool> AllowFromMafia;
    public static ConfigEntry<bool> AllowFromUndead;

    public static ConfigEntry<bool> PreventOffspringDamageToPlayer;
    public static ConfigEntry<bool> PreventPlayerDamageToOffspring;
    public static ConfigEntry<bool> PreventOffspringFactionFriendlyFire;

    public static ConfigEntry<bool> AltarResetWombMeter;
    public static ConfigEntry<bool> AltarResetActivePregnancy;

    public static ConfigEntry<bool> EnableShelterAttack;
    public static ConfigEntry<float> ShelterAttackTriggerChance;
    public static ConfigEntry<float> ShelterAttackArmDelaySeconds;
    public static ConfigEntry<float> ShelterAttackTimerSeconds;
    public static ConfigEntry<float> ShelterAttackAlertSeconds;
    public static ConfigEntry<float> ShelterAttackPhraseIntervalSeconds;
    public static ConfigEntry<float> ShelterAttackSpawnCooldownMin;
    public static ConfigEntry<float> ShelterAttackSpawnCooldownMax;
    public static ConfigEntry<float> ShelterAttackWaveIntroSeconds;
    public static ConfigEntry<float> ShelterAttackWaveBreakSeconds;
    public static ConfigEntry<float> ShelterAttackFinalWaveBreakSeconds;
    public static ConfigEntry<float> ShelterAttackTimeoutFlashSeconds;
    public static ConfigEntry<bool> ShelterAttackShowTimerHud;
    public static ConfigEntry<bool> ShelterAttackResetOnWin;
    public static ConfigEntry<bool> ShelterAttackResetOnLoss;

    private static bool _initialized;

    /// <summary>Convenience getter that handles null-config safely.</summary>
    public static bool IsEnabled => Enable != null && Enable.Value;

    public static void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;

        ConfigFile cfg = Plugin.Instance.Config;

        Enable = cfg.Bind(SectionGeneral, "Enable", true,
            "Master switch for the extended HellGate Pregnancy module (womb meter, faction-typed conception, trimesters, offspring). Vanilla base pregnancy is unaffected when this is false.");

        WombCapacityMl = cfg.Bind(SectionGeneral, "WombCapacityMl", 500f,
            "Womb buffer capacity in milliliters. While the womb is below capacity it is 'safe'; reaching capacity triggers a guaranteed conception by the dominant seed faction.");

        MlPerContactOverride = cfg.Bind(SectionGeneral, "MlPerContactOverride", 0f,
            "If > 0, every creampie adds this fixed amount of ml regardless of the game's native value. 0 = use the native per-event ml count from EnemyDate.Nakadasi.");

        ShowWombMeter = cfg.Bind(SectionGeneral, "ShowWombMeter", true,
            "Show the on-screen womb fill meter (bar + percentage).");

        DebugLogging = cfg.Bind(SectionGeneral, "DebugLogging", false,
            "Verbose logging of seed intake and conception events to the BepInEx console / LogOutput.log.");

        BirthTransformDelaySeconds = cfg.Bind(SectionGeneral, "BirthTransformDelaySeconds", 3f,
            "Seconds after birth before the slime transforms into the MafiaMuscle offspring.");

        BirthSlimeDisplayScale = cfg.Bind(SectionGeneral, "BirthSlimeDisplayScale", 0.5f,
            "Uniform scale of the birth slime before it transforms (1.0 = vanilla suraimu size).");

        OffspringDisplaySeconds = cfg.Bind(SectionGeneral, "OffspringDisplaySeconds", 30f,
            "Seconds the transformed offspring remains visible before moving to the hideout (or despawning outside ParishChurch).");

        OffspringArchetypeEnable = cfg.Bind(
            SectionOffspringArchetype,
            "Enable",
            true,
            "Roll a per-faction offspring prefab at birth (see HellGateJson/Pregnancy/OffspringArchetypes.json).");

        OffspringArchetypeLogRolls = cfg.Bind(
            SectionOffspringArchetype,
            "LogRolls",
            false,
            "Log each offspring archetype roll to BepInEx (independent of Pregnancy.DebugLogging).");

        TrimesterTotalSeconds = cfg.Bind(SectionTrimester, "TrimesterTotalSeconds", 90f,
            "Total duration of the pregnancy in real-time seconds. Default is 90s (30s per trimester) for testing; raise to 360s (6 minutes) for normal play.");

        Trimester2Threshold = cfg.Bind(SectionTrimester, "Trimester2Threshold", 0.333f,
            "Fraction of the pregnancy duration when the second trimester begins.");

        Trimester3Threshold = cfg.Bind(SectionTrimester, "Trimester3Threshold", 0.666f,
            "Fraction of the pregnancy duration when the third trimester begins.");

        BlockDashInThirdTrimester = cfg.Bind(SectionPhysics, "BlockDashInThirdTrimester", true,
            "If true, all dash actions (dodge, double-tap dash, dash-jump) are blocked during the third trimester.");

        ThirdTrimesterJumpMultiplier = cfg.Bind(SectionPhysics, "ThirdTrimesterJumpMultiplier", 0.65f,
            "Jump impulse multiplier during the third trimester (0.65 = 35% shorter jumps).");

        ThirdTrimesterMoveSpeedMultiplier = cfg.Bind(SectionPhysics, "ThirdTrimesterMoveSpeedMultiplier", 1f,
            "Ground movement speed multiplier during the third trimester (1 = no change).");

        // Universal trimester debuffs: stat drain and move speed penalty.
        TrimesterStatPenaltyPerLevel = cfg.Bind(SectionTrimesterModifiers, "TrimesterStatPenaltyPerLevel", 3,
            "Flat penalty to STR/DEX/INT/crit applied per current trimester level. Trimester 1 = -3, Trimester 2 = -6, Trimester 3 = -9.");
        TrimesterMoveSpeedPenalty = cfg.Bind(SectionTrimesterModifiers, "TrimesterMoveSpeedPenalty", 0.30f,
            "Ground move speed multiplier from II trimester onward (0.30 = -30%).");

        // Bloodline: permanent stat bonuses per living child in the hideout.
        DemonsIntBonusPerChild = cfg.Bind(SectionBloodline, "DemonsIntBonusPerChild", 2,
            "Per demon child: +INT.");
        DemonsStrBonusPerChild = cfg.Bind(SectionBloodline, "DemonsStrBonusPerChild", 1,
            "Per demon child: +STR.");
        DemonsRagePerSecondPerChild = cfg.Bind(SectionBloodline, "DemonsRagePerSecondPerChild", 0.05f,
            "Per demon child: +Rage % per second.");

        ChurchStaBonusPerChild = cfg.Bind(SectionBloodline, "ChurchStaBonusPerChild", 2,
            "Per church child: +STA (MAXtough).");
        ChurchLuckBonusPerChild = cfg.Bind(SectionBloodline, "ChurchLuckBonusPerChild", 1,
            "Per church child: +luck.");
        ChurchRagePerSecondPerChild = cfg.Bind(SectionBloodline, "ChurchRagePerSecondPerChild", 0.05f,
            "Per church child: +Rage % per second.");

        MonstersStrBonusPerChild = cfg.Bind(SectionBloodline, "MonstersStrBonusPerChild", 2,
            "Per monster child: +STR.");
        MonstersStaBonusPerChild = cfg.Bind(SectionBloodline, "MonstersStaBonusPerChild", 1,
            "Per monster child: +STA (MAXtough).");
        MonstersRagePerSecondPerChild = cfg.Bind(SectionBloodline, "MonstersRagePerSecondPerChild", 0.05f,
            "Per monster child: +Rage % per second.");

        UndeadStrBonusPerChild = cfg.Bind(SectionBloodline, "UndeadStrBonusPerChild", 1,
            "Per undead child: +STR.");
        UndeadLuckBonusPerChild = cfg.Bind(SectionBloodline, "UndeadLuckBonusPerChild", 1,
            "Per undead child: +luck.");
        UndeadStaBonusPerChild = cfg.Bind(SectionBloodline, "UndeadStaBonusPerChild", 1,
            "Per undead child: +STA (MAXtough).");
        UndeadRagePerSecondPerChild = cfg.Bind(SectionBloodline, "UndeadRagePerSecondPerChild", 0.05f,
            "Per undead child: +Rage % per second.");

        BanditsDexBonusPerChild = cfg.Bind(SectionBloodline, "BanditsDexBonusPerChild", 2,
            "Per bandit child: +DEX.");
        BanditsLuckBonusPerChild = cfg.Bind(SectionBloodline, "BanditsLuckBonusPerChild", 1,
            "Per bandit child: +luck.");
        BanditsRagePerSecondPerChild = cfg.Bind(SectionBloodline, "BanditsRagePerSecondPerChild", 0.05f,
            "Per bandit child: +Rage % per second.");

        MafiaLuckBonusPerChild = cfg.Bind(SectionBloodline, "MafiaLuckBonusPerChild", 2,
            "Per mafia child: +luck.");
        MafiaDexBonusPerChild = cfg.Bind(SectionBloodline, "MafiaDexBonusPerChild", 1,
            "Per mafia child: +DEX.");
        MafiaRagePerSecondPerChild = cfg.Bind(SectionBloodline, "MafiaRagePerSecondPerChild", 0.05f,
            "Per mafia child: +Rage % per second.");

        // Bloodline caps
        MaxBloodlineStrBonus = cfg.Bind(SectionBloodline, "MaxBloodlineStrBonus", 20,
            "Maximum total +STR from all bloodline sources.");
        MaxBloodlineIntBonus = cfg.Bind(SectionBloodline, "MaxBloodlineIntBonus", 20,
            "Maximum total +INT from all bloodline sources.");
        MaxBloodlineDexBonus = cfg.Bind(SectionBloodline, "MaxBloodlineDexBonus", 20,
            "Maximum total +DEX from all bloodline sources.");
        MaxBloodlineStaBonus = cfg.Bind(SectionBloodline, "MaxBloodlineStaBonus", 20,
            "Maximum total +STA (MAXtough) from all bloodline sources.");
        MaxBloodlineLuckBonus = cfg.Bind(SectionBloodline, "MaxBloodlineLuckBonus", 20,
            "Maximum total +luck from all bloodline sources.");
        MaxBloodlineRagePerSecond = cfg.Bind(SectionBloodline, "MaxBloodlineRagePerSecond", 1.0f,
            "Maximum total passive Rage % per second from all bloodline sources.");

        // Trimester visual effects.
        TrimesterVisualIntervalSeconds = cfg.Bind(SectionVisuals, "TrimesterVisualIntervalSeconds", 5f,
            "Seconds between periodic visual effects during II and III trimesters.");
        TrimesterVisualDurationSeconds = cfg.Bind(SectionVisuals, "TrimesterVisualDurationSeconds", 2f,
            "Duration of each spawned visual effect.");
        TrimesterVisualOffsetY = cfg.Bind(SectionVisuals, "TrimesterVisualOffsetY", 0.35f,
            "Vertical offset for the spawned effect relative to the player root.");
        DemonsVisualEffectIndex = cfg.Bind(SectionVisuals, "DemonsVisualEffectIndex", 3,
            "playereffect.Buffeffect index used for Demons trimester visuals (-1 = off).");
        MonstersVisualEffectIndex = cfg.Bind(SectionVisuals, "MonstersVisualEffectIndex", 3,
            "playereffect.Buffeffect index used for Monsters trimester visuals (-1 = off).");
        ChurchVisualEffectIndex = cfg.Bind(SectionVisuals, "ChurchVisualEffectIndex", 3,
            "playereffect.Buffeffect index used for Church trimester visuals (-1 = off).");
        BanditsVisualEffectIndex = cfg.Bind(SectionVisuals, "BanditsVisualEffectIndex", 0,
            "playereffect.Buffeffect index used for Bandits trimester visuals (-1 = off).");
        MafiaVisualEffectIndex = cfg.Bind(SectionVisuals, "MafiaVisualEffectIndex", 0,
            "playereffect.Buffeffect index used for Mafia trimester visuals (-1 = off).");
        UndeadVisualEffectIndex = cfg.Bind(SectionVisuals, "UndeadVisualEffectIndex", 1,
            "playereffect.Buffeffect index used for Undead trimester visuals (-1 = off).");

        // Semen value multiplier for pregnancy fill speed.
        EnableSemenValueMultiplier = cfg.Bind(SectionSemenValue, "EnableSemenValueMultiplier", true,
            "If true, weak enemies deposit more semen during Nakadasi so pregnancy progresses at a reasonable pace.");
        MinimalCategoryMultiplier = cfg.Bind(SectionSemenValue, "MinimalCategoryMultiplier", 6.0f,
            "Multiplier for the MINIMAL semen category (base <= 20 ml).");
        StandardCategoryMultiplier = cfg.Bind(SectionSemenValue, "StandardCategoryMultiplier", 3.0f,
            "Multiplier for the STANDARD semen category (base 24-60 ml).");
        MaxSemenValueCap = cfg.Bind(SectionSemenValue, "MaxSemenValueCap", 120,
            "Maximum ml per Nakadasi after multipliers are applied.");

        BlockAllPregnancy = cfg.Bind(SectionBlock, "BlockAllPregnancy", false,
            "If true, no seed is ever accumulated and conception never occurs through this module.");

        AllowFromDemons = cfg.Bind(SectionBlock, "AllowFromDemons", true, "Allow conception sourced from the Demons faction.");
        AllowFromMonsters = cfg.Bind(SectionBlock, "AllowFromMonsters", true, "Allow conception sourced from the Monsters faction.");
        AllowFromChurch = cfg.Bind(SectionBlock, "AllowFromChurch", true, "Allow conception sourced from the Church faction.");
        AllowFromBandits = cfg.Bind(SectionBlock, "AllowFromBandits", true, "Allow conception sourced from the Bandits faction (all bandit sub-families).");
        AllowFromMafia = cfg.Bind(SectionBlock, "AllowFromMafia", true, "Allow conception sourced from the Mafia faction.");
        AllowFromUndead = cfg.Bind(SectionBlock, "AllowFromUndead", true, "Allow conception sourced from the Undead faction.");

        PreventOffspringDamageToPlayer = cfg.Bind(
            SectionOffspringCombat,
            "PreventOffspringDamageToPlayer",
            true,
            "If true, hideout offspring cannot damage or grab Aradia (player). Includes grab-via-attack and collision grab.");

        PreventPlayerDamageToOffspring = cfg.Bind(
            SectionOffspringCombat,
            "PreventPlayerDamageToOffspring",
            true,
            "If true, player weapons and magic cannot damage offspring in the hideout.");

        PreventOffspringFactionFriendlyFire = cfg.Bind(
            SectionOffspringCombat,
            "PreventOffspringFactionFriendlyFire",
            true,
            "If true, Witch-faction offspring cannot damage each other. Set false to allow sibling brawls.");

        AltarResetWombMeter = cfg.Bind(
            SectionAltar,
            "ResetWombMeter",
            true,
            "When touching an altar (Savepoint_on.fun_ALLreset): clear accumulated semen in the HellGate womb meter. Disable when using future cleanse items instead.");

        AltarResetActivePregnancy = cfg.Bind(
            SectionAltar,
            "ResetActivePregnancy",
            true,
            "When touching an altar: abort active gestation (trimester I–III) and any queued post-H-scene conception. Mirrors vanilla BADstatusReset for pregnancy. Disable when using future abortifacient items instead.");

        EnableShelterAttack = cfg.Bind(
            SectionShelter,
            "Enable",
            true,
            "Enable dynamic hideout shelter attack events (children in ParishChurch are attacked while Aradia is away).");

        ShelterAttackTriggerChance = cfg.Bind(
            SectionShelter,
            "TriggerChance",
            0.20f,
            "Chance (0.0–1.0) that a shelter attack is rolled after ArmDelaySeconds following any zone transition (door, altar, teleport). 1.0 = always try, 0.0 = never.");

        ShelterAttackArmDelaySeconds = cfg.Bind(
            SectionShelter,
            "ArmDelaySeconds",
            2f,
            "Real-time seconds after a zone transition before the trigger chance is rolled once. Avoids hitches right after loads.");

        ShelterAttackTimerSeconds = cfg.Bind(
            SectionShelter,
            "TimerSeconds",
            60f,
            "Real-time seconds after a successful arm roll before the assault can begin in ParishChurch.");

        ShelterAttackAlertSeconds = cfg.Bind(
            SectionShelter,
            "AlertSeconds",
            15f,
            "How many seconds before the assault deadline the warning phrases start appearing above Aradia (clamped to TimerSeconds).");

        ShelterAttackPhraseIntervalSeconds = cfg.Bind(
            SectionShelter,
            "PhraseIntervalSeconds",
            5f,
            "Seconds between red floating warning phrases during the alert phase (also used as on-screen phrase display duration).");

        ShelterAttackSpawnCooldownMin = cfg.Bind(
            SectionShelter,
            "SpawnCooldownMin",
            4f,
            "Minimum cooldown between enemy spawns at the same ParishChurch point.");

        ShelterAttackSpawnCooldownMax = cfg.Bind(
            SectionShelter,
            "SpawnCooldownMax",
            8f,
            "Maximum cooldown between enemy spawns at the same ParishChurch point.");

        ShelterAttackWaveIntroSeconds = cfg.Bind(
            SectionShelter,
            "WaveIntroSeconds",
            10f,
            "Seconds to show the WAVE 1 banner in the hideout before the first enemies spawn.");

        ShelterAttackWaveBreakSeconds = cfg.Bind(
            SectionShelter,
            "WaveBreakSeconds",
            10f,
            "Real-time pause between cleared waves (before waves 2, 3, ...). Shows the next wave banner and a countdown in ParishChurch.");

        ShelterAttackFinalWaveBreakSeconds = cfg.Bind(
            SectionShelter,
            "FinalWaveBreakSeconds",
            15f,
            "Real-time pause before the final (boss) wave spawns, overriding WaveBreakSeconds for the last wave.");

        ShelterAttackTimeoutFlashSeconds = cfg.Bind(
            SectionShelter,
            "TimeoutFlashSeconds",
            3f,
            "Seconds the red TIME OUT label and bar stay on screen before timeout defeat presentation.");

        ShelterAttackShowTimerHud = cfg.Bind(
            SectionShelter,
            "ShowTimerHud",
            true,
            "Show on-screen timers: attack countdown while away, inter-wave countdown in the hideout, and wave banners.");

        ShelterAttackResetOnWin = cfg.Bind(
            SectionShelter,
            "ResetOnWin",
            true,
            "After a successful defense, reset the event so it can trigger again later.");

        ShelterAttackResetOnLoss = cfg.Bind(
            SectionShelter,
            "ResetOnLoss",
            true,
            "After a failed defense (Aradia knocked out), reset the event so it can trigger again later.");

        Plugin.Log?.LogInfo($"[Pregnancy] Module initialized. Enable={Enable.Value}, WombCapacityMl={WombCapacityMl.Value}, ShowWombMeter={ShowWombMeter.Value}, DebugLogging={DebugLogging.Value}");
    }

    /// <summary>
    /// True when the given source faction must not contribute seed.
    /// Recognised factions honour their per-faction toggle. Unclassified sources
    /// (Neutral / EventCore / unknown traps and creatures) count as neutral fill.
    /// Witch (Aradia's own faction) can never impregnate.
    /// </summary>
    public static bool IsFactionBlocked(int factionId)
    {
        if (BlockAllPregnancy != null && BlockAllPregnancy.Value)
            return true;

        switch (NormalizeSourceFaction(factionId))
        {
            case FactionIds.Demons: return AllowFromDemons != null && !AllowFromDemons.Value;
            case FactionIds.Monsters: return AllowFromMonsters != null && !AllowFromMonsters.Value;
            case FactionIds.Church: return AllowFromChurch != null && !AllowFromChurch.Value;
            case FactionIds.Bandits: return AllowFromBandits != null && !AllowFromBandits.Value;
            case FactionIds.Mafia: return AllowFromMafia != null && !AllowFromMafia.Value;
            case FactionIds.Undead: return AllowFromUndead != null && !AllowFromUndead.Value;
            case FactionIds.Witch: return true; // cannot self-impregnate
            default:
                // Neutral / EventCore / unknown -> allowed, counts as neutral fill.
                return false;
        }
    }

    /// <summary>
    /// Collapses bandit sub-families (101-103) into the canonical Bandits id so blocking
    /// and dominance work on the five player-facing source factions (+ Undead).
    /// </summary>
    public static int NormalizeSourceFaction(int factionId)
    {
        if (FactionIds.IsBanditFamily(factionId))
            return FactionIds.Bandits;
        return factionId;
    }
}
