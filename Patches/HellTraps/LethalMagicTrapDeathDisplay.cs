using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Spine.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Patches.HellTraps;

/// <summary>World-space PNG death overlay, player visual suppression, and respawn cleanup.</summary>
internal static class LethalMagicTrapDeathDisplay
{
    private static LethalMagicTrapDeathClipRunner _activeRunner;

    internal static bool HasActiveClip => _activeRunner != null;

    internal static void Preload()
    {
        Sprite[] frames = LethalMagicTrapAssetLoader.GetDeathFrames();
        if (frames == null || frames.Length == 0)
        {
            Plugin.Log?.LogWarning(
                "[LethalMagicTrapDeathDisplay] No death PNG frames loaded. Check sources/HellGate_sources/CustomDeath/Exp_Death.");
            return;
        }

        Plugin.Log?.LogInfo(
            "[LethalMagicTrapDeathDisplay] Loaded "
            + frames.Length
            + " death frame(s) from "
            + LethalMagicTrapAssetLoader.GetCachedDirectory());
    }

    internal static void TryApply(playercon player)
    {
        if (!Plugin.enableLethalMagicTrap.Value || player == null)
            return;

        Sprite[] frames = LethalMagicTrapAssetLoader.GetDeathFrames();
        if (frames == null || frames.Length == 0)
        {
            Plugin.Log?.LogWarning("[LethalMagicTrapDeathDisplay] Death clip skipped — no PNG frames.");
            return;
        }

        TryApplyWithFrames(player, frames, null);
    }

    /// <summary>Same runner as magic trap: bone start, fall toward TrapFloorWorld, optional scale.</summary>
    internal static bool TryApplyWithFrames(
        playercon player,
        Sprite[] frames,
        float? displayScaleOverride = null,
        LethalDeathClipPlaybackProfile playbackProfile = null)
    {
        if (player == null || frames == null || frames.Length == 0)
            return false;

        if (_activeRunner != null)
            _activeRunner.Restore();

        GameObject clipRoot = new GameObject("HellGateLethalMagicTrapDeathClip");
        clipRoot.transform.SetParent(null, true);

        _activeRunner = clipRoot.AddComponent<LethalMagicTrapDeathClipRunner>();
        _activeRunner.Begin(player, frames, displayScaleOverride, playbackProfile);
        LethalMagicTrapDeathContext.MarkCustomDeathActive();

        Plugin.Log?.LogInfo(
            "[LethalMagicTrapDeathDisplay] Playing death clip (bone start -> trap floor) ("
            + LethalTrapDeathSpriteLoader.DescribeSpriteWorldSize(frames[0])
            + ").");
        return true;
    }

    /// <summary>
    /// Lethal cocoon: same clip runner as magic trap, but PNG stays at trap X,Y (no bone fall).
    /// </summary>
    internal static bool TryApplyAtTrapAnchor(
        playercon player,
        Sprite[] frames,
        Vector3 trapAnchorWorld,
        float? displayScaleOverride = null)
    {
        if (player == null || frames == null || frames.Length == 0)
            return false;

        if (_activeRunner != null)
            _activeRunner.Restore();

        GameObject clipRoot = new GameObject("HellGateLethalMagicTrapDeathClip");
        clipRoot.transform.SetParent(null, true);

        _activeRunner = clipRoot.AddComponent<LethalMagicTrapDeathClipRunner>();
        _activeRunner.BeginFixedAtTrap(player, frames, trapAnchorWorld, displayScaleOverride);
        LethalMagicTrapDeathContext.MarkCustomDeathActive();

        Plugin.Log?.LogInfo(
            "[LethalMagicTrapDeathDisplay] Trap-anchored death clip @ "
            + trapAnchorWorld
            + " ("
            + LethalTrapDeathSpriteLoader.DescribeSpriteWorldSize(frames[0])
            + ")");
        return true;
    }

    /// <summary>White screen flash (NoREroMod UImngPatch.WhiteFadeIn) synced with death clip frame 1.</summary>
    internal static void TriggerDeathClipFlash()
    {
        try
        {
            System.Type uimngType = HellGateTypeResolver.Resolve("NoREroMod.UImngPatch");
            MethodInfo whiteFadeIn = uimngType?.GetMethod(
                "WhiteFadeIn",
                BindingFlags.Public | BindingFlags.Static);
            whiteFadeIn?.Invoke(null, null);
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[LethalMagicTrapDeathDisplay] Death clip flash failed: " + ex.Message);
        }
    }

