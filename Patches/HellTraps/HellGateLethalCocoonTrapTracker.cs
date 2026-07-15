using UnityEngine;

namespace NoREroMod.Patches.HellTraps;

/// <summary>Registers spawned lethal cocoon trap roots for reliable hit detection (marker alone is not enough on some prefab hierarchies).</summary>
internal sealed class HellGateLethalCocoonTrapTracker : MonoBehaviour
{
    private void OnEnable()
    {
        LethalCocoonTrapRegistry.Register(gameObject);
    }

    private void OnDisable()
    {
        LethalCocoonTrapRegistry.Unregister(gameObject);
    }

    private void OnDestroy()
    {
        LethalCocoonTrapRegistry.Unregister(gameObject);
    }
}
