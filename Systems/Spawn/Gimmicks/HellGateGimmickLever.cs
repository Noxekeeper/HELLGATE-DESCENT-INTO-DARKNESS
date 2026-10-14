using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.Spawn.Gimmicks;

/// <summary>
/// Lever of a spawned HellGate gimmick. Sits on the object that carried vanilla DoorOpen / ladder_on
/// (keeps its trigger collider). A built-in lever drives only its own gimmick; a standalone lever
/// (no owner) drives gimmicks without their own lever within <see cref="radius"/>.
/// Vanilla gates/ladders have no <see cref="HellGateGimmickController"/> and are never affected.
/// </summary>
internal sealed class HellGateGimmickLever : MonoBehaviour
{
    internal const float DefaultRadius = 10f;
    private const string MoveAnim = "MOVE";
    private const float UseCooldown = 1f;
    private const float FallbackUseDistance = 2f;
    private const string PlayerHitTag = "playerDAMAGEcol";

    private static readonly List<HellGateGimmickLever> activeLevers = new List<HellGateGimmickLever>();

    private SkeletonAnimation leverSpine;
    private Canvas prompt;
    private HellGateGimmickController owner;
    private bool interactive;
    private float radius = DefaultRadius;
    private bool pulled;
    private bool started;
    private bool hasTrigger;
    private bool playerInside;
    private float nextUseTime;
    private string idleAnim = string.Empty;
    private playercon player;
    private PlayerStatus playerStatus;

    internal static List<HellGateGimmickLever> ActiveLevers => activeLevers;

    internal bool IsInteractive => interactive;
    internal float Radius => radius;
    internal bool HasOwner => owner != null;

    /// <summary>Visible lever handle; center of the standalone-lever radius.</summary>
    internal Vector3 WorldAnchor => VisualTransform.position;

    internal Transform VisualTransform => leverSpine != null ? leverSpine.transform : transform;

    internal void Bind(SkeletonAnimation spine, Canvas promptCanvas, HellGateGimmickController ownerController)
    {
        leverSpine = spine;
        prompt = promptCanvas;
        owner = ownerController;
        if (owner != null)
            owner.SetOwnLever(this);
    }

    internal void ClearOwner()
    {
        if (owner != null)
            owner.SetOwnLever(null);
        owner = null;
    }

    internal void Configure(bool isInteractive, float useRadius)
    {
        interactive = isInteractive;
        radius = useRadius > 0f ? useRadius : DefaultRadius;
        if (!interactive)
            SetPrompt(false);
    }

    internal void SetInitialPulled(bool value)
    {
        pulled = value;
        if (started)
            ApplyVisual(false);
    }

    private void OnEnable()
    {
        if (!activeLevers.Contains(this))
            activeLevers.Add(this);
    }

    private void OnDisable()
    {
        activeLevers.Remove(this);
    }

    private void Start()
    {
        started = true;
        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].isTrigger)
            {
                hasTrigger = true;
                break;
            }
        }

        if (leverSpine != null && leverSpine.state != null)
        {
            TrackEntry current = leverSpine.state.GetCurrent(0);
            idleAnim = current != null && current.Animation != null ? current.Animation.Name : string.Empty;
        }

        SetPrompt(false);
        ApplyVisual(false);
    }

    private void Update()
    {
        if (!interactive || !ResolvePlayer())
            return;

        bool inRange = hasTrigger
            ? playerInside
            : Vector2.Distance(player.transform.position, transform.position) <= FallbackUseDistance;
        if (!hasTrigger)
            SetPrompt(inRange);

        if (!inRange || Time.time < nextUseTime || !player._key_submit)
            return;
        if (playerStatus == null || !playerStatus._SOUSA)
            return;
        if (player.state != "IDLE" && player.state != "WALK")
            return;

        Pull();
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!interactive || col == null || col.gameObject.tag != PlayerHitTag)
            return;
        playerInside = true;
        SetPrompt(true);
    }

    private void OnTriggerExit2D(Collider2D col)
    {
        if (col == null || col.gameObject.tag != PlayerHitTag)
            return;
        playerInside = false;
        SetPrompt(false);
    }

    private void Pull()
    {
        nextUseTime = Time.time + UseCooldown;
        if (owner != null && owner.IsGuardDriven)
        {
            Plugin.Log?.LogInfo($"[GIMMICK] Lever '{name}' ignored: its gimmick is driven by a guard zone.");
            return;
        }

        pulled = !pulled;
        ApplyVisual(true);
        int affected = HellGateGimmickController.SetStateInRadius(WorldAnchor, radius, pulled, owner);
        string scope = owner != null ? "own gimmick" : $"gimmick(s) within {radius:0.#}";
        Plugin.Log?.LogInfo($"[GIMMICK] Lever '{name}' → {(pulled ? "open" : "close")} {affected} {scope}.");
    }

    private void ApplyVisual(bool animate)
    {
        if (leverSpine == null || leverSpine.state == null)
            return;

        try
        {
            if (pulled)
            {
                if (!HasAnimation(MoveAnim))
                    return;
                TrackEntry entry = leverSpine.state.SetAnimation(0, MoveAnim, false);
                if (!animate && entry != null)
                    entry.Time = entry.EndTime;
                return;
            }

            if (!string.IsNullOrEmpty(idleAnim) && HasAnimation(idleAnim))
                leverSpine.state.SetAnimation(0, idleAnim, true);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[GIMMICK] Lever animation failed on {name}: {ex.Message}");
        }
    }

    private bool HasAnimation(string animationName)
    {
        if (leverSpine.skeletonDataAsset == null)
            return false;
        SkeletonData data = leverSpine.skeletonDataAsset.GetSkeletonData(true);
        return data != null && data.FindAnimation(animationName) != null;
    }

    private void SetPrompt(bool visible)
    {
        if (prompt != null)
            prompt.enabled = visible;
    }

    private bool ResolvePlayer()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                player = playerObject.GetComponent<playercon>();
        }

        if (playerStatus == null)
        {
            GameObject controller = GameObject.FindWithTag("GameController");
            if (controller != null)
                playerStatus = controller.GetComponent<PlayerStatus>();
        }

        return player != null;
    }
}
