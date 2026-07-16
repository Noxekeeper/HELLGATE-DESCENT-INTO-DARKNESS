# EventCore JSON Reference

Schemas for all content files under `BepInEx/plugins/HellGateJson/EventCore/`.
System behavior is described in [EVENT_CORE.md](../modules/EVENT_CORE.md);
this document covers only the on-disk contracts.

All files are parsed with Unity 5's `JsonUtility`, which imposes two
authoring constraints:

- schemas are **flat** — nested wrapper objects are not reliably filled, which
  is why steps live in separate files rather than deep inline structures;
- a missing numeric field reads as `0`, and several fields treat `0` as
  "use the runtime default" (noted per field below).

Malformed files are logged and skipped; they must never crash a scene load.

## Directory layout

```
EventCore/
├── eventcore_manifest.json        # which event definitions to load
├── eventcore_<event>.json         # one definition per modal event
├── event_trap_registry.json       # EventTrap subsystem root
├── reinforcement_registry.json    # Reinforcement subsystem root
├── strings_default.json           # legacy shared string keys
├── _shared/<pack>/config.json     # language-independent encounter packs
└── <Lang>/                        # En, Ru, Jp, Cn, Kr, Fr, De, Pt, Br, Es
    ├── Stranger/eventcore_lang.json     # language pack (entries + linePools)
    ├── Stranger/<event>_s<N>.json       # step files
    ├── FactionSocial/<faction>/<kind>/  # FSP step files + *_lang.json
    └── <event folder>/phrases.json      # EventTrap/Reinforcement lines
```

Language selection: `[General] HellGateLanguage` in the cfg maps to a folder
code (`EventCoreLanguage.ResolveFolderCode()`, default `Ru`). Step files
resolve against the active language first, then fall back through the other
language folders, so a missing translation degrades to another language
instead of breaking the event.

## Manifest — `eventcore_manifest.json`

```json
{ "eventFiles": ["eventcore_broker_gate.json"] }
```

Each listed file is loaded as an event definition. Files not listed are
ignored.

## Event definition — `eventcore_<event>.json`

Loaded into `EventCoreEventDefinitionFile`
(`Systems/EventCore/Content/EventCoreJsonModels.cs`).

| Field | Type | Meaning |
|-------|------|---------|
| `id` | string | unique event id |
| `handlerId` | string | C# handler that owns outcome logic (`broker_toll`, `faction_social`; see `EventCoreHandlerIds`) |
| `tollGold` | int | `broker_toll`: gold debited by `pay_pass`; `0` = handler default |
| `negotiateTakeGoldBranchWeight` | int | weight of the negotiation branch that also takes carried gold; `0` = default 70 |
| `negotiateBodyOnlyBranchWeight` | int | weight of the body-payment branch (no gold debit); `0` = default 30 |
| `revealFactionPool` | string[] | combat factions rolled once per host when the encounter escalates |
| `peacefulFactionId` | string | faction kept after a peaceful/body resolution; empty = the EventCore encounter shell |
| `fspFactionKey` | string | FactionSocial only: faction key for reputation/gold (e.g. `bandits`) |
| `fspKind` | string | FactionSocial only: scenario kind (e.g. `sex_paid`) |
| `steps` | object[] | inline steps — **avoid**; `JsonUtility` often leaves nested arrays empty |
| `stepFiles` | string[] | step file paths relative to the language folder (preferred) |
| `ambushes` | object[] | named reinforcement packs addressable by handler logic |

Ambush pack shape:

```json
{
  "ambushId": "broker_refusal_ambush",
  "slots": [
    { "enemyType": "TouzokuNormal", "factionId": "", "eventId": "",
      "offsetX": -8.0, "offsetY": 0.0, "count": 4 }
  ]
}
```

Offsets are relative to the active EventCore host. `enemyType` is a spawn
registry key (`EnemyPrefabRegistry`).

## Step file — one `EventCoreStepDefinition` per file

```json
{
  "stepId": "broker_gate",
  "stepKind": "choice",
  "speakerLabel": "Stranger",
  "npcLinePoolId": "broker_open_a",
  "npcLine": "",
  "choiceLabels": [],
  "choicePoolIds": [],
  "choiceOutcomeIds": [],
  "choiceJumpStepIds": []
}
```

| Field | Meaning |
|-------|---------|
| `stepId` | unique within the event; jump targets refer to it |
| `stepKind` | `choice` (player picks an option) or `continue` (click-through) |
| `npcLine` | literal body text (lowest priority) |
| `npcLineKey` | key into the string registry; wins over `npcLine` when the key exists |
| `npcLinePoolId` | random line from a language-pack pool; wins over both |
| `speakerLabel` | optional name shown above the body text |
| `choiceLabels` | up to 5 button labels |
| `choicePoolIds` | per-slot pool ids — random label per slot (parallel to outcomes) |
| `choiceOutcomeIds` | parallel semantic ids consumed by the handler (`pay_pass`, `refuse_threat`, …) |
| `choiceJumpStepIds` | parallel jump targets; empty/unknown = next step is linear |
| `continueNextStepId` | `continue` steps: jump target instead of linear next |
| `continueOutcomeId` | `continue` steps: outcome applied on leaving the step |

