using System;

namespace NoREroMod.Systems.EventCore.Content;

/// <summary>
/// Manifest schema kept flat — Unity 5.x JsonUtility does not reliably fill nested wrapper objects.
/// </summary>
[Serializable]
internal class EventCoreManifestFile
{
    public string[] eventFiles = new string[0];
}

[Serializable]
internal class EventCoreEventDefinitionFile
{
    public string id = string.Empty;
    public string handlerId = string.Empty;

    /// <summary>When &gt; 0, broker_toll pay_pass debits this amount; when 0, handler uses its built-in default.</summary>
    public int tollGold;

    /// <summary>
    /// Weight for the negotiation branch that still takes the player's carried gold before the H-scene.
    /// When the JSON value is 0, the runtime default is 70.
    /// </summary>
    public int negotiateTakeGoldBranchWeight;

    /// <summary>
    /// Weight for the direct body-payment branch that does not debit gold on acceptance.
    /// When the JSON value is 0, the runtime default is 30.
    /// </summary>
    public int negotiateBodyOnlyBranchWeight;

    /// <summary>
    /// Optional pool of real combat factions rolled once per host instance and applied
    /// when the encounter escalates into hostility.
    /// </summary>
    public string[] revealFactionPool = new string[0];

    /// <summary>
    /// Optional faction to keep after peaceful/body resolution. Defaults to the
    /// EventCore encounter shell when omitted or invalid.
    /// </summary>
    public string peacefulFactionId = string.Empty;

    /// <summary>FSP: faction key for rep/gold (e.g. bandits). Used when <see cref="handlerId"/> is faction_social.</summary>
    public string fspFactionKey = string.Empty;

    /// <summary>FSP: <c>sex_paid</c> (bandits).</summary>
    public string fspKind = string.Empty;

    /// <summary>Optional inline steps — Unity 5 JsonUtility often leaves this empty; prefer stepFiles.</summary>
    public EventCoreStepDefinition[] steps = new EventCoreStepDefinition[0];

    /// <summary>Paths under HellGateJson/EventCore/, one JSON file per step (single EventCoreStepDefinition object each).</summary>
    public string[] stepFiles = new string[0];

    /// <summary>
    /// Optional reinforcement packs addressable by handler logic.
    /// Each pack contains authored offset slots relative to the active EventCore host.
    /// </summary>
    public EventCoreAmbushDefinition[] ambushes = new EventCoreAmbushDefinition[0];
}

[Serializable]
internal class EventCoreAmbushDefinition
{
    public string ambushId = string.Empty;
    public EventCoreAmbushSpawnSlot[] slots = new EventCoreAmbushSpawnSlot[0];
}

[Serializable]
internal class EventCoreAmbushSpawnSlot
{
    public string enemyType = string.Empty;
    public string factionId = string.Empty;
    public string eventId = string.Empty;
    public float offsetX;
    public float offsetY;
    public int count = 1;
}

[Serializable]
internal class EventCoreStepDefinition
{
    public string stepId = string.Empty;

    /// <summary>choice | continue</summary>
    public string stepKind = "choice";

    public string npcLine = string.Empty;

    /// <summary>Optional speaker line above npcLine (shown on canvas).</summary>
    public string speakerLabel = string.Empty;

    /// <summary>If set, body text is taken from strings_default.json when the key exists.</summary>
    public string npcLineKey = string.Empty;

    /// <summary>Random broker/NPC line from language pack pool (see eventcore_lang.json linePools).</summary>
    public string npcLinePoolId = string.Empty;

    /// <summary>Up to 5 slots: random label per slot from matching pool id (parallel to choiceOutcomeIds).</summary>
    public string[] choicePoolIds = new string[0];

    /// <summary>After this continue step, jump to stepId instead of linear next (optional).</summary>
    public string continueNextStepId = string.Empty;

    /// <summary>Outcome applied when leaving this continue step (broker toll partial gold, etc.).</summary>
    public string continueOutcomeId = string.Empty;

    public string[] choiceLabels = new string[0];

    /// <summary>Parallel to choiceLabels: semantic id for handlers (e.g. pay_pass, refuse_threat).</summary>
    public string[] choiceOutcomeIds = new string[0];

    /// <summary>Parallel to choiceLabels: optional stepId to jump to after this choice; if missing or unknown, next step is linear.</summary>
    public string[] choiceJumpStepIds = new string[0];
}
