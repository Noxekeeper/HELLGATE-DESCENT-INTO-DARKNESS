using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Spine.Unity;
using UnityEngine;
using NoREroMod.Systems.Spawn;

namespace NoREroMod.Patches.Enemy.DoreiModCustom;

/// <summary>
/// Replaces visual idle Dorei with fapping animation when Dorei waits in queue during H-scene.
///
/// Overlay approach: create separate child GameObject with its own SkeletonAnimation
/// for fapping, hide original MeshRenderer. Original mySpine is NEVER modified
/// (no Initialize, no skeletonDataAsset replacement) — prevents invalidation of cached
/// bone/slot refs in SinnerslaveCrossbow which caused freeze.
/// </summary>
[HarmonyPatch(typeof(SinnerslaveCrossbow))]
internal static class DoreiSpectatorIdlePatch
{
    private static SkeletonDataAsset _fappingAsset;

    private static readonly Dictionary<SinnerslaveCrossbow, GameObject> _fappingOverlays = new Dictionary<SinnerslaveCrossbow, GameObject>();
    private static readonly Dictionary<SinnerslaveCrossbow, float> _fappingEligibleSince = new Dictionary<SinnerslaveCrossbow, float>();

    private const float MoveThreshold = 0.15f;
    private const float FappingActivationDelay = 5f;
    private const float RestoreAfterEscapeDelay = 2f;
    private static float _restoreScheduledAt = -1f;
    private static bool _escapeCooldown;

    private static FieldInfo _mySpineField;
    private static FieldInfo _comPlayerField;
    private static FieldInfo _eroflagField;
    private static FieldInfo _stateField;

    private const int EROWALK = 16;

    static DoreiSpectatorIdlePatch()
    {
        _mySpineField = typeof(SinnerslaveCrossbow).GetField("mySpine", BindingFlags.NonPublic | BindingFlags.Instance);
        _comPlayerField = typeof(EnemyDate).GetField("com_player", BindingFlags.Public | BindingFlags.Instance);
        _eroflagField = typeof(EnemyDate).GetField("eroflag", BindingFlags.Public | BindingFlags.Instance);
        _stateField = typeof(SinnerslaveCrossbow).GetField("state", BindingFlags.Public | BindingFlags.Instance);
    }

    [HarmonyPatch("Update")]
    [HarmonyPostfix]
    static void Update_Postfix(SinnerslaveCrossbow __instance)
    {
        try
        {
            if (_restoreScheduledAt > 0f && Time.time >= _restoreScheduledAt)
            {
                _restoreScheduledAt = -1f;
                _escapeCooldown = false;
                RemoveAllOverlays();
            }

            if (_fappingAsset == null)
            {
                EnemyPrefabRegistry.Initialize();
                var prefab = EnemyPrefabRegistry.GetPrefab("Dorei") ?? EnemyPrefabRegistry.GetPrefab("SinnerslaveCrossbow");
                _fappingAsset = DoreiSkeletonLoader.GetDoreiFappingSkeleton(prefab);
                if (_fappingAsset == null) return;
            }

            var mySpine = _mySpineField?.GetValue(__instance) as SkeletonAnimation;
            if (mySpine == null) return;

            var comPlayer = _comPlayerField?.GetValue(__instance) as playercon;
            if (comPlayer == null) return;

            bool playerInHScene = comPlayer.eroflag;
            bool thisDoreiInHScene = _eroflagField != null && _eroflagField.GetValue(__instance) is bool ef && ef;
            int stateVal = _stateField?.GetValue(__instance) is Enum e ? Convert.ToInt32(e) : -1;
            var rb = __instance.GetComponent<Rigidbody2D>();
            bool isMoving = rb != null && Mathf.Abs(rb.velocity.x) > MoveThreshold;

            bool shouldUseFapping = playerInHScene && !thisDoreiInHScene && stateVal == EROWALK && !isMoving && !_escapeCooldown;

            if (shouldUseFapping)
            {
                if (!_fappingEligibleSince.ContainsKey(__instance))
                    _fappingEligibleSince[__instance] = Time.time;
                if (Time.time - _fappingEligibleSince[__instance] < FappingActivationDelay)
                    return;

                if (!_fappingOverlays.ContainsKey(__instance))
                    CreateOverlay(__instance, mySpine);
            }
            else
            {
                _fappingEligibleSince.Remove(__instance);
                if (_fappingOverlays.ContainsKey(__instance))
                    RemoveOverlay(__instance);
            }
        }
        catch (Exception) { }
    }