The choice arrays are **parallel**: index N of every array describes button N.
Keep their lengths equal.

## Language pack — `<Lang>/Stranger/eventcore_lang.json`

```json
{
  "entries":  [ { "key": "eventcore_ui_continue", "text": "Continue (1 / Enter)" } ],
  "linePools": [ { "poolId": "broker_open_a", "lines": ["...", "..."] } ]
}
```

- `entries` — key → text lookups used by `npcLineKey` and UI strings;
- `linePools` — pools used by `npcLinePoolId` / `choicePoolIds`; one line is
  rolled per display.

FactionSocial scenarios ship their own `*_lang.json` next to their step files;
these are merged into the same registry. `strings_default.json` in the root
holds legacy shared keys and is merged last, never overriding language
values. Missing pack file is a logged error (strings fall back to raw keys).

## EventTrap — `event_trap_registry.json` + `_shared/<pack>/config.json`

Non-modal ambush encounters (suspicion thoughts, knockdown-triggered spawns).
Root registry (`EventTrapRegistryFile`):

| Field | Meaning |
|-------|---------|
| `enabled` | subsystem gate (bootstrap always installs; this gates runtime) |
| `checkIntervalSeconds` | driver poll interval (default `0.25`) |
| `discoverAnchorsFromSpawnPoint` | scan spawn packs for `EVENTTRAP` anchor lines and register one encounter per line |
| `eventFoldersAllowed` | whitelist of pack folders; empty = every discovered pack |
| `encounters[]` | manual entries (`eventFolder`, `eventSceneContains`) when discovery is off or finds nothing |
| `eventFolder` / `eventSceneContains` | single-encounter legacy form |

Anchor lines in `HellGateSpawn_*.txt` accept two forms (`#` starts a
comment; the spawn file must be listed in `HellGateSpawnSceneHints`):

```
EVENTTRAP,<packFolder>,<x>,<y>
EVENTTRAP,<anchorId>,<packFolder>,<x>,<y>
```

The 4-part form uses the pack folder as the anchor id, so it allows only one
anchor per pack per scene; use the 5-part form (unique `anchorId`) to place
the same pack several times.

Each pack lives in `_shared/<folder>/config.json` (`EventTrapConfigFile`) —
language-independent tuning:

| Group | Fields |
|-------|--------|
| Anchor | `anchorX/Y` (fallback), `anchorHellGateSpawnFile` + `anchorTrapKey` (bind to a `TRAP` line in a spawn pack) |
| Zones | `suspicionEnterRadius`, `ambushZoneRadius` |
| Thoughts | `thoughtVerticalOffsetPx`, `thoughtDurationSeconds`, `suspicionRepeatCooldownSeconds`, `suspicionThoughtOnlyOnEnteringZone`, `phrasesFromEventFolder` |
| Spawning | `spawnEnemyType` or `enemyTypesCsv` (random pick), `horizontalSpawnDistancesCsv`, `spawnRightOnly`, `spawnCountMin/Max`, `spawnFactionIdRaw` |
| Limits | `ambushOnce`, `maxAmbushSpawns` (0 = unlimited), `ambushSpawnDelaySeconds`, `ambushSideOffset`, flank fields (`flankDistanceMin/Max`, `flankAmbushPerSideMin/Max`) |

Localized suspicion lines come from `<Lang>/<folder>/phrases.json`:

```json
{ "lines": ["A thought line...", "Another..."] }
```

`phrasesFromEventFolder` lets several packs share one phrase folder (all the
`etrap_*` packs reuse `event_trap_gate`).

## Reinforcement — `reinforcement_registry.json` + `_shared/<pack>/config.json`

Same registry shape and discovery model as EventTrap — anchor lines use the
`REINFORCEMENT` command with the same 4/5-part forms — but tuned for
boss-area knockdown reinforcements
(`ReinforcementConfigFile`): `triggerRadiusFromAnchor` (default 15),
`enemyTypesCsv`, `horizontalSpawnDistancesCsv`, `spawnRightOnly`,
`spawnCountMin/Max`, `spawnFactionIdRaw`, `maxKnockdownSpawns`
(default 15, 0 = unlimited), `spawnDelaySeconds`, `verticalJitter`, and the
same suspicion-thought fields as EventTrap.

## Authoring rules

- UTF-8; a leading BOM is tolerated by the loaders.
- Add new events by creating the definition + step files and listing the
  definition in `eventcore_manifest.json`; no C# change is needed unless a
  new `handlerId` is required.
- Provide step files and phrase files for every language folder you ship;
  fallback keeps events working but mixes languages.
- After editing content, reload the scene and check the BepInEx log for
  `[EventCore]` parse warnings (see
  [TESTING.md](TESTING.md) — module spot checks).
