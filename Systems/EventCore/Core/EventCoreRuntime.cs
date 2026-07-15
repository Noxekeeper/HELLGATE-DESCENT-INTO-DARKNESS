using System;
using NoREroMod;
using NoREroMod.Systems.Economy;
using NoREroMod.Systems.EventCore.Content;
using NoREroMod.Systems.EventCore.Handlers;
using NoREroMod.Systems.EventCore.Host;
using NoREroMod.Systems.EventCore.UI;
using UnityEngine;

namespace NoREroMod.Systems.EventCore.Core;

/// <summary>
/// Orchestrates one active EventCore modal session (canvas UI only).
/// </summary>
internal static class EventCoreRuntime
{
    private const long DefaultDisplayToll = 25L;

    private static EventCoreEventDefinitionFile _activeDefinition;
    private static EventCoreHost _activeHost;
    private static int _stepIndex;
    private static bool _sessionOpen;

    private static string[] _resolvedChoiceLabels;

    private static EventCoreChoiceSlotUi[] _brokerPaySlots;
    private static EventCoreChoiceSlotUi[] _fspChoiceSlots;
    private static string[] _brokerPayOutcomes;
    private static string[] _brokerPayJumps;
    private static string _negotiateIntroNextStepId;

    private static int _lastRenderedStepIndex = -1;

    /// <summary>
    /// Set after dismissing the NPC line shown before a choice step so the next render displays only the choices.
    /// </summary>
    private static bool _choiceNpcLineDismissed;

    /// <summary>
    /// True while the modal expects a continue action, either for a real continue step or a pre-choice preamble.
    /// </summary>
    private static bool _continuePromptActive;

    /// <summary>
    /// True while a random prelude line must be shown before the first real step (broker gate, FSP sex_paid, …).
    /// </summary>
    private static bool _eventPreludePending;

    private static string _eventPreludePoolId = string.Empty;

    internal static bool IsSessionOpen => _sessionOpen;

    internal static bool ContinuePromptActive => _continuePromptActive;

    internal static bool CurrentStepIsContinue =>
        _sessionOpen &&
        _activeDefinition?.steps != null &&
        _stepIndex >= 0 &&
        _stepIndex < _activeDefinition.steps.Length &&
        string.Equals(_activeDefinition.steps[_stepIndex].stepKind, "continue", StringComparison.OrdinalIgnoreCase);

    /// <summary>Keyboard: ignore hotkeys for greyed broker rows.</summary>
    internal static bool CanActivateBrokerPaySlot(int choiceIndex)
    {
        EventCoreChoiceSlotUi[] slots = _brokerPaySlots ?? _fspChoiceSlots;
        if (slots == null || choiceIndex < 0 || choiceIndex >= slots.Length)
            return true;
        return slots[choiceIndex].Interactable;
    }

    internal static void Initialize()
    {
        EventCoreDefinitionRegistry.ReloadFromDisk();
        EventCoreStringRegistry.ReloadFromDisk();
    }

    private static void ClearBrokerGateTransientState()
    {
        _brokerPayOutcomes = null;
        _brokerPayJumps = null;
        _brokerPaySlots = null;
        _fspChoiceSlots = null;
        _negotiateIntroNextStepId = null;
    }

    internal static void ShutdownSession()
    {
        if (!_sessionOpen)
            return;

        EventCoreModalCanvas.Hide();
        EventCoreContinueResolve.CompleteSession();
        EventCoreBrokerGateFlow.ClearSessionState();
        _sessionOpen = false;
        _activeDefinition = null;
        _activeHost = null;
        EventCoreFactionSocialSession.Clear();
        _stepIndex = 0;
        _resolvedChoiceLabels = null;
        ClearBrokerGateTransientState();
        _lastRenderedStepIndex = -1;
        _choiceNpcLineDismissed = false;
        _continuePromptActive = false;
        _eventPreludePending = false;
        _eventPreludePoolId = string.Empty;
    }

