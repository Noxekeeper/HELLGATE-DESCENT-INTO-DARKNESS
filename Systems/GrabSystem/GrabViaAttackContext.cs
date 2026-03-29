namespace NoREroMod.Systems.GrabSystem;

/// <summary>
/// Per-hit context for the GrabViaAttack system.
/// Tracks the attacking enemy and whether the damage source is ranged.
/// Set by MeleeAttackerContextPatches / RangedDamageFlagPatches, consumed by GrabViaAttackPatch.
/// </summary>
internal static class GrabViaAttackContext
{
    [System.ThreadStatic]
    private static bool _lastDamageWasRanged;

    [System.ThreadStatic]
    private static EnemyDate _currentAttacker;

    internal static bool LastDamageWasRanged => _lastDamageWasRanged;

    /// <summary>Flags the next fun_damage call as ranged. Called from RangedDamageFlagPatches.</summary>
    internal static void MarkNextDamageAsRanged()
    {
        _lastDamageWasRanged = true;
    }

    /// <summary>The enemy that initiated the current melee hit. null for ranged or unknown sources.</summary>
    internal static EnemyDate CurrentAttacker
    {
        get => _currentAttacker;
        set => _currentAttacker = value;
    }

    /// <summary>Resets all flags after processing. Called from GrabViaAttackPatch on every exit path.</summary>
    internal static void Reset()
    {
        _lastDamageWasRanged = false;
        _currentAttacker = null;
    }
}
