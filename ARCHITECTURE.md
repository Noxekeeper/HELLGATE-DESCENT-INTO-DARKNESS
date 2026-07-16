# HellGate Architecture

Architecture reference for **NoREroMod HellGate** — a BepInEx plugin for *Night of Revenge*.

| | |
|---|---|
| **Assembly** | `NoR_HellGate.dll` |
| **GUID** | `NoREroMod_HellGate` |
| **Version** | **1.2.4** (`Core/PluginInfo.cs`) |
| **Companion** | `NoREroMod.dll` (Rebalance / HellGate fork) — required, loaded side by side |
| **Aligned** | 2026-07-17 |

This document describes **live structure**: layers, the full module map, data roots, config surface, and extension rules. Behavior details live in code and local maintainer notes (`docs/` — private; not shipped in the public git tree).

---

## 1. Role

HellGate is a **standalone BepInEx plugin** that extends *Night of Revenge* directly:

- Patches **vanilla game types** (`EnemyDate`, `playercon`, `UImng`, `Bigoni`, `suraimu`, …) with Harmony.
- Adds its own systems: factions, EventCore, economy, pregnancy, QTE 3.0, spawn pipeline, rage, handoff chains, hell traps, MindBroken, H-scene UX.
- Keeps features **config- and JSON-driven** so content can change without recompiling when possible.

**NoREroMod (Rebalance fork) is a required companion — not the foundation:**

- Both DLLs must load side by side. Most HellGate code has no NoREroMod dependency at all; it hooks the game.
- HellGate references `NoREroMod.dll` for a few shared types (e.g. `StruggleSystem`) and reflects into its internals (`NoREroMod.EnemyDatePatch`, `NoREroMod.UImngPatch`) where compatibility requires it.
- Where features overlap, **HellGate takes ownership and disables the NoREroMod path** (legacy QTE/struggle disablers, elite grab disabler, scaffold config push).
- Base enemy stat scaling (HP / speed / poise) stays in NoREroMod (`NoREroMod.cfg`).
- `RunNoREroModCompatibilityProbe()` verifies expected NoREroMod symbols at startup and logs anything missing.

---

## 2. Stack

| Layer | Choice |
|--------|--------|
| Game | Unity 5.x managed assemblies (`NightofRevenge_Data/Managed/`) |
| Loader | BepInEx |
| Patching | Harmony (`HarmonyLib`) |
| Language | C# → **.NET Framework 3.5** |
| Project | `NoREroMod_HellGate.csproj` → `NoR_HellGate.dll` |

Referenced assemblies: `Assembly-CSharp` (+ firstpass), `UnityEngine`, `UnityEngine.UI`, `BepInEx`, `0Harmony`, `NoREroMod`, `ES2`, `Rewired_Core`.

---

## 3. Layered model

```text
┌──────────────────────────────────────────────────────────────┐
│  Night of Revenge (Unity, Assembly-CSharp)                   │
│  EnemyDate · playercon · UImng · Bigoni · game_fragmng · …   │
└───────────────▲──────────────────────────────▲───────────────┘
                │ Harmony patches              │ Harmony patches
┌───────────────┴───────────────┐  ┌───────────┴───────────────┐
│  NoR_HellGate.dll (this repo) │  │  NoREroMod.dll (companion)│
│   Core/Plugin  config · init  │  │   base scaffold, enemy    │
│   Patches/     game hooks     │◄─┤   stat scaling; HellGate  │
│   Systems/     runtime svcs   │  │   disables overlapping    │
│                               │  │   QTE/struggle paths      │
└───────────────┬───────────────┘  └───────────────────────────┘
                │ reads
┌───────────────▼──────────────────────────────────────────────┐
│  Data: BepInEx/plugins/HellGateJson/  (JSON, spawn txt)      │
│  Assets: sources/HellGate_sources/    (WAV/PNG, external)    │
└──────────────────────────────────────────────────────────────┘
```

HellGate hooks the game directly; the arrow into NoREroMod is narrow (shared `StruggleSystem`, reflection into `EnemyDatePatch` / `UImngPatch`, compatibility probe).

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
| `Systems/` | Feature modules (~315 `.cs`; see §8) |
| `Patches/` | Game-facing Harmony types (~145 `.cs`; see §9) |
| `Properties/` | Assembly metadata |
| `References/` | Local TF 3.5 framework path for MSBuild |
| `HellGateAssets/BepInEx/plugins/HellGateJson/` | **Shipped data mirror** (JSON, spawn txt, EventCore packs) |
| `ARCHITECTURE.md` | This document |
| `.gitignore` | Excludes build junk, private trees, heavy sources |