    internal static void ScheduleDeferredApply(playercon player)
    {
        if (player == null || LethalMagicTrapDeathContext.IsCustomDeathActive)
            return;

        LethalMagicTrapDeathApplyHost host =
            player.GetComponent<LethalMagicTrapDeathApplyHost>();
        if (host == null)
            host = player.gameObject.AddComponent<LethalMagicTrapDeathApplyHost>();

        host.Schedule(player);
    }

    internal static void Restore(playercon player)
    {
        ForceCleanupForRespawn(player);
    }

    internal static void ForceCleanupForRespawn(playercon player = null)
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                player = playerObj.GetComponent<playercon>();
        }

        if (_activeRunner != null)
        {
            _activeRunner.Restore();
            _activeRunner = null;
        }

        LethalMagicTrapDeathClipRunner[] orphanedRunners =
            Object.FindObjectsOfType<LethalMagicTrapDeathClipRunner>();
        for (int i = 0; i < orphanedRunners.Length; i++)
        {
            LethalMagicTrapDeathClipRunner runner = orphanedRunners[i];
            if (runner == null)
                continue;
            runner.Restore();
        }

        _activeRunner = null;

        if (player != null)
        {
            EmergencyRestorePlayer(player);
            LethalMagicTrapRuntime.ClearLethalTrapDeathSlowMo(player);

            LethalMagicTrapDeathApplyHost host =
                player.GetComponent<LethalMagicTrapDeathApplyHost>();
            if (host != null)
                Object.Destroy(host);
        }

        LethalMagicTrapDeathContext.ClearCustomDeathActive();
    }

    private static void EmergencyRestorePlayer(playercon player)
    {
        if (player == null)
            return;

        Transform stuckClip = player.transform.Find("HellGateLethalMagicTrapDeathClip");
        if (stuckClip != null)
            Object.Destroy(stuckClip.gameObject);

        SkeletonAnimation spine = player.GetComponent<SkeletonAnimation>();
        if (spine == null)
            spine = player.GetComponentInChildren<SkeletonAnimation>(true);

        if (spine != null)
        {
            spine.enabled = true;
            spine.timeScale = 1f;
            if (spine.skeleton != null)
                spine.skeleton.SetColor(Color.white);

            MeshRenderer meshRenderer = spine.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.enabled = true;
        }

        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;
            if (renderer.gameObject.name.IndexOf("HellGateLethalMagicTrap") >= 0)
                continue;
            renderer.enabled = true;
        }

        SkeletonAnimation[] skeletonAnimations =
            player.GetComponentsInChildren<SkeletonAnimation>(true);
        for (int i = 0; i < skeletonAnimations.Length; i++)
        {
            if (skeletonAnimations[i] != null)
                skeletonAnimations[i].enabled = true;
        }
    }
}

internal sealed class LethalMagicTrapDeathApplyHost : MonoBehaviour
{
    /// <summary>Deferred one-frame apply when the first TryApply call races death setup.</summary>
    private playercon _player;

    internal void Schedule(playercon player)
    {
        _player = player;
        StopAllCoroutines();
        StartCoroutine(ApplyNextFrame());
    }

    private IEnumerator ApplyNextFrame()
    {
        yield return null;

        if (_player == null || !Plugin.enableLethalMagicTrap.Value)
            yield break;

        if (LethalMagicTrapDeathContext.IsCustomDeathActive)
            yield break;

        Plugin.Log?.LogInfo("[LethalMagicTrapDeathDisplay] Deferred death clip apply (next frame).");
        LethalMagicTrapDeathDisplay.TryApply(_player);
    }
}

internal sealed class LethalMagicTrapDeathClipRunner : MonoBehaviour
{
    /// <summary>Advances PNG frames in unscaled time while suppressing player renderers.</summary>
    private const string SpriteChildName = "HellGateLethalMagicTrapDeathSprite";
    private const string BoneEmptySpriteChildName = "HellGateLethalCocoonBoneEmptySprite";
    private const float PlayerVisualRescanSeconds = 0.4f;
    private const float ArriveDistance = 0.08f;
    private static readonly string[] StartBoneCandidates =
        { "sdw", "kubi", "body", "pelvis", "hip", "bone13", "bone16" };

    private playercon _player;
    private SkeletonAnimation _spine;
    private SpriteRenderer _spriteRenderer;
    private SpriteRenderer _boneEmptySpriteRenderer;
    private Sprite[] _frames;
    private float _frameTimer;
    private int _frameIndex;
    private float _targetOffsetY;
    private float _fallAcceleration;
    private float _fallMaxSpeed;
    private float _scale;
    private bool _active;
    private bool _hadSkeletonColor;
    private Color _originalSkeletonColor = Color.white;
    private float _originalSpineTimeScale = 1f;
    private float _visualRescanTimer;

