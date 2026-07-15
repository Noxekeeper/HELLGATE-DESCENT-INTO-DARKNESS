using System;
using UnityEngine;

namespace NoREroMod.Systems.EventCore.EventTrap;

[Serializable]
internal sealed class EventTrapRegistryEncounterSpec
{
    /// <summary>Subfolder under EventCore language trees and <c>_shared/</c>, e.g. <c>event_trap_gate</c>.</summary>
    public string eventFolder = string.Empty;

    /// <summary>If non-empty, encounter runs only when scene name contains this substring (case-insensitive).</summary>
    public string eventSceneContains = string.Empty;
}

[Serializable]
internal sealed class EventTrapRegistryFile
{
    public bool enabled;
    public float checkIntervalSeconds = 0.25f;

    /// <summary>
    /// When true, scan <c>HellGateJson/HellGateSpawnPoint/HellGateSpawn_*.txt</c> for <c>EVENTTRAP,event_folder,x,y</c> lines
    /// and register one encounter per matching line (anchor from spawn file). Falls back to manual registry entries if nothing matches.
    /// </summary>
    public bool discoverAnchorsFromSpawnPoint;

    /// <summary>Optional whitelist of event folder names (e.g. <c>event_trap_gate</c>). When null/empty, every @marker with a matching <c>_shared</c> pack is loaded.</summary>
    public string[] eventFoldersAllowed;

    /// <summary>
    /// When non-empty and <see cref="discoverAnchorsFromSpawnPoint"/> is false or finds no lines, each element registers one EventTrap pack.
    /// </summary>
    public EventTrapRegistryEncounterSpec[] encounters;

    /// <summary>Subfolder under <c>HellGateJson/EventCore/&lt;Lang&gt;/</c> (e.g. <c>event_trap_gate</c>). Required when <see cref="encounters"/> is empty.</summary>
    public string eventFolder = string.Empty;

    /// <summary>If non-empty, encounter runs only when the resolved gameplay scene name contains this substring (case-insensitive).</summary>
    public string eventSceneContains = string.Empty;
}

[Serializable]
internal sealed class EventTrapRegistryEntry
{
    /// <summary>Unique anchor id from spawn line (e.g. <c>forest_trap_a</c>).</summary>
    public string anchorId = string.Empty;

    /// <summary>Subfolder under <c>HellGateJson/EventCore/&lt;Lang&gt;/</c>, e.g. <c>event_trap_gate</c>.</summary>
    public string folder = string.Empty;

    /// <summary>If non-empty, encounter runs only when <see cref="UnityEngine.SceneManagement.Scene.name"/> contains this substring (case-insensitive). Multiple hints: separate with <c>;</c> or <c>|</c>.</summary>
    public string sceneNameContains = string.Empty;

    /// <summary>When true, <see cref="EventTrapEncounterLoader"/> sets anchor from spawn discovery instead of JSON / spawn-key lookup.</summary>
    internal bool useSpawnBindingAnchor;

    internal float spawnBindingAnchorX;
    internal float spawnBindingAnchorY;

