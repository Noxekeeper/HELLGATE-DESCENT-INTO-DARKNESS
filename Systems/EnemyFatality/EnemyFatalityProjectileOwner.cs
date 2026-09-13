using UnityEngine;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>
/// Marks a magic projectile with its spawning <see cref="EnemyDate"/> so
/// HeavyCritical can arm on hit (projectiles call <c>fun_damage</c> directly,
/// not <c>EnemyDate.OndamageSend</c>).
/// </summary>
internal sealed class EnemyFatalityProjectileOwner : MonoBehaviour
{
    internal EnemyDate Owner;
    internal string ProjectileKind;
}
