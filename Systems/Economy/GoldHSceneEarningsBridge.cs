using System;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Gameplay;
using UnityEngine;

namespace NoREroMod.Systems.Economy;

/// <summary>
/// Mirrors <see cref="NoREroMod.Systems.CombatAi.Factions.Patches.FactionHSceneReputationBridge"/>
/// for the Gold module: detects the H-scene end transition (player <c>eroflag</c> →
/// <c>false</c>) and pays the player based on the current faction's
/// <see cref="EconomicHSceneSettings.PerFaction"/> rule.
///
/// Driven from <c>PlayerConUpdateDispatcher.Dispatch</c> — one extra try/catch block,
/// no separate Harmony postfix (project's perf discipline).
/// </summary>
internal static class GoldHSceneEarningsBridge
{
    private static bool _wasHSceneActive;
    private static int _lastActiveFactionId = FactionIds.Neutral;

    public static void Process(playercon player)
    {
        if (player == null) return;
        if (!EconomicConfig.Enable) return;
        if (!EconomicConfig.HSceneEarnings.Enable) return;

        bool isHSceneActive = player.eroflag && player.erodown != 0;

        if (isHSceneActive)
        {
            int currentFaction = TryResolveCurrentHSceneFaction();
            if (currentFaction != FactionIds.Neutral)
                _lastActiveFactionId = currentFaction;
        }

        if (_wasHSceneActive && !isHSceneActive)
        {
            // Same gating as faction reputation: only pay on real handoff/gangbang cycles.
            if (!FactionReputationDynamics.HasPendingHandoffBonus())
            {
                _lastActiveFactionId = FactionIds.Neutral;
                _wasHSceneActive = isHSceneActive;
                return;
            }

            int factionId = TryResolveCurrentHSceneFaction();
            if (factionId == FactionIds.Neutral)
                factionId = _lastActiveFactionId;

            if (factionId != FactionIds.Neutral)
                Award(factionId);

            _lastActiveFactionId = FactionIds.Neutral;
        }

        _wasHSceneActive = isHSceneActive;
    }

    private static void Award(int factionId)
    {
        EconomicHSceneFactionRule[] rules = EconomicConfig.HSceneEarnings.PerFaction;
        if (rules == null) return;

        string key = EconomicFactionUtil.FactionIdToKey(factionId);
        EconomicHSceneFactionRule rule = null;
        for (int i = 0; i < rules.Length; i++)
        {
            EconomicHSceneFactionRule r = rules[i];
            if (r == null || string.IsNullOrEmpty(r.Faction)) continue;
            if (string.Equals(r.Faction, key, StringComparison.OrdinalIgnoreCase))
            {
                rule = r;
                break;
            }
        }
        if (rule == null || rule.MaxAmount <= 0) return;

        int min = Mathf.Max(0, rule.MinAmount);
        int max = Mathf.Max(min, rule.MaxAmount);
        int amount = min == max ? min : UnityEngine.Random.Range(min, max + 1);
        if (amount <= 0) return;

        try { GoldWallet.ModifyGold(amount); } catch { }
        try { if (EconomicConfig.Popup.Enable) GoldPopupSystem.ShowOverPlayer(amount); } catch { }
        if (EconomicConfig.Audio.Enable && GoldAssetLoader.HasPickupClip)
        {
            GoldAudioPlayer.Play2D(GoldAssetLoader.PickupClips[0], EconomicConfig.Audio.PickupVolume);
        }

        if (EconomicConfig.DebugLogging)
            Plugin.Log?.LogInfo($"[GoldHScene] Awarded {amount} for faction={factionId}");
    }

    private static int TryResolveCurrentHSceneFaction()
    {
        try
        {
            object enemyInstance = QTESystem.GetCurrentEnemyInstance();
            if (enemyInstance == null) return FactionIds.Neutral;

            GameObject obj = enemyInstance as GameObject;
            if (obj == null && enemyInstance is Component comp) obj = comp.gameObject;
            if (obj == null) return FactionIds.Neutral;

            return EnemyFactionRuntime.GetFaction(obj);
        }
        catch
        {
            return FactionIds.Neutral;
        }
    }

}
