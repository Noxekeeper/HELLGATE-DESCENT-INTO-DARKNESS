using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Spine.Unity;
using NoREroMod.Systems.Cache;

namespace NoREroMod.Systems.Rage;

/// <summary>
/// Demon wings sprite loop for Rage Tier 3. CPU decode + GPU upload are warmed at load so the first
/// Tier 3 activation does not hitch (subsequent activations were already smooth).
/// </summary>
internal static class RageWingsSystem
{
    private const string EffectObjectName = "RageDemonWings_XUAIGNORE";
    private static readonly string[] WingBoneCandidates = { "kubi", "pelvis", "hip", "root" };
    private const int FrameCountExpected = 28;

    private static bool WingsEnabled => Plugin.rageWingsEnable?.Value ?? true;
    /// <summary>0 = loop until <see cref="OnRageDeactivated"/> (no timer destroy).</summary>
    private static float WingsDurationSeconds => Mathf.Max(0f, Plugin.rageWingsDurationSeconds?.Value ?? 0f);
    private static float WingsFps => Mathf.Max(0.1f, Plugin.rageWingsFps?.Value ?? 24f);
    private static float WingsScale => Mathf.Max(0.01f, Plugin.rageWingsScale?.Value ?? 1f);
    private static float WingsOffsetX => Plugin.rageWingsOffsetX?.Value ?? -0.05f;
    private static float WingsOffsetY => Plugin.rageWingsOffsetY?.Value ?? 0f;

    private static readonly List<Sprite> _frames = new List<Sprite>(FrameCountExpected);
    private static bool _initialized;
    private static bool _framesLoaded;
    private static bool _gpuWarmedUp;
    private static GameObject _activeEffect;

    internal static void Initialize()
    {
        if (_initialized) return;

        try
        {
            RageSystem.OnActivated += OnRageActivated;
            RageSystem.OnDeactivated += OnRageDeactivated;
            _initialized = true;
            // Spread CPU decode + GPU warmup across frames (menu / load) so gameplay stays smooth.
            if ((Plugin.enableRageMode?.Value ?? false) && WingsEnabled)
            {
                var host = new GameObject("RageWingsWarmupHost_XUAIGNORE");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.hideFlags = HideFlags.HideAndDontSave;
                host.AddComponent<RageWingsWarmupHost>();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RageWings] Init failed: {ex.Message}");
        }
    }

    private static void OnRageActivated()
    {
        if (!WingsEnabled)
        {
            OnRageDeactivated();
            return;
        }

        if (RageSystem.CurrentTier != RageSystem.RageTier.Tier3)
        {
            OnRageDeactivated();
            return;
        }

        try
        {
            EnsureFramesLoaded();
            if (_frames.Count == 0) return;
            EnsureGpuWarmupComplete();

            if (_activeEffect != null)
            {
                UnityEngine.Object.Destroy(_activeEffect);
                _activeEffect = null;
            }

            GameObject playerObj = UnifiedPlayerCacheManager.GetPlayerObject();
            if (playerObj == null) return;

            _activeEffect = new GameObject(EffectObjectName);
            _activeEffect.transform.SetParent(playerObj.transform, false);

            SpriteRenderer sr = _activeEffect.AddComponent<SpriteRenderer>();
            ApplyPlayerSorting(sr, playerObj);
            _activeEffect.transform.localScale = Vector3.one * WingsScale;

            float computedFps = Mathf.Max(0.1f, WingsFps);
            if (_frames.Count < 2)
            {
                Plugin.Log?.LogWarning("[RageWings] Only one frame loaded. Loop animation will look static.");
            }
            Plugin.Log?.LogInfo($"[RageWings] Tier3 activate: frames={_frames.Count}, duration={WingsDurationSeconds:F2}s, fps={computedFps:F2}");

            RageWingsRunner runner = _activeEffect.AddComponent<RageWingsRunner>();
            runner.Setup(playerObj, WingBoneCandidates, _frames.ToArray(), sr, computedFps, WingsDurationSeconds, WingsOffsetX, WingsOffsetY);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[RageWings] Activate failed: {ex.Message}");
        }
    }

