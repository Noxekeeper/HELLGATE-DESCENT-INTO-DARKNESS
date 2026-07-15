using UnityEngine;

namespace NoREroMod.Systems.Spawn;

/// <summary>Marks HellGate-managed spawns for altar hot-reload cleanup.</summary>
internal sealed class SpawnManagedInstance : MonoBehaviour
{
    internal bool SpawnHostileToPlayer;
    internal bool SuppressFactionMarker;
}
