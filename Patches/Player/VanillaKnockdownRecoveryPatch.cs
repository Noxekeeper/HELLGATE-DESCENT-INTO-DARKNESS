using HarmonyLib;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Safety net: if another patch zeros <see cref="playercon.erodown"/> without vanilla jump physics,
/// re-apply <c>act_downup</c> + <c>vspeed</c> for combat knockdown recovery.
/// </summary>
[HarmonyPatch(typeof(playercon), "fun_nowdamage")]
internal static class VanillaKnockdownRecoveryPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void CaptureErodown(playercon __instance, ref int __state)
    {
        __state = __instance != null ? __instance.erodown : 0;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void EnsureStandUpJump(playercon __instance, int __state)
    {
        if (!VanillaKnockdownRecoveryUtility.NeedsStandUpJump(__instance, __state))
            return;

        VanillaKnockdownRecoveryUtility.ApplyStandUpFromKnockdown(__instance);
    }
}
