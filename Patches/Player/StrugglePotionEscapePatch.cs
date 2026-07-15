using HarmonyLib;

namespace NoREroMod.Patches.Player;

/// <summary>
/// HellGate ↔ NoREroMod struggle potion compatibility (no duplicate escape logic).
/// NoREroMod <see cref="PlayerConPatch"/> handles Q / HP potion escape; HellGate only:
/// 1) sets <see cref="PlayerStatus._SOUSA"/> before <c>fun_nowdamage</c> (Update postfix is too late);
/// 2) blocks vanilla <see cref="playercon.Item_use"/> during H/struggle so MP is not drunk after Q;
/// 3) resyncs potion HUD after NoREroMod <c>_USE_HPposion</c> (writes HP count onto MP slot icon).
/// </summary>
internal static class StrugglePotionNorCompat
{
    internal static int HpBeforeFunNowdamage;

    internal static bool IsPotionEscapeEnabled() => Plugin.allowStrugglePotion?.Value ?? false;

    internal static bool IsStruggleContext(playercon player)
    {
        if (player == null)
            return false;
        return player.erodown != 0 || player.eroflag;
    }

    internal static bool ShouldBlockVanillaItemUse(playercon player)
    {
        return IsPotionEscapeEnabled() && IsStruggleContext(player);
    }
}

/// <summary>Run before NoREroMod <c>fun_nowdamage</c> prefix (~400).</summary>
[HarmonyPatch(typeof(playercon), "fun_nowdamage")]
internal static class StrugglePotionPrepareFunNowdamagePatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix(playercon __instance, PlayerStatus ___playerstatus)
    {
        if (__instance == null || ___playerstatus == null)
            return;
        if (PlayerEroContextUtility.ShouldBlockEnemyStruggleAutomation(__instance))
            return;
        if (__instance.erodown == 0 && !__instance.eroflag)
            return;

        if (StrugglePotionNorCompat.IsPotionEscapeEnabled())
            StrugglePotionNorCompat.HpBeforeFunNowdamage = ___playerstatus.HP_Posion;

        PlayerEnemyGrabStruggleSupport.EnableStruggleFlags(__instance, ___playerstatus);
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void FixPotionHudAfterNoREroMod(PlayerStatus ___playerstatus)
    {
        if (!StrugglePotionNorCompat.IsPotionEscapeEnabled() || ___playerstatus == null)
            return;
        if (___playerstatus.HP_Posion >= StrugglePotionNorCompat.HpBeforeFunNowdamage)
            return;

        try
        {
            if (___playerstatus._ItemChange)
                ___playerstatus._MPposnow = ___playerstatus.MP_Posion;
            else
                ___playerstatus._HPposnow = ___playerstatus.HP_Posion;
        }
        catch
        {
        }
    }
}

[HarmonyPatch(typeof(playercon), "Item_use")]
internal static class StrugglePotionBlockVanillaItemUsePatch
{
    [HarmonyPrefix]
    private static bool Prefix(playercon __instance)
    {
        if (StrugglePotionNorCompat.ShouldBlockVanillaItemUse(__instance))
            return false;
        return true;
    }
}
