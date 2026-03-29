# HellGate Architecture

This document describes how the **NoREroMod HellGate** BepInEx plugin is organized, how it hooks into *Night of Revenge*, and where to look when changing behavior or adding features. It is intended for contributors and maintainers.

---

## 1. Purpose and scope

**HellGate** is a gameplay and content expansion mod distributed as a single assembly (`NoR_HellGate.dll`). It builds on top of the existing **NoREroMod** plugin (same GUID namespace patterns, shared Harmony patches into game types such as `EnemyDate`, `playercon`, `UImng`).

The codebase mixes:

- **Harmony patches** (prefix/postfix/transpiler) on game and NoREroMod types.
- **Runtime systems** (UI, rage, dialogue, audio, spawn, grab, mind-broken, etc.) implemented as static or MonoBehaviour-backed services initialized from the main plugin.

---

## 2. Technology stack

| Layer | Technology |
|--------|------------|
| Game | Unity (managed assemblies under `NightofRevenge_Data/Managed/`) |
| Mod loader | BepInEx |
| IL patching | Harmony (`HarmonyLib`) |
| Language | C# (language version set in the `.csproj`; target **.NET Framework 3.5** for Unity compatibility) |
| Base mod | `NoREroMod.dll` (referenced, not embedded) |

---

## 3. Repository layout

Paths below are relative to the repository root (the folder containing `NoREroMod_HellGate.csproj`).

| Path | Role |
|------|------|
| `Core/Plugin.cs` | Main `BaseUnityPlugin`: config binding, `Awake`, Harmony setup, subsystem initialization order. |
| `Core/PluginInfo.cs` | `PLUGIN_GUID`, `PLUGIN_NAME`, `PLUGIN_VERSION` for BepInEx. |
| `Systems/` | Feature modules: dialogue, rage, audio, UI, grab, spawn, cache, camera, gameplay, effects, bad-end player, combat AI, etc. |
| `Systems/**/Patches/` | Harmony types that logically belong to a system but live under `Systems` (e.g. rage patches, grab patches). |
| `Patches/` | Game-facing Harmony patches grouped by area (`Enemy/`, `Player/`, `UI/`, `Spawn/`, `Performance/`, …). |
| `Properties/AssemblyInfo.cs` | Assembly metadata. |
| `References/` | Local framework override for MSBuild (`FrameworkPathOverride` in the project file). |
| `NoREroMod_HellGate.csproj` | Project file: output name `NoR_HellGate`, references to game and BepInEx assemblies via `$(NorGameRoot)`. |
| `HellGateAssets/` | Shipped content mirror: JSON, spawn data, optional `sources/HellGate_sources` tree for documentation and packaging (not compiled into the DLL). |
| `.gitignore` | Excludes `bin/`, `obj/`, IDE folders, etc. |

---

## 4. Build and game root resolution

The project uses **`NorGameRoot`**: the Night of Revenge install directory, resolved as **two levels up** from the project directory (`MSBuildProjectDirectory/../../`). All `HintPath` entries for `Assembly-CSharp`, BepInEx, `NoREroMod.dll`, etc. use `$(NorGameRoot)\...` so the repo stays portable.

- **Output:** `bin/Release/NoR_HellGate.dll` (or `bin/Debug/`) — copy to `BepInEx/plugins/` next to `NoREroMod.dll`.
- **Do not** commit build outputs; they are ignored.

---

## 5. Plugin entry and lifecycle

### 5.1 BepInEx metadata

- Declared in `Core/Plugin.cs`: `[BepInPlugin(PluginInfo.PLUGIN_GUID, ...)]` and `[BepInProcess("NightofRevenge.exe")]`.
- **GUID:** `NoREroMod_HellGate` (see `Core/PluginInfo.cs`).

### 5.2 `Awake()` sequence (high level)

1. **`SetUpConfigs()`** — binds hundreds of `ConfigEntry` values (BepInEx configuration); sections group combat, enemies, mind broken, rage, grab, audio, UI, etc.
2. **`SetUpPatches()`** — creates a `Harmony` instance with the plugin GUID, applies patches in a defined order (see below), then calls **`AttackSoundSystem.Initialize(this)`** and **`RageSystem.Initialize()`**, plus manual patches for `RageInputPatch` methods.
3. **Optional subsystem init** — struggle indicators, dialogue frameworks, H-scene camera helpers, corruption captions, mind-broken recovery/visuals, rage UI/input, grab chance UI, particle/wings systems, splash screen, etc.
4. **`SceneManager.sceneLoaded`** — registered to reset caches on scene changes (avoids stale player/camera references).

