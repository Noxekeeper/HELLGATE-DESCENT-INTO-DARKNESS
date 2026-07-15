using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Session flags for lethal cocoon trap hit flow and custom death clip.</summary>
internal static class LethalCocoonTrapDeathContext
{
    internal static bool IsLethalDamageInFlight { get; set; }

    internal static bool IsCustomDeathActive { get; private set; }

    private static bool _pendingCustomDeath;

    internal static bool HitDealtDamage { get; set; }

    /// <summary>World position of the lethal cocoon trap that dealt damage (spawn / trap root).</summary>
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

    internal static bool ShouldTreatAsLethalTrapHit(float getatk)
    {
        return HasPending ||
               HitDealtDamage ||
               IsLethalDamageInFlight ||
               getatk >= LethalCocoonTrapRuntime.GetLethalAtk() * 0.5f;
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
        LethalCocoonTrapRuntime.ResetFinalizeGuard();
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
    }

    /// <summary>Release trap ERO suppression if the player escaped without finishing custom death.</summary>
    internal static void ClearStaleEroSuppression()
    {
        if (IsCustomDeathActive || IsLethalHitInProgress)
            return;

        DisableEroSuppression();
    }
}
