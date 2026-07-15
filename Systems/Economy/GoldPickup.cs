using System.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoREroMod.Systems.Economy;

/// <summary>
/// MonoBehaviour attached to a runtime gold pile. Mirrors the surface of vanilla
/// <c>Pickup_Dropitem</c>: trigger on <c>playerDAMAGEcol</c>, then pick up via
/// <c>playercon._key_submit</c> (Rewired Submit / keyboard E).
///
/// Also runs the pickup loop animation and an optional coroutine-based "drop arc"
/// so newly spawned piles bounce into place. No <see cref="Rigidbody2D"/> is added —
/// the player's own <c>playerDAMAGEcol</c> hurtbox carries the rigidbody required for
/// 2D trigger detection (same setup vanilla pickups rely on).
/// </summary>
internal sealed class GoldPickup : MonoBehaviour
{
    private const string PlayerDamageColTag = "playerDAMAGEcol";

    private long _amount;
    private bool _isLostPile;
    private bool _suppressDropSfx;
    private bool _staticPlacement;
    private bool _consumed;
    private bool _stayflag;

    private SpriteRenderer _renderer;
    private playercon _player;

    private float _frameTimer;
    private int _frameIndex;
    private float _frameDuration;

    private bool _physicsActive;
    private float _physicsVx;
    private float _physicsVy;
    private float _startY;

    public void Initialize(long amount, bool isLostPile, bool suppressDropSfx = false, bool staticPlacement = false)
    {
        _amount = amount;
        _isLostPile = isLostPile;
        _suppressDropSfx = suppressDropSfx;
        _staticPlacement = staticPlacement;
    }

    private void Start()
    {
        _renderer = GetComponent<SpriteRenderer>();
        if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();

        if (GoldAssetLoader.HasFrames)
            _renderer.sprite = GoldAssetLoader.PickupFrames[0];

        _frameDuration = 1f / Mathf.Max(1, EconomicConfig.AnimFps);

        ApplyPlayerSorting();

        if (EconomicConfig.PhysicsEnabled && !_isLostPile && !_staticPlacement)
        {
            _physicsActive = true;
            _physicsVx = Random.Range(-EconomicConfig.PhysicsInitialVelocityX, EconomicConfig.PhysicsInitialVelocityX);
            _physicsVy = Random.Range(EconomicConfig.PhysicsInitialVelocityY * 0.85f, EconomicConfig.PhysicsInitialVelocityY * 1.15f);
            _startY = transform.position.y;
        }

        if (!_isLostPile && !_suppressDropSfx && EconomicConfig.Audio.Enable && GoldAssetLoader.HasDropClip)
        {
            GoldAudioPlayer.Play2D(GoldAssetLoader.DropClip, EconomicConfig.Audio.DropVolume);
        }
    }

    private void ApplyPlayerSorting()
    {
        try
        {
            GameObject playerObj = NoREroMod.Systems.Cache.UnifiedPlayerCacheManager.GetPlayerObject();
            if (playerObj == null) return;

            Renderer playerRenderer = playerObj.GetComponentInChildren<SpriteRenderer>(true);
            if (playerRenderer == null) playerRenderer = playerObj.GetComponentInChildren<MeshRenderer>(true);
            if (playerRenderer == null) return;

            _renderer.sortingLayerName = playerRenderer.sortingLayerName;
            _renderer.sortingOrder = playerRenderer.sortingOrder - 1;
        }
        catch (System.Exception ex)
        {
            Plugin.Log?.LogWarning("[GoldPickup] ApplyPlayerSorting failed: " + ex.Message);
        }
    }

    private void Update()
    {
        AdvanceAnimation();

        if (_consumed) return;

        if (_player == null) _player = ResolvePlayer();

        if (_stayflag && _player != null && _player._key_submit)
            Collect();
    }

    private void FixedUpdate()
    {
        if (!_physicsActive || _consumed) return;

        Vector3 p = transform.position;
        float dt = Time.fixedDeltaTime;
        p.x += _physicsVx * dt;
        p.y += _physicsVy * dt;
        _physicsVy -= EconomicConfig.PhysicsGravity * dt;
        transform.position = p;

        if (_physicsVy < 0f && p.y <= _startY)
        {
            p.y = _startY;
            transform.position = p;
            _physicsActive = false;
            StartCoroutine(BounceSquash());
        }
    }

