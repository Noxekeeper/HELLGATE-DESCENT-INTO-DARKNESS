using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// Multiplies the amount of semen deposited during <c>EnemyDate.Nakadasi</c> and
/// <c>Trapdata.Nakadasi</c> for weak enemy categories. The categories are based on
/// the semen mapping in <c>docs/Pregnancy/SemenValue_Mapping.md</c>.
/// Also zeroes deposits from non-vaginal H-scenes so they never fill the womb meter.
/// </summary>
internal static class SemenValueMultiplier
{
    // Non-vaginal climax sources: Nakadasi must not raise NakadashiValue / womb fill.
    // Applied even when EnableSemenValueMultiplier is false (correctness, not balance).
    private static readonly HashSet<string> NoWombDeposit = new()
    {
        "MummyMan" // oral (MummyManERO FIN/FIN2); not a vaginal creampie
    };

    // MINIMAL category: base 10-20 ml. Names come from SemenValue_Mapping.md.
    private static readonly HashSet<string> MinimalCategory = new()
    {
        "suraimu",
        "TyoukyoushiRed",
        "TouzokuAxe",
        "Tyoukyoushi",
        "SuccubusSpine",
        "Pilgrim",
        "BossTouzoku",
        "Praymaiden",
        "Kinoko",
        "Arulaune",
        "Ivy_monster",
        "Librarian"
    };

    // STANDARD category: base 24-60 ml (30-50 in the mapping). Names come from SemenValue_Mapping.md.
    private static readonly HashSet<string> StandardCategory = new()
    {
        "TouzokuNormal",
        "TrapSpider",
        "Tentacle",
        "GobTrap",
        "WallHip",
        "CrawlingDead",
        "Gorotuki",
        "MummyDog",
        "Rosewarm",
        "PictureEroNon",
        "Inquisition",
        "BlackOozetrap",
        "BlackOozeTrapTypeB",
        "Vagrant",
        "Sisiruirui",
        "Kakashi",
        "SinnerslaveCrossbow"
    };

    public static int ApplyMultiplier(object source, int count)
    {
        if (!PregnancyConfig.IsEnabled)
            return count;
        if (source == null || count <= 0)
            return count;

        string className = source.GetType().Name;
        if (NoWombDeposit.Contains(className))
            return 0;

        if (PregnancyConfig.EnableSemenValueMultiplier == null || !PregnancyConfig.EnableSemenValueMultiplier.Value)
            return count;

        float multiplier = 1f;

        if (MinimalCategory.Contains(className))
            multiplier = PregnancyConfig.MinimalCategoryMultiplier?.Value ?? 4.0f;
        else if (StandardCategory.Contains(className))
            multiplier = PregnancyConfig.StandardCategoryMultiplier?.Value ?? 2.0f;

        if (multiplier <= 1f)
            return count;

        int cap = PregnancyConfig.MaxSemenValueCap?.Value ?? 120;
        int result = Mathf.CeilToInt(count * multiplier);
        return Mathf.Min(result, cap);
    }

    [HarmonyPatch(typeof(EnemyDate), "Nakadasi")]
    internal static class EnemyDateNakadasiMultiplierPatch
    {
        [HarmonyPrefix]
        private static void Prefix(EnemyDate __instance, ref int count)
        {
            count = ApplyMultiplier(__instance, count);
        }
    }

    [HarmonyPatch(typeof(Trapdata), "Nakadasi")]
    internal static class TrapdataNakadasiMultiplierPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Trapdata __instance, ref int count)
        {
            count = ApplyMultiplier(__instance, count);
        }
    }
}