`Systems/H_Scenes/` is currently an empty placeholder (no compiled files).

**Not in public git** (kept locally / external hosting):

| Path | Policy |
|------|--------|
| `docs/` | Maintainer docs (often RU); public EN docs planned as a separate push |
| `dev/` | Local notes, `compili.bat`, changelogs, Manifesto generators |
| `HellGateAssets/sources/` | Heavy WAV/PNG packs — **MEGA / Releases**, not git |

---

## 5. Build & deploy

1. **Game root (`NorGameRoot`)** — two levels above the project directory (`…/NightofRevenge107`).
2. Build: `dotnet build -c Release` or local `dev/compili.bat` (clean → build → copy).
3. Output: `bin/Release/NoR_HellGate.dll` → copy to `BepInEx/plugins/` next to `NoREroMod.dll`.
4. Runtime data: `BepInEx/plugins/HellGateJson/` (synced from `HellGateAssets/…/HellGateJson/` for releases).
5. Binary assets: `sources/HellGate_sources/` relative to the game install.

Do not commit `bin/`, `obj/`, root `BepInEx/`, or logs.

---

## 6. Plugin lifecycle

### 6.1 Entry

- `[BepInPlugin(PluginInfo.PLUGIN_GUID, …)]`
- `[BepInProcess("NightofRevenge.exe")]`

### 6.2 `Awake()` (order matters)

1. **`SetUpConfigs()`** — binds `NoREroMod_HellGate.cfg` (~65 sections; map in §10).
2. **Early module configs** — `PregnancyConfig.Initialize`, `SpawnTemplateCatalog.Initialize`.
3. **HellTraps preload** — `LethalMagicTrapRuntime` / `LethalCocoonTrapRuntime` template registration, death-display preload, death/shock audio init.
4. **`SetUpPatches()`** — explicit Harmony registration (authoritative list in `Plugin.cs`; see §7).
5. **`EventCoreBootstrap.Install`**; then, each behind its own enable flag: **EventTrap** bootstrap, **Reinforcement** bootstrap.
6. **Frameworks & UX** — struggle visual indicators, `DialogueFramework`, `QTEReactionFramework`, `HSceneCameraController`, `HSceneStartZoomEffect`.
7. **MindBroken** — corruption captions, recovery system, visual effects (per-flag).
8. **Rage** — hands glow / particles, wings, slow-mo bone glow, `TimeSlowMoActivateClipSystem` (when rage enabled).
9. **UI** — `PortraitModSystem`, `HellGateFontProvider` routing.
10. **Diagnostics** (JSON-gated, off by default) — Tentacle, TrapBody, Kinoko.
11. **Economy** — `EconomicConfig.Initialize`; when enabled: `GoldAssetLoader`, `GoldWallet`, `GoldLostPileSceneLoader`, gold patches.
12. **Audio / Rage core** — `AttackSoundSystem`, `RageSystem`, manual rage input patches.
13. **`SceneManager.sceneLoaded`** — cache resets; EventCore session reload.

### 6.3 Per-frame hub

**`Patches/Player/PlayerConUpdateDispatcher`** — postfix on `playercon.Update`:

- H-scene start zoom check
- QTE / struggle bridges
- Faction H-scene reputation + Mercy de-escalation
- Gold H-scene earnings (economy on)

---

## 7. Harmony model

- Prefer **named types** registered via `PatchType` / `PatchTypeWithLog`, not a blind `PatchAll(assembly)`.
- Special registrations: Kakash grab (`PatchAll` on type), Dorei combat AI apply, HeckGate/biscord `PatchAll` on module type, `BigoniBrotherGameOverBypass.Apply` (nested classes), `NoREroModEliteGrabDisablerPatch.Apply`.
- **`RunNoREroModCompatibilityProbe()`** logs missing NoREroMod symbols at startup.

### Do not reintroduce

