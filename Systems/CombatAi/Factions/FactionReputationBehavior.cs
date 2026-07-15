using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions;

/// <summary>
/// Bridge between <see cref="PlayerFactionReputation"/> (numeric score) and the AI
/// patches (<c>EnemyDateFactionTargetingPatches</c>, provocation, etc).
///
/// <para>
/// Vanilla gameplay code never saw the reputation score. Without this bridge, a
/// faction whose score dropped to -100% would still be ignored by the player until
/// the player physically hit someone — because only the short-lived "provoked"
/// timer was checked.
/// </para>
///
/// <para>
/// This helper maps score → discrete <see cref="Level"/> and exposes a handful of
/// queries used by the AI patches. All thresholds and behavior toggles live in
/// <see cref="EnemyFactionsConfig"/>, so tuning is JSON-only and hot-reloaded.
/// </para>
/// </summary>
internal static class FactionReputationBehavior
{
    internal enum Level
    {
        Hostile,
        Neutral,
        Friendly,
    }

    /// <summary>
    /// Reputation score at or above which sign-based aggro treats the faction as peaceful toward the player.
    /// Defaults to <see cref="EnemyFactionsConfig.ReputationFriendlyThreshold"/> (+65).
    /// </summary>
    public static float GetPeaceReputationThreshold()
    {
        if (!EnemyFactionsConfig.EnableSignBasedPlayerAggro)
            return EnemyFactionsConfig.PlayerAggroPeaceThreshold;

        float friendly = EnemyFactionsConfig.ReputationFriendlyThreshold;
        float configured = EnemyFactionsConfig.PlayerAggroPeaceThreshold;
        return configured > friendly ? configured : friendly;
    }

    /// <summary>
    /// True when the player's reputation is below the peace gate — faction treats the player as hostile.
    /// Covers -100..+64 (vision/speed still scale across the full range).
    /// </summary>
    public static bool IsPlayerHostileReputation(int factionId)
    {
        if (FactionIds.IsPassiveNonCombat(factionId) || FactionIds.IsPlayerNativeFaction(factionId))
            return false;
        return PlayerFactionReputation.GetScore(factionId) < GetPeaceReputationThreshold();
    }

    /// <summary>
    /// Legacy JSON key; player hostility is now derived from <see cref="GetPeaceReputationThreshold"/>.
    /// </summary>
    public static float GetHostileReputationThreshold()
    {
        return EnemyFactionsConfig.ReputationHostileThreshold;
    }

    /// <summary>
    /// Discrete behavior bucket derived from the current reputation score.
    /// Neutral faction (id 0) always maps to <see cref="Level.Neutral"/>.
    /// Below the peace gate there is no middle band — only Hostile vs Friendly.
    /// </summary>
    public static Level GetLevel(int factionId)
    {
        if (FactionIds.IsPassiveNonCombat(factionId))
            return Level.Neutral;
        if (FactionIds.IsPlayerNativeFaction(factionId))
            return Level.Friendly;

        if (IsPlayerHostileReputation(factionId))
            return Level.Hostile;
        return Level.Friendly;
    }

    public static Level GetLevelFor(GameObject enemyObject)
    {
        if (enemyObject == null)
            return Level.Neutral;
        int faction = EnemyFactionRuntime.GetFaction(enemyObject);
        return GetLevel(faction);
    }

    /// <summary>
    /// True if the player's reputation with this enemy's faction is deep enough to
    /// override the usual "wait until hit" logic and treat them as aggressive now.
    /// Requires both <see cref="EnemyFactionsConfig.HostileAutoProvokeInRadius"/>
    /// and <see cref="EnemyFactionsConfig.Enable"/>.
    /// </summary>
    public static bool ShouldAutoProvoke(GameObject enemyObject)
    {
        if (!EnemyFactionsConfig.Enable || !EnemyFactionsConfig.HostileAutoProvokeInRadius)
            return false;
        if (enemyObject != null && EnemyFactionRuntime.IsBossEnemy(enemyObject))
            return false;
        if (EnemyFactionsConfig.EnableSignBasedPlayerAggro)
        {
            int faction = enemyObject != null ? EnemyFactionRuntime.GetFaction(enemyObject) : FactionIds.Neutral;
            return IsPlayerHostileReputation(faction);
        }
        return GetLevelFor(enemyObject) == Level.Hostile;
    }

