using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Patches.Enemy.HeckGateEnemy;

/// <summary>
/// Attaches animated biscord eyes sprite strip to Spine bone "body_ue".
/// Loop is infinite while object exists.
/// </summary>
internal sealed class BiscodEyesAttachment : MonoBehaviour
{
    private const string BoneName = "body_ue";
    private const string EyesObjectName = "BiscodEyes_XUAIGNORE";
    private const float EyesFps = 8f;
    private const float OffsetX = 0f;
    private const float OffsetY = 0f;
    private const float EyesScale = 1f;
    private const float TearsSecondsAfterHit = 1.25f;

    private enum EyesMood
    {
        Normal,
        Attack,
        Tears
    }

    private static readonly Dictionary<EyesMood, List<Sprite>> FramesByMood = new Dictionary<EyesMood, List<Sprite>>
    {
        { EyesMood.Normal, new List<Sprite>(40) },
        { EyesMood.Attack, new List<Sprite>(40) },
        { EyesMood.Tears, new List<Sprite>(40) }
    };

    private static bool FramesLoaded;
    private static string FramesRootDirectory;

    private suraimu _slime;
    private SkeletonAnimation _spine;
    private Bone _bone;
    private GameObject _eyesObject;
    private SpriteRenderer _eyesRenderer;
    private float _frameTimer;
    private int _frameIndex;
    private bool _initialized;
    private float _lastHp = -1f;
    private float _tearsTimer;
    private EyesMood _currentMood = EyesMood.Normal;

    private void Awake()
    {
        _slime = GetComponent<suraimu>();
    }

    private void Start()
    {
        TryInitialize();
    }

    private void LateUpdate()
    {
        if (!_initialized) TryInitialize();
        if (!_initialized) return;
        if (_slime == null || _spine == null || _spine.skeleton == null || _bone == null)
        {
            SafeDestroyEyes();
            _initialized = false;
            return;
        }

        transformEyesToBone();
        animateFrames();
    }

    private void OnDestroy()
    {
        SafeDestroyEyes();
    }

    private void TryInitialize()
    {
        EnsureFramesLoaded();
        if (GetFrames(EyesMood.Normal).Count == 0) return;

        _spine = GetComponent<SkeletonAnimation>();
        if (_spine == null || _spine.skeleton == null) return;
        _bone = _spine.skeleton.FindBone(BoneName);
        if (_bone == null) return;

        if (_eyesObject == null)
        {
            _eyesObject = new GameObject(EyesObjectName);
            _eyesObject.transform.SetParent(transform, false);
            _eyesObject.transform.localScale = Vector3.one * EyesScale;
            _eyesRenderer = _eyesObject.AddComponent<SpriteRenderer>();
            ApplySortingFromOwner(_eyesRenderer);
        }

        _frameIndex = 0;
        _frameTimer = 0f;
        _currentMood = EyesMood.Normal;
        _lastHp = _slime != null ? _slime.Hp : -1f;
        SetSpriteSafe(GetFrames(_currentMood), 0);
        _initialized = true;
    }

    private void transformEyesToBone()
    {
        _eyesObject.transform.position = _spine.transform.TransformPoint(_bone.WorldX + OffsetX, _bone.WorldY + OffsetY, 0f);
        Vector3 s = _eyesObject.transform.localScale;
        float dir = Mathf.Sign(transform.lossyScale.x);
        if (Mathf.Approximately(dir, 0f)) dir = 1f;
        s.x = Mathf.Abs(EyesScale) * dir;
        s.y = Mathf.Abs(EyesScale);
        s.z = 1f;
        _eyesObject.transform.localScale = s;
    }

    private void animateFrames()
    {
        if (_eyesRenderer == null || _slime == null) return;

        UpdateMoodByCombatState();
        List<Sprite> activeFrames = GetFrames(_currentMood);
        if (activeFrames.Count == 0)
            activeFrames = GetFrames(EyesMood.Normal);
        if (activeFrames.Count == 0) return;

        float frameDuration = 1f / Mathf.Max(1f, EyesFps);
        _frameTimer += Time.unscaledDeltaTime;
        while (_frameTimer >= frameDuration)
        {
            _frameTimer -= frameDuration;
            _frameIndex = (_frameIndex + 1) % activeFrames.Count;
            _eyesRenderer.sprite = activeFrames[_frameIndex];
        }
    }

    private void UpdateMoodByCombatState()
    {
        float hpNow = _slime.Hp;
        if (_lastHp < 0f) _lastHp = hpNow;
        if (hpNow < _lastHp - 0.01f)
            _tearsTimer = TearsSecondsAfterHit;
        _lastHp = hpNow;

        if (_tearsTimer > 0f)
            _tearsTimer = Mathf.Max(0f, _tearsTimer - Time.deltaTime);

        EyesMood nextMood;
        if (_tearsTimer > 0f)
        {
            nextMood = EyesMood.Tears;
        }
        else
        {
            bool attacking =
                _slime.state == suraimu.enemystate.ATK1 ||
                _slime.state == suraimu.enemystate.ATK2 ||
                _slime.state == suraimu.enemystate.ATK3 ||
                _slime.state == suraimu.enemystate.ATK4;
            nextMood = attacking ? EyesMood.Attack : EyesMood.Normal;
        }

        if (nextMood == _currentMood) return;
        _currentMood = nextMood;
        _frameIndex = 0;
        _frameTimer = 0f;
        SetSpriteSafe(GetFrames(_currentMood), 0);
    }