    private IEnumerator BounceSquash()
    {
        float t = 0f;
        float duration = Mathf.Max(0.01f, EconomicConfig.PhysicsBounceTime);
        float peak = Mathf.Max(1f, EconomicConfig.PhysicsBounceScale);
        Vector3 baseScale = transform.localScale;
        while (t < duration)
        {
            t += Time.deltaTime;
            float n = t / duration;
            float k = n < 0.5f
                ? Mathf.Lerp(1f, peak, n / 0.5f)
                : Mathf.Lerp(peak, 1f, (n - 0.5f) / 0.5f);
            transform.localScale = baseScale * k;
            yield return null;
        }
        transform.localScale = baseScale;
    }

    private void AdvanceAnimation()
    {
        if (_renderer == null) return;
        int count = GoldAssetLoader.HasFrames ? GoldAssetLoader.PickupFrames.Count : 0;
        if (count <= 1) return;

        _frameTimer += Time.deltaTime;
        if (_frameTimer < _frameDuration) return;
        _frameTimer = 0f;
        _frameIndex = (_frameIndex + 1) % count;
        _renderer.sprite = GoldAssetLoader.PickupFrames[_frameIndex];
    }

    private static playercon ResolvePlayer()
    {
        GameObject obj = GameObject.FindGameObjectWithTag("Player");
        return obj != null ? obj.GetComponent<playercon>() : null;
    }

    private void OnTriggerEnter2D(Collider2D coll) => HandlePlayerOverlap(coll, true);
    private void OnTriggerStay2D(Collider2D coll) => HandlePlayerOverlap(coll, true);
    private void OnTriggerExit2D(Collider2D coll) => HandlePlayerOverlap(coll, false);

    private void HandlePlayerOverlap(Collider2D coll, bool inside)
    {
        if (coll == null || coll.gameObject == null) return;
        if (!string.Equals(coll.gameObject.tag, PlayerDamageColTag)) return;
        _stayflag = inside;
        if (inside && _player == null) _player = ResolvePlayer();
    }

    private void Collect()
    {
        if (_consumed) return;
        _consumed = true;

        long awarded = _amount;
        GoldWallet.ModifyGold(awarded);

        if (_isLostPile)
            GoldStaticMng.Clear();

        if (EconomicConfig.Popup.Enable)
            GoldPopupSystem.ShowOverPlayer(awarded);

        if (EconomicConfig.Audio.Enable && GoldAssetLoader.HasPickupClip)
        {
            AudioClip clip = ChoosePickupClip();
            GoldAudioPlayer.Play2D(clip, EconomicConfig.Audio.PickupVolume);
        }

        if (EconomicConfig.DebugLogging)
            Plugin.Log?.LogInfo($"[GoldPickup] Collected {awarded} (lost={_isLostPile}, wallet={GoldWallet.Current})");

        Destroy(gameObject);
    }

    private static AudioClip ChoosePickupClip()
    {
        var clips = GoldAssetLoader.PickupClips;
        if (clips == null || clips.Count == 0) return null;
        if (clips.Count == 1 || !EconomicConfig.Audio.RandomizePickup) return clips[0];
        return clips[Random.Range(0, clips.Count)];
    }

    /// <summary>
    /// Destroys every uncollected pile in the scene (enemy drops, config spawns, lost-pile visual).
    /// Wallet and <see cref="GoldStaticMng"/> are untouched — a souls-style lost pile can respawn on scene re-entry.
    /// </summary>
    internal static void CleanupAllUncollectedInScene()
    {
        if (!EconomicConfig.Enable)
            return;

        GoldPickup[] pickups = Object.FindObjectsOfType<GoldPickup>();
        if (pickups == null || pickups.Length == 0)
            return;

        int removed = 0;
        for (int i = 0; i < pickups.Length; i++)
        {
            GoldPickup pickup = pickups[i];
            if (pickup == null)
                continue;

            GameObject go = pickup.gameObject;
            if (go == null)
                continue;

            Object.Destroy(go);
            removed++;
        }

        if (removed > 0 && EconomicConfig.DebugLogging)
            Plugin.Log?.LogInfo($"[GoldPickup] Removed {removed} uncollected pile(s) on spawn refresh.");
    }
}
