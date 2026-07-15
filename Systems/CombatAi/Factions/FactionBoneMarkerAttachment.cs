using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.CombatAi.Factions;

internal sealed class FactionBoneMarkerAttachment : MonoBehaviour
{
    private const string MarkerObjectName = "FactionBoneMarker_XUAIGNORE";
    // Fallback list — covers humanoid bandits, inquisition, mafia, demons
    // and most monsters. The first bone found wins.
    private static readonly string[] BoneNames =
    {
        "bone6", "bone27", "head", "neck", "body_ue", "body", "hips", "root"
    };
    private const float OffsetX = 0f;
    private const float OffsetY = 0.15f;
    private const float MarkerScale = 0.84f;

    private static Sprite _markerSprite;

    private EnemyDate _enemy;
    private SkeletonAnimation _spine;
    private Bone _bone;
    private string _boneName;
    private Skeleton _skeletonRef;
    private GameObject _markerObject;
    private SpriteRenderer _markerRenderer;
    private bool _initialized;
    private bool _loggedBoneMissing;
    private bool _hiddenForHScene;
    private float _visualScale = MarkerScale;
    private float _visualOffsetX = OffsetX;
    private float _visualOffsetY = OffsetY;
    private string[] _preferredBones;

    public static void Ensure(EnemyDate enemy, Color color)
    {
        if (enemy == null || enemy.gameObject == null)
            return;

        // Do not decorate neutral enemies (no meaningful faction).
        int factionId = EnemyFactionRuntime.GetFaction(enemy.gameObject);
        if (FactionIds.IsPassiveNonCombat(factionId))
            return;

        FactionBoneMarkerAttachment attachment = enemy.GetComponent<FactionBoneMarkerAttachment>();
        if (attachment == null)
            attachment = enemy.gameObject.AddComponent<FactionBoneMarkerAttachment>();

        attachment.ApplyVisual(color, factionId);
    }

    internal static void Remove(EnemyDate enemy)
    {
        if (enemy == null || enemy.gameObject == null)
            return;

        FactionBoneMarkerAttachment attachment = enemy.GetComponent<FactionBoneMarkerAttachment>();
        attachment?.Dismiss();
    }

    private void Dismiss()
    {
        if (_markerObject != null)
            Destroy(_markerObject);

        Destroy(this);
    }

    private void Awake()
    {
        _enemy = GetComponent<EnemyDate>();
    }

    private void Start()
    {
        TryInitialize();
    }

    private void LateUpdate()
    {
        if (!_initialized)
            TryInitialize();
        if (!_initialized)
            return;

        if (_spine == null || _spine.skeleton == null || _bone == null || _markerObject == null)
        {
            _initialized = false;
            return;
        }

        // Some enemies rebuild their SkeletonAnimation at runtime (e.g. Wolf swaps the
        // MummyDog skeleton via Initialize(true)). The rebuild creates a brand-new Skeleton
        // with new Bone instances; our cached _bone becomes a stale (non-null) orphan that
        // freezes the emblem in place. Detect the swap and re-resolve the bone by name.
        if (!ReferenceEquals(_spine.skeleton, _skeletonRef))
        {
            Bone rebound = !string.IsNullOrEmpty(_boneName) ? _spine.skeleton.FindBone(_boneName) : null;
            if (rebound == null)
                rebound = FindBestBone(_spine.skeleton, _preferredBones ?? BoneNames, out _boneName);
            if (rebound == null)
            {
                _initialized = false;
                return;
            }
            _bone = rebound;
            _skeletonRef = _spine.skeleton;
        }

        bool hideForHScene = ShouldHideForHScene();
        if (hideForHScene)
        {
            if (_markerRenderer != null)
                _markerRenderer.enabled = false;
            _hiddenForHScene = true;
            return;
        }
        if (_hiddenForHScene)
        {
            _hiddenForHScene = false;
            if (_markerRenderer != null)
                _markerRenderer.enabled = true;
        }

        Vector3 world = _spine.transform.TransformPoint(_bone.WorldX + _visualOffsetX, _bone.WorldY + _visualOffsetY, 0f);
        _markerObject.transform.position = world;
        Vector3 s = _markerObject.transform.localScale;
        float dir = Mathf.Sign(transform.lossyScale.x);
        if (Mathf.Approximately(dir, 0f))
            dir = 1f;
        s.x = Mathf.Abs(_visualScale) * dir;
        s.y = Mathf.Abs(_visualScale);
        s.z = 1f;
        _markerObject.transform.localScale = s;
    }