| Removed | Why |
|---------|-----|
| `SpawnSceneTransitionFix` | Overwrote `_re_Scenename`; broke additive EV scenes |
| Per-map `HellGateSpawn_*.cs` + `UnifiedSpawnManager` | Replaced by JSON/txt + location refresh pipeline |
| Custom `GameSettingsMenu` stack | Abandoned; settings live in cfg / JSON |
| `BigoniBrotherERO` component path | Live path patches **`StartBigoniERO`** via identity |

---

## 8. Systems map (`Systems/`)

### 8.1 Content & world

#### Spawn (~30 files)

- **Refresh owner:** `HellGateLocationSpawnRefresh` + `HellGateSpawnSceneHints` (zone→pack registry).
- **Executor:** `SpawnConfigExecutor` — line formats: fixed, `RANDOM`, `RANDOM_GROUP`, `POOL[…]`, `gold=`, `TRAP`, `DECOR`, `EVENTTRAP`, `REINFORCEMENT`, `RANDOM_HOSTAGE`.
- **Line metadata:** `|faction=`, `|ec_event=` / `|ec=` / `|ec_pool=`, facing / sort suffixes.
- **Registry & caches:** `EnemyPrefabRegistry` (spawn keys → prefab names, incl. custom clones), `SpawnTemplateCatalog` + disk caches, `SpawnDecorCatalog`, `SpawnTemplateWhitelist`.
- **Refresh patches:** primary `SceneLoadSpawnRefreshPatch` (after `LoadSceneAndWait`); door wipe `SceneMoveTransitionSpawnPatch`; altar `SpawnRespawnAfterAltarPatch`; safety net `LocationTransitionSpawnController`.
- **Support:** boss spawn bootstrap/runtime, hostage runtime + markers, anchor discovery (EVENTTRAP/REINFORCEMENT anchors), flip/rotation/depth utilities, weather guard.
- **Data:** `HellGateJson/HellGateSpawnPoint/HellGateSpawn_*.txt` + authoring cheatsheets.

#### EventCore (~38 files)

- **Pipeline:** `Core` (bootstrap, runtime, paths, pause) → `Content` (JSON registry, manual lang parsing) → `Host` (`EventCoreHost` per spawned NPC) → `Handlers` (broker gate flow, FSP sex_paid flow) → `UI` (modal canvas, side portraits).
- **Subsystems:** `EventTrap/` (KO-zone `etrap_*` packs), `Reinforcement/` — separate bootstraps, non-modal.
- **Data:** `HellGateJson/EventCore/` — manifest, definitions, per-language packs (**10 langs**: Ru En Cn Jp Kr De Pt Br Es Fr), `event_trap_registry.json`, `reinforcement_registry.json`. String pools fail closed (no silent RU fallback).
- **Runtime rules:** modal NPCs use **consent grab** (not knockdown/struggle); ambush branches use session hostility; encounter shells run under passive faction `eventcore_encounter` (no emblem/HUD until resolved).
- **Spawn integration:** `SpawnConfigExecutor` attaches `EventCoreHost` from line metadata.

#### CombatAi + Factions (~32 files)

- **CombatAi:** distance / on-damage reaction patches driven by `HellGateJson/CombatAi/*.json`; Dorei-specific config; Sinnerslave crossbow patch.
- **Factions runtime:** `EnemyFactionRuntime` + `EnemyFactionsConfig` (hot reload ~2 s from `CombatAi/Factions.json`); `FactionIds`, `FactionStyle`, bone marker attachments, marker visibility.
- **Combat:** all-vs-all damage routing incl. projectiles (`FactionProjectileOwner` / projectile patches); activation radius near player; inter-faction target distance cap; **combat commit** (fight continues after player leaves); friendly-fire toggle; H-scene freeze.
- **Reputation:** `PlayerFactionReputation` (+ save hook → per-slot JSON), reputation dynamics/behavior, sign-based aggro, provocation (incl. player magic via `mgname` ownership), Mercy de-escalation (`FactionDeescalationRuntime` + `MercyEventUISystem`), speed/vision scaling.
- **Boss policy:** `FactionBossDetection` (BOSSflag / name prefixes / JSON lists) — shared by Rage and MindBroken kill logic.
- **HUD:** `FactionReputationHud` (toggle key), bootstrap patch.

#### Pregnancy (~57 files; largest module)

