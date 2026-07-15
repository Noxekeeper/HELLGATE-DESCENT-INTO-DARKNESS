# HellGate Architecture

Architecture reference for **NoREroMod HellGate** — a BepInEx overlay for *Night of Revenge*.

| | |
|---|---|
| **Assembly** | `NoR_HellGate.dll` |
| **GUID** | `NoREroMod_HellGate` |
| **Version** | **1.2.4** (`Core/PluginInfo.cs`) |
| **Companion** | `NoREroMod.dll` (Rebalance / HellGate fork scaffold) |
| **Aligned** | 2026-07-16 |

This document describes **live structure**: layers, module map, data roots, and extension rules. Behavior details live in code and local maintainer notes (`docs/` — private; not shipped in the public git tree).

---

## 1. Role

HellGate is a **modular overhaul** on top of NoREroMod:

- Expands combat, H-scene UX, spawn content, factions, world events, economy, pregnancy, and enemy pass/handoff chains.
- Integrates via **Harmony** into Unity game types and NoREroMod hooks (`EnemyDate`, `playercon`, `UImng`, …).
- Keeps features **config- and JSON-driven** so content can change without recompiling when possible.

HellGate does **not** replace NoREroMod; both DLLs must load.

---

## 2. Stack

| Layer | Choice |
|--------|--------|
| Game | Unity 5.x managed assemblies (`NightofRevenge_Data/Managed/`) |
| Loader | BepInEx |
| Patching | Harmony (`HarmonyLib`) |
| Language | C# → **.NET Framework 3.5** |
| Project | `NoREroMod_HellGate.csproj` → `NoR_HellGate.dll` |

---

## 3. Layered model

```text
┌─────────────────────────────────────────────────────────────┐
│  BepInEx / Night of Revenge                                 │
├─────────────────────────────────────────────────────────────┤
│  NoREroMod.dll          shared scaffold & base hooks        │
├─────────────────────────────────────────────────────────────┤
│  NoR_HellGate.dll                                           │
│    Core/Plugin          config · Harmony · init order       │
│    Patches/             game-facing hooks                   │
│    Systems/             runtime services & colocated patches│
│    HellGateJson/        data (at runtime under BepInEx)     │
│    sources/…            binary assets (external / local)    │
└─────────────────────────────────────────────────────────────┘
```

**Design rules**

1. New gameplay logic → `Systems/<Feature>/`.
2. Thin game hooks → `Patches/` or `Systems/<Feature>/Patches/`.
3. Tunables → `SetUpConfigs()` and/or JSON under `HellGateJson/`.
4. Prefer **`PlayerConUpdateDispatcher`** over new `playercon.Update` patches.
5. One failing `PatchAll` must not abort others (`PatchType` / `PatchTypeWithLog`).

---

## 4. Repository layout (public)

Paths relative to the repo root (`NoREroMod_HellGate.csproj`).

| Path | Role |
|------|------|
| `Core/Plugin.cs` | `BaseUnityPlugin`: config, `Awake`, Harmony registration, subsystem init |
| `Core/PluginInfo.cs` | GUID / name / **version** |
| `Core/HellGateTypeResolver.cs` | Safe type/member resolve helpers |
| `Systems/` | Feature modules (~315 `.cs`) |
| `Patches/` | Game-facing Harmony types (~145 `.cs`) |
| `Properties/` | Assembly metadata |
| `References/` | Local TF 3.5 framework path for MSBuild |
| `HellGateAssets/BepInEx/plugins/HellGateJson/` | **Shipped data mirror** (JSON, spawn txt, EventCore packs) |
| `ARCHITECTURE.md` | This document |
| `.gitignore` | Excludes build junk, private trees, heavy sources |

**Not in public git** (kept locally / external hosting):

| Path | Policy |
|------|--------|
| `docs/` | Maintainer docs (often RU); push later as a separate docs package if needed |
| `dev/` | Local notes, `compili.bat`, changelogs, Manifesto generators |
| `HellGateAssets/sources/` | Heavy WAV/PNG packs — **MEGA / Releases**, not git |

---

## 5. Build & deploy

1. **Game root (`NorGameRoot`)** — two levels above the project directory (`…/NightofRevenge107`).
2. Build: `dotnet build -c Release` or local `dev/compili.bat` (clean → build → copy).
3. Output: `bin/Release/NoR_HellGate.dll` → copy to `BepInEx/plugins/` next to `NoREroMod.dll`.
4. Runtime data: `BepInEx/plugins/HellGateJson/` (synced from `HellGateAssets/…/HellGateJson/` for releases).
5. Binary assets: `sources/HellGate_sources/` relative to the game install (or mirrored under local `HellGateAssets/sources/`).

Do not commit `bin/`, `obj/`, root `BepInEx/`, or logs.

---

## 6. Plugin lifecycle

### 6.1 Entry

- `[BepInPlugin(PluginInfo.PLUGIN_GUID, …)]`
- `[BepInProcess("NightofRevenge.exe")]`

### 6.2 `Awake()` (order matters)

