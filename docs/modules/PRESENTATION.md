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
  (`[HSceneBlackBackground]`). MindBroken tick while active uses
  `MindBrokenRealtimeGate` (see `MIND_BROKEN.md`).
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
  `SplashScreenUILabels`, `HellGateTitleMenuBackdrop`,
  `HellGateSplashOptionsMenu`.
- **Splash Options** — red **Options** label above Start opens a compact panel
  (Start hidden): Gore Content checkbox, Simple QTE checkbox, Easy / Medium /
  Hard preset buttons (copies `NoREroMod.cfg` + `NoREroMod_HellGate.cfg` from
  `BepInEx/config/{EASY|MEDIUM|HARD}/`; restart required for balance), bottom
  row Done / Language / Exit. Language opens a centered 2-column submenu (Cancel
  back; pick other locale → save `HellGateLanguage` + Quit). Selection
  sidecar: `HellGateDifficulty.selection`. Guide:
  `BepInEx/config/DIFFICULTY_PRESETS_GUIDE.txt`. Full reference:
  [SPLASH_OPTIONS_AND_DIFFICULTY.md](SPLASH_OPTIONS_AND_DIFFICULTY.md).
- **Gore Content** — `General.EnableGoreContent` (default on), toggled from
  Options. Gates DeadArmor death PNG clips, CustomDeath lethal traps, and
  EnemyFatality combat fatalities; armored grab-throw stays on
  `[DeadArmor] ArmoredGrabThrowEnable`. See
  [DEAD_ARMOR.md](DEAD_ARMOR.md) / [HELL_TRAPS.md](HELL_TRAPS.md) /
  [ENEMY_FATALITY.md](ENEMY_FATALITY.md).
- **Simple QTE** — `QTEFreeStruggle.Enable` (default off), splash Options;
  live Free Struggle mode (no restart). Struggle hints use a compact 36px
  d-pad cross; default QTE keeps the 56px row. See
  [QTE_STRUGGLE_AND_GAMEPLAY.md](QTE_STRUGGLE_AND_GAMEPLAY.md).
- Splash **CREDITS** — two-column IMGUI list in `LoadingScreenSystem`
  (`BuildCreditsList`); names/roles only (no credit logos).
- **Boot tips + tutorial guide** — early-boot loading tips and illustrated
  video guide before splash Start unlock. Locale
  `{LANG}/BootLoadingTips.json`; media under
  `sources/HellGate_sources/Tutorial Guide source/`. Full reference:
  [BOOT_TIPS_AND_GUIDE.md](BOOT_TIPS_AND_GUIDE.md).
- Portrait: `PortraitModSystem` + `PortraitAssetLoader` +
  `PortraitStateResolver` replace the vanilla `UIface` Spine portrait with
  PNG cycles. State priority: Sex → Rage/NakedRage → Brainwash →
  Normal/NakedNormal.
  Config `[PortraitMod]`; frames from the external `Portrait_mod/` tree.

## Audio

Code: `Systems/Audio/` · Config: `[AttackSounds]`, `[GrabThreats]`

`AttackSoundSystem` + `AttackSoundRegistry` play regular/power attack sounds,
per-language threat lines, and death WAVs from the external `AttackSounds/`
tree via `AttackSoundPatch` / `DeathSoundPatch`.
