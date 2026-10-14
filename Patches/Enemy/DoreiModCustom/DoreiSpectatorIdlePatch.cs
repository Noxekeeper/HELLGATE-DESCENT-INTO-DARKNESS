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
    private static readonly Dictionary<SinnerslaveCrossbow, float> _facingBeforeSwap = new Dictionary<SinnerslaveCrossbow, float>();

    private const float MoveThreshold = 0.15f;
    private const float FappingActivationDelay = 5f;
    private const float RestoreAfterEscapeDelay = 2f;
    private static float _restoreScheduledAt = -1f;
    private static bool _escapeCooldown;

    private static FieldInfo _mySpineField;
    private static FieldInfo _comPlayerField;
    private static FieldInfo _eroflagField;
    private static FieldInfo _stateField;

    private const int IDLE = 0;
    private const int WALK = 2;
    private const int RUN = 3;
    private const int BLANK = 15;
    private const int EROWALK = 16;
    private const float SpectatorRangeX = 15f;
    private const float SpectatorRangeY = 3f;

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

            bool shouldUseFapping = playerInHScene && !thisDoreiInHScene && !isMoving && !_escapeCooldown &&
                                    __instance.Hp > 0f && IsSpectatorEligible(__instance, comPlayer, stateVal);

            if (shouldUseFapping)
            {
                if (!_fappingEligibleSince.ContainsKey(__instance))
                {
                    _fappingEligibleSince[__instance] = Time.time;
                    _facingBeforeSwap[__instance] = WorldFacing(__instance.transform);
                }
                if (Time.time - _fappingEligibleSince[__instance] < FappingActivationDelay)
                    return;

                if (!_fappingOverlays.ContainsKey(__instance))
                    CreateOverlay(__instance, mySpine);
                KeepOverlayFacing(__instance);
            }
            else
            {
                _fappingEligibleSince.Remove(__instance);
                _facingBeforeSwap.Remove(__instance);
                if (_fappingOverlays.ContainsKey(__instance))
                    RemoveOverlay(__instance);
            }
        }
        catch (Exception) { }
    }

    /// <summary>
    /// Vanilla EROWALK only starts while the player is down, and faction passive-wait fakes the
    /// player distance during the H-scene, so Dorei that arrive later never reach it. Idle-like
    /// states within real range count as spectators too; attack / damage / guard never do.
    /// </summary>
    private static bool IsSpectatorEligible(SinnerslaveCrossbow instance, playercon player, int stateVal)
    {
        if (stateVal == EROWALK)
            return true;
        if (stateVal != IDLE && stateVal != WALK && stateVal != RUN && stateVal != BLANK)
            return false;

        Vector3 self = instance.transform.position;
        Vector3 target = player.transform.position;
        return Mathf.Abs(target.x - self.x) < SpectatorRangeX && Mathf.Abs(target.y - self.y) < SpectatorRangeY;
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

        float captured = _facingBeforeSwap.TryGetValue(instance, out var f) ? f : WorldFacing(instance.transform);
        Plugin.Log?.LogInfo($"[DoreiSpectator] overlay on {instance.name}: facingAtEligible={captured:+0;-0} facingNow={WorldFacing(instance.transform):+0;-0} spineOnRoot={origSpine.transform == instance.transform}");
    }

    private static float WorldFacing(Transform t) => t.lossyScale.x < 0f ? -1f : 1f;

    /// <summary>
    /// Pins the overlay to the facing the Dorei had when it became a spectator; vanilla keeps
    /// rewriting the root scale from faked passive-wait player positions while the overlay is up.
    /// </summary>
    private static void KeepOverlayFacing(SinnerslaveCrossbow instance)
    {
        if (!_fappingOverlays.TryGetValue(instance, out var overlay) || overlay == null) return;
        if (!_facingBeforeSwap.TryGetValue(instance, out var wanted)) return;

        Transform parent = overlay.transform.parent;
        float parentSign = parent != null ? WorldFacing(parent) : 1f;
        Vector3 s = overlay.transform.localScale;
        float x = wanted * parentSign;
        if (s.x != x)
        {
            s.x = x;
            overlay.transform.localScale = s;
        }
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
        _facingBeforeSwap.Remove(instance);
    }

    private static void RemoveAllOverlays()
    {
        var keys = new List<SinnerslaveCrossbow>(_fappingOverlays.Keys);
        foreach (var key in keys)
            RemoveOverlay(key);
        _fappingEligibleSince.Clear();
        _facingBeforeSwap.Clear();
    }

    [HarmonyPatch("OnDestroy")]
    [HarmonyPostfix]
    static void OnDestroy_Postfix(SinnerslaveCrossbow __instance)
    {
        _fappingOverlays.Remove(__instance);
        _fappingEligibleSince.Remove(__instance);
        _facingBeforeSwap.Remove(__instance);
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
