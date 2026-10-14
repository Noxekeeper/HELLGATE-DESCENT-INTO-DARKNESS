using System;
using System.Collections.Generic;
using System.Reflection;
using NoREroMod.Systems.Cache;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.Fear;

/// <summary>
/// Applies H-phase Fear beats from FearEnemies.json (<c>enemies[].hPhases</c>).
/// Code hooks only detect the moment (spine event or clip change); values live in JSON.
/// </summary>
internal static class FearHPhases
{
    private const float DedupeSeconds = 1.5f;

    private static readonly Dictionary<string, float> _lastApplied =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

    internal static FearHPhaseBeat Find(string enemyKey, string phase)
    {
        if (string.IsNullOrEmpty(phase))
            return null;
        FearHPhaseBeat[] phases = FearCatalog.GetHPhases(enemyKey);
        if (phases == null)
            return null;
        for (int i = 0; i < phases.Length; i++)
        {
            FearHPhaseBeat beat = phases[i];
            if (beat != null && string.Equals(beat.animation, phase, StringComparison.OrdinalIgnoreCase))
                return beat;
        }
        return null;
    }

    /// <summary>One-shot beat on entering <paramref name="phase"/>. Same beat is ignored for 1.5 s.</summary>
    internal static void Enter(string enemyKey, string phase)
    {
        try
        {
            if (!FearConfig.IsEnabled)
                return;
            FearHPhaseBeat beat = Find(enemyKey, phase);
            if (beat == null || (!beat.absolute && Mathf.Abs(beat.spikeMorale) < 0.0001f))
                return;

            string reason = enemyKey + "_" + phase;
            float now = Time.unscaledTime;
            if (_lastApplied.TryGetValue(reason, out float last) && now - last < DedupeSeconds)
                return;
            _lastApplied[reason] = now;

            if (beat.absolute)
                FearSystem.SetMoraleDirect(beat.spikeMorale, reason);
            else
                FearSystem.NotifyHPhaseSpike(beat.spikeMorale, reason);
            Plugin.Log?.LogInfo("[Fear] " + enemyKey + " " + phase + " Morale " + FearSystem.Morale.ToString("0.#"));
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Fear] " + enemyKey + " " + phase + " beat failed: " + ex.Message);
        }
    }

    internal static SkeletonAnimation ResolveSpine(MonoBehaviour host)
    {
        if (host == null)
            return null;
        try
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            Type type = host.GetType();
            var spine = (type.GetField("myspine", flags) ?? type.GetField("mySpine", flags))
                ?.GetValue(host) as SkeletonAnimation;
            return spine
                ?? host.GetComponent<SkeletonAnimation>()
                ?? host.GetComponentInChildren<SkeletonAnimation>(true);
        }
        catch
        {
            return null;
        }
    }

    internal static string ResolveAnimName(SkeletonAnimation spine)
    {
        if (spine == null)
            return string.Empty;
        try
        {
            if (!string.IsNullOrEmpty(spine.AnimationName))
                return spine.AnimationName;
            TrackEntry entry = spine.state?.GetCurrent(0);
            if (entry != null && entry.Animation != null)
                return entry.Animation.Name;
        }
        catch { }
        return string.Empty;
    }
}

/// <summary>Fires JSON beats on clip change; applies <c>moralePerSecond</c> while the clip plays during H.</summary>
internal sealed class FearAnimPhaseTracker : MonoBehaviour
{
    private string _enemyKey = string.Empty;
    private MonoBehaviour _host;
    private SkeletonAnimation _spine;
    private string _lastAnim = string.Empty;
    private FearHPhaseBeat _passiveBeat;
    private float _passiveAccum;

    internal static void AttachTo(MonoBehaviour host, string enemyKey)
    {
        if (host == null)
            return;
        try
        {
            var tracker = host.gameObject.GetComponent<FearAnimPhaseTracker>()
                ?? host.gameObject.AddComponent<FearAnimPhaseTracker>();
            tracker._enemyKey = enemyKey;
            tracker._host = host;
            tracker._spine = FearHPhases.ResolveSpine(host);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Fear] " + enemyKey + " tracker attach failed: " + ex.Message);
        }
    }

    private void OnEnable()
    {
        _lastAnim = string.Empty;
        _passiveBeat = null;
        _passiveAccum = 0f;
    }

    private void Update()
    {
        if (!FearConfig.IsEnabled)
            return;
        if (_spine == null)
        {
            _spine = FearHPhases.ResolveSpine(_host);
            if (_spine == null)
                return;
        }

        string anim = FearHPhases.ResolveAnimName(_spine);
        if (string.IsNullOrEmpty(anim))
            return;

        if (!string.Equals(anim, _lastAnim, StringComparison.OrdinalIgnoreCase))
        {
            FlushPassiveLog(_lastAnim);
            _lastAnim = anim;
            FearHPhases.Enter(_enemyKey, anim);
            _passiveBeat = FearHPhases.Find(_enemyKey, anim);
        }

        TickPassive(anim);
    }

    private void TickPassive(string anim)
    {
        if (_passiveBeat == null || Mathf.Abs(_passiveBeat.moralePerSecond) < 0.0001f)
            return;
        playercon player = UnifiedPlayerCacheManager.GetPlayer();
        if (player == null || !player.eroflag)
            return;

        float delta = _passiveBeat.moralePerSecond * Time.deltaTime;
        if (Mathf.Abs(delta) < 0.00001f)
            return;
        FearSystem.NotifyHPhaseSpike(delta, _enemyKey + "_passive_" + anim);
        _passiveAccum += delta > 0f ? -delta : delta;
    }

    private void FlushPassiveLog(string leftAnim)
    {
        if (Mathf.Abs(_passiveAccum) < 0.01f)
            return;
        Plugin.Log?.LogInfo(
            "[Fear] " + _enemyKey + " passive " + leftAnim + " total "
            + _passiveAccum.ToString("0.#") + " Morale " + FearSystem.Morale.ToString("0.#"));
        _passiveAccum = 0f;
    }
}
