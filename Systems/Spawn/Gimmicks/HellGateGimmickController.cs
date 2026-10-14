using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.Spawn.Gimmicks;

internal enum HellGateGimmickKind
{
    Unknown,
    Gate,
    Ladder
}

/// <summary>
/// Replaces vanilla DoorOpen / ladder_on / GimmickLadder_on on spawned clones.
/// Vanilla scripts read/write game_fragmng._GimmickArraystage (saved), so clones must not keep them.
/// State lives here only; <see cref="HellGateGimmickLever"/> and <see cref="HellGateGimmickGuard"/> drive it.
/// </summary>
internal sealed class HellGateGimmickController : MonoBehaviour
{
    private const string GateClosedAnim = "IDLE";
    private const string GateOpeningAnim = "OPEN";
    private const string GateOpenAnim = "OPENIDLE";
    private const string GateClosingAnim = "CLOSE";
    private const string LadderClosedAnim = "NON";
    private const string LadderOpeningAnim = "START";
    private const string LadderOpenAnim = "IDLE";

    private static readonly List<HellGateGimmickController> activeControllers = new List<HellGateGimmickController>();

    private HellGateGimmickKind kind;
    private SkeletonAnimation mainSpine;
    private AudioSource audioSource;
    private HellGateGimmickLever ownLever;
    private HellGateGimmickGuard guard;
    private bool isOpen;
    private bool started;

    internal static List<HellGateGimmickController> ActiveControllers => activeControllers;

    internal bool IsOpen => isOpen;
    internal GameObject MainSpineObject => mainSpine != null ? mainSpine.gameObject : null;

    /// <summary>Pivot of the animated gate/ladder; fallback when its mesh has no bounds.</summary>
    internal Vector3 AnchorPosition => mainSpine != null ? mainSpine.transform.position : transform.position;

    /// <summary>Has its own interactive lever; standalone levers never drive it.</summary>
    internal bool HasOwnLever => ownLever != null && ownLever.IsInteractive;

    /// <summary>Driven by a guard zone; levers never drive it.</summary>
    internal bool IsGuardDriven => Guard != null;

    internal HellGateGimmickGuard Guard
    {
        get
        {
            if (guard == null)
                guard = GetComponent<HellGateGimmickGuard>();
            return guard;
        }
    }

    internal void SetOwnLever(HellGateGimmickLever lever)
    {
        ownLever = lever;
    }

    /// <summary>
    /// XY distance² from <paramref name="point"/> to the nearest edge of the visible gate/ladder mesh.
    /// Spine pivots sit far from the art (a ladder pivot is at its top), so pivot distance misleads.
    /// </summary>
    internal float DistanceSqrTo(Vector3 point)
    {
        Vector2 p = point;
        Renderer renderer = mainSpine != null ? mainSpine.GetComponent<Renderer>() : null;
        if (renderer != null && renderer.enabled)
        {
            Bounds b = renderer.bounds;
            if (b.size.x > 0.01f || b.size.y > 0.01f)
            {
                Vector2 nearest = new Vector2(
                    Mathf.Clamp(p.x, b.min.x, b.max.x),
                    Mathf.Clamp(p.y, b.min.y, b.max.y));
                return (nearest - p).sqrMagnitude;
            }
        }

        return ((Vector2)AnchorPosition - p).sqrMagnitude;
    }

    internal void Bind(HellGateGimmickKind gimmickKind, SkeletonAnimation main, AudioSource audio)
    {
        kind = gimmickKind;
        mainSpine = main;
        audioSource = audio;
    }

    /// <summary>Spawn-time state: applied without transition animation or sound.</summary>
    internal void SetInitialState(bool open)
    {
        isOpen = open;
        if (started)
            ApplyState(open, false);
    }

    /// <summary>Poses and rebuilds the spine mesh this frame, without transition (preview bake, guard snap on load).</summary>
    internal void ApplyPoseNow(bool open)
    {
        isOpen = open;
        ApplyState(open, false);
        if (mainSpine == null)
            return;
        mainSpine.Update(0f);
        mainSpine.LateUpdate();
    }

