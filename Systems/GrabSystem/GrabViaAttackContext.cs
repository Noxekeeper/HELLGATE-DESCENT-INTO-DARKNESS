namespace NoREroMod.Systems.GrabSystem;

/// <summary>
/// Per-hit context for the GrabViaAttack system.
/// Tracks the attacking enemy and whether the damage source is ranged.
/// Set by MeleeAttackerContextPatches / RangedDamageFlagPatches, consumed by GrabViaAttackPatch / Fear.
/// </summary>
internal static class GrabViaAttackContext
{
    [System.ThreadStatic]
    private static bool _lastDamageWasRanged;

    [System.ThreadStatic]
    private static EnemyDate _currentAttacker;

    [System.ThreadStatic]
    private static EnemyDate _lastRangedAttacker;

    internal static bool LastDamageWasRanged => _lastDamageWasRanged;

    /// <summary>Enemy that fired the current/last ranged hit when attribution succeeded.</summary>
    internal static EnemyDate LastRangedAttacker => _lastRangedAttacker;

    /// <summary>Flags the next fun_damage call as ranged. Called from RangedDamageFlagPatches.</summary>
    internal static void MarkNextDamageAsRanged()
    {
        MarkNextDamageAsRanged(null);
    }

    /// <summary>Flags the next fun_damage as ranged and optionally records the projectile owner.</summary>
    internal static void MarkNextDamageAsRanged(EnemyDate rangedAttacker)
    {
        _lastDamageWasRanged = true;
        if (rangedAttacker != null)
            _lastRangedAttacker = rangedAttacker;
    }

    /// <summary>The enemy that initiated the current melee hit. null for ranged or unknown sources.</summary>
    internal static EnemyDate CurrentAttacker
    {
        get => _currentAttacker;
        set => _currentAttacker = value;
    }

    /// <summary>
    /// Best available attacker for the current hit: melee CurrentAttacker, else last ranged owner.
    /// </summary>
    internal static EnemyDate ResolveAttackerForFear()
    {
        if (_currentAttacker != null)
            return _currentAttacker;
        return _lastRangedAttacker;
    }

    /// <summary>Resets all flags after processing. Called from GrabViaAttackPatch on every exit path.</summary>
    internal static void Reset()
    {
        _lastDamageWasRanged = false;
        _currentAttacker = null;
        // Keep LastRangedAttacker until the next ranged mark so Fear postfix can still read it
        // if GrabViaAttackPatch resets before FearCombatHooks postfix runs.
    }

    /// <summary>Clear ranged attribution after Fear (and grab) have consumed the hit.</summary>
    internal static void ClearRangedAttacker()
    {
        _lastRangedAttacker = null;
    }
}