1. **`SetUpConfigs()`** — `NoREroMod_HellGate.cfg` (combat, enemies, MindBroken, Rage, Grab, EventCore, QTE, Portrait, fonts, traps, …).
2. **`SetUpPatches()`** — explicit Harmony registration (authoritative list in `Plugin.cs`).
3. **Subsystem init** (representative):
   - Spawn catalogs / template cache
   - **`EventCoreBootstrap.Install`** (+ EventTrap / Reinforcement when enabled)
   - Dialogue / QTE frameworks, struggle indicators
   - H-scene camera + **`HSceneStartZoomEffect`**
   - MindBroken / Rage UI & persistence
   - **`PortraitModSystem`**, **`HellGateFontProvider`**
   - Optional diagnostics (Tentacle / TrapBody / Kinoko — JSON toggles)
   - **`EconomicConfig`** → wallet / HUD / drop hooks when enabled
   - **`PregnancyConfig`** → patches + HUD when enabled
   - Audio / Rage / Attack sounds
4. **`SceneManager.sceneLoaded`** — cache resets; EventCore session reload.

### 6.3 Per-frame hub

**`Patches/Player/PlayerConUpdateDispatcher`** — postfix on `playercon.Update`:

- H-scene start zoom check
- QTE / struggle bridges
- Faction H-scene reputation + Mercy de-escalation
- Gold H-scene earnings (economy on)

---

## 7. Harmony model

- Prefer **named types** registered via `PatchType` / `PatchTypeWithLog`, not a blind `PatchAll(assembly)`.
- Special registrations: Kakash grab, Dorei combat AI, HeckGate/biscord `PatchAll` on module type, Bigoni GameOver bypass nested apply.
- **`RunNoREroModCompatibilityProbe()`** logs missing NoREroMod symbols at startup.

### Do not reintroduce

| Removed | Why |
|---------|-----|
| `SpawnSceneTransitionFix` | Overwrote `_re_Scenename`; broke additive EV scenes |
| Per-map `HellGateSpawn_*.cs` + `UnifiedSpawnManager` | Replaced by JSON/txt + location refresh pipeline |
| Custom `GameSettingsMenu` stack | Abandoned; settings live in cfg / JSON |
| `BigoniBrotherERO` component path | Live path patches **`StartBigoniERO`** via identity |

---

## 8. Module map

### 8.1 Systems (`Systems/`)

| Module | Responsibility |
|--------|----------------|
| **Rage** | Tiered rage, combo UI, wings/particles, slow-mo, persistence, active immunity |
| **GrabSystem** | Grab-via-attack chance/context; UI label on Rage canvas |
| **Dialogue** | H-scene lines, threats, QTE reactions, spectator / biscord branches |
| **Audio** | Attack / threat / death WAV routing |
| **Spawn** | Zone packs, prefab registry, decor/traps/hostages, scene refresh after load |
| **CombatAi** | Distance/damage AI JSON + **Factions** (combat, rep, Mercy) |
| **EventCore** | Modal broker/FSP, host NPC, EventTrap, Reinforcement |
| **Economy** | Gold wallet, drops, HUD, lost pile, save hooks |
| **Pregnancy** | Womb meter, trimesters, shelter attacks, birth guards, offspring hooks |
| **Handoff** | Cross-enemy handoff counters / delayed transfer |
| **Gameplay** | **QTE 3.0**, struggle visuals/disablers, AirGuard, VengeanceStrike, weapon anim patches |
| **Camera / HSceneEffects** | H-camera control, cum display, start zoom |
| **Effects / BadEndPlayer** | Black H background, bad-end playback |
| **UI** | Loading/splash, fonts, title backdrop, PortraitMod, HUD gates |
| **Cache** | Player / camera / GameController caches |
| **Rewards** | Drop tables (e.g. biscord) |
| **Diagnostics** | Tentacle / TrapBody / Kinoko — opt-in JSON |
| **Compatibility** | NoREroMod scaffold / config push helpers |

### 8.2 Patches (`Patches/`)

