namespace NoREroMod.Patches.HellTraps;

/// <summary>Prevents lethal trap fun_damage hooks from fighting each other.</summary>
internal static class LethalTrapHitGate
{
    internal static bool IsCocoonLethalHitActive()
    {
        if (!Plugin.IsLethalCocoonTrapActive)
            return false;

        return LethalCocoonTrapDeathContext.IsLethalDamageInFlight ||
               LethalCocoonTrapDeathContext.HasPending ||
               LethalCocoonTrapDeathContext.HitDealtDamage ||
               LethalCocoonTrapDeathContext.IsCustomDeathActive ||
               LethalCocoonTrapDeathContext.IsEroSuppressionActive;
    }

    internal static bool IsMagicLethalHitActive()
    {
        if (!Plugin.IsLethalMagicTrapActive)
            return false;

        return LethalMagicTrapDeathContext.IsLethalDamageInFlight ||
               LethalMagicTrapDeathContext.HasPending ||
               LethalMagicTrapDeathContext.BulletHitDealtDamage ||
               LethalMagicTrapDeathContext.IsLethalTrapDamageArmed ||
               LethalMagicTrapDeathContext.IsCustomDeathActive ||
               LethalMagicTrapDeathContext.IsEroSuppressionActive;
    }

    internal static bool IsLightningLethalHitActive()
    {
        if (!Plugin.IsLethalLightningTrapActive)
            return false;

        return LethalLightningTrapDeathContext.IsLethalDamageInFlight ||
               LethalLightningTrapDeathContext.HasPending ||
               LethalLightningTrapDeathContext.HitDealtDamage ||
               LethalLightningTrapDeathContext.IsCustomDeathActive ||
               LethalLightningTrapDeathContext.IsEroSuppressionActive;
    }
}
