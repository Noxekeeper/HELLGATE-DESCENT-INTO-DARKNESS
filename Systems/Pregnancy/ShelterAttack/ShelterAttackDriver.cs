using System;
using System.Collections;
using System.Collections.Generic;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Dialogue;
using NoREroMod.Systems.Pregnancy.Patches;
using NoREroMod.Systems.Spawn;
using UnityEngine;
using Random = UnityEngine.Random;

namespace NoREroMod.Systems.Pregnancy.ShelterAttack;

/// <summary>
/// Main controller for the hideout shelter attack event. After any zone transition, waits
/// <see cref="PregnancyConfig.ShelterAttackArmDelaySeconds"/>, rolls trigger chance once, then
/// counts down real time before the assault in ParishChurch.
/// </summary>
internal static class ShelterAttackDriver
{
    private const string PlayerBoneName = "hair1";
    private const float ThoughtVerticalOffsetPx = 32f;
    private const float ThoughtBoneWorldOffsetY = 0.3f;
    private static readonly Color ThoughtTextColor = new Color(1f, 0.2f, 0.2f, 1f);
    private static readonly Color ThoughtOutlineColor = new Color(0f, 0f, 0f, 1f);

    private static readonly int[] PossibleFactions =
    {
        FactionIds.Bandits,
        FactionIds.Church,
        FactionIds.Demons,
        FactionIds.Undead,
        FactionIds.Monsters,
        FactionIds.Mafia
    };

    private static Coroutine _runningCoroutine;
    private static Coroutine _phraseRetryCoroutine;
    private static Coroutine _timeoutDefeatCoroutine;
    private static bool _armRollPending;
    private static float _armRollDueUnscaled;
    private static float _armRollNotBeforeUnscaled = float.MaxValue;
    private static double _lastPhraseUtcSeconds;
    private static bool _assaultReadyForHideout;
    private static string _lastObservedZone = string.Empty;
    private static string _lastHandledTransitionKey = string.Empty;
    private static float _lastHandledTransitionAt;
    private static string _pendingFastTravelFromZone;
    private static string _pendingFastTravelToZone;

    internal static void Initialize()
    {
        CancelPendingArmRoll();
        ShelterAttackOutcomePresentation.Cancel();
        ShelterAttackSlotStore.BindActiveSlot(PregnancySlotStore.ActiveSlot);
        ShelterAttackSlotStore.LoadFromActiveSlot();
        SyncObservedZone(HellGateLocationSpawnRefresh.GetActiveGameplayZone());

        if (PregnancyConfig.IsEnabled && IsShelterAttackEnabled())
            EnsureScenePoller();

        if (ShelterAttackState.IsEventActive && ShelterAttackState.AttackingFaction != 0)
            ShelterAttackState.TotalWaves = ShelterAttackWaves.GetTotalWaveCount(ShelterAttackState.AttackingFaction);

        HandleActiveEventAfterLoad(logResume: true);
    }

    private static void SyncObservedZone(string zone)
    {
        _lastObservedZone = zone ?? string.Empty;
    }

    /// <summary>Altar fast-travel sets Idea_Nowscene before LoadSceneAndWait — capture origin here.</summary>
    internal static void NotifyFastTravelPending(string fromZone, string toZone)
    {
        if (string.IsNullOrEmpty(fromZone) || string.IsNullOrEmpty(toZone))
            return;

        if (string.Equals(fromZone, toZone, StringComparison.OrdinalIgnoreCase))
            return;

        // A roll scheduled before the teleport would otherwise keep ticking through the scene load
        // (the poller survives scene changes) and arm mid-transition. Cancel it so the countdown
        // never starts instantly on arrival — it is rescheduled with the full delay after landing.
        CancelPendingArmRoll();

        _pendingFastTravelFromZone = fromZone;
        _pendingFastTravelToZone = toZone;

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
        {
            Plugin.Log?.LogInfo(
                $"[Pregnancy.ShelterAttack] Fast travel pending: \"{fromZone}\" -> \"{toZone}\".");
        }
    }

    /// <summary>Call from transition / altar hooks when the active scene changes.</summary>
    internal static void OnSceneChanged(string targetScene = null)
    {
        string toZone = !string.IsNullOrEmpty(targetScene)
            ? targetScene
            : HellGateLocationSpawnRefresh.GetActiveGameplayZone();

        string fromZone = TryConsumePendingFastTravelFrom(toZone);
        if (string.IsNullOrEmpty(fromZone))
            fromZone = _lastObservedZone;

        OnZoneTransition(fromZone ?? string.Empty, toZone);
    }

