# QTE, Struggle, and Gameplay Systems

Player-facing combat mechanics owned by `Systems/Gameplay/`.

## QTE 3.0

HellGate's own QTE implementation, replacing the NoREroMod legacy path.

- `QTESystem` — session lifecycle, button pooling, per-session enemy
  resolution.
- `QTESPCalculator` — SP reward/penalty math.
- `QTEStruggleWindowManager` — window timing between struggle phases.
- Config: `[QTE]`; localized reactions come from `{LANG}/QTEReactionData.json`
  through `QTEReactionFramework` / `QTEReactionDatabase` (see
  `PRESENTATION.md`).

**Ownership rule:** the NoREroMod QTE/struggle path stays disabled by
`QTEStruggleSystemDisabler`, `QTEStruggleHistoryDisabler`, and
`StruggleCameraShakeDisabler`. Do not re-enable both paths simultaneously.

## Struggle UX

- `StruggleVisualIndicators` — on-screen struggle feedback
  (`[VisualIndicators]`).
- Difficulty and pleasure/MindBroken interaction are tuned via
  `[StruggleDifficulty]`, `[Ero]`, `[PleasureStatus]`.
- Escape recovery invariants live in `Patches/Player/` — see
  `COMPATIBILITY.md` before touching escape flow.

## VengeanceStrike

Parry-stab presentation package (`Systems/Gameplay/VengeanceStrike/`):
runtime + content/paths (PNG/WAV from the external
`VengeanceStrike/` asset tree), hand-glow patch, stab presentation and sound
patches, a no-grab-during-stab guard, and a player-update patch. Config
`[VengeanceStrike]` covers slow-mo, hand glow, rage cost, and spine boost.

## WeaponAnimations

Extended weapon combos (`Systems/Gameplay/WeaponAnimations/`):

- witch greatsword: `WitchFineGreatswordPatch`,
  `WitchExtendedGroundSwordComboPatch`, `WitchGreatswordComboSequences`;
- light one-hand sword: `LightOneHand3HitExtendedComboEquipPatch`;
- combo profiles under `Profiles/`; shared mechanics in
  `WeaponAnimationMechanics`. Config: `[WeaponAnimations]`.

## AirGuard

`AirGuardPatch` enables guarding while airborne (`[AirGuard]`).

## Misc

- `EnemyConstantVisibilityPatch` — keeps enemies rendered when required.
- `PlayerEroContextUtility` — shared player ERO state queries used by several
  modules.
