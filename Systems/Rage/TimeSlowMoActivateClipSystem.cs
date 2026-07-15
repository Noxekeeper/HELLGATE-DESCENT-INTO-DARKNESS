using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Spine.Unity;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Systems.Rage;

/// <summary>
/// Frame-by-frame PNG clip attached to player bone while Time Slow-Mo is active.
/// Immediately disappears on slow-mo deactivation.
/// </summary>
internal static class TimeSlowMoActivateClipSystem
{
    private const string EffectObjectName = "TimeSlowMoActivateClip_XUAIGNORE";
    private static readonly string[] BoneCandidates = { "body", "kubi", "pelvis", "hip", "root" };

    private static readonly List<Sprite> _frames = new List<Sprite>(32);
    private static bool _initialized;
    private static bool _framesLoaded;
    private static GameObject? _activeEffect;

    private static bool Enabled => Plugin.enableRageMode?.Value ?? false;
    private static float ClipFps => 24f;
    private static float ClipScale => 2f;
    private static float ClipOffsetX => 0f;
    private static float ClipOffsetY => 0f;

    internal static void Initialize()
    {
        if (_initialized) return;

        try
        {
            TimeSlowMoSystem.OnActivated += OnSlowMoActivated;
            TimeSlowMoSystem.OnDeactivated += OnSlowMoDeactivated;
            _initialized = true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[TimeSlowMoClip] Init failed: {ex.Message}");
        }
    }

    private static void OnSlowMoActivated()
    {
        if (!Enabled)
        {
            OnSlowMoDeactivated();
            return;
        }

        try
        {
            EnsureFramesLoaded();
            if (_frames.Count == 0) return;

            if (_activeEffect != null)
            {
                UnityEngine.Object.Destroy(_activeEffect);
                _activeEffect = null;
            }

            GameObject? playerObj = UnifiedPlayerCacheManager.GetPlayerObject();
            if (playerObj == null) return;

            _activeEffect = new GameObject(EffectObjectName);
            _activeEffect.transform.SetParent(playerObj.transform, false);

            SpriteRenderer sr = _activeEffect.AddComponent<SpriteRenderer>();
            ApplyPlayerSorting(sr, playerObj);
            _activeEffect.transform.localScale = Vector3.one * ClipScale;

            var runner = _activeEffect.AddComponent<TimeSlowMoActivateClipRunner>();
            runner.Setup(playerObj, BoneCandidates, _frames.ToArray(), sr, ClipFps, ClipOffsetX, ClipOffsetY);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[TimeSlowMoClip] Activate failed: {ex.Message}");
        }
    }

    private static void OnSlowMoDeactivated()
    {
        if (_activeEffect != null)
        {
            UnityEngine.Object.Destroy(_activeEffect);
            _activeEffect = null;
        }
    }

    private static void EnsureFramesLoaded()
    {
        if (_framesLoaded) return;
        _framesLoaded = true;

        string dir = ResolveClipDirectory();
        if (!Directory.Exists(dir))
        {
            Plugin.Log?.LogWarning("[TimeSlowMoClip] Folder not found: " + dir);
            return;
        }

        string[] files = Directory.GetFiles(dir, "frame_*.png");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        foreach (string file in files)
        {
            Sprite? sprite = LoadSpriteFromFile(file);
            if (sprite != null)
                _frames.Add(sprite);
        }

        if (_frames.Count == 0)
            Plugin.Log?.LogWarning("[TimeSlowMoClip] No frame_*.png found in: " + dir);
        else
            Plugin.Log?.LogInfo($"[TimeSlowMoClip] Loaded {_frames.Count} frames.");
    }

