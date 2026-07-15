using System.Reflection;
using HarmonyLib;

namespace NoREroMod.Patches.Player;

[HarmonyPatch(typeof(StruggleSystem), nameof(StruggleSystem.startGrabInvul))]
internal static class StruggleInvulnPatch
{
    private const float AdditionalInvulSeconds = 2f;
    private static readonly FieldInfo EliteGrabInvulField = AccessTools.Field(typeof(StruggleSystem), "eliteGrabInvulTimer");

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
            // Ignore reset failures; invulnerability extension must still apply.
        }
    }

    [HarmonyPostfix]
    private static void ExtendInvulnerability()
    {
        if (EliteGrabInvulField == null)
        {
            return;
        }

        float current = (float)EliteGrabInvulField.GetValue(null);
        float updated = current + AdditionalInvulSeconds;
        EliteGrabInvulField.SetValue(null, updated);

    }
}

