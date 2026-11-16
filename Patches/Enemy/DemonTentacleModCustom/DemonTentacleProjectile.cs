using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NoREroMod.Systems.CombatAi.Factions;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.DemonTentacleModCustom;

/// <summary>
/// Faction support for DemonTentacle projectiles. Vanilla AngelStatue bullets home on the player only and
/// pass through enemy hurtboxes. This component damages hostile-faction enemies on contact and, while the
/// owner is in a committed faction brawl, steers the bullet at the owner's faction target instead of the player.
/// Player damage stays vanilla: disabled behaviours still receive OnTriggerEnter2D.
/// </summary>
internal sealed class DemonTentacleProjectile : MonoBehaviour
{
    private const float TurnDegreesPerStep = 5f;
    private const float StartSpeed = 4f;
    private const float MaxSpeed = 10f;
    private const float SpeedGrowthPerStep = 1.03f;
    private const float EnemyTargetLifetime = 8f;

    private static bool _componentsLogged;

    private GameObject _root;
    private EnemyDate _owner;
    private EnemyDate _target;
    private Rigidbody2D _body;
    private readonly List<Behaviour> _disabledSteering = new List<Behaviour>();
    private float _speed = StartSpeed;
    private bool _steering;
    private bool _hit;

    internal static void Attach(GameObject bullet, EnemyDate owner)
    {
        if (bullet == null || owner == null || !EnemyFactionsConfig.Enable)
            return;

        // Trigger messages reach the collider's and the Rigidbody2D's GameObject, so live next to the body.
        Rigidbody2D body = bullet.GetComponent<Rigidbody2D>() ?? bullet.GetComponentInChildren<Rigidbody2D>();
        DemonTentacleProjectile projectile = (body != null ? body.gameObject : bullet).AddComponent<DemonTentacleProjectile>();
        projectile._root = bullet;
        projectile._owner = owner;
        projectile._body = body;

        if (EnemyFactionRuntime.IsFactionCombatCommitted(owner.gameObject) &&
            EnemyFactionRuntime.TryGetNearestHostile(owner, out EnemyDate target))
            projectile._target = target;

        LogComponentsOnce(bullet);
    }

    private IEnumerator Start()
    {
        if (_target == null || _body == null)
            yield break;

        // Wait one frame so vanilla Start/Invoke setup (collider enable, player lookup) has run before steering is taken over.
        yield return null;
        if (_hit || !IsAlive(_target))
            yield break;

        DisableVanillaSteering();
        _speed = Mathf.Max(StartSpeed, _body.velocity.magnitude);
        _steering = true;
        Destroy(_root, EnemyTargetLifetime);
    }

    private void FixedUpdate()
    {
        if (!_steering)
            return;

        if (!IsAlive(_target))
        {
            ReleaseToVanilla();
            return;
        }

        Vector2 toTarget = (Vector2)AimPoint(_target) - _body.position;
        float current = _body.velocity.sqrMagnitude > 0.0001f
            ? Mathf.Atan2(_body.velocity.y, _body.velocity.x) * Mathf.Rad2Deg
            : Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        float desired = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        float angle = Mathf.MoveTowardsAngle(current, desired, TurnDegreesPerStep);

        if (_speed < MaxSpeed)
            _speed = Mathf.Min(MaxSpeed, _speed * SpeedGrowthPerStep);

        float rad = angle * Mathf.Deg2Rad;
        _body.velocity = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * _speed;
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (_hit || col == null || col.gameObject == null || col.gameObject.tag != "EnemyCol")
            return;
        if (_owner == null || _owner.Hp <= 0f || !EnemyFactionsConfig.Enable)
            return;

        EnemyDate defender = col.GetComponentInParent<EnemyDate>();
        if (defender == null || defender == _owner || defender.Hp <= 0f)
            return;
        if (!EnemyFactionRuntime.AreHostile(_owner.gameObject, defender.gameObject))
            return;

        _hit = true;
        EnemyFactionRuntime.ApplyProjectileFactionDamage(_owner, defender, 0f, "DemonTentacle");
        Destroy(_root);
    }

    /// <summary>Homing scripts on the bullet that chase the player (have a playercon field and a FixedUpdate).</summary>
    private void DisableVanillaSteering()
    {
        foreach (MonoBehaviour behaviour in _root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null || behaviour == this || !behaviour.enabled)
                continue;
            if (!IsPlayerHomingScript(behaviour.GetType()))
                continue;

            behaviour.enabled = false;
            _disabledSteering.Add(behaviour);
        }
    }

    private void ReleaseToVanilla()
    {
        _steering = false;
        foreach (Behaviour behaviour in _disabledSteering)
        {
            if (behaviour != null)
                behaviour.enabled = true;
        }
        _disabledSteering.Clear();
    }

    private static bool IsPlayerHomingScript(Type type)
    {
        if (type == typeof(HomingMissile))
            return true;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        return type.GetMethod("FixedUpdate", flags) != null &&
               type.GetFields(flags).Any(f => f.FieldType == typeof(playercon));
    }

    private static bool IsAlive(EnemyDate enemy)
    {
        return enemy != null && enemy.gameObject != null && enemy.gameObject.activeInHierarchy && enemy.Hp > 0f;
    }

    private static Vector3 AimPoint(EnemyDate target)
    {
        ChildColliderTrigger hurtbox = target.GetComponentInChildren<ChildColliderTrigger>();
        Collider2D col = hurtbox != null ? hurtbox.GetComponent<Collider2D>() : null;
        return col != null && col.enabled ? col.bounds.center : target.transform.position + Vector3.up;
    }

    private static void LogComponentsOnce(GameObject bullet)
    {
        if (_componentsLogged)
            return;
        _componentsLogged = true;

        string names = string.Join(", ", bullet.GetComponentsInChildren<Component>(true)
            .Where(c => c != null)
            .Select(c => c.gameObject.name + ":" + c.GetType().Name)
            .ToArray());
        Plugin.Log?.LogInfo("[DemonTentacle] projectile components: " + names);
    }
}
