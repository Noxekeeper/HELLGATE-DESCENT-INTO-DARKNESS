# EventCore

Modal world encounters: spawned NPCs that open a dialogue canvas with choices,
plus two non-modal encounter subsystems (event traps and reinforcements).

Code: `Systems/EventCore/` · Data: `HellGateJson/EventCore/` · Config: `[EventCore]`

Placement of modal NPCs and EventTrap / Reinforcement anchors is owned by
the spawn pipeline — [SPAWN.md](SPAWN.md). This document is runtime,
content, and the spawn-line contract that attaches a host.

## Pipeline

```text
Core      bootstrap, runtime session, paths, pause handling
Content   JSON definition registry + manual language parsing
Host      EventCoreHost attached to a spawned NPC
Handlers  flow logic (broker gate, faction-social sex_paid, choice apply)
UI        modal canvas, portrait pair, frame art, input
```

- `EventCoreBootstrap.Install` runs during plugin `Awake`.
- `SpawnConfigExecutor` attaches `EventCoreHost` to spawned NPCs from spawn
  line metadata (`|ec_event=`, `|ec=`, `|ec_pool=`).
- The session reloads on `SceneManager.sceneLoaded`.

## Spawn lines

Enemy pack lines carry EventCore as **key-field pipes** (same field as
`|faction=` / `|elite=1`). Parsed by `SpawnConfigExecutor.ParseEnemySpec`.

| Token | Meaning |
|-------|---------|
| `\|ec_event=<id>` | attach this event (`\|ec=` is an alias) |
| `\|ec_pool=id_a,id_b` | uniform random id per spawn; overrides `ec_event` when present |
| `\|ec_chance=<0–1>` | probability the host attaches (`\|ec_p=` is an alias). NPC still spawns. |

Omit `ec_chance` (or write `1`) to **always** attach. The token is applied
only when it is present and `< 0.999`. F11 Place with Event p= **1** does
not write `|ec_chance=`.

Empty `|faction=` on an EventCore spawn becomes **`eventcore_encounter`**
(`FactionIds.EventCoreEncounter` = 50). Aliases: `eventcore`, `encounter`.

Example (always attach):

```text
X,Y,TouzokuNormal|faction=eventcore_encounter|ec_event=eventcore_broker_gate,1
```

Example (30% attach; otherwise a plain Touzoku):

```text
X,Y,TouzokuNormal|faction=eventcore_encounter|ec_event=eventcore_broker_gate|ec_chance=0.3,1
```

`RANDOM,chance,…` is the **NPC spawn** roll. It is independent of Event p=.

## F11 EventCore catalog

Modal NPCs are a **separate catalog** in Spawn System Editor V2.0, not an
Enemies pending option. Placement rules live in [SPAWN.md](SPAWN.md).

| Input | Role |
|-------|------|
| **C** | EventCore catalog (`Ctrl+C` is still copy) |
| Place | writes the enemy line above; preview always attaches `EventCoreHost` |
| RMB reload | applies `|ec_chance=` (F11 preview ignores that roll) |
| LMB on the NPC | Edit: event id, Event p=, faction, elite, flip, random, XY |

The list is `EventCoreDefinitionRegistry.GetAuthoringEventIds()` — every id
loaded from `eventcore_manifest.json`. Broker / FSP ids whose names contain
`broker_gate` or `fsp_bandits` lock the prefab to **`TouzokuNormal`**. Other
ids also Place as `TouzokuNormal` unless a bound key is added in
`TryGetBoundEnemyKey`.

Shipped manifest ids:

- `eventcore_broker_gate`
- `eventcore_fsp_bandits_sex_paid`

Favorites store kind `eventcore` plus `ecChance`. Old enemy favorites that
already carry an EventCore field open this catalog.

Region / clipboard paste keeps `|ec_event=` (`ExtractEventCoreFromSourceLine`
on preview spawn). Cutting the key at the first `|` would drop the host.

## Runtime rules

- Approach trigger: **3.5** world units (`EventCoreHost.TriggerDistance`).
- While F11 recording is on (`SpawnPointAnalyzer.IsRecordingModeActive`),
  the host does not start a session and `EventCoreRuntime.TryBeginSession`
  returns false — the modal must not cover the editor.
- Modal NPCs are entered through a **consent grab**, not knockdown or
  struggle.
- Ambush/betrayal branches switch the NPC to session hostility.
- Encounter shells run under the passive faction `eventcore_encounter`: no
  faction emblem or HUD entry until the encounter resolves.
- Combat threat dialogue is suppressed while a modal host is active and
  non-hostile (see `PRESENTATION.md`).
- `EventCoreVanillaUiSuppressor` hides conflicting vanilla UI during a modal.

## Content and localization

`HellGateJson/EventCore/` contains:

- the manifest and event definitions;
- per-language string packs in ten languages
  (`Ru En Cn Jp Kr De Pt Br Es Fr`);
- `event_trap_registry.json` and `reinforcement_registry.json`.

Full file-by-file schemas:
[`docs/development/EVENTCORE_DATA.md`](../development/EVENTCORE_DATA.md).

String pools **fail closed**: a missing key is an error, not a silent
fallback to another language. This is stricter than most other HellGate
loaders (see `DATA_FORMATS.md`).

Portraits: `EventCorePortraitPair` renders side portraits from PNG clip
folders (`EventCoreBrokerPortraitMap`, `EventCoreFspPortraitMap`) under the
external asset tree.

## EventTrap and Reinforcement

Both bootstraps install unconditionally after EventCore. Their independent
enable flags gate runtime/reload behavior inside the drivers:

- **EventTrap** (`Systems/EventCore/EventTrap/`) — knockout-zone encounters
  defined by `etrap_*` packs; anchors placed via `EVENTTRAP` spawn lines
  (F11 catalog **M**).
- **Reinforcement** (`Systems/EventCore/Reinforcement/`) — delayed hostile
  wave encounters; anchors placed via `REINFORCEMENT` spawn lines
  (same EventTrap catalog).

Each has its own data loader, driver, discovery, and registry JSON.