- **Gate:** `PregnancyConfig.Enable`; ~11 cfg subsections (`[Pregnancy.*]`).
- **Conception & tracking:** nakadashi tracker patch on `EnemyDate.Nakadasi`, `WombMeterNakadashiPoller`, `SemenValueMultiplier`, `PregnancySourceResolver`, `PregnancyConceptionApplier`.
- **Progression:** `TrimesterProgression`, trimester visual effects, `FactionTrimesterModifier`, physics/blocking options.
- **Offspring:** `OffspringArchetype/` catalog (weighted JSON), `ChildData`, bloodline bonuses (`BloodlineRageBonus`, `OffspringBloodlineBonuses`), witch offspring friendly-fire patch.
- **ShelterAttack:** wave scheduler/poller/tracker, slot store, timer HUD, phrases, JSON wave parser — raid events on the shelter.
- **HUD:** `WombMeterHud` + shared `WombMeterHudLayout`; suppresses vanilla creampie value UI.
- **Safety:** birth guards (`PregnancyBirthGuardPatch` nested pre/postfixes), birth recovery (`BirthRecoveryJigoPatch`, `BirthRecoveryStruggleState`), altar/vengeance/runtime cleanup, `WhiteFadeInNullSafePatch`.
- **Persistence:** `PregnancySlotStore` (per save slot).

#### Economy (~22 files)

- **Gate:** `EconomicConfig.Enable` (JSON `HellGateJson/Economic/Economy.json`).
- **Core:** `GoldWallet` (save-hook persistence only — gameplay never flushes to disk), `GoldStaticMng`, `GoldDropTable` + awarder, `GoldPickup`, lost pile (Souls-style) + scene loader.
- **Loss modes:** combat gold loss (chance on damage), knockdown loss, death drop percent — `Systems/Economy/Patches/`.
- **HUD & FX:** `GoldHud` (anchored via `Economy.json → Hud.*`), popup system, audio player, asset loader.
- **Bridges:** `GoldHSceneEarningsBridge` via dispatcher; enemy death drops via patches.
- **Saves:** `Economic/PlayerGold_Slot0N.json`.

#### Rewards (~2 files)

- `DropSystem` — weighted drop tables from `HellGateJson/DropSystem/*.json` (e.g. biscord); `VanillaEnemyDropWiring` binds tables to vanilla enemies.

### 8.2 Combat & player

#### Gameplay (~25 files)

- **QTE 3.0:** `QTESystem` (HellGate-original), `QTESPCalculator`, `QTEStruggleWindowManager`; NoREroMod legacy paths disabled by `QTEStruggleSystemDisabler` / `QTEStruggleHistoryDisabler` / `StruggleCameraShakeDisabler`. Cfg `[QTE]`, per-language `QTEReactionData.json`.
- **Struggle UX:** `StruggleVisualIndicators`; difficulty & MindBroken hooks via cfg (`[StruggleDifficulty]`, `[Ero]`, `[PleasureStatus]`).
- **VengeanceStrike:** parry-stab presentation package — runtime, content/paths, hands patch, stab presentation/sound, no-grab-during-stab, player-update patch. Assets `sources/HellGate_sources/VengeanceStrike/`; cfg `[VengeanceStrike]` (slow-mo, hand glow, rage cost, spine boost).
- **WeaponAnimations:** witch greatsword patches + combo sequences, light one-hand 3-hit extended combo, `Profiles/` (combo profiles), `WeaponAnimationMechanics`. Cfg `[WeaponAnimations]`.
- **AirGuard:** `AirGuardPatch` (cfg `[AirGuard]`).
- **Misc:** `EnemyConstantVisibilityPatch`, `PlayerEroContextUtility`.

#### Rage (~23 files)

- **Core:** `RageSystem` (3 tiers), combo system + combo UI, input handler/patch, hit & universal-kill trackers (boss detection shared with factions), reset-on-grab-down.
- **Visuals:** `RageUISystem` (PNG banner, bar, tier sparks), wings, hands glow/particles, fire gradient, combo blood, slow-mo visual systems, `TimeSlowMoActivateClipSystem`.
- **Persistence:** `RageMindBrokenSlotStore` + save/load hooks → `HellGateJson/PlayerState/PlayerRageMindBroken_Slot0N.json`.
- **Options:** `RageActiveImmunityPatch` (blocks grab/knockdown during active window), MindBroken persistence hooks.
- Cfg `[RageMode]`, `[RageVisualEffects]`, `[SlowMoVisualEffects]`, `[TakeVengeance]`.

#### GrabSystem (~6 files)

