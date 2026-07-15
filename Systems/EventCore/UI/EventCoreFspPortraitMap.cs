using System;
using NoREroMod.Systems.EventCore.Content;
using NoREroMod.Systems.EventCore.Handlers;

namespace NoREroMod.Systems.EventCore.UI;

/// <summary>
/// Maps FSP sex_paid steps, pools, and choice outcomes to AradiaAva / TouzokuAva folders
/// (same asset roots as broker — <see cref="EventCorePortraitPaths"/>).
/// </summary>
internal static class EventCoreFspPortraitMap
{
    private static class Aradia
    {
        internal const string Default = EventCoreBrokerPortraitMap.Aradia.Default;
        internal const string DontTuchMePls = EventCoreBrokerPortraitMap.Aradia.DontTuchMePls;
        internal const string Dubcon = EventCoreBrokerPortraitMap.Aradia.Dubcon;
        internal const string Fear = EventCoreBrokerPortraitMap.Aradia.Fear;
        internal const string Fury = EventCoreBrokerPortraitMap.Aradia.Fury;
        internal const string Threat = EventCoreBrokerPortraitMap.Aradia.Threat;
    }

    private static class Touzoku
    {
        internal const string Agre = EventCoreBrokerPortraitMap.Touzoku.Agre;
        internal const string Default = EventCoreBrokerPortraitMap.Touzoku.Default;
        internal const string Fear = EventCoreBrokerPortraitMap.Touzoku.Fear;
        internal const string Pleased = EventCoreBrokerPortraitMap.Touzoku.Pleased;
    }

    internal static EventCorePortraitPair ResolvePrelude()
    {
        return new EventCorePortraitPair(Aradia.Fear, null);
    }

    internal static EventCorePortraitPair ResolveContinueStep(EventCoreStepDefinition step)
    {
        if (step == null)
            return ChoiceMenuIdle();

        string poolId = step.npcLinePoolId ?? string.Empty;
        if (!string.IsNullOrEmpty(poolId))
            return ResolveFromNpcPool(step.stepId, poolId);

        return ResolveFromStepIdOnly(step.stepId);
    }

    internal static EventCorePortraitPair ResolveNpcPreamble(EventCoreStepDefinition step)
    {
        return ResolveContinueStep(step);
    }

    internal static EventCorePortraitPair ResolveChoiceMenu(EventCoreStepDefinition step)
    {
        if (step == null)
            return ChoiceMenuIdle();

        string stepId = step.stepId ?? string.Empty;
        if (string.Equals(stepId, EventCoreFactionSocialFlow.SexPaidOpenStepId, StringComparison.OrdinalIgnoreCase))
            return new EventCorePortraitPair(Aradia.Fear, Touzoku.Default);

        return ChoiceMenuIdle();
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
            else if (step?.choiceOutcomeIds != null && i < step.choiceOutcomeIds.Length)
                left = ResolveLeftFromOutcomeId(step.choiceOutcomeIds[i]);

            enriched[i] = slots[i].WithLeftPortrait(left);
        }

