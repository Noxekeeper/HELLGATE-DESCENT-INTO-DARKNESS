using BepInEx.Configuration;

namespace NoREroMod.Systems.Fear;

/// <summary>
/// BepInEx binds for [FearSystem]. Kill-switch is <see cref="Enable"/>.
/// </summary>
internal static class FearConfig
{
    internal static ConfigEntry<bool> Enable;
    internal static ConfigEntry<bool> ShowBar;
    internal static ConfigEntry<bool> EnableCombatEffects;
    internal static ConfigEntry<float> BarOffsetX;
    internal static ConfigEntry<float> BarOffsetY;
    internal static ConfigEntry<float> BarOpacity;
    internal static ConfigEntry<float> PrideFallMult;
    internal static ConfigEntry<float> AuraFalloffMeters;
    internal static ConfigEntry<int> AuraStrongestN;
    internal static ConfigEntry<float> SoftDecayPerSecond;
    internal static ConfigEntry<float> HSceneFearPerSecond;
    internal static ConfigEntry<float> LowSpAuraThreshold;
    internal static ConfigEntry<float> LowSpAuraMult;
    internal static ConfigEntry<float> PanicBandThreshold;
    internal static ConfigEntry<float> ConfidenceBandThreshold;
    internal static ConfigEntry<float> LethalRespawnFloor;
    internal static ConfigEntry<float> NormalRespawnMorale;
    internal static ConfigEntry<float> BlockMorale;
    internal static ConfigEntry<float> AvoidMorale;
    internal static ConfigEntry<float> CounterMorale;
    internal static ConfigEntry<float> PerfectParryMorale;
    internal static ConfigEntry<float> HitMorale;
    internal static ConfigEntry<float> ComboEvery5Morale;
    internal static ConfigEntry<float> KillNormalMorale;
    internal static ConfigEntry<float> KillBossMorale;
    internal static ConfigEntry<float> RageActivationMorale;
    internal static ConfigEntry<float> VengeanceStrikeMorale;
    internal static ConfigEntry<float> StruggleQteMorale;
    internal static ConfigEntry<float> HeavyHitMorale;
    internal static ConfigEntry<float> GrabLandMorale;
    internal static ConfigEntry<float> HSceneStartMorale;
    internal static ConfigEntry<float> FinClimaxMorale;
    internal static ConfigEntry<float> BlockHeavyNoCounterMorale;
    internal static ConfigEntry<float> TurtleBlockNoCounterMorale;
    internal static ConfigEntry<float> CounterWindowSeconds;
    internal static ConfigEntry<float> DashCostPanicMax;
    internal static ConfigEntry<float> StaminaRegenConfidenceMax;
    internal static ConfigEntry<float> StruggleSpMultPerTenMorale;
    internal static ConfigEntry<bool> HeartbeatEnable;
    internal static ConfigEntry<float> HeartbeatVolume;
    internal static bool IsEnabled => Enable?.Value ?? false;