    /// <summary>
    /// Bandits only ignore the player when reputation is at or above the peace gate (+65 by default).
    /// Below that they fight; provoked bandits always fight regardless of score.
    /// </summary>
    public static bool ShouldBanditsIgnorePlayer(GameObject enemyObject)
    {
        if (!EnemyFactionsConfig.Enable || !EnemyFactionsConfig.BanditsIgnorePlayer)
            return false;
        if (enemyObject == null || EnemyFactionRuntime.IsBossEnemy(enemyObject))
            return false;
        if (!EnemyFactionRuntime.IsBanditFamily(enemyObject))
            return false;
        if (EnemyFactionRuntime.IsHostileToPlayer(enemyObject))
            return false;
        if (ShouldBreakBanditIgnore(enemyObject))
            return false;

        int faction = EnemyFactionRuntime.GetFaction(enemyObject);
        if (FactionIds.IsPassiveNonCombat(faction))
            return false;

        return PlayerFactionReputation.GetScore(faction) >= GetPeaceReputationThreshold();
    }

    /// <summary>
    /// True if a bandit-family unit should stop being invisible to the player because
    /// their faction reputation crossed the hostile threshold.
    /// </summary>
    public static bool ShouldBreakBanditIgnore(GameObject enemyObject)
    {
        if (!EnemyFactionsConfig.Enable || !EnemyFactionsConfig.HostileBreaksBanditIgnore)
            return false;
        if (enemyObject != null && EnemyFactionRuntime.IsBossEnemy(enemyObject))
            return true;
        if (!EnemyFactionRuntime.IsBanditFamily(enemyObject))
            return false;
        if (EnemyFactionsConfig.EnableSignBasedPlayerAggro)
        {
            int faction = enemyObject != null ? EnemyFactionRuntime.GetFaction(enemyObject) : FactionIds.Neutral;
            return IsPlayerHostileReputation(faction);
        }
        return GetLevelFor(enemyObject) == Level.Hostile;
    }

    /// <summary>
    /// True if a faction that would normally attack the player (Church, Demons, etc.)
    /// should act passively because the player earned their favor.
    /// </summary>
    public static bool ShouldSuppressVanillaAggro(GameObject enemyObject)
    {
        if (!EnemyFactionsConfig.Enable || !EnemyFactionsConfig.FriendlyDisablesVanillaAggro)
            return false;
        if (enemyObject != null && EnemyFactionRuntime.IsBossEnemy(enemyObject))
            return false;
        // Temporary provocation must win over "peaceful" sign-based passivity so the
        // enemy can retaliate for PlayerProvocationDurationSeconds even if score stays high.
        if (enemyObject != null && EnemyFactionRuntime.IsHostileToPlayer(enemyObject))
            return false;
        if (EnemyFactionsConfig.EnableSignBasedPlayerAggro)
        {
            int faction = enemyObject != null ? EnemyFactionRuntime.GetFaction(enemyObject) : FactionIds.Neutral;
            if (FactionIds.IsPassiveNonCombat(faction))
                return false;
            return PlayerFactionReputation.GetScore(faction) >= GetPeaceReputationThreshold();
        }
        return GetLevelFor(enemyObject) == Level.Friendly;
    }

    /// <summary>
    /// True if this faction should ignore the provocation hostile timer (reputation hit
    /// from the strike still applies). Uses <see cref="EnemyFactionsConfig.ProvocationIgnoredReputationThreshold"/>:
    /// at or above that score (default 96) there is no temporary aggro; 95 and below react.
    /// </summary>
    public static bool ShouldBlockProvocation(GameObject enemyObject)
    {
        if (!EnemyFactionsConfig.Enable || !EnemyFactionsConfig.FriendlyBlocksProvocation)
            return false;
        int faction = enemyObject != null ? EnemyFactionRuntime.GetFaction(enemyObject) : FactionIds.Neutral;
        if (FactionIds.IsPassiveNonCombat(faction))
            return false;
        float score = PlayerFactionReputation.GetScore(faction);
        return score >= EnemyFactionsConfig.ProvocationIgnoredReputationThreshold;
    }
}