    private static void CreateOverlay(SinnerslaveCrossbow instance, SkeletonAnimation origSpine)
    {
        if (!DoreiSkeletonLoader.HasIdleAnimation(_fappingAsset)) return;

        float origScale = origSpine.skeletonDataAsset?.scale ?? 0.01f;
        if (origScale < 0.001f) origScale = 0.01f;
        float mult = Plugin.doreiSpectatorScaleMultiplier?.Value ?? 1f;
        _fappingAsset.scale = origScale * mult;

        var overlayGO = new GameObject("DoreiFapOverlay");
        overlayGO.SetActive(false);
        overlayGO.transform.SetParent(origSpine.transform, false);
        overlayGO.transform.localPosition = Vector3.zero;
        overlayGO.transform.localScale = Vector3.one;

        var fapSpine = overlayGO.AddComponent<SkeletonAnimation>();
        fapSpine.skeletonDataAsset = _fappingAsset;

        var origRenderer = origSpine.GetComponent<MeshRenderer>();
        var overlayRenderer = overlayGO.GetComponent<MeshRenderer>();
        if (origRenderer != null && overlayRenderer != null)
        {
            overlayRenderer.sortingLayerID = origRenderer.sortingLayerID;
            overlayRenderer.sortingOrder = origRenderer.sortingOrder;
        }

        overlayGO.SetActive(true);

        if (fapSpine.skeleton == null)
            fapSpine.Initialize(true);

        fapSpine.AnimationState?.SetAnimation(0, "IDLE", true);

        if (origRenderer != null)
            origRenderer.enabled = false;

        _fappingOverlays[instance] = overlayGO;
    }

    private static void RemoveOverlay(SinnerslaveCrossbow instance)
    {
        if (_fappingOverlays.TryGetValue(instance, out var overlay))
        {
            if (overlay != null)
                UnityEngine.Object.Destroy(overlay);
            _fappingOverlays.Remove(instance);
        }

        var spine = _mySpineField?.GetValue(instance) as SkeletonAnimation;
        if (spine != null)
        {
            var renderer = spine.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.enabled = true;
        }

        _fappingEligibleSince.Remove(instance);
    }

    private static void RemoveAllOverlays()
    {
        var keys = new List<SinnerslaveCrossbow>(_fappingOverlays.Keys);
        foreach (var key in keys)
            RemoveOverlay(key);
        _fappingEligibleSince.Clear();
    }

    [HarmonyPatch("OnDestroy")]
    [HarmonyPostfix]
    static void OnDestroy_Postfix(SinnerslaveCrossbow __instance)
    {
        _fappingOverlays.Remove(__instance);
        _fappingEligibleSince.Remove(__instance);
    }

    [HarmonyPatch(typeof(StruggleSystem), "startGrabInvul")]
    [HarmonyPostfix]
    static void ClearOnStruggleEscape()
    {
        ScheduleRestore();
    }

    [HarmonyPatch(typeof(playercon), "ImmediatelyERO")]
    [HarmonyPostfix]
    static void ClearOnImmediatelyERO()
    {
        ScheduleRestore();
    }

    private static void ScheduleRestore()
    {
        _escapeCooldown = true;
        _fappingEligibleSince.Clear();
        RemoveAllOverlays();
        _restoreScheduledAt = Time.time + RestoreAfterEscapeDelay;
    }

    /// <summary>When EROWALK Dorei moves during H-scene — use WALK instead of IDLE.</summary>
    [HarmonyPatch("setanimation")]
    [HarmonyPrefix]
    static void setanimation_Prefix(SinnerslaveCrossbow __instance, ref string name)
    {
        try
        {
            if (name != "IDLE") return;
            var comPlayer = _comPlayerField?.GetValue(__instance) as playercon;
            if (comPlayer == null || !comPlayer.eroflag) return;
            if (_eroflagField != null && _eroflagField.GetValue(__instance) is bool ef && ef) return;
            int stateVal = _stateField?.GetValue(__instance) is Enum e ? Convert.ToInt32(e) : -1;
            if (stateVal != EROWALK) return;
            var rb = __instance.GetComponent<Rigidbody2D>();
            if (rb == null || Mathf.Abs(rb.velocity.x) <= MoveThreshold) return;
            name = "WALK";
        }
        catch (Exception) { }
    }
}
