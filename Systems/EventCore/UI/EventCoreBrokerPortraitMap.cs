using System;
using NoREroMod.Systems.EventCore.Content;
using NoREroMod.Systems.EventCore.Handlers;

namespace NoREroMod.Systems.EventCore.UI;

/// <summary>Maps broker gate dialogue steps, pools, and outcomes to AradiaAva / TouzokuAva expression folders.</summary>
internal static class EventCoreBrokerPortraitMap
{
    internal static class Aradia
    {
        internal const string Default = "Default";
        internal const string DontTuchMePls = "DontTuchMePls";
        internal const string Dubcon = "Dubcon";
        internal const string Fear = "Fear";
        internal const string Fury = "FURY";
        internal const string MindBroken = "MindBroken";
        internal const string Threat = "Threat";
    }

    internal static class Touzoku
    {
        internal const string Agre = "Agre";
        internal const string Default = "Default";
        internal const string Fear = "Fear";
        internal const string Pleased = "Pleased";
    }

    internal static EventCorePortraitPair ResolvePrelude()
    {
        return new EventCorePortraitPair(Aradia.Fear, null);
    }

    internal static EventCorePortraitPair ResolveContinueStep(
        EventCoreStepDefinition step,
        string negotiateIntroNextStepId)
    {
        if (step == null)
            return ChoiceMenuIdle();

        string stepId = step.stepId ?? string.Empty;
        string poolId = step.npcLinePoolId ?? string.Empty;
        string lineKey = step.npcLineKey ?? string.Empty;

        if (string.Equals(stepId, EventCoreBrokerGateFlow.StepNegotiateIntro, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(negotiateIntroNextStepId, "broker_neg_soft_choice", StringComparison.OrdinalIgnoreCase))
                return new EventCorePortraitPair(Aradia.Fear, Touzoku.Agre);
            if (string.Equals(negotiateIntroNextStepId, "broker_body_choice", StringComparison.OrdinalIgnoreCase))
                return new EventCorePortraitPair(Aradia.Fear, Touzoku.Default);
            return new EventCorePortraitPair(Aradia.Fear, Touzoku.Default);
        }

        if (!string.IsNullOrEmpty(poolId))
            return ResolveFromNpcPool(stepId, poolId);

        if (string.Equals(lineKey, "eventcore_broker_pass_done", StringComparison.OrdinalIgnoreCase))
            return new EventCorePortraitPair(Aradia.Default, Touzoku.Agre);

        return ResolveFromStepIdOnly(stepId);
    }

    internal static EventCorePortraitPair ResolveNpcPreamble(EventCoreStepDefinition step)
    {
        if (step == null)
            return ChoiceMenuIdle();

        string poolId = step.npcLinePoolId ?? string.Empty;
        if (!string.IsNullOrEmpty(poolId))
            return ResolveFromNpcPool(step.stepId, poolId);

        return ResolveFromStepIdOnly(step.stepId);
    }

    internal static EventCorePortraitPair ResolveChoiceMenu(EventCoreStepDefinition step)
    {
        return ChoiceMenuIdle();
    }

    internal static string ResolveLeftFromPoolId(string poolId)
    {
        if (string.IsNullOrEmpty(poolId))
            return Aradia.Default;

        switch (poolId)
        {
            case "broker_pc_peace":
            case "broker_pay_agree_e":
                return Aradia.Default;
            case "broker_pc_threat":
            case "broker_refuse_pay_l":
                return Aradia.Threat;
            case "broker_pc_walk_ignore":
            case "broker_pc_insufficient_f":
            case "broker_pc_negotiate_other_m":
                return Aradia.Fear;
            case "broker_pc_body_yes_j":
            case "broker_pc_soft_agree_o":
                return Aradia.Dubcon;
            case "broker_pc_refuse_body_i":
            case "broker_pc_soft_refuse_p":
                return Aradia.DontTuchMePls;
            case "broker_pc_mindbroken_seduce_u":
                return Aradia.MindBroken;
            case "broker_pc_rage_r":
                return Aradia.Fury;
            default:
                return Aradia.Default;
        }
    }

    internal static string ResolveLeftFromOutcomeId(string outcomeId)
    {
        if (string.IsNullOrEmpty(outcomeId))
            return Aradia.Default;

        switch (outcomeId)
        {
            case "open_peace":
            case "pay_pass":
                return Aradia.Default;
            case "open_threat":
            case "refuse_pay":
                return Aradia.Threat;
            case "open_ignore":
            case "insufficient_declare":
            case "negotiate_try":
                return Aradia.Fear;
            case "accept_body":
            case "soft_agree":
                return Aradia.Dubcon;
            case "refuse_body":
            case "soft_refuse":
                return Aradia.DontTuchMePls;
            case "mindbroken_seduce":
            case "mindbroken_seduce_locked":
                return Aradia.MindBroken;
            case "rage_intimidate":
            case "rage_intimidate_locked":
                return Aradia.Fury;
            default:
                return Aradia.Default;
        }
    }

