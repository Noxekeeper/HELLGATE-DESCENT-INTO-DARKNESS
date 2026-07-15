using HarmonyLib;

namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// Mirrors vanilla altar healing/status reset for the extended pregnancy module.
/// Vanilla <see cref="Savepoint_on.fun_ALLreset"/> calls <see cref="PlayerStatus.BADstatusReset"/>,
/// but <see cref="TrimesterProgression"/> restores <c>Buff._Pregnancy</c> while HellGate gestation remains active.
/// </summary>
internal static class PregnancyAltarCleanup
{
    internal static void ApplyAfterAltarReset()
    {
        if (!PregnancyConfig.IsEnabled)
            return;

        bool resetWomb = PregnancyConfig.AltarResetWombMeter == null || PregnancyConfig.AltarResetWombMeter.Value;
        bool resetGestation = PregnancyConfig.AltarResetActivePregnancy == null || PregnancyConfig.AltarResetActivePregnancy.Value;

        if (!resetWomb && !resetGestation)
            return;

        if (resetWomb)
            PregnancyRuntimeCleanup.ClearWombMeter("altar");

        if (resetGestation)
            PregnancyRuntimeCleanup.ClearGestation("altar");
    }
}

[HarmonyPatch(typeof(Savepoint_on), "fun_ALLreset")]
internal static class PregnancyAltarCleanupPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        PregnancyAltarCleanup.ApplyAfterAltarReset();
    }
}
