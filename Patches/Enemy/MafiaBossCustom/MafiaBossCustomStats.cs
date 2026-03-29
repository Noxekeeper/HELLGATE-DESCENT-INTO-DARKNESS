using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.MafiaBossCustom;

/// <summary>
/// Имя объекта кастомного мафии-босса (as BigoniBrother — by name отличаем from оригинала).
/// Характеристики понижены, so that 3–4 the same enemy can было победить (оригинал 1800 HP).
/// </summary>
internal static class MafiaBossCustomStats
{
    public const string ObjectNameKey = "MafiaBossCustom";

    /// <summary> HP кастомного босса (оригинал 1800) </summary>
    public const float CustomMaxHp = 600f;

    public static bool IsMafiaBossCustom(Mafiamuscle mafia)
    {
        return mafia != null && mafia.gameObject != null
            && mafia.gameObject.name != null
            && mafia.gameObject.name.Contains(ObjectNameKey);
    }
}

/// <summary>
/// Патч Mafiamuscle.Start — for объектоin with именем MafiaBossCustom понижаем HP и сложность.
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
