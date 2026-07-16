# HellGate Architecture

Architectural source of truth for **NoREroMod HellGate**, a BepInEx plugin
for *Night of Revenge*.

| | |
|---|---|
| Assembly | `NoR_HellGate.dll` |
| GUID | `NoREroMod_HellGate` |
| Version | `1.2.4` (`Core/PluginInfo.cs`) |
| Companion | `NoREroMod.dll` (HellGate fork) — required, loaded side by side |
| License | GPL-3.0 (`LICENSE`) |

This document covers layers, lifecycle, the patching model, subsystem
boundaries, data flow, and architectural constraints. Per-subsystem detail
lives in [`docs/modules/`](docs/modules/); procedures live in
[`docs/development/`](docs/development/).

---

## 1. Role

HellGate is a standalone BepInEx plugin that extends *Night of Revenge*
directly:

- it patches vanilla game types (`EnemyDate`, `playercon`, `UImng`,
  `Bigoni`, `suraimu`, …) with Harmony;
- it runs its own gameplay services: spawn pipeline, EventCore, factions,
  pregnancy, economy, QTE 3.0, rage, handoff, HellTraps, MindBroken, and the
  presentation stack;
- features are config- and JSON-driven so content can change without
  recompiling wherever possible.

**NoREroMod is a required companion, not the foundation.** Most HellGate code
has no NoREroMod dependency; the deliberate contact surface is small (shared
types, reflection into two patch classes, disablers for overlapping
features, and a config push). Where features overlap, HellGate takes
ownership and disables the NoREroMod path. Base enemy stat scaling stays in
NoREroMod. `RunNoREroModCompatibilityProbe()` verifies expected symbols at
startup. The full boundary is documented in
[`docs/development/COMPATIBILITY.md`](docs/development/COMPATIBILITY.md).

## 2. Stack

| Layer | Choice |
|-------|--------|
| Game | Unity 5.x managed assemblies (`NightofRevenge_Data/Managed/`) |
| Loader | BepInEx |
| Patching | Harmony (`HarmonyLib`) |
| Language | C# targeting .NET Framework 3.5 |
| Project | `NoREroMod_HellGate.csproj` → `NoR_HellGate.dll` |

Referenced assemblies: `Assembly-CSharp` (+ firstpass), `UnityEngine`,
`UnityEngine.UI`, `BepInEx`, `0Harmony`, `NoREroMod`, `ES2`, `Rewired_Core`.

## 3. Layered model

```text
┌──────────────────────────────────────────────────────────────┐
│  Night of Revenge (Unity, Assembly-CSharp)                   │
│  EnemyDate · playercon · UImng · Bigoni · game_fragmng · …   │
└───────────────▲──────────────────────────────▲───────────────┘
                │ Harmony patches              │ Harmony patches
┌───────────────┴───────────────┐  ┌───────────┴───────────────┐
│  NoR_HellGate.dll (this repo) │  │  NoREroMod.dll (companion)│
│   Core/     config · init     │  │   base scaffold, enemy    │
│   Patches/  game hooks        │◄─┤   stat scaling; HellGate  │
│   Systems/  runtime services  │  │   disables overlapping    │
│                               │  │   QTE/struggle paths      │
└───────────────┬───────────────┘  └───────────────────────────┘
                │ reads
┌───────────────▼──────────────────────────────────────────────┐
│  Data:   BepInEx/plugins/HellGateJson/   (JSON, spawn txt)   │
│  Assets: sources/HellGate_sources/       (binary, external)  │
└──────────────────────────────────────────────────────────────┘
```

**Design rules**

1. Feature behavior lives in `Systems/<Feature>/`.
2. Game-facing hooks stay thin, in `Patches/` or
   `Systems/<Feature>/Patches/`.
3. Feature gates and tuning go to BepInEx config; content and balance go to
   JSON/text data.
4. Per-frame player work hooks into `PlayerConUpdateDispatcher`, not new
   `playercon.Update` patches.
5. One failing patch registration must not abort other modules.

## 4. Repository layout

