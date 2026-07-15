using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Identifies a lethal trap projectile and clears stale arm state on despawn.</summary>
internal sealed class LethalMagicTrapBulletMarker : MonoBehaviour
{
    private bool _destroyNotified;

    private void OnDestroy()
    {
        if (_destroyNotified)
            return;

        _destroyNotified = true;
        LethalMagicTrapRuntime.NotifyLethalBulletDestroyed(transform.root.gameObject);
    }
}
