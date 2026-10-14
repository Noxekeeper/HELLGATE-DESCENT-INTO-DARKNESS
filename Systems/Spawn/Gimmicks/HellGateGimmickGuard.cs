using System.Collections.Generic;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Pregnancy.Patches;
using UnityEngine;

namespace NoREroMod.Systems.Spawn.Gimmicks;

internal enum HellGateGimmickGuardSide
{
    Both,
    Left,
    Right
}

/// <summary>
/// Sensor-driven gate/ladder. Levers never drive it.
/// Guard (token <c>guard=N</c>): closed while any living hostile enemy is inside the zone.
/// Auto (token <c>auto[=N]</c>): open only while the player is within N units along X.
/// Enemies in the guard zone always win over the player's proximity.
/// Both are anchored at the spawn point (the XY shown in F11), not at the spine pivot:
/// <c>guard</c> units along X to the chosen side(s), <c>guardh</c> total height split evenly up and down.
/// </summary>
internal sealed class HellGateGimmickGuard : MonoBehaviour
{
    internal const float DefaultHeight = 8f;
    internal const float DefaultAutoRange = 2f;
    private const float AutoCloseHysteresis = 0.5f;
    private const float ScanInterval = 0.25f;

    private HellGateGimmickController controller;
    private float length;
    private float height = DefaultHeight;
    private float autoRange;
    private HellGateGimmickGuardSide side = HellGateGimmickGuardSide.Both;
    private readonly List<EnemyDate> inside = new List<EnemyDate>();
    private Transform player;
    private bool playerNear;
    private bool firstScanDone;
    private float nextScan;

    internal List<EnemyDate> EnemiesInside => inside;
    internal bool HasEnemies => inside.Count > 0;
    internal bool HasGuardZone => length > 0f;
    internal bool IsAuto => autoRange > 0f;
    internal float AutoRange => autoRange;
    internal float Height => height;
    internal bool PlayerNear => playerNear;

    internal void Configure(
        HellGateGimmickController owner,
        float zoneLength,
        float zoneHeight,
        HellGateGimmickGuardSide zoneSide,
        float autoOpenRange)
    {
        controller = owner;
        length = Mathf.Max(0f, zoneLength);
        height = zoneHeight > 0f ? zoneHeight : DefaultHeight;
        side = zoneSide;
        autoRange = Mathf.Max(0f, autoOpenRange);
        inside.Clear();
        playerNear = false;
        firstScanDone = false;
        nextScan = 0f;
    }

    /// <summary>World box watched for enemies, anchored at the spawn point.</summary>
    internal Rect GetZone()
    {
        Vector3 p = transform.position;
        float left = side == HellGateGimmickGuardSide.Right ? p.x : p.x - length;
        float right = side == HellGateGimmickGuardSide.Left ? p.x : p.x + length;
        return Rect.MinMaxRect(left, p.y - height * 0.5f, right, p.y + height * 0.5f);
    }

    private void Update()
    {
        if (controller == null || Time.time < nextScan)
            return;
        nextScan = Time.time + ScanInterval;
        Scan();
    }

    private void Scan()
    {
        bool hadEnemies = HasEnemies;
        ScanEnemies();
        if (IsAuto)
            playerNear = IsPlayerNear(controller.IsOpen && firstScanDone);

        bool open = !HasEnemies && (!IsAuto || playerNear);
        if (!firstScanDone)
        {
            // Map load: snap to the right pose instead of playing an open/close animation.
            firstScanDone = true;
            controller.SetInitialState(open);
            controller.ApplyPoseNow(open);
            return;
        }

        if (open == controller.IsOpen)
            return;

        if (open)
            controller.Open();
        else
            controller.Close();

        if (HasEnemies != hadEnemies)
        {
            Plugin.Log?.LogInfo(
                $"[GIMMICK] Guard '{controller.name}': {(HasEnemies ? inside.Count + " enemy(ies) in zone — closed" : "zone clear — " + (open ? "opened" : "waiting for player"))}.");
        }
    }

    private void ScanEnemies()
    {
        inside.Clear();
        if (!HasGuardZone)
            return;

        Rect zone = GetZone();
        EnemyDate[] enemies = NoREroMod.Systems.Cache.LiveEnemyCache.GetEnemies();
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyDate enemy = enemies[i];
            if (IsLivingHostile(enemy) && zone.Contains((Vector2)enemy.transform.position))
                inside.Add(enemy);
        }
    }

    /// <summary>Horizontal distance to the spawn point; vertical tolerance is half the zone height.</summary>
    private bool IsPlayerNear(bool currentlyOpen)
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
                return false;
            player = playerObject.transform;
        }

        Vector3 p = transform.position;
        Vector3 pp = player.position;
        float range = currentlyOpen ? autoRange + AutoCloseHysteresis : autoRange;
        return Mathf.Abs(pp.x - p.x) <= range && Mathf.Abs(pp.y - p.y) <= height * 0.5f;
    }

    private static bool IsLivingHostile(EnemyDate enemy)
    {
        if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.Hp <= 0f)
            return false;
        if (enemy.GetComponent<WitchOffspringController>() != null)
            return false;
        return !FactionIds.IsPlayerNativeFaction(EnemyFactionRuntime.GetFaction(enemy.gameObject));
    }
}