    private void TryInitialize()
    {
        if (_enemy == null || _enemy.gameObject == null)
            return;

        _spine = _enemy.GetComponentInChildren<SkeletonAnimation>(true);
        if (_spine == null || _spine.skeleton == null)
            return;

        _bone = FindBestBone(_spine.skeleton, _preferredBones ?? BoneNames, out _boneName);
        if (_bone == null)
        {
            if (EnemyFactionsConfig.DebugLogging && !_loggedBoneMissing)
            {
                _loggedBoneMissing = true;
                Plugin.Log?.LogWarning("[EnemyFactions] Marker bone not found for " + _enemy.GetType().Name + " (tried bone6/head/body/body_ue).");
            }
            return;
        }
        _skeletonRef = _spine.skeleton;

        if (_markerObject == null)
        {
            _markerObject = new GameObject(MarkerObjectName);
            _markerObject.transform.SetParent(_enemy.transform, false);
            _markerObject.transform.localScale = Vector3.one * MarkerScale;
            _markerRenderer = _markerObject.AddComponent<SpriteRenderer>();
            ApplySortingFromOwner(_markerRenderer);
        }

        if (_markerRenderer == null)
            return;

        if (_markerRenderer.sprite == null)
        {
            EnsureMarkerSprite();
            if (_markerSprite == null)
                return;
            _markerRenderer.sprite = _markerSprite;
        }

        _initialized = true;
    }

    private void ApplyVisual(Color color, int factionId)
    {
        if (!_initialized)
            TryInitialize();
        if (_markerRenderer == null)
            return;

        if (FactionStyle.TryGetIconStyle(factionId, out FactionStyle.IconStyle style))
        {
            FactionStyle.IconStyle resolvedStyle = FactionStyle.ResolveForEnemy(_enemy, style);
            _visualScale = resolvedStyle.Scale > 0f ? resolvedStyle.Scale : MarkerScale;
            _visualOffsetX = resolvedStyle.OffsetX;
            _visualOffsetY = resolvedStyle.OffsetY;
            _preferredBones = resolvedStyle.PreferredBones;
            if (resolvedStyle.Icon != null)
                _markerRenderer.sprite = resolvedStyle.Icon;
            _markerRenderer.color = Color.white;
        }
        else
        {
            _visualScale = MarkerScale;
            _visualOffsetX = OffsetX;
            _visualOffsetY = OffsetY;
            _preferredBones = BoneNames;
            EnsureMarkerSprite();
            if (_markerSprite != null)
                _markerRenderer.sprite = _markerSprite;
            _markerRenderer.color = color;
        }

        _markerRenderer.enabled = !_hiddenForHScene;
    }

    private bool ShouldHideForHScene()
    {
        if (_enemy == null)
            return false;
        if (_enemy.com_player == null || !_enemy.com_player.eroflag)
            return false;

        // Hide only for the enemy that is currently in active ERO state.
        return _enemy.eroflag;
    }

    private static void EnsureMarkerSprite()
    {
        if (_markerSprite != null)
            return;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float center = (size - 1) * 0.5f;
        float radius = size * 0.42f;
        float edge = size * 0.06f;
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color solid = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= radius - edge)
                {
                    tex.SetPixel(x, y, solid);
                }
                else if (d <= radius + edge)
                {
                    float t = Mathf.InverseLerp(radius + edge, radius - edge, d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, t));
                }
                else
                {
                    tex.SetPixel(x, y, clear);
                }
            }
        }

        tex.Apply(false, false);
        _markerSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Bone FindBestBone(Skeleton skeleton, string[] candidateBones, out string matchedName)
    {
        matchedName = null;
        if (skeleton == null)
            return null;
        if (candidateBones == null || candidateBones.Length == 0)
            return null;

        for (int i = 0; i < candidateBones.Length; i++)
        {
            Bone bone = skeleton.FindBone(candidateBones[i]);
            if (bone != null)
            {
                matchedName = candidateBones[i];
                return bone;
            }
        }
        return null;
    }

    private void ApplySortingFromOwner(SpriteRenderer sr)
    {
        if (sr == null)
            return;

        string layer = "Default";
        int order = 30000;

        Renderer ownerRenderer = GetComponent<Renderer>();
        if (ownerRenderer == null)
            ownerRenderer = GetComponentInChildren<Renderer>(true);
        if (ownerRenderer != null)
        {
            layer = ownerRenderer.sortingLayerName;
            order = ownerRenderer.sortingOrder + 10;
        }

        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
    }
}
