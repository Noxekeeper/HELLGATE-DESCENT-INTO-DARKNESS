using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace NoREroMod.Systems.EnemyFatality;

/// <summary>
/// Fatality PNG clip on Aradia: advance frames, hold last frame until respawn cleanup.
/// Transform scale is always 1. Frame timing uses Time.deltaTime so world slow-mo slows the clip.
/// </summary>
internal sealed class EnemyFatalityClipPlayer : MonoBehaviour
{
    internal const string RootName = "HellGate_EnemyFatalityClip";

    private static EnemyFatalityClipPlayer _active;

    private SpriteRenderer _renderer;
    private Sprite[] _frames;
    private float _frameSeconds;
    private float _fallDistance;
    private float _fallSpeedMultiplier;
    private float _elapsed;
    private float _frameTimer;
    private Vector3 _startWorld;
    private int _frameIndex = -1;
    private int _slowMoStartFrame1Based;
    private float _slowMoTimeScale;
    private float _slowMoDuration;
    private bool _slowMoEnabled;
    private bool _slowMoStarted;
    private float _slowMoEndsAtUnscaled = -1f;
    private playercon _player;
    private IEnemyFatalityProfile _profile;
    private bool _tauntScheduled;
    private bool _midClipSfxArmed;
    private bool _midClipSfxPlayed;
    private float _midClipSfxPlayAtUnscaled = -1f;
    private bool _faceLeft;
    private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
    private readonly List<SkeletonAnimation> _hiddenSpines = new List<SkeletonAnimation>();
    private readonly List<Graphic> _hiddenGraphics = new List<Graphic>();

    internal static bool HasActiveClip => _active != null;

    internal static EnemyFatalityClipPlayer Spawn(
        playercon player,
        IEnemyFatalityProfile profile,
        Vector3 startWorld,
        Sprite[] frames,
        bool faceLeft,
        float frameSeconds,
        float fallDistance,
        int sortingOrder,
        string sortingLayerName,
        float fallSpeedMultiplier,
        bool slowMoEnable,
        float slowMoTimeScale,
        float slowMoDuration,
        int slowMoStartFrame1Based)
    {
        if (frames == null || frames.Length == 0 || player == null || profile == null)
            return null;

        if (_active != null)
            _active.RestoreAndDestroy();

        var go = new GameObject(RootName);
        go.transform.position = startWorld;
        go.transform.localScale = Vector3.one;

        var runner = go.AddComponent<EnemyFatalityClipPlayer>();
        _active = runner;
        runner._renderer = go.AddComponent<SpriteRenderer>();
        if (!string.IsNullOrEmpty(sortingLayerName))
            runner._renderer.sortingLayerName = sortingLayerName;
        runner._renderer.sortingOrder = sortingOrder;
        runner._frames = frames;
        runner._frameSeconds = Mathf.Max(0.01f, frameSeconds);
        runner._fallDistance = Mathf.Max(0f, fallDistance);
        runner._fallSpeedMultiplier = Mathf.Max(0.01f, fallSpeedMultiplier);
        runner._startWorld = startWorld;
        runner._player = player;
        runner._profile = profile;
        runner._tauntScheduled = false;
        runner._midClipSfxArmed = false;
        runner._midClipSfxPlayed = false;
        runner._midClipSfxPlayAtUnscaled = -1f;
        runner._slowMoEnabled = slowMoEnable;
        runner._slowMoTimeScale = Mathf.Clamp(slowMoTimeScale, 0.05f, 1f);
        runner._slowMoDuration = Mathf.Max(0f, slowMoDuration);
        runner._slowMoStartFrame1Based = Mathf.Max(1, slowMoStartFrame1Based);
        runner._faceLeft = faceLeft;
        runner._renderer.flipX = faceLeft;

        runner.HidePlayerVisuals(player.gameObject);
        runner.ApplyFrame(0);
        // Re-asserted each frame — vanilla death Invoke("timescale") resets Time.timeScale.
        runner.TryStartSlowMo(1);
        return runner;
    }

    internal static void ForceCleanupForRespawn(playercon player)
    {
        EnemyFatalityTaunts.CancelPending();
        EnemyFatalityFlash.ForceStop();
        EnemyFatalityPlayback.DestroyActiveFatalityIcon();

        if (_active != null)
        {
            _active.RestoreAndDestroy();
            EnsurePlayerVisuallyRestored(player);
            return;
        }

        GameObject orphan = GameObject.Find(RootName);
        if (orphan != null)
            Object.Destroy(orphan);

        EnsurePlayerVisuallyRestored(player);
    }

