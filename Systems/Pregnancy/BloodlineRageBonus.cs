using NoREroMod.Systems.Rage;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy;

/// <summary>
/// Passively adds Rage % every second based on the number of living hideout children.
/// The value is read from <see cref="OffspringBloodlineBonuses.RagePerSecond"/> and is already capped.
/// </summary>
internal static class BloodlineRageBonus
{
    public static void Process(float deltaTime)
    {
        if (!PregnancyConfig.IsEnabled)
            return;

        float ragePerSecond = OffspringBloodlineBonuses.RagePerSecond;
        if (ragePerSecond <= 0f)
            return;

        RageSystem.AddRage(ragePerSecond * deltaTime, "bloodline_offspring");
    }
}
