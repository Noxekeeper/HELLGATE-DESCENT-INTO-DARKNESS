using HarmonyLib;
using Spine;
using UnityEngine;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Birth spine JIGO hands off to downed recovery. Vanilla clears <see cref="playercon._easyESC"/>;
/// we reset SP and start the manual struggle phase without re-locking input.
/// </summary>
internal static class BirthRecoveryJigoPatch
{
    private static void OnBirthJigoEvent(Spine.Event e)
    {
        if (e?.Data == null || e.Data.Name != "JIGO")
            return;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
            return;

        playercon player = playerObj.GetComponent<playercon>();
        if (player == null)
            return;

        PlayerStatus status = null;
        try { status = Traverse.Create(player).Field<PlayerStatus>("playerstatus").Value; } catch { }

        BirthRecoveryStruggleState.OnBirthJigo(player, status);
    }

    [HarmonyPatch(typeof(BadstatusBirthMonster), "OnEvent")]
    internal static class BirthMonsterJigoPostfix
    {
        [HarmonyPostfix]
        private static void Postfix(Spine.Event e) => OnBirthJigoEvent(e);
    }

    [HarmonyPatch(typeof(BadstatusBirthMonstersecond), "OnEvent")]
    internal static class BirthMonsterSecondJigoPostfix
    {
        [HarmonyPostfix]
        private static void Postfix(Spine.Event e) => OnBirthJigoEvent(e);
    }
}

[HarmonyPatch(typeof(playercon), "fun_nowdamage")]
internal static class BirthRecoveryStandGuardPatch
{
    [HarmonyPrefix]
    private static void Prefix(playercon __instance, ref int __state)
    {
        __state = __instance != null ? __instance.erodown : 0;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(playercon __instance, int __state, PlayerStatus ___playerstatus)
    {
        if (__instance == null || __state == 0)
            return;

        if (!BirthRecoveryStruggleState.IsActive || !PlayerEroContextUtility.IsActivePregnancyBirth(__instance))
            return;

        if (__instance.erodown != 0)
            return;

        if (BirthRecoveryStruggleState.IsReadyToStand(___playerstatus))
        {
            BirthRecoveryStruggleState.PermitStandAndReleaseEasyEsc(__instance);
            return;
        }

        __instance.erodown = __state;
    }
}
