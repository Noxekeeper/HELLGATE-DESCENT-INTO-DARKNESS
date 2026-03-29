using System.Reflection;
using HarmonyLib;

namespace NoREroMod.Patches.Player;

[HarmonyPatch(typeof(StruggleSystem), nameof(StruggleSystem.startGrabInvul))]
internal static class StruggleInvulnPatch
{
    private const float AdditionalInvulSeconds = 2f;
    private static readonly FieldInfo EliteGrabInvulField = AccessTools.Field(typeof(StruggleSystem), "eliteGrabInvulTimer");

    /// <summary>
    /// On побеге from H-scene сбрасываем глобальное state handoff.
    /// Иначе следующий enemy (даже другого типа) ошибочbut начинает with середины animation.
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
            // Игнорируем ошибки on сбросе
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