    private void SetSpriteSafe(List<Sprite> frames, int index)
    {
        if (_eyesRenderer == null || frames == null || frames.Count == 0) return;
        if (index < 0 || index >= frames.Count) index = 0;
        _eyesRenderer.sprite = frames[index];
    }

    private void ApplySortingFromOwner(SpriteRenderer sr)
    {
        string layer = "Default";
        int order = 1;

        Renderer ownerRenderer = GetComponent<Renderer>();
        if (ownerRenderer == null) ownerRenderer = GetComponentInChildren<Renderer>();
        if (ownerRenderer != null)
        {
            layer = ownerRenderer.sortingLayerName;
            order = ownerRenderer.sortingOrder + 1;
        }

        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
    }

    private void SafeDestroyEyes()
    {
        if (_eyesObject != null)
        {
            Destroy(_eyesObject);
            _eyesObject = null;
            _eyesRenderer = null;
        }
    }

    private static void EnsureFramesLoaded()
    {
        if (FramesLoaded) return;
        FramesLoaded = true;

        FramesRootDirectory = ResolveEyesDirectory();
        LoadMoodFrames(EyesMood.Normal, "Normal");
        LoadMoodFrames(EyesMood.Attack, "Attack");
        LoadMoodFrames(EyesMood.Tears, "Tears");

        int normal = GetFrames(EyesMood.Normal).Count;
        int attack = GetFrames(EyesMood.Attack).Count;
        int tears = GetFrames(EyesMood.Tears).Count;
        Plugin.Log?.LogInfo($"[biscord][eyes] Loaded frames: Normal={normal}, Attack={attack}, Tears={tears} from: {FramesRootDirectory}");

        if (normal == 0)
            Plugin.Log?.LogWarning("[biscord][eyes] Normal folder has no frames. Eye animation disabled.");
        if (attack == 0)
            Plugin.Log?.LogWarning("[biscord][eyes] Attack folder empty. Fallback to Normal.");
        if (tears == 0)
            Plugin.Log?.LogWarning("[biscord][eyes] Tears folder empty. Fallback to Normal.");
    }

    private static string ResolveEyesDirectory()
    {
        string gameRoot = Application.dataPath;
        if (gameRoot.EndsWith("_Data"))
            gameRoot = gameRoot.Substring(0, gameRoot.Length - 5);

        var list = new List<string>(10);
        AddUniquePath(list, Combine(gameRoot, "sources", "HellGate_sources", "HeckGateMobs", "biscord_eyes"));
        AddUniquePath(list, Combine(Path.Combine(gameRoot, ".."), "sources", "HellGate_sources", "HeckGateMobs", "biscord_eyes"));
        AddUniquePath(list, Combine(gameRoot, "BepInEx", "plugins", "NoR_HellGate", "sources", "HellGate_sources", "HeckGateMobs", "biscord_eyes"));

        try
        {
            string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(dllDir))
            {
                AddUniquePath(list, Combine(dllDir, "sources", "HellGate_sources", "HeckGateMobs", "biscord_eyes"));
                AddUniquePath(list, Combine(dllDir, "HellGate_sources", "HeckGateMobs", "biscord_eyes"));
            }
        }
        catch { }

        for (int i = 0; i < list.Count; i++)
        {
            if (Directory.Exists(list[i]))
                return list[i];
        }

        return list.Count > 0 ? list[0] : Combine(gameRoot, "sources", "HellGate_sources", "HeckGateMobs", "biscord_eyes");
    }

    private static List<Sprite> GetFrames(EyesMood mood)
    {
        if (FramesByMood.TryGetValue(mood, out List<Sprite> list) && list != null)
            return list;
        return FramesByMood[EyesMood.Normal];
    }

    private static void LoadMoodFrames(EyesMood mood, string folderName)
    {
        List<Sprite> target = GetFrames(mood);
        target.Clear();

        if (string.IsNullOrEmpty(FramesRootDirectory)) return;
        string dir = Path.Combine(FramesRootDirectory, folderName);
        if (!Directory.Exists(dir)) return;

        string[] files;
        try
        {
            files = Directory.GetFiles(dir, "frame_*.png");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[biscord][eyes] Failed to enumerate {folderName} frames: {ex.Message}");
            return;
        }

        for (int i = 0; i < files.Length; i++)
        {
            Sprite sprite = LoadSpriteFromFile(files[i]);
            if (sprite != null) target.Add(sprite);
        }
    }

    private static string Combine(string root, params string[] parts)
    {
        string path = root;
        for (int i = 0; i < parts.Length; i++)
            path = Path.Combine(path, parts[i]);
        return path;
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

            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[biscord][eyes] Failed loading frame {filePath}: {ex.Message}");
            return null;
        }
    }
}
