namespace NoREroMod.Patches.HellTraps;

using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Session flags for lethal trap hit flow, custom death clip, and ERO suppression.</summary>
internal static class LethalMagicTrapDeathContext
{
    internal static bool IsLethalDamageInFlight { get; set; }

    internal static bool IsCustomDeathActive { get; private set; }

    private static bool _pendingCustomDeath;

    /// <summary>True after vanilla bullet path applied fun_damage for a lethal hit.</summary>
    internal static bool BulletHitDealtDamage { get; set; }

    /// <summary>True while at least one live lethal bullet expects the next player fun_damage.</summary>
    internal static bool IsLethalTrapDamageArmed { get; private set; }

    /// <summary>Floor spawn point of the lethal trap that fired (X,Y from spawn line).</summary>
    internal static Vector3? TrapFloorWorld { get; private set; }

    private static GameObject _queuedHitEffectPrefab;
    private static Quaternion _queuedHitEffectRotation = Quaternion.identity;

    internal static bool IsEroSuppressionActive { get; private set; }

    internal static bool HasPending => _pendingCustomDeath;

    internal static bool IsLethalHitInProgress =>
        IsLethalDamageInFlight ||
        HasPending ||
        BulletHitDealtDamage ||
        IsCustomDeathActive;

    internal static void SetTrapFloorWorld(Vector3 worldPosition)
    {
        TrapFloorWorld = worldPosition;
    }

    internal static void ClearTrapFloorWorld()
    {
        TrapFloorWorld = null;
    }

    internal static void QueueHitEffect(GameObject prefab, Quaternion rotation)
    {
        _queuedHitEffectPrefab = prefab;
        _queuedHitEffectRotation = rotation;
    }

    internal static void SpawnQueuedHitEffect(Vector3 worldPosition)
    {
        if (_queuedHitEffectPrefab == null)
            return;

        Object.Instantiate(_queuedHitEffectPrefab, worldPosition, _queuedHitEffectRotation);
        _queuedHitEffectPrefab = null;
    }

    internal static void ClearQueuedHitEffect()
    {
        _queuedHitEffectPrefab = null;
    }

    internal static void ArmLethalTrapPlayerHit()
    {
        IsLethalTrapDamageArmed = true;
    }

    internal static void ClearLethalTrapDamageArmed()
    {
        IsLethalTrapDamageArmed = false;
    }

    internal static bool ShouldTreatAsLethalTrapHit(float getatk)
    {
        return IsLethalTrapDamageArmed ||
               IsLethalDamageInFlight ||
               HasPending;
    }

    /// <summary>Clears magic-trap hit flags (e.g. when cocoon lethal takes over the same fun_damage).</summary>
    internal static void ClearMagicHitState()
    {
        ClearPending();
        ClearBulletHitDealtDamage();
        ClearLethalTrapDamageArmed();
        if (!IsCustomDeathActive)
            DisableEroSuppression();
    }

    internal static void MarkPending()
    {
        _pendingCustomDeath = true;
        EnableEroSuppression();
    }

    internal static void MarkBulletHitDealtDamage()
    {
        BulletHitDealtDamage = true;
    }

    internal static void ClearBulletHitDealtDamage()
    {
        BulletHitDealtDamage = false;
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
        DisableEroSuppression();
        ClearTrapFloorWorld();
        ClearQueuedHitEffect();
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

    /// <summary>Clears armed state when no live bullets remain and no hit is in progress.</summary>
    internal static void TryClearStaleArmState()
    {
        if (LethalMagicTrapRuntime.HasLiveLethalBullets())
            return;

        if (IsLethalHitInProgress)
            return;

        ClearLethalTrapDamageArmed();
    }
}