    private void LateUpdate()
    {
        if (_frames == null || _frames.Length == 0 || _renderer == null)
            return;

        if (_player != null)
            EnemyFatalityEroSuppression.PinPlayerBody(_player);

        KeepPlayerHidden();
        EnemyFatalityEroSuppression.MaintainEnemyFreeze(forceImmediate: false);

        if (_renderer != null && _renderer.flipX != _faceLeft)
            _renderer.flipX = _faceLeft;

        MaintainSlowMo();
        AdvanceFallMotion();
        TryPlayMidClipSfx();

        // Hold last frame until Death_flag / respawn cleanup.
        if (_frameIndex >= _frames.Length - 1)
        {
            TryScheduleTaunt();
            return;
        }

        _frameTimer += Time.deltaTime;
        while (_frameIndex < _frames.Length - 1)
        {
            if (_frameTimer < _frameSeconds)
                break;

            _frameTimer -= _frameSeconds;
            ApplyFrame(_frameIndex + 1);
            ArmMidClipSfxIfNeeded();
            TryPlayMidClipSfx();
            TryStartSlowMo(_frameIndex + 1);
            if (_frameIndex >= _frames.Length - 1)
            {
                if (_profile != null)
                    EnemyFatalityAudio.PlayFinalOnce(_profile);
                TryScheduleTaunt();
            }
        }
    }

    private void ArmMidClipSfxIfNeeded()
    {
        if (_midClipSfxArmed || _profile == null)
            return;

        int afterFrame = _profile.MidClipSfxAfterFrame;
        if (afterFrame <= 0 || string.IsNullOrEmpty(_profile.MidClipSfxFileName))
            return;

        // _frameIndex is 0-based; cfg/docs use 1-based PNG names (20.png → frame 20).
        if (_frameIndex + 1 != afterFrame)
            return;

        _midClipSfxArmed = true;
        float delay = Mathf.Max(0f, _profile.MidClipSfxDelaySeconds);
        _midClipSfxPlayAtUnscaled = Time.unscaledTime + delay;
        EnemyFatalityConfig.LogDebug(
            "[" + _profile.Id + "] Mid-clip SFX armed at frame " + afterFrame
            + " (+" + delay.ToString("0.###") + "s realtime)");
    }

    private void TryPlayMidClipSfx()
    {
        if (!_midClipSfxArmed || _midClipSfxPlayed || _profile == null)
            return;

        if (Time.unscaledTime < _midClipSfxPlayAtUnscaled)
            return;

        _midClipSfxPlayed = true;
        EnemyFatalityAudio.PlayMidClipOnce(_profile);
    }

    private void TryScheduleTaunt()
    {
        if (_tauntScheduled)
            return;

        _tauntScheduled = true;
        EnemyFatalityTaunts.ScheduleAfterClip(_profile);
    }

    private void AdvanceFallMotion()
    {
        // Keep clip locked at spawn world position (FallDistance 0 = no drop).
        if (_fallDistance <= 0.0001f)
        {
            transform.position = _startWorld;
            transform.localScale = Vector3.one;
            return;
        }

        _elapsed += Time.deltaTime;
        float animWindow = _frameSeconds * _frames.Length;
        float fallWindow = animWindow > 0.0001f ? animWindow / _fallSpeedMultiplier : 0f;
        float t = fallWindow > 0.0001f ? Mathf.Clamp01(Mathf.Min(_elapsed, fallWindow) / fallWindow) : 1f;
        Vector3 pos = _startWorld;
        pos.y = _startWorld.y - (_fallDistance * t);
        pos.z = _startWorld.z;
        transform.position = pos;
        transform.localScale = Vector3.one;
    }

    private void TryStartSlowMo(int frame1Based)
    {
        if (!_slowMoEnabled || _slowMoStarted || _slowMoDuration <= 0f)
            return;

        if (frame1Based < _slowMoStartFrame1Based)
            return;

        _slowMoStarted = true;

        if (_player != null)
        {
            try { _player.CancelInvoke("timescale"); }
            catch { }
        }

        Time.timeScale = _slowMoTimeScale;
        _slowMoEndsAtUnscaled = Time.unscaledTime + _slowMoDuration;

        Plugin.Log?.LogInfo(
            "[EnemyFatality] Slow-mo ON frame="
            + frame1Based
            + " scale="
            + _slowMoTimeScale
            + " for "
            + _slowMoDuration
            + "s realtime");
    }

    /// <summary>
    /// When HellGate slow-mo is off: keep world at 1.0 (vanilla death hit sets 0.25).
    /// When on: pin our scale until the realtime window ends (vanilla Invoke restores 1.0).
    /// </summary>
    private void MaintainSlowMo()
    {
        if (!_slowMoEnabled)
        {
            if (_player != null)
            {
                try { _player.CancelInvoke("timescale"); }
                catch { }
            }

            if (Time.timeScale < 0.99f)
                Time.timeScale = 1f;
            return;
        }

        if (!_slowMoStarted)
            return;

        if (_slowMoEndsAtUnscaled < 0f)
            return;

        if (Time.unscaledTime >= _slowMoEndsAtUnscaled)
        {
            _slowMoEndsAtUnscaled = -1f;
            if (Time.timeScale < 0.99f)
                Time.timeScale = 1f;
            return;
        }

        if (_player != null)
        {
            try { _player.CancelInvoke("timescale"); }
            catch { }
        }

        if (Mathf.Abs(Time.timeScale - _slowMoTimeScale) > 0.001f)
            Time.timeScale = _slowMoTimeScale;
    }

