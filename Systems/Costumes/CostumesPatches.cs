using System;
using System.Reflection;
using HarmonyLib;

namespace NoREroMod.Systems.Costumes;

/// <summary>
/// Keeps CosSkinflag[1]/[2] unlocked while [Costumes] Enable is on.
/// Prefix CostumeChange.Start so skinflagcheck sees unlocked flags before UI blanks slots.
/// </summary>
internal static class CostumesPatches
{
    private static bool _applied;

    public static void Apply(Harmony harmony)
    {
        if (_applied || harmony == null)
            return;

        try
        {
            MethodInfo dataLoad = AccessTools.Method(typeof(game_fragmng), "fun_DataLoad");
            if (dataLoad != null)
            {
                harmony.Patch(
                    dataLoad,
                    postfix: new HarmonyMethod(typeof(CostumesPatches), nameof(FunDataLoad_Postfix)));
            }

            MethodInfo costumeStart = AccessTools.Method(typeof(CostumeChange), "Start");
            if (costumeStart != null)
            {
                harmony.Patch(
                    costumeStart,
                    prefix: new HarmonyMethod(typeof(CostumesPatches), nameof(CostumeChangeStart_Prefix)));
            }

            _applied = true;
            Plugin.Log?.LogInfo("[Costumes] Patches applied");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError("[Costumes] Apply failed: " + ex.Message);
        }
    }

    private static void FunDataLoad_Postfix(game_fragmng __instance)
    {
        CostumesUnlock.ApplyToFrag(__instance);
    }

    private static void CostumeChangeStart_Prefix()
    {
        CostumesUnlock.ApplyIfEnabled();
    }
}
