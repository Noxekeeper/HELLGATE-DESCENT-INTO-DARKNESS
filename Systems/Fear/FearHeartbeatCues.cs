using UnityEngine;

namespace NoREroMod.Systems.Fear;

/// <summary>
/// Fear HeartBeat cues:
/// - Fear intensity = −Morale while Morale &lt; 0 (0…100%).
/// - One-shot per 10% step (10, 20, …, 90), one step at a time. A jump arms only the next
///   step (9→15 → 10%); further steps wait until the current one-shot finishes.
/// - At 100% (Morale ≤ −100): intermittent pulse — play 3–6s, pause 5–10s, repeat
///   (except during vengeance trauma, when the lethal-trap shock HeartBeat owns audio).
/// </summary>
internal static class FearHeartbeatCues
{
    private const float FloorMorale = -100f;
    private const float StepPercent = 10f;

    private static bool _hooked;
    /// <summary>Highest 10%-fear step already played/armed (0, 10, …, 90).</summary>
    private static int _lastArmedStep;
    private static bool _silencedForTrauma;

    internal static void Initialize(MonoBehaviour host)
    {
        FearHeartbeatAudio.Initialize(host);
        if (_hooked)
            return;
        _hooked = true;
        FearSystem.OnMoraleChanged += OnMoraleChanged;
        SyncFromMorale(FearSystem.Morale, playThresholds: false);
    }

    internal static void Tick(playercon player, PlayerStatus status)
    {
        if (!FearConfig.IsEnabled || !(FearConfig.HeartbeatEnable?.Value ?? true))
        {
            FearHeartbeatAudio.StopAll();
            return;
        }

        if (IsPlayerDead(player, status))
        {
            FearHeartbeatAudio.StopAll();
            return;
        }

        SyncFromMorale(FearSystem.Morale, playThresholds: true);
        if (FearSystem.Morale <= FloorMorale + 0.001f &&
            !FearSystem.IsVengeanceTraumaRunning &&
            !IsPlayerDead(player, status))
        {
            FearHeartbeatAudio.TickMaxFearPulse();
        }
    }

    private static void OnMoraleChanged(float oldMorale, float newMorale)
    {
        if (!FearConfig.IsEnabled || !(FearConfig.HeartbeatEnable?.Value ?? true))
            return;

        SyncFromMorale(newMorale, playThresholds: true);
    }

    private static void SyncFromMorale(float morale, bool playThresholds)
    {
        // Vengeance trauma shock already loops HeartBeat — mute Fear HB to avoid double.
        if (FearSystem.IsVengeanceTraumaRunning)
        {
            if (!_silencedForTrauma)
            {
                FearHeartbeatAudio.StopAll();
                _silencedForTrauma = true;
            }

            float fearDuringTrauma = morale < 0f ? -morale : 0f;
            _lastArmedStep = morale <= FloorMorale + 0.001f
                ? 100
                : FearStep(fearDuringTrauma);
            return;
        }

        if (_silencedForTrauma)
        {
            _silencedForTrauma = false;
            // Re-arm the current step silently after trauma so skipped steps are not replayed.
            float fearAfter = morale < 0f ? -morale : 0f;
            _lastArmedStep = morale <= FloorMorale + 0.001f ? 100 : FearStep(fearAfter);
            if (morale <= FloorMorale + 0.001f)
                FearHeartbeatAudio.StartMaxFearPulse();
            else
                FearHeartbeatAudio.StopLoop();

            return;
        }

        if (morale <= FloorMorale + 0.001f)
        {
            _lastArmedStep = 100;
            FearHeartbeatAudio.StartMaxFearPulse();
            return;
        }

        if (FearHeartbeatAudio.IsLoopPlaying || FearHeartbeatAudio.IsMaxFearPulseActive)
            FearHeartbeatAudio.StopLoop();

        float fearPct = morale < 0f ? -morale : 0f;
        int step = FearStep(fearPct); // 0, 10, …, 90

        if (!playThresholds)
        {
            _lastArmedStep = step;
            return;
        }

        if (step < _lastArmedStep)
            _lastArmedStep = step;

        if (step < 10)
            return;

        // Next unplayed 10% step only; a step is consumed only when its one-shot actually starts.
        int nextStep = _lastArmedStep < 10 ? 10 : _lastArmedStep + (int)StepPercent;
        if (nextStep > 90)
            nextStep = 90;

        if (step < nextStep)
            return;

        if (!FearHeartbeatAudio.IsReady)
            return;
        if (FearHeartbeatAudio.IsOneShotPlaying)
            return;

        _lastArmedStep = nextStep;
        FearHeartbeatAudio.PlayOneShot();
    }

    private static int FearStep(float fearPct)
    {
        if (fearPct < StepPercent)
            return 0;
        int step = Mathf.FloorToInt(fearPct / StepPercent) * (int)StepPercent;
        if (step > 90)
            step = 90;
        return step;
    }

    private static bool IsPlayerDead(playercon player, PlayerStatus status)
    {
        try
        {
            if (status != null && status.Hp <= 0.001f)
                return true;
        }
        catch { }

        try
        {
            if (player != null && player._Death)
                return true;
        }
        catch { }

        return false;
    }
}
