namespace NoREroMod.Patches.HellTraps;

/// <summary>
/// Set when the player dies via lethal magic/cocoon trap; consumed on Take Vengeance respawn
/// to run the Mind Broken shock sequence.
/// </summary>
internal static class LethalTrapVengeanceShockSession
{
    private static bool _pendingAfterLethalDeath;

    internal static bool HasPending => _pendingAfterLethalDeath;

    internal static void MarkLethalTrapDeath()
    {
        _pendingAfterLethalDeath = true;
    }

    internal static void ClearPending()
    {
        _pendingAfterLethalDeath = false;
    }

    /// <summary>Returns true if a pending flag was consumed.</summary>
    internal static bool TryConsumePending()
    {
        if (!_pendingAfterLethalDeath)
            return false;

        _pendingAfterLethalDeath = false;
        return true;
    }
}
