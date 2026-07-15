using System;
using System.Collections.Generic;
using System.Globalization;
using NoREroMod;
using NoREroMod.Patches.UI.MindBroken;
using NoREroMod.Systems.Cache;
using NoREroMod.Systems.CombatAi.Factions;
using NoREroMod.Systems.Economy;
using NoREroMod.Systems.EventCore.Content;
using NoREroMod.Systems.EventCore.Host;
using NoREroMod.Systems.EventCore.UI;
using NoREroMod.Systems.Rage;
using NoREroMod.Systems.Spawn;
using UnityEngine;

namespace NoREroMod.Systems.EventCore.Handlers;

/// <summary>
/// Broker toll dialogue for <c>eventcore_broker_gate</c>: gold, partial pay, negotiate, body, rage/MindBroken rows (special colours).
/// </summary>
internal static class EventCoreBrokerGateFlow
{
    internal const string EventId = "eventcore_broker_gate";

    /// <summary>
    /// Pool of randomized intro paragraphs shown before the broker's first spoken line.
    /// </summary>
    internal const string PreludePoolId = "broker_gate_prelude";

    internal const string StepGateOpen = "broker_gate";
    internal const string StepPayChoice = "broker_pay_choice";
    internal const string StepNegotiateIntro = "broker_negotiate_intro";

    internal const string StepThreatRetort = "broker_threat_retort";

    /// <summary>
    /// Negotiation refusal branch with the broker's "I will take it anyway" line, without an extra
    /// <see cref="StepThreatRetort"/> screen because the tone would be redundant.
    /// </summary>
    internal const string StepForceTake = "broker_force_take";

    /// <summary>
    /// Failed rage intimidation branch: a single broker retort with no extra <see cref="StepThreatRetort"/> step.
    /// </summary>
    internal const string StepRageDefiant = "broker_rage_defiant";
    internal const string StepRageScared = "broker_rage_scared";

    internal const string StepPassDone = "broker_pass_done";
    internal const string StepBodyOk = "broker_body_ok";
    internal const string StepMindbrokenBrokerReply = "broker_mindbroken_broker";
    internal const string AmbushRefusalPackId = "broker_refusal_ambush";

    /// <summary>
    /// After an ambush terminal step the modal closes while the player is still adjacent to the broker.
    /// Delay hostile reveal so the player gets a beat to move before grab AI can fire.
    /// </summary>
    internal const float AmbushHostileRevealDelaySeconds = 0.85f;

    /// <summary>
    /// Set when <see cref="StepInsufficientBodyLine"/> routes into body payment — <c>accept_body</c> must debit carried gold.
    /// Cleared for negotiate-only body branches.
    /// </summary>
    internal static bool BodyChoiceDebitCarriedGold;

    internal const string StepInsufficientBodyLine = "broker_insufficient_body_line";

    /// <summary>
    /// Minimum Rage percent required to unlock the intimidation choice.
    /// Success then scales linearly with the current Rage value; see <see cref="ResolveRageIntimidationStepId"/>.
    /// </summary>
    internal const float RageIntimidateUnlockPercentMin = 5f;

    private static readonly Color MbActive = new Color(1f, 0.52f, 0.82f, 1f);
    private static readonly Color RageActive = new Color(1f, 0.38f, 0.34f, 1f);
    private static readonly Color Normal = Color.white;
    private static readonly Color LockedSpecial = new Color(0.5f, 0.5f, 0.53f, 1f);

    internal static bool IsBrokerGate(EventCoreEventDefinitionFile ev) =>
        ev != null && string.Equals(ev.id, EventId, StringComparison.OrdinalIgnoreCase);

    internal static long GetToll(EventCoreEventDefinitionFile ev)
    {
        if (ev != null && ev.tollGold > 0)
            return ev.tollGold;
        return 25L;
    }

    internal static bool ShouldOfferInsufficient(long toll, long gold) => gold > 0 && gold < toll;

    internal static bool ShouldOfferNegotiate(long toll, long gold) => gold < toll;

    internal static bool IsAcceptLessBracket(long toll, long gold) =>
        gold > 0 && gold < toll && gold >= (long)(toll * 0.80);