Patch application uses a helper **`PatchType(Type)`** which wraps `harmony.PatchAll(type)` in a try/catch so one failing type does not abort the rest.

---

## 6. Harmony patching model

### 6.1 Explicit patch types

`SetUpPatches()` registers many types **one by one** (enemy pass logic, spawn handlers, grab, mind broken, rage, camera, performance, etc.). This makes load order and inclusion obvious when reading `Plugin.cs`.

Some patches are applied with special handling (e.g. `KakashGrabPatch` via `PatchAll` on a single type, `NoREroModEliteGrabDisablerPatch.Apply(harmony)`, Dorei combat AI via `ApplyDoreiCombatAiPatch`).

### 6.2 Relationship to NoREroMod

HellGate expects NoREroMod to expose certain patched members (e.g. `EnemyDatePatch.CanEliteGrabPlayer`, `UImngPatch.WhiteFadeIn`). `RunNoREroModCompatibilityProbe()` in `Awake` uses reflection to log whether critical types/members exist.

---

## 7. Major functional areas (where to look)

The following is a **map**, not an exhaustive file list (~190+ `.cs` files).

### 7.1 Rage mode

- **`Systems/Rage/`** — `RageSystem`, combo UI, wings, visuals, hands particles, input patches, hit tracking, reset on grab, etc.
- Patches registered from `SetUpPatches()` (e.g. `RageUniversalKillTrackerPatch`, `RageHitTrackerPatch`, `WitchFineGreatswordPatch`).

### 7.2 Mind Broken and related UI

- **`Patches/UI/MindBroken/`** — core mind-broken logic, bad end, corruption captions, recovery, kill-based recovery, prefab-specific controls (e.g. Mutude, Pilgrim, Crow Inquisition, Inquisition White).
- Integrates with **`PlayerConUpdateDispatcher`** and various `EnemyDate` / struggle flows.

### 7.3 Grab system (GrabViaAttack, elite grab)

- **`Systems/GrabSystem/`** — chance calculation, UI, patches for ranged/melee context, `GrabViaAttackPatch`.
- Config under `Plugin` static fields (`enableGrabViaAttack`, `grabChance*` , etc.).

### 7.4 Dialogue and threats

- **`Systems/Dialogue/`** — dialogue databases, H-scene lines per enemy type, QTE reactions, spectator comments, **grab threat** lines.
- **`GrabThreatDialogues`** loads JSON from **`BepInEx/plugins/HellGateJson/{Language}/GrabThreatsData.json`** (language from `hellGateLanguage`; folder name matches the code, e.g. `EN`, `RU`).
- **`GrabThreatIdlePatch`** ties threats to animation/idle transitions.

### 7.5 Audio

- **`Systems/Audio/AttackSoundSystem`** — loads WAV categories from under the game’s **`sources/HellGate_sources/AttackSounds/Human/`** tree (`RegularAttack`, `PowerAttack`, per-language threat folders such as **`ThreatsEN`**, **`ThreatsRU`**, `Death`), registers clips, plays hit and threat sounds.
- **`AttackSoundRegistry`** maps threat phrases to clips (file name / normalized phrase key).
- **`AttackSoundPatch`**, **`DeathSoundPatch`** connect combat/death events to the system.

### 7.6 Spawn and scenes

- **`Patches/Spawn/`** — analyzers, scene transition fixes, test hooks.
- **`Systems/Spawn/`** (and patch types named `HellGateSpawn_*`) — fixed spawn positions driven by data under **`HellGateJson`** (e.g. spawn point text/JSON files referenced by scene name).

### 7.7 Enemy-specific “pass” and boss logic

- **`Patches/Enemy/`** — large set of `*PassPatch` / `*PassLogic` types (Touzoku, Inquisition, Vagrant, Mafia boss, Dorei, Goblin, Kakashi, Bigoni brother, etc.).
- Subfolders group **Wolf/Dorei mod custom** loaders (skeleton/texture), **HG_Mini_bose**, **goblin**, **CrowInquisition**, etc.

### 7.8 Camera and H-scene

- **`Systems/Camera/`** — H-scene camera behavior, cum display, combat presets, cache/reflection helpers.
- Multiple **`HSceneCamera*Patch`** types registered at the end of `SetUpPatches()`.

### 7.9 UI and settings

- **`Systems/UI/`** — loading screen, settings builders (new/old), menus, sound manager, input helpers, animation manager, **`SettingsDataManager`** / **`SettingsValueManager`** for persistence and HellGate-specific options.

