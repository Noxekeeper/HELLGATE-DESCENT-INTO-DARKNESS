using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions;

/// <summary>
/// Runtime tag attached to enemy projectiles (Arrow/fallBullet) to remember which
/// EnemyDate fired them. Used only by the Factions module to apply faction damage
/// when an enemy projectile hits another enemy's hurtbox (which vanilla ignores).
/// </summary>
internal class FactionProjectileOwner : MonoBehaviour
{
    public int AttackerInstanceId;
    public float SpawnTime;
}