    internal static void Bind(BepInEx.Configuration.ConfigFile config)
    {
        Enable = config.Bind(
            "FearSystem",
            "Enable",
            true,
            "Master kill-switch for Fear/Morale. When false: no aura tick, no combat deltas, no bar, no save writes."
        );

        ShowBar = config.Bind(
            "FearSystem",
            "ShowBar",
            true,
            "Show bipolar Fear/Confidence bar (bottom-center, mirrored Mercy inset + BarOffset)."
        );

        EnableCombatEffects = config.Bind(
            "FearSystem",
            "EnableCombatEffects",
            true,
            "Soft combat modifiers in panic/confidence bands (dash cost, idle SP regen, Struggle/QTE SP mult)."
        );

        BarOffsetX = config.Bind("FearSystem", "BarOffsetX", 0f, "Extra X offset from Fear HUD base (bottom-center, Y=32), mirrored Mercy inset.");
        BarOffsetY = config.Bind("FearSystem", "BarOffsetY", 0f, "Extra Y offset from Fear HUD base (bottom-center, Y=32), mirrored Mercy inset.");
        BarOpacity = config.Bind("FearSystem", "BarOpacity", 0.9f, "Fear bar root opacity (0..1).");

        PrideFallMult = config.Bind(
            "FearSystem",
            "PrideFallMult",
            0.5f,
            "Scales negative ΔMorale when Morale > 0: delta *= 1 + (Morale/100)*PrideFallMult."
        );

        AuraFalloffMeters = config.Bind("FearSystem", "AuraFalloffMeters", 12f, "Distance beyond which enemy aura is 0.");
        AuraStrongestN = config.Bind("FearSystem", "AuraStrongestN", 2, "1 = strongest nearby aura only, 2+ = sum of the two strongest.");
        SoftDecayPerSecond = config.Bind("FearSystem", "SoftDecayPerSecond", 0.1f, "Morale soft-decay toward 0 when out of combat / no aura.");
        HSceneFearPerSecond = config.Bind(
            "FearSystem",
            "HSceneFearPerSecond",
            0.4f,
            "Fallback H-scene Morale pressure/sec if JSON Default.hSceneFearPerSecond is missing. Prefer HellGateJson/Fear/FearEnemies.json (per-enemy overrides when that enemy has eroflag)."
        );
        LowSpAuraThreshold = config.Bind(
            "FearSystem",
            "LowSpAuraThreshold",
            0.3f,
            "When current SP / max SP is at or below this (0.3 = 30%), aura and H-scene pressure are multiplied by LowSpAuraMult."
        );
        LowSpAuraMult = config.Bind(
            "FearSystem",
            "LowSpAuraMult",
            2f,
            "Aura / H-scene fear pressure multiplier while SP is at or below LowSpAuraThreshold."
        );
        PanicBandThreshold = config.Bind("FearSystem", "PanicBandThreshold", -50f, "Panic band starts at or below this Morale.");
        ConfidenceBandThreshold = config.Bind("FearSystem", "ConfidenceBandThreshold", 50f, "Confidence band starts at or above this Morale.");
        LethalRespawnFloor = config.Bind(
            "FearSystem",
            "LethalRespawnFloor",
            -30f,
            "After lethal-trap / fatality Take Vengeance trauma: Morale rises to −100, then settles to this floor (default −30)."
        );
        NormalRespawnMorale = config.Bind(
            "FearSystem",
            "NormalRespawnMorale",
            -10f,
            "After ordinary death Take Vengeance (no trap/fatality trauma): Morale is set to this value (default −10)."
        );

        BlockMorale = config.Bind("FearSystem", "BlockMorale", 1f, "ΔMorale on successful block (no counter yet).");
        AvoidMorale = config.Bind("FearSystem", "AvoidMorale", 2f, "ΔMorale on dash Avoid (Acttext Avoided).");
        CounterMorale = config.Bind("FearSystem", "CounterMorale", 2f, "ΔMorale on counter within CounterWindowSeconds after block.");
        PerfectParryMorale = config.Bind("FearSystem", "PerfectParryMorale", 10f, "ΔMorale on perfect parry (Acttext PARRY!!).");
        HitMorale = config.Bind("FearSystem", "HitMorale", 2f, "ΔMorale on each player weapon/magic hit that lands (OndamageSend / OndamageSendMagic).");
        ComboEvery5Morale = config.Bind("FearSystem", "ComboEvery5Morale", 0f, "Extra ΔMorale every 5 hits in combo (0 = off; per-hit uses HitMorale).");
        KillNormalMorale = config.Bind("FearSystem", "KillNormalMorale", 4f, "ΔMorale on normal kill.");
        KillBossMorale = config.Bind("FearSystem", "KillBossMorale", 10f, "ΔMorale on boss/elite kill.");
        RageActivationMorale = config.Bind("FearSystem", "RageActivationMorale", 3f, "ΔMorale when Rage mode turns on.");
        VengeanceStrikeMorale = config.Bind("FearSystem", "VengeanceStrikeMorale", 15f, "ΔMorale when VengeanceStrike spends Rage.");
        StruggleQteMorale = config.Bind("FearSystem", "StruggleQteMorale", 1f, "ΔMorale on successful struggle QTE yellow beat.");
        HeavyHitMorale = config.Bind("FearSystem", "HeavyHitMorale", -6f, "ΔMorale on heavy hit / knockdown (already negative).");
        GrabLandMorale = config.Bind("FearSystem", "GrabLandMorale", -8f, "ΔMorale when GrabViaAttack lands (already negative). H-scene start edge uses HSceneStartMorale.");
        HSceneStartMorale = config.Bind("FearSystem", "HSceneStartMorale", -4f, "ΔMorale on H-scene / trap-H start (eroflag rising edge). Deduped with GrabLand.");
        FinClimaxMorale = config.Bind("FearSystem", "FinClimaxMorale", -3.33f, "ΔMorale on FIN / climax beat (black-bg Activate / goblin 2ERO_iki SE). Already negative.");
        BlockHeavyNoCounterMorale = config.Bind("FearSystem", "BlockHeavyNoCounterMorale", -2.5f, "ΔMorale if heavy block expires without counter.");
        TurtleBlockNoCounterMorale = config.Bind("FearSystem", "TurtleBlockNoCounterMorale", -1f, "ΔMorale if light block expires without counter.");
        CounterWindowSeconds = config.Bind("FearSystem", "CounterWindowSeconds", 3f, "Seconds after block to land a counter for bonus Morale.");

        DashCostPanicMax = config.Bind("FearSystem", "DashCostPanicMax", 0.4f, "At Morale -100: +40% dash SP cost (lerp from panic band).");
        StaminaRegenConfidenceMax = config.Bind("FearSystem", "StaminaRegenConfidenceMax", 0.25f, "At Morale +100: +25% idle SP regen.");
        StruggleSpMultPerTenMorale = config.Bind(
            "FearSystem",
            "StruggleSpMultPerTenMorale",
            0.025f,
            "Linear Struggle/QTE/SimpleQTE SP mult: ±this fraction per ±10 Morale. Default 0.025 = −10 fear → −2.5% SP/click, +10 morale → +2.5% SP/click."
        );

        HeartbeatEnable = config.Bind(
            "FearSystem",
            "HeartbeatEnable",
            true,
            "Play HeartBeat.wav from sources/HellGate_sources/Fear (fallback CustomDeath) while afraid."
        );
        HeartbeatVolume = config.Bind(
            "FearSystem",
            "HeartbeatVolume",
            0.85f,
            "Fear HeartBeat volume (0 = mute, 1 = full)."
        );
    }
}