| Path | Role |
|------|------|
| `Core/Plugin.cs` | entry point: config binding, `Awake`, patch registration, subsystem init |
| `Core/PluginInfo.cs` | GUID / name / version |
| `Core/HellGateTypeResolver.cs` | safe type/member resolution helpers |
| `Systems/` | feature modules |
| `Patches/` | game-facing Harmony types |
| `HellGateAssets/BepInEx/plugins/HellGateJson/` | shipped data mirror |
| `docs/` | public developer documentation |
| `References/` | local .NET 3.5 framework path for MSBuild |
| `Properties/` | assembly metadata |

The compile list in `NoREroMod_HellGate.csproj` is explicit; files not listed
there do not build. `Systems/H_Scenes/` is currently an empty placeholder.
Binary asset packs are excluded from git and distributed externally
(`sources/HellGate_sources/` at the game root). Build and deploy procedure:
[`docs/development/BUILDING.md`](docs/development/BUILDING.md).

## 5. Plugin lifecycle

Entry: `[BepInPlugin]` + `[BepInProcess("NightofRevenge.exe")]`.

### 5.1 `Awake()` order

Order matters; later stages depend on earlier ones:

1. `SetUpConfigs()` — binds `NoREroMod_HellGate.cfg`.
2. Early module configs — `PregnancyConfig.Initialize`,
   `SpawnTemplateCatalog.Initialize`.
3. HellTraps preload — trap template registration and death-asset preload
   (must precede spawn execution).
4. `SetUpPatches()` — explicit Harmony registration; the authoritative list
   is in `Plugin.cs`.
5. EventCore bootstrap; then EventTrap and Reinforcement bootstraps, each
   behind its own flag.
6. Frameworks and UX — struggle indicators, dialogue framework, QTE
   reactions, H-scene camera, start zoom.
7. MindBroken, Rage, and UI systems (per-flag).
8. Diagnostics (JSON-gated, off by default).
9. Economy initialization and, when enabled, gold systems.
10. Audio and rage core; `SceneManager.sceneLoaded` handlers (cache resets,
    EventCore session reload).

### 5.2 Per-frame hub

`Patches/Player/PlayerConUpdateDispatcher` is the single postfix on
`playercon.Update`. It currently drives the H-scene start zoom check,
QTE/struggle bridges, faction H-scene reputation and Mercy de-escalation,
and gold H-scene earnings.

## 6. Harmony model

- Patch types are registered explicitly (`PatchType` / `PatchTypeWithLog`)
  rather than by a blind assembly-wide `PatchAll`, so a single incompatible
  target fails in isolation and is named in the log.
- A few modules use type-scoped `PatchAll` or custom `Apply` methods for
  nested classes; they are registered from the same `SetUpPatches()`
  sequence.
- The NoREroMod compatibility probe runs at startup and logs missing
  symbols.

Removed approaches that must not return (incident history in
[`COMPATIBILITY.md`](docs/development/COMPATIBILITY.md)): the scene-transition
field rewrite, per-map C# spawn files, the custom settings-menu UI stack, and
the `BigoniBrotherERO` component path.

## 7. Subsystem map

Detailed references live in [`docs/modules/`](docs/modules/).