    /// <summary>Primary entry for any gameplay zone change (walk, altar, teleport, load).</summary>
    internal static void OnZoneTransition(string fromZone, string toZone)
    {
        if (string.IsNullOrEmpty(toZone))
            toZone = HellGateLocationSpawnRefresh.GetActiveGameplayZone();

        if (string.IsNullOrEmpty(fromZone)
            || string.Equals(fromZone, toZone, StringComparison.OrdinalIgnoreCase))
        {
            string pendingFrom = TryConsumePendingFastTravelFrom(toZone);
            if (!string.IsNullOrEmpty(pendingFrom))
                fromZone = pendingFrom;
        }

        if (HellGateLocationSpawnRefresh.ShouldIgnoreSceneName(toZone))
            return;

        string transitionKey = (fromZone ?? string.Empty) + "->" + toZone;
        float now = Time.unscaledTime;
        if (string.Equals(transitionKey, _lastHandledTransitionKey, StringComparison.Ordinal)
            && now - _lastHandledTransitionAt < 0.3f)
        {
            if (!string.IsNullOrEmpty(toZone))
                _lastObservedZone = toZone;

            return;
        }

        _lastHandledTransitionKey = transitionKey;
        _lastHandledTransitionAt = now;

        bool wasInHideout = IsParishHideoutZone(fromZone);
        bool nowInHideout = IsParishHideoutZone(toZone);

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
        {
            Plugin.Log?.LogInfo(
                $"[Pregnancy.ShelterAttack] Zone transition: \"{fromZone}\" -> \"{toZone}\", " +
                $"hideout {wasInHideout}->{nowInHideout}, phase={ShelterAttackState.Phase}, " +
                $"active={ShelterAttackState.IsEventActive}, children={PregnancySlotStore.GetAliveChildrenInHideout().Count}");
        }

        if (!PregnancyConfig.IsEnabled || !IsShelterAttackEnabled())
        {
            _lastObservedZone = toZone;
            return;
        }

        bool leftHideout = wasInHideout && !nowInHideout;
        bool enteredHideout = !wasInHideout && nowInHideout;

        if (leftHideout)
            ShelterAttackSceneGuard.RestoreAssaultMask();

        _lastObservedZone = toZone;

        if (enteredHideout && ShelterAttackState.IsEventActive)
        {
            ClearAlertPhrases();
            OnReturnedToShelter();
            OnAssaultHideoutEntered();
            return;
        }

        bool zoneChanged = !string.IsNullOrEmpty(fromZone)
            && !string.Equals(fromZone, toZone, StringComparison.OrdinalIgnoreCase);

        // Ignore title / load transitions — they are not player travel. Opening an altar after load
        // must not inherit a pending arm from "Gametitle -> village_main".
        if (IsNonGameplayOriginZone(fromZone))
            zoneChanged = false;

        if ((zoneChanged || leftHideout) && CanScheduleArmRoll())
            ScheduleArmRollAfterTransition(fromZone, toZone);
    }