    private Vector3 _startWorld;
    private Vector3 _targetWorld;
    private Vector3 _currentWorld;
    private Vector3 _fallVelocity;
    private bool _hasFallTarget;
    private bool _reachedTarget;
    private bool _playedMeatFallSound;
    private bool _fixedAtTrapAnchor;
    private string _startBoneName;
    private Vector2 _referenceSpriteWorldSize = Vector2.one;

    private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
    private readonly List<SkeletonAnimation> _hiddenSkeletonAnimations = new List<SkeletonAnimation>();
    private readonly List<SkeletonGraphic> _hiddenSkeletonGraphics = new List<SkeletonGraphic>();
    private readonly List<GameObject> _hiddenObjects = new List<GameObject>();

    private LethalDeathClipPlaybackProfile _playbackProfile;
    private bool _transitionEffectsTriggered;
    private bool _splitBoneEmptyAndTrapContent;
    private Vector3 _trapContentBaseWorld;

    internal void Begin(
        playercon player,
        Sprite[] frames,
        float? displayScaleOverride = null,
        LethalDeathClipPlaybackProfile playbackProfile = null)
    {
        BeginCore(player, frames, displayScaleOverride, null, playbackProfile);
    }

    internal void BeginFixedAtTrap(
        playercon player,
        Sprite[] frames,
        Vector3 trapAnchorWorld,
        float? displayScaleOverride = null)
    {
        BeginCore(player, frames, displayScaleOverride, trapAnchorWorld, null);
    }

