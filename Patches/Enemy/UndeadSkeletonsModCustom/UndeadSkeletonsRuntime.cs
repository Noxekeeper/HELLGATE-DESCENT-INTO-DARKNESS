using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.UndeadSkeletonsModCustom;

/// <summary>Identifies an undead skeleton clone; survives renames and drives registry cleanup.</summary>
internal sealed class UndeadSkeletonMarker : MonoBehaviour
{
    internal UndeadSkeletonVariant Variant;

    private void OnDestroy()
    {
        UndeadSkeletonsRuntime.Unregister(gameObject);
    }
}

/// <summary>
/// Per-instance behaviour for undead skeletons: stats from config, animation speed, and H-scene entry suppression.
/// </summary>
internal static class UndeadSkeletonsRuntime
{
    private sealed class Entry
    {
        internal UndeadSkeletonVariant Variant;
        internal SkeletonAnimation BattleSpine;
    }

    private sealed class StateAccess
    {
        internal FieldInfo StateField;
        internal object EroWalk;
        internal object Idle;
    }

    private static readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>();
    private static readonly Dictionary<Type, StateAccess> StateAccessCache = new Dictionary<Type, StateAccess>();

    internal static void Register(GameObject spawned, UndeadSkeletonVariant variant)
    {
        if (spawned == null)
            return;

        UndeadSkeletonMarker marker = spawned.GetComponent<UndeadSkeletonMarker>();
        if (marker == null)
            marker = spawned.AddComponent<UndeadSkeletonMarker>();
        marker.Variant = variant;

        Entries[spawned.GetInstanceID()] = new Entry
        {
            Variant = variant,
            BattleSpine = spawned.GetComponent<SkeletonAnimation>()
        };
    }

    internal static void Unregister(GameObject go)
    {
        if (go != null)
            Entries.Remove(go.GetInstanceID());
    }

    internal static bool IsUndeadSkeleton(Component enemy)
    {
        return enemy != null && TryGetEntry(enemy.gameObject, out _);
    }

    private static bool TryGetEntry(GameObject go, out Entry entry)
    {
        entry = null;
        if (go == null)
            return false;

        if (Entries.TryGetValue(go.GetInstanceID(), out entry))
            return true;

        UndeadSkeletonMarker marker = go.GetComponent<UndeadSkeletonMarker>();
        if (marker == null)
            return false;

        Register(go, marker.Variant);
        return Entries.TryGetValue(go.GetInstanceID(), out entry);
    }

    /// <summary>Runs from vanilla Start postfix (before NoREroMod elite roll, so elite multipliers stack on top).</summary>
    internal static void ApplyStats(EnemyDate enemy)
    {
        if (enemy == null || !Entries.TryGetValue(enemy.gameObject.GetInstanceID(), out Entry entry))
            return;

        float vanillaHp = enemy.MaxHp;
        float maxHp = UndeadSkeletonsConfig.GetMaxHp(entry.Variant);
        if (maxHp <= 0f)
        {
            float hpMult = UndeadSkeletonsConfig.GetHpMultiplier(entry.Variant);
            if (hpMult > 0f && !Mathf.Approximately(hpMult, 1f))
                maxHp = Mathf.Max(1f, Mathf.Round(enemy.MaxHp * hpMult));
        }
        if (maxHp > 0f)
        {
            enemy.MaxHp = maxHp;
            enemy.Hp = maxHp;
        }

        float moveMult = UndeadSkeletonsConfig.GetMoveSpeedMultiplier(entry.Variant);
        if (moveMult > 0f && !Mathf.Approximately(moveMult, 1f))
            enemy.enmMovespeed *= moveMult;

        if (LoggedVariants.Add(entry.Variant))
            Plugin.Log?.LogInfo(
                $"[UndeadSkeletons] {UndeadSkeletonsLoader.GetSpawnKey(entry.Variant)}: HP {vanillaHp} -> {enemy.MaxHp}, "
                + $"move speed {enemy.enmMovespeed}, anim x{UndeadSkeletonsConfig.GetAnimSpeedMultiplier(entry.Variant)}");
    }

    private static readonly HashSet<UndeadSkeletonVariant> LoggedVariants = new HashSet<UndeadSkeletonVariant>();

    /// <summary>Vanilla Update rewrites mySpine.timeScale every frame; scale it right after.</summary>
    internal static void ApplyAnimSpeed(EnemyDate enemy)
    {
        if (enemy == null || !Entries.TryGetValue(enemy.gameObject.GetInstanceID(), out Entry entry))
            return;

        float mult = UndeadSkeletonsConfig.GetAnimSpeedMultiplier(entry.Variant);
        if (mult <= 0f || Mathf.Approximately(mult, 1f) || entry.BattleSpine == null)
            return;

        entry.BattleSpine.timeScale *= mult;
    }

    /// <summary>
    /// Vanilla OnTriggerStay2D starts the H-scene only from EROWALK. Report IDLE for the duration of the call so
    /// the skeleton still approaches a downed player but never grabs; ground/stab handling is unaffected.
    /// </summary>
    internal static void SuppressEroEntry(EnemyDate enemy, out object savedState)
    {
        savedState = null;
        if (enemy == null || !Entries.ContainsKey(enemy.gameObject.GetInstanceID()))
            return;

        StateAccess access = ResolveStateAccess(enemy.GetType());
        if (access == null)
            return;

        object current = access.StateField.GetValue(enemy);
        if (!Equals(current, access.EroWalk))
            return;

        savedState = current;
        access.StateField.SetValue(enemy, access.Idle);
    }

    internal static void RestoreEroEntry(EnemyDate enemy, object savedState)
    {
        if (enemy == null || savedState == null)
            return;

        StateAccess access = ResolveStateAccess(enemy.GetType());
        if (access != null && Equals(access.StateField.GetValue(enemy), access.Idle))
            access.StateField.SetValue(enemy, savedState);
    }

    /// <summary>
    /// FixedUpdate runs as IDLE while EROWALK is suppressed, which keeps the previous velocity; stop horizontal drift.
    /// </summary>
    internal static void RestoreEroEntryAndHold(EnemyDate enemy, object savedState)
    {
        if (enemy == null || savedState == null)
            return;

        RestoreEroEntry(enemy, savedState);

        RigidbodyField ??= AccessTools.Field(typeof(EnemyDate), "rigi2D");
        if (RigidbodyField?.GetValue(enemy) is Rigidbody2D body)
            body.velocity = new Vector2(0f, body.velocity.y);
    }

    private static FieldInfo RigidbodyField;

    private static StateAccess ResolveStateAccess(Type enemyType)
    {
        if (StateAccessCache.TryGetValue(enemyType, out StateAccess cached))
            return cached;

        StateAccess access = null;
        try
        {
            FieldInfo stateField = AccessTools.Field(enemyType, "state");
            Type stateEnum = enemyType.GetNestedType("enemystate", BindingFlags.Public | BindingFlags.NonPublic);
            if (stateField != null && stateEnum != null && stateField.FieldType == stateEnum)
            {
                access = new StateAccess
                {
                    StateField = stateField,
                    EroWalk = Enum.Parse(stateEnum, "EROWALK"),
                    Idle = Enum.Parse(stateEnum, "IDLE")
                };
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[UndeadSkeletons] State access failed for " + enemyType.Name + ": " + ex.Message);
            access = null;
        }

        if (access == null)
            Plugin.Log?.LogWarning("[UndeadSkeletons] No EROWALK/IDLE state on " + enemyType.Name + "; H-scene entry is not suppressed.");

        StateAccessCache[enemyType] = access;
        return access;
    }
}
