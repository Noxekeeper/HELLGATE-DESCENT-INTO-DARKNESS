# Changelog

Notable changes per release. The plugin version lives in
`Core/PluginInfo.cs` (`PLUGIN_VERSION`); the public API is versioned
separately (see [`docs/development/API.md`](docs/development/API.md)).

Versions 1.2.2–1.2.3 were internal iterations folded into 1.2.4.

## Unreleased

- Public read-only API `0.1.0` (`NoREroMod.HellGate.Api.HellGateApi`):
  snapshots for Rage, MindBroken, faction reputation, Gold, Pregnancy, plus
  lifecycle/state-change events.
- Source released under GPL-3.0 with a full developer documentation tree
  (`ARCHITECTURE.md`, `docs/modules/`, `docs/development/`).

## 1.2.4 — 2026-07

### Added

- **Pregnancy and offspring**: HellGate's own pregnancy system
  (`Pregnancy.Enable`, independent from the NoREroMod one). Womb meter fills
  during H-scenes; conception leads to trimesters with debuffs; birth
  produces a child with an archetype from the father's faction. Children
  live in the ParishChurch hideout, grant bloodline bonuses, and fight as
  allies, growing through stages 0–3 after successful shelter defenses.
- **Shelter Attack**: with living children in the hideout, leaving
  ParishChurch can trigger a raid — return timer, enemy waves, win/lose
  consequences. Raid UI localized to all 10 languages.
- Take Vengeance / death clears active pregnancy and semen (hideout children
  persist); the altar offers optional pregnancy/womb reset.

### Changed

- Grab-via-attack no longer triggers during dash, parry, or post-hit
  invulnerability frames; block grants full grab immunity by default
  (`GrabBlockImmunity`); zero base grab chances are no longer overridden by
  MindBroken/HP/Pleasure bonuses.
- H-scene Start Zoom toned down; manual zoom cycle is 1.5 → 3 → 5. Fatality
  camera skip limited to the RequiemKnight family.

### Fixed

- Struggle no longer allows an early escape at ~50% SP.
- Kinoko H-scene no longer freezes at START6 → ERO; FIN fills the womb
  meter.
- TouzokuAxe womb fill happens on climax rather than at FIN start.
- MummyMan: anal scenes no longer fill the womb; handoff chains hand over on
  JIGO with the previous enemy hidden until stand-up.
- Korean localization: three H-dialogue files that contained Chinese text
  replaced with proper Korean.

## 1.2.1 — 2026-06/07 (hotfix series)

### Added

- Centralized UI font provider (`HellGateFontProvider`) with a `[Fonts]`
  config section: Western family plus optional Asian override with sensible
  per-language Windows defaults.

### Fixed

- QTE `ButtonPositionX`/`ButtonPositionY` cfg values now actually move the
  button row (previously hardcoded).
- Faction provocation by player magic (`PlayerProvocationFromMagic`):
  projectile hits landing after the cast animation are attributed correctly
  via projectile ownership.
- Potion escape (`allowPotionEasyEscape`) works in H-scenes through a thin
  NoREroMod compatibility patch; vanilla `Item_use` is blocked during
  struggle so the wrong potion is not consumed.
- Death gold pile spawns immediately at the death position instead of on
  scene reload.
- Inter-faction melee range taken from `Factions.json` instead of inflated
  boss attack distances.

## 1.2 — 2026-06

- Factions: peace threshold raised to +65%; Mercy window 7 s with a late
  penalty from 4 s.
- Economy: combat gold loss, knockdown loss, death drop with a recoverable
  lost pile; gold HUD moved bottom-left.
- EventCore: broker gate encounter in VillageMain; walk-transition respawn
  fix; refusal ambush composition shuffle.
- Spawn: `RANDOM` and `DECOR` line support, sort/depth control, Scapegoat ↔
  VillageMain zone-transition fix.
- BossTouzokuCustom field boss with QTE/struggle integration.
- Threat/sound suppression for peaceful EventCore NPCs; trap H-scene mosaic
  patch; Manifesto refreshed in 10 languages.

## 1.1 — initial public release

First public release: handoff chains, JSON-editable dialogues, data-driven
spawn packs, QTE and MindBroken systems, three-tier Rage with slow motion,
combat camera, Witch greatsword animations, grab system, Bad End Player,
aggressive AI, and audio/immersion content.
