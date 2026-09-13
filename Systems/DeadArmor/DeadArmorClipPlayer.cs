using UnityEngine;

namespace NoREroMod.Systems.DeadArmor;

/// <summary>
/// One-shot PNG clip at a world point: play frames once, optional flipX, fall, hold last frame.
/// Uses unscaled time so hit-stop / slow-mo do not stall or skip the clip.
/// </summary>
internal sealed class DeadArmorClipPlayer : MonoBehaviour
{
    private SpriteRenderer _renderer;
    private Sprite[] _frames;
    private float _frameSeconds;
    private float _fallDistance;
    private float _fallSpeedMultiplier;
    private float _elapsed;
    private float _totalDuration;
    private float _holdLastFrameSeconds;
    private Vector3 _startWorld;
    private int _frameIndex = -1;

    internal static DeadArmorClipPlayer Spawn(
        Vector3 startWorld,
        Sprite[] frames,
        bool faceLeft,
        float displayScale,
        float frameSeconds,
        float fallDistance,
        int sortingOrder,
        string sortingLayerName,
        float holdLastFrameSeconds,
        float fallSpeedMultiplier = 3f)
    {
        if (frames == null || frames.Length == 0)
            return null;

        var go = new GameObject("HellGate_DeadArmorClip");
        go.transform.position = startWorld;

        var player = go.AddComponent<DeadArmorClipPlayer>();
        player._renderer = go.AddComponent<SpriteRenderer>();
        if (!string.IsNullOrEmpty(sortingLayerName))
            player._renderer.sortingLayerName = sortingLayerName;
        player._renderer.sortingOrder = sortingOrder;
        player._frames = frames;
        player._frameSeconds = Mathf.Max(0.01f, frameSeconds);
        player._fallDistance = Mathf.Max(0f, fallDistance);
        player._fallSpeedMultiplier = Mathf.Max(0.01f, fallSpeedMultiplier);
        player._holdLastFrameSeconds = Mathf.Max(0f, holdLastFrameSeconds);
        player._totalDuration = player._frameSeconds * frames.Length + player._holdLastFrameSeconds;
        player._startWorld = startWorld;

        float scale = Mathf.Max(0.01f, displayScale);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        player._renderer.flipX = faceLeft;

        player.ApplyFrame(0);
        return player;
    }

    private void Update()
    {
        if (_frames == null || _frames.Length == 0 || _renderer == null)
        {
            Destroy(gameObject);
            return;
        }

        _elapsed += Time.unscaledDeltaTime;
        if (_elapsed >= _totalDuration)
        {
            Destroy(gameObject);
            return;
        }

        float animWindow = _frameSeconds * _frames.Length;
        int index;
        if (_elapsed >= animWindow)
            index = _frames.Length - 1;
        else
            index = Mathf.Min(_frames.Length - 1, Mathf.FloorToInt(_elapsed / _frameSeconds));

        ApplyFrame(index);

        float fallWindow = animWindow > 0.0001f ? animWindow / _fallSpeedMultiplier : 0f;
        float t = fallWindow > 0.0001f ? Mathf.Clamp01(Mathf.Min(_elapsed, fallWindow) / fallWindow) : 1f;
        Vector3 pos = _startWorld;
        pos.y = _startWorld.y - (_fallDistance * t);
        pos.z = _startWorld.z;
        transform.position = pos;
    }

    private void ApplyFrame(int index)
    {
        if (index == _frameIndex)
            return;

        _frameIndex = index;
        if (index >= 0 && index < _frames.Length && _frames[index] != null)
            _renderer.sprite = _frames[index];
    }
}