    internal static string ResolveInsufficientJumpStepId(EventCoreEventDefinitionFile ev)
    {
        long toll = GetToll(ev);
        long g = GoldWallet.Current;
        if (g <= 0)
            return "broker_insufficient_body_line";
        return IsAcceptLessBracket(toll, g) ? "broker_insufficient_accept_less" : "broker_insufficient_body_line";
    }

    private static string PickLine(string poolId, long toll, long gold)
    {
        if (!EventCoreStringRegistry.TryGetRandomLine(poolId, out string line) || string.IsNullOrEmpty(line))
            return "[EventCore: missing line pool text; check active EventCore language pack (eventcore_lang.json), pool " + poolId + "]";
        return EventCoreStringRegistry.FormatLine(line, toll, gold);
    }

    private static string MindBrokenLockedHint =>
        EventCoreStringRegistry.TryGet("eventcore_hint_need_mb", out string s) ? s : string.Empty;

    private static string RageLockedHint =>
        EventCoreStringRegistry.TryGet("eventcore_hint_need_rage", out string s) ? s : string.Empty;

    private static bool TryAddChoice(
        List<EventCoreChoiceSlotUi> slots,
        List<string> outcomes,
        List<string> jumps,
        EventCoreChoiceSlotUi slot,
        string outcome,
        string jumpStepId)
    {
        if (slots == null || outcomes == null || jumps == null || slots.Count >= 5)
            return false;

        slots.Add(slot);
        outcomes.Add(outcome ?? string.Empty);
        jumps.Add(jumpStepId ?? string.Empty);
        return true;
    }

    private static void AddNormalChoice(
        List<EventCoreChoiceSlotUi> slots,
        List<string> outcomes,
        List<string> jumps,
        string poolId,
        string outcome,
        string jumpStepId,
        long toll,
        long gold)
    {
        TryAddChoice(
            slots,
            outcomes,
            jumps,
            new EventCoreChoiceSlotUi(PickLine(poolId, toll, gold), true, Normal),
            outcome,
            jumpStepId);
    }

    private static void AddConditionalChoice(
        List<EventCoreChoiceSlotUi> slots,
        List<string> outcomes,
        List<string> jumps,
        string poolId,
        bool unlocked,
        string unlockedOutcome,
        string lockedOutcome,
        string unlockedJumpStepId,
        Color activeColor,
        string lockedHint,
        long toll,
        long gold)
    {
        TryAddChoice(
            slots,
            outcomes,
            jumps,
            new EventCoreChoiceSlotUi(
                PickLine(poolId, toll, gold),
                unlocked,
                unlocked ? activeColor : LockedSpecial,
                unlocked ? string.Empty : lockedHint),
            unlocked ? unlockedOutcome : lockedOutcome,
            unlocked ? unlockedJumpStepId : string.Empty);
    }

    internal static void BuildPayChoices(
        EventCoreEventDefinitionFile ev,
        out EventCoreChoiceSlotUi[] slots,
        out string[] outcomes,
        out string[] jumps)
    {
        long toll = GetToll(ev);
        long gold = GoldWallet.Current;

        var sl = new List<EventCoreChoiceSlotUi>();
        var oc = new List<string>();
        var jm = new List<string>();

        if (gold >= toll)
            AddNormalChoice(sl, oc, jm, "broker_pay_agree_e", "pay_pass", "broker_pass_done", toll, gold);

        AddNormalChoice(sl, oc, jm, "broker_refuse_pay_l", "refuse_pay", "broker_threat_retort", toll, gold);

        if (ShouldOfferInsufficient(toll, gold))
            AddNormalChoice(sl, oc, jm, "broker_pc_insufficient_f", "insufficient_declare", string.Empty, toll, gold);

        if (ShouldOfferNegotiate(toll, gold))
            AddNormalChoice(sl, oc, jm, "broker_pc_negotiate_other_m", "negotiate_try", "broker_negotiate_intro", toll, gold);

        AddConditionalChoice(
            sl, oc, jm,
            "broker_pc_mindbroken_seduce_u",
            MindBrokenSystem.Enabled && MindBrokenSystem.Percent >= 0.2f,
            "mindbroken_seduce",
            "mindbroken_seduce_locked",
            "broker_mindbroken_broker",
            MbActive,
            MindBrokenLockedHint,
            toll,
            gold);

        AddConditionalChoice(
            sl, oc, jm,
            "broker_pc_rage_r",
            RageSystem.Enabled && RageSystem.Percent >= RageIntimidateUnlockPercentMin,
            "rage_intimidate",
            "rage_intimidate_locked",
            string.Empty,
            RageActive,
            RageLockedHint,
            toll,
            gold);

        slots = sl.ToArray();
        outcomes = oc.ToArray();
        jumps = jm.ToArray();
    }