### 7.10 Combat AI (JSON-driven)

- **`Systems/CombatAi/Patches/`** — distance/on-damage hooks; optional Dorei-specific patch application from `Plugin.ApplyDoreiCombatAiPatch`.
- Configuration/data can live under **`HellGateJson/CombatAi`** (referenced in comments in `Plugin.cs`).

### 7.11 Bad End player and effects

- **`Systems/BadEndPlayer/`**, **`Systems/Effects/`** — bad-end playback integration, H-scene black background triggers, pregnancy clip triggers (see `Patches/Effects`).

### 7.12 Player and QTE

- **`Patches/Player/`** — guard/parry mind broken, QTE restart/give-up, time scale reset, struggle invulnerability, **`PlayerConUpdateDispatcher`** as a central per-frame hub for several features.

### 7.13 Performance

- **`Patches/Performance/`** — reduces repeated `GetComponent` / `FindWithTag` costs in hot paths (camera, EroMafiamuscle start, etc.).

---

## 8. Data files and localization

### 8.1 `BepInEx/plugins/HellGateJson/`

Runtime configuration and text **per language folder** (e.g. `EN`, `RU`, `JP`, …). The active folder is selected via **`hellGateLanguage`** (same code as folder name, e.g. `EN`).

Typical contents include:

- **`GrabThreatsData.json`** — grab threat phrases and settings.
- Spawn definitions, combat AI JSON, and other feature-specific files as referenced by the corresponding systems.

If a language folder is missing, many systems fall back to **`EN`** (each loader documents its own behavior).

### 8.2 `sources/HellGate_sources/` (next to game executable)

Binary assets and audio:

- **`AttackSounds/Human/`** — `RegularAttack`, `PowerAttack`, **`Threats` + language code** (e.g. `ThreatsEN`, `ThreatsRU`) for localized threat WAVs, `Death`, plus text lists such as `HumansPrephabs.txt` / `ThreatsPrephabsHuman.txt`.
- Other subfolders may exist for mod-specific spine/audio (Wolf/Dorei paths are configurable via `Plugin` string entries).

Threat clips are keyed by **phrase ↔ file name** conventions inside `AttackSoundRegistry`. Empty or missing language-specific threat folders yield **no** clip for that phrase (threat audio does not fall back to a random unrelated clip).

---

## 9. Caching

- **`Systems/Cache/`** — centralized access to player, camera, game controller, etc., to avoid repeated lookups across patches.

---

## 10. Configuration surface

Almost all tuning is exposed via **BepInEx `Config.Bind`** in `Plugin.SetUpConfigs()`. Sections include (non-exhaustive): **Enemies**, **Bosses**, **Player**, **GrabSystem**, **MindBroken**, **Rage**, **GrabThreats**, **Audio**, **UI**, **Wolf Mod**, **Dorei**, etc.

The in-game settings UI (where present) reads/writes the same entries via the `Systems/UI` layer and `SettingsDataManager`.

---

## 11. Adding a new feature (practical checklist)

1. Prefer a **new static system** or small **MonoBehaviour** under `Systems/<Feature>/` with a clear `Initialize`/`Dispose` if needed.
2. Add **Harmony** types under `Patches/` or `Systems/.../Patches/`; register them in **`SetUpPatches()`** in a sensible order (dependencies first).
3. If you need JSON, add a subfolder under **`HellGateJson/{Language}/`** and a loader that uses `hellGateLanguage` with **`EN` fallback** where appropriate.
4. For new WAV, place files under the correct **`sources/HellGate_sources/.../Threats{Language}`** tree (e.g. `ThreatsEN`) and ensure names match the phrase-key rules expected by `AttackSoundRegistry`.
5. Bind any tunables in **`SetUpConfigs()`** with clear section names and descriptions.
6. Build with **`dotnet build -c Release`** and test with both **NoREroMod** and HellGate enabled.

---

## 12. Related files for version and packaging

- **`Properties/AssemblyInfo.cs`** — assembly version (may differ from `PluginInfo.PLUGIN_VERSION`; align when releasing).
- **`HellGateAssets/`** — use for bundling JSON and `sources` mirrors for GitHub releases; keep in sync with what the game actually needs at runtime.

---

## 13. Document maintenance

When architecture changes significantly (new subsystem, new data root, or patch registration strategy), update this file so new contributors can rely on it. Prefer **relative paths** and **folder names** over machine-specific absolute paths.
