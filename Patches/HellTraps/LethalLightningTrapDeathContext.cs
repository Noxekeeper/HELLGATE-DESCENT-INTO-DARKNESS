using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Session flags for lethal lightning button trap hit flow and custom death clip.</summary>
internal static class LethalLightningTrapDeathContext
{
    internal static bool IsLethalDamageInFlight { get; set; }

    internal static bool IsCustomDeathActive { get; private set; }

    private static bool _pendingCustomDeath;

    internal static bool HitDealtDamage { get; set; }

    internal static Vector3? TrapAnchorWorld { get; private set; }

    internal static bool IsEroSuppressionActive { get; private set; }

    internal static bool HasPending => _pendingCustomDeath;

    internal static bool IsLethalHitInProgress =>
        IsLethalDamageInFlight ||
        HasPending ||
        HitDealtDamage ||
        IsCustomDeathActive;

    internal static void SetTrapAnchorWorld(Vector3 worldPosition)
    {
        TrapAnchorWorld = worldPosition;
    }

    internal static void ClearTrapAnchorWorld()
    {
        TrapAnchorWorld = null;
    }

    internal static void MarkPending()
    {
        _pendingCustomDeath = true;
        EnableEroSuppression();
    }

    internal static void MarkHitDealtDamage()
    {
        HitDealtDamage = true;
    }

    internal static void ClearHitDealtDamage()
    {
        HitDealtDamage = false;
    }

    internal static void MarkCustomDeathActive()
    {
        IsCustomDeathActive = true;
        EnableEroSuppression();
    }

    internal static void ClearPending()
    {
        _pendingCustomDeath = false;
    }

    internal static void ClearCustomDeathActive()
    {
        IsCustomDeathActive = false;
        _pendingCustomDeath = false;
        HitDealtDamage = false;
        IsLethalDamageInFlight = false;
        DisableEroSuppression();
        ClearTrapAnchorWorld();
        LethalLightningTrapRuntime.ResetFinalizeGuard();
    }

    internal static void EnableEroSuppression()
    {
        if (IsEroSuppressionActive)
            return;

        IsEroSuppressionActive = true;
        LethalMagicTrapDeathAudio.OnSuppressionEnabled();
    }

    internal static void DisableEroSuppression()
    {
        IsEroSuppressionActive = false;
        LethalMagicTrapEroSuppression.OnEroSuppressionDisabled();
    }

    internal static void ClearStaleEroSuppression()
    {
        if (IsCustomDeathActive || IsLethalHitInProgress)
            return;

        DisableEroSuppression();
    }
}
