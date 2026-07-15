using System;
using UnityEngine;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Economy;
using NoREroMod.Systems.EventCore.Content;
using NoREroMod.Systems.EventCore.Host;
using NoREroMod.Systems.EventCore.UI;
using NoREroMod.Systems.Rage;

namespace NoREroMod.Systems.EventCore.Handlers;

/// <summary>
/// Faction Social Provocation — <c>eventcore_fsp_bandits_sex_paid</c> only.
/// </summary>
internal enum FspHHandoffKind
{
    None,
    Paid,
    Forced,
}

internal static class EventCoreFactionSocialFlow
{
    internal const string HandlerId = "faction_social";

    private static FspHHandoffKind _pendingHHandoff = FspHHandoffKind.None;

    internal const string SexPaidPreludePoolId = "fsp_sex_paid_prelude";
    internal const string SexPaidOpenStepId = "fsp_sex_paid_open";

    internal static bool IsFactionSocial(EventCoreEventDefinitionFile ev) =>
        ev != null && string.Equals(ev.handlerId, HandlerId, StringComparison.OrdinalIgnoreCase);

    internal static bool IsSexPaid(EventCoreEventDefinitionFile ev) =>
        IsFactionSocial(ev) &&
        string.Equals(ev.fspKind?.Trim(), "sex_paid", StringComparison.OrdinalIgnoreCase);

    internal static bool UsesPrelude(EventCoreEventDefinitionFile ev) => IsSexPaid(ev);

    internal static string ResolvePreludePoolId(EventCoreEventDefinitionFile ev) =>
        IsSexPaid(ev) ? SexPaidPreludePoolId : string.Empty;

    internal static string ResolveEntryStepId(EventCoreEventDefinitionFile ev) =>
        IsSexPaid(ev) ? SexPaidOpenStepId : string.Empty;

