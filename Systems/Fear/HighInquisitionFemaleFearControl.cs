using System;
using HarmonyLib;
using Spine.Unity;
using UnityEngine;

namespace NoREroMod.Systems.Fear;

/// <summary>
/// High Inquisition Female H Fear ramp. Phase order and weights come from JSON
/// <c>HighInquisition_famale.hPhases</c> (|spikeMorale| = weight); the ramp always ends at Morale −100
/// on the last phase, even if the scene started with positive Morale.
/// </summary>
internal static class HighInquisitionFemaleFearControl
{
    internal const string EnemyKey = "HighInquisition_famale";

    [HarmonyPatch(typeof(HiInquisition_famale_Ftality), "Start")]
    [HarmonyPostfix]
    private static void AttachTracker(HiInquisition_famale_Ftality __instance)
    {
        try
        {
            if (__instance == null)
                return;
            var tracker = __instance.gameObject.GetComponent<HighInquisitionFemaleFearTracker>()
                ?? __instance.gameObject.AddComponent<HighInquisitionFemaleFearTracker>();
            tracker.Init(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Fear] HighInquisitionFemaleFearControl attach failed: " + ex.Message);
        }
    }
}

internal class HighInquisitionFemaleFearTracker : MonoBehaviour
{
    private const float FearFloor = -100f;

    private MonoBehaviour _host;
    private SkeletonAnimation _spine;
    private string _lastAnim = string.Empty;

    private FearHPhaseBeat[] _phases;
    private int _entryPhase = -1;
    private float _startMorale;
    private float _weightFromEntry;

    internal void Init(MonoBehaviour host)
    {
        _host = host;
        _spine = FearHPhases.ResolveSpine(host);
        ResetRampState();
    }

    private void OnEnable() => ResetRampState();

    private void ResetRampState()
    {
        _lastAnim = string.Empty;
        _phases = null;
        _entryPhase = -1;
        _startMorale = 0f;
        _weightFromEntry = 0f;
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
        if (string.IsNullOrEmpty(anim) || string.Equals(anim, _lastAnim, StringComparison.OrdinalIgnoreCase))
            return;

        string prev = _lastAnim;
        _lastAnim = anim;

        FearHPhaseBeat[] phases = _phases ?? FearCatalog.GetHPhases(HighInquisitionFemaleFearControl.EnemyKey);
        int phase = IndexOfPhase(phases, anim);
        if (phase < 0)
            return;

        if (_entryPhase < 0)
            BeginRamp(phases, phase);

        bool last = phase == _phases.Length - 1;
        float target = last ? FearFloor : ResolveTargetMorale(phase);
        FearSystem.SetMoraleDirect(target, "hi_female_" + anim);

        Plugin.Log?.LogInfo(
            "[Fear] HighInquisitionFemale phase " + anim
            + " (was " + (string.IsNullOrEmpty(prev) ? "-" : prev)
            + ") Morale " + FearSystem.Morale.ToString("0.#")
            + " (start " + _startMorale.ToString("0.#") + " → floor " + FearFloor.ToString("0.#") + ")");
    }

    private void BeginRamp(FearHPhaseBeat[] phases, int phase)
    {
        _phases = phases;
        _entryPhase = phase;
        _startMorale = FearSystem.Morale;
        _weightFromEntry = 0f;
        for (int i = _entryPhase; i < phases.Length; i++)
            _weightFromEntry += Weight(phases[i]);
        if (_weightFromEntry < 0.001f)
            _weightFromEntry = 1f;
    }

    private float ResolveTargetMorale(int phase)
    {
        if (phase < _entryPhase)
            phase = _entryPhase;
        float done = 0f;
        for (int i = _entryPhase; i <= phase && i < _phases.Length; i++)
            done += Weight(_phases[i]);
        return Mathf.Lerp(_startMorale, FearFloor, Mathf.Clamp01(done / _weightFromEntry));
    }

    private static float Weight(FearHPhaseBeat beat) => beat != null ? Mathf.Abs(beat.spikeMorale) : 0f;

    private static int IndexOfPhase(FearHPhaseBeat[] phases, string anim)
    {
        if (phases == null)
            return -1;
        for (int i = 0; i < phases.Length; i++)
        {
            if (phases[i] != null && string.Equals(anim, phases[i].animation, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
}