    /// <summary>
    /// First broker screen after opening line: one row per logical choice (peace, threat, walk, MB, rage).
    /// JSON repeating the same pool id on multiple slots caused duplicate labels — routing is defined here per row.
    /// </summary>
    internal static void BuildGateOpenChoices(
        EventCoreEventDefinitionFile ev,
        out EventCoreChoiceSlotUi[] slots,
        out string[] outcomes,
        out string[] jumps)
    {
        long toll = GetToll(ev);
        long gold = GoldWallet.Current;

        var sl = new List<EventCoreChoiceSlotUi>();
        var oc = new List<string>();
        var jm = new List<string>();

        AddNormalChoice(sl, oc, jm, "broker_pc_peace", "open_peace", "broker_demand", toll, gold);
        AddNormalChoice(sl, oc, jm, "broker_pc_threat", "open_threat", "broker_threat_retort", toll, gold);
        AddNormalChoice(sl, oc, jm, "broker_pc_walk_ignore", "open_ignore", "broker_demand", toll, gold);

        AddConditionalChoice(
            sl, oc, jm,
            "broker_pc_mindbroken_seduce_u",
            MindBrokenSystem.Enabled && MindBrokenSystem.Percent >= 0.2f,
            "mindbroken_seduce",
            "mindbroken_seduce_locked",
            "broker_mindbroken_broker",
            MbActive,
            MindBrokenLockedHint,
            toll,
            gold);

        AddConditionalChoice(
            sl, oc, jm,
            "broker_pc_rage_r",
            RageSystem.Enabled && RageSystem.Percent >= RageIntimidateUnlockPercentMin,
            "rage_intimidate",
            "rage_intimidate_locked",
            string.Empty,
            RageActive,
            RageLockedHint,
            toll,
            gold);

        slots = sl.ToArray();
        outcomes = oc.ToArray();
        jumps = jm.ToArray();
    }

    internal static void PrepareNegotiateIntro(EventCoreEventDefinitionFile ev, out string body, out string nextStepId)
    {
        long toll = GetToll(ev);
        long gold = GoldWallet.Current;
        int wTake = ev != null && ev.negotiateTakeGoldBranchWeight > 0 ? ev.negotiateTakeGoldBranchWeight : 70;
        int wBody = ev != null && ev.negotiateBodyOnlyBranchWeight > 0 ? ev.negotiateBodyOnlyBranchWeight : 30;
        int sum = wTake + wBody;
        if (sum <= 0)
        {
            wTake = 70;
            wBody = 30;
            sum = 100;
        }

        // The "negotiate -> soft_agree" branch still takes carried gold.
        // The direct body-payment branch reaches accept_body without debiting gold here.
        bool takeGoldNegotiationBranch = UnityEngine.Random.Range(0, sum) < wTake;
        if (takeGoldNegotiationBranch)
        {
            EventCoreStringRegistry.TryGetRandomLine("broker_broker_entertain_offer_n", out string line);
            body = EventCoreStringRegistry.FormatLine(line ?? string.Empty, toll, gold);
            if (string.IsNullOrEmpty(body))
                body = PickLine("broker_broker_entertain_offer_n", toll, gold);
            nextStepId = "broker_neg_soft_choice";
            BodyChoiceDebitCarriedGold = false;
        }
        else
        {
            EventCoreStringRegistry.TryGetRandomLine("broker_broker_body_topup_h", out string line);
            body = EventCoreStringRegistry.FormatLine(line ?? string.Empty, toll, gold);
            if (string.IsNullOrEmpty(body))
                body = PickLine("broker_broker_body_topup_h", toll, gold);
            nextStepId = "broker_body_choice";
            BodyChoiceDebitCarriedGold = gold > 0;
        }
    }

