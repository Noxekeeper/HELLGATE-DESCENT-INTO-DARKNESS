using System;
using UnityEngine;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.EventCore.Content;

namespace NoREroMod.Systems.EventCore.Handlers;

/// <summary>
/// Per-session context for <c>eventcore_fsp_bandits_sex_paid</c>.
/// </summary>
internal static class EventCoreFactionSocialSession
{
    internal const float RageThreatenUnlockPercent = 20f;

    internal static bool Active { get; private set; }
    internal static int FactionId = FactionIds.Bandits;
    internal static long SexPriceGold;

    /// <summary>True while <see cref="EventCoreChoiceApply.ApplyContinueOutcome"/> is running (vs choice pick).</summary>
    internal static bool ApplyingOnContinue { get; set; }

    internal static void Begin(EventCoreEventDefinitionFile ev)
    {
        Active = ev != null && EventCoreFactionSocialFlow.IsSexPaid(ev);
        if (!Active)
        {
            Clear();
            return;
        }

        FactionId = ResolveFactionId(ev.fspFactionKey);

        float mbPct = Mathf.Clamp01(MindBrokenSystem.Percent) * 100f;
        float tSex = Mathf.Clamp01(mbPct / 100f);
        SexPriceGold = Mathf.RoundToInt(Mathf.Lerp(200f, 20f, tSex));

        Plugin.Log?.LogInfo(
            $"[EventCore/fsp] sex_paid sexPrice={SexPriceGold} mb={mbPct:F0}%");
    }

    internal static void Clear()
    {
        Active = false;
        FactionId = FactionIds.Bandits;
        SexPriceGold = 0;
        EventCoreFactionSocialFlow.ClearPendingHHandoff();
    }

    internal static long GetDisplayGoldForKind() =>
        Active ? SexPriceGold : 0;

    private static int ResolveFactionId(string key)
    {
        if (FactionIds.TryParse(key, out int id))
            return id;
        return FactionIds.Bandits;
    }
}