    private static bool IsNonGameplayOriginZone(string zone)
    {
        if (string.IsNullOrEmpty(zone))
            return true;

        return zone.Equals("Gametitle", StringComparison.OrdinalIgnoreCase)
            || zone.Equals("Common", StringComparison.OrdinalIgnoreCase)
            || zone.Equals("Savemenu", StringComparison.OrdinalIgnoreCase)
            || zone.IndexOf("Gameover", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string TryConsumePendingFastTravelFrom(string toZone)
    {
        if (string.IsNullOrEmpty(_pendingFastTravelFromZone))
            return null;

        if (!string.IsNullOrEmpty(_pendingFastTravelToZone)
            && !string.IsNullOrEmpty(toZone)
            && !string.Equals(_pendingFastTravelToZone, toZone, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string from = _pendingFastTravelFromZone;
        _pendingFastTravelFromZone = null;
        _pendingFastTravelToZone = null;
        return from;
    }

    private static bool CanScheduleArmRoll()
    {
        ClearResolvedPhaseIfConfigured();

        if (ShelterAttackState.IsResolved)
            return false;

        if (ShelterAttackState.IsEventActive)
            return false;

        return ShelterAttackState.Phase == ShelterAttackPhase.Idle || IsArmRollPending();
    }

    private static void ClearResolvedPhaseIfConfigured()
    {
        if (ShelterAttackState.Phase == ShelterAttackPhase.Defeat
            && PregnancyConfig.ShelterAttackResetOnLoss != null
            && PregnancyConfig.ShelterAttackResetOnLoss.Value)
        {
            ShelterAttackState.Reset();
            ShelterAttackSlotStore.MarkDirty();
            return;
        }

        if (ShelterAttackState.Phase == ShelterAttackPhase.Victory
            && PregnancyConfig.ShelterAttackResetOnWin != null
            && PregnancyConfig.ShelterAttackResetOnWin.Value)
        {
            ShelterAttackState.Reset();
            ShelterAttackSlotStore.MarkDirty();
        }
    }

    internal static bool IsArmRollPending()
    {
        return _armRollPending && Time.unscaledTime < _armRollDueUnscaled;
    }

    /// <summary>Called after pregnancy slot JSON reload (in-game load / slot bind).</summary>
    internal static void OnAfterSlotLoad()
    {
        CancelPendingArmRoll();
        EnsureScenePoller();

        if (!PregnancyConfig.IsEnabled || !IsShelterAttackEnabled())
            return;

        if (ShelterAttackState.IsEventActive && ShelterAttackState.AttackingFaction != 0)
            ShelterAttackState.TotalWaves = ShelterAttackWaves.GetTotalWaveCount(ShelterAttackState.AttackingFaction);

        if (ShelterAttackState.IsEventActive)
        {
            HandleActiveEventAfterLoad(logResume: false);
            return;
        }

        if (ShelterAttackState.IsResolved)
        {
            ClearResolvedPhaseIfConfigured();
            StopDriver();
            ResetTransientFlags();
            ShelterAttackTimerHud.Reset();
            return;
        }

        ResetTransientFlags();
        ShelterAttackTimerHud.Reset();
    }

    private static void HandleActiveEventAfterLoad(bool logResume)
    {
        if (!ShelterAttackState.IsEventActive)
            return;

        if (PregnancySlotStore.GetAliveChildrenInHideout().Count == 0)
        {
            Plugin.Log?.LogWarning("[Pregnancy.ShelterAttack] Stale active event with no children — resetting.");
            ShelterAttackState.Reset();
            ResetTransientFlags();
            ShelterAttackSlotStore.MarkDirty();
            StopDriver();
            return;
        }

        if (ShelterAttackState.IsAssaultPhase && !HideoutSceneUtility.IsParishHideoutActive())
        {
            StartDriver();
            return;
        }

        ResumeActiveEvent();
        StartDriver();

        if (logResume && PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
        {
            Plugin.Log?.LogInfo(
                $"[Pregnancy.ShelterAttack] Resumed active event on load: phase={ShelterAttackState.Phase}, " +
                $"faction={ShelterAttackState.AttackingFaction}, remaining={ShelterAttackState.GetRemainingSeconds():F0}s");
        }
    }

    private static void ScheduleArmRollAfterTransition(string fromZone, string toZone)
    {
        int children = PregnancySlotStore.GetAliveChildrenInHideout().Count;
        if (children == 0)
            return;

        CancelPendingArmRoll();
        EnsureScenePoller();

        float delay = PregnancyConfig.ShelterAttackArmDelaySeconds?.Value ?? 2f;
        delay = Mathf.Max(0f, delay);

        _armRollPending = delay > 0.01f;
        _armRollDueUnscaled = Time.unscaledTime + delay;
        _armRollNotBeforeUnscaled = _armRollDueUnscaled;
        ShelterAttackTimerHud.Reset();
        ShelterAttackTimerHud.ClearTimeoutFlash();

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
        {
            Plugin.Log?.LogInfo(
                $"[Pregnancy.ShelterAttack] Arm roll scheduled in {delay:F0}s after \"{fromZone}\" -> \"{toZone}\".");
        }

        if (!ShelterAttackScenePoller.ScheduleDelayedAction(delay, () =>
            {
                _armRollPending = false;
                if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                {
                    Plugin.Log?.LogInfo(
                        $"[Pregnancy.ShelterAttack] Arm roll firing after delay (\"{fromZone}\" -> \"{toZone}\").");
                }

                TryArmEventIfEligible();
            }))
        {
            _armRollPending = false;
            Plugin.Log?.LogError("[Pregnancy.ShelterAttack] Arm roll delay failed — scene poller host is missing.");
        }
    }

    private static void TryArmEventIfEligible()
    {
        if (!IsArmRollGateOpen())
        {
            float wait = _armRollNotBeforeUnscaled - Time.unscaledTime;

            // The delayed action fired slightly before the gate opened (double transition / scene-load
            // drift). Reschedule for the remaining time so the roll is never silently dropped.
            // A huge wait means the gate was cancelled (float.MaxValue) — leave it dropped in that case.
            if (wait > 0f && wait < 60f)
            {
                if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
                    Plugin.Log?.LogInfo($"[Pregnancy.ShelterAttack] Arm roll gated for {wait:F1}s more — rescheduling.");

                ShelterAttackScenePoller.ScheduleDelayedAction(wait + 0.05f, TryArmEventIfEligible);
            }

            return;
        }

        if (ShelterAttackState.IsEventActive || ShelterAttackState.IsResolved)
            return;

        TryArmEvent();
    }

    private static void CancelPendingArmRoll()
    {
        _armRollPending = false;
        _armRollDueUnscaled = 0f;
        _armRollNotBeforeUnscaled = float.MaxValue;
        ShelterAttackScenePoller.CancelDelayedAction();
    }

    private static bool IsArmRollGateOpen()
    {
        return Time.unscaledTime + 0.001f >= _armRollNotBeforeUnscaled;
    }

    private static bool IsParishHideoutZone(string zone)
    {
        if (string.IsNullOrEmpty(zone))
            return HideoutSceneUtility.IsParishHideoutActive();

        if (zone.IndexOf("Parishchurch", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (zone.IndexOf("Inunderground", StringComparison.OrdinalIgnoreCase) >= 0
            || zone.IndexOf("Underground", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        return zone.IndexOf("Parish", StringComparison.OrdinalIgnoreCase) >= 0
            && zone.IndexOf("Church", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ResumeActiveEvent()
    {
        if (ShelterAttackState.Phase == ShelterAttackPhase.Armed
            || ShelterAttackState.Phase == ShelterAttackPhase.Alerting)
        {
            double remaining = ShelterAttackState.GetRemainingSeconds();
            if (remaining <= 0)
            {
                if (!HideoutSceneUtility.IsParishHideoutActive())
                {
                    // Stale save loaded away from hideout — apply timeout outcome without popups on load.
                    ShelterAttackOutcome.ResolveTimeoutDefeatSilent();
                    StopDriver();
                    return;
                }

                _assaultReadyForHideout = true;
                if (ShelterAttackState.Phase == ShelterAttackPhase.Armed)
                    ShelterAttackState.Phase = ShelterAttackPhase.Alerting;

                ShelterAttackTimerHud.Reset();

                TryStartAssaultInHideout();
                return;
            }

            float timerSeconds = PregnancyConfig.ShelterAttackTimerSeconds?.Value ?? 60f;
            PresentPreAssaultCountdown(timerSeconds);
            return;
        }

        if (ShelterAttackState.Phase == ShelterAttackPhase.WaveBreak)
        {
            NormalizeWaveBreakCountdown();
            ShelterAttackSceneGuard.ApplyAssaultMaskIfNeeded();
            return;
        }

        if (ShelterAttackState.IsAssaultPhase && ShelterAttackState.Phase != ShelterAttackPhase.WaveBreak)
        {
            ResumeAssaultWaveAfterLoad();
        }
    }

    private static void ResumeAssaultWaveAfterLoad()
    {
        // Assault enemies are not persisted — replay the current wave from the start.
        ShelterAttackTracker.DestroyAllRemaining();
        ShelterAttackState.SpawnIndexInWave = 0;
        ShelterAttackSpawnScheduler.Reset();
        ShelterAttackState.Phase = ShelterAttackPhase.WaveBreak;

        float duration = GetPreWaveDuration(ShelterAttackState.CurrentWave == 0);
        ShelterAttackState.WaveBreakUntilUnscaled = Time.unscaledTime + duration;
        ShelterAttackTimerHud.NotifyWaveBreakStarted(duration);
        ShelterAttackSlotStore.MarkDirty();
        ShelterAttackSceneGuard.ApplyAssaultMaskIfNeeded();

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
        {
            Plugin.Log?.LogInfo(
                $"[Pregnancy.ShelterAttack] Reloaded mid-assault — replaying wave {ShelterAttackState.CurrentWave + 1} intro ({duration:F0}s).");
        }
    }

    private static void EnsureScenePoller()
    {
        GameObject existing = GameObject.Find("PregnancyShelterAttackPoller_XUAIGNORE");
        if (existing != null)
        {
            ShelterAttackScenePoller.EnsureHost(existing);
            return;
        }

        try
        {
            GameObject go = new GameObject("PregnancyShelterAttackPoller_XUAIGNORE");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<ShelterAttackScenePoller>();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[Pregnancy.ShelterAttack] Failed to create scene poller: {ex.Message}");
        }
    }

    private static void TryArmEvent()
    {
        if (!IsArmRollGateOpen())
            return;

        CancelPendingArmRoll();
        _armRollNotBeforeUnscaled = float.MaxValue;

        int children = PregnancySlotStore.GetAliveChildrenInHideout().Count;
        if (children == 0)
            return;

        float chance = PregnancyConfig.ShelterAttackTriggerChance?.Value ?? 1f;
        chance = Mathf.Clamp01(chance);
        if (chance < 1f && Random.value > chance)
            return;

        int faction = PossibleFactions[Random.Range(0, PossibleFactions.Length)];

        ShelterAttackWaves.ThreatTier tier = ShelterAttackWaves.ResolveThreatTier(children);
        ShelterAttackState.ThreatTier = tier;
        ShelterAttackState.ThreatTierLocked = true;

        int totalWaves = ShelterAttackWaves.GetTotalWaveCount(faction);
        if (totalWaves <= 0)
        {
            ShelterAttackState.ThreatTierLocked = false;
            Plugin.Log?.LogError(
                $"[Pregnancy.ShelterAttack] Cannot arm: {ShelterAttackWaves.GetTierFileName(tier)} has no waves for faction {ShelterAttackWaves.GetFactionKey(faction)}.");
            return;
        }

        double now = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        float timerSeconds = PregnancyConfig.ShelterAttackTimerSeconds?.Value ?? 60f;

        ShelterAttackState.Phase = ShelterAttackPhase.Armed;
        ShelterAttackState.AttackingFaction = faction;
        ShelterAttackState.TotalWaves = totalWaves;
        ShelterAttackState.UtcDeadlineSeconds = now + timerSeconds;
        ShelterAttackState.CurrentWave = 0;
        _lastPhraseUtcSeconds = 0;

        PresentPreAssaultCountdown(timerSeconds);
        ShelterAttackSlotStore.MarkDirty();

        StartDriver();

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
        {
            Plugin.Log?.LogInfo(
                $"[Pregnancy.ShelterAttack] Armed attack by faction {faction} in {timerSeconds:F0}s " +
                $"(deadline {ShelterAttackState.UtcDeadlineSeconds}), children={children}, " +
                $"tier={tier} ({ShelterAttackWaves.GetTierFileName(tier)}).");
        }
    }

    private static void PresentPreAssaultCountdown(float timerSeconds)
    {
        ShelterAttackTimerHud.NotifyAttackArmed(timerSeconds);

        if (HideoutSceneUtility.IsParishHideoutActive())
            return;

        if (ShelterAttackState.Phase == ShelterAttackPhase.Armed)
        {
            ShelterAttackState.Phase = ShelterAttackPhase.Alerting;
            ShelterAttackSlotStore.MarkDirty();
        }

        ScheduleAlertPhraseWhenPlayerReady();
    }

    private static void ScheduleAlertPhraseWhenPlayerReady()
    {
        if (_phraseRetryCoroutine != null && Plugin.Instance != null)
            Plugin.Instance.StopCoroutine(_phraseRetryCoroutine);

        if (Plugin.Instance != null)
            _phraseRetryCoroutine = Plugin.Instance.StartCoroutine(WaitForPlayerAndShowAlertPhrase());
    }

    private static IEnumerator WaitForPlayerAndShowAlertPhrase()
    {
        for (int i = 0; i < 40; i++)
        {
            yield return new WaitForSecondsRealtime(0.25f);

            if (ShelterAttackState.Phase != ShelterAttackPhase.Armed
                && ShelterAttackState.Phase != ShelterAttackPhase.Alerting)
            {
                _phraseRetryCoroutine = null;
                yield break;
            }

            if (HideoutSceneUtility.IsParishHideoutActive())
            {
                _phraseRetryCoroutine = null;
                yield break;
            }

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null)
                continue;

            ShowAlertPhrase();
            _lastPhraseUtcSeconds = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            _phraseRetryCoroutine = null;
            yield break;
        }

        _phraseRetryCoroutine = null;
    }

    private static void StartDriver()
    {
        StopDriverCoroutine();
        if (Plugin.Instance != null)
            _runningCoroutine = Plugin.Instance.StartCoroutine(DriverLoop());
    }

    internal static void ResetTransientFlags()
    {
        _assaultReadyForHideout = false;
    }

    private static void StopDriver()
    {
        StopDriverCoroutine();
        CancelPendingArmRoll();
        CancelTimeoutDefeatSequence();
        CancelPhraseRetry();
        ResetTransientFlags();
        ShelterAttackTimerHud.Reset();
        ShelterAttackTimerHud.ClearTimeoutFlash();
    }

    private static void CancelPhraseRetry()
    {
        if (_phraseRetryCoroutine != null && Plugin.Instance != null)
            Plugin.Instance.StopCoroutine(_phraseRetryCoroutine);

        _phraseRetryCoroutine = null;
        _lastPhraseUtcSeconds = 0;
    }

    /// <summary>Clears overhead alert thoughts so they do not linger after teleporting into ParishChurch.</summary>
    private static void ClearAlertPhrases()
    {
        CancelPhraseRetry();
        try
        {
            if (!DialogueFramework.IsInitialized)
                return;

            DialogueDisplay display = DialogueFramework.GetDisplay();
            display?.DismissAllVisible();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Pregnancy.ShelterAttack] Failed to clear alert phrases: " + ex.Message);
        }

        // Belt-and-suspenders: destroy leftover thought containers that may outlive DismissAllVisible
        // if the dialogue canvas was recreated during the scene load.
        DestroyOrphanThoughtUi();
    }

    private static void DestroyOrphanThoughtUi()
    {
        try
        {
            // Scan DontDestroyOnLoad / active scene for leftover thought containers
            // that may outlive DismissAllVisible if the dialogue canvas was recreated.
            UnityEngine.UI.Text[] texts = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Text>();
            for (int i = 0; i < texts.Length; i++)
            {
                UnityEngine.UI.Text t = texts[i];
                if (t == null)
                    continue;

                Transform tr = t.transform;
                while (tr != null)
                {
                    string n = tr.name;
                    if (n.IndexOf("AradiaThought", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("AradiaFloating", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        UnityEngine.Object.Destroy(tr.gameObject);
                        break;
                    }

                    tr = tr.parent;
                }
            }
        }
        catch
        {
        }
    }

    private static void CancelTimeoutDefeatSequence()
    {
        if (_timeoutDefeatCoroutine != null && Plugin.Instance != null)
            Plugin.Instance.StopCoroutine(_timeoutDefeatCoroutine);

        _timeoutDefeatCoroutine = null;
    }

    private static void StopDriverCoroutine()
    {
        if (_runningCoroutine != null && Plugin.Instance != null)
        {
            Plugin.Instance.StopCoroutine(_runningCoroutine);
            _runningCoroutine = null;
        }
    }

    private static IEnumerator DriverLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(0.5f);

            if (ShelterAttackState.Phase == ShelterAttackPhase.Idle)
                break;

            if (ShelterAttackState.Phase == ShelterAttackPhase.Armed ||
                ShelterAttackState.Phase == ShelterAttackPhase.Alerting)
            {
                ProcessArmedAlert();
            }
            else if (ShelterAttackState.Phase == ShelterAttackPhase.WaveBreak)
            {
                ProcessWaveBreak();
            }
            else if (ShelterAttackState.Phase == ShelterAttackPhase.Spawning ||
                     ShelterAttackState.Phase == ShelterAttackPhase.Combat)
            {
                ProcessCombat();
            }
        }

        _runningCoroutine = null;
    }

    private static void ProcessArmedAlert()
    {
        double nowUtc = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        float timerSeconds = PregnancyConfig.ShelterAttackTimerSeconds?.Value ?? 60f;
        float alertSeconds = PregnancyConfig.ShelterAttackAlertSeconds?.Value ?? 15f;
        alertSeconds = Mathf.Min(alertSeconds, timerSeconds);
        float intervalSeconds = PregnancyConfig.ShelterAttackPhraseIntervalSeconds?.Value ?? 15f;

        double remaining = ShelterAttackState.UtcDeadlineSeconds - nowUtc;
        if (remaining <= alertSeconds)
        {
            if (ShelterAttackState.Phase == ShelterAttackPhase.Armed)
            {
                ShelterAttackState.Phase = ShelterAttackPhase.Alerting;
                ShelterAttackSlotStore.MarkDirty();
            }
        }

        if (ShelterAttackState.Phase == ShelterAttackPhase.Alerting)
        {
            if (!HideoutSceneUtility.IsParishHideoutActive()
                && _lastPhraseUtcSeconds > 0
                && (nowUtc - _lastPhraseUtcSeconds) >= intervalSeconds)
            {
                ShowAlertPhrase();
                _lastPhraseUtcSeconds = nowUtc;
            }
        }

        if (nowUtc >= ShelterAttackState.UtcDeadlineSeconds)
        {
            if (!HideoutSceneUtility.IsParishHideoutActive())
            {
                BeginTimeoutDefeatSequence();
                return;
            }

            BeginAssaultPhase();
        }
    }

    private static void BeginTimeoutDefeatSequence()
    {
        if (_timeoutDefeatCoroutine != null
            || ShelterAttackState.IsResolved
            || ShelterAttackState.Phase == ShelterAttackPhase.Defeat
            || ShelterAttackState.Phase == ShelterAttackPhase.Victory)
            return;

        StopDriverCoroutine();
        CancelPhraseRetry();

        if (Plugin.Instance == null)
        {
            ShelterAttackOutcome.ResolveTimeoutDefeat();
            StopDriver();
            return;
        }

        _timeoutDefeatCoroutine = Plugin.Instance.StartCoroutine(TimeoutDefeatSequence());
    }

    private static IEnumerator TimeoutDefeatSequence()
    {
        float flashSeconds = PregnancyConfig.ShelterAttackTimeoutFlashSeconds?.Value ?? 3f;
        flashSeconds = Mathf.Max(1f, flashSeconds);

        ShelterAttackTimerHud.BeginTimeoutFlash(flashSeconds);

        float end = Time.unscaledTime + flashSeconds;
        while (Time.unscaledTime < end)
            yield return new WaitForSecondsRealtime(0.25f);

        _timeoutDefeatCoroutine = null;
        ShelterAttackTimerHud.ClearTimeoutFlash();
        ShelterAttackOutcome.ResolveTimeoutDefeat();
        StopDriver();
    }

    private static void TryStartAssaultInHideout()
    {
        if (!_assaultReadyForHideout && ShelterAttackState.GetRemainingSeconds() > 0)
            return;

        if (!HideoutSceneUtility.IsParishHideoutActive())
            return;

        _assaultReadyForHideout = false;
        BeginAssaultPhase();
    }

    private static void OnAssaultHideoutEntered()
    {
        if (ShelterAttackState.Phase == ShelterAttackPhase.WaveBreak)
            NormalizeWaveBreakCountdown();

        if (ShelterAttackState.Phase == ShelterAttackPhase.Spawning
            || ShelterAttackState.Phase == ShelterAttackPhase.Combat)
            ShelterAttackSpawnScheduler.PrepareCurrentWaveQueue();

        ShelterAttackSceneGuard.ApplyAssaultMaskIfNeeded();
    }

    private static float GetWaveIntroSeconds()
    {
        return Mathf.Max(1f, PregnancyConfig.ShelterAttackWaveIntroSeconds?.Value ?? 10f);
    }

    private static float GetWaveBreakSeconds()
    {
        return Mathf.Max(1f, PregnancyConfig.ShelterAttackWaveBreakSeconds?.Value ?? 10f);
    }

    private static float GetFinalWaveBreakSeconds()
    {
        return Mathf.Max(1f, PregnancyConfig.ShelterAttackFinalWaveBreakSeconds?.Value ?? 15f);
    }

    /// <summary>Pre-wave countdown for the wave the player is about to face (intro, regular break, or final-wave break).</summary>
    private static float GetPreWaveDuration(bool isIntro)
    {
        if (isIntro)
            return GetWaveIntroSeconds();

        if (IsFinalWaveIndex(ShelterAttackState.CurrentWave))
            return GetFinalWaveBreakSeconds();

        return GetWaveBreakSeconds();
    }

    private static bool IsFinalWaveIndex(int waveIndex)
    {
        return ShelterAttackState.TotalWaves > 0
            && waveIndex >= ShelterAttackState.TotalWaves - 1;
    }

    private static void NormalizeWaveBreakCountdown()
    {
        if (ShelterAttackState.Phase != ShelterAttackPhase.WaveBreak)
            return;

        if (!HideoutSceneUtility.IsParishHideoutActive())
            return;

        bool isIntro = ShelterAttackState.CurrentWave == 0;
        float targetDuration = GetPreWaveDuration(isIntro);
        float remaining = ShelterAttackState.GetWaveBreakRemainingSeconds();

        if (remaining < targetDuration * 0.9f)
        {
            ShelterAttackState.WaveBreakUntilUnscaled = Time.unscaledTime + targetDuration;
            ShelterAttackTimerHud.NotifyWaveBreakStarted(targetDuration);
            ShelterAttackSlotStore.MarkDirty();

            if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
            {
                Plugin.Log?.LogInfo(
                    $"[Pregnancy.ShelterAttack] Wave break timer refreshed to {targetDuration:F0}s (wave {ShelterAttackState.CurrentWave + 1}, intro={isIntro}).");
            }
        }
        else
        {
            ShelterAttackTimerHud.NotifyWaveBreakStarted(remaining);
        }
    }

    private static void BeginAssaultPhase()
    {
        if (ShelterAttackState.Phase == ShelterAttackPhase.Spawning
            || ShelterAttackState.Phase == ShelterAttackPhase.Combat
            || ShelterAttackState.Phase == ShelterAttackPhase.WaveBreak)
            return;

        if (!HideoutSceneUtility.IsParishHideoutActive())
        {
            _assaultReadyForHideout = true;
            return;
        }

        _assaultReadyForHideout = false;
        ShelterAttackSpawnScheduler.ResetForAssault();
        BeginWaveBreak(isIntro: true);

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
        {
            Plugin.Log?.LogInfo(
                $"[Pregnancy.ShelterAttack] Assault armed — faction {ShelterAttackState.AttackingFaction}, wave 1 intro.");
        }
    }

    private static void BeginWaveBreak(bool isIntro)
    {
        float duration = GetPreWaveDuration(isIntro);

        ShelterAttackState.Phase = ShelterAttackPhase.WaveBreak;
        ShelterAttackState.WaveBreakUntilUnscaled = Time.unscaledTime + duration;
        ShelterAttackTimerHud.NotifyWaveBreakStarted(duration);
        ShelterAttackSlotStore.MarkDirty();
        ShelterAttackSceneGuard.ApplyAssaultMaskIfNeeded();

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
        {
            Plugin.Log?.LogInfo(
                $"[Pregnancy.ShelterAttack] Wave break started for wave {ShelterAttackState.CurrentWave + 1} ({duration:F1}s, intro={isIntro}).");
        }
    }

    private static void ProcessWaveBreak()
    {
        if (!HideoutSceneUtility.IsParishHideoutActive())
            return;

        CheckPlayerDefeat();

        if (ShelterAttackState.Phase != ShelterAttackPhase.WaveBreak)
            return;

        if (Time.unscaledTime < ShelterAttackState.WaveBreakUntilUnscaled)
            return;

        FinishWaveBreak();
    }

    private static void FinishWaveBreak()
    {
        ShelterAttackState.Phase = ShelterAttackPhase.Spawning;
        ShelterAttackSpawnScheduler.PrepareCurrentWaveQueue();
        ShelterAttackSlotStore.MarkDirty();

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
        {
            Plugin.Log?.LogInfo(
                $"[Pregnancy.ShelterAttack] Wave {ShelterAttackState.CurrentWave + 1} spawning started.");
        }
    }

    private static void ScheduleNextWaveBreak()
    {
        ShelterAttackState.CurrentWave++;
        ShelterAttackState.SpawnIndexInWave = 0;
        ShelterAttackSpawnScheduler.Reset();
        BeginWaveBreak(isIntro: false);
    }

    private static void ShowAlertPhrase()
    {
        if (HideoutSceneUtility.IsParishHideoutActive())
            return;

        if (!ShelterAttackPhrases.TryGetRandomLine(out string line))
            return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
            return;

        try
        {
            if (!DialogueFramework.IsInitialized)
                DialogueFramework.Initialize();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[Pregnancy.ShelterAttack] DialogueFramework init failed: " + ex.Message);
            return;
        }

        DialogueDisplay display = DialogueFramework.GetDisplay();
        if (display == null)
            return;

        float duration = PregnancyConfig.ShelterAttackPhraseIntervalSeconds?.Value ?? 15f;
        float fontSize = (Plugin.dialogueFontSize?.Value ?? 16f) * 2f + 2f;

        DialogueStyle style = DialogueDisplay.BuildAradiaThoughtStyle(
            ThoughtVerticalOffsetPx,
            0f,
            true,
            ThoughtTextColor,
            ThoughtOutlineColor);
        style.FontSize = fontSize;

        display.ShowAradiaThought(
            playerObj,
            line,
            PlayerBoneName,
            style,
            duration,
            disableBoneFallbacks: false,
            boneWorldOffsetY: ThoughtBoneWorldOffsetY,
            textColor: ThoughtTextColor,
            outlineColor: ThoughtOutlineColor);
    }

    private static void OnReturnedToShelter()
    {
        // Travel timer is for getting back from the world — once the player is in hideout, start wave 1 intro.
        if (TryBeginAssaultFromHideoutReturn())
            return;

        TryStartAssaultInHideout();
    }

    /// <summary>
    /// Safety entry from the scene poller: physical Parishchurch is loaded while Armed/Alerting with
    /// travel time left. Zone-transition hooks sometimes miss this arrival.
    /// </summary>
    internal static void NotifyPhysicallyReturnedToHideout()
    {
        if (!ShelterAttackState.IsEventActive)
            return;

        if (ShelterAttackState.Phase != ShelterAttackPhase.Armed
            && ShelterAttackState.Phase != ShelterAttackPhase.Alerting)
            return;

        if (!HideoutSceneUtility.IsParishHideoutActive())
            return;

        if (ShelterAttackState.GetRemainingSeconds() <= 0)
            return;

        ClearAlertPhrases();
        SyncObservedZone("Parishchurch");
        OnReturnedToShelter();
        OnAssaultHideoutEntered();
    }

    /// <summary>
    /// Player reached hideout while the pre-assault travel countdown still has time left —
    /// cancel it and begin the wave-1 intro countdown immediately.
    /// </summary>
    private static bool TryBeginAssaultFromHideoutReturn()
    {
        if (ShelterAttackState.Phase != ShelterAttackPhase.Armed
            && ShelterAttackState.Phase != ShelterAttackPhase.Alerting)
            return false;

        if (ShelterAttackState.GetRemainingSeconds() <= 0)
            return false;

        if (!HideoutSceneUtility.IsParishHideoutActive())
            return false;

        ClearAlertPhrases();
        ShelterAttackState.UtcDeadlineSeconds = 0;
        _assaultReadyForHideout = false;
        ShelterAttackSlotStore.MarkDirty();
        ShelterAttackTimerHud.Reset();

        if (PregnancyConfig.DebugLogging != null && PregnancyConfig.DebugLogging.Value)
        {
            Plugin.Log?.LogInfo(
                "[Pregnancy.ShelterAttack] Hideout entered with travel time left — wave 1 intro started.");
        }

        BeginAssaultPhase();
        return true;
    }

    private static void ProcessCombat()
    {
        ShelterAttackSpawnScheduler.Tick();
        ShelterAttackTracker.PruneDead();

        if (ShelterAttackState.Phase == ShelterAttackPhase.Spawning
            && ShelterAttackTracker.AliveCount > 0)
        {
            ShelterAttackState.Phase = ShelterAttackPhase.Combat;
            ShelterAttackSlotStore.MarkDirty();
        }

        CheckPlayerDefeat();

        if (ShelterAttackState.Phase != ShelterAttackPhase.Spawning
            && ShelterAttackState.Phase != ShelterAttackPhase.Combat)
            return;

        if (!HideoutSceneUtility.IsParishHideoutActive())
            return;

        if (ShelterAttackTracker.AliveCount == 0 && !ShelterAttackSpawnScheduler.HasPendingSpawns)
        {
            if (ShelterAttackState.CurrentWave >= ShelterAttackState.TotalWaves - 1)
            {
                ShelterAttackOutcome.ResolveVictory();
                StopDriver();
                return;
            }

            ScheduleNextWaveBreak();
        }
    }

    internal static void ResolveDefeatIfAssaultActive()
    {
        if (!PregnancyConfig.IsEnabled || !IsShelterAttackEnabled())
            return;

        if (!ShelterAttackState.IsAssaultPhase || ShelterAttackState.IsResolved)
            return;

        ShelterAttackOutcome.ResolveDefeat();
        StopDriver();
    }

    private static void CheckPlayerDefeat()
    {
        if (!HideoutSceneUtility.IsParishHideoutActive())
            return;

        playercon player = UnifiedPlayerCacheManager.GetPlayer();
        PlayerStatus status = UnifiedPlayerCacheManager.GetPlayerStatus();
        if (player == null)
            return;

        bool defeated = player._Death || (status != null && status.Hp <= 0f);
        if (!defeated)
            return;

        ShelterAttackOutcome.ResolveDefeat();
        StopDriver();
    }

    private static bool IsShelterAttackEnabled()
    {
        return PregnancyConfig.EnableShelterAttack != null && PregnancyConfig.EnableShelterAttack.Value;
    }
}