    internal static EventCoreChoiceSlotUi[] EnrichChoiceSlots(
        EventCoreChoiceSlotUi[] slots,
        string[] outcomeIds,
        EventCoreStepDefinition step)
    {
        if (slots == null || slots.Length == 0)
            return slots;

        var enriched = new EventCoreChoiceSlotUi[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            string left = Aradia.Default;
            if (outcomeIds != null && i < outcomeIds.Length && !string.IsNullOrEmpty(outcomeIds[i]))
                left = ResolveLeftFromOutcomeId(outcomeIds[i]);
            else if (step != null && step.choicePoolIds != null && i < step.choicePoolIds.Length)
                left = ResolveLeftFromPoolId(step.choicePoolIds[i]);
            else if (step != null && step.choiceOutcomeIds != null && i < step.choiceOutcomeIds.Length)
                left = ResolveLeftFromOutcomeId(step.choiceOutcomeIds[i]);

            enriched[i] = slots[i].WithLeftPortrait(left);
        }

        return enriched;
    }

    private static EventCorePortraitPair ChoiceMenuIdle()
    {
        return new EventCorePortraitPair(Aradia.Default, Touzoku.Default);
    }

    private static EventCorePortraitPair ResolveFromNpcPool(string stepId, string poolId)
    {
        switch (poolId)
        {
            case "broker_open_a":
                return new EventCorePortraitPair(Aradia.Fear, Touzoku.Default);
            case "broker_demand":
                return new EventCorePortraitPair(Aradia.Fear, Touzoku.Default);
            case "broker_after_threat_d":
                return new EventCorePortraitPair(Aradia.Threat, Touzoku.Default);
            case "broker_broker_accept_less_g":
                return new EventCorePortraitPair(Aradia.Default, Touzoku.Agre);
            case "broker_broker_body_topup_h":
                return new EventCorePortraitPair(Aradia.Fear, Touzoku.Default);
            case "broker_broker_body_ok_k":
                return new EventCorePortraitPair(Aradia.Dubcon, Touzoku.Pleased);
            case "broker_broker_entertain_offer_n":
                return new EventCorePortraitPair(Aradia.Fear, Touzoku.Agre);
            case "broker_broker_take_anyway_q":
                return new EventCorePortraitPair(Aradia.DontTuchMePls, Touzoku.Default);
            case "broker_walk_away":
                return new EventCorePortraitPair(Aradia.Default, Touzoku.Default);
            case "broker_broker_rage_scared_s":
                return new EventCorePortraitPair(Aradia.Fury, Touzoku.Fear);
            case "broker_broker_rage_defiant_t":
                return new EventCorePortraitPair(Aradia.Fury, Touzoku.Default);
            case "broker_broker_mindbroken_reply_v":
                return new EventCorePortraitPair(Aradia.MindBroken, Touzoku.Pleased);
            default:
                return ResolveFromStepIdOnly(stepId);
        }
    }

    private static EventCorePortraitPair ResolveFromStepIdOnly(string stepId)
    {
        if (string.IsNullOrEmpty(stepId))
            return ChoiceMenuIdle();

        switch (stepId)
        {
            case "broker_threat_retort":
                return new EventCorePortraitPair(Aradia.Threat, Touzoku.Default);
            case "broker_insufficient_accept_less":
                return new EventCorePortraitPair(Aradia.Default, Touzoku.Agre);
            case "broker_insufficient_body_line":
                return new EventCorePortraitPair(Aradia.Fear, Touzoku.Default);
            case "broker_body_ok":
                return new EventCorePortraitPair(Aradia.Dubcon, Touzoku.Pleased);
            case "broker_force_take":
                return new EventCorePortraitPair(Aradia.DontTuchMePls, Touzoku.Default);
            case "broker_rage_scared":
                return new EventCorePortraitPair(Aradia.Fury, Touzoku.Fear);
            case "broker_rage_defiant":
                return new EventCorePortraitPair(Aradia.Fury, Touzoku.Default);
            case "broker_mindbroken_broker":
                return new EventCorePortraitPair(Aradia.MindBroken, Touzoku.Pleased);
            case "broker_pass_done":
                return new EventCorePortraitPair(Aradia.Default, Touzoku.Agre);
            case "broker_walk_away":
                return new EventCorePortraitPair(Aradia.Default, Touzoku.Default);
            default:
                return ChoiceMenuIdle();
        }
    }
}
