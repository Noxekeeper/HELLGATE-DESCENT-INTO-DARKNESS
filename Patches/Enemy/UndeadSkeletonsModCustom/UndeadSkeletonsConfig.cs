using System.Collections.Generic;
using BepInEx.Configuration;

namespace NoREroMod.Patches.Enemy.UndeadSkeletonsModCustom;

/// <summary>
/// BepInEx binds for [UndeadSkeletons] (assets path) and one [UndeadSkeletons.&lt;SpawnKey&gt;] section per variant.
/// </summary>
internal static class UndeadSkeletonsConfig
{
    internal sealed class VariantEntries
    {
        internal ConfigEntry<float> HpMultiplier;
        internal ConfigEntry<float> MaxHp;
        internal ConfigEntry<float> MoveSpeedMultiplier;
        internal ConfigEntry<float> AnimSpeedMultiplier;
    }

    private const float DefaultHpMultiplier = 0.25f;
    private const float DefaultSpeedMultiplier = 1f;

    // Previous defaults; cfg files still holding them move to the new defaults.
    private const float LegacyHpMultiplier = 0.3333f;
    private const float LegacySpeedMultiplier = 1.75f;

    internal static ConfigEntry<string> AssetsPath;

    private static readonly Dictionary<UndeadSkeletonVariant, VariantEntries> Entries =
        new Dictionary<UndeadSkeletonVariant, VariantEntries>();

    internal static void Bind(ConfigFile config)
    {
        AssetsPath = config.Bind(
            "UndeadSkeletons",
            "AssetsPath",
            "",
            "Path to UndeadSkeletons Spine folder (relative to game root). Empty = sources/HellGate_sources/Enemies/UndeadSkeletons. "
            + "One subfolder per spawn key (BigMermanUndead, BigoniUndead, PilgrimUndead, VagrantUndead) with <Key>.json/.atlas/.png."
        );

        foreach (UndeadSkeletonVariant variant in UndeadSkeletonsLoader.AllVariants)
        {
            string key = UndeadSkeletonsLoader.GetSpawnKey(variant);
            string section = "UndeadSkeletons." + key;
            string baseName = UndeadSkeletonsLoader.GetBaseDescription(variant);
            Entries[variant] = new VariantEntries
            {
                HpMultiplier = config.Bind(
                    section,
                    "HpMultiplier",
                    DefaultHpMultiplier,
                    new ConfigDescription(
                        $"HP of {key} relative to vanilla {baseName} HP (after game difficulty). 0.25 = four times less. Ignored when MaxHp > 0.",
                        new AcceptableValueRange<float>(0.01f, 10f))
                ),
                MaxHp = config.Bind(
                    section,
                    "MaxHp",
                    0f,
                    $"Fixed max HP for {key}; overrides HpMultiplier. 0 = use HpMultiplier."
                ),
                MoveSpeedMultiplier = config.Bind(
                    section,
                    "MoveSpeedMultiplier",
                    DefaultSpeedMultiplier,
                    new ConfigDescription(
                        $"Walk/run and attack-lunge speed of {key} relative to vanilla {baseName} (EnemyDate.enmMovespeed). 1 = vanilla. Applied on spawn.",
                        new AcceptableValueRange<float>(0.1f, 5f))
                ),
                AnimSpeedMultiplier = config.Bind(
                    section,
                    "AnimSpeedMultiplier",
                    DefaultSpeedMultiplier,
                    new ConfigDescription(
                        $"Spine playback speed of {key} relative to vanilla: attack wind-up and all combat animations. 1 = vanilla. Applied every frame.",
                        new AcceptableValueRange<float>(0.1f, 5f))
                )
            };
            MigrateLegacyDefaults(Entries[variant]);
        }
    }

    private static void MigrateLegacyDefaults(VariantEntries e)
    {
        if (System.Math.Abs(e.HpMultiplier.Value - LegacyHpMultiplier) < 0.0005f)
            e.HpMultiplier.Value = DefaultHpMultiplier;
        if (System.Math.Abs(e.MoveSpeedMultiplier.Value - LegacySpeedMultiplier) < 0.0005f)
            e.MoveSpeedMultiplier.Value = DefaultSpeedMultiplier;
        if (System.Math.Abs(e.AnimSpeedMultiplier.Value - LegacySpeedMultiplier) < 0.0005f)
            e.AnimSpeedMultiplier.Value = DefaultSpeedMultiplier;
    }

    internal static float GetHpMultiplier(UndeadSkeletonVariant variant)
    {
        return Entries.TryGetValue(variant, out VariantEntries e) ? e.HpMultiplier?.Value ?? 1f : 1f;
    }

    internal static float GetMaxHp(UndeadSkeletonVariant variant)
    {
        return Entries.TryGetValue(variant, out VariantEntries e) ? e.MaxHp?.Value ?? 0f : 0f;
    }

    internal static float GetMoveSpeedMultiplier(UndeadSkeletonVariant variant)
    {
        return Entries.TryGetValue(variant, out VariantEntries e) ? e.MoveSpeedMultiplier?.Value ?? 1f : 1f;
    }

    internal static float GetAnimSpeedMultiplier(UndeadSkeletonVariant variant)
    {
        return Entries.TryGetValue(variant, out VariantEntries e) ? e.AnimSpeedMultiplier?.Value ?? 1f : 1f;
    }
}
