using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Keeps lightning trap roots registered for hit / proximity detection.</summary>
internal sealed class HellGateLethalLightningTrapTracker : MonoBehaviour
{
    private void OnEnable()
    {
        LethalLightningTrapRegistry.Register(gameObject);
    }

    private void OnDisable()
    {
        LethalLightningTrapRegistry.Unregister(gameObject);
    }

    private void OnDestroy()
    {
        LethalLightningTrapRegistry.Unregister(gameObject);
    }
}