| Subsystem | Code | Reference |
|-----------|------|-----------|
| Spawn pipeline | `Systems/Spawn/` | [SPAWN.md](docs/modules/SPAWN.md) |
| EventCore + EventTrap + Reinforcement | `Systems/EventCore/` | [EVENT_CORE.md](docs/modules/EVENT_CORE.md) |
| Factions, reputation, combat AI | `Systems/CombatAi/` | [FACTIONS_AND_COMBAT_AI.md](docs/modules/FACTIONS_AND_COMBAT_AI.md) |
| Pregnancy | `Systems/Pregnancy/` | [PREGNANCY.md](docs/modules/PREGNANCY.md) |
| Economy + rewards | `Systems/Economy/`, `Systems/Rewards/` | [ECONOMY_AND_REWARDS.md](docs/modules/ECONOMY_AND_REWARDS.md) |
| QTE, struggle, weapon mechanics | `Systems/Gameplay/` | [QTE_STRUGGLE_AND_GAMEPLAY.md](docs/modules/QTE_STRUGGLE_AND_GAMEPLAY.md) |
| Rage | `Systems/Rage/` | [RAGE.md](docs/modules/RAGE.md) |
| Grab + handoff | `Systems/GrabSystem/`, `Systems/Handoff/` | [GRAB_AND_HANDOFF.md](docs/modules/GRAB_AND_HANDOFF.md) |
| MindBroken | `Patches/UI/MindBroken/` | [MIND_BROKEN.md](docs/modules/MIND_BROKEN.md) |
| HellTraps | `Patches/HellTraps/` | [HELL_TRAPS.md](docs/modules/HELL_TRAPS.md) |
| Enemy integration + custom packs | `Patches/Enemy/` | [CUSTOM_ENEMIES.md](docs/modules/CUSTOM_ENEMIES.md) |
| Dialogue, camera, UI, audio, effects | `Systems/Dialogue/`, `Systems/Camera/`, `Systems/UI/`, `Systems/Audio/`, `Systems/Effects/`, `Systems/BadEndPlayer/` | [PRESENTATION.md](docs/modules/PRESENTATION.md) |

Cross-cutting infrastructure:

- `Systems/Cache/` — unified player/camera/game-controller interval caches
  replacing hot-path `Find*Tag` / `GetComponent`; reset on scene change.
- `Patches/Player/` — player state guards and recovery (H-scene escape
  cleanup, vanilla flow guards, birth recovery). These encode soft-lock
  fixes; see the invariants in `COMPATIBILITY.md`.
- `Patches/Performance/` — vanilla hot-path rewrites onto the caches.
- `Systems/Compatibility/` — NoREroMod config push.
- `Systems/Diagnostics/` — opt-in, JSON-gated investigation kits (Tentacle,
  TrapBody, Kinoko); off by default, excluded from release configuration.

## 8. Configuration and data

Two-tier model:

- **BepInEx config** (`NoREroMod_HellGate.cfg`, generated by
  `SetUpConfigs()`) holds feature gates and developer/player tuning. Every
  feature is fully disableable. Section names follow the owning module (the
  per-module docs list their sections).
- **JSON/text data** under `HellGateJson/` holds content and balance:
  spawn packs, faction definitions, combat AI, EventCore content, economy,
  drop tables, localized dialogue. Some loaders hot-reload (factions, spawn
  packs via the analyzer).

Localization: ten languages with loader-specific EN fallback; EventCore
string pools fail closed. Per-slot runtime saves (reputation, gold,
rage/MindBroken, pregnancy) are written through the game's save/load hooks
only. Roots, formats, and save files:
[`docs/development/DATA_FORMATS.md`](docs/development/DATA_FORMATS.md).

## 9. Architectural constraints

- H-scene escape flows through the existing cleanup patch set; no parallel
  escape paths.
- `Time.timeScale` changes are restored via the escape/cleanup path; grab
  slow-mo defers to the start-zoom effect when enabled.
- Persistence only via save/load hooks; per-slot files; gameplay code never
  flushes to disk.
- Boss classification is centralized in `FactionBossDetection`.
- Fonts route through `HellGateFontProvider`; custom HUDs obey
  `HudVisibilityGate` (alpha, not `SetActive`).
- Runtime assets resolve relative to the game root; no absolute paths.
- C# comments and public docs are professional English; player-facing
  strings may be localized.

The full hazard list and the NoREroMod boundary:
[`docs/development/COMPATIBILITY.md`](docs/development/COMPATIBILITY.md).

## 10. Maintenance

Update this file when subsystem boundaries, `Awake()` order, the patching
model, data roots, or persistence change. Update the matching document in
`docs/modules/` in the same change that alters the subsystem. Procedures:
[`ADDING_FEATURES.md`](docs/development/ADDING_FEATURES.md),
[`ADDING_ENEMIES.md`](docs/development/ADDING_ENEMIES.md).
