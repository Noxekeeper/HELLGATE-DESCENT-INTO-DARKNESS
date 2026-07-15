using System;
using NoREroMod;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Economy;
using NoREroMod.Systems.EventCore.Content;

namespace NoREroMod.Systems.EventCore.Handlers;

/// <summary>
/// Runs gameplay effects when the player picks a choice. Parallel arrays on the step align with <see cref="EventCoreStepDefinition.choiceLabels"/>.
/// </summary>
internal static class EventCoreChoiceApply
{
    internal static void ApplyContinueOutcome(EventCoreEventDefinitionFile ev, EventCoreStepDefinition step)
    {
        if (ev == null || step == null)
            return;

        string cid = step.continueOutcomeId;
        if (string.IsNullOrEmpty(cid))
            return;

        string handlerId = ev.handlerId ?? string.Empty;
        if (string.Equals(handlerId, EventCoreHandlerIds.FactionSocial, StringComparison.OrdinalIgnoreCase))
        {
            EventCoreFactionSocialSession.ApplyingOnContinue = true;
            try
            {
                EventCoreFactionSocialFlow.ApplyOutcome(ev, cid.Trim());
            }
            finally
            {
                EventCoreFactionSocialSession.ApplyingOnContinue = false;
            }
            return;
        }

        if (!string.Equals(handlerId, EventCoreHandlerIds.BrokerToll, StringComparison.OrdinalIgnoreCase))
            return;

        ApplyBrokerOutcome(ev, cid.Trim(), choiceSlot: -1);
    }

    /// <returns>False if the choice must be blocked (e.g. cannot afford pay).</returns>
    internal static bool TryApplyChoiceOutcome(EventCoreEventDefinitionFile ev, EventCoreStepDefinition step, int choiceIndex, string outcomeId)
    {
        if (ev == null || step == null || choiceIndex < 0)
            return true;

        string handlerId = ev.handlerId ?? string.Empty;
        if (string.Equals(handlerId, EventCoreHandlerIds.FactionSocial, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(outcomeId))
                return true;
            EventCoreFactionSocialSession.ApplyingOnContinue = false;
            return EventCoreFactionSocialFlow.ApplyOutcome(ev, outcomeId.Trim());
        }

        if (!string.Equals(handlerId, EventCoreHandlerIds.BrokerToll, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.IsNullOrEmpty(outcomeId))
        {
            Plugin.Log?.LogInfo($"[EventCore] '{ev.id}' choice slot {choiceIndex}: missing outcome id.");
            return true;
        }

        return ApplyBrokerOutcome(ev, outcomeId.Trim(), choiceIndex);
    }

    private const long DefaultTollGold = 25L;

    private static bool ApplyBrokerOutcome(EventCoreEventDefinitionFile ev, string outcomeKey, int choiceSlot)
    {
        string eventId = ev?.id ?? string.Empty;
        string key = outcomeKey.ToLowerInvariant();

        switch (key)
        {
            case "pay_pass":
            case "pay":
                long toll = DefaultTollGold;
                if (ev != null && ev.tollGold > 0)
                    toll = ev.tollGold;

                if (GoldWallet.Current < toll)
                {
                    Plugin.Log?.LogWarning(
                        $"[EventCore/broker_toll] '{eventId}': need {toll} gold (have {GoldWallet.Current}) — blocked.");
                    return false;
                }

                GoldWallet.ModifyGold(-toll);
                Plugin.Log?.LogInfo($"[EventCore/broker_toll] '{eventId}': paid {toll} gold (balance {GoldWallet.Current}).");
                break;

            case "pay_all_player_gold":
                long g = GoldWallet.Current;
                if (g <= 0)
                {
                    Plugin.Log?.LogWarning($"[EventCore/broker_toll] '{eventId}': pay_all_player_gold but wallet empty.");
                    break;
                }

                GoldWallet.ModifyGold(-g);
                Plugin.Log?.LogInfo($"[EventCore/broker_toll] '{eventId}': paid all carried gold ({g}), balance {GoldWallet.Current}.");
                break;

            case "refuse_threat":
            case "refuse":
                Plugin.Log?.LogInfo($"[EventCore/broker_toll] '{eventId}': refused — no gold charged.");
                break;

            case "ignore_walk":
            case "ignore":
            case "open_ignore":
                Plugin.Log?.LogInfo($"[EventCore/broker_toll] '{eventId}': tried to walk off — broker blocks, toll demand.");
                break;

            case "open_peace":
                Plugin.Log?.LogInfo($"[EventCore/broker_toll] '{eventId}': peaceful reply — route to payment.");
                break;

            case "open_threat":
                Plugin.Log?.LogInfo($"[EventCore/broker_toll] '{eventId}': threat — aggression branch.");
                break;

            case "refuse_pay":
                Plugin.Log?.LogInfo($"[EventCore/broker_toll] '{eventId}': refused to pay — aggression.");
                break;

            case "insufficient_declare":
            case "negotiate_try":
            case "rage_intimidate":
                break;

            case "mindbroken_seduce_locked":
            case "rage_intimidate_locked":
                return false;

            case "mindbroken_seduce":
                if (MindBrokenSystem.Enabled)
                    MindBrokenSystem.AddPercent(0.1f, "eventcore_broker_seduce");
                Plugin.Log?.LogInfo($"[EventCore/broker_toll] '{eventId}': MindBroken seduce (+10% MB if enabled).");
                break;

            case "soft_agree":
                long carried = GoldWallet.Current;
                if (carried > 0)
                {
                    GoldWallet.ModifyGold(-carried);
                    Plugin.Log?.LogInfo(
                        $"[EventCore/broker_toll] '{eventId}': soft agree after negotiate — took carried gold {carried} (balance {GoldWallet.Current}).");
                }
                else
                    Plugin.Log?.LogInfo($"[EventCore/broker_toll] '{eventId}': soft agree — wallet empty; consent grab after modal.");

                break;

            case "accept_body":
                EventCoreBrokerGateFlow.TryDebitCarriedGoldForBodyConsent(ev, eventId);
                break;

            case "refuse_body":
            case "soft_refuse":
                Plugin.Log?.LogInfo($"[EventCore/broker_toll] '{eventId}': refused body / soft refuse — aggression.");
                break;

            default:
                Plugin.Log?.LogInfo($"[EventCore/broker_toll] '{eventId}': outcome '{outcomeKey}' (slot {choiceSlot}).");
                break;
        }

        return true;
    }
}
