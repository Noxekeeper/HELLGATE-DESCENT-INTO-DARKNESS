using NoREroMod.Systems.Gameplay;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions.Patches;

/// <summary>
/// Bridges H-scene lifecycle (player eroflag/erodown) to faction reputation.
/// On H-scene end (handoff chain only), applies HSceneCompletedReputationDelta to the taker faction.
/// </summary>
internal static class FactionHSceneReputationBridge
{
    private static bool _wasHSceneActive;
    private static int _lastActiveFactionId = FactionIds.Neutral;

    internal static void Process(playercon player)
    {
        if (player == null || !EnemyFactionsConfig.Enable)
            return;

        bool isHSceneActive = player.eroflag && player.erodown != 0;

        if (isHSceneActive)
        {
            int currentFaction = TryResolveCurrentHSceneFaction();
            if (!FactionIds.IsPassiveNonCombat(currentFaction))
                _lastActiveFactionId = currentFaction;
        }

        if (_wasHSceneActive && !isHSceneActive)
        {
            // Reward only for real handoff/gangbang cycles.
            // Plain grab escape (no handoff marker) must not grant H-scene relation gain.
            if (!FactionReputationDynamics.HasPendingHandoffBonus())
            {
                if (EnemyFactionsConfig.DebugLogging)
                    Plugin.Log?.LogInfo("[Reputation] H-scene end without handoff cycle -> no faction reward");
                _lastActiveFactionId = FactionIds.Neutral;
                _wasHSceneActive = isHSceneActive;
                return;
            }

            int factionToReward = TryResolveCurrentHSceneFaction();
            if (FactionIds.IsPassiveNonCombat(factionToReward))
                factionToReward = _lastActiveFactionId;

            if (!FactionIds.IsPassiveNonCombat(factionToReward))
            {
                PlayerFactionReputation.NotifyCompletedHSceneWithFaction(factionToReward);
                if (FactionReputationDynamics.TryConsumePendingHandoffBonus(out float handoffBonus))
                {
                    PlayerFactionReputation.ModifyScore(factionToReward, handoffBonus);
                    if (EnemyFactionsConfig.DebugLogging)
                    {
                        Plugin.Log?.LogInfo("[Reputation] handoff chain bonus applied to taker faction=" + factionToReward + " delta=" + handoffBonus.ToString("0.##"));
                    }
                }
                if (EnemyFactionsConfig.DebugLogging)
                {
                    Plugin.Log?.LogInfo("[Reputation] H-scene completed with faction=" + factionToReward +
                        " -> +" + EnemyFactionsConfig.HSceneCompletedReputationDelta.ToString("0.##"));
                }
            }

            _lastActiveFactionId = FactionIds.Neutral;
        }

        _wasHSceneActive = isHSceneActive;
    }

    private static int TryResolveCurrentHSceneFaction()
    {
        object enemyInstance = QTESystem.GetCurrentEnemyInstance();
        if (enemyInstance == null)
            return FactionIds.Neutral;

        GameObject enemyObject = ExtractGameObject(enemyInstance);
        if (enemyObject == null)
            return FactionIds.Neutral;

        return EnemyFactionRuntime.GetFaction(enemyObject);
    }

    private static GameObject ExtractGameObject(object instance)
    {
        if (instance is GameObject gameObject)
            return gameObject;

        if (instance is Component component)
            return component.gameObject;

        return null;
    }
}
