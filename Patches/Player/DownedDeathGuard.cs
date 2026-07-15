using NoREroMod.Patches.HellTraps;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.EventCore.Core;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Converts a "downed at 0 HP" state into a clean death (SpDeath -> game over).
///
/// Vanilla <c>playercon.fun_nowdamage</c> lets the player rise from a knockdown
/// (<c>erodown != 0</c>) when <see cref="PlayerStatus._SOUSA"/> is true and struggle SP is
/// full — it checks neither HP nor <c>_Death</c>. A lethal combat hit clears <c>_SOUSA</c>
/// and sets <c>_Death</c>, so that path is safe. But when HP reaches 0 through a non-combat
/// drain (no lethal <c>fun_damage</c> block runs), <c>_Death</c> stays false and
/// <c>_SOUSA</c> stays true, so the player can fill SP and "stand up" with no control.
///
/// This mirrors the H-scene path (<c>IncreaseStatusAndDamageOnEro -> SpDeath</c> at HP 0):
/// as soon as the player is downed at/below 0 HP without a registered death, force SpDeath.
/// </summary>
internal static class DownedDeathGuard
{
    internal static void Process(playercon player, PlayerStatus status)
    {
        if (player == null || status == null)
            return;

        // Only act on a knockdown that the game has not already killed, at/below 0 HP.
        if (player.erodown == 0 || player._Death || status.Hp > 0f)
            return;

        // Do not interrupt bespoke death / bad-end / paused sequences that drive HP themselves.
        if (EventCorePause.IsFrozen)
            return;
        if (MindBrokenBadEndSystem.IsBadEndActive)
            return;
        if (LethalTrapDeathCleanup.ShouldCleanupOnRespawn())
            return;

        player.SpDeath();
    }
}
