using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>
/// Marks hostages placed by HellGate spawn lines (including regular template fallback,
/// <c>HOSTAGE</c>, and <c>RANDOM_HOSTAGE</c>; not vanilla map objects).
/// Used to override rescue rewards without touching original scene instances.
/// </summary>
internal sealed class HellGateSpawnedHostageMarker : MonoBehaviour
{
    [SerializeField] internal int MinGold = 30;
    [SerializeField] internal int MaxGold = 60;

    internal static bool TryGet(GameObject root, out HellGateSpawnedHostageMarker marker)
    {
        marker = null;
        if (root == null)
            return false;

        marker = root.GetComponent<HellGateSpawnedHostageMarker>();
        if (marker != null)
            return true;

        marker = root.GetComponentInParent<HellGateSpawnedHostageMarker>();
        return marker != null;
    }
}
