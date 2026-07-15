using HarmonyLib;

namespace NoREroMod.Patches.Enemy.BossTouzokuCustom;

/// <summary>EroBOSSTouzoku bootstrap + field-mob tweaks on vanilla eroanime.</summary>
[HarmonyPatch(typeof(BossTouzoku), "EROstartset")]
internal static class BossTouzokuCustomEroStartSetPatch
{
    [HarmonyPrefix]
    private static bool Prefix(BossTouzoku __instance)
    {
        if (!BossTouzokuCustomStats.IsCustom(__instance))
            return true;

        BossTouzokuCustomRuntime.ApplyEroStartSet(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(BossTouzoku), "eroanime")]
internal static class BossTouzokuCustomEroAnimePatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(BossTouzoku __instance)
    {
        if (!BossTouzokuCustomStats.IsCustom(__instance))
            return true;

        if (__instance.eroflag)
        {
            BossTouzokuCustomRuntime.RunSafeEroAnime(__instance);
            return false;
        }

        return true;
    }

    [HarmonyPostfix]
    private static void Postfix(BossTouzoku __instance)
    {
        if (!BossTouzokuCustomStats.IsCustom(__instance))
            return;

        if (!__instance.eroflag)
            BossTouzokuCustomRuntime.OnVanillaEroExit(__instance);
    }
}
