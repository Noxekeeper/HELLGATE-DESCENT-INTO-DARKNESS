using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Com.LuisPedroFonseca.ProCamera2D;
using HarmonyLib;
using Spine;
using Spine.Unity;
using UnityEngine;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Patches.Enemy.SlaveBigAxeMeatArmor;

/// <summary>
/// Post-fade MeatArmor sequence for <see cref="SlaveBigAxeEro"/>.
/// After JIGOTOFADE/ZGAMEOVER: hold, fade, swap to disk-loaded Aradia_armor (random Niku skin),
/// then EROWALK patrol until struggle escape. Escape restores the H skeleton and rebinds Spine events
/// (<c>Initialize(true)</c> drops the Start() Event subscription).
/// </summary>
internal static class SlaveBigAxeMeatArmorRuntime
{
    private enum PatrolPhase
    {
        Walking,
        Stopping
    }

    private sealed class SavedInfluence
    {
        public Transform Transform;
        public float H;
        public float V;
    }

    private sealed class InstanceState
    {
        public bool SequenceStarted;
        public bool Swapped;
        public bool Patrolling;
        public Coroutine Routine;
        public SkeletonDataAsset OriginalAsset;
        public Vector3 OriginalScale = Vector3.one;
        public float AnchorX;
        public int Dir = 1;
        public float ScaleMag = 1f;
        public float ScaleMagY = 1f;
        public readonly Queue<float> Waypoints = new();
        public float TargetX;
        public bool HasTarget;
        public PatrolPhase Phase = PatrolPhase.Walking;
        public float StopUntil;
        public int StopCount;
    }

    private static readonly Dictionary<int, InstanceState> States = new();
    private static readonly string[] RandomSkins = { "Niku_JIGO", "Niku_JIGO2", "Niku_NORMAL" };
    private static readonly float[] Distances = { 3f, 4f, 5f, 6f, 7f };
    private static readonly List<SavedInfluence> MutedInfluences = new();

    private const float PreSwapHoldSeconds = 5f;
    private const float FadeToBlackWait = 1.2f;
    private const float WalkSpeed = 1.1f;
    private const float ArriveEpsilon = 0.08f;
    private const float StopHoldSeconds = 5f;
    private const float AnimMixSeconds = 0.22f;
    private const float VisualScaleMultiplier = 0.85f;

    private static Transform _cameraFollowTarget;
    private static bool _zoomFrozen;
    private static bool _influencesMuted;

    private static FieldInfo _myspineField;
    private static FieldInfo _fadeimgField;
    private static FieldInfo _oyaField;
    private static MethodInfo _onEventMethod;

    private static FieldInfo MyspineField =>
        _myspineField ??= AccessTools.Field(typeof(SlaveBigAxeEro), "myspine")
                       ?? AccessTools.Field(typeof(SlaveBigAxeEro), "mySpine");

    private static FieldInfo FadeimgField =>
        _fadeimgField ??= AccessTools.Field(typeof(SlaveBigAxeEro), "fadeimg");

    private static FieldInfo OyaField =>
        _oyaField ??= AccessTools.Field(typeof(SlaveBigAxeEro), "oya");

    private static MethodInfo OnEventMethod =>
        _onEventMethod ??= AccessTools.Method(typeof(SlaveBigAxeEro), "OnEvent");

    /// <summary>True while the H spine is replaced by MeatArmor (suppresses vanilla OnEvent / H dialogue).</summary>
    internal static bool IsSwapped(SlaveBigAxeEro ero)
    {
        if (ero == null)
        {
            return false;
        }

        return States.TryGetValue(ero.GetInstanceID(), out InstanceState state) && state.Swapped;
    }

    private static bool IsFadeTriggerEvent(string eventName)
    {
        if (string.IsNullOrEmpty(eventName))
        {
            return false;
        }

        return eventName.Equals("JIGOTOFADE", StringComparison.OrdinalIgnoreCase)
            || eventName.Equals("ZGAMEOVER", StringComparison.OrdinalIgnoreCase);
    }