    private void BeginCore(
        playercon player,
        Sprite[] frames,
        float? displayScaleOverride,
        Vector3? fixedTrapAnchorWorld,
        LethalDeathClipPlaybackProfile playbackProfile)
    {
        if (player == null || frames == null || frames.Length == 0)
            return;

        _player = player;
        _frames = frames;
        _playbackProfile = playbackProfile;
        _transitionEffectsTriggered = false;
        _splitBoneEmptyAndTrapContent =
            playbackProfile != null && playbackProfile.UseBoneEmptyFrameWithTrapContent;
        _frameIndex = 0;
        _frameTimer = 0f;
        _visualRescanTimer = 0f;
        _active = true;
        _reachedTarget = false;
        _playedMeatFallSound = false;
        _fallVelocity = Vector3.zero;
        _fixedAtTrapAnchor = fixedTrapAnchorWorld.HasValue;

        _targetOffsetY = LethalMagicTrapDeathTuning.TrapFloorOffsetY;
        if (_splitBoneEmptyAndTrapContent && playbackProfile != null)
            _targetOffsetY = playbackProfile.TrapContentOffsetY;

        _fallAcceleration = LethalMagicTrapDeathTuning.FallAcceleration;
        _fallMaxSpeed = LethalMagicTrapDeathTuning.FallMaxSpeed;
        _scale = displayScaleOverride.HasValue
            ? Mathf.Max(0.01f, displayScaleOverride.Value)
            : ResolveDisplayScale();

        _referenceSpriteWorldSize = frames[0] != null
            ? frames[0].bounds.size
            : Vector2.one;
        if (_referenceSpriteWorldSize.y <= 0.001f)
            _referenceSpriteWorldSize = Vector2.one;

        _spine = player.GetComponent<SkeletonAnimation>();
        if (_spine == null)
            _spine = player.GetComponentInChildren<SkeletonAnimation>(true);

        if (_fixedAtTrapAnchor)
        {
            _startWorld = fixedTrapAnchorWorld.Value;
            if (_player != null)
                _startWorld.z = _player.transform.position.z;

            _currentWorld = _startWorld;
            _hasFallTarget = false;
        }
        else
        {
            _startBoneName = null;
            if (_splitBoneEmptyAndTrapContent)
            {
                _trapContentBaseWorld = ResolveTrapContentWorld();
                _startWorld = _trapContentBaseWorld;
                _currentWorld = ResolveTrapContentWorldForFrame();
                _hasFallTarget = false;
            }
            else
            {
                _startWorld = CaptureStartWorldPosition();
                _startWorld.y += ResolveStartOffsetY(_startBoneName);
                _currentWorld = _startWorld;
                _hasFallTarget = TryResolveTargetWorld(out _targetWorld);
                if (_hasFallTarget && (_targetWorld - _startWorld).sqrMagnitude <= ArriveDistance * ArriveDistance)
                    _reachedTarget = true;
            }
        }

        HidePlayerVisuals(player.gameObject);
        EnsureSpriteRenderer();
        if (_splitBoneEmptyAndTrapContent)
            EnsureBoneEmptySpriteRenderer();

        _spriteRenderer.sprite = _frames[0];
        _spriteRenderer.enabled = true;
        if (_splitBoneEmptyAndTrapContent)
        {
            _boneEmptySpriteRenderer.sprite = LethalTrapDeathSpriteLoader.GetEmptyBonePlaceholderSprite();
            _boneEmptySpriteRenderer.enabled = true;
            UpdateBoneEmptyWorldPosition();
        }
        ApplyClipWorldPosition();
        enabled = true;

        if (_playbackProfile != null && _playbackProfile.UseBlackBackdropDuringClip)
            LethalTrapDeathBlackScreen.Show();

        if (_playbackProfile == null || !_playbackProfile.DeferFlashUntilClipTransition)
            LethalMagicTrapDeathDisplay.TriggerDeathClipFlash();
        if (!_fixedAtTrapAnchor)
            LethalMagicTrapDeathContext.SpawnQueuedHitEffect(_startWorld);

        string frameDesc = _frames != null && _frames.Length > 0
            ? LethalTrapDeathSpriteLoader.DescribeSpriteWorldSize(_frames[0])
            : "no frames";

        if (_fixedAtTrapAnchor)
        {
            Plugin.Log?.LogInfo(
                "[LethalMagicTrapDeathDisplay] Trap-anchored clip @ "
                + _startWorld
                + " scale="
                + _scale.ToString("0.##")
                + " ("
                + frameDesc
                + ")");
        }
        else
        {
            if (_splitBoneEmptyAndTrapContent)
            {
                Plugin.Log?.LogInfo(
                    "[LethalMagicTrapDeathDisplay] Cocoon split clip — empty bone anchor + PNG @ trap "
                    + ResolveTrapContentWorldForFrame()
                    + " scale="
                    + _scale.ToString("0.##")
                    + " ("
                    + frameDesc
                    + ").");
            }
            else
            {
                string boneLabel = string.IsNullOrEmpty(_startBoneName) ? "transform" : _startBoneName;
                Plugin.Log?.LogInfo(
                    "[LethalMagicTrapDeathDisplay] Clip start="
                    + _startWorld
                    + " bone="
                    + boneLabel
                    + " target="
                    + (_hasFallTarget ? _targetWorld.ToString() : "none")
                    + " scale="
                    + _scale.ToString("0.##")
                    + " ("
                    + frameDesc
                    + ").");
            }
        }
    }

    private Vector3 ResolveTrapContentWorld()
    {
        Vector3? trapFloor = LethalMagicTrapDeathContext.TrapFloorWorld;
        if (trapFloor.HasValue)
        {
            Vector3 trap = trapFloor.Value;
            float z = _player != null ? _player.transform.position.z : trap.z;
            return new Vector3(trap.x, trap.y + _targetOffsetY, z);
        }

        Vector3 boneWorld = CaptureStartWorldPosition();
        boneWorld.y += ResolveStartOffsetY(_startBoneName);
        return boneWorld;
    }

    private Vector3 ResolveTrapContentWorldForFrame()
    {
        Vector3 pos = _trapContentBaseWorld;
        if (!_splitBoneEmptyAndTrapContent || _playbackProfile == null)
            return pos;

        int frameOneBased = _frameIndex + 1;
        if (frameOneBased <= _playbackProfile.FastPhaseFrameCountOneBased)
            pos.y += _playbackProfile.FastPhaseTrapContentYOffset;

        return pos;
    }

    private void UpdateBoneEmptyWorldPosition()
    {
        if (_boneEmptySpriteRenderer == null)
            return;

        int frameOneBased = _frameIndex + 1;
        bool showBoneEmpty = _playbackProfile != null &&
            frameOneBased <= _playbackProfile.FastPhaseFrameCountOneBased;

        if (!showBoneEmpty)
        {
            _boneEmptySpriteRenderer.enabled = false;
            return;
        }

        _boneEmptySpriteRenderer.enabled = true;
        _startBoneName = null;
        Vector3 boneWorld = CaptureStartWorldPosition();
        boneWorld.y += ResolveStartOffsetY(_startBoneName);
        if (_player != null)
            boneWorld.z = _player.transform.position.z;

        Transform boneTransform = _boneEmptySpriteRenderer.transform;
        boneTransform.position = boneWorld;
        boneTransform.localScale = Vector3.one;
    }

