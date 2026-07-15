using System;
using UnityEngine;

namespace NoREroMod.Systems.EventCore.Reinforcement;

[Serializable]
internal sealed class ReinforcementRegistryEncounterSpec
{
    public string eventFolder = string.Empty;
    public string eventSceneContains = string.Empty;
}

/// <summary>Root JSON: <c>HellGateJson/EventCore/reinforcement_registry.json</c>.</summary>
[Serializable]
internal sealed class ReinforcementRegistryFile
{
    public bool enabled;
    public float checkIntervalSeconds = 0.25f;
    public bool discoverAnchorsFromSpawnPoint;
    public string[] eventFoldersAllowed;
    public string eventSceneContains = string.Empty;
    public ReinforcementRegistryEncounterSpec[] encounters;
    public string eventFolder = string.Empty;
}

[Serializable]
internal sealed class ReinforcementRegistryEntry
{
    /// <summary>Unique anchor id from spawn line (e.g. <c>fm_reinf_north</c>).</summary>
    public string anchorId = string.Empty;

    /// <summary>Config pack folder under <c>EventCore/_shared/</c> (e.g. <c>reinforcement</c>).</summary>
    public string folder = string.Empty;

    public string sceneNameContains = string.Empty;
    internal bool useSpawnBindingAnchor;
    internal float spawnBindingAnchorX;
    internal float spawnBindingAnchorY;

    internal static ReinforcementRegistryEntry FromSpawnBinding(
        string anchorId,
        string eventFolder,
        string sceneHintsJoined,
        float ax,
        float ay)
    {
        return new ReinforcementRegistryEntry
        {
            anchorId = anchorId ?? string.Empty,
            folder = eventFolder,
            sceneNameContains = sceneHintsJoined ?? string.Empty,
            useSpawnBindingAnchor = true,
            spawnBindingAnchorX = ax,
            spawnBindingAnchorY = ay
        };
    }
}

/// <summary>Shared pack: <c>EventCore/_shared/&lt;folder&gt;/config.json</c>.</summary>
[Serializable]
internal sealed class ReinforcementConfigFile
{
    public float anchorX;
    public float anchorY;

    /// <summary>World distance from anchor: player must be inside for knockdown spawns and (when phrases are loaded) suspicion lines.</summary>
    public float triggerRadiusFromAnchor = 15f;

    /// <summary>Comma-separated registry keys (see <see cref="EnemyPrefabRegistry"/>), e.g. <c>TouzokuNormal,TouzokuAxe</c>.</summary>
    public string enemyTypesCsv = "TouzokuNormal,TouzokuAxe";

    /// <summary>Comma-separated horizontal distances (world units) from the player on spawn.</summary>
    public string horizontalSpawnDistancesCsv = "2,4,5,6";

    /// <summary>When true, spawns only on +X (right of player). When false, each unit picks random left/right.</summary>
    public bool spawnRightOnly;

    public int spawnCountMin = 1;
    public int spawnCountMax = 4;

    public string spawnFactionIdRaw = "bandits";

    /// <summary>Successful spawn waves per encounter this session (each knockdown trigger that spawns ≥1 unit). 0 = unlimited.</summary>
    public int maxKnockdownSpawns = 15;

    public float spawnDelaySeconds;

    /// <summary>Optional tiny vertical jitter so stacked spawns do not clip identically.</summary>
    public float verticalJitter = 0.15f;

    /// <summary>Load <c>phrases.json</c> from <c>EventCore/&lt;Lang&gt;/&lt;folder&gt;/</c> (e.g. <c>event_trap_gate</c>) instead of this pack's folder.</summary>
    public string phrasesFromEventFolder = string.Empty;

    /// <summary>When ≤ 0, uses <see cref="triggerRadiusFromAnchor"/> for suspicion thought radius.</summary>
    public float suspicionEnterRadius;

    public float thoughtVerticalOffsetPx = 75f;
    public float thoughtDurationSeconds = 5f;
    public float suspicionRepeatCooldownSeconds = 8f;
    public bool suspicionThoughtOnlyOnEnteringZone;
}

[Serializable]
internal sealed class ReinforcementPhrasesFile
{
    public string[] lines = new string[0];
}

internal sealed class ReinforcementLoadedEncounter
{
    internal readonly string AnchorId;
    internal readonly ReinforcementRegistryEntry Registry;
    internal readonly ReinforcementConfigFile Config;
    internal readonly string[] EnemyTypes;
    internal readonly int[] HorizontalDistances;
    internal readonly string[] PhraseLines;

    internal bool InsideSuspicionZone;
    internal float LastThoughtTime = -9999f;
    internal bool PrevPlayerKnockdown;
    internal float PrevDistanceToAnchor = float.MaxValue;
    internal float PendingSpawnAt = -1f;
    internal int SpawnSuccessCount;
    internal bool Depleted;

    internal ReinforcementLoadedEncounter(
        string anchorId,
        ReinforcementRegistryEntry registry,
        ReinforcementConfigFile config,
        string[] enemyTypes,
        int[] horizontalDistances,
        string[] phraseLines)
    {
        AnchorId = !string.IsNullOrEmpty(anchorId) ? anchorId : (registry != null ? registry.anchorId : string.Empty);
        Registry = registry;
        Config = config;
        EnemyTypes = enemyTypes;
        HorizontalDistances = horizontalDistances;
        PhraseLines = phraseLines ?? new string[0];
    }

    internal string LogLabel
    {
        get
        {
            string pack = Registry != null && !string.IsNullOrEmpty(Registry.folder) ? Registry.folder : "?";
            string id = !string.IsNullOrEmpty(AnchorId) ? AnchorId : pack;
            return id + " (pack=" + pack + ")";
        }
    }

    internal Vector2 Anchor => new Vector2(Config.anchorX, Config.anchorY);

    internal float TriggerR =>
        Config.triggerRadiusFromAnchor > 0.01f ? Config.triggerRadiusFromAnchor : 15f;

    internal float SuspicionEnterR
    {
        get
        {
            if (Config.suspicionEnterRadius > 0.01f)
                return Config.suspicionEnterRadius;
            return TriggerR;
        }
    }

    internal float ThoughtCooldown =>
        Config.suspicionRepeatCooldownSeconds > 0.01f ? Config.suspicionRepeatCooldownSeconds : 60f;
}