    private void ApplyFrame(int index)
    {
        if (index == _frameIndex)
            return;

        _frameIndex = index;
        if (index >= 0 && index < _frames.Length && _frames[index] != null)
            _renderer.sprite = _frames[index];
    }

    private void HidePlayerVisuals(GameObject playerRoot)
    {
        _hiddenRenderers.Clear();
        _hiddenSpines.Clear();
        _hiddenGraphics.Clear();

        Renderer[] renderers = playerRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.transform.IsChildOf(transform))
                continue;
            if (!renderer.enabled)
                continue;

            renderer.enabled = false;
            _hiddenRenderers.Add(renderer);
        }

        SkeletonAnimation[] spines = playerRoot.GetComponentsInChildren<SkeletonAnimation>(true);
        for (int i = 0; i < spines.Length; i++)
        {
            SkeletonAnimation spine = spines[i];
            if (spine == null)
                continue;

            if (spine.enabled)
            {
                spine.enabled = false;
                _hiddenSpines.Add(spine);
            }

            // Force invisible; restore always uses Color.white (damage-hit can leave Color.red).
            if (spine.skeleton != null)
            {
                Color color = spine.skeleton.GetColor();
                if (color.a > 0.001f)
                    spine.skeleton.SetColor(new Color(color.r, color.g, color.b, 0f));
            }
        }

        Graphic[] graphics = playerRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null || !graphic.enabled)
                continue;
            if (graphic.transform.IsChildOf(transform))
                continue;

            graphic.enabled = false;
            _hiddenGraphics.Add(graphic);
        }
    }

    private void KeepPlayerHidden()
    {
        for (int i = 0; i < _hiddenRenderers.Count; i++)
        {
            Renderer renderer = _hiddenRenderers[i];
            if (renderer != null && renderer.enabled)
                renderer.enabled = false;
        }

        for (int i = 0; i < _hiddenSpines.Count; i++)
        {
            SkeletonAnimation spine = _hiddenSpines[i];
            if (spine != null && spine.enabled)
                spine.enabled = false;
        }
    }

    private void RestorePlayerVisuals()
    {
        for (int i = 0; i < _hiddenRenderers.Count; i++)
        {
            Renderer renderer = _hiddenRenderers[i];
            if (renderer != null)
                renderer.enabled = true;
        }

        for (int i = 0; i < _hiddenSpines.Count; i++)
        {
            SkeletonAnimation spine = _hiddenSpines[i];
            if (spine == null)
                continue;

            spine.enabled = true;
            // Always Color.white — never restore a damage-flash Color.red snapshot.
            if (spine.skeleton != null)
                spine.skeleton.SetColor(Color.white);
        }

        for (int i = 0; i < _hiddenGraphics.Count; i++)
        {
            Graphic graphic = _hiddenGraphics[i];
            if (graphic != null)
                graphic.enabled = true;
        }

        _hiddenRenderers.Clear();
        _hiddenSpines.Clear();
        _hiddenGraphics.Clear();
    }

    /// <summary>
    /// Respawn safety net: re-enable spines/renderers and clear damage-red tint.
    /// </summary>
    internal static void EnsurePlayerVisuallyRestored(playercon player)
    {
        if (player == null)
            return;

        try
        {
            player.CancelInvoke("colarrcovery");
        }
        catch
        {
        }

        SkeletonAnimation[] spines = player.GetComponentsInChildren<SkeletonAnimation>(true);
        for (int i = 0; i < spines.Length; i++)
        {
            SkeletonAnimation spine = spines[i];
            if (spine == null)
                continue;

            spine.enabled = true;
            if (spine.skeleton != null)
                spine.skeleton.SetColor(Color.white);

            MeshRenderer mesh = spine.GetComponent<MeshRenderer>();
            if (mesh != null)
                mesh.enabled = true;
        }

        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;
            if (renderer.gameObject.name.IndexOf(RootName) >= 0)
                continue;
            renderer.enabled = true;
        }
    }

    internal void RestoreAndDestroy()
    {
        EnemyFatalityFlash.ForceStop();

        if (_slowMoStarted && Time.timeScale < 0.99f)
            Time.timeScale = 1f;

        RestorePlayerVisuals();
        EnsurePlayerVisuallyRestored(_player);

        if (_active == this)
            _active = null;

        EnemyFatalitySession.End();

        if (gameObject != null)
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_slowMoStarted && Time.timeScale < 0.99f)
            Time.timeScale = 1f;

        // If Destroy bypassed RestoreAndDestroy, still unhide and clear damage-red.
        if (_hiddenRenderers.Count > 0 || _hiddenSpines.Count > 0)
            RestorePlayerVisuals();
        EnsurePlayerVisuallyRestored(_player);

        if (_active == this)
        {
            _active = null;
            if (EnemyFatalitySession.IsActive)
                EnemyFatalitySession.End();
        }
    }
}