- `GrabViaAttackPatch` + `GrabChanceCalculator` + `DamageSourceClassifier` (melee/ranged context).
- Grab-chance UI label rides the Rage overlay canvas (`GrabChanceRageUILabel` in Rage module).
- When `StartZoom.Enable` is on, grab slowmo defers to `HSceneStartZoomEffect`. Cfg `[GrabSystemNG]`.

#### Handoff (~3 files)

- `EnemyHandoffSystem` — global handoff counter across **all** enemy types (force-mid entry after first handoff; reset on escape/scene change).
- `DelayedHandoffScript` (real-time delay, slow-mo independent), `EnemyHandoffPlayerHelper`.
- Per-enemy execution lives in `Patches/Enemy/*Pass*` (§9.1); gates `[HandoffSystem]`, `[EnemyPass]`.

### 8.3 Presentation & UX

#### Dialogue (~23 files)

- **Framework:** `DialogueFramework` + `DialoguePool` + `DialogueDisplay` (bubbles), `DialogueEventProcessor`, `DialogueDatabase`.
- **Content sets:** per-enemy H-scene dialogues (Touzoku normal/axe, Kakasi, Goblin, InquisitionBlack, Aradia variants), `GrabThreatDialogues` (+ idle threats), `SpectatorCommentsSystem`, `BiscordDamageDialogues`, QTE reactions (framework + database).
- **Sound glue:** `SoundRegistry`, `SoundOnomatopoeiaPatch` (cfg `[SoundOnomatopoeia]`).
- **EventCore gate:** combat threats suppressed while a modal host is active and non-hostile.
- **Data:** `{LANG}/GrabThreatsData.json`, dialogue JSON per enemy, `QTEReactionData.json`.

#### Camera (~17 files) + HSceneEffects

- H-scene camera controller, direct pan, `GetTargetsMidPoint` / move override / smoothing disable / zoom control patches; combat camera presets (cfg `[CombatCamera]`).
- `CumDisplayManager` (cfg `[CumDisplay]`) — X-ray / pregnancy clip slots.
- `HSceneStartZoomEffect` — center + zoom + slowmo at H start (from dispatcher; cfg `[HSceneEffects]` → `StartZoom.*`).

#### Effects / BadEndPlayer

- `HSceneBlackBackgroundSystem` + trigger patch (FIN detection incl. BigoniBrother/Mutude special-cases; cfg `[HSceneBlackBackground]`).
- `BadEndPlayerSystem` + manifest/loader — bad-end playback (cfg `[BadEndPlayer]`); audio under `sources/HellGate_sources/BadEndPlayer/`.
- `Patches/Effects/PregnancyClipTrigger` — pregnancy clip FX.

#### UI (~8 files)

- `LoadingScreenSystem` (custom art, sponsor labels, locale filters), `SplashScreenUILabels`, `HellGateTitleMenuBackdrop`.
- **`HellGateFontProvider`** — central font routing for all HellGate `UnityEngine.UI.Text` surfaces; cfg `[Fonts]` (`FontFamilyWestern`, `FontFamilyAsian` with per-locale Windows defaults), `[DialogueFonts]`.
- **`HudVisibilityGate`** — custom HUDs follow vanilla canvas visibility; `CanvasGroup.alpha` instead of `SetActive`.
- **Portrait:** `PortraitModSystem` + asset loader + state resolver — replaces `UIface` Spine with PNG cycles (Sex → Rage → Brainwash → Normal priority); cfg `[PortraitMod]`; assets `sources/HellGate_sources/Portrait_mod/`.

#### Audio (~4 files)

- `AttackSoundSystem` — regular/power attack, threat (per language), death WAVs from `sources/HellGate_sources/AttackSounds/`; attack/death sound patches. Cfg `[AttackSounds]`, `[GrabThreats]`.

### 8.4 Infrastructure

| Module | Content |
|--------|---------|
| **Cache** | `UnifiedPlayerCacheManager`, `UnifiedCameraCacheManager`, `UnifiedGameControllerCacheManager` — interval caches replacing hot-path `Find*Tag` / `GetComponent` |
| **Compatibility** | `NoREroModScaffoldConfigPush` — pushes required values into NoREroMod config surface |
| **Diagnostics** | Opt-in JSON-gated investigation kits: `Tentacle/`, `TrapBody/`, `Kinoko/` (snapshot + monitor + lifecycle patches + dedicated log each). Off by default; keep out of release cfg |