        return enriched;
    }

    internal static string ResolveLeftFromOutcomeId(string outcomeId)
    {
        if (string.IsNullOrEmpty(outcomeId))
            return Aradia.Default;

        switch (outcomeId.Trim().ToLowerInvariant())
        {
            case "refuse_open":
            case "walk_away":
                return Aradia.DontTuchMePls;
            case "hesitate_open":
            case "rep_roll_soft":
                return Aradia.Fear;
            case "money_open":
                return Aradia.Default;
            case "soft_open":
            case "pay_confirm":
                return Aradia.Dubcon;
            case "rough_open":
                return Aradia.Threat;
            case "threaten":
                return Aradia.Fury;
            default:
                return Aradia.Default;
        }
    }

    private static EventCorePortraitPair ChoiceMenuIdle()
    {
        return new EventCorePortraitPair(Aradia.Default, Touzoku.Default);
    }

    private static EventCorePortraitPair ResolveFromNpcPool(string stepId, string poolId)
    {
        switch (poolId)
        {
            case "fsp_sex_paid_prelude":
                return new EventCorePortraitPair(Aradia.Fear, null);
            case "fsp_bandits_sex_paid_open":
                return new EventCorePortraitPair(Aradia.Fear, Touzoku.Default);
            case "fsp_sex_refuse_react":
                return new EventCorePortraitPair(Aradia.DontTuchMePls, Touzoku.Default);
            case "fsp_sex_hesitate_react":
                return new EventCorePortraitPair(Aradia.Fear, Touzoku.Agre);
            case "fsp_sex_money_react":
                return new EventCorePortraitPair(Aradia.Default, Touzoku.Default);
            case "fsp_sex_soft_react":
                return new EventCorePortraitPair(Aradia.Fear, Touzoku.Agre);
            case "fsp_sex_rough_react":
                return new EventCorePortraitPair(Aradia.Threat, Touzoku.Pleased);
            case "fsp_bandits_sex_paid_after_yes":
                return new EventCorePortraitPair(Aradia.Dubcon, Touzoku.Pleased);
            case "fsp_bandits_sex_paid_rage_win":
                return new EventCorePortraitPair(Aradia.Fury, Touzoku.Fear);
            case "fsp_bandits_sex_paid_rage_fail":
                return new EventCorePortraitPair(Aradia.Fury, Touzoku.Default);
            case "fsp_bandits_close_peaceful":
                return new EventCorePortraitPair(Aradia.Default, Touzoku.Default);
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
            case "fsp_sex_refuse_c_p04":
                return new EventCorePortraitPair(Aradia.Dubcon, Touzoku.Agre);
            case "fsp_sex_refuse_d_p03":
                return new EventCorePortraitPair(Aradia.DontTuchMePls, Touzoku.Default);
            case "fsp_sex_hesitate_3_p03":
                return new EventCorePortraitPair(Aradia.Threat, Touzoku.Default);
            case "fsp_sex_hesitate_1b_p04":
            case "fsp_sex_money_price":
            case "fsp_sex_money_after_price":
                return new EventCorePortraitPair(Aradia.Default, Touzoku.Default);
            case "fsp_sex_money_2_p04":
            case "fsp_sex_money_3_p04":
            case "fsp_sex_money_5_p04":
                return new EventCorePortraitPair(Aradia.Dubcon, Touzoku.Agre);
            case "fsp_sex_money_4_p03":
                return new EventCorePortraitPair(Aradia.Fear, Touzoku.Default);
            case "fsp_sex_soft_1_p04":
            case "fsp_sex_soft_3_p04":
                return new EventCorePortraitPair(Aradia.Dubcon, Touzoku.Pleased);
            case "fsp_sex_rough_1_p04":
            case "fsp_sex_rough_2_p04":
            case "fsp_sex_rough_3_p04":
            case "fsp_sex_rough_4_p04":
                return new EventCorePortraitPair(Aradia.Threat, Touzoku.Pleased);
            case "fsp_sex_paid_rage_win":
                return new EventCorePortraitPair(Aradia.Fury, Touzoku.Fear);
            case "fsp_sex_paid_rage_fail":
                return new EventCorePortraitPair(Aradia.Fury, Touzoku.Default);
            case "fsp_sex_paid_close":
                return new EventCorePortraitPair(Aradia.Default, Touzoku.Default);
            default:
                if (stepId.StartsWith("fsp_sex_refuse_", StringComparison.OrdinalIgnoreCase))
                    return new EventCorePortraitPair(Aradia.DontTuchMePls, Touzoku.Default);
                if (stepId.StartsWith("fsp_sex_hesitate_", StringComparison.OrdinalIgnoreCase))
                    return new EventCorePortraitPair(Aradia.Fear, Touzoku.Agre);
                if (stepId.StartsWith("fsp_sex_money_", StringComparison.OrdinalIgnoreCase))
                    return new EventCorePortraitPair(Aradia.Default, Touzoku.Default);
                if (stepId.StartsWith("fsp_sex_soft_", StringComparison.OrdinalIgnoreCase))
                    return new EventCorePortraitPair(Aradia.Dubcon, Touzoku.Agre);
                if (stepId.StartsWith("fsp_sex_rough_", StringComparison.OrdinalIgnoreCase))
                    return new EventCorePortraitPair(Aradia.Threat, Touzoku.Pleased);
                return ChoiceMenuIdle();
        }
    }
}