    internal static bool TryBeginSession(string eventId, EventCoreHost host = null)
    {
        if (_sessionOpen)
            return false;

        EventCoreDefinitionRegistry.EnsureLoaded();

        if (!EventCoreDefinitionRegistry.TryGet(eventId, out var def))
        {
            Plugin.Log?.LogError($"[EventCore] Cannot open event '{eventId}'.");
            return false;
        }

        var steps = def.steps;
        if (steps == null || steps.Length == 0)
        {
            Plugin.Log?.LogError($"[EventCore] Event '{eventId}' has no steps.");
            return false;
        }

        _activeDefinition = def;
        _activeHost = host;
        _stepIndex = ResolveEntryStepIndex(def, steps);
        _sessionOpen = true;
        _resolvedChoiceLabels = null;
        ClearBrokerGateTransientState();
        _lastRenderedStepIndex = -1;
        _choiceNpcLineDismissed = false;
        _continuePromptActive = false;
        ResolveEventPreludeOnBegin(def);
        EventCoreFactionSocialSession.Begin(def);

        EventCorePause.BeginSessionFreeze();
        RenderCurrentStep();
        return true;
    }

    /// <summary>
    /// Advances either a true continue step or the temporary continue prompt shown before a choice step.
    /// </summary>
    internal static void AdvanceContinuePrompt()
    {
        if (!_sessionOpen || _activeDefinition?.steps == null)
            return;

        if (_stepIndex < 0 || _stepIndex >= _activeDefinition.steps.Length)
            return;

        if (_eventPreludePending)
        {
            _eventPreludePending = false;
            RenderCurrentStep();
            return;
        }

        if (CurrentStepIsContinue)
        {
            AdvanceContinueStep();
            return;
        }

        _choiceNpcLineDismissed = true;
        RenderCurrentStep();
    }

