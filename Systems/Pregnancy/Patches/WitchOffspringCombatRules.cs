using NoREroMod.Systems.CombatAi.Factions;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.Patches;

/// <summary>Shared rules for offspring friendly-fire and mother protection.</summary>
internal static class WitchOffspringCombatRules
{
    internal static bool IsOffspring(GameObject obj)
    {
        if (obj == null)
            return false;

        return obj.GetComponentInParent<WitchOffspringController>() != null;
    }

    internal static bool IsOffspring(EnemyDate enemy)
    {
        return enemy != null && enemy.gameObject != null && IsOffspring(enemy.gameObject);
    }

    internal static bool ShouldBlockOffspringVsPlayer(EnemyDate attacker)
    {
        if (!PregnancyConfig.IsEnabled)
            return false;
        if (PregnancyConfig.PreventOffspringDamageToPlayer == null || !PregnancyConfig.PreventOffspringDamageToPlayer.Value)
            return false;

        return IsOffspring(attacker);
    }

    internal static bool ShouldBlockPlayerVsOffspring(EnemyDate victim)
    {
        if (!PregnancyConfig.IsEnabled)
            return false;
        if (PregnancyConfig.PreventPlayerDamageToOffspring == null || !PregnancyConfig.PreventPlayerDamageToOffspring.Value)
            return false;

        return IsOffspring(victim);
    }

    internal static bool ShouldBlockWitchFactionFriendlyFire(int leftFaction, int rightFaction)
    {
        if (!PregnancyConfig.IsEnabled)
            return true;
        if (PregnancyConfig.PreventOffspringFactionFriendlyFire == null || PregnancyConfig.PreventOffspringFactionFriendlyFire.Value)
            return leftFaction == FactionIds.Witch && rightFaction == FactionIds.Witch;

        return false;
    }
}