    internal void Open(bool animate = true)
    {
        if (isOpen)
            return;
        isOpen = true;
        ApplyState(true, animate);
    }

    internal void Close(bool animate = true)
    {
        if (!isOpen)
            return;
        isOpen = false;
        ApplyState(false, animate);
    }

    /// <summary>
    /// Lever hook. A built-in lever (<paramref name="owner"/> set) drives only its own gimmick.
    /// A standalone lever drives gimmicks without a lever of their own whose visible edge is within
    /// <paramref name="radius"/> (XY). Guard-driven gimmicks ignore every lever.
    /// </summary>
    internal static int SetStateInRadius(Vector3 center, float radius, bool open, HellGateGimmickController owner)
    {
        if (owner != null)
        {
            if (owner.IsGuardDriven)
                return 0;
            owner.SetState(open, true);
            return 1;
        }

        float radiusSqr = radius * radius;
        int matched = 0;
        for (int i = activeControllers.Count - 1; i >= 0; i--)
        {
            HellGateGimmickController controller = activeControllers[i];
            if (controller == null || controller.HasOwnLever || controller.IsGuardDriven)
                continue;
            if (controller.DistanceSqrTo(center) > radiusSqr)
                continue;

            controller.SetState(open, true);
            matched++;
        }

        return matched;
    }

    private void SetState(bool open, bool animate)
    {
        if (open)
            Open(animate);
        else
            Close(animate);
    }

    private void OnEnable()
    {
        if (!activeControllers.Contains(this))
            activeControllers.Add(this);
    }

    private void OnDisable()
    {
        activeControllers.Remove(this);
    }

    private void Start()
    {
        started = true;
        ApplyState(isOpen, false);
    }

    private void ApplyState(bool open, bool animate)
    {
        try
        {
            ApplyMainSpine(open, animate);
            if (animate && open && audioSource != null && audioSource.clip != null)
                audioSource.PlayOneShot(audioSource.clip);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[GIMMICK] Failed to apply state on {name}: {ex.Message}");
        }
    }

    private void ApplyMainSpine(bool open, bool animate)
    {
        if (mainSpine == null || mainSpine.state == null)
            return;

        switch (kind)
        {
            case HellGateGimmickKind.Gate:
                if (open)
                    PlayWithFollowUp(mainSpine, animate ? GateOpeningAnim : null, GateOpenAnim);
                else
                    PlayWithFollowUp(mainSpine, animate ? GateClosingAnim : null, GateClosedAnim);
                break;

            case HellGateGimmickKind.Ladder:
                if (open)
                    PlayWithFollowUp(mainSpine, animate ? LadderOpeningAnim : null, LadderOpenAnim);
                else
                    PlayWithFollowUp(mainSpine, null, LadderClosedAnim);
                break;
        }
    }

    private static void PlayWithFollowUp(SkeletonAnimation spine, string transitionAnim, string loopAnim)
    {
        bool hasTransition = !string.IsNullOrEmpty(transitionAnim) && HasAnimation(spine, transitionAnim);
        bool hasLoop = HasAnimation(spine, loopAnim);

        if (hasTransition)
        {
            spine.state.SetAnimation(0, transitionAnim, false);
            if (hasLoop)
                spine.state.AddAnimation(0, loopAnim, true, 0f);
            return;
        }

        if (hasLoop)
            spine.state.SetAnimation(0, loopAnim, true);
    }

    private static bool HasAnimation(SkeletonAnimation spine, string animationName)
    {
        if (spine == null || spine.skeletonDataAsset == null || string.IsNullOrEmpty(animationName))
            return false;

        SkeletonData data = spine.skeletonDataAsset.GetSkeletonData(true);
        return data != null && data.FindAnimation(animationName) != null;
    }
}