    private static string ResolveClipDirectory()
    {
        string gameRoot = Application.dataPath;
        if (gameRoot.EndsWith("_Data"))
            gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

        var list = new List<string>(3);
        AddUniquePath(list, Path.Combine(Path.Combine(Path.Combine(Path.Combine(gameRoot, "sources"), "HellGate_sources"), "Rage"), "TimeSlowMoActivate"));
        AddUniquePath(list, Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(gameRoot, ".."), "sources"), "HellGate_sources"), "Rage"), "TimeSlowMoActivate"));
        AddUniquePath(list, Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(gameRoot, "BepInEx"), "plugins"), "NoR_HellGate"), "sources"), "HellGate_sources"), "Rage"), "TimeSlowMoActivate"));

        for (int i = 0; i < list.Count; i++)
        {
            if (Directory.Exists(list[i]))
                return list[i];
        }

        return list[0];
    }

    private static void AddUniquePath(List<string> list, string path)
    {
        try
        {
            string full = Path.GetFullPath(path);
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], full, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            list.Add(full);
        }
        catch { }
    }

    private static Sprite? LoadSpriteFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            byte[] data = File.ReadAllBytes(filePath);
            if (data == null || data.Length == 0) return null;

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(data, false)) return null;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            return Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[TimeSlowMoClip] Frame load failed: {filePath} :: {ex.Message}");
            return null;
        }
    }

    private static void ApplyPlayerSorting(SpriteRenderer sr, GameObject playerObj)
    {
        string layerName = "Default";
        int order = -1;

        Renderer? renderer = playerObj.GetComponent<Renderer>();
        if (renderer == null) renderer = playerObj.GetComponentInChildren<MeshRenderer>();
        if (renderer == null) renderer = playerObj.GetComponentInChildren<SkinnedMeshRenderer>();
        if (renderer == null) renderer = playerObj.GetComponentInChildren<SpriteRenderer>();

        if (renderer != null)
        {
            layerName = renderer.sortingLayerName;
            order = renderer.sortingOrder - 1;
        }

        sr.sortingLayerName = layerName;
        sr.sortingOrder = order;
    }
}

internal sealed class TimeSlowMoActivateClipRunner : MonoBehaviour
{
    private SkeletonAnimation? _spine;
    private Spine.Bone? _bone;
    private bool _useSpineRootFallback;
    private Sprite[]? _frames;
    private SpriteRenderer? _renderer;
    private float _frameDuration;
    private float _timer;
    private int _frameIndex;
    private float _offsetX;
    private float _offsetY;
    private bool _initialized;

    internal void Setup(
        GameObject playerObj,
        string[] boneNameCandidates,
        Sprite[] frames,
        SpriteRenderer renderer,
        float fps,
        float offsetX,
        float offsetY)
    {
        _frames = frames;
        _renderer = renderer;
        _frameDuration = 1f / Mathf.Max(1f, fps);
        _offsetX = offsetX;
        _offsetY = offsetY;

        _spine = playerObj.GetComponentInChildren<SkeletonAnimation>(true);
        if (_spine != null && _spine.skeleton != null && boneNameCandidates != null)
        {
            foreach (string name in boneNameCandidates)
            {
                if (string.IsNullOrEmpty(name)) continue;
                _bone = _spine.skeleton.FindBone(name);
                if (_bone != null) break;
            }
        }

        if (_bone == null && _spine != null)
            _useSpineRootFallback = true;

        if (_frames == null || _frames.Length == 0 || _renderer == null || _spine == null)
        {
            UnityEngine.Object.Destroy(gameObject);
            return;
        }

        _renderer.sprite = _frames[0];
        _initialized = true;
    }

    private void LateUpdate()
    {
        if (!_initialized) return;
        if (!TimeSlowMoSystem.IsActive)
        {
            UnityEngine.Object.Destroy(gameObject);
            return;
        }

        if (_spine == null || _spine.skeleton == null || _frames == null || _frames.Length == 0 || _renderer == null)
        {
            UnityEngine.Object.Destroy(gameObject);
            return;
        }

        if (_bone != null)
            transform.position = _spine.transform.TransformPoint(_bone.WorldX + _offsetX, _bone.WorldY + _offsetY, 0f);
        else if (_useSpineRootFallback)
            transform.position = _spine.transform.position + new Vector3(_offsetX, _offsetY + 0.35f, 0f);
        else
            transform.position = _spine.transform.position;

        _timer += Time.unscaledDeltaTime;
        while (_timer >= _frameDuration)
        {
            _timer -= _frameDuration;
            _frameIndex = (_frameIndex + 1) % _frames.Length;
            _renderer.sprite = _frames[_frameIndex];
        }
    }
}

