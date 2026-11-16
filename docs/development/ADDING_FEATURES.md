# Adding a Feature

Conventions and the standard checklist for new feature modules.

## Placement

- Feature behavior lives in `Systems/<Feature>/`.
- Game-facing Harmony hooks stay thin — either in `Patches/` (cross-cutting)
  or in `Systems/<Feature>/Patches/` (feature-owned). A hook should extract
  context and delegate; it should not contain the feature logic.
- If the feature needs setup or teardown, give it an explicit `Initialize`
  and reset its session state on `SceneManager.sceneLoaded` where relevant.

## Registration

- Add every new `.cs` file to `NoREroMod_HellGate.csproj`.
- Register patch types explicitly in `SetUpPatches()` in `Core/Plugin.cs`,
  in dependency order, using the isolated registration helpers so one failing
  target cannot abort unrelated modules. Keep the `Plugin.cs` line thin —
  prefer `Module.Initialize` / `*Config.Bind` inside the feature folder when
  the pattern already exists (Fear, DeadArmor, …). Full composition-root
  split is planned **after 1.3.0**
  ([ARCHITECTURE.md §10](../../ARCHITECTURE.md#10-planned-composition-after-130)).
- If initialization order matters (config before patches, templates before
  spawn), follow the existing `Awake()` sequence documented in
  `../../ARCHITECTURE.md`.

## Configuration and data

- Feature gates and player-tunable values: bind under a clearly named cfg
  section (in `SetUpConfigs()` or the feature’s `*Config.Bind`, matching
  nearby modules). Every feature must be fully disableable, and disabled
  means no patches with behavioral side effects.
- Content and balance data: JSON (or spawn text) under `HellGateJson/` —
  see `DATA_FORMATS.md` for roots, localization, and fallback policy.
- Binary assets go to the external `sources/HellGate_sources/` tree, resolved
  relative to the game root. Never commit them.

## Runtime conventions

- Per-frame player work hooks into `PlayerConUpdateDispatcher` instead of
  adding another `playercon.Update` patch.
- Hot paths use the unified caches (`Systems/Cache/`) instead of
  `GameObject.FindGameObjectWithTag` / repeated `GetComponent`.
- Custom HUD elements register with `HudVisibilityGate` and toggle via
  `CanvasGroup.alpha`, and pull fonts from `HellGateFontProvider`.
- No `FindObjectOfType` / `FindObjectsOfType` in per-frame paths: they walk the
  whole scene. Track the instance from its `Start` / `OnEnable` instead (see
  `HudVisibilityGateRegistryPatch`, `GameplayCursorHiderAltarMenuPatch`), and
  return early from disabled diagnostics before any scan.
- Boss classification goes through the shared `FactionBossDetection`.
- Anything that changes `Time.timeScale` or player control must restore it
  through the existing escape/cleanup paths (see `COMPATIBILITY.md`).

## Checklist

1. Implement under `Systems/<Feature>/`.
2. Add files to the `.csproj` manifest.
3. Register patches in `SetUpPatches()`.
4. Bind cfg gates/tuning in `SetUpConfigs()`.
5. Add JSON data and (if needed) external assets.
6. Build Release, deploy, verify the BepInEx log.
7. Test with the feature enabled and disabled.
8. Update `../../ARCHITECTURE.md` (subsystem map) and add or update the
   module document under `../modules/`.