    private static void OnRageDeactivated()
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

        string dir = ResolveWingsDirectory();
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(dir, "frame_*.png");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[RageWings] Failed to enumerate frame files: {ex.Message}");
                files = new string[0];
            }

            if (files != null && files.Length > 0)
            {
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < files.Length; i++)
                {
                    Sprite sprite = LoadSpriteFromFile(files[i]);
                    if (sprite != null)
                        _frames.Add(sprite);
                }
            }
        }

        if (_frames.Count == 0)
        {
            Plugin.Log?.LogWarning(
                "[RageWings] No wing PNGs found. Expected folder with frame_*.png — same layouts as other Rage assets, e.g. " +
                "[Game]/sources/HellGate_sources/Rage/BlackRedWings/ or BepInEx/plugins/NoR_HellGate/sources/HellGate_sources/Rage/BlackRedWings/");
        }
        else
        {
            Plugin.Log?.LogInfo($"[RageWings] Loaded {_frames.Count} wing frame(s) from: {dir}");
        }
    }

    /// <summary>
    /// Sync GPU + sprite pipeline warmup. Used if background warmup has not finished before first Tier 3.
    /// </summary>
    private static void EnsureGpuWarmupComplete()
    {
        if (_gpuWarmedUp || _frames.Count == 0) return;
        WarmupGpuTexturesSync();
        WarmupSpriteRendererCameraOnce();
        _gpuWarmedUp = true;
        Plugin.Log?.LogInfo("[RageWings] GPU + sprite pipeline warmup (sync fallback).");
    }

    /// <summary>
    /// Spread GPU Blit across frames (menu/load), then one off-screen sprite render for shader/mesh path.
    /// </summary>
    internal static IEnumerator CoWarmupFromMenu()
    {
        if (_gpuWarmedUp) yield break;

        EnsureFramesLoaded();
        if (_frames.Count == 0) yield break;
        if (_gpuWarmedUp) yield break;

        var seen = new HashSet<int>();
        int blitIndex = 0;
        for (int i = 0; i < _frames.Count; i++)
        {
            if (_gpuWarmedUp) yield break;

            Sprite sprite = _frames[i];
            if (sprite == null || sprite.texture == null) continue;
            Texture2D tex = sprite.texture;
            int id = tex.GetInstanceID();
            if (!seen.Add(id)) continue;

            int w = Mathf.Clamp(tex.width, 8, 512);
            int h = Mathf.Clamp(tex.height, 8, 512);
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0);
            try
            {
                Graphics.Blit(tex, rt);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
            }

            blitIndex++;
            if (blitIndex % 2 == 0)
            {
                yield return null;
            }
        }

        if (_gpuWarmedUp) yield break;

        WarmupSpriteRendererCameraOnce();
        _gpuWarmedUp = true;
        Plugin.Log?.LogInfo("[RageWings] GPU + sprite pipeline warmup (background) complete.");
    }

    /// <summary>
    /// Same layout rules as <see cref="RageVisualEffectsSystem"/> (Rage sounds / blood banners): portable
    /// sources/HellGate_sources/Rage/, optional parent-folder install, and BepInEx/plugins/NoR_HellGate/sources/...
    /// </summary>
    private static string ResolveWingsDirectory()
    {
        string gameRoot = Application.dataPath;
        if (gameRoot.EndsWith("_Data"))
            gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

        var list = new List<string>(12);

        // [Game]/sources/HellGate_sources/Rage/BlackRedWings
        AddUniqueWingPath(list, Combine7(gameRoot, "sources", "HellGate_sources", "Rage", "BlackRedWings"));
        // Some installs (see RageVisualEffectsSystem path2)
        AddUniqueWingPath(list, Combine7(Path.Combine(gameRoot, ".."), "sources", "HellGate_sources", "Rage", "BlackRedWings"));
        // BepInEx/plugins/NoR_HellGate/sources/HellGate_sources/Rage/BlackRedWings (AssemblyName NoR_HellGate)
        {
            string p = gameRoot;
            p = Path.Combine(p, "BepInEx");
            p = Path.Combine(p, "plugins");
            p = Path.Combine(p, "NoR_HellGate");
            p = Path.Combine(p, "sources");
            p = Path.Combine(p, "HellGate_sources");
            p = Path.Combine(p, "Rage");
            p = Path.Combine(p, "BlackRedWings");
            AddUniqueWingPath(list, p);
        }
        // Legacy plugin layouts
        AddUniqueWingPath(list, Combine6(gameRoot, "BepInEx", "plugins", "HellGate_sources", "Rage", "BlackRedWings"));
        AddUniqueWingPath(list, Combine6(gameRoot, "BepInEx", "plugins", "HellGate", "Rage", "BlackRedWings"));

        try
        {
            string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(dllDir))
            {
                AddUniqueWingPath(list, Combine7(dllDir, "sources", "HellGate_sources", "Rage", "BlackRedWings"));
                AddUniqueWingPath(list, Combine4(dllDir, "HellGate_sources", "Rage", "BlackRedWings"));
            }
        }
        catch { }

        for (int i = 0; i < list.Count; i++)
        {
            if (Directory.Exists(list[i]))
                return list[i];
        }

        return list.Count > 0 ? list[0] : Combine7(gameRoot, "sources", "HellGate_sources", "Rage", "BlackRedWings");
    }

    private static void AddUniqueWingPath(List<string> list, string path)
    {
        try
        {
            string p = Path.GetFullPath(path);
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], p, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            list.Add(p);
        }
        catch { }
    }

    private static string Combine4(string a, string b, string c, string d)
    {
        return Path.Combine(Path.Combine(Path.Combine(a, b), c), d);
    }

    private static string Combine6(string root, string a, string b, string c, string d, string e)
    {
        return Path.Combine(Path.Combine(Path.Combine(Path.Combine(Path.Combine(root, a), b), c), d), e);
    }

    private static string Combine7(string root, string a, string b, string c, string d)
    {
        return Path.Combine(Path.Combine(Path.Combine(Path.Combine(root, a), b), c), d);
    }

    private static Sprite LoadSpriteFromFile(string filePath)
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
            Plugin.Log?.LogWarning($"[RageWings] Failed loading frame {filePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Forces each texture through a Blit so VRAM upload happens before first real draw.
    /// </summary>
    private static void WarmupGpuTexturesSync()
    {
        try
        {
            var seen = new HashSet<int>();
            for (int i = 0; i < _frames.Count; i++)
            {
                Sprite sprite = _frames[i];
                if (sprite == null || sprite.texture == null) continue;
                Texture2D tex = sprite.texture;
                int id = tex.GetInstanceID();
                if (!seen.Add(id)) continue;

                int w = Mathf.Clamp(tex.width, 8, 512);
                int h = Mathf.Clamp(tex.height, 8, 512);
                RenderTexture rt = RenderTexture.GetTemporary(w, h, 0);
                Graphics.Blit(tex, rt);
                RenderTexture.ReleaseTemporary(rt);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[RageWings] GPU texture warmup: {ex.Message}");
        }
    }

    /// <summary>
    /// One off-screen camera render of the first frame so SpriteRenderer / sprite shader path is compiled and ready.
    /// </summary>
    private static void WarmupSpriteRendererCameraOnce()
    {
        if (_frames.Count == 0 || _frames[0] == null) return;

        const int kWarmupLayer = 31;
        RenderTexture rt = null;
        GameObject camGo = null;
        GameObject sprGo = null;

        try
        {
            sprGo = new GameObject("RageWingsWarmupSprite_XUAIGNORE");
            sprGo.layer = kWarmupLayer;
            sprGo.transform.position = new Vector3(10000f, 10000f, 0f);
            SpriteRenderer sr = sprGo.AddComponent<SpriteRenderer>();
            sr.sprite = _frames[0];

            camGo = new GameObject("RageWingsWarmupCamera_XUAIGNORE");
            UnityEngine.Camera cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << kWarmupLayer;
            cam.orthographic = true;
            cam.orthographicSize = 2f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 50f;
            cam.transform.position = new Vector3(10000f, 10000f, -10f);

            rt = RenderTexture.GetTemporary(64, 64, 0);
            cam.targetTexture = rt;
            cam.enabled = false;
            cam.Render();

            RenderTexture.ReleaseTemporary(rt);
            rt = null;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[RageWings] Sprite render warmup: {ex.Message}");
        }
        finally
        {
            if (rt != null)
            {
                RenderTexture.ReleaseTemporary(rt);
            }
            if (sprGo != null)
            {
                UnityEngine.Object.Destroy(sprGo);
            }
            if (camGo != null)
            {
                UnityEngine.Object.Destroy(camGo);
            }
        }
    }

    private static void ApplyPlayerSorting(SpriteRenderer sr, GameObject playerObj)
    {
        string layerName = "Default";
        int order = -1;

        Renderer renderer = playerObj.GetComponent<Renderer>();
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

/// <summary>
/// Runs <see cref="RageWingsSystem.CoWarmupFromMenu"/> after a couple of frames so decode/GPU work does not hit the first gameplay frame.
/// </summary>
internal sealed class RageWingsWarmupHost : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return null;
        yield return null;
        yield return RageWingsSystem.CoWarmupFromMenu();
        Destroy(gameObject);
    }
}

internal class RageWingsRunner : MonoBehaviour
{
    private SkeletonAnimation _spine;
    private Spine.Bone _bone;
    private bool _useSpineRootFallback;
    private playercon _player;
    private Sprite[] _frames;
    private SpriteRenderer _renderer;
    private float _frameDuration;
    private float _timer;
    private int _frameIndex;
    private float _offsetX;
    private float _offsetY;
    private float _durationSeconds;
    private float _elapsed;
    private bool _initialized;

    internal void Setup(
        GameObject playerObj,
        string[] boneNameCandidates,
        Sprite[] frames,
        SpriteRenderer renderer,
        float fps,
        float durationSeconds,
        float offsetX,
        float offsetY)
    {
        _frames = frames;
        _renderer = renderer;
        _frameDuration = 1f / Mathf.Max(1f, fps);
        _durationSeconds = Mathf.Max(0f, durationSeconds);
        _offsetX = offsetX;
        _offsetY = offsetY;

        _spine = playerObj.GetComponentInChildren<SkeletonAnimation>(true);
        _player = playerObj.GetComponent<playercon>();
        if (_spine != null && _spine.skeleton != null && boneNameCandidates != null)
        {
            foreach (var name in boneNameCandidates)
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
        if (ShouldStopForHScene())
        {
            UnityEngine.Object.Destroy(gameObject);
            return;
        }

        if (_spine == null || _spine.skeleton == null)
        {
            UnityEngine.Object.Destroy(gameObject);
            return;
        }

        if (_durationSeconds > 0f)
        {
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= _durationSeconds)
            {
                UnityEngine.Object.Destroy(gameObject);
                return;
            }
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

    private bool ShouldStopForHScene()
    {
        if (_player == null) return false;
        if (_player.eroflag) return true;
        if (_player.erodown != 0)
        {
            // Do not remove Tier3 wings because knockdown fired; immunity should prevent erodown, but avoid one-frame destroy.
            if (RageSystem.IsActive && RageSystem.CurrentTier == RageSystem.RageTier.Tier3)
                return false;
            return true;
        }

        return false;
    }
}
