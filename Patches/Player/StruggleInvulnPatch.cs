using HarmonyLib;
using NoREroMod.Systems.GrabSystem;

namespace NoREroMod.Patches.Player;

/// <summary>
/// Escape path: <see cref="StruggleSystem.startGrabInvul"/> arms HellGate
/// <see cref="PostEscapeGrabImmunity"/> (default 3s unscaled) — same coverage style as Rage
/// (GrabViaAttack + CanEliteGrabPlayer + power-knockdown suppress).
/// </summary>
[HarmonyPatch(typeof(StruggleSystem), nameof(StruggleSystem.startGrabInvul))]
internal static class StruggleInvulnPatch
{
    /// <summary>
    /// On H-scene escape, reset global handoff state so the next enemy does not resume mid-animation.
    /// </summary>
    [HarmonyPostfix]
    private static void ResetHandoffStateOnEscape()
    {
        try
        {
            EnemyHandoffSystem.ResetAllData();
        }
        catch
        {
            // Ignore reset failures; immunity arm must still apply.
        }
    }

    [HarmonyPostfix]
    private static void ArmPostEscapeGrabImmunity()
    {
        PostEscapeGrabImmunity.ArmFromEscape();
    }
}
