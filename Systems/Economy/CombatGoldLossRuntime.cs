using HarmonyLib;
using UnityEngine;

namespace NoREroMod.Systems.Economy;

/// <summary>
/// Gold loss on player hits and knockdowns. Hit detection mirrors GrabViaAttack:
/// any <see cref="playercon.fun_damage"/> / fun_damage_Improvement that is not a dash-avoid,
/// including guard blocks and grab-via-attack intercepts.
/// </summary>
internal static class CombatGoldLossRuntime
{
    private static float _nextHitAllowedAt = -999f;
    private static float _nextKnockdownAllowedAt = -999f;
    private static int _prevErodown;

    /// <summary>Called when a melee/ranged hit reaches the player damage pipeline.</summary>
    public static void TryProcessPlayerHit(playercon player, bool wasDodged)
    {
        if (!EconomicConfig.Enable)
            return;

        EconomicCombatGoldLossSettings cfg = EconomicConfig.CombatGoldLoss;
        if (cfg == null || !cfg.Enable)
            return;

        if (player == null || wasDodged)
            return;

        if (IsDeathOrHScene(player))
            return;

        float now = Time.time;
        if (now < _nextHitAllowedAt)
            return;

        long wallet = GoldWallet.Current;
        if (wallet < Mathf.Max(0, cfg.MinWalletToDrop))
            return;

        float chance = Mathf.Clamp01(cfg.ChanceOnDamage);
        if (chance <= 0f || Random.value > chance)
            return;

        int minLoss = Mathf.Max(1, cfg.MinLossAmount);
        int maxLoss = Mathf.Max(minLoss, cfg.MaxLossAmount);
        long loss = Random.Range(minLoss, maxLoss + 1);
        if (loss <= 0)
            return;

        _nextHitAllowedAt = now + Mathf.Max(0f, cfg.CooldownSeconds);

        ApplyFixedLoss(player, loss, cfg.SpawnPickupPile, cfg.SpawnPickupPile, "CombatGoldLoss");
    }

    /// <summary>Edge-detect combat knockdown (erodown 0 → non-zero), not H-scene grab.</summary>
    public static void ProcessKnockdownEdge(playercon player, int erodown, bool eroflag)
    {
        bool edge = erodown != 0 && _prevErodown == 0;
        _prevErodown = erodown;
        if (erodown == 0)
            _prevErodown = 0;

        if (!edge || eroflag)
            return;

        TryProcessKnockdownLoss(player);
    }

    public static void ResetKnockdownTracking()
    {
        _prevErodown = 0;
    }

    private static void TryProcessKnockdownLoss(playercon player)
    {
        if (!EconomicConfig.Enable)
            return;

        EconomicKnockdownGoldLossSettings cfg = EconomicConfig.KnockdownGoldLoss;
        if (cfg == null || !cfg.Enable)
            return;

        if (player == null || IsDeathOrHScene(player))
            return;

        float now = Time.time;
        if (now < _nextKnockdownAllowedAt)
            return;

        long wallet = GoldWallet.Current;
        if (wallet <= 0)
            return;

        long loss = ComputePercentLoss(wallet, cfg.LossPercent, cfg.MinLossAmount);
        if (loss <= 0)
            return;

        _nextKnockdownAllowedAt = now + Mathf.Max(0f, cfg.CooldownSeconds);

        ApplyFixedLoss(player, loss, cfg.SpawnPickupPile, cfg.ShowPopup, "KnockdownGoldLoss");
    }

    internal static long ComputePercentLoss(long wallet, float percent, long minLossWhenZero)
    {
        if (wallet <= 0)
            return 0;

        float pct = Mathf.Clamp01(percent);
        long loss = (long)Mathf.Floor(wallet * pct);
        if (loss <= 0 && minLossWhenZero > 0 && wallet > 0)
            loss = minLossWhenZero;
        if (loss > wallet)
            loss = wallet;
        return loss;
    }

    private static void ApplyFixedLoss(playercon player, long loss, bool spawnPile, bool showPopup, string logTag)
    {
        if (loss <= 0)
            return;

        GoldWallet.ModifyGold(-loss);

        if (spawnPile && player != null && player.transform != null)
        {
            Vector2 pos = player.transform.position;
            pos.x += Random.Range(-0.35f, 0.35f);
            pos.y += EconomicConfig.DropSpawnYOffset;
            GoldDropAwarder.TrySpawnDrop(pos, loss);
        }

        if (showPopup && EconomicConfig.Popup.Enable)
            GoldPopupSystem.ShowOverPlayer(-loss);

        if (EconomicConfig.DebugLogging)
            Plugin.Log?.LogInfo($"[{logTag}] lost={loss} wallet={GoldWallet.Current}");
    }

    private static bool IsDeathOrHScene(playercon player)
    {
        if (player == null)
            return true;
        if (Traverse.Create(player).Field<bool>("Death").Value)
            return true;
        if (Traverse.Create(player).Field<bool>("eroflag").Value)
            return true;
        return false;
    }
}