    internal static void ClearSessionState()
    {
        BodyChoiceDebitCarriedGold = false;
    }

    internal static void MarkInsufficientBodyChoiceDebit()
    {
        BodyChoiceDebitCarriedGold = true;
    }

    internal static void TryDebitCarriedGoldForBodyConsent(EventCoreEventDefinitionFile ev, string eventId)
    {
        if (!BodyChoiceDebitCarriedGold)
            return;

        long carriedGold = GoldWallet.Current;
        if (carriedGold <= 0)
        {
            Plugin.Log?.LogInfo($"[EventCore/broker_toll] '{eventId}': body consent — wallet empty.");
            return;
        }

        GoldWallet.ModifyGold(-carriedGold);
        Plugin.Log?.LogInfo(
            $"[EventCore/broker_toll] '{eventId}': body consent — took carried gold {carriedGold} (balance {GoldWallet.Current}).");
    }

    /// <summary>
    /// Resolves the rage intimidation outcome with a linear success chance:
    /// <c>P = clamp(ragePercent, 0..100) / 100</c>.
    /// </summary>
    internal static string ResolveRageIntimidationStepId()
    {
        float rage = RageSystem.Enabled ? Mathf.Clamp(RageSystem.Percent, 0f, 100f) : 0f;
        float scareChance = rage / 100f;
        return UnityEngine.Random.value < scareChance ? "broker_rage_scared" : StepRageDefiant;
    }

