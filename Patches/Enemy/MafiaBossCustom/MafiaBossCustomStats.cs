using HarmonyLib;
using NoREroMod.Systems.Pregnancy.Patches;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.MafiaBossCustom;

/// <summary>
/// Object-name key for the custom mafia boss (like BigoniBrother — distinguished from the original by name).
/// Stats are lowered so that 3–4 of the same enemy can be beaten (original has 1800 HP).
/// </summary>
internal static class MafiaBossCustomStats
{
    public const string ObjectNameKey = "MafiaBossCustom";

    /// <summary> Custom boss HP (original is 1800). </summary>
    public const float CustomMaxHp = 600f;

    public static bool IsMafiaBossCustom(Mafiamuscle mafia)
    {
        if (mafia == null || mafia.gameObject == null)
            return false;

        // Offspring use WitchOffspring_* names that still contain the archetype key.
        if (mafia.GetComponent<WitchOffspringController>() != null)
            return false;

        return mafia.gameObject.name != null
            && mafia.gameObject.name.Contains(ObjectNameKey);
    }
}

/// <summary>
/// Patch for Mafiamuscle.Start — lower HP and difficulty for objects named MafiaBossCustom.
/// </summary>
[HarmonyPatch(typeof(Mafiamuscle), "Start")]
internal static class MafiaBossCustomStartPatch
{
    [HarmonyPostfix]
    private static void Postfix(Mafiamuscle __instance)
    {
        try
        {
            if (!MafiaBossCustomStats.IsMafiaBossCustom(__instance))
                return;

            __instance.MaxHp = MafiaBossCustomStats.CustomMaxHp;
            __instance.Hp = MafiaBossCustomStats.CustomMaxHp;
        }
        catch (System.Exception)
        {
            // ignore
        }
    }
}
