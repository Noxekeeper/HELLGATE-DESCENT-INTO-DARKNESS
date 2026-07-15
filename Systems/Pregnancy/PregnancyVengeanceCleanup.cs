namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// Clears extended pregnancy runtime state when the player takes vengeance after death.
/// Hideout offspring JSON is untouched; cleared state is persisted only on the next slot save.
/// </summary>
internal static class PregnancyVengeanceCleanup
{
    internal static void ClearOnTakeVengeance()
    {
        if (!PregnancyConfig.IsEnabled)
            return;

        bool hadState = WitchPregnancyState.IsActive || WitchPregnancyState.HasPending || WitchWombMeter.TotalMl > 0f;
        if (!hadState)
            return;

        PregnancyRuntimeCleanup.ClearGestation("take_vengeance");
        PregnancyRuntimeCleanup.ClearWombMeter("take_vengeance");
    }
}
