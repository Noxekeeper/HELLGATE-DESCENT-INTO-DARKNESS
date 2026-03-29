namespace NoREroMod.Systems.CombatAi;

/// <summary>
/// Dorei AI (SinnerslaveCrossbow) config from same file as general Combat AI:
/// BepInEx/plugins/HellGateJson/CombatAi/CombatAi.json (DoreiEnable, DoreiDisableFlee, etc.).
/// </summary>
internal static class DoreiCombatAiConfig
{
    public static bool Enable => CombatAiConfig.DoreiEnable;
    public static bool DisableFlee => CombatAiConfig.DoreiDisableFlee;
    public static float PreferMeleeOverFleeChance => CombatAiConfig.DoreiPreferMeleeOverFleeChance;
    public static float MeleeRangeThreshold => CombatAiConfig.DoreiMeleeRangeThreshold;
    public static float MeleeAttackRateMultiplier => CombatAiConfig.DoreiMeleeAttackRateMultiplier;
}
