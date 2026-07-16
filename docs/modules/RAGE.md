# Rage

Three-tier rage resource with combos, slow motion, visual effects, and
per-slot persistence shared with MindBroken.

Code: `Systems/Rage/` · Config: `[RageMode]`, `[RageVisualEffects]`, `[SlowMoVisualEffects]`, `[TakeVengeance]` · Save: `PlayerState/PlayerRageMindBroken_Slot0N.json`

## Core

- `RageSystem` — tier state machine and rage gain/decay.
- Gain sources: `RageHitTrackerPatch` (dealt/received hits) and
  `RageUniversalKillTrackerPatch` (kills; boss classification comes from the
  shared `FactionBossDetection` — see `FACTIONS_AND_COMBAT_AI.md`).
- `RageComboSystem` — kill/hit combo counting feeding rage gain.
- Activation: `RageInputHandler` + `RageInputPatch` (manual trigger).
- `RageResetOnGrabDownPatch` — rage resets when the player is grabbed down.
- `RageActiveImmunityPatch` — optional immunity to grab/knockdown during the
  active rage window.

## Slow motion

`TimeSlowMoSystem` owns rage slow motion; `TimeSlowMoActivateClipSystem`
plays the activation clip. Slow-mo visual layers:
`SlowMoVisualEffectsSystem`, `SlowMoBoneGlowSystem`.

Interaction rule: when H-scene start zoom is enabled, grab slow-mo defers to
`HSceneStartZoomEffect` (see `PRESENTATION.md`) to avoid fighting over
`Time.timeScale`. Timescale must always be restored through the escape
cleanup path (`TimeScaleResetOnEscapePatch`).

## Presentation

- `RageUISystem` — PNG banner, rage bar, tier sparks (assets from the
  external `Rage/` asset tree).
- `RageVisualEffectsSystem`, `RageFireGradientEffect`, `RageWingsSystem`,
  `RageHandsGlowSystem`, `RageHandsParticleSystem`,
  `RageComboUISystem`, `RageComboBloodEffect`.
- The rage overlay canvas also hosts the grab-chance label
  (`GrabChanceRageUILabel`, used by GrabSystem).

## Persistence

`RageMindBrokenSlotStore` stores rage and MindBroken state per save slot;
`RageMindBrokenPersistenceHooks` binds it to the game's save/load. MindBroken
shares this store deliberately — both values travel together with the slot.