    private static float ResolveStartOffsetY(string boneName)
    {
        if (string.Equals(boneName, "sdw", System.StringComparison.OrdinalIgnoreCase))
            return LethalMagicTrapDeathTuning.ShadowBoneStartOffsetY;

        return LethalMagicTrapDeathTuning.StartOffsetY;
    }

    private Vector3 CaptureStartWorldPosition()
    {
        if (_spine != null && _spine.skeleton != null)
        {
            _spine.skeleton.UpdateWorldTransform();
            for (int i = 0; i < StartBoneCandidates.Length; i++)
            {
                string candidate = StartBoneCandidates[i];
                Spine.Bone bone = _spine.skeleton.FindBone(candidate);
                if (bone == null)
                    continue;

                _startBoneName = candidate;
                return _spine.transform.TransformPoint(bone.WorldX, bone.WorldY, 0f);
            }

            _startBoneName = null;
            return _spine.transform.position;
        }

        _startBoneName = null;
        return _player != null ? _player.transform.position : Vector3.zero;
    }

    private bool TryResolveTargetWorld(out Vector3 targetWorld)
    {
        Vector3? trapFloor = LethalMagicTrapDeathContext.TrapFloorWorld;
        if (trapFloor.HasValue)
        {
            Vector3 trap = trapFloor.Value;
            targetWorld = new Vector3(trap.x, trap.y + _targetOffsetY, trap.z);
            return true;
        }

        targetWorld = _startWorld;
        return false;
    }

    private bool IsOverlayRenderer(Renderer renderer)
    {
        return renderer != null && renderer.transform.IsChildOf(transform);
    }

    internal void Restore()
    {
        if (!_active && _hiddenRenderers.Count == 0 && _hiddenSkeletonAnimations.Count == 0)
        {
            if (gameObject != null)
                Destroy(gameObject);
            return;
        }

        _active = false;
        _frames = null;
        _frameIndex = 0;
        _frameTimer = 0f;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.sprite = null;
            _spriteRenderer.enabled = false;
        }

        if (_boneEmptySpriteRenderer != null)
        {
            _boneEmptySpriteRenderer.sprite = null;
            _boneEmptySpriteRenderer.enabled = false;
        }

        for (int i = 0; i < _hiddenRenderers.Count; i++)
        {
            Renderer renderer = _hiddenRenderers[i];
            if (renderer != null)
                renderer.enabled = true;
        }

        for (int i = 0; i < _hiddenSkeletonAnimations.Count; i++)
        {
            SkeletonAnimation skeleton = _hiddenSkeletonAnimations[i];
            if (skeleton != null)
                skeleton.enabled = true;
        }

        for (int i = 0; i < _hiddenSkeletonGraphics.Count; i++)
        {
            SkeletonGraphic graphic = _hiddenSkeletonGraphics[i];
            if (graphic != null)
                graphic.enabled = true;
        }

        for (int i = 0; i < _hiddenObjects.Count; i++)
        {
            GameObject hidden = _hiddenObjects[i];
            if (hidden != null)
                hidden.SetActive(true);
        }

        _hiddenRenderers.Clear();
        _hiddenSkeletonAnimations.Clear();
        _hiddenSkeletonGraphics.Clear();
        _hiddenObjects.Clear();

        if (_spine != null)
        {
            _spine.enabled = true;
            _spine.timeScale = _originalSpineTimeScale;
            if (_hadSkeletonColor && _spine.skeleton != null)
                _spine.skeleton.SetColor(_originalSkeletonColor);
        }

        _hadSkeletonColor = false;

        LethalTrapDeathBlackScreen.Hide();

