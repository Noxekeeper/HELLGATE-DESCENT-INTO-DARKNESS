using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.Economy.Patches;

/// <summary>
/// On-death gold loss. Hooks vanilla <see cref="PlayerStatus.REstart_menu"/>.
///
/// JSON (<c>Economy.json</c>):
///   <see cref="EconomicConfig.OnPlayerDeath"/> — Keep | DropAll | DropPercent
///   <see cref="EconomicConfig.DeathDropPercent"/> — fraction when DropPercent (0.10 = 10%)
///   <see cref="EconomicConfig.DeathLossReturnable"/> — souls pile vs permanent destruction
///   <see cref="EconomicConfig.DeathLossMinAmount"/> — floor when percent rounds to 0
///   <see cref="EconomicConfig.DeathLossShowPopup"/> — floating −N on permanent loss
/// </summary>
[HarmonyPatch(typeof(PlayerStatus), "REstart_menu")]
internal static class PlayerDeathGoldDropPatch
{
    private static bool _alreadyHandledThisDeath;

    [HarmonyPostfix]
    private static void Postfix(PlayerStatus __instance)
    {
        if (__instance == null) return;
        if (!EconomicConfig.Enable) return;

        try
        {
            if (_alreadyHandledThisDeath) return;
            _alreadyHandledThisDeath = true;

            string mode = EconomicConfig.OnPlayerDeath;
            if (string.IsNullOrEmpty(mode) || mode.Equals("Keep", System.StringComparison.OrdinalIgnoreCase))
                return;

            long current = GoldWallet.Current;
            if (current <= 0) return;

            long lost = ComputeDeathLoss(current, mode);
            if (lost <= 0) return;
            if (lost > current)
                lost = current;

            GoldWallet.ModifyGold(-lost);

            bool returnable = EconomicConfig.DeathLossReturnable;
            if (returnable)
            {
                string sceneName = GetActiveSceneName();
                Vector3 deathPos = __instance.transform != null ? __instance.transform.position : Vector3.zero;
                Vector2 lostPos = new Vector2(deathPos.x + 1.0f, deathPos.y);

                GoldStaticMng.Set(lost, sceneName, lostPos);
                GoldDropAwarder.TrySpawnLostPile(lostPos, lost);
            }
            else if (EconomicConfig.DeathLossShowPopup && EconomicConfig.Popup.Enable)
            {
                GoldPopupSystem.ShowOverPlayer(-lost);
            }

            if (EconomicConfig.DebugLogging)
            {
                Plugin.Log?.LogInfo(
                    $"[GoldDeath] mode={mode} lost={lost} returnable={returnable} wallet={GoldWallet.Current}");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[GoldDeath] REstart_menu postfix threw: " + ex.Message);
        }
    }

    internal static long ComputeDeathLoss(long wallet, string mode)
    {
        if (wallet <= 0)
            return 0;

        if (mode.Equals("DropPercent", System.StringComparison.OrdinalIgnoreCase))
            return CombatGoldLossRuntime.ComputePercentLoss(wallet, EconomicConfig.DeathDropPercent, EconomicConfig.DeathLossMinAmount);

        if (mode.Equals("DropAll", System.StringComparison.OrdinalIgnoreCase))
            return wallet;

        return 0;
    }

    /// <summary>Re-arms the per-death idempotency flag when player respawns.</summary>
    public static void NotifyPlayerRespawn()
    {
        _alreadyHandledThisDeath = false;
    }

    private static string GetActiveSceneName()
    {
        try
        {
            var fragMng = NoREroMod.Systems.Cache.UnifiedGameControllerCacheManager.GetGameFragMng();
            if (fragMng != null && !string.IsNullOrEmpty(fragMng._re_Scenename))
                return fragMng._re_Scenename;
        }
        catch { }

        try { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; }
        catch { return string.Empty; }
    }
}

/// <summary>
/// When vanilla calls <see cref="PlayerStatus.REstrat"/> (the respawn entry, note the
/// vanilla typo), re-arm the gold-death guard so the next death is processed.
/// </summary>
[HarmonyPatch(typeof(PlayerStatus), "REstrat")]
internal static class PlayerRespawnGoldArmPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        try { PlayerDeathGoldDropPatch.NotifyPlayerRespawn(); } catch { }
    }
}
