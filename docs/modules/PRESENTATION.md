# Presentation: Dialogue, Camera, UI, Audio, Effects

Presentation-layer subsystems. Individually small; grouped here because they
share loaders, fonts, and the HUD visibility contract.

## Dialogue

Code: `Systems/Dialogue/` · Data: `{LANG}/` dialogue JSON

- Framework: `DialogueFramework`, `DialoguePool`, `DialogueDisplay` (bubble
  rendering), `DialogueSelector`, `DialogueEventProcessor`,
  `DialogueDatabase`, `ColorParser`, base class `DialogueSystemBase`.
- Content sets: per-enemy H-scene dialogues (Touzoku normal/axe, Kakasi,
  Goblin, InquisitionBlack, Aradia variants), `GrabThreatDialogues` +
  `GrabThreatIdlePatch`, `SpectatorCommentsSystem`,
  `BiscordDamageDialogues`.
- QTE reactions: `QTEReactionFramework` + `QTEReactionDatabase` from
  `{LANG}/QTEReactionData.json`.
- Sound glue: `SoundRegistry`, `SoundOnomatopoeiaPatch`
  (`[SoundOnomatopoeia]`).
- Gate: combat threat lines are suppressed while an EventCore modal host is
  active and non-hostile.

## Camera and H-scene effects

Code: `Systems/Camera/`, `Systems/HSceneEffects/`

- `HSceneCameraController` + patch set (direct pan, mid-point, move override,
  smoothing disable, zoom control, reset prevention, arrow-key block).
- `CombatCameraPresetSystem` (`[CombatCamera]`).
- `HSceneStartZoomEffect` — center + zoom + slow-mo at H-scene start, driven
  from `PlayerConUpdateDispatcher` (`[HSceneEffects]` → `StartZoom.*`). Grab
  slow-mo defers to this effect when enabled.
- `CumDisplayManager` (`[CumDisplay]`) — X-ray / pregnancy clip slots.

## Effects and bad ends

- `HSceneBlackBackgroundSystem` + trigger patch — black backdrop on FIN
  detection, with BigoniBrother/Mutude special cases
  (`[HSceneBlackBackground]`).
- `Systems/BadEndPlayer/` — manifest-driven bad-end playback
  (`[BadEndPlayer]`), audio from the external `BadEndPlayer/` tree.
- `Patches/Effects/PregnancyClipTrigger` — pregnancy clip FX.
- `Patches/Trap/TrapHSceneMosaicDisablePatch` — trap H-scene mosaic disable
  with an extensible owner list.

## UI

Code: `Systems/UI/`

- `HellGateFontProvider` — **all** HellGate `UnityEngine.UI.Text` surfaces
  route fonts through this provider. Config `[Fonts]`
  (`FontFamilyWestern` / `FontFamilyAsian`, per-locale Windows defaults) and
  `[DialogueFonts]`. Never assign fonts directly.
- `HudVisibilityGate` — custom HUDs must follow vanilla canvas visibility,
  using `CanvasGroup.alpha` rather than `SetActive` (so bootstraps and
  coroutines survive).
- `LoadingScreenSystem` (custom art, sponsor labels, locale filters),
  `SplashScreenUILabels`, `HellGateTitleMenuBackdrop`.
- Portrait: `PortraitModSystem` + `PortraitAssetLoader` +
  `PortraitStateResolver` replace the vanilla `UIface` Spine portrait with
  PNG cycles. State priority: Sex → Rage → Brainwash → Normal.
  Config `[PortraitMod]`; frames from the external `Portrait_mod/` tree.

## Audio

Code: `Systems/Audio/` · Config: `[AttackSounds]`, `[GrabThreats]`

`AttackSoundSystem` + `AttackSoundRegistry` play regular/power attack sounds,
per-language threat lines, and death WAVs from the external `AttackSounds/`
tree via `AttackSoundPatch` / `DeathSoundPatch`.