    internal static void AdvanceContinueStep()
    {
        if (!_sessionOpen || _activeDefinition == null)
            return;

        var steps = _activeDefinition.steps;
        if (steps == null || _stepIndex < 0 || _stepIndex >= steps.Length)
            return;

        EventCoreStepDefinition leaving = steps[_stepIndex];
        EventCoreChoiceApply.ApplyContinueOutcome(_activeDefinition, leaving);
        EventCoreContinueResolve.RaiseStepContinue();

        if (EventCoreBrokerGateFlow.IsBrokerGate(_activeDefinition) &&
            string.Equals(leaving.stepId, EventCoreBrokerGateFlow.StepInsufficientBodyLine, StringComparison.OrdinalIgnoreCase))
            EventCoreBrokerGateFlow.MarkInsufficientBodyChoiceDebit();

        if (EventCoreFactionSocialFlow.IsFactionSocial(_activeDefinition) &&
            EventCoreFactionSocialFlow.TryTakePendingHHandoff(out FspHHandoffKind handoffKind))
        {
            EventCoreHost handoffHost = _activeHost;
            ShutdownSession();
            EventCoreFactionSocialFlow.ApplyPostModalHHandoff(handoffHost, handoffKind);
            return;
        }

        if (EventCoreBrokerGateFlow.IsBrokerGate(_activeDefinition) &&
            EventCoreBrokerGateFlow.ShouldBrokerContinueApplyResolutionBeforeAdvance(leaving.stepId))
            EventCoreBrokerGateFlow.ApplyTerminalResolution(leaving.stepId, _activeHost);

        if (EventCoreBrokerGateFlow.IsBrokerGate(_activeDefinition) &&
            EventCoreBrokerGateFlow.IsBrokerTerminalContinueStep(leaving.stepId))
        {
            EventCoreEventDefinitionFile finishedEvent = _activeDefinition;
            EventCoreHost finishedHost = _activeHost;
            bool knockdown = EventCoreBrokerGateFlow.ShouldBrokerTerminalApplyKnockdown(leaving.stepId);
            bool spawnAmbush = EventCoreBrokerGateFlow.ShouldBrokerTerminalSpawnAmbush(leaving.stepId);
            EventCoreBrokerGateFlow.ApplyTerminalResolution(leaving.stepId, finishedHost);
            ShutdownSession();
            if (spawnAmbush)
            {
                EventCoreBrokerGateFlow.TrySpawnBrokerAmbush(finishedEvent, leaving.stepId, finishedHost);
                EventCoreBrokerGateFlow.ScheduleAmbushHostileReveal(finishedHost);
            }
            if (knockdown)
                EventCoreBrokerGateFlow.ApplyBrokerConsentGrab(finishedHost, leaving.stepId);
            return;
        }

        if (EventCoreFactionSocialFlow.IsFactionSocial(_activeDefinition) &&
            EventCoreFactionSocialFlow.IsTerminalCloseStep(leaving.stepId))
        {
            ShutdownSession();
            return;
        }

        int nextIndex;
        if (string.Equals(leaving.stepId, EventCoreBrokerGateFlow.StepNegotiateIntro, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(_negotiateIntroNextStepId))
        {
            int found = FindStepIndexById(steps, _negotiateIntroNextStepId.Trim());
            nextIndex = found >= 0 ? found : _stepIndex + 1;
            _negotiateIntroNextStepId = null;
        }
        else if (!string.IsNullOrEmpty(leaving.continueNextStepId))
        {
            int found = FindStepIndexById(steps, leaving.continueNextStepId.Trim());
            nextIndex = found >= 0 ? found : _stepIndex + 1;
        }
        else
            nextIndex = _stepIndex + 1;

        _stepIndex = nextIndex;
        _resolvedChoiceLabels = null;
        ClearBrokerGateTransientState();

        if (_stepIndex < 0 || _stepIndex >= steps.Length)
        {
            ShutdownSession();
            return;
        }

        RenderCurrentStep();
    }

    internal static void AdvanceChoiceStep(int choiceIndex)
    {
        if (!_sessionOpen || _activeDefinition == null)
            return;

        var steps = _activeDefinition.steps;
        if (steps == null || _stepIndex < 0 || _stepIndex >= steps.Length)
            return;

        var step = steps[_stepIndex];

        bool brokerDynamicSlots =
            EventCoreBrokerGateFlow.IsBrokerGate(_activeDefinition) &&
            _brokerPaySlots != null &&
            (string.Equals(step.stepId, EventCoreBrokerGateFlow.StepPayChoice, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(step.stepId, EventCoreBrokerGateFlow.StepGateOpen, StringComparison.OrdinalIgnoreCase));

        if (brokerDynamicSlots &&
            choiceIndex >= 0 &&
            choiceIndex < _brokerPaySlots.Length &&
            !_brokerPaySlots[choiceIndex].Interactable)
            return;

        if (EventCoreFactionSocialFlow.IsFactionSocial(_activeDefinition) &&
            _fspChoiceSlots != null &&
            choiceIndex >= 0 &&
            choiceIndex < _fspChoiceSlots.Length &&
            !_fspChoiceSlots[choiceIndex].Interactable)
            return;

        bool brokerOutcomeDynamic = brokerDynamicSlots && _brokerPayOutcomes != null;
        if (brokerOutcomeDynamic)
        {
            if (choiceIndex < 0 || choiceIndex >= _brokerPayOutcomes.Length)
                return;
        }
        else
        {
            string[] labels = _resolvedChoiceLabels ?? step.choiceLabels;
            if (labels == null || choiceIndex < 0 || choiceIndex >= labels.Length)
                return;
        }

        string outcomeId = ResolveChoiceOutcome(step, choiceIndex);
        if (!EventCoreChoiceApply.TryApplyChoiceOutcome(_activeDefinition, step, choiceIndex, outcomeId))
            return;

        string key = outcomeId?.ToLowerInvariant() ?? string.Empty;

        if (EventCoreFactionSocialFlow.IsFactionSocial(_activeDefinition) &&
            string.Equals(key, "walk_away", StringComparison.OrdinalIgnoreCase))
        {
            EventCoreContinueResolve.RaiseStepContinue();
            ShutdownSession();
            return;
        }

        int nextIndex;
        if (string.Equals(key, "insufficient_declare", StringComparison.OrdinalIgnoreCase))
        {
            string jumpId = EventCoreBrokerGateFlow.ResolveInsufficientJumpStepId(_activeDefinition);
            nextIndex = FindStepIndexById(steps, jumpId);
            if (nextIndex < 0)
                nextIndex = _stepIndex + 1;
        }
        else if (string.Equals(key, "negotiate_try", StringComparison.OrdinalIgnoreCase))
        {
            nextIndex = FindStepIndexById(steps, "broker_negotiate_intro");
            if (nextIndex < 0)
                nextIndex = _stepIndex + 1;
        }
        else if (string.Equals(key, "rage_intimidate", StringComparison.OrdinalIgnoreCase))
        {
            nextIndex = FindStepIndexById(steps, EventCoreBrokerGateFlow.ResolveRageIntimidationStepId());
            if (nextIndex < 0)
                nextIndex = _stepIndex + 1;
        }
        else if (string.Equals(key, "threaten", StringComparison.OrdinalIgnoreCase) &&
                 EventCoreFactionSocialFlow.IsFactionSocial(_activeDefinition))
        {
            string jumpId = EventCoreFactionSocialFlow.ResolveThreatenStepId(EventCoreFactionSocialFlow.IsRageThreatUnlocked());
            nextIndex = FindStepIndexById(steps, jumpId);
            if (nextIndex < 0)
                nextIndex = _stepIndex + 1;
        }
        else
            nextIndex = ResolveNextStepIndexAfterChoice(steps, step, choiceIndex);

        EventCoreContinueResolve.RaiseStepContinue();

        _stepIndex = nextIndex;
        _resolvedChoiceLabels = null;
        ClearBrokerGateTransientState();

        if (_stepIndex < 0 || _stepIndex >= steps.Length)
        {
            ShutdownSession();
            return;
        }

        RenderCurrentStep();
    }

    private static string ResolveChoiceOutcome(EventCoreStepDefinition step, int choiceIndex)
    {
        if (EventCoreBrokerGateFlow.IsBrokerGate(_activeDefinition) &&
            _brokerPayOutcomes != null &&
            choiceIndex >= 0 &&
            choiceIndex < _brokerPayOutcomes.Length &&
            (string.Equals(step.stepId, EventCoreBrokerGateFlow.StepPayChoice, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(step.stepId, EventCoreBrokerGateFlow.StepGateOpen, StringComparison.OrdinalIgnoreCase)))
            return _brokerPayOutcomes[choiceIndex];

        string[] ids = step.choiceOutcomeIds;
        if (ids == null || choiceIndex < 0 || choiceIndex >= ids.Length)
            return null;

        string raw = ids[choiceIndex];
        return string.IsNullOrEmpty(raw) ? null : raw.Trim();
    }

    private static int FindStepIndexById(EventCoreStepDefinition[] steps, string stepId)
    {
        if (steps == null || string.IsNullOrEmpty(stepId))
            return -1;

        for (int i = 0; i < steps.Length; i++)
        {
            if (string.Equals(steps[i].stepId, stepId, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static int ResolveNextStepIndexAfterChoice(EventCoreStepDefinition[] steps, EventCoreStepDefinition step, int choiceIndex)
    {
        int linear = _stepIndex + 1;

        string targetId = null;
        if (EventCoreBrokerGateFlow.IsBrokerGate(_activeDefinition) &&
            _brokerPayJumps != null &&
            choiceIndex >= 0 &&
            choiceIndex < _brokerPayJumps.Length &&
            (string.Equals(step.stepId, EventCoreBrokerGateFlow.StepPayChoice, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(step.stepId, EventCoreBrokerGateFlow.StepGateOpen, StringComparison.OrdinalIgnoreCase)))
        {
            string dj = _brokerPayJumps[choiceIndex];
            if (!string.IsNullOrEmpty(dj))
                targetId = dj.Trim();
        }

        if (string.IsNullOrEmpty(targetId))
        {
            string[] jumpIds = step.choiceJumpStepIds;
            if (jumpIds != null && choiceIndex >= 0 && choiceIndex < jumpIds.Length && !string.IsNullOrEmpty(jumpIds[choiceIndex]))
                targetId = jumpIds[choiceIndex].Trim();
        }

        if (string.IsNullOrEmpty(targetId))
            return linear;

        int found = FindStepIndexById(steps, targetId);
        if (found >= 0)
            return found;

        if (EventCoreFactionSocialFlow.IsFactionSocial(_activeDefinition))
        {
            Plugin.Log?.LogError(
                $"[EventCore/fsp] choiceJumpStepIds target '{targetId}' not found at step '{step.stepId}' — branch broken, staying on step.");
            return _stepIndex;
        }

        Plugin.Log?.LogWarning($"[EventCore] choiceJumpStepIds references unknown stepId '{targetId}' — using linear next step.");
        return linear;
    }

    private static EventCoreChoiceSlotUi[] WrapPlainChoiceSlots(string[] labels)
    {
        if (labels == null || labels.Length == 0)
            return new EventCoreChoiceSlotUi[0];

        var a = new EventCoreChoiceSlotUi[labels.Length];
        for (int i = 0; i < labels.Length; i++)
            a[i] = new EventCoreChoiceSlotUi(labels[i], true, Color.white);
        return a;
    }

    private static void SyncLabelsFromSlots(EventCoreChoiceSlotUi[] slots)
    {
        if (slots == null || slots.Length == 0)
        {
            _resolvedChoiceLabels = new string[0];
            return;
        }

        var lb = new string[slots.Length];
        for (int i = 0; i < slots.Length; i++)
            lb[i] = slots[i].Label;
        _resolvedChoiceLabels = lb;
    }

    private static void RenderCurrentStep()
    {
        var steps = _activeDefinition?.steps;
        if (steps == null || _stepIndex < 0 || _stepIndex >= steps.Length)
            return;

        if (_eventPreludePending &&
            !string.IsNullOrEmpty(_eventPreludePoolId) &&
            EventCoreStringRegistry.TryGetRandomLine(_eventPreludePoolId, out string preludeRaw) &&
            !string.IsNullOrEmpty(preludeRaw?.Trim()))
        {
            _continuePromptActive = true;
            EventCorePortraitPair preludePortraits = ResolveEventPreludePortraits();
            EventCoreModalCanvas.Show(string.Empty, preludeRaw.Trim(), null, continueOnly: true, preludeNarration: true, preludePortraits);
            return;
        }

        _eventPreludePending = false;
        _eventPreludePoolId = string.Empty;

        var step = steps[_stepIndex];
        bool isContinue = string.Equals(step.stepKind, "continue", StringComparison.OrdinalIgnoreCase);

        bool brokerPayStep = EventCoreBrokerGateFlow.IsBrokerGate(_activeDefinition) &&
                             string.Equals(step.stepId, EventCoreBrokerGateFlow.StepPayChoice, StringComparison.OrdinalIgnoreCase);
        bool brokerGateOpenStep = EventCoreBrokerGateFlow.IsBrokerGate(_activeDefinition) &&
                                  string.Equals(step.stepId, EventCoreBrokerGateFlow.StepGateOpen, StringComparison.OrdinalIgnoreCase);

        if (!brokerPayStep && !brokerGateOpenStep)
        {
            _brokerPayOutcomes = null;
            _brokerPayJumps = null;
            _brokerPaySlots = null;
        }

        long reqGold = DefaultDisplayToll;
        if (EventCoreFactionSocialFlow.IsFactionSocial(_activeDefinition))
            reqGold = EventCoreFactionSocialSession.GetDisplayGoldForKind();
        else if (_activeDefinition != null && _activeDefinition.tollGold > 0)
            reqGold = _activeDefinition.tollGold;

        long playerGold = GoldWallet.Current;

        string body = EventCoreStringRegistry.ResolveStepBody(step, reqGold, playerGold);
        string speaker = step.speakerLabel != null ? step.speakerLabel.Trim() : string.Empty;

        if (EventCoreBrokerGateFlow.IsBrokerGate(_activeDefinition) &&
            string.Equals(step.stepId, EventCoreBrokerGateFlow.StepNegotiateIntro, StringComparison.OrdinalIgnoreCase))
        {
            EventCoreBrokerGateFlow.PrepareNegotiateIntro(_activeDefinition, out string introBody, out string nextId);
            body = introBody;
            _negotiateIntroNextStepId = nextId;
        }

        // A choice step with an empty body should not render a separate continue screen just because a speaker label exists.
        if (!isContinue && string.IsNullOrEmpty(body?.Trim()))
            speaker = string.Empty;

        EventCoreChoiceSlotUi[] slotUi;

        if (!isContinue &&
            EventCoreBrokerGateFlow.IsBrokerGate(_activeDefinition) &&
            string.Equals(step.stepId, EventCoreBrokerGateFlow.StepPayChoice, StringComparison.OrdinalIgnoreCase))
        {
            speaker = string.Empty;
            body = string.Empty;
            EventCoreBrokerGateFlow.BuildPayChoices(_activeDefinition, out slotUi, out string[] dynOut, out string[] dynJump);
            slotUi = EventCoreBrokerPortraitMap.EnrichChoiceSlots(slotUi, dynOut, step);
            _brokerPaySlots = slotUi;
            _brokerPayOutcomes = dynOut;
            _brokerPayJumps = dynJump;
            SyncLabelsFromSlots(slotUi);
        }
        else if (!isContinue &&
                 EventCoreBrokerGateFlow.IsBrokerGate(_activeDefinition) &&
                 string.Equals(step.stepId, EventCoreBrokerGateFlow.StepGateOpen, StringComparison.OrdinalIgnoreCase))
        {
            EventCoreBrokerGateFlow.BuildGateOpenChoices(_activeDefinition, out slotUi, out string[] gateOut, out string[] gateJump);
            slotUi = EventCoreBrokerPortraitMap.EnrichChoiceSlots(slotUi, gateOut, step);
            _brokerPaySlots = slotUi;
            _brokerPayOutcomes = gateOut;
            _brokerPayJumps = gateJump;
            SyncLabelsFromSlots(slotUi);
        }
        else if (!isContinue)
        {
            string[] labels = EventCoreStringRegistry.ResolveChoiceLabels(step, reqGold, playerGold);
            if (EventCoreFactionSocialFlow.IsFactionSocial(_activeDefinition))
            {
                slotUi = EventCoreFactionSocialFlow.BuildChoiceSlots(step, labels, step.choiceOutcomeIds);
                if (EventCoreFactionSocialFlow.IsSexPaid(_activeDefinition))
                    slotUi = EventCoreFspPortraitMap.EnrichChoiceSlots(slotUi, step.choiceOutcomeIds, step);
                _fspChoiceSlots = slotUi;
                SyncLabelsFromSlots(slotUi);
            }
            else
            {
                slotUi = WrapPlainChoiceSlots(labels);
                if (EventCoreBrokerGateFlow.IsBrokerGate(_activeDefinition))
                    slotUi = EventCoreBrokerPortraitMap.EnrichChoiceSlots(slotUi, step.choiceOutcomeIds, step);
                _resolvedChoiceLabels = labels;
            }
        }
        else
            slotUi = null;

        if (_lastRenderedStepIndex != _stepIndex)
        {
            _choiceNpcLineDismissed = false;
            _lastRenderedStepIndex = _stepIndex;
        }

        bool hasNpc = HasDisplayableNpcContent(body);

        if (!isContinue && hasNpc && !_choiceNpcLineDismissed)
        {
            _continuePromptActive = true;
            EventCorePortraitPair preamblePortraits = ResolveStepPortraits(step, isPreChoiceNpcLine: true);
            EventCoreModalCanvas.Show(speaker, body, null, continueOnly: true, portraits: preamblePortraits);
            return;
        }

        string showSpeaker = speaker;
        string showBody = body;
        if (!isContinue && hasNpc && _choiceNpcLineDismissed)
        {
            showSpeaker = string.Empty;
            showBody = string.Empty;
        }

        _continuePromptActive = isContinue;
        EventCorePortraitPair portraits = ResolveStepPortraits(step, isPreChoiceNpcLine: false, choiceSlots: slotUi);
        EventCoreModalCanvas.Show(showSpeaker, showBody, slotUi, continueOnly: isContinue, portraits: portraits);
    }

    private static EventCorePortraitPair ResolveEventPreludePortraits()
    {
        if (EventCoreBrokerGateFlow.IsBrokerGate(_activeDefinition))
            return EventCoreBrokerPortraitMap.ResolvePrelude();

        if (EventCoreFactionSocialFlow.IsSexPaid(_activeDefinition))
            return EventCoreFspPortraitMap.ResolvePrelude();

        return EventCorePortraitPair.Hidden;
    }

    private static EventCorePortraitPair ResolveStepPortraits(
        EventCoreStepDefinition step,
        bool isPreChoiceNpcLine,
        EventCoreChoiceSlotUi[] choiceSlots = null)
    {
        if (EventCoreBrokerGateFlow.IsBrokerGate(_activeDefinition))
        {
            if (isPreChoiceNpcLine)
                return EventCoreBrokerPortraitMap.ResolveNpcPreamble(step);

            if (step != null && string.Equals(step.stepKind, "continue", StringComparison.OrdinalIgnoreCase))
                return EventCoreBrokerPortraitMap.ResolveContinueStep(step, _negotiateIntroNextStepId);

            return EventCoreBrokerPortraitMap.ResolveChoiceMenu(step);
        }

        if (EventCoreFactionSocialFlow.IsSexPaid(_activeDefinition))
        {
            if (isPreChoiceNpcLine)
                return EventCoreFspPortraitMap.ResolveNpcPreamble(step);

            if (step != null && string.Equals(step.stepKind, "continue", StringComparison.OrdinalIgnoreCase))
                return EventCoreFspPortraitMap.ResolveContinueStep(step);

            return EventCoreFspPortraitMap.ResolveChoiceMenu(step);
        }

        return EventCorePortraitPair.Hidden;
    }

    /// <summary>
    /// Determines whether a pre-choice NPC preamble should be shown. A speaker label alone is not enough.
    /// </summary>
    private static bool HasDisplayableNpcContent(string bodyLine)
    {
        return !string.IsNullOrEmpty(bodyLine?.Trim());
    }

    private static int ResolveEntryStepIndex(EventCoreEventDefinitionFile def, EventCoreStepDefinition[] steps)
    {
        if (steps == null || steps.Length == 0)
            return 0;

        if (EventCoreFactionSocialFlow.IsFactionSocial(def))
        {
            string entryId = EventCoreFactionSocialFlow.ResolveEntryStepId(def);
            if (!string.IsNullOrEmpty(entryId))
            {
                int found = FindStepIndexById(steps, entryId);
                if (found >= 0)
                    return found;
            }
        }

        if (EventCoreBrokerGateFlow.IsBrokerGate(def))
        {
            int broker = FindStepIndexById(steps, EventCoreBrokerGateFlow.StepGateOpen);
            if (broker >= 0)
                return broker;
        }

        return 0;
    }

    private static void ResolveEventPreludeOnBegin(EventCoreEventDefinitionFile def)
    {
        _eventPreludePending = false;
        _eventPreludePoolId = string.Empty;
        if (def == null)
            return;

        if (EventCoreBrokerGateFlow.IsBrokerGate(def))
        {
            _eventPreludePending = true;
            _eventPreludePoolId = EventCoreBrokerGateFlow.PreludePoolId;
            return;
        }

        string fspPool = EventCoreFactionSocialFlow.ResolvePreludePoolId(def);
        if (string.IsNullOrEmpty(fspPool))
            return;

        _eventPreludePending = true;
        _eventPreludePoolId = fspPool;
    }
}