---

## 9. Patches map (`Patches/`)

### 9.1 Enemy (~65 files)

**Pass/handoff for vanilla enemies** — `*PassPatch` / `*PassLogic`: Touzoku (normal/axe), Inquisition (black/white/red), Vagrant, PrisonOfficer, Librarian, MummyDog, MummyMan (+ handoff grab-block/state), Pilgrim, Undead, CrowInquisition (+ ERO fix), Goblin (+ hardcore struggle spawn), Kakasi (cross patch, handoff hide), Dorei, Mutude/Six_hand (+ effects, video tracker). `_Template/EnemyNamePassLogic.cs` is the copy-paste scaffold; `Base/BaseEnemyPassPatch` shares plumbing.

**Custom enemy packs** (spawn key → cloned vanilla prefab + swapped visuals/logic):

| Pack | Basis | Notes |
|------|-------|-------|
| `HG_Mini_bose` (**BigoniBrother**) | `Bigoni` + `StartBigoniERO` | Identity/marker tagging; patch + pass logic + GameOver bypass; no custom ERO class |
| `MafiaBossCustom` | mafia_muscle | Stats, grab, ERO patches, pass logic; **not** a faction boss |
| `BossTouzokuCustom` | Touzoku boss | Runtime + stats + intro/combat/safety/ero patch sets, HP scale, activator |
| `WolfModCustom` | MummyDog | Skeleton + texture swap (cfg `[WolfMod]`) |
| `HellishTouzokuModCustom` | Touzoku | Skeleton/texture swap + H-escape patch (cfg `[HellishTouzoku]`) |
| `DoreiModCustom` | Dorei | Skeleton/texture swap, spectator idle patch (cfg `[DoreiMod]`) |
| `ButcherModCustom` | Slaughterer | Rick-style fatality only (cfg `[ButcherMod]`) |
| `RickEnemyModShared` | — | Shared fatality logo/icon + spine/texture loaders for Rick-family packs (cfg `[RickEnemyMod]`) |
| `HeckGateEnemy` (**biscord**) | `suraimu` | Slime module (`PatchAll`), visual profile, eyes attachment, struggle/escape patch set; forced neutral faction |

New packs register spawn keys in `EnemyPrefabRegistry` and branch in `SpawnConfigExecutor`.

### 9.2 Player (+ PlayerRespawn)