        if (gameObject != null)
            Destroy(gameObject);
    }

    private void LateUpdate()
    {
        if (!_active || _frames == null || _frames.Length == 0 || _spriteRenderer == null)
            return;

        SuppressPlayerVisuals();
        if (_playbackProfile != null &&
            _playbackProfile.UseBlackBackdropDuringClip &&
            _frameIndex + 1 <= _playbackProfile.FastPhaseFrameCountOneBased)
        {
            LethalTrapDeathBlackScreen.RefreshHiddenVisuals();
        }

        if (_splitBoneEmptyAndTrapContent)
        {
            UpdateBoneEmptyWorldPosition();
            _currentWorld = ResolveTrapContentWorldForFrame();
        }
        else if (_fixedAtTrapAnchor)
            _currentWorld = _startWorld;
        else
            AdvanceFallMotion();

        ApplyClipWorldPosition();
        LethalMagicTrapEroSuppression.ProcessDuringCustomDeath(_player);

        if (_frameIndex >= _frames.Length - 1)
            return;

        _frameTimer += Time.deltaTime;

        while (_frameIndex < _frames.Length - 1)
        {
            float frameSeconds = GetFrameSecondsForIndex(_frameIndex);
            if (_frameTimer < frameSeconds)
                break;

            _frameTimer -= frameSeconds;
            int nextIndex = _frameIndex + 1;
            TryTriggerTransitionEffectsForFrame(nextIndex);
            _frameIndex = nextIndex;
            _spriteRenderer.sprite = _frames[_frameIndex];
            TryPlayMeatFallSoundOnFrame();
        }
    }

    private float GetFrameSecondsForIndex(int frameIndexZeroBased)
    {
        float baseSeconds = LethalMagicTrapDeathTuning.FrameSeconds;
        if (_playbackProfile == null)
            return baseSeconds;

        int frameOneBased = frameIndexZeroBased + 1;
        if (frameOneBased <= _playbackProfile.FastPhaseFrameCountOneBased &&
            _playbackProfile.FastPhaseSpeedMultiplier > 0.001f)
        {
            return baseSeconds / _playbackProfile.FastPhaseSpeedMultiplier;
        }

        return baseSeconds;
    }

    private void TryTriggerTransitionEffectsForFrame(int nextIndexZeroBased)
    {
        if (_playbackProfile == null || _transitionEffectsTriggered)
            return;

        int frameOneBased = nextIndexZeroBased + 1;
        if (frameOneBased != _playbackProfile.SlowMoFlashAtFrameOneBased)
            return;

        _transitionEffectsTriggered = true;

        if (_playbackProfile.DeferFlashUntilClipTransition)
            LethalMagicTrapDeathDisplay.TriggerDeathClipFlash();

        if (_playbackProfile.DeferSlowMoUntilClipTransition && _player != null)
        {
            LethalTrapDeathCommon.ApplyDeathSlowMo(
                _player,
                _playbackProfile.SlowMoScale,
                _playbackProfile.SlowMoRealSeconds);

            Plugin.Log?.LogInfo(
                "[LethalMagicTrapDeathDisplay] Cocoon clip transition @ frame "
                + frameOneBased
                + " — flash + slow-mo "
                + _playbackProfile.SlowMoRealSeconds.ToString("0.##")
                + "s @ "
                + _playbackProfile.SlowMoScale.ToString("0.##")
                + " scale.");
        }

        if (_playbackProfile.UseBlackBackdropDuringClip)
            LethalTrapDeathBlackScreen.Hide();
    }

    private void TryPlayMeatFallSoundOnFrame()
    {
        if (_playedMeatFallSound)
            return;

        if (_frameIndex < GetMeatFallSoundFrameIndex())
            return;

        _playedMeatFallSound = true;
        LethalMagicTrapDeathAudio.TryPlayMeatFallSound();
    }

    private static int GetMeatFallSoundFrameIndex()
    {
        return Mathf.Max(0, LethalMagicTrapDeathTuning.MeatFallSoundFrameOneBased - 1);
    }

    private void AdvanceFallMotion()
    {
        if (!ShouldFallTowardTrap())
        {
            _currentWorld = _startWorld;
            _fallVelocity = Vector3.zero;
            return;
        }

        if (_reachedTarget || !_hasFallTarget)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        Vector3 toTarget = _targetWorld - _currentWorld;
        float distance = toTarget.magnitude;
        if (distance <= ArriveDistance)
        {
            _currentWorld = _targetWorld;
            _fallVelocity = Vector3.zero;
            NotifyClipLanded();
            return;
        }

        Vector3 direction = toTarget / distance;
        _fallVelocity += direction * (_fallAcceleration * dt);
        float speed = _fallVelocity.magnitude;
        if (speed > _fallMaxSpeed)
            _fallVelocity = _fallVelocity * (_fallMaxSpeed / speed);

        float step = _fallVelocity.magnitude * dt;
        if (step >= distance)
        {
            _currentWorld = _targetWorld;
            _fallVelocity = Vector3.zero;
            NotifyClipLanded();
            return;
        }

        _currentWorld += _fallVelocity * dt;
    }

    private void NotifyClipLanded()
    {
        if (_reachedTarget)
            return;

        _reachedTarget = true;
    }

    private bool ShouldFallTowardTrap()
    {
        return _hasFallTarget && _frameIndex >= GetFallStartFrameIndex();
    }

    private static int GetFallStartFrameIndex()
    {
        return Mathf.Max(0, LethalMagicTrapDeathTuning.FallStartFrameOneBased - 1);
    }

    private static float ResolveDisplayScale()
    {
        float scale = Plugin.lethalMagicTrapDeathClipDisplayScale?.Value
            ?? LethalMagicTrapDeathTuning.DisplayScale;
        return Mathf.Max(0.01f, scale);
    }

    private void ApplyClipWorldPosition()
    {
        if (_spriteRenderer == null)
            return;

        Transform spriteTransform = _spriteRenderer.transform;
        float uniformScale = _scale * GetCurrentFrameSizeCompensation();
        spriteTransform.localScale = new Vector3(uniformScale, uniformScale, 1f);
        spriteTransform.position = _currentWorld;
    }

    /// <summary>
    /// WebSpike (and similar) exports often change canvas size per frame; without this,
    /// localScale=1 makes later frames appear larger (last frame is usually the biggest).
    /// </summary>
    private float GetCurrentFrameSizeCompensation()
    {
        if (_frames == null || _frameIndex < 0 || _frameIndex >= _frames.Length)
            return 1f;

        Sprite current = _frames[_frameIndex];
        if (current == null)
            return 1f;

        float currentHeight = current.bounds.size.y;
        if (currentHeight <= 0.001f)
            return 1f;

        return _referenceSpriteWorldSize.y / currentHeight;
    }

    private void HidePlayerVisuals(GameObject playerRoot)
    {
        _hiddenRenderers.Clear();
        _hiddenSkeletonAnimations.Clear();
        _hiddenSkeletonGraphics.Clear();
        _hiddenObjects.Clear();

        MakeSkeletonInvisible();

        Renderer[] renderers = playerRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || IsOverlayRenderer(renderer))
                continue;

            DisableRenderer(renderer);
        }

        SkeletonAnimation[] skeletonAnimations =
            playerRoot.GetComponentsInChildren<SkeletonAnimation>(true);
        for (int i = 0; i < skeletonAnimations.Length; i++)
        {
            SkeletonAnimation skeleton = skeletonAnimations[i];
            if (skeleton == null || skeleton.transform.IsChildOf(transform))
                continue;

            if (skeleton == _spine)
            {
                MakeSkeletonInvisible();
                continue;
            }

            MeshRenderer meshRenderer = skeleton.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                DisableRenderer(meshRenderer);

            if (skeleton.enabled)
            {
                skeleton.enabled = false;
                _hiddenSkeletonAnimations.Add(skeleton);
            }
        }

        SkeletonGraphic[] skeletonGraphics =
            playerRoot.GetComponentsInChildren<SkeletonGraphic>(true);
        for (int i = 0; i < skeletonGraphics.Length; i++)
        {
            SkeletonGraphic graphic = skeletonGraphics[i];
            if (graphic == null || graphic.transform.IsChildOf(transform))
                continue;

            if (graphic.enabled)
            {
                graphic.enabled = false;
                _hiddenSkeletonGraphics.Add(graphic);
            }
        }

        HideNamedChild(playerRoot.transform, "UIface");
        HideNamedChild(playerRoot.transform, "damageUI");
        HideNamedChild(playerRoot.transform, "blood");
    }

    private void SuppressPlayerVisuals()
    {
        if (_player == null)
            return;

        MakeSkeletonInvisible();

        for (int i = 0; i < _hiddenRenderers.Count; i++)
        {
            Renderer cached = _hiddenRenderers[i];
            if (cached != null && cached.enabled)
                cached.enabled = false;
        }

        _visualRescanTimer += Time.unscaledDeltaTime;
        if (_visualRescanTimer < PlayerVisualRescanSeconds)
            return;

        _visualRescanTimer = 0f;
        RescanPlayerRenderers();
    }

    private void RescanPlayerRenderers()
    {
        Renderer[] renderers = _player.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || IsOverlayRenderer(renderer))
                continue;

            if (renderer.enabled)
                DisableRenderer(renderer);
        }
    }

    private void MakeSkeletonInvisible()
    {
        if (_spine == null || _spine.skeleton == null)
            return;

        if (!_hadSkeletonColor)
        {
            _originalSkeletonColor = _spine.skeleton.GetColor();
            _originalSpineTimeScale = _spine.timeScale;
            _hadSkeletonColor = true;
        }

        _spine.timeScale = 0f;
        Color color = _spine.skeleton.GetColor();
        if (color.a > 0.001f)
            _spine.skeleton.SetColor(new Color(color.r, color.g, color.b, 0f));

        MeshRenderer meshRenderer = _spine.GetComponent<MeshRenderer>();
        if (meshRenderer != null && meshRenderer.enabled)
            DisableRenderer(meshRenderer);
    }

    private void DisableRenderer(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled)
            return;

        renderer.enabled = false;
        if (!_hiddenRenderers.Contains(renderer))
            _hiddenRenderers.Add(renderer);
    }

    private void HideNamedChild(Transform playerRoot, string childName)
    {
        Transform child = playerRoot.Find(childName);
        if (child == null || !child.gameObject.activeSelf)
            return;

        child.gameObject.SetActive(false);
        _hiddenObjects.Add(child.gameObject);
    }

    private void EnsureSpriteRenderer()
    {
        Transform existing = transform.Find(SpriteChildName);
        GameObject overlayObject;
        if (existing != null)
        {
            overlayObject = existing.gameObject;
        }
        else
        {
            overlayObject = new GameObject(SpriteChildName);
            overlayObject.transform.SetParent(transform, false);
            overlayObject.transform.localRotation = Quaternion.identity;
        }

        _spriteRenderer = overlayObject.GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null)
            _spriteRenderer = overlayObject.AddComponent<SpriteRenderer>();

        ApplyPlayerSorting(
            _spriteRenderer,
            _player != null ? _player.gameObject : null,
            _fixedAtTrapAnchor || _splitBoneEmptyAndTrapContent);
    }

    private void EnsureBoneEmptySpriteRenderer()
    {
        Transform existing = transform.Find(BoneEmptySpriteChildName);
        GameObject overlayObject;
        if (existing != null)
        {
            overlayObject = existing.gameObject;
        }
        else
        {
            overlayObject = new GameObject(BoneEmptySpriteChildName);
            overlayObject.transform.SetParent(transform, false);
            overlayObject.transform.localRotation = Quaternion.identity;
        }

        _boneEmptySpriteRenderer = overlayObject.GetComponent<SpriteRenderer>();
        if (_boneEmptySpriteRenderer == null)
            _boneEmptySpriteRenderer = overlayObject.AddComponent<SpriteRenderer>();

        ApplyPlayerSorting(_boneEmptySpriteRenderer, _player != null ? _player.gameObject : null, false);
    }

    private static void ApplyPlayerSorting(
        SpriteRenderer spriteRenderer,
        GameObject playerObj,
        bool trapAnchoredClip = false)
    {
        string layerName = "Default";
        int order = 0;

        if (playerObj != null)
        {
            Renderer bodyRenderer = playerObj.GetComponent<Renderer>();
            if (bodyRenderer == null)
                bodyRenderer = playerObj.GetComponentInChildren<MeshRenderer>(true);
            if (bodyRenderer == null)
                bodyRenderer = playerObj.GetComponentInChildren<SpriteRenderer>(true);

            if (bodyRenderer != null)
            {
                layerName = bodyRenderer.sortingLayerName;
                order = bodyRenderer.sortingOrder;
            }
        }

        spriteRenderer.sortingLayerName = layerName;
        spriteRenderer.sortingOrder = order + (trapAnchoredClip ? 50 : 1);
    }

    private void OnDestroy()
    {
        if (!_active)
            return;

        Restore();
    }
}

/// <summary>Restores timeScale after a real-time delay (lethal trap death slow-mo only).</summary>
internal sealed class LethalMagicTrapDeathSlowMoHost : MonoBehaviour
{
    private Coroutine _restoreRoutine;

    internal void ScheduleRestore(float realSeconds)
    {
        if (_restoreRoutine != null)
            StopCoroutine(_restoreRoutine);

        _restoreRoutine = StartCoroutine(RestoreAfterRealtime(realSeconds));
    }

    private IEnumerator RestoreAfterRealtime(float realSeconds)
    {
        if (realSeconds > 0f)
            yield return new WaitForSecondsRealtime(realSeconds);

        if (Time.timeScale < 1f)
            Time.timeScale = 1f;

        _restoreRoutine = null;
        Destroy(this);
    }

    private void OnDestroy()
    {
        if (_restoreRoutine != null)
            StopCoroutine(_restoreRoutine);
    }
}
