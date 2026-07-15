using HarmonyLib;
using NoREroMod;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.EventCore.Host;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Runs after all type-specific H-scene abort postfixes on <see cref="StruggleSystem.startGrabInvul"/>.
/// </summary>
[HarmonyPatch(typeof(StruggleSystem), nameof(StruggleSystem.startGrabInvul))]
internal static class StruggleEscapeCombatRecoveryPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void RestoreCombatAfterAllEscapeCleanup()
    {
        playercon player = UnifiedPlayerCacheManager.GetPlayer();
        if (player == null)
            return;

        // H-scene escape only — not combat knockdown and not pregnancy birth overlay.
        if (!player.eroflag || PlayerEroContextUtility.ShouldPreserveBadstatusBirthVisuals(player))
            return;

        EventCoreHost.NotifyPlayerFreedFromHScene();
        PlayerCombatControlRecovery.RestoreAfterStruggleEscape();
    }
}