    /// <summary>
    /// Only voluntary body-payment outcomes transition into the enemy knockdown flow.
    /// Threat branches and refusals never apply knockdown here.
    /// </summary>
    internal static bool ShouldBrokerTerminalApplyKnockdown(string stepId)
    {
        if (string.IsNullOrEmpty(stepId))
            return false;
        if (ShouldBrokerTerminalSpawnAmbush(stepId))
            return false;
        return string.Equals(stepId, StepBodyOk, StringComparison.OrdinalIgnoreCase)
               || string.Equals(stepId, StepMindbrokenBrokerReply, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ShouldBrokerTerminalSpawnAmbush(string stepId)
    {
        if (string.IsNullOrEmpty(stepId))
            return false;

        return string.Equals(stepId, StepThreatRetort, StringComparison.OrdinalIgnoreCase)
               || string.Equals(stepId, StepForceTake, StringComparison.OrdinalIgnoreCase)
               || string.Equals(stepId, StepRageDefiant, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ShouldBrokerContinueApplyResolutionBeforeAdvance(string stepId)
    {
        if (string.IsNullOrEmpty(stepId))
            return false;

        return string.Equals(stepId, StepRageScared, StringComparison.OrdinalIgnoreCase);
    }

    internal static void ApplyTerminalResolution(string stepId, EventCoreHost host)
    {
        if (host == null || string.IsNullOrEmpty(stepId))
            return;

        if (ShouldBrokerTerminalSpawnAmbush(stepId))
            return;

        if (string.Equals(stepId, StepRageScared, StringComparison.OrdinalIgnoreCase))
        {
            host.MarkCowardlyFleePending();
            return;
        }

        if (ShouldBrokerTerminalApplyKnockdown(stepId))
        {
            host.ResolveBodyPayment();
            return;
        }

        if (string.Equals(stepId, StepPassDone, StringComparison.OrdinalIgnoreCase))
        {
            if (host.ShouldResolveCowardlyFleeOnPassDone())
                host.ResolveCowardlyFlee();
            else
                host.ResolvePeaceful();
        }
    }

    internal static void TrySpawnBrokerAmbush(EventCoreEventDefinitionFile ev, string stepId, EventCoreHost host)
    {
        if (!ShouldBrokerTerminalSpawnAmbush(stepId))
            return;

        if (ev == null)
        {
            Plugin.Log?.LogWarning("[EventCore/broker_toll] Ambush spawn skipped: event definition missing.");
            return;
        }

        if (!IsHostAlive(host))
        {
            Plugin.Log?.LogInfo("[EventCore/broker_toll] Ambush spawn skipped: broker host is missing or dead.");
            return;
        }

        if (!TryGetAmbush(ev, AmbushRefusalPackId, out EventCoreAmbushDefinition ambush))
        {
            Plugin.Log?.LogWarning(
                $"[EventCore/broker_toll] Ambush pack '{AmbushRefusalPackId}' missing for event '{ev.id}'.");
            return;
        }

        EventCoreAmbushSpawnSlot[] slots = ambush.slots;
        if (slots == null || slots.Length == 0)
        {
            Plugin.Log?.LogWarning(
                $"[EventCore/broker_toll] Ambush pack '{AmbushRefusalPackId}' has no slots for event '{ev.id}'.");
            return;
        }

        Vector2 origin = new Vector2(host.transform.position.x, host.transform.position.y);
        var runtimePoints = BuildShuffledAmbushRuntimePoints(slots, origin, host);

        if (runtimePoints.Count == 0)
        {
            Plugin.Log?.LogWarning(
                $"[EventCore/broker_toll] Ambush pack '{AmbushRefusalPackId}' has no valid runtime slots for event '{ev.id}'.");
            return;
        }

        int spawned = SpawnConfigExecutor.SpawnRuntimePack(
            runtimePoints.ToArray(),
            "[EventCore/broker_toll]",
            markHostileToPlayer: true,
            suppressFactionMarker: true);
        if (spawned <= 0)
        {
            Plugin.Log?.LogWarning($"[EventCore/broker_toll] Ambush spawn failed for event '{ev.id}'.");
            return;
        }

        Plugin.Log?.LogInfo(
            $"[EventCore/broker_toll] Spawned ambush '{AmbushRefusalPackId}' for event '{ev.id}' ({spawned} enemy instance(s)).");
    }

    internal static void ScheduleAmbushHostileReveal(EventCoreHost host)
    {
        if (host == null)
            return;

        host.ScheduleHostileReveal(AmbushHostileRevealDelaySeconds);
        Plugin.Log?.LogInfo(
            $"[EventCore/broker_toll] Broker hostile reveal scheduled in {AmbushHostileRevealDelaySeconds:0.##}s (ambush branch).");
    }

    /// <summary>
    /// After voluntary body-payment the modal closes and the broker uses grab-via-attack (no knockdown fall).
    /// </summary>
    internal static void ApplyBrokerConsentGrab(
        EventCoreHost host,
        string terminalStepId = null,
        EventCoreHandoffMode mode = EventCoreHandoffMode.Consent)
    {
        if (!string.IsNullOrEmpty(terminalStepId) && ShouldBrokerTerminalSpawnAmbush(terminalStepId))
        {
            Plugin.Log?.LogWarning(
                $"[EventCore/broker_toll] ApplyBrokerConsentGrab blocked for ambush terminal step '{terminalStepId}'.");
            return;
        }

        if (host == null)
        {
            Plugin.Log?.LogWarning("[EventCore/broker_toll] ApplyBrokerConsentGrab: EventCoreHost missing.");
            return;
        }

        playercon pc = UnifiedPlayerCacheManager.GetPlayer();
        if (pc == null)
        {
            Plugin.Log?.LogWarning("[EventCore/broker_toll] ApplyBrokerConsentGrab: playercon missing.");
            return;
        }

        if (pc.eroflag || pc._eroflag2)
        {
            Plugin.Log?.LogInfo("[EventCore/broker_toll] ApplyBrokerConsentGrab skipped: player already in H-scene.");
            return;
        }

        host.BeginBrokerConsentGrab(mode);
    }

    /// <summary>Legacy knockdown handoff (ImmediatelyERO). Broker/FSP use <see cref="ApplyBrokerConsentGrab"/>.</summary>
    internal static void ApplyBrokerKnockdown(EventCoreHost host, string terminalStepId = null)
    {
        if (!string.IsNullOrEmpty(terminalStepId) && ShouldBrokerTerminalSpawnAmbush(terminalStepId))
        {
            Plugin.Log?.LogWarning(
                $"[EventCore/broker_toll] ApplyBrokerKnockdown blocked for ambush terminal step '{terminalStepId}'.");
            return;
        }

        playercon pc = UnifiedPlayerCacheManager.GetPlayer();
        if (pc == null)
        {
            Plugin.Log?.LogWarning("[EventCore/broker_toll] ApplyBrokerKnockdown: playercon missing.");
            return;
        }

        if (pc.eroflag || pc._eroflag2)
        {
            Plugin.Log?.LogInfo("[EventCore/broker_toll] ApplyBrokerKnockdown skipped: player already in H-scene.");
            return;
        }

        try
        {
            pc.ImmediatelyERO();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[EventCore/broker_toll] ImmediatelyERO failed: {ex.Message}");
        }

        try
        {
            StruggleSystem.setStruggleLevel(-1);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"[EventCore/broker_toll] StruggleSystem.setStruggleLevel failed: {ex.Message}");
        }

        PlayerStatus ps = ResolveGameControllerPlayerStatus();
        if (ps != null)
            ps.Sp = 0f;
    }

    private static PlayerStatus ResolveGameControllerPlayerStatus()
    {
        GameObject gc = GameObject.FindGameObjectWithTag("GameController");
        return gc != null ? gc.GetComponent<PlayerStatus>() : null;
    }

    private static List<SpawnConfigExecutor.RuntimeSpawnPoint> BuildShuffledAmbushRuntimePoints(
        EventCoreAmbushSpawnSlot[] slots,
        Vector2 origin,
        EventCoreHost host)
    {
        var runtimePoints = new List<SpawnConfigExecutor.RuntimeSpawnPoint>();
        if (slots == null || slots.Length == 0)
            return runtimePoints;

        var totalByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var slotPlans = new List<AmbushSlotPlan>();
        string defaultFaction = host != null
            ? host.GetRolledRevealFactionId().ToString(CultureInfo.InvariantCulture)
            : FactionIds.Bandits.ToString(CultureInfo.InvariantCulture);

        for (int i = 0; i < slots.Length; i++)
        {
            EventCoreAmbushSpawnSlot slot = slots[i];
            if (slot == null || string.IsNullOrEmpty(slot.enemyType))
                continue;

            string enemyType = slot.enemyType.Trim();
            int count = Mathf.Max(1, slot.count);
            if (!totalByType.ContainsKey(enemyType))
                totalByType[enemyType] = 0;
            totalByType[enemyType] += count;

            slotPlans.Add(new AmbushSlotPlan
            {
                SlotCenter = origin + new Vector2(slot.offsetX, slot.offsetY),
                Count = count,
                FactionIdRaw = string.IsNullOrEmpty(slot.factionId) ? defaultFaction : slot.factionId.Trim(),
                EventId = string.IsNullOrEmpty(slot.eventId) ? null : slot.eventId.Trim()
            });
        }

        if (slotPlans.Count == 0)
            return runtimePoints;

        var remainingByType = new Dictionary<string, int>(totalByType, StringComparer.OrdinalIgnoreCase);
        int remainingUnits = 0;
        foreach (KeyValuePair<string, int> pair in totalByType)
            remainingUnits += pair.Value;

        bool mixTypes = totalByType.Count > 1;

        for (int i = 0; i < slotPlans.Count; i++)
        {
            AmbushSlotPlan plan = slotPlans[i];
            List<string> slotTypes = mixTypes
                ? BuildMixedTypesForGroup(plan.Count, remainingByType, ref remainingUnits)
                : BuildSingleTypeGroup(plan.Count, remainingByType);

            for (int j = 0; j < plan.Count; j++)
            {
                runtimePoints.Add(new SpawnConfigExecutor.RuntimeSpawnPoint
                {
                    Center = plan.SlotCenter + SpawnConfigExecutor.GetGroupSpawnOffset(j, plan.Count),
                    EnemyType = slotTypes[j],
                    FactionIdRaw = plan.FactionIdRaw,
                    EventCoreEventId = plan.EventId,
                    Count = 1
                });
            }
        }

        return runtimePoints;
    }

    private sealed class AmbushSlotPlan
    {
        internal Vector2 SlotCenter;
        internal int Count;
        internal string FactionIdRaw = string.Empty;
        internal string EventId;
    }

    private static List<string> BuildSingleTypeGroup(int groupSize, Dictionary<string, int> remainingByType)
    {
        var result = new List<string>(groupSize);
        foreach (KeyValuePair<string, int> pair in remainingByType)
        {
            if (pair.Value <= 0)
                continue;

            int take = Mathf.Min(groupSize, pair.Value);
            for (int i = 0; i < take; i++)
                result.Add(pair.Key);
            remainingByType[pair.Key] -= take;
            break;
        }

        return result;
    }

    /// <summary>
    /// Split the remaining ambush roster across one flank/slot so each type appears
    /// proportionally (4N+4A with two flanks of 4 → ~2 sword + 2 axe per side).
    /// </summary>
    private static List<string> BuildMixedTypesForGroup(
        int groupSize,
        Dictionary<string, int> remainingByType,
        ref int remainingUnits)
    {
        var result = new List<string>(groupSize);
        if (groupSize <= 0 || remainingUnits <= 0)
            return result;

        var shares = new List<AmbushTypeShare>();
        int assigned = 0;

        foreach (KeyValuePair<string, int> pair in remainingByType)
        {
            if (pair.Value <= 0)
                continue;

            float exact = pair.Value * (float)groupSize / remainingUnits;
            int floor = Mathf.FloorToInt(exact);
            shares.Add(new AmbushTypeShare
            {
                EnemyType = pair.Key,
                Floor = floor,
                Fraction = exact - floor
            });
            assigned += floor;
        }

        shares.Sort((a, b) => b.Fraction.CompareTo(a.Fraction));

        int leftover = groupSize - assigned;
        for (int i = 0; i < shares.Count && leftover > 0; i++)
        {
            shares[i].Floor++;
            leftover--;
        }

        for (int i = 0; i < shares.Count; i++)
        {
            AmbushTypeShare share = shares[i];
            if (share.Floor <= 0)
                continue;

            int available = remainingByType.TryGetValue(share.EnemyType, out int left) ? left : 0;
            int take = Mathf.Min(share.Floor, available);
            for (int j = 0; j < take; j++)
                result.Add(share.EnemyType);
            remainingByType[share.EnemyType] = available - take;
            remainingUnits -= take;
        }

        ShuffleEnemyTypes(result);
        return result;
    }

    private sealed class AmbushTypeShare
    {
        internal string EnemyType = string.Empty;
        internal int Floor;
        internal float Fraction;
    }

    private static void ShuffleEnemyTypes(List<string> enemyTypes)
    {
        for (int i = enemyTypes.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            string tmp = enemyTypes[i];
            enemyTypes[i] = enemyTypes[j];
            enemyTypes[j] = tmp;
        }
    }

    private static bool TryGetAmbush(EventCoreEventDefinitionFile ev, string ambushId, out EventCoreAmbushDefinition ambush)
    {
        ambush = null;
        if (ev == null || string.IsNullOrEmpty(ambushId) || ev.ambushes == null || ev.ambushes.Length == 0)
            return false;

        for (int i = 0; i < ev.ambushes.Length; i++)
        {
            EventCoreAmbushDefinition candidate = ev.ambushes[i];
            if (candidate == null || string.IsNullOrEmpty(candidate.ambushId))
                continue;

            if (string.Equals(candidate.ambushId, ambushId, StringComparison.OrdinalIgnoreCase))
            {
                ambush = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool IsHostAlive(EventCoreHost host)
    {
        if (host == null || host.gameObject == null || !host.gameObject.activeInHierarchy)
            return false;

        EnemyDate enemy = host.GetComponent<EnemyDate>();
        return enemy == null || enemy.Hp > 0f;
    }

    internal static bool IsBrokerTerminalContinueStep(string stepId)
    {
        if (string.IsNullOrEmpty(stepId))
            return false;
        return string.Equals(stepId, StepThreatRetort, StringComparison.OrdinalIgnoreCase)
               || string.Equals(stepId, StepForceTake, StringComparison.OrdinalIgnoreCase)
               || string.Equals(stepId, StepRageDefiant, StringComparison.OrdinalIgnoreCase)
               || string.Equals(stepId, StepPassDone, StringComparison.OrdinalIgnoreCase)
               || string.Equals(stepId, StepBodyOk, StringComparison.OrdinalIgnoreCase)
               || string.Equals(stepId, StepMindbrokenBrokerReply, StringComparison.OrdinalIgnoreCase);
    }
}
