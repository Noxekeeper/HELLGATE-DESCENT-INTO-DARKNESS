using System;
using System.Collections;
using NoREroMod.Patches.Player;
using NoREroMod.Systems.Pregnancy.Patches;
using UnityEngine;

namespace NoREroMod.Systems.Pregnancy.ShelterAttack;

/// <summary>
/// DontDestroyOnLoad host for shelter-attack HUD ticks, lightweight assault-mask refresh during combat,
/// and the post-transition arm-roll delay coroutine (survives scene loads).
/// </summary>
internal sealed class ShelterAttackScenePoller : MonoBehaviour
{
    private const float TickSeconds = 0.25f;
    private const float MaskPollSeconds = 3f;

    private static ShelterAttackScenePoller _host;

    private float _maskPollAccum;
    private Coroutine _runtimeLoop;
    private Coroutine _armDelayCoroutine;

    private void Awake()
    {
        _host = this;
    }

    private void OnDestroy()
    {
        if (_host == this)
            _host = null;

        if (_runtimeLoop != null)
            StopCoroutine(_runtimeLoop);

        CancelDelayedActionInternal();
    }

    internal static bool ScheduleDelayedAction(float delaySeconds, Action action)
    {
        if (_host == null)
            return false;

        _host.CancelDelayedActionInternal();
        _host._armDelayCoroutine = _host.StartCoroutine(DelayedActionRoutine(delaySeconds, action));
        return true;
    }

    internal static void CancelDelayedAction()
    {
        if (_host == null)
            return;

        _host.CancelDelayedActionInternal();
    }

    internal static void EnsureHost(GameObject go)
    {
        if (go == null)
            return;

        if (_host != null)
            return;

        _host = go.GetComponent<ShelterAttackScenePoller>();
        if (_host == null)
            _host = go.AddComponent<ShelterAttackScenePoller>();
    }

    private void CancelDelayedActionInternal()
    {
        if (_armDelayCoroutine == null)
            return;

        StopCoroutine(_armDelayCoroutine);
        _armDelayCoroutine = null;
    }

    private static IEnumerator DelayedActionRoutine(float delaySeconds, Action action)
    {
        if (delaySeconds > 0.01f)
            yield return new WaitForSecondsRealtime(delaySeconds);
        else
            yield return null;

        if (_host != null)
            _host._armDelayCoroutine = null;

        action?.Invoke();
    }

    private void Start()
    {
        _runtimeLoop = StartCoroutine(RuntimeLoop());
    }

    private IEnumerator RuntimeLoop()
    {
        var wait = new WaitForSecondsRealtime(TickSeconds);
        while (true)
        {
            yield return wait;

            if (!PregnancyConfig.IsEnabled || !IsShelterAttackEnabled())
                continue;

            // Safety net: scene-load / altar hooks can miss ParishChurch arrival while Alerting.
            // Physically loaded hideout + travel countdown still running → start wave intro now.
            if ((ShelterAttackState.Phase == ShelterAttackPhase.Armed
                 || ShelterAttackState.Phase == ShelterAttackPhase.Alerting)
                && HideoutSceneUtility.IsParishHideoutActive()
                && ShelterAttackState.GetRemainingSeconds() > 0)
            {
                ShelterAttackDriver.NotifyPhysicallyReturnedToHideout();
            }

            if (ShelterAttackTimerHud.ShouldTick())
                ShelterAttackTimerHud.Process();

            if (ShelterAttackTimerHud.IsTimeoutFlashActive())
                ShelterAttackTimerHud.ProcessTimeoutFlash();

            if (VanillaCutsceneSceneGuard.IsAdditiveEvSceneActive())
            {
                _maskPollAccum = 0f;
                continue;
            }

            _maskPollAccum += TickSeconds;
            if (_maskPollAccum < MaskPollSeconds
                || !ShelterAttackState.IsAssaultPhase
                || !HideoutSceneUtility.IsParishHideoutActive())
            {
                if (!ShelterAttackState.IsAssaultPhase)
                    _maskPollAccum = 0f;

                continue;
            }

            _maskPollAccum = 0f;
            ShelterAttackSceneGuard.ApplyAssaultMaskIfNeeded();
        }
    }

    private static bool IsShelterAttackEnabled()
    {
        return PregnancyConfig.EnableShelterAttack != null && PregnancyConfig.EnableShelterAttack.Value;
    }
}
