using BepInEx.Configuration;

namespace NoREroMod.Patches.Enemy.DemonTentacleModCustom;

/// <summary>BepInEx binds for [DemonTentacle].</summary>
internal static class DemonTentacleConfig
{
    private const float DefaultMaxHp = 800f;
    private const float DefaultScaleMultiplier = 0.35f;

    internal static ConfigEntry<string> AssetsPath;
    internal static ConfigEntry<float> MaxHp;
    internal static ConfigEntry<float> ScaleMultiplier;

    internal static void Bind(ConfigFile config)
    {
        AssetsPath = config.Bind(
            "DemonTentacle",
            "AssetsPath",
            "",
            "Path to DemonTentacle Spine folder (relative to game root). Empty = sources/HellGate_sources/Enemies/Demon_tentacle. "
            + "Expects Demon_tentacle.json/.atlas/.png."
        );

        MaxHp = config.Bind(
            "DemonTentacle",
            "MaxHp",
            DefaultMaxHp,
            new ConfigDescription(
                "Max HP of DemonTentacle on Normal/Hard. Scaled by game difficulty like vanilla enemies (Easy x0.6, Hardest x1.1). "
                + "Vanilla AngelStatue dies to any single hit; DemonTentacle takes regular weapon/magic damage instead.",
                new AcceptableValueRange<float>(1f, 100000f))
        );

        ScaleMultiplier = config.Bind(
            "DemonTentacle",
            "ScaleMultiplier",
            DefaultScaleMultiplier,
            new ConfigDescription(
                "Visual size of the DemonTentacle skeleton relative to the vanilla AngelStatue import scale. "
                + "Hitboxes stay vanilla AngelStatue size. Requires game restart.",
                new AcceptableValueRange<float>(0.05f, 3f))
        );
    }

    internal static float GetScaleMultiplier()
    {
        float value = ScaleMultiplier?.Value ?? DefaultScaleMultiplier;
        return value > 0f ? value : DefaultScaleMultiplier;
    }

    internal static float GetBaseMaxHp()
    {
        float value = MaxHp?.Value ?? DefaultMaxHp;
        return value > 0f ? value : DefaultMaxHp;
    }
}