| Area | Responsibility |
|------|----------------|
| **Enemy/** | Pass/handoff per enemy; custom bosses (Mafia, BossTouzoku, BigoniBrother, HeckGate slime, Rick/Butcher/Wolf/HellishTouzoku, …) |
| **Player/** | Escape cleanup, knockdown recovery, altar/cutscene guards, pregnancy birth, dispatcher |
| **PlayerRespawn/** | Vengeance / soul offset presentation |
| **UI/MindBroken/** | Corruption, recovery, bad end, captions |
| **HellTraps/** | Lethal / hell trap content hooks |
| **Trap/** | Trap H-scene mosaic / escape fixes |
| **Spawn/** | F11 spawn analyzer / recorder |
| **Performance/** | Hot-path Find/GetComponent reductions |
| **Effects/** | Pregnancy clip triggers, related FX |

### 8.3 Signature subsystems (detail)

#### Spawn

- **Refresh owner:** `HellGateLocationSpawnRefresh` + `HellGateSpawnSceneHints`.
- **Executor:** `SpawnConfigExecutor` (fixed / RANDOM / pools / gold / TRAP / DECOR / EVENTTRAP / REINFORCEMENT / hostage).
- **Primary refresh:** `SceneLoadSpawnRefreshPatch` after `LoadSceneAndWait`.
- **Doors / altar:** `SceneMoveTransitionSpawnPatch`, `SpawnRespawnAfterAltarPatch`.
- **Metadata:** `|faction=`, `|ec_event=` / `|ec=` / `|ec_pool=`.
- **Data:** `HellGateJson/HellGateSpawnPoint/HellGateSpawn_*.txt`.

#### Factions

- Config: `HellGateJson/CombatAi/Factions.json` (hot reload).
- Inter-faction combat commit, activation radius, reputation slots, Mercy window.
- Passive encounter faction `eventcore_encounter` for EventCore shells.

#### EventCore

- Bootstrap → runtime → host → handlers (broker gate, FSP sex_paid) → UI.
- Data: `HellGateJson/EventCore/` (manifest, 10 languages, trap/reinforcement registries).
- Modal NPCs: consent grab (not knockdown); ambush uses session hostility.

#### QTE / Struggle

- HellGate **QTE 3.0** (`Systems/Gameplay/QTESystem.cs`); NoREroMod legacy QTE/struggle disabled via disablers.
- Struggle visual indicators + cfg difficulty / MindBroken hooks.

#### BigoniBrother

- Vanilla **`Bigoni` + `StartBigoniERO`**, tagged via `BigoniBrotherIdentity`.
- Live: `BigoniBrotherPatch`, `BigoniBrotherPassLogic`, `BigoniBrotherGameOverBypass`.

#### Pregnancy

- Gated by `PregnancyConfig.Enable`.
- Womb meter HUD, nakadashi tracking, trimester / bloodline, shelter-attack waves, birth recovery guards.

---

## 9. Data & assets

### 9.1 Runtime: `BepInEx/plugins/HellGateJson/`

| Area | Location |
|------|----------|
| Localized dialogue / QTE / splash | `{LANG}/…` (`EN`, `RU`, `JP`, …) |
| Combat AI | `CombatAi/*.json` |
| Factions | `CombatAi/Factions.json` |
| Spawn packs | `HellGateSpawnPoint/HellGateSpawn_*.txt` |
| EventCore | `EventCore/**` |
| Economy | `Economic/` |
| Drops | `DropSystem/` |
| Diagnostics toggles | `Diagnostics/*.json` |
| Player saves (runtime) | reputation / gold / rage-MB slot JSON |

Active language follows HellGate language config. Many loaders fall back to **EN** when a key/folder is missing (loader-specific).

### 9.2 Binary assets: `sources/HellGate_sources/`

AttackSounds, Rage UI, Portrait_mod, EventCore portraits, BadEndPlayer audio, Economic art, VengeanceStrike, etc.

**Distribution:** external store (MEGA / GitHub Release zip). Public git tracks **JSON only** under `HellGateAssets/BepInEx/…`.

### 9.3 Config

**`BepInEx/config/NoREroMod_HellGate.cfg`** — generated from `Plugin.SetUpConfigs()`. Faction / economy / spawn balance heavily JSON-driven with optional cfg gates.

---

## 10. Caching & performance

- **`UnifiedPlayerCacheManager`**, **`UnifiedCameraCacheManager`**, **`UnifiedGameControllerCacheManager`** — replace hot-path `Find*Tag` / repeated `GetComponent`.
- **`Patches/Performance/`** — camera / Ero start path patches.
- Spawn decor catalogs throttle disk I/O when files are missing.

---

## 11. Adding a feature (checklist)

1. Implement under `Systems/<Feature>/` with explicit `Initialize` / teardown if needed.
2. Add Harmony types; register in `SetUpPatches()` in dependency order.
3. Add JSON under `HellGateJson/`; honor language + EN fallback policy.
4. Place WAV/PNG under the correct `sources/HellGate_sources/…` tree (not into git).
5. Bind tunables in `SetUpConfigs()` with a clear section name.
6. Hook per-frame player work via **`PlayerConUpdateDispatcher`** when possible.
7. Build Release, deploy DLL, verify with NoREroMod + HellGate both enabled.
8. Update this architecture file when you add a **new subsystem or data root**.

---

## 12. Compatibility & safety

- Target the HellGate NoREroMod fork; probe logs missing members — treat as hard compatibility signals.
- Avoid overwriting vanilla scene-transition fields.
- EventCore modal: short trigger distance; do not invent behavior from obsolete concept docs — verify against current `EventCore` code and changelogs.
- Comment language in HellGate **C#**: professional **English** only (player-facing strings may stay localized).

---

## 13. Document maintenance

Update **`ARCHITECTURE.md`** when:

- A subsystem moves or is deleted
- A new JSON root appears under `HellGateJson/`
- Patch registration strategy or init order changes
- Version / distribution policy of assets changes

Prefer relative paths and folder names — never machine-specific absolute paths.
