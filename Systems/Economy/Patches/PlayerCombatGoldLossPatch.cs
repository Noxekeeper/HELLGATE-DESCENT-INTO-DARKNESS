using HarmonyLib;

namespace NoREroMod.Systems.Economy.Patches;

/// <summary>
/// Registers combat hits on the same pipeline as GrabViaAttack (fun_damage entry, not HP delta).
/// Dash-avoid (stepfrag) is excluded; guard blocks and chip still count.
/// </summary>
[HarmonyPatch(typeof(playercon), nameof(playercon.fun_damage))]
internal static class PlayerCombatGoldLossLegacyPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(playercon __instance, ref bool __state)
    {
        __state = __instance != null && __instance.stepfrag;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(playercon __instance, bool __state)
    {
        CombatGoldLossRuntime.TryProcessPlayerHit(__instance, __state);
    }
}

[HarmonyPatch(typeof(playercon), nameof(playercon.fun_damage_Improvement))]
internal static class PlayerCombatGoldLossImprovedPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(playercon __instance, ref bool __state)
    {
        __state = __instance != null && __instance.stepfrag;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(playercon __instance, bool __state)
    {
        CombatGoldLossRuntime.TryProcessPlayerHit(__instance, __state);
    }
}