    internal static bool IsTerminalCloseStep(string stepId)
    {
        if (string.IsNullOrEmpty(stepId))
            return false;
        return stepId.Trim().EndsWith("_close", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsRageThreatUnlocked() =>
        RageSystem.Percent >= EventCoreFactionSocialSession.RageThreatenUnlockPercent;

    internal static string ResolveThreatenStepId(bool success) =>
        success ? "fsp_sex_paid_rage_win" : "fsp_sex_paid_rage_fail";

    internal static EventCoreChoiceSlotUi[] BuildChoiceSlots(
        EventCoreStepDefinition step,
        string[] labels,
        string[] outcomeIds)
    {
        if (labels == null || labels.Length == 0)
            return new EventCoreChoiceSlotUi[0];

        int n = labels.Length;
        var slots = new EventCoreChoiceSlotUi[n];

        for (int i = 0; i < n; i++)
            slots[i] = new EventCoreChoiceSlotUi(labels[i] ?? string.Empty, true, Color.white);

        return slots;
    }

    internal static bool ApplyOutcome(EventCoreEventDefinitionFile ev, string outcomeKey)
    {
        if (!IsSexPaid(ev) || string.IsNullOrEmpty(outcomeKey))
            return true;

        string key = outcomeKey.Trim().ToLowerInvariant();
        string eventId = ev.id ?? string.Empty;
        int faction = EventCoreFactionSocialSession.FactionId;

        switch (key)
        {
            case "start_h_paid":
                if (!EventCoreFactionSocialSession.ApplyingOnContinue)
                    return true;
                GrantGold(EventCoreFactionSocialSession.SexPriceGold, eventId);
                ApplySexAcceptRoll(faction, eventId);
                ScheduleHHandoff(FspHHandoffKind.Paid, eventId);
                return true;

            case "pay_confirm":
            case "pay_blocked":
            case "noop":
            case "close_peaceful":
            case "walk_away":
                return true;

            case "rep_roll_soft":
                ApplyRepRollSoft(faction, eventId);
                return true;

            case "rep_roll_hard":
                ApplyRepRollHard(faction, eventId);
                return true;

            case "threaten_rage_success":
                return true;

            case "threaten_rage_fail":
                ApplyThreatenRageFail(faction, eventId);
                ScheduleHHandoff(FspHHandoffKind.Forced, eventId);
                return true;

            default:
                Plugin.Log?.LogInfo($"[EventCore/fsp] '{eventId}': unhandled outcome '{outcomeKey}'.");
                return true;
        }
    }

    private static void GrantGold(long amount, string eventId)
    {
        if (amount <= 0)
            return;
        GoldWallet.ModifyGold(amount);
        Plugin.Log?.LogInfo($"[EventCore/fsp] '{eventId}': granted {amount} gold (balance {GoldWallet.Current}).");
    }

    private static void ApplyRepRollSoft(int faction, string eventId)
    {
        if (UnityEngine.Random.value < 0.70f)
            return;
        PlayerFactionReputation.ModifyScore(faction, 1f);
        Plugin.Log?.LogInfo($"[EventCore/fsp] '{eventId}': rep_roll_soft +1.");
    }

    private static void ApplyRepRollHard(int faction, string eventId)
    {
        if (UnityEngine.Random.value < 0.50f)
            return;
        PlayerFactionReputation.ModifyScore(faction, -2f);
        Plugin.Log?.LogInfo($"[EventCore/fsp] '{eventId}': rep_roll_hard -2.");
    }

    private static void ApplyThreatenRageFail(int faction, string eventId)
    {
        if (UnityEngine.Random.value < 0.50f)
            return;
        PlayerFactionReputation.ModifyScore(faction, -2f);
        Plugin.Log?.LogInfo($"[EventCore/fsp] '{eventId}': rage fail -2 rep.");
    }

    private static void ApplySexAcceptRoll(int faction, string eventId)
    {
        float roll = UnityEngine.Random.value;
        if (roll < 0.60f)
        {
            PlayerFactionReputation.ModifyScore(faction, 3f);
            Plugin.Log?.LogInfo($"[EventCore/fsp] '{eventId}': sex_accept +3 rep.");
        }
        else if (roll < 0.85f)
        {
            Plugin.Log?.LogInfo($"[EventCore/fsp] '{eventId}': sex_accept noop rep.");
        }
        else
        {
            PlayerFactionReputation.ModifyScore(faction, -1f);
            Plugin.Log?.LogInfo($"[EventCore/fsp] '{eventId}': sex_accept -1 rep.");
        }
    }

    internal static void ClearPendingHHandoff() => _pendingHHandoff = FspHHandoffKind.None;

    internal static bool TryTakePendingHHandoff(out FspHHandoffKind kind)
    {
        kind = _pendingHHandoff;
        if (kind == FspHHandoffKind.None)
            return false;

        _pendingHHandoff = FspHHandoffKind.None;
        return true;
    }

    private static void ScheduleHHandoff(FspHHandoffKind kind, string eventId)
    {
        _pendingHHandoff = kind;
        Plugin.Log?.LogInfo($"[EventCore/fsp] '{eventId}': H-handoff scheduled ({kind}).");
    }

    internal static void ApplyPostModalHHandoff(EventCoreHost host, FspHHandoffKind kind)
    {
        EventCoreHandoffMode mode = kind == FspHHandoffKind.Paid
            ? EventCoreHandoffMode.Consent
            : EventCoreHandoffMode.Forced;

        if (host != null)
        {
            if (kind == FspHHandoffKind.Paid)
                host.ResolveBodyPayment();
            else
                host.MarkHostileOnPlayerFreed();
        }
        else
            Plugin.Log?.LogWarning($"[EventCore/fsp] H-handoff ({kind}): EventCoreHost missing.");

        FactionReputationDynamics.RegisterHandoffOccurred();
        EventCoreBrokerGateFlow.ApplyBrokerConsentGrab(host, null, mode);
        Plugin.Log?.LogInfo($"[EventCore/fsp] H-handoff applied ({kind}) via consent grab ({mode}).");
    }
}
