using System.Collections.Generic;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions;

/// <summary>
/// Runtime tag attached to enemy projectiles (Arrow/fallBullet/SlashDamage/…) to remember which
/// EnemyDate fired them. Used only by the Factions module to apply faction damage
/// when an enemy projectile hits another enemy's hurtbox (which vanilla ignores).
/// </summary>
internal class FactionProjectileOwner : MonoBehaviour
{
    public int AttackerInstanceId;
    public float SpawnTime;

    private HashSet<int> _hitTargetIds;

    /// <summary>Returns true the first time this target is marked (AOE / multi-path dedupe).</summary>
    public bool TryMarkHit(int targetInstanceId)
    {
        if (targetInstanceId == 0)
            return false;
        if (_hitTargetIds == null)
            _hitTargetIds = new HashSet<int>();
        return _hitTargetIds.Add(targetInstanceId);
    }
}