    private static InstanceState GetOrCreate(SlaveBigAxeEro ero)
    {
        int id = ero.GetInstanceID();
        if (!States.TryGetValue(id, out InstanceState state))
        {
            state = new InstanceState();
            States[id] = state;
        }

        return state;
    }

    /// <summary>
    /// Re-subscribes vanilla OnEvent after Initialize(true). Without this, START never advances to ERO.
    /// </summary>
    private static void RebindSpineOnEvent(SlaveBigAxeEro ero, SkeletonAnimation spine)
    {
        try
        {
            if (ero == null || spine?.state == null || OnEventMethod == null)
            {
                return;
            }

            var handler = (Spine.AnimationState.EventDelegate)Delegate.CreateDelegate(
                typeof(Spine.AnimationState.EventDelegate),
                ero,
                OnEventMethod);

            spine.state.Event -= handler;
            spine.state.Event += handler;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SlaveBigAxeMeatArmor] Rebind OnEvent failed: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(SlaveBigAxeEro), "OnEvent")]
    [HarmonyPrefix]
    private static bool OnEvent_SuppressVanillaWhileSwapped(SlaveBigAxeEro __instance)
    {
        return !IsSwapped(__instance);
    }

    [HarmonyPatch(typeof(SlaveBigAxeEro), "OnEvent")]
    [HarmonyPostfix]
    private static void OnEvent_Postfix(SlaveBigAxeEro __instance, Spine.Event e)
    {
        try
        {
            if (__instance == null || !__instance.isActiveAndEnabled)
            {
                return;
            }

            string eventName = e?.Data?.Name ?? string.Empty;
            if (!IsFadeTriggerEvent(eventName))
            {
                return;
            }

            // Illusive / Rodenia church event SlaveBigAxe — keep vanilla fade → scene flow.
            if (NoREroMod.Patches.Enemy.SlaveBigAxeIllusiveEventGate.ShouldSkipHellGateLogic())
            {
                return;
            }

            InstanceState state = GetOrCreate(__instance);
            if (state.SequenceStarted)
            {
                return;
            }

            state.SequenceStarted = true;
            if (state.Routine != null)
            {
                try { __instance.StopCoroutine(state.Routine); } catch { }
            }

            state.Routine = __instance.StartCoroutine(MeatArmorSequence(__instance, state));
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SlaveBigAxeMeatArmor] OnEvent failed: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(SlaveBigAxeEro), "Update")]
    [HarmonyPostfix]
    private static void Update_Postfix(SlaveBigAxeEro __instance)
    {
        try
        {
            if (__instance == null || !__instance.isActiveAndEnabled)
            {
                return;
            }

            int id = __instance.GetInstanceID();
            if (!States.TryGetValue(id, out InstanceState state))
            {
                return;
            }

            if (!state.Patrolling || !state.Swapped)
            {
                return;
            }

            var player = UnifiedPlayerCacheManager.GetPlayer();
            if (player == null || !player.eroflag || player.erodown == 0)
            {
                return;
            }

            TickPatrol(__instance, state);
        }
        catch
        {
        }
    }

    /// <summary>Snaps the camera to the walker after ProCamera2D resolves muted targets.</summary>
    [HarmonyPatch(typeof(ProCamera2D), "Move")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void ProCamera2D_Move_Postfix(ProCamera2D __instance)
    {
        try
        {
            if (_cameraFollowTarget == null || __instance == null)
            {
                return;
            }

            var player = UnifiedPlayerCacheManager.GetPlayer();
            if (player == null || !player.eroflag || player.erodown == 0)
            {
                StopCameraFollow();
                return;
            }

            Vector3 target = _cameraFollowTarget.position;
            __instance.MoveCameraInstantlyToPosition(new Vector2(target.x, target.y));
        }
        catch
        {
        }
    }

    [HarmonyPatch(typeof(SlaveBigAxe), "eroanime")]
    [HarmonyPrefix]
    private static void SlaveBigAxe_EroAnime_Prefix(SlaveBigAxe __instance)
    {
        TryCleanupOnVanillaEscape(__instance);
    }

    [HarmonyPatch(typeof(OtherSlavebigAxe), "eroanime")]
    [HarmonyPrefix]
    private static void OtherSlaveBigAxe_EroAnime_Prefix(OtherSlavebigAxe __instance)
    {
        TryCleanupOnVanillaEscape(__instance);
    }

    private static void TryCleanupOnVanillaEscape(EnemyDate enemy)
    {
        try
        {
            if (enemy == null || !enemy.eroflag)
            {
                return;
            }

            var player = enemy.com_player;
            if (player == null || player.erodown != 0)
            {
                return;
            }

            SlaveBigAxeEro ero = enemy.erodata != null
                ? enemy.erodata.GetComponent<SlaveBigAxeEro>()
                : null;

            CleanupEro(ero, player, enemy);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SlaveBigAxeMeatArmor] Escape cleanup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Restores H skeleton / transform, stops follow, and clears instance state.
    /// Camera combat restore remains vanilla <c>ero_camerareset</c>.
    /// </summary>
    private static void CleanupEro(SlaveBigAxeEro ero, playercon player, EnemyDate enemy)
    {
        StopCameraFollow();

        if (ero == null)
        {
            return;
        }

        int id = ero.GetInstanceID();
        if (!States.TryGetValue(id, out InstanceState state))
        {
            return;
        }

        try
        {
            if (state.Routine != null)
            {
                try { ero.StopCoroutine(state.Routine); } catch { }
                state.Routine = null;
            }

            Vector3 walkEnd = ero.transform.position;

            if (state.Swapped)
            {
                player ??= UnifiedPlayerCacheManager.GetPlayer();
                enemy ??= OyaField?.GetValue(ero) as EnemyDate;

                if (player != null)
                {
                    Vector3 pp = player.transform.position;
                    player.transform.position = new Vector3(walkEnd.x, pp.y, pp.z);
                }

                if (enemy != null)
                {
                    Vector3 ep = enemy.transform.position;
                    enemy.transform.position = new Vector3(walkEnd.x, ep.y, ep.z);
                }

                var spine = MyspineField?.GetValue(ero) as SkeletonAnimation;
                if (spine != null && state.OriginalAsset != null)
                {
                    try
                    {
                        spine.skeletonDataAsset = state.OriginalAsset;
                        spine.Initialize(true);
                        RebindSpineOnEvent(ero, spine);
                    }
                    catch
                    {
                    }
                }

                try
                {
                    ero.transform.localScale = state.OriginalScale;
                    if (enemy != null)
                    {
                        Vector3 p = enemy.transform.position;
                        p.z = walkEnd.z;
                        ero.transform.position = p;
                    }
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        States.Remove(id);
    }

    private static IEnumerator MeatArmorSequence(SlaveBigAxeEro ero, InstanceState state)
    {
        yield return new WaitForSeconds(PreSwapHoldSeconds);

        if (ero == null || !ero.isActiveAndEnabled)
        {
            yield break;
        }

        fadein_out fade = ResolveFade(ero);
        if (fade != null)
        {
            try { fade.on(); } catch { }
        }

        yield return new WaitForSeconds(FadeToBlackWait);

        if (ero == null || !ero.isActiveAndEnabled)
        {
            yield break;
        }

        if (!TrySwapToMeatArmor(ero, state))
        {
            if (fade != null)
            {
                try { fade.on_fade_in_fast1(); } catch { }
            }

            yield break;
        }

        if (fade != null)
        {
            try { fade.on_fade_in_fast1(); } catch { }
        }

        state.Patrolling = true;
        state.Phase = PatrolPhase.Walking;
        state.StopCount = 0;
        state.AnchorX = ero.transform.position.x;
        state.Dir = 1;
        QueueRandomRoute(state);
        BeginCameraFollow(ero);
    }

    private static fadein_out ResolveFade(SlaveBigAxeEro ero)
    {
        try
        {
            var fade = FadeimgField?.GetValue(ero) as fadein_out;
            if (fade != null)
            {
                return fade;
            }
        }
        catch
        {
        }

        try
        {
            GameObject ui = GameObject.Find("UIeffect");
            if (ui != null)
            {
                return ui.GetComponent<fadein_out>();
            }
        }
        catch
        {
        }

        return null;
    }

    private static bool TrySwapToMeatArmor(SlaveBigAxeEro ero, InstanceState state)
    {
        var spine = MyspineField?.GetValue(ero) as SkeletonAnimation;
        if (spine == null)
        {
            Plugin.Log?.LogWarning("[SlaveBigAxeMeatArmor] myspine missing");
            return false;
        }

        GameObject template = SlaveBigAxeMeatArmorSkeletonLoader.ResolveMaterialTemplate()
                              ?? ero.gameObject;
        SkeletonDataAsset armorAsset = SlaveBigAxeMeatArmorSkeletonLoader.GetArmorSkeleton(template);
        if (armorAsset == null)
        {
            Plugin.Log?.LogWarning("[SlaveBigAxeMeatArmor] Armor skeleton failed to load");
            return false;
        }

        try
        {
            if (!state.Swapped)
            {
                state.OriginalAsset = spine.skeletonDataAsset;
                state.OriginalScale = ero.transform.localScale;
            }

            spine.skeletonDataAsset = armorAsset;
            spine.Initialize(true);
            RebindSpineOnEvent(ero, spine);
            EnsureAnimMix(spine);

            string skinName = RandomSkins[UnityEngine.Random.Range(0, RandomSkins.Length)];
            TrySetSkin(spine, skinName);

            float magX = Mathf.Abs(state.OriginalScale.x);
            float magY = Mathf.Abs(state.OriginalScale.y);
            if (magX < 0.0001f) magX = 1f;
            if (magY < 0.0001f) magY = 1f;

            state.ScaleMag = magX * VisualScaleMultiplier;
            state.ScaleMagY = magY * VisualScaleMultiplier;
            state.Dir = 1;
            ApplyFacing(ero, spine, state);
            PlayAnim(spine, "EROWALK", true, 1.2f);

            state.Swapped = true;
            Plugin.Log?.LogInfo($"[SlaveBigAxeMeatArmor] Applied skin {skinName}");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SlaveBigAxeMeatArmor] Swap failed: {ex.Message}");
            try
            {
                if (state.OriginalAsset != null)
                {
                    spine.skeletonDataAsset = state.OriginalAsset;
                    spine.Initialize(true);
                    RebindSpineOnEvent(ero, spine);
                }
            }
            catch
            {
            }

            return false;
        }
    }

    private static void TrySetSkin(SkeletonAnimation spine, string skinName)
    {
        try
        {
            if (spine?.Skeleton?.Data == null)
            {
                return;
            }

            Skin skin = spine.Skeleton.Data.FindSkin(skinName);
            if (skin == null)
            {
                return;
            }

            spine.Skeleton.SetSkin(skin);
            spine.Skeleton.SetSlotsToSetupPose();
            spine.AnimationState?.Apply(spine.Skeleton);
        }
        catch
        {
        }
    }

    private static void EnsureAnimMix(SkeletonAnimation spine)
    {
        try
        {
            if (spine?.state?.Data == null)
            {
                return;
            }

            spine.state.Data.DefaultMix = AnimMixSeconds;
        }
        catch
        {
        }
    }

    private static void PlayAnim(SkeletonAnimation spine, string animName, bool loop, float timeScale)
    {
        try
        {
            if (spine?.state == null || spine.Skeleton?.Data == null)
            {
                return;
            }

            EnsureAnimMix(spine);

            if (spine.Skeleton.Data.FindAnimation(animName) == null)
            {
                if (!animName.Equals("IDLE", StringComparison.OrdinalIgnoreCase)
                    && spine.Skeleton.Data.FindAnimation("IDLE") != null)
                {
                    spine.state.SetAnimation(0, "IDLE", true);
                    spine.timeScale = timeScale;
                }

                return;
            }

            spine.state.SetAnimation(0, animName, loop);
            spine.timeScale = timeScale;
        }
        catch
        {
        }
    }

    private static void PlayFinThenIdle(SkeletonAnimation spine)
    {
        try
        {
            if (spine?.state == null || spine.Skeleton?.Data == null)
            {
                return;
            }

            EnsureAnimMix(spine);

            if (spine.Skeleton.Data.FindAnimation("FIN") == null)
            {
                PlayAnim(spine, "IDLE", true, 1f);
                return;
            }

            spine.state.SetAnimation(0, "FIN", false);
            spine.timeScale = 1f;

            if (spine.Skeleton.Data.FindAnimation("IDLE") != null)
            {
                spine.state.AddAnimation(0, "IDLE", true, 0f);
            }
        }
        catch
        {
        }
    }

    private static void TickPatrol(SlaveBigAxeEro ero, InstanceState state)
    {
        var spine = MyspineField?.GetValue(ero) as SkeletonAnimation;

        if (state.Phase == PatrolPhase.Stopping)
        {
            if (Time.time < state.StopUntil)
            {
                return;
            }

            state.Phase = PatrolPhase.Walking;
            if (state.Waypoints.Count == 0)
            {
                QueueRandomRoute(state);
            }
            else
            {
                AdvanceWaypoint(state);
            }

            UpdateMoveDir(state, ero.transform.position.x);
            PlayAnim(spine, "EROWALK", true, 1.2f);
            return;
        }

        if (!state.HasTarget)
        {
            AdvanceWaypoint(state);
        }

        Vector3 pos = ero.transform.position;
        UpdateMoveDir(state, pos.x);
        ApplyFacing(ero, spine, state);

        try
        {
            string anim = spine?.AnimationName ?? string.Empty;
            if (!anim.Equals("EROWALK", StringComparison.OrdinalIgnoreCase))
            {
                PlayAnim(spine, "EROWALK", true, 1.2f);
            }
        }
        catch
        {
        }

        float nextX = pos.x + WalkSpeed * state.Dir * Time.deltaTime;
        bool reached = state.Dir > 0
            ? nextX >= state.TargetX - ArriveEpsilon
            : nextX <= state.TargetX + ArriveEpsilon;

        if (reached)
        {
            pos.x = state.TargetX;
            ero.transform.position = pos;
            BeginStop(spine, state);
            return;
        }

        pos.x = nextX;
        ero.transform.position = pos;
    }

    private static void BeginStop(SkeletonAnimation spine, InstanceState state)
    {
        state.StopCount++;
        state.Phase = PatrolPhase.Stopping;
        state.StopUntil = Time.time + StopHoldSeconds;
        state.HasTarget = false;

        if ((state.StopCount % 2) == 0)
        {
            PlayFinThenIdle(spine);
        }
        else
        {
            PlayAnim(spine, "IDLE", true, 1f);
        }
    }

    private static float PickDistance()
    {
        return Distances[UnityEngine.Random.Range(0, Distances.Length)];
    }

    private static void QueueRandomRoute(InstanceState state)
    {
        state.Waypoints.Clear();
        state.HasTarget = false;

        int side = UnityEngine.Random.value < 0.5f ? -1 : 1;
        float dist = PickDistance();
        state.Waypoints.Enqueue(state.AnchorX + side * dist);
        state.Waypoints.Enqueue(state.AnchorX);

        AdvanceWaypoint(state);
    }

    private static void AdvanceWaypoint(InstanceState state)
    {
        if (state.Waypoints.Count == 0)
        {
            QueueRandomRoute(state);
            return;
        }

        state.TargetX = state.Waypoints.Dequeue();
        state.HasTarget = true;
    }

    private static void UpdateMoveDir(InstanceState state, float currentX)
    {
        state.Dir = state.TargetX >= currentX ? 1 : -1;
    }

    /// <summary>
    /// Aligns world facing with movement (<c>lossyScale.x</c> sign == Dir), compensating parent combat-body flip.
    /// </summary>
    private static void ApplyFacing(SlaveBigAxeEro ero, SkeletonAnimation spine, InstanceState state)
    {
        if (spine?.Skeleton != null)
        {
            spine.Skeleton.FlipX = false;
        }

        float parentSign = 1f;
        if (ero.transform.parent != null)
        {
            float px = ero.transform.parent.lossyScale.x;
            if (px < 0f) parentSign = -1f;
            else if (px > 0f) parentSign = 1f;
        }

        float localSign = state.Dir * parentSign;

        Vector3 scale = ero.transform.localScale;
        scale.x = Mathf.Abs(state.ScaleMag) * localSign;
        if (Mathf.Abs(state.ScaleMagY) > 0.0001f)
        {
            scale.y = Mathf.Abs(state.ScaleMagY);
        }

        ero.transform.localScale = scale;
    }

    private static void BeginCameraFollow(SlaveBigAxeEro ero)
    {
        try
        {
            if (ero == null)
            {
                return;
            }

            ProCamera2DZoomToFitTargets prozoom = UnifiedCameraCacheManager.GetProCamera2DZoomToFitTargets();
            if (prozoom != null && !_zoomFrozen)
            {
                // Freeze ZoomToFit while walking; combat zoom is restored by vanilla ero_camerareset.
                prozoom.enabled = false;
                _zoomFrozen = true;
            }

            MuteCameraTargetInfluences();
            _cameraFollowTarget = ero.transform;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[SlaveBigAxeMeatArmor] BeginCameraFollow failed: {ex.Message}");
        }
    }

    private static void MuteCameraTargetInfluences()
    {
        if (_influencesMuted)
        {
            return;
        }

        ProCamera2D pro2d = UnifiedCameraCacheManager.GetProCamera2D();
        if (pro2d?.CameraTargets == null)
        {
            return;
        }

        MutedInfluences.Clear();
        for (int i = 0; i < pro2d.CameraTargets.Count; i++)
        {
            CameraTarget t = pro2d.CameraTargets[i];
            if (t?.TargetTransform == null)
            {
                continue;
            }

            MutedInfluences.Add(new SavedInfluence
            {
                Transform = t.TargetTransform,
                H = t.TargetInfluenceH,
                V = t.TargetInfluenceV
            });
            t.TargetInfluenceH = 0f;
            t.TargetInfluenceV = 0f;
        }

        _influencesMuted = true;
    }

    private static void RestoreCameraTargetInfluences()
    {
        if (!_influencesMuted)
        {
            return;
        }

        try
        {
            ProCamera2D pro2d = UnifiedCameraCacheManager.GetProCamera2D();
            if (pro2d?.CameraTargets != null)
            {
                for (int i = 0; i < MutedInfluences.Count; i++)
                {
                    SavedInfluence saved = MutedInfluences[i];
                    if (saved.Transform == null)
                    {
                        continue;
                    }

                    CameraTarget existing = pro2d.GetCameraTarget(saved.Transform);
                    if (existing != null)
                    {
                        existing.TargetInfluenceH = saved.H;
                        existing.TargetInfluenceV = saved.V;
                    }
                }
            }
        }
        catch
        {
        }

        MutedInfluences.Clear();
        _influencesMuted = false;
    }

    private static void StopCameraFollow()
    {
        _cameraFollowTarget = null;
        RestoreCameraTargetInfluences();
        _zoomFrozen = false;
    }

    /// <summary>Clears runtime patrol/camera state (scene load).</summary>
    internal static void ResetAll()
    {
        StopCameraFollow();
        States.Clear();
    }
}