    internal static EventTrapRegistryEntry FromSpawnBinding(
        string anchorId,
        string eventFolder,
        string sceneHintsJoined,
        float ax,
        float ay)
    {
        return new EventTrapRegistryEntry
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

[Serializable]
internal sealed class EventTrapConfigFile
{
    /// <summary>Fallback world anchor when <see cref="anchorTrapKey"/> is empty or spawn lookup fails.</summary>
    public float anchorX;

    public float anchorY;

    /// <summary>File name under <c>BepInEx/plugins/HellGateJson/HellGateSpawnPoint/</c> (e.g. <c>HellGateSpawn_hidden Forest area.txt</c>). Used with <see cref="anchorTrapKey"/>.</summary>
    public string anchorHellGateSpawnFile = string.Empty;

    /// <summary>Template key from <c>TRAP,key,X,Y,Count</c> or <c>SPAWN,Trap,key,X,Y,Count</c> in the spawn file; when set, overrides <see cref="anchorX"/>/<see cref="anchorY"/> from that line.</summary>
    public string anchorTrapKey = string.Empty;

    public float suspicionEnterRadius;
    public float ambushZoneRadius;
    /// <summary>Pixels added on top of the bone screen position (canvas anchored Y); larger = higher on screen.</summary>
    public float thoughtVerticalOffsetPx = 75f;
    public float thoughtDurationSeconds = 5f;
    public float suspicionRepeatCooldownSeconds = 8f;

    /// <summary>
    /// If true, suspicion lines only when crossing into the suspicion radius. If false, lines can repeat every
    /// <see cref="suspicionRepeatCooldownSeconds"/> while the player stays inside the zone.
    /// </summary>
    public bool suspicionThoughtOnlyOnEnteringZone;
    public string spawnEnemyType = string.Empty;

    /// <summary>When set, ambush picks a random type (overrides <see cref="spawnEnemyType"/>).</summary>
    public string enemyTypesCsv = string.Empty;

    /// <summary>Comma-separated horizontal distances from player (used with <see cref="enemyTypesCsv"/>).</summary>
    public string horizontalSpawnDistancesCsv = string.Empty;

    public bool spawnRightOnly;

    public int spawnCountMin = 1;
    public int spawnCountMax = 1;

    public string spawnFactionIdRaw = string.Empty;
    public bool ambushOnce = true;

    /// <summary>
    /// Maximum successful ambush spawn waves for this encounter in the current load (each knockdown-triggered spawn that spawns ≥1 enemy counts as one).
    /// When reached, suspicion thoughts and ambush scheduling stop for this encounter until the next registry reload / scene load. 0 = unlimited.
    /// </summary>
    public int maxAmbushSpawns = 3;

    /// <summary>Horizontal distance from player for flank spawns when <see cref="flankAmbushPerSideMax"/> is 2 or more.</summary>
    public int flankDistanceMin;

    public int flankDistanceMax;

    /// <summary>Random count per side (inclusive). Both must be set (max ≥ 2) to enable flank packs.</summary>
    public int flankAmbushPerSideMin;

    public int flankAmbushPerSideMax;

    /// <summary>Seconds after knockdown edge before ambush spawn (uses player position at fire time). 0 = immediate.</summary>
    public float ambushSpawnDelaySeconds;

    /// <summary>Horizontal offset from player for left/right pair when <see cref="flankAmbushPerSideMax"/> is below 2.</summary>
    public float ambushSideOffset;

    /// <summary>Load phrases from <c>EventCore/&lt;Lang&gt;/&lt;folder&gt;/phrases.json</c> (e.g. <c>event_trap_gate</c>) when pack name differs from phrase files.</summary>
    public string phrasesFromEventFolder = string.Empty;
}

[Serializable]
internal sealed class EventTrapPhrasesFile
{
    public string[] lines = new string[0];
}

internal sealed class EventTrapLoadedEncounter
{
    internal readonly string AnchorId;
    internal readonly EventTrapRegistryEntry Registry;
    internal readonly EventTrapConfigFile Config;
    internal readonly string[] PhraseLines;
    internal readonly string PhrasesSourcePath;
    internal readonly string[] EnemyTypes;
    internal readonly int[] HorizontalDistances;

    internal bool InsideSuspicionZone;
    internal float LastThoughtTime = -9999f;
    internal bool PrevPlayerKnockdown;
    internal bool LifetimeAmbushSpawned;
    internal float PendingAmbushSpawnAt = -1f;

    /// <summary>Successful ambush waves (TrySpawnAmbush returned &gt; 0).</summary>
    internal int AmbushSpawnSuccessCount;

    /// <summary>No further thoughts or spawns for this encounter this session.</summary>
    internal bool AmbushDepleted;

    /// <summary>Distance to anchor on the previous tick (for ambush if knocked down and entering the zone).</summary>
    internal float PrevDistanceToAnchor = float.MaxValue;

    internal EventTrapLoadedEncounter(
        string anchorId,
        EventTrapRegistryEntry registry,
        EventTrapConfigFile config,
        string[] phraseLines,
        string phrasesSourcePath,
        string[] enemyTypes,
        int[] horizontalDistances)
    {
        AnchorId = !string.IsNullOrEmpty(anchorId) ? anchorId : (registry != null ? registry.anchorId : string.Empty);
        Registry = registry;
        Config = config;
        PhraseLines = phraseLines;
        PhrasesSourcePath = phrasesSourcePath;
        EnemyTypes = enemyTypes ?? new string[0];
        HorizontalDistances = horizontalDistances ?? new int[0];
    }

    internal bool UsesPlayerRelativeAmbushSpawn =>
        EnemyTypes != null && EnemyTypes.Length > 0 &&
        HorizontalDistances != null && HorizontalDistances.Length > 0;

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

    internal float SuspicionEnterR =>
        Config.suspicionEnterRadius > 0.01f ? Config.suspicionEnterRadius : 8f;

    internal float AmbushZoneR =>
        Config.ambushZoneRadius > 0.01f ? Config.ambushZoneRadius : 6f;

    internal float ThoughtCooldown =>
        Config.suspicionRepeatCooldownSeconds > 0.01f ? Config.suspicionRepeatCooldownSeconds : 60f;
}
