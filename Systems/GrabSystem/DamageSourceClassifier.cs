namespace NoREroMod.Systems.GrabSystem;

/// <summary>
/// Classifies damage source as melee vs ranged and identifies knockdown attacks.
/// See EnemyAttacksReference.md for the full attack list.
/// </summary>
internal static class DamageSourceClassifier
{
    /// <summary>Returns true for knockdown/grab attacks (kickbackkind 3, 4, or 6).</summary>
    internal static bool IsPowerAttack(int kickbackkind)
    {
        return kickbackkind == 3 || kickbackkind == 4 || kickbackkind == 6;
    }

    /// <summary>Returns true if the current damage source is ranged (arrows, magic, projectiles).</summary>
    internal static bool IsRanged => GrabViaAttackContext.LastDamageWasRanged;
}
