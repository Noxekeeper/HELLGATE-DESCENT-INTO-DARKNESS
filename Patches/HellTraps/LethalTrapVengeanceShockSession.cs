namespace NoREroMod.Patches.HellTraps;

/// <summary>
/// Set when the player dies via lethal magic/cocoon trap, combat EnemyFatality,
/// or vanilla H-scene fatality (SpDeath while a *Fatality / Ftality driver is active);
/// consumed on Take Vengeance to run Mind Broken shock + Fear trauma sequences.
/// </summary>
internal static class LethalTrapVengeanceShockSession
{
    private static bool _pendingAfterTraumaDeath;

    internal static bool HasPending => _pendingAfterTraumaDeath;

    internal static void MarkLethalTrapDeath()
    {
        _pendingAfterTraumaDeath = true;
    }

    /// <summary>Same latch as lethal traps — fatality Take Vengeance reuses the shock FX.</summary>
    internal static void MarkFatalityDeath()
    {
        _pendingAfterTraumaDeath = true;
    }

    internal static void ClearPending()
    {
        _pendingAfterTraumaDeath = false;
    }

    /// <summary>Returns true if a pending flag was consumed.</summary>
    internal static bool TryConsumePending()
    {
        if (!_pendingAfterTraumaDeath)
            return false;

        _pendingAfterTraumaDeath = false;
        return true;
    }
}
