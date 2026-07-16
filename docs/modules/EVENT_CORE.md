# EventCore

Modal world encounters: spawned NPCs that open a dialogue canvas with choices,
plus two non-modal encounter subsystems (event traps and reinforcements).

Code: `Systems/EventCore/` · Data: `HellGateJson/EventCore/` · Config: `[EventCore]`

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

## Runtime rules

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
  defined by `etrap_*` packs; anchors placed via `EVENTTRAP` spawn lines.
- **Reinforcement** (`Systems/EventCore/Reinforcement/`) — delayed hostile
  wave encounters; anchors placed via `REINFORCEMENT` spawn lines.

Each has its own data loader, driver, discovery, and registry JSON.
