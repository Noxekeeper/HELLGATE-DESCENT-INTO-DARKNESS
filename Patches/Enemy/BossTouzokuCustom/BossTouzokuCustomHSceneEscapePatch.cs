using System;
using HarmonyLib;
using NoREroMod.Patches.Player;
using NoREroMod.Systems.Cache;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Patches.Enemy.BossTouzokuCustom;

/// <summary>
/// Field BossTouzokuCustom can keep eroflag/erodata after the player fills SP if vanilla
/// <c>fun_nowdamage</c> never ran (missing _SOUSA) or boss Update lags one frame behind erodown=0.
/// </summary>
internal static class BossTouzokuCustomHSceneEscapePatch
{
    internal static void AbortActiveFieldHSceneOnPlayerEscape(playercon player, bool requireErodownClear)
    {
        if (player == null)
            return;

        if (requireErodownClear && player.erodown != 0)
            return;

        bool abortedAny = false;

        foreach (BossTouzoku boss in Object.FindObjectsOfType<BossTouzoku>())
        {
            if (boss == null || !BossTouzokuCustomStats.IsCustom(boss))
                continue;

            if (!boss.eroflag && (boss.erodata == null || !boss.erodata.activeSelf))
                continue;

            try
            {
                if (player.erodown == 0)
                    BossTouzokuCustomRuntime.RunSafeEroAnime(boss);
                else
                    BossTouzokuCustomRuntime.ForceAbortFieldHScene(boss);

                abortedAny = true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("[BossTouzokuCustom] H-scene abort failed: " + ex.Message);
            }
        }

        if (!abortedAny)
            return;

        ClearPlayerHSceneFlags(player);
        PlayerCombatControlRecovery.RestoreAfterStruggleEscape();
    }

    private static void ClearPlayerHSceneFlags(playercon player)
    {
        player.eroflag = false;

        try
        {
            Traverse.Create(player).Field("_eroflag2").SetValue(false);
        }
        catch
        {
        }
    }
}

[HarmonyPatch(typeof(StruggleSystem), nameof(StruggleSystem.startGrabInvul))]
internal static class BossTouzokuCustomHSceneEscapeStrugglePatch
{
    [HarmonyPostfix]
    private static void OnStruggleEscapeCleanup()
    {
        try
        {
            playercon player = UnifiedPlayerCacheManager.GetPlayer();
            BossTouzokuCustomHSceneEscapePatch.AbortActiveFieldHSceneOnPlayerEscape(
                player,
                requireErodownClear: false);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[BossTouzokuCustom] Struggle cleanup failed: " + ex.Message);
        }
    }
}

[HarmonyPatch(typeof(playercon), "fun_nowdamage")]
internal static class BossTouzokuCustomHSceneEscapeFunNowDamagePatch
{
    private static int _playerErodownBeforeFunNowdamage;

    [HarmonyPrefix]
    private static void BeforeFunNowdamage(playercon __instance)
    {
        _playerErodownBeforeFunNowdamage = __instance != null ? __instance.erodown : 0;
    }

    [HarmonyPostfix]
    private static void AfterFunNowdamage(playercon __instance)
    {
        try
        {
            if (__instance == null
                || _playerErodownBeforeFunNowdamage == 0
                || __instance.erodown != 0)
            {
                return;
            }

            BossTouzokuCustomHSceneEscapePatch.AbortActiveFieldHSceneOnPlayerEscape(
                __instance,
                requireErodownClear: true);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[BossTouzokuCustom] fun_nowdamage cleanup failed: " + ex.Message);
        }
    }
}
