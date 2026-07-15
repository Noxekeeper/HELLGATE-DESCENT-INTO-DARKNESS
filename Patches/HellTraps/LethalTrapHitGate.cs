namespace NoREroMod.Patches.HellTraps;

/// <summary>Prevents lethal_magictrap and lethal_cocoontrap fun_damage hooks from fighting each other.</summary>
internal static class LethalTrapHitGate
{
    internal static bool IsCocoonLethalHitActive()
    {
        if (!Plugin.enableLethalCocoonTrap.Value)
            return false;

        return LethalCocoonTrapDeathContext.IsLethalDamageInFlight ||
               LethalCocoonTrapDeathContext.HasPending ||
               LethalCocoonTrapDeathContext.HitDealtDamage ||
               LethalCocoonTrapDeathContext.IsCustomDeathActive ||
               LethalCocoonTrapDeathContext.IsEroSuppressionActive;
    }

    internal static bool IsMagicLethalHitActive()
    {
        if (!Plugin.enableLethalMagicTrap.Value)
            return false;

        return LethalMagicTrapDeathContext.IsLethalDamageInFlight ||
               LethalMagicTrapDeathContext.HasPending ||
               LethalMagicTrapDeathContext.BulletHitDealtDamage ||
               LethalMagicTrapDeathContext.IsLethalTrapDamageArmed ||
               LethalMagicTrapDeathContext.IsCustomDeathActive ||
               LethalMagicTrapDeathContext.IsEroSuppressionActive;
    }
}
