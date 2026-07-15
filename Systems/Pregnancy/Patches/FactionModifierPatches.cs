using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>
/// Harmony patches that apply bloodline bonuses and trimester debuffs to player stats.
/// Bloodline bonuses are always active while children are in the hideout.
/// Trimester debuffs are active while pregnant and scale with the current trimester.
/// </summary>
internal static class FactionModifierPatches
{
    [HarmonyPatch(typeof(PlayerStatus), "AllSTR")]
    internal static class PlayerStatusAllStrPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref int __result)
        {
            if (!PregnancyConfig.IsEnabled)
                return;
            __result += OffspringBloodlineBonuses.StrBonus;
            __result += TrimesterDebuffs.StrPenalty;
        }
    }

    [HarmonyPatch(typeof(PlayerStatus), "AllINT")]
    internal static class PlayerStatusAllIntPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref int __result)
        {
            if (!PregnancyConfig.IsEnabled)
                return;
            __result += OffspringBloodlineBonuses.IntBonus;
            __result += TrimesterDebuffs.IntPenalty;
        }
    }

    [HarmonyPatch(typeof(PlayerStatus), "AllDEX")]
    internal static class PlayerStatusAllDexPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref int __result)
        {
            if (!PregnancyConfig.IsEnabled)
                return;
            __result += OffspringBloodlineBonuses.DexBonus;
            __result += TrimesterDebuffs.DexPenalty;
        }
    }

    [HarmonyPatch(typeof(PlayerStatus), "AllLuck")]
    internal static class PlayerStatusAllLuckPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref int __result)
        {
            if (!PregnancyConfig.IsEnabled)
                return;
            __result += OffspringBloodlineBonuses.LuckBonus;
            __result += TrimesterDebuffs.LuckPenalty;
        }
    }

    [HarmonyPatch(typeof(PlayerStatus), "AllTough")]
    internal static class PlayerStatusAllToughPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref float __result)
        {
            if (!PregnancyConfig.IsEnabled)
                return;
            __result += OffspringBloodlineBonuses.StaBonus;
        }
    }
}