- **Hub:** `PlayerConUpdateDispatcher` (§6.3).
- **H-scene/struggle recovery:** `HSceneEscapeStateCleanup` (overlay cleanup on escape), `StruggleEscapeCombatRecoveryPatch`, `PlayerCombatControlRecovery`, `StruggleInvulnPatch`, `TimeScaleResetOnEscapePatch`, `StrugglePotionEscapePatch` (potion easy-escape compat), `PlayerEnemyGrabStruggleSupport` (field-boss H escape).
- **Vanilla-flow guards:** `VanillaEvSceneExitPatch` (EV scene exit / vengeance reset), `VanillaCutsceneSceneGuard`, `VanillaAltarCatalog`, `VanillaStoryEventInputGuard`, `VanillaKnockdownRecoveryPatch` (+ utility), `DownedDeathGuard`, `EnemyLibraryEroStatusGuardPatch`.
- **Pregnancy:** `PregnancyBirthGuardPatch`, `BirthRecoveryJigoPatch`, `BirthRecoveryStruggleState`.
- **Misc:** `GuardParryMindBrokenPatch`, `PlayerHitBloodCleanupPatch`.
- **PlayerRespawn/**: vengeance respawn effect, death soul offset.

### 9.3 UI / MindBroken (~13 files)

MindBroken is a first-class feature living under `Patches/UI/MindBroken/`:

- **Core:** `MindBrokenSystem` (state machine), `MindBrokenBadEndSystem`, `MindBrokenVisualEffectsSystem`, `CorruptionCaptionsSystem` (captions at high corruption), `H_scenesAllEnemiesCorruption`.
- **Recovery:** `MindBrokenRecoverySystem`, `EnemyKillRecoveryPatch` (per-class OnDestroy tracking), `MindBrokenUniversalKillRecoveryPatch` (boss detection shared with factions).
- **Per-enemy controls:** Mutude, CrowInquisition, InquisitionWhite, Pilgrim.
- Cfg: `[MindBroken]`, `[MindBrokenRecovery]`, `[MindBrokenVisualEffects]`, `[CorruptionCaptions]`, per-enemy `[<X>MindBroken]` sections.
- `Patches/UI/BadstatusUiPatch` — badstatus bar tweaks.

### 9.4 HellTraps (~34 files)

Lethal trap content pack (cfg `[HellTraps]`), two trap families sharing common death infrastructure:

- **LethalMagicTrap:** runtime + registry/template registration, asset loader, paths, patches, bullet marker, ero suppression (reflects NoREroMod `EnemyDatePatch`), death context/tuning/display, death audio.
- **LethalCocoonTrap:** same structure (runtime, registry, loader, paths, patches, death context/tuning/display) + scene markers/tracker.
- **Shared death kit:** `LethalTrapDeathCommon`, black screen, cleanup, sprite loader, PNG clip playback profile, heartbeat loop, hit gate, danger thoughts + phrases.
- **Vengeance shock:** MindBroken shock, session, tuning, audio — post-death vengeance sequence.
- Templates registered into the Spawn template catalog during `Awake` (§6.2 step 3); spawned via `TRAP`/template lines.

### 9.5 Other patch areas

| Area | Content |
|------|---------|
| `Trap/` | Trap H-scene mosaic disable (extensible owner list), trap escape/context fixes |
| `Spawn/` | `SpawnPointAnalyzer` — F11 coordinate recorder, RMB hot-reload; attack-input block while recording |
| `Performance/` | `CameraGetComponentPatch` (EnemyDate/Trapdata/Slavehelp `camera_GetComponent` → cache), `EroEnemyStartPatch` |
| `Effects/` | `PregnancyClipTrigger` |
| `Base/` | Shared patch plumbing |

---

## 10. Configuration surface

**`BepInEx/config/NoREroMod_HellGate.cfg`** — generated by `SetUpConfigs()`; ~65 sections. Groups:

| Group | Sections |
|-------|----------|
| General / combat | `[General]`, `[Hardcore]`, `[Combat]`, `[FieldOfView]`, `[SavePoints]`, `[PlayerVisualFixes]`, `[TouzokuAggression]` |
| H-scene & struggle | `[Ero]`, `[PleasureStatus]`, `[StruggleDifficulty]`, `[QTE]`, `[VisualIndicators]`, `[HSceneEffects]`, `[HSceneBlackBackground]`, `[HSceneCameraZoom]`, `[CumDisplay]` |
| Handoff / pass | `[HandoffSystem]`, `[EnemyPass]`, `[GoblinHardcore]`, `[BigoniBrother]` |
| MindBroken | `[MindBroken]`, `[MindBrokenRecovery]`, `[MindBrokenVisualEffects]`, `[CorruptionCaptions]`, `[MutudeMindBroken]`, `[CrowInquisitionMindBroken]`, `[InquisitionWhiteMindBroken]`, `[PilgrimMindBroken]` |
| Rage & vengeance | `[RageMode]`, `[RageVisualEffects]`, `[SlowMoVisualEffects]`, `[TakeVengeance]`, `[VengeanceStrike]` |
| Grab | `[GrabSystemNG]`, `[GrabThreats]` |
| Pregnancy | `[Pregnancy]` + `[Pregnancy.Altar/Blocking/Bloodline/OffspringArchetype/OffspringCombat/Physics/SemenValue/ShelterAttack/Trimester/TrimesterModifiers/TrimesterVisuals]` |
| World & content | `[EventCore]`, `[SpawnTemplates]`, `[HellTraps]`, `[WeaponAnimations]`, `[AirGuard]` |
| Custom enemies | `[WolfMod]`, `[HellishTouzoku]`, `[DoreiMod]`, `[ButcherMod]`, `[RickEnemyMod]` |
| Audio & dialogue | `[AttackSounds]`, `[SoundOnomatopoeia]`, `[DialogueEventProcessor]`, `[DialogueFonts]` |
| UI & presentation | `[Fonts]`, `[PortraitMod]`, `[BadEndPlayer]`, `[CombatCamera]` |

**Rule of thumb:** cfg holds **feature gates and player-facing tuning**; content balance lives in JSON — factions (`Factions.json`), economy (`Economy.json`, `GoldDropTable.json`), combat AI, drop tables, spawn packs (txt), EventCore packs. JSON supports hot reload where noted per subsystem.

---

## 11. Data & assets

### 11.1 Runtime: `BepInEx/plugins/HellGateJson/`

| Area | Location |
|------|----------|
| Localized dialogue / QTE / splash | `{LANG}/…` (`EN`, `RU`, `JP`, `KR`, `CN`, `DE`, `FR`, `ES`, `PT`, `BR`) |
| Combat AI | `CombatAi/*.json` |
| Factions | `CombatAi/Factions.json` (language-agnostic, hot reload) |
| Spawn packs | `HellGateSpawnPoint/HellGateSpawn_*.txt` + catalogs/cheatsheets |
| EventCore | `EventCore/**` (manifest, 10-lang packs, trap/reinforcement registries) |
| Economy | `Economic/Economy.json`, `Economic/GoldDropTable.json` |
| Drops | `DropSystem/*.json` |
| Diagnostics toggles | `Diagnostics/*.json` (all `Enable: false` by default) |

**Per-slot runtime saves** (written next to their module data):

| Save | Path |
|------|------|
| Faction reputation | `PlayerReputation_Slot0N.json` |
| Gold wallet | `Economic/PlayerGold_Slot0N.json` |
| Rage / MindBroken | `PlayerState/PlayerRageMindBroken_Slot0N.json` |
| Pregnancy | pregnancy slot store JSON |

Active language follows the HellGate language config. Many loaders fall back to **EN** when a key/folder is missing (loader-specific); EventCore string pools fail closed instead.

### 11.2 Binary assets: `sources/HellGate_sources/`

AttackSounds, Rage UI, Portrait_mod frames, EventCore portrait folders (AradiaAva / TouzokuAva), BadEndPlayer audio, Economic gold art, BiscordSounds, VengeanceStrike, RickEnemyMod (Butcher, Fatality Logo), HellTraps death clips, Fonts (legacy).

**Distribution:** external store (MEGA / GitHub Release zip). Public git tracks **JSON/txt only** under `HellGateAssets/BepInEx/…`. Loaders resolve assets relative to the game root — never machine-specific absolute paths.

---

## 12. Caching & performance

- Interval caches (§8.4) replace hot-path `FindGameObjectWithTag` / `GetComponent` across dialogue, camera, and grab code; reset on scene change.
- `Patches/Performance/` rewrites vanilla `camera_GetComponent` and `EroMafiamuscle.Start` hot paths onto the caches (cached `FieldInfo`, no per-call reflection).
- Spawn/decor catalogs throttle disk I/O and missing-file logging.
- QTE resolves its enemy once per session start; UI buttons are pooled.

---

## 13. Adding a feature (checklist)

1. Implement under `Systems/<Feature>/` with explicit `Initialize` / session teardown if needed.
2. Add Harmony types; register in `SetUpPatches()` in dependency order.
3. Add JSON under `HellGateJson/`; honor language + EN fallback policy.
4. Place WAV/PNG under the correct `sources/HellGate_sources/…` tree (not into git).
5. Bind tunables in `SetUpConfigs()` with a clear section name (see §10 groups).
6. Hook per-frame player work via **`PlayerConUpdateDispatcher`** when possible.
7. New spawnable enemy: register key in `EnemyPrefabRegistry`, branch in `SpawnConfigExecutor`, start pass logic from `_Template`.
8. Build Release, deploy DLL, verify with NoREroMod + HellGate both enabled.
9. Update this file when you add a **new subsystem, data root, or cfg section group**.

---

## 14. Compatibility & safety

- Target the HellGate NoREroMod fork; compatibility probe misses are hard signals.
- Never overwrite vanilla scene-transition fields (`_re_Scenename` incident — §7).
- EventCore modal: short trigger distance; verify behavior against current code/changelogs, not old concept docs.
- H-scene escape must go through the cleanup patches (overlays, timescale, combat control) — do not add parallel escape paths.
- Comment language in HellGate **C#**: professional **English** only (player-facing strings may stay localized).

---

## 15. Document maintenance

Update **`ARCHITECTURE.md`** when:

- A subsystem is added, moved, or deleted
- A new JSON root or save file appears under `HellGateJson/`
- Patch registration strategy or init order changes
- A new cfg section group is introduced
- Version / distribution policy of assets changes

Prefer relative paths and folder names — never machine-specific absolute paths.
