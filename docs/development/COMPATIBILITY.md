# Compatibility and Known Hazards

The NoREroMod boundary, invariants that must not be broken, and incidents
that shaped current rules.

## NoREroMod boundary

HellGate patches the game directly; `NoREroMod.dll` is a required companion,
not a base layer. The deliberate contact surface is small:

| Contact | Mechanism |
|---------|-----------|
| Shared types (e.g. `StruggleSystem`) | direct assembly reference |
| `NoREroMod.EnemyDatePatch`, `NoREroMod.UImngPatch` internals | reflection |
| Legacy QTE/struggle path | disabled by HellGate disabler patches |
| Elite grab behavior | disabled via `NoREroModEliteGrabDisablerPatch` |
| Required companion config values | pushed by `NoREroModScaffoldConfigPush` |
| Base enemy stat scaling (HP/speed/poise) | stays in NoREroMod (`NoREroMod.cfg`) |

`RunNoREroModCompatibilityProbe()` verifies expected NoREroMod symbols at
startup. A probe warning means the companion build does not match the
expected fork — treat it as a hard integration failure. Compatibility with
the original NoREroMod or other forks is not maintained.

When features overlap, HellGate takes ownership and the NoREroMod path stays
disabled. Never run both implementations of QTE/struggle at once.

## Invariants

- **H-scene escape** must flow through the existing cleanup patches
  (`HSceneEscapeStateCleanup`, `TimeScaleResetOnEscapePatch`,
  `PlayerCombatControlRecovery`, `StruggleEscapeCombatRecoveryPatch`). Do not
  add parallel escape paths; extend the cleanup set instead.
- **Timescale**: anything slowing time restores it via the escape/cleanup
  path. Grab slow-mo defers to `HSceneStartZoomEffect` when start zoom is
  enabled.
- **Per-frame player logic** goes through `PlayerConUpdateDispatcher`.
- **Persistence** goes through save/load hook patches; per-slot files only.
- **Boss detection** is centralized in `FactionBossDetection`.
- **Fonts and HUD visibility** go through `HellGateFontProvider` and
  `HudVisibilityGate`.
- Vanilla flow guards in `Patches/Player/` (altar, cutscene, story-event
  input, knockdown recovery, downed-death) encode fixes for real soft locks —
  changing them requires regression-testing those flows, including while
  pregnant. Guard-by-guard map: [PLAYER_GUARDS.md](PLAYER_GUARDS.md).

## Incident-derived rules (do not reintroduce)

| Removed | Reason |
|---------|--------|
| `SpawnSceneTransitionFix` | overwrote the vanilla `_re_Scenename` field and broke additive EV scenes |
| per-map `HellGateSpawn_*.cs` + `UnifiedSpawnManager` | replaced by the data-driven spawn pipeline |
| custom `GameSettingsMenu` UI stack | abandoned; settings live in cfg/JSON |
| `BigoniBrotherERO` component | replaced by patching vanilla `StartBigoniERO` with identity-based detection |

## High-risk change areas

Require targeted in-game regression, not just a clean build:

- `playercon.Update` and player state recovery;
- scene and additive-event transitions;
- H-scene entry, handoff, escape, cleanup;
- save/load hooks and slot files;
- pregnancy birth/altar/vengeance recovery;
- faction targeting and projectile ownership;
- prefab cloning and Spine/texture replacement;
- any reflection into NoREroMod internals.
