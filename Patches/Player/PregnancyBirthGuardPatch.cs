using HarmonyLib;
using NoREroMod;
using UnityEngine;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Pregnancy birth sets <see cref="playercon._easyESC"/> on FEEL3 END to block mash escape.
/// HellGate struggle prep must not clear that lock; premature <c>erodown=0</c> must not abort birth.
/// </summary>
internal static class PregnancyBirthGuardPatch
{
    [HarmonyPatch(typeof(playercon), nameof(playercon.Birth))]
    internal static class BirthPostfix
    {
        [HarmonyPostfix]
        private static void LockEasyEscForBirthSequence(playercon __instance)
        {
            if (__instance == null)
                return;

            __instance._easyESC = true;
            Plugin.isBirthing = true;
        }
    }

    [HarmonyPatch(typeof(PlayerconBadstatusPregnancy), nameof(PlayerconBadstatusPregnancy.Birthstart))]
    internal static class BirthstartPostfix
    {
        [HarmonyPostfix]
        private static void HideMainBodyForBirthOverlay(PlayerconBadstatusPregnancy __instance)
        {
            playercon player = Traverse.Create(__instance).Field("con_player").GetValue<playercon>();
            if (player == null)
                return;

            Plugin.isBirthing = true;
            PlayerEroContextUtility.HideMainPlayerBodyForBadstatusOverlay(player);
        }
    }

    [HarmonyPatch(typeof(PlayerconBadstatusPregnancy), nameof(PlayerconBadstatusPregnancy.Birthstart2))]
    internal static class Birthstart2Postfix
    {
        [HarmonyPostfix]
        private static void HideMainBodyForBirthOverlay2(PlayerconBadstatusPregnancy __instance)
        {
            playercon player = Traverse.Create(__instance).Field("con_player").GetValue<playercon>();
            if (player == null)
                return;

            Plugin.isBirthing = true;
            PlayerEroContextUtility.HideMainPlayerBodyForBadstatusOverlay(player);
        }
    }

    [HarmonyPatch(typeof(PlayerconBadstatusPregnancy), nameof(PlayerconBadstatusPregnancy.Eroreset))]
    internal static class EroresetPrefix
    {
        [HarmonyPrefix]
        private static bool BlockPrematureBirthAbort(PlayerconBadstatusPregnancy __instance, int val)
        {
            if (val != 3)
                return true;

            playercon player = Traverse.Create(__instance).Field("con_player").GetValue<playercon>();
            if (player == null || player.erodown != 0)
                return true;

            // Natural completion clears _easyESC at birth spine JIGO before erodown drops.
            return !player._easyESC;
        }
    }

    [HarmonyPatch(typeof(PlayerconBadstatusPregnancy), nameof(PlayerconBadstatusPregnancy.Eroreset))]
    internal static class EroresetPostfix
    {
        [HarmonyPostfix]
        private static void ClearBirthFlag(PlayerconBadstatusPregnancy __instance, int val)
        {
            if (val == 3)
            {
                Plugin.isBirthing = false;
                BirthRecoveryStruggleState.EndRecovery();
            }
        }
    }

    [HarmonyPatch(typeof(PlayerconBadstatusPregnancy), nameof(PlayerconBadstatusPregnancy.Eroreset2))]
    internal static class Eroreset2Prefix
    {
        [HarmonyPrefix]
        private static bool BlockPrematureBirthAbort2(PlayerconBadstatusPregnancy __instance, int val)
        {
            if (val != 4)
                return true;

            playercon player = Traverse.Create(__instance).Field("con_player").GetValue<playercon>();
            if (player == null || player.erodown != 0)
                return true;

            return !player._easyESC;
        }
    }

    [HarmonyPatch(typeof(PlayerconBadstatusPregnancy), nameof(PlayerconBadstatusPregnancy.Eroreset2))]
    internal static class Eroreset2Postfix
    {
        [HarmonyPostfix]
        private static void ClearBirthFlag2(PlayerconBadstatusPregnancy __instance, int val)
        {
            if (val == 4)
            {
                Plugin.isBirthing = false;
                BirthRecoveryStruggleState.EndRecovery();
            }
        }
    }
}
