# Configuration Reference

Complete reference of `BepInEx/config/NoREroMod_HellGate.cfg`.

This file is **generated** from the live cfg by
`dev/tools/generate_configuration_md.py` (maintainer tooling). Do not edit the
tables by hand: change `SetUpConfigs()` (or the owning module), run the game
once so BepInEx regenerates the cfg, regenerate this document, and commit both
together.

Notes:

- The cfg holds feature gates and tuning. Content balance lives in JSON under
  `HellGateJson/` and is documented in [DATA_FORMATS.md](DATA_FORMATS.md).
- Some modules are gated by JSON instead of cfg (Economy, Diagnostics).
- Values are read once at startup unless the owning module documents
  hot-reload behavior.

Sections: **65** · Settings: **477**

## Section index

- [`[AirGuard]`](#airguard) — 1 settings
- [`[AttackSounds]`](#attacksounds) — 10 settings
- [`[BadEndPlayer]`](#badendplayer) — 1 settings
- [`[BigoniBrother]`](#bigonibrother) — 2 settings
- [`[ButcherMod]`](#butchermod) — 1 settings
- [`[Combat]`](#combat) — 6 settings
- [`[CombatCamera]`](#combatcamera) — 3 settings
- [`[CorruptionCaptions]`](#corruptioncaptions) — 2 settings
- [`[CrowInquisitionMindBroken]`](#crowinquisitionmindbroken) — 2 settings
- [`[CumDisplay]`](#cumdisplay) — 8 settings
- [`[DialogueEventProcessor]`](#dialogueeventprocessor) — 1 settings
- [`[DialogueFonts]`](#dialoguefonts) — 16 settings
- [`[DoreiMod]`](#doreimod) — 2 settings
- [`[EnemyPass]`](#enemypass) — 7 settings
- [`[Ero]`](#ero) — 28 settings
- [`[EventCore]`](#eventcore) — 10 settings
- [`[FieldOfView]`](#fieldofview) — 3 settings
- [`[Fonts]`](#fonts) — 4 settings
- [`[General]`](#general) — 2 settings
- [`[GoblinHardcore]`](#goblinhardcore) — 1 settings
- [`[GrabSystemNG]`](#grabsystemng) — 14 settings
- [`[GrabThreats]`](#grabthreats) — 2 settings
- [`[HandoffSystem]`](#handoffsystem) — 3 settings
- [`[Hardcore]`](#hardcore) — 1 settings
- [`[HellishTouzoku]`](#hellishtouzoku) — 2 settings
- [`[HellTraps]`](#helltraps) — 10 settings
- [`[HSceneBlackBackground]`](#hsceneblackbackground) — 2 settings
- [`[HSceneCameraZoom]`](#hscenecamerazoom) — 7 settings
- [`[HSceneEffects]`](#hsceneeffects) — 10 settings
- [`[InquisitionWhiteMindBroken]`](#inquisitionwhitemindbroken) — 2 settings
- [`[MindBroken]`](#mindbroken) — 12 settings
- [`[MindBrokenRecovery]`](#mindbrokenrecovery) — 5 settings
- [`[MindBrokenVisualEffects]`](#mindbrokenvisualeffects) — 26 settings
- [`[MutudeMindBroken]`](#mutudemindbroken) — 1 settings
- [`[PilgrimMindBroken]`](#pilgrimmindbroken) — 1 settings
- [`[PlayerVisualFixes]`](#playervisualfixes) — 2 settings
- [`[PleasureStatus]`](#pleasurestatus) — 18 settings
- [`[PortraitMod]`](#portraitmod) — 6 settings
- [`[Pregnancy]`](#pregnancy) — 8 settings
- [`[Pregnancy.Altar]`](#pregnancyaltar) — 2 settings
- [`[Pregnancy.Blocking]`](#pregnancyblocking) — 7 settings
- [`[Pregnancy.Bloodline]`](#pregnancybloodline) — 25 settings
- [`[Pregnancy.OffspringArchetype]`](#pregnancyoffspringarchetype) — 2 settings
- [`[Pregnancy.OffspringCombat]`](#pregnancyoffspringcombat) — 3 settings
- [`[Pregnancy.Physics]`](#pregnancyphysics) — 3 settings
- [`[Pregnancy.SemenValue]`](#pregnancysemenvalue) — 4 settings
- [`[Pregnancy.ShelterAttack]`](#pregnancyshelterattack) — 15 settings
- [`[Pregnancy.Trimester]`](#pregnancytrimester) — 3 settings
- [`[Pregnancy.TrimesterModifiers]`](#pregnancytrimestermodifiers) — 2 settings
- [`[Pregnancy.TrimesterVisuals]`](#pregnancytrimestervisuals) — 9 settings
- [`[QTE]`](#qte) — 26 settings
- [`[RageMode]`](#ragemode) — 40 settings
- [`[RageVisualEffects]`](#ragevisualeffects) — 22 settings
- [`[RickEnemyMod]`](#rickenemymod) — 1 settings
- [`[SavePoints]`](#savepoints) — 2 settings
- [`[SlowMoVisualEffects]`](#slowmovisualeffects) — 10 settings
- [`[SoundOnomatopoeia]`](#soundonomatopoeia) — 1 settings
- [`[SpawnTemplates]`](#spawntemplates) — 8 settings
- [`[StruggleDifficulty]`](#struggledifficulty) — 2 settings
- [`[TakeVengeance]`](#takevengeance) — 9 settings
- [`[TouzokuAggression]`](#touzokuaggression) — 2 settings
- [`[VengeanceStrike]`](#vengeancestrike) — 29 settings
- [`[VisualIndicators]`](#visualindicators) — 5 settings
- [`[WeaponAnimations]`](#weaponanimations) — 2 settings
- [`[WolfMod]`](#wolfmod) — 1 settings

## AirGuard

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enable` | Boolean | `true` | Block (Guard) while airborne. |

## AttackSounds

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enable` | Boolean | `true` | Enable custom attack sounds from sources/HellGate_sources/AttackSounds |
| `Volume` | Single | `0.85` | Global volume for custom attack sounds (0.0 - 1.0) |
| `EnableThreatSounds` | Boolean | `false` | Play threat sounds from AttackSounds/Human/Threats<LANG> (e.g. ThreatsEN) when human enemies are 4-6 units away (same flow as dialogue threats) |
| `ThreatSoundsVolume` | Single | `0.9` | Volume for threat sounds (0.0 - 1.0) |
| `ThreatSoundsGlobalCooldown` | Single | `5` | Minimum seconds between ANY threat sounds. Should match threatDisplayDuration for text/sound sync. |
| `ThreatSoundsPerEnemyCooldown` | Single | `10` | Seconds before the same enemy can play another threat sound. |
| `EnableDeathSounds` | Boolean | `true` | Play death sounds from AttackSounds/Human/Death when human enemies die (DEATH animation) |
| `DeathSoundsVolume` | Single | `1` | Volume for death sounds (0.0 - 1.0) |
| `AttackSoundsGlobalInterval` | Single | `0.12` | Minimum seconds between attack sounds globally (reduces spam when fighting many enemies). |
| `AttackSoundsPerAttackerInterval` | Single | `0.2` | Minimum seconds before same attacker can play another attack sound. |

## BadEndPlayer

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enable` | Boolean | `true` | Enable BadEnd Player module. When true, BadEnd (MindBroken 100% + timer) shows the image player instead of YOU LOSE + epilogue. Content from sources/HellGate_sources/BadEndPlayer_Proto. |

## BigoniBrother

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Start2RepeatCount` | Int32 | `3` | Number of times START2 animation should play before transitioning to START3 (default: 3) |
| `Start2TimeScale` | Single | `1` | Time scale for START2 animation (1.0 = normal speed, 2.0 = 2x speed, default: 1.0) |

## ButcherMod

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `AssetsPath` | String | (empty) | Deprecated — use [RickEnemyMod] AssetsPath. Kept for backward compatibility when RickEnemyMod path is empty. |

## Combat

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `MPGainPerHit` | Single | `3` | Base amount of MP gained per attack with a INT scaling weapon |
| `SPGuardModifier` | Single | `0.5` | SP damage on guard is equal to the HP damage taken after guarding an attack multiplied by this value |
| `DashSPCost` | Single | `40` | SP cost to dash/evade (base game = 20) |
| `SPRegenWhenIdle` | Single | `3` | Number of secs to go from 0% to 100% SP when idle (base game = 2) |
| `SPRegenWhenGuarding` | Single | `10` | Number of secs to go from 0% to 100% SP when guarding (base game = 7.5) |
| `HiddenEnemyHPBars` | Boolean | `true` | Hides HP bars for non-boss enemies |

## CombatCamera

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `EnableCombatCameraPresets` | Boolean | `true` | Enable V key to toggle between standard and far zoom during combat (outside H-scenes) |
| `FarZoom` | Single | `1.4` | Far zoom multiplier (1st V press). Camera zooms out by this factor. Values <= 1.1 are clamped to 1.4. |
| `UltraFarZoom` | Single | `1.8` | Ultra-far zoom multiplier (2nd V press). Camera zooms out even further. Values <= 1.1 are clamped to 1.8. |

## CorruptionCaptions

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enable` | Boolean | `true` | Enable corruption caption system - red text messages when MindBroken increases |
| `CaptionCooldown` | Single | `1.5` | Cooldown between captions in seconds (1.5 = 1.5 sec) |

## CrowInquisitionMindBroken

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `MindBrokenPerSecondIKI` | Single | `6` | MindBroken percentage added per second during IKI animation (time-stop orgasm sequence) (default: 6 = 6%/sec) |
| `MindBrokenPerSecondIKI2` | Single | `3` | MindBroken percentage added per second during IKI2 animation (time-stop orgasm sequence) (default: 3 = 3%/sec) |

## CumDisplay

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `FrameDuration` | Single | `0.04` | X-ray banner frame duration in seconds (1/25 = ~25 FPS) |
| `AnchoredOffsetX` | Single | `450` | X-ray banner X offset from screen center in pixels (right) |
| `AnchoredOffsetY` | Single | `100` | X-ray banner Y offset from screen center in pixels (up) |
| `OralOffsetYDelta` | Single | `-140` | Additional Y offset for oral clips (negative = down) |
| `PregnantOffsetX` | Single | `0.25` | Pregnancy banner X offset in normalized viewport coordinates (0.25 = right from center) |
| `PregnantOffsetY` | Single | `0` | Pregnancy banner Y offset in normalized viewport coordinates |
| `WorldDepth` | Single | `3` | Distance from camera for WorldSpace banner rendering |
| `SizeMultiplier` | Single | `2.5` | Banner size multiplier (2.5 = 2.5x increase) |

## DialogueEventProcessor

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `MinCooldown` | Single | `0.1` | Minimum cooldown in seconds between dialogue event processing |

## DialogueFonts

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `FontSize` | Single | `22` | Font size for all dialogue systems (22 = standard size) |
| `EnemyFontStyle` | Int32 | `1` | Font style for enemy comments (0 = Normal, 1 = Bold, 2 = Italic, 3 = BoldAndItalic) |
| `AradiaResponseFontStyle` | Int32 | `0` | Font style for Aradia responses (0 = Normal, 1 = Bold, 2 = Italic, 3 = BoldAndItalic) |
| `AradiaThoughtFontStyle` | Int32 | `0` | Font style for Aradia thoughts (0 = Normal, 1 = Bold, 2 = Italic, 3 = BoldAndItalic) |
| `SpectatorFontStyle` | Int32 | `0` | Font style for spectator comments (0 = Normal, 1 = Bold, 2 = Italic, 3 = BoldAndItalic) |
| `ThreatFontStyle` | Int32 | `1` | Font style for grab threats (0 = Normal, 1 = Bold, 2 = Italic, 3 = BoldAndItalic) |
| `EnemyColor` | String | `1.0,1.0,1.0,1.0` | Text color for enemies (R, G, B, A - values 0-1) |
| `EnemyOutlineColor` | String | `0.0,0.0,0.0,1.0` | Outline color for enemies (R, G, B, A - values 0-1) |
| `AradiaResponseColor` | String | `0.8,0.4,1.0,1.0` | Text color for Aradia responses (R, G, B, A - values 0-1) |
| `AradiaResponseOutlineColor` | String | `1.0,1.0,1.0,1.0` | Outline color for Aradia responses (R, G, B, A - values 0-1) |
| `AradiaThoughtColor` | String | `0.9176,0.8902,0.8235,1.0` | Text color for Aradia thoughts — dusty white #EAE3D2 (R, G, B, A values 0-1) |
| `AradiaThoughtOutlineColor` | String | `0.0,0.0,0.0,1.0` | Outline color for Aradia thoughts (R, G, B, A - values 0-1) |
| `SpectatorColor` | String | `1.0,1.0,1.0,1.0` | Text color for spectators (R, G, B, A - values 0-1) |
| `SpectatorOutlineColor` | String | `0.0,0.0,0.0,1.0` | Outline color for spectators (R, G, B, A - values 0-1) |
| `ThreatColor` | String | `1.0,0.0,0.0,1.0` | Text color for threats (R, G, B, A - values 0-1). Default: red |
| `ThreatOutlineColor` | String | `0.0,0.0,0.0,1.0` | Outline color for threats (R, G, B, A - values 0-1) |

## DoreiMod

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `FappingAssetsPath` | String | (empty) | Path to DoreiFapping folder (relative to game root). Empty = use default: sources/HellGate_sources/DoreiFapping. Dorei plays fapping IDLE while waiting in H-scene. |
| `SpectatorScaleMultiplier` | Single | `1` | Scale multiplier for Dorei fapping spectator skeleton. 1.0 = same as original. If fapping looks larger, try 0.85-0.95. |

## EnemyPass

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `EnableEnemyPassMechanic` | Boolean | `true` | Enable enemy pass mechanic - player will be passed between enemies after several animation cycles |
| `CyclesBeforePass` | Int32 | `2` | Number of animation cycles before pushback (1-5) |
| `PushDistance` | Single | `2` | Pushback distance for player to the side (1.0-5.0) |
| `MinCycleInterval` | Single | `2` | Minimum interval between cycles in seconds (0.5-5.0) |
| `HandoffDelay` | Single | `3` | Delay before player handoff in seconds (1.0-10.0). Higher = slower handoff. |
| `EnableDirtyTalkMessages` | Boolean | `true` | Enable dirty talk during H-scenes |
| `EnableHandoffMessages` | Boolean | `true` | Enable messages when player is passed between enemies |

## Ero

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `HPLosePerSec` | Single | `0` | Amount HP lose per sec during ero |
| `HPLosePerCreampie` | Single | `5` | Amount HP lose per creampie or other orgasm (most enemies creampie multiple times per animation) |
| `EnableDeleveling` | Boolean | `true` | Enables or disables going down a level if exp would drain below zero |
| `EXPLosePerSec` | Single | `0.01` | Percentage of exp to next level to lose per sec during ero (0-1) |
| `EXPLosePerCreampie` | Single | `0.15` | Percentage of exp to next level to lose per creampie or other orgasm (0-1) (most enemies creampie multiple times per animation) |
| `EXPLoseOnAnimationEventMultiplier` | Single | `1` | Exp lose caused by certain ero animations will be multiplied by this value |
| `DelevelEXPRefundPercentage` | Single | `1` | Percentage of exp to refund back to the exp pool due to deleveling (0-1) |
| `SPRegenMax` | Single | `-30` | Number of secs to go from 0% to 100% SP when downed at max pleasure |
| `SPRegenMin` | Single | `-60` | Number of secs to go from 0% to 100% SP when downed at zero pleasure |
| `SPLoseOnEroEvent` | Single | `0.5` | Current SP is multiplied by this value after penetration, player orgasm, or creampies. 1 = no loss, 0.5 = lose half, 0 = full reset (0-1) |
| `SPGainOnStruggleDowned` | Single | `0.025` | Percentage of max SP gained back on struggle while downed (downed but not yet in ero animation) (0-1) |
| `SPGainOnStruggleEro` | Single | `0.025` | Percentage of max SP gained back on struggle (during ero animation) (0-1) |
| `SPLoseOnBadStruggleEro` | Single | `0.12` | Percentage of max SP lose when struggling outside of the allowed time (during ero animation) (0-1) |
| `AnimationHPDamageMultiplier` | Single | `1` | HP damage caused by certain ero animations will be multiplied by this value |
| `AnimationPleasureBuildupMultiplier` | Single | `1` | Pleasure buildup caused by certain ero animations will be multiplied by this value |
| `easyStruggleCount` | Single | `4` | Enables easier struggles for a set number of struggles |
| `fatalityDifficulty` | Single | `0.4` | How difficult it is to struggle out of a fatal animation (0-1) |
| `fatalityEasyStruggles` | Boolean | `false` | Enable easy struggles to work on fatality animations |
| `bossStruggleFatigue` | Boolean | `true` | Enable struggling to get harder per escape during boss fights |
| `bossEasyStruggles` | Boolean | `false` | Enable easy struggles to work during boss fights |
| `enemyHealthEffectiveness` | Single | `0.5` | How much non-boss enemy max Hp effects struggle difficulty (0-1) |
| `playerHealthEffectiveness` | Single | `0.5` | How strongly health effects struggle difficulty (0-1) (0=Disabled) |
| `SpFactorEffectiveness` | Single | `0.5` | How strongly Max Sp eases struggle difficulty (0-1) (0=Disabled) |
| `playerMpEffectiveness` | Single | `0` | How strongly mp effects struggle difficulty (0-1) (0=Disabled) |
| `playerPleasureEffectiveness` | Single | `1.5` | How strongly pleasure effects struggle difficulty (0-1) (0=Disabled) |
| `enableCriticalStruggle` | Boolean | `false` | enables a certain chance to double your sp gain each time you struggle, but you could also lose that amount of progress (chances are based on your Aradia's Luck) Let's go gambling! |
| `allowPotionEasyEscape` | Boolean | `false` | Allows use of a potion to escape any struggle instantly |
| `enableImpossibleStruggles` | Boolean | `true` | Enable to make some struggles impossible based on the animation (When disabled, struggles will simply be harder instead of impossible) |

## EventCore

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enable` | Boolean | `true` | Enable EventCore (modal dialogues / branches; spawn lines use \|ec_event=). HellGateJson/EventCore content is inactive when false. |
| `DevHotkey` | KeyCode | `F9` | In-game: open DevEventId modal when EventCore is enabled Range: None, Backspace, Tab, Clear, Return, Pause, Escape, Space, Exclaim, DoubleQuote, Hash, Dollar, Ampersand, Quote, LeftParen, RightParen, Asterisk, Plus, Comma, Minus, Period, Slash, Alpha0, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9, Colon, Semicolon, Less, Equals, Greater, Question, At, LeftBracket, Backslash, RightBracket, Caret, Underscore, BackQuote, A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z, Delete, Keypad0, Keypad1, Keypad2, Keypad3, Keypad4, Keypad5, Keypad6, Keypad7, Keypad8, Keypad9, KeypadPeriod, KeypadDivide, KeypadMultiply, KeypadMinus, KeypadPlus, KeypadEnter, KeypadEquals, UpArrow, DownArrow, RightArrow, LeftArrow, Insert, Home, End, PageUp, PageDown, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, F13, F14, F15, Numlock, CapsLock, ScrollLock, RightShift, LeftShift, RightControl, LeftControl, RightAlt, LeftAlt, RightApple, RightCommand, LeftCommand, LeftApple, LeftWindows, RightWindows, AltGr, Help, Print, SysReq, Break, Menu, Mouse0, Mouse1, Mouse2, Mouse3, Mouse4, Mouse5, Mouse6, JoystickButton0, JoystickButton1, JoystickButton2, JoystickButton3, JoystickButton4, JoystickButton5, JoystickButton6, JoystickButton7, JoystickButton8, JoystickButton9, JoystickButton10, JoystickButton11, JoystickButton12, JoystickButton13, JoystickButton14, JoystickButton15, JoystickButton16, JoystickButton17, JoystickButton18, JoystickButton19, Joystick1Button0, Joystick1Button1, Joystick1Button2, Joystick1Button3, Joystick1Button4, Joystick1Button5, Joystick1Button6, Joystick1Button7, Joystick1Button8, Joystick1Button9, Joystick1Button10, Joystick1Button11, Joystick1Button12, Joystick1Button13, Joystick1Button14, Joystick1Button15, Joystick1Button16, Joystick1Button17, Joystick1Button18, Joystick1Button19, Joystick2Button0, Joystick2Button1, Joystick2Button2, Joystick2Button3, Joystick2Button4, Joystick2Button5, Joystick2Button6, Joystick2Button7, Joystick2Button8, Joystick2Button9, Joystick2Button10, Joystick2Button11, Joystick2Button12, Joystick2Button13, Joystick2Button14, Joystick2Button15, Joystick2Button16, Joystick2Button17, Joystick2Button18, Joystick2Button19, Joystick3Button0, Joystick3Button1, Joystick3Button2, Joystick3Button3, Joystick3Button4, Joystick3Button5, Joystick3Button6, Joystick3Button7, Joystick3Button8, Joystick3Button9, Joystick3Button10, Joystick3Button11, Joystick3Button12, Joystick3Button13, Joystick3Button14, Joystick3Button15, Joystick3Button16, Joystick3Button17, Joystick3Button18, Joystick3Button19, Joystick4Button0, Joystick4Button1, Joystick4Button2, Joystick4Button3, Joystick4Button4, Joystick4Button5, Joystick4Button6, Joystick4Button7, Joystick4Button8, Joystick4Button9, Joystick4Button10, Joystick4Button11, Joystick4Button12, Joystick4Button13, Joystick4Button14, Joystick4Button15, Joystick4Button16, Joystick4Button17, Joystick4Button18, Joystick4Button19, Joystick5Button0, Joystick5Button1, Joystick5Button2, Joystick5Button3, Joystick5Button4, Joystick5Button5, Joystick5Button6, Joystick5Button7, Joystick5Button8, Joystick5Button9, Joystick5Button10, Joystick5Button11, Joystick5Button12, Joystick5Button13, Joystick5Button14, Joystick5Button15, Joystick5Button16, Joystick5Button17, Joystick5Button18, Joystick5Button19, Joystick6Button0, Joystick6Button1, Joystick6Button2, Joystick6Button3, Joystick6Button4, Joystick6Button5, Joystick6Button6, Joystick6Button7, Joystick6Button8, Joystick6Button9, Joystick6Button10, Joystick6Button11, Joystick6Button12, Joystick6Button13, Joystick6Button14, Joystick6Button15, Joystick6Button16, Joystick6Button17, Joystick6Button18, Joystick6Button19, Joystick7Button0, Joystick7Button1, Joystick7Button2, Joystick7Button3, Joystick7Button4, Joystick7Button5, Joystick7Button6, Joystick7Button7, Joystick7Button8, Joystick7Button9, Joystick7Button10, Joystick7Button11, Joystick7Button12, Joystick7Button13, Joystick7Button14, Joystick7Button15, Joystick7Button16, Joystick7Button17, Joystick7Button18, Joystick7Button19, Joystick8Button0, Joystick8Button1, Joystick8Button2, Joystick8Button3, Joystick8Button4, Joystick8Button5, Joystick8Button6, Joystick8Button7, Joystick8Button8, Joystick8Button9, Joystick8Button10, Joystick8Button11, Joystick8Button12, Joystick8Button13, Joystick8Button14, Joystick8Button15, Joystick8Button16, Joystick8Button17, Joystick8Button18, Joystick8Button19. |
| `DevEventId` | String | `eventcore_broker_gate` | Event id loaded from eventcore_manifest.json (e.g. eventcore_broker_gate, eventcore_smoke_test) |
| `ModalDimAlpha` | Single | `0` | Darkening behind the text/button panel only (not across the full decorative PNG width). 0 = off; higher values add subtle dimming under the UI. Range: From 0 to 1. |
| `HideVanillaHudDuringModal` | Boolean | `true` | While the EventCore modal is open, disable the vanilla gameplay HUD (root Canvas). When false, the HUD stays visible under the modal. |
| `BrokerPortraitAradiaScale` | Single | `1` | Display scale for Aradia (left) broker portraits. Lower if she looks larger than Touzoku despite smaller PNG width. Range: From 0.25 to 2. |
| `BrokerPortraitTouzokuScale` | Single | `1` | Display scale for Touzoku (right) broker portraits. Raise if hood/mask art looks too small in the frame. Range: From 0.25 to 2. |
| `AmbientSpikeEncountersEnable` | Boolean | `true` | Deprecated: use EventTrapEncountersEnable. This entry is read once to seed the new key if your cfg still only has the old name. |
| `EventTrapEncountersEnable` | Boolean | `true` | EventTrap: non-modal coordinate-zone suspicion + knockdown ambush. Requires event_trap_registry.json (preferred) or legacy ambient_spike_registry.json under HellGateJson/EventCore/. System options: EventCore/_shared/<eventFolder>/config.json when present (otherwise per-language EventCore/<Lang>/<eventFolder>/config.json). Phrases: EventCore/<Lang>/<eventFolder>/phrases.json with fallback order En, Ru, Jp, Cn, Kr, Fr, De, Pt, Br, Es. |
| `ReinforcementEncountersEnable` | Boolean | `true` | Reinforcement: knockdown-triggered extra spawns when the player is within triggerRadiusFromAnchor of a REINFORCEMENT,folder,x,y anchor. Optional suspicion lines (phrasesFromEventFolder). Requires HellGateJson/EventCore/reinforcement_registry.json and EventCore/_shared/<folder>/config.json. |

## FieldOfView

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `EnableFieldOfView` | Boolean | `false` | When enabled, enemies behind or too far away from the player fade out (NoREroMod FoV). Default off — all enemies stay fully visible. |
| `FrontViewDistance` | Single | `9` | Vision radius for enemies in front of the player (10 ~= half screen distance) |
| `BackViewDistance` | Single | `2.5` | Vision radius for enemies behind the player (2 ~= touching distance) |

## Fonts

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `FontFileWestern` | String | (empty) | Legacy/reserved. External font files are not loaded by Unity 5.6 at runtime; leave empty and use FontFamilyWestern. |
| `FontFamilyWestern` | String | (empty) | Windows-installed font family for En/Ru/De/Pt/Br/Es/Fr. Recommended: Georgia, Constantia, Cambria, Segoe UI. Empty = automatic fallback. |
| `FontFileAsian` | String | (empty) | Legacy/reserved. External font files are not loaded by Unity 5.6 at runtime; leave empty and use FontFamilyAsian or automatic fallbacks. |
| `FontFamilyAsian` | String | (empty) | Windows-installed font family override for Cn/Jp/Kr. Empty = automatic per-language fallback: Cn=Microsoft YaHei, Jp=Yu Gothic, Kr=Malgun Gothic. |

## General

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `ShowSplashScreenOnStartup` | Boolean | `true` | Show HELLGATE splash screen on game startup. Set to false to skip splash screen. |
| `HellGateLanguage` | String | (empty) | Selected language for HELLGATE mod. Available: RU, EN, JP, CN, KR, FR, DE, PT, BR, ES. Set automatically on first language selection. |

## GoblinHardcore

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `EnableStruggleSpawn` | Boolean | `true` | HARDMODE: When player breaks free from goblin START animation (where 3 goblins appear), spawn 2 additional goblins to maintain consistency. Disable if causing issues. |

## GrabSystemNG

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `EnableGrabViaAttack` | Boolean | `true` | Enable grab on attack hit (melee only, 0% from ranged) |
| `DisableOriginalEliteGrab` | Boolean | `true` | Disable collision-based Elite Grab from NoREroMod |
| `GrabViaAttackEliteOnly` | Boolean | `false` | Grab only from Elite (red) enemies. false = all enemies can grab |
| `GrabBlockImmunity` | Boolean | `true` | When true, guarding fully blocks grab-via-attack. When false, use GrabChanceThroughBlock / GrabChancePowerThroughBlock. |
| `GrabChanceMelee` | Single | `0.1` | Base chance of grab from normal melee attack when NOT blocking (0.10 = 10%). Affected by MindBroken (+), low HP (+), Pleasure (+) and Rage (-) only when base chance > 0. |
| `GrabChancePowerAttack` | Single | `0.15` | Base chance of grab from knockdown/power attack when NOT blocking (0.15 = 15%). Affected by MindBroken (+), low HP (+), Pleasure (+) and Rage (-) only when base chance > 0. |
| `GrabChanceThroughBlock` | Single | `0.05` | When GrabBlockImmunity is false: chance normal melee grabs through block (0.05 = 5%). Modifiers apply only when this base chance > 0. |
| `GrabChancePowerThroughBlock` | Single | `0.1` | When GrabBlockImmunity is false: chance knockdown attack grabs through block (0.10 = 10%). Modifiers apply only when this base chance > 0. |
| `GrabChanceMindBrokenBonusPer10Percent` | Single | `0.02` | Extra grab chance per 10% MindBroken in grab logic (0.02 = +2% per 10%). UI can use a different value. |
| `GrabChanceRageReductionPerPercent` | Single | `0.005` | Grab chance reduction per 1% Rage (0.005 = 0.5% per 1% Rage). At 100% Rage grab chance is halved. |
| `GrabChancePleasureBonusMax` | Single | `0.2` | Maximum additional grab chance from Pleasure gauge (BadstatusVal[0]). 0.20 = +20% at 100 pleasure, scaled linearly. |
| `GrabViaAttackSlowmo` | Boolean | `true` | Slow down time when grab via attack triggers (runs immediately, HScene zoom has no slowmo) |
| `GrabViaAttackSlowmoTimeScale` | Single | `0.3` | Time scale during grab (0.3 = 30% speed, 2 sec) |
| `GrabViaAttackSlowmoDuration` | Single | `2` | Duration of grab slowmo in real seconds |

## GrabThreats

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enable` | Boolean | `true` | Master switch: enable grab threat system (text phrases and/or sounds when enemies are about to grab). When false, disables both text and threat sounds from this system. |
| `EnableThreatText` | Boolean | `true` | Show threat text phrases above enemies. Can be toggled separately from threat sounds (e.g. text only, sound only, or both) |

## HandoffSystem

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `EnableEnemyHandoff` | Boolean | `true` | Enables enemy handoff system - enemies will pass around the player after completing animation cycles |
| `HandoffCooldownTime` | Single | `2` | Time in seconds between handoffs to prevent spam |
| `EnableHandoffVisualEffects` | Boolean | `true` | Shows visual effects during handoffs |

## Hardcore

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `IsHardcoreMode` | Boolean | `false` | CAUTION!!! Deletes ALL save files upon death or bad end scene |

## HellishTouzoku

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `AssetsPath` | String | (empty) | Path to Hellish Touzoku Spine folder (relative to game root). Empty = use default: sources/HellGate_sources/Hellish Touzoku Spine. Subfolders: HelllishTouzokuBoSS, HelllishTouzokuAxe, HelllishTouzokuSword. |
| `SpawnScaleMultiplier` | Single | `0.8` | Visual scale multiplier applied to Hellish Touzoku on spawn (Boss / Axe / Sword). 1.0 = prefab size, 0.8 = 80%. |

## HellTraps

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `EnableLethalMagicTrap` | Boolean | `true` | Enable lethal magic trap spawn key 'lethal_magictrap' (legacy alias: letal_magictrap; 100x bullet damage by default) and custom PNG death clip on kill. |
| `LethalMagicTrapDamageMultiplier` | Single | `100` | Damage multiplier vs vanilla SetupFireball enmATK (vanilla ~70). Lethal default: 100 (= ~7000 per hit). |
| `DeathClipAssetsPath` | String | (empty) | Folder with numbered PNG frames (1.png..15.png), relative to game root. Empty = sources/HellGate_sources/CustomDeath/Exp_Death. |
| `LethalMagicTrapDeathClipDisplayScale` | Single | `1` | Uniform world scale for lethal magic trap death PNG overlay (1 = native size at 100 pixels per unit; Exp_Death default frames are 1400x835 px). |
| `LethalMagicTrapActTimeMultiplier` | Single | `1` | Delay before lethal trap fires (multiplier on vanilla acttime ~1.2s). Lower = faster shot, higher = longer warning icon. |
| `LethalMagicTrapBulletSpeedMultiplier` | Single | `1` | SetupFireball/Fireball Xspd/Yspd/startYspd multiplier for lethal_magictrap bullets. |
| `LethalMagicTrapSpawnScale` | Single | `1` | Uniform scale on spawned lethal trap instance (trigger collider + visuals). Use for wider/narrower activation area. |
| `EnableLethalCocoonTrap` | Boolean | `true` | Enable lethal cocoon trap spawn key 'lethal_cocoontrap' (alias: Lethal_cocoontrap). Based on cocoontrap; uses LethalMagicTrapDamageMultiplier vs vanilla 10 ATK; WebSpike_Death PNG clip at trap position. |
| `LethalCocoonTrapDeathClipPath` | String | (empty) | Folder with numbered PNG frames for lethal cocoon death (PPU 100). Empty = sources/HellGate_sources/CustomDeath/WebSpike_Death. |
| `LethalCocoonTrapDeathClipDisplayScale` | Single | `1` | Uniform world scale for lethal cocoon death PNG overlay (same bone playback as magic trap; 1 = native at 100 PPU; WebSpike_Death ~823x984 px vs Exp_Death ~1400x835). |

## HSceneBlackBackground

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enable` | Boolean | `true` | Black fullscreen background on H-scene climax (FIN / iki triggers). Set false to disable the effect. |
| `MindBrokenPerSecondPercent` | Single | `0.2` | MindBroken growth while H-scene black background is active (0.2 = +0.2% per second) |

## HSceneCameraZoom

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `ResetZoomValue` | Single | `1.5` | H-scene spacebar zoom — step 1 (base). Also applied when H-scene ends. Cycle: 1.5x → 3x → 5x → 1.5x |
| `ZoomLevel3x` | Single | `3` | H-scene spacebar zoom — step 2 (medium). Cycle: 1.5x → 3x → 5x → 1.5x |
| `ZoomLevel5x` | Single | `5` | H-scene spacebar zoom — step 3 (max). Cycle: 1.5x → 3x → 5x → 1.5x |
| `ZoomLevel2x` | Single | `2` | [UNUSED] Reserved zoom preset (not in current spacebar cycle) |
| `ZoomLevel4x` | Single | `4` | [UNUSED] Reserved zoom preset (not in current spacebar cycle) |
| `ZoomLevel8x` | Single | `8` | [UNUSED] Reserved zoom preset (not in current spacebar cycle) |
| `ZoomLevel10x` | Single | `10` | [UNUSED] Reserved zoom preset (not in current spacebar cycle) |

## HSceneEffects

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `StartZoom.Enable` | Boolean | `true` | Enable zoom and slowmo effect when H-scene starts |
| `StartZoom.SkipEnemyFatality` | Boolean | `true` | When true, HellGate start-zoom and spacebar zoom skip RequiemKnight death-fatality only (void camera risk). Other *Fatality grabs (Butcher/Slaughterer, BossScapegoatentrance, Candore, …) use HellGate camera like normal grabs. |
| `StartZoom.Amount` | Single | `3` | Zoom level when H-scene starts (3.0 = 3.0x zoom) |
| `StartZoom.Duration` | Single | `4` | Duration of zoom animation in seconds (4.0 = smooth 2.0 second zoom) |
| `StartZoom.SlowmoDelay` | Single | `0` | Seconds after zoom begins before slowmo starts (0 = together with zoom) |
| `StartZoom.SlowmoTimeScale` | Single | `0.2` | Time scale during slowmo (0.2 = 80% slowdown) |
| `StartZoom.SlowmoDuration` | Single | `3` | Duration of slowmo effect in seconds (real time, runs parallel with zoom when delay is 0) |
| `StartCenter.Enable` | Boolean | `true` | Enable camera centering on animation center when H-scene starts |
| `StartCenter.Duration` | Single | `0.5` | Duration of camera centering animation in seconds (0.5 = faster, more aggressive) |
| `StartCenter.YOffset` | Single | `0` | Y offset for camera centering (positive = up, negative = down) |

## InquisitionWhiteMindBroken

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `EnableWaveEffect` | Boolean | `true` | Enable visual wave effect during InquisitionWhite ERO_START3 animation and at 100% MindBroken |
| `MindBrokenPerSecond` | Single | `3` | MindBroken percentage added per second during syringe injection (ERO_START2) (default: 3 = 3%/sec) |

## MindBroken

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enable` | Boolean | `true` | Enable Mind Broken system (increases struggle difficulty and pleasure gain when player is passed between enemies) |
| `PercentPerPass` | Single | `0.01` | Mind Broken percentage added per handoff (0.01 = 1%) |
| `HScenePercentPerSecond` | Single | `0.1` | Passive MindBroken gain per second while in H-scene (eroflag + erodown). 0.1 = +0.1%/sec. 0 = disable. Stacks with enemy-specific ticks (Mutude, Pilgrim, etc.). |
| `StruggleBonusPerStep` | Single | `0.3` | Additional struggle difficulty per Mind Broken step (0.30 = +30%) |
| `MaxPercent` | Single | `1` | Maximum Mind Broken value (1.0 = 100%) |
| `BadEndCountdownDuration` | Single | `180` | Countdown duration in seconds before Bad End triggers at 100% MindBroken (default: 180.0 = 3 minutes) |
| `BadEndResetThreshold` | Single | `0.9` | MindBroken percentage threshold for countdown reset (default: 0.9 = 90%, timer resets if MB drops below this) |
| `HighRagePassiveEnable` | Boolean | `true` | While Rage bar is above HighRageThresholdPercent, apply passive MindBroken gain (encourages spending Rage). |
| `HighRageThresholdPercent` | Single | `60` | Rage percent (0-103) above which passive MindBroken applies (e.g. 60 = Tier-2 gate and above). |
| `HighRagePassivePercentPerSecond` | Single | `0.1` | MindBroken gain per second while Rage is above threshold (0.1 = +0.1%/sec). |
| `HighRagePassiveOnlyWhenRageInactive` | Boolean | `true` | If true, passive gain applies only while Rage mode is OFF (avoids stacking with rage_active / overdrive MB). |
| `DebugLogAddPercent` | Boolean | `false` | Log every MindBroken AddPercent call with reason to BepInEx (for diagnosing runaway temptation). |

## MindBrokenRecovery

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enable` | Boolean | `true` | Enable MindBroken recovery system - recover MindBroken by killing enemies |
| `PercentPerKill` | Single | `0.01` | Recovery percentage per normal enemy kill (0.01 = 1%) |
| `PercentPerBossKill` | Single | `0.05` | Recovery percentage per boss kill (0.05 = 5%) |
| `BossNames` | String | (empty) | Optional extra boss type keys (lowercase class names, comma-separated). Story bosses use FactionBossDetection (vanilla BOSSflag) automatically. |
| `CaptionCooldown` | Single | `1.5` | Cooldown between recovery captions in seconds (1.5 = 1.5 sec) |

## MindBrokenVisualEffects

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `FogAppearanceThreshold` | Single | `0.15` | MindBroken percentage threshold for fog to appear (0.15 = 15%) - later appearance for performance |
| `FogColorR` | Single | `1` | Fog color red component (0.0-1.0) |
| `FogColorG` | Single | `0.7` | Fog color green component (0.0-1.0) |
| `FogColorB` | Single | `0.95` | Fog color blue component (0.0-1.0) |
| `FogMaxAlpha` | Single | `0.3` | Maximum fog alpha intensity (0.0-1.0, 0.3 = 30% opacity) - reduced for performance |
| `FogPulseSpeed` | Single | `1` | Fog pulse animation speed (higher = faster pulse, 1.0 = gentle pulse) |
| `FogCenterRadiusMin` | Single | `0.35` | Legacy parameter - not used with horizontal bars effect |
| `FogCenterRadiusMax` | Single | `0.2` | Legacy parameter - not used with horizontal bars effect |
| `NegativeEffectDuration` | Single | `1.5` | Negative effect duration in seconds when triggered - reduced for performance |
| `NegativeActivationThreshold` | Single | `0.5` | MindBroken percentage threshold for negative effect to start (0.5 = 50%) |
| `NegativeActivationStep` | Single | `0.15` | MindBroken percentage step for negative effect triggers (0.15 = every 15% after threshold) - less frequent |
| `DreamEffectSpeed` | Single | `3` | Dream distortion effect animation speed (0-32, default: 3 = slow waves) |
| `DreamEffectDistortion` | Single | `4` | Dream distortion effect intensity (0-100, default: 4 = subtle distortion) |
| `FlashStartThreshold` | Single | `0.2` | MindBroken percentage to start flash effect (0.2 = 20%, then every 10%) |
| `FlashDuration` | Single | `3` | Flash effect total duration in seconds (default: 3) |
| `FlashPulseCycles` | Int32 | `3` | Number of pulse cycles during flash (default: 3) |
| `FlashMinAlpha` | Single | `0.08` | Flash minimum transparency (0.0-1.0, default: 0.08 = very subtle) |
| `FlashMaxAlpha` | Single | `0.22` | Flash maximum transparency (0.0-1.0, default: 0.22 = gentle) |
| `FlashColorR` | Single | `1` | Flash color red component (0.0-1.0, default: 1.0) |
| `FlashColorG` | Single | `0.75` | Flash color green component (0.0-1.0, default: 0.75 = soft pink) |
| `FlashColorB` | Single | `0.88` | Flash color blue component (0.0-1.0, default: 0.88 = soft pink) |
| `FlashFadeOutTime` | Single | `0.8` | Flash fade out duration in seconds (default: 0.8 = smooth end) |
| `DreamDuration` | Single | `5` | Dream effect total duration in seconds at 100% MindBroken (default: 5) |
| `DreamFadeInTime` | Single | `1.2` | Dream effect fade in duration in seconds (default: 1.2 = smooth start) |
| `DreamFadeOutTime` | Single | `1.5` | Dream effect fade out duration in seconds (default: 1.5 = very smooth end) |
| `FogPulseAmount` | Single | `0.03` | Fog pulse amplitude (0.03 = barely visible) |

## MutudeMindBroken

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `MindBrokenPerSecondPercent` | Single | `1` | MindBroken growth while Mutude DRINK/ERO3/ERO4/ERO5 animations are active (1 = +1% per second) |

## PilgrimMindBroken

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `MindBrokenPerSecondBell` | Single | `2` | MindBroken percentage added per second during bell-ringing hypnosis phases (START2, FERA1, EROSTART, 2ERO) (default: 2 = 2%/sec) |

## PlayerVisualFixes

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `EnableHitBloodParticleCleanup` | Boolean | `true` | After player takes damage, clear lingering vanilla blood particles (Blood7 / playercon.blood) that otherwise follow the player when HellGate is loaded. |
| `HitBloodParticleCleanupDelaySeconds` | Single | `1.25` | Real-time seconds after a hit before blood sub-emitters (Head/Right/Main/Left) are stopped and cleared. |

## PleasureStatus

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `PleasurePercentAfterOrgasm` | Single | `0.75` | After an orgasm cause by Pleasure Paralysis, Pleasure Paralysis will be set back to this percentage (0-1) |
| `EnemyAttackMultiplierMax` | Single | `2.5` | Player takes this much more damage when at max pleasure |
| `EnemyAttackMultiplierMin` | Single | `1` | Player takes this much more damage when at zero pleasure |
| `PlayerAttackMultiplierMax` | Single | `0.3` | Player deals this much more damage when at max pleasure |
| `PlayerAttackMultiplierMin` | Single | `1` | Player deals this much more damage when at zero pleasure |
| `PlayerAttackSpeedMultiplierMax` | Single | `0.7` | Player attacks this much faster when at max pleasure |
| `PlayerAttackSpeedMultiplierMin` | Single | `1.3` | Player attacks this much faster when at zero pleasure |
| `GainPerSecDuringEro` | Single | `1` | Amount pleasure bar fills per sec during ero (0-100) |
| `GainWhenHit` | Single | `0` | Amount pleasure bar fills when hit by an attack (0-100) |
| `LossWhenHit` | Single | `5` | Amount pleasure bar reduces when player lands an attack (0-100) |
| `GainWhenBlock` | Single | `0` | Amount pleasure bar fills when hit by chip damage from block (0-100) |
| `GainWhenDowned` | Single | `5` | Amount pleasure bar fills when downed by an attack (0-100) |
| `EnablePregnancy` | Boolean | `true` | Enables or disables additional pregnancy content such as multiple births and birthing based on sperm type (base game preg content will always be enabled) |
| `EnableAnyPregnancy` | Boolean | `true` | Allows aradia to give birth to any non-boss enemy (Aradia will give birth to a green slime everytime if disabled) |
| `PregnancyChance` | Single | `0.8` | Chance to get pregnant after a creampie (0-1) |
| `ExtraBirthChance` | Single | `0.1` | Chance to birth again after giving birth (0-1) |
| `DisableParalysis` | Boolean | `false` | Set to true to disable the vanilla Pleasure Paralysis effect (flinch/stun effect that occurs randominly when at max pleasure) |
| `OrgasmFlashStrength` | Single | `0.25` | Intensity of white flash of pleasure when Aradia experiences orgasm (0 = disabled, 1 = full intensity, 0.25 = default) |

## PortraitMod

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enable` | Boolean | `true` | When true, replaces the vanilla UIface Spine portrait with looping PNGs from Portrait_mod (Normal, NakedNormal, Sex, Rage, NakedRage, Brainwash). When false, vanilla Spine is restored. |
| `AssetsPath` | String | (empty) | Root folder for portrait assets, relative to the game install. Leave empty to use sources/HellGate_sources/Portrait_mod. |
| `SecondsPerFrame` | Single | `0.06666667` | Display duration per frame in the PNG cycle (seconds). Default 1/15 s (~15 FPS); lower values advance frames faster. |
| `BrainwashMindBrokenFraction` | Single | `0.5` | Minimum MindBroken normalized value [0,1] to select the Brainwash asset folder. |
| `DisplayScale` | Single | `1` | Uniform scale applied to the overlay RectTransform after native size and optional MaxNativeWidth clamp. |
| `MaxNativeWidth` | Single | `384` | Maximum width in layout units after SetNativeSize (aspect preserved). Caps oversized textures; set 0 to disable. |

## Pregnancy

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enable` | Boolean | `true` | Master switch for the extended HellGate Pregnancy module (womb meter, faction-typed conception, trimesters, offspring). Vanilla base pregnancy is unaffected when this is false. |
| `WombCapacityMl` | Single | `500` | Womb buffer capacity in milliliters. While the womb is below capacity it is 'safe'; reaching capacity triggers a guaranteed conception by the dominant seed faction. |
| `MlPerContactOverride` | Single | `0` | If > 0, every creampie adds this fixed amount of ml regardless of the game's native value. 0 = use the native per-event ml count from EnemyDate.Nakadasi. |
| `ShowWombMeter` | Boolean | `true` | Show the on-screen womb fill meter (bar + percentage). |
| `DebugLogging` | Boolean | `false` | Verbose logging of seed intake and conception events to the BepInEx console / LogOutput.log. |
| `BirthTransformDelaySeconds` | Single | `3` | Seconds after birth before the slime transforms into the MafiaMuscle offspring. |
| `BirthSlimeDisplayScale` | Single | `0.5` | Uniform scale of the birth slime before it transforms (1.0 = vanilla suraimu size). |
| `OffspringDisplaySeconds` | Single | `30` | Seconds the transformed offspring remains visible before moving to the hideout (or despawning outside ParishChurch). |

## Pregnancy.Altar

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `ResetWombMeter` | Boolean | `true` | When touching an altar (Savepoint_on.fun_ALLreset): clear accumulated semen in the HellGate womb meter. Disable when using future cleanse items instead. |
| `ResetActivePregnancy` | Boolean | `true` | When touching an altar: abort active gestation (trimester I–III) and any queued post-H-scene conception. Mirrors vanilla BADstatusReset for pregnancy. Disable when using future abortifacient items instead. |

## Pregnancy.Blocking

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `BlockAllPregnancy` | Boolean | `false` | If true, no seed is ever accumulated and conception never occurs through this module. |
| `AllowFromDemons` | Boolean | `true` | Allow conception sourced from the Demons faction. |
| `AllowFromMonsters` | Boolean | `true` | Allow conception sourced from the Monsters faction. |
| `AllowFromChurch` | Boolean | `true` | Allow conception sourced from the Church faction. |
| `AllowFromBandits` | Boolean | `true` | Allow conception sourced from the Bandits faction (all bandit sub-families). |
| `AllowFromMafia` | Boolean | `true` | Allow conception sourced from the Mafia faction. |
| `AllowFromUndead` | Boolean | `true` | Allow conception sourced from the Undead faction. |

## Pregnancy.Bloodline

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `DemonsIntBonusPerChild` | Int32 | `2` | Per demon child: +INT. |
| `DemonsStrBonusPerChild` | Int32 | `1` | Per demon child: +STR. |
| `DemonsRagePerSecondPerChild` | Single | `0.05` | Per demon child: +Rage % per second. |
| `ChurchStaBonusPerChild` | Int32 | `2` | Per church child: +STA (MAXtough). |
| `ChurchLuckBonusPerChild` | Int32 | `1` | Per church child: +luck. |
| `ChurchRagePerSecondPerChild` | Single | `0.05` | Per church child: +Rage % per second. |
| `MonstersStrBonusPerChild` | Int32 | `2` | Per monster child: +STR. |
| `MonstersStaBonusPerChild` | Int32 | `1` | Per monster child: +STA (MAXtough). |
| `MonstersRagePerSecondPerChild` | Single | `0.05` | Per monster child: +Rage % per second. |
| `UndeadStrBonusPerChild` | Int32 | `1` | Per undead child: +STR. |
| `UndeadLuckBonusPerChild` | Int32 | `1` | Per undead child: +luck. |
| `UndeadStaBonusPerChild` | Int32 | `1` | Per undead child: +STA (MAXtough). |
| `UndeadRagePerSecondPerChild` | Single | `0.05` | Per undead child: +Rage % per second. |
| `BanditsDexBonusPerChild` | Int32 | `2` | Per bandit child: +DEX. |
| `BanditsLuckBonusPerChild` | Int32 | `1` | Per bandit child: +luck. |
| `BanditsRagePerSecondPerChild` | Single | `0.05` | Per bandit child: +Rage % per second. |
| `MafiaLuckBonusPerChild` | Int32 | `2` | Per mafia child: +luck. |
| `MafiaDexBonusPerChild` | Int32 | `1` | Per mafia child: +DEX. |
| `MafiaRagePerSecondPerChild` | Single | `0.05` | Per mafia child: +Rage % per second. |
| `MaxBloodlineStrBonus` | Int32 | `20` | Maximum total +STR from all bloodline sources. |
| `MaxBloodlineIntBonus` | Int32 | `20` | Maximum total +INT from all bloodline sources. |
| `MaxBloodlineDexBonus` | Int32 | `20` | Maximum total +DEX from all bloodline sources. |
| `MaxBloodlineStaBonus` | Int32 | `20` | Maximum total +STA (MAXtough) from all bloodline sources. |
| `MaxBloodlineLuckBonus` | Int32 | `20` | Maximum total +luck from all bloodline sources. |
| `MaxBloodlineRagePerSecond` | Single | `1` | Maximum total passive Rage % per second from all bloodline sources. |

## Pregnancy.OffspringArchetype

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enable` | Boolean | `true` | Roll a per-faction offspring prefab at birth (see HellGateJson/Pregnancy/OffspringArchetypes.json). |
| `LogRolls` | Boolean | `false` | Log each offspring archetype roll to BepInEx (independent of Pregnancy.DebugLogging). |

## Pregnancy.OffspringCombat

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `PreventOffspringDamageToPlayer` | Boolean | `true` | If true, hideout offspring cannot damage or grab Aradia (player). Includes grab-via-attack and collision grab. |
| `PreventPlayerDamageToOffspring` | Boolean | `true` | If true, player weapons and magic cannot damage offspring in the hideout. |
| `PreventOffspringFactionFriendlyFire` | Boolean | `true` | If true, Witch-faction offspring cannot damage each other. Set false to allow sibling brawls. |

## Pregnancy.Physics

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `BlockDashInThirdTrimester` | Boolean | `true` | If true, all dash actions (dodge, double-tap dash, dash-jump) are blocked during the third trimester. |
| `ThirdTrimesterJumpMultiplier` | Single | `0.65` | Jump impulse multiplier during the third trimester (0.65 = 35% shorter jumps). |
| `ThirdTrimesterMoveSpeedMultiplier` | Single | `1` | Ground movement speed multiplier during the third trimester (1 = no change). |

## Pregnancy.SemenValue

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `EnableSemenValueMultiplier` | Boolean | `true` | If true, weak enemies deposit more semen during Nakadasi so pregnancy progresses at a reasonable pace. |
| `MinimalCategoryMultiplier` | Single | `6` | Multiplier for the MINIMAL semen category (base <= 20 ml). |
| `StandardCategoryMultiplier` | Single | `3` | Multiplier for the STANDARD semen category (base 24-60 ml). |
| `MaxSemenValueCap` | Int32 | `120` | Maximum ml per Nakadasi after multipliers are applied. |

## Pregnancy.ShelterAttack

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enable` | Boolean | `true` | Enable dynamic hideout shelter attack events (children in ParishChurch are attacked while Aradia is away). |
| `TriggerChance` | Single | `0.2` | Chance (0.0–1.0) that a shelter attack is rolled after ArmDelaySeconds following any zone transition (door, altar, teleport). 1.0 = always try, 0.0 = never. |
| `ArmDelaySeconds` | Single | `2` | Real-time seconds after a zone transition before the trigger chance is rolled once. Avoids hitches right after loads. |
| `TimerSeconds` | Single | `60` | Real-time seconds after a successful arm roll before the assault can begin in ParishChurch. |
| `AlertSeconds` | Single | `15` | How many seconds before the assault deadline the warning phrases start appearing above Aradia (clamped to TimerSeconds). |
| `PhraseIntervalSeconds` | Single | `5` | Seconds between red floating warning phrases during the alert phase (also used as on-screen phrase display duration). |
| `SpawnCooldownMin` | Single | `4` | Minimum cooldown between enemy spawns at the same ParishChurch point. |
| `SpawnCooldownMax` | Single | `8` | Maximum cooldown between enemy spawns at the same ParishChurch point. |
| `WaveIntroSeconds` | Single | `10` | Seconds to show the WAVE 1 banner in the hideout before the first enemies spawn. |
| `WaveBreakSeconds` | Single | `10` | Real-time pause between cleared waves (before waves 2, 3, ...). Shows the next wave banner and a countdown in ParishChurch. |
| `FinalWaveBreakSeconds` | Single | `15` | Real-time pause before the final (boss) wave spawns, overriding WaveBreakSeconds for the last wave. |
| `TimeoutFlashSeconds` | Single | `3` | Seconds the red TIME OUT label and bar stay on screen before timeout defeat presentation. |
| `ShowTimerHud` | Boolean | `true` | Show on-screen timers: attack countdown while away, inter-wave countdown in the hideout, and wave banners. |
| `ResetOnWin` | Boolean | `true` | After a successful defense, reset the event so it can trigger again later. |
| `ResetOnLoss` | Boolean | `true` | After a failed defense (Aradia knocked out), reset the event so it can trigger again later. |

## Pregnancy.Trimester

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `TrimesterTotalSeconds` | Single | `90` | Total duration of the pregnancy in real-time seconds. Default is 90s (30s per trimester) for testing; raise to 360s (6 minutes) for normal play. |
| `Trimester2Threshold` | Single | `0.333` | Fraction of the pregnancy duration when the second trimester begins. |
| `Trimester3Threshold` | Single | `0.666` | Fraction of the pregnancy duration when the third trimester begins. |

## Pregnancy.TrimesterModifiers

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `TrimesterStatPenaltyPerLevel` | Int32 | `3` | Flat penalty to STR/DEX/INT/crit applied per current trimester level. Trimester 1 = -3, Trimester 2 = -6, Trimester 3 = -9. |
| `TrimesterMoveSpeedPenalty` | Single | `0.3` | Ground move speed multiplier from II trimester onward (0.30 = -30%). |

## Pregnancy.TrimesterVisuals

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `TrimesterVisualIntervalSeconds` | Single | `5` | Seconds between periodic visual effects during II and III trimesters. |
| `TrimesterVisualDurationSeconds` | Single | `2` | Duration of each spawned visual effect. |
| `TrimesterVisualOffsetY` | Single | `0.35` | Vertical offset for the spawned effect relative to the player root. |
| `DemonsVisualEffectIndex` | Int32 | `3` | playereffect.Buffeffect index used for Demons trimester visuals (-1 = off). |
| `MonstersVisualEffectIndex` | Int32 | `3` | playereffect.Buffeffect index used for Monsters trimester visuals (-1 = off). |
| `ChurchVisualEffectIndex` | Int32 | `3` | playereffect.Buffeffect index used for Church trimester visuals (-1 = off). |
| `BanditsVisualEffectIndex` | Int32 | `0` | playereffect.Buffeffect index used for Bandits trimester visuals (-1 = off). |
| `MafiaVisualEffectIndex` | Int32 | `0` | playereffect.Buffeffect index used for Mafia trimester visuals (-1 = off). |
| `UndeadVisualEffectIndex` | Int32 | `1` | playereffect.Buffeffect index used for Undead trimester visuals (-1 = off). |

## QTE

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `RageAttackClickSPGainPercent` | Single | `0.5` | SP gain per attack click during Rage as percentage of max SP (0.5 = 50%). In QTE section for consistency. |
| `SuccessVolumeMultiplier` | Single | `1.5` | Volume multiplier for successful QTE button press sound (1.0 = 100%) |
| `FailureVolumeMultiplier` | Single | `1.5` | Volume multiplier for QTE error sound (1.0 = 100%) |
| `SPGainBase` | Single | `0.016` | SP gain for A/D buttons at 0% MindBroken (0.05 = 5% of MaxSP) |
| `SPGainMin` | Single | `0.002` | SP gain for A/D buttons at 100% MindBroken (0.02 = 2% of MaxSP) |
| `YellowButtonSPGainMin` | Single | `0.05` | Minimum SP gain for yellow W/S buttons (0.15 = 15% of MaxSP) |
| `YellowButtonSPGainMax` | Single | `0.2` | Maximum SP gain for yellow W/S buttons (0.3 = 30% of MaxSP) |
| `ClickSPGainBase` | Single | `0.01` | SP gain for mouse/E click during struggle at 0% MindBroken (0.015 = 1.5% of MaxSP) |
| `ClickSPGainMin` | Single | `0.005` | SP gain for mouse/E click during struggle at 100% MindBroken (0.005 = 0.5% of MaxSP) |
| `MPPenaltyPercent` | Single | `0.3` | MP penalty for wrong button press (0.3 = 30% of MaxMP) |
| `MindBrokenPenaltyPercent` | Single | `0.002` | MindBroken penalty for wrong W/S press during cooldown (0.002 = 0.2%) |
| `RedButtonMindBrokenPenalty` | Single | `0.002` | MindBroken penalty for pressing red W/S button (0.002 = 0.2%) |
| `SPPenaltyMultiplier` | Single | `2` | SP penalty multiplier for wrong A/D press during cooldown (2.0 = 2x the correct press gain) |
| `WindowDurationMin` | Single | `2` | Minimum QTE window duration in seconds |
| `WindowDurationMax` | Single | `3.5` | Maximum QTE window duration in seconds |
| `CooldownDurationMin` | Single | `2` | Minimum cooldown between windows in seconds |
| `CooldownDurationMax` | Single | `4` | Maximum cooldown between windows in seconds |
| `ButtonPositionX` | Single | `0` | Shift the whole QTE button row left/right from screen center (NOT spacing). Pixels at 1080p ref: negative = left, positive = right. Example: -150 left, +150 right. ButtonSpacing is separate. |
| `ButtonPositionY` | Single | `70` | Distance from top of screen to the button row center, in pixels (1080p reference). Default 70 matches pre-1.2.1 HUD height. |
| `ButtonSpacing` | Single | `100` | Gap between adjacent QTE buttons in the row (does NOT move the row left/right — use ButtonPositionX for that) |
| `ColorChangeInterval` | Single | `1` | Color change interval for W/S buttons in seconds |
| `PressIndicatorDuration` | Single | `0.15` | Visual press indicator duration (green/red flash) in seconds |
| `MaxButtonTransparency` | Single | `0.5` | Maximum button transparency at 100% MindBroken (0.5 = 50%, 0.0 = opaque, 1.0 = fully transparent) |
| `MaxPinkShadowIntensity` | Single | `1` | Maximum pink neon shadow brightness at 100% MindBroken (1.0 = 100%, 0.0 = no shadow) |
| `ComboMilestone` | Int32 | `10` | Combo threshold for bonus activation (counter of correct yellow button presses) |
| `EnableQTESystem` | Boolean | `true` | Enable or disable QTE System 3.0 (struggle system) |

## RageMode

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enable` | Boolean | `true` | Enable Rage Mode system (counter-mechanic to MindBroken) |
| `ActiveImmuneGrabAndKnockdown` | Boolean | `true` | While Rage is active: block elite grab (collision + grab-via-attack) and prevent knockdown from kickback types 3/4/6 (damage still applies). |
| `CritMultiplier` | Single | `1.5` | Critical damage multiplier during Rage (1.5 = 50% bonus, 2.0 = 100% bonus) |
| `MindBrokenBaseGainPerSecondPercent` | Single | `0.5` | Base MindBroken gain during active Rage (0.5 = +0.5% per second) |
| `HandsParticleMaxParticles` | Int32 | `15` | Maximum particles per hand for fire effects (lower = better performance) |
| `PerformanceMode` | Boolean | `false` | Enable performance mode: reduces particles and effects for better FPS |
| `HandsGlowSizePx` | Single | `96` | Size of the glow effect around hands during Rage (in pixels) |
| `GainPerKill` | Single | `3` | Rage percent per normal enemy kill on death (3 = +3%). Bosses use GainPerBossKill. |
| `GainPerBossKill` | Single | `30` | Rage Energy percentage per boss kill (30.0 = 30%). Boss detection uses vanilla BOSSflag / FactionBossDetection. |
| `PassiveTickAmount` | Single | `0.3` | Rage Energy percentage per passive tick (only if MB >70%, 0.3 = 0.3%) |
| `PassiveTickInterval` | Single | `3` | Passive tick interval in seconds (3.0 = 3 sec) |
| `ActivationCost` | Single | `50` | LEGACY: single-mode activation cost. Tiered system uses fixed per-tier costs (T1=30, T2=60, T3=100). |
| `ActivationDuration` | Single | `8` | LEGACY: single-mode activation duration. Tiered system uses RageTier1/2/3Duration. |
| `CooldownDuration` | Single | `10` | Cooldown duration after activation in seconds (10.0 = 10 sec) |
| `TimeSlowMoTimeScale` | Single | `0.4` | Time slow-mo time scale (T key) (0.4 = 60% slowdown, 0.5 = 50%, 1.0 = no slowdown) |
| `TimeSlowMoRageDrainPerSecond` | Single | `5` | Rage Energy drain per second when using Time Slow-Mo (T) (5.0 = 5% per second) |
| `MinActivationPercent` | Single | `50` | LEGACY: previous single-threshold activation. Tiered system uses RageTier1/2/3 thresholds below. |
| `CostDuringQTE` | Single | `50` | LEGACY: QTE now uses tier-based activation costs. Kept for backward compatibility with old configs/log paths. |
| `RageTier1Threshold` | Single | `30` | Tier1 threshold (outside H-scene only). |
| `RageTier2Threshold` | Single | `60` | Tier2 threshold (outside and inside H-scene; minimum for Rage-based H escape). |
| `RageTier3OverflowThreshold` | Single | `103` | Tier3 threshold using overflow (internal cap above 100; UI still shows max 100). |
| `RageTier1Duration` | Single | `5` | Tier1 activation duration in seconds. |
| `RageTier2Duration` | Single | `10` | Tier2 activation duration in seconds. |
| `RageTier3Duration` | Single | `15` | Tier3 activation duration in seconds. |
| `ActivationCameraShake` | Boolean | `true` | Camera shake effect when Rage activates |
| `GrabDrainMin` | Single | `1` | Rage drain per second when grabbed at 0% MindBroken (default: 1.0 = 1%/sec) |
| `GrabDrainMax` | Single | `10` | Rage drain per second when grabbed at 100% MindBroken (default: 10.0 = 10%/sec, linear interpolation) |
| `SlowMoDrainMultiplier` | Single | `2` | Multiplier for SlowMo rage drain (default: 2.0 = base drain * 2.0) |
| `SlowMoMBGainMultiplier` | Single | `2` | Multiplier for SlowMo MindBroken gain (default: 2.0 = base gain * 2.0) |
| `UIPositionX` | Single | `360` | Rage UI X position from left edge (default: 360.0 = 360px) |
| `UIPositionY` | Single | `-25` | Rage UI Y position from top edge (default: -25.0 = 25px down from top, negative = down from top) |
| `BloodEffectDuration` | Single | `0.5` | Duration of Vision_Blood_Fast effect on activation in seconds (0.5 = 0.5 sec) |
| `OutburstFuryDrainPerSecond` | Single | `10` | LEGACY: old auto-Outburst drain value. Tiered mode uses timer windows and does not rely on legacy auto-Outburst. |
| `KillTimeoutSeconds` | Single | `5` | Seconds without kill to refresh overdrive timeout |
| `ComboTimeout` | Single | `2` | Seconds without attack to reset combo (2.0 = 2 sec) |
| `ComboBaseGain` | Single | `3` | Base rage per hit before ComboGainMultiplier and global hit scale (1/3). With ComboGainMultiplier=0.5 and global 1/3 => +0.5% rage per hit. Every 10th hit adds flat +1%, +2%, +3%... on the bar. |
| `ComboGainMultiplier` | Single | `0.5` | Multiplier for base per-hit combo rage only. Does not affect x10 flat milestones (+1/+2/...), kills, parry, block, vengeance. |
| `ResetHCPenaltyGrab` | Single | `0.05` | MindBroken penalty when Rage is interrupted by grab / H activation (0.05 = +5% MB). Applies to normal Rage and Outburst Fury. |
| `ResetHCPenaltyKnockdown` | Single | `0.02` | MindBroken penalty when Rage is interrupted by knockdown only (0.02 = +2% MB). Grab uses ResetHCPenaltyGrab. |
| `KeyPressCooldown` | Single | `0.2` | Cooldown between Rage key presses in seconds (0.2 = 200ms) |

## RageVisualEffects

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `GlowColorR` | Single | `1` | Rage edge glow red (0-1) |
| `GlowColorG` | Single | `0` | Rage edge glow green (0-1) |
| `GlowColorB` | Single | `0.15` | Rage edge glow blue (0-1) |
| `GlowMaxAlpha` | Single | `0.55` | Rage edge glow max alpha (0-1) |
| `HandsGlowEnable` | Boolean | `true` | Enable red glow on Aradia hands during Rage |
| `HandsGlowColorR` | Single | `1` | Hands glow red (0-1) |
| `HandsGlowColorG` | Single | `0` | Hands glow green (0-1) |
| `HandsGlowColorB` | Single | `0.15` | Hands glow blue (0-1) |
| `HandsGlowAlpha` | Single | `0.85` | Hands glow alpha (0-1) |
| `HandsGlowSizePx` | Single | `96` | Hands glow size in pixels |
| `HandsParticleEnable` | Boolean | `true` | Enable red fire particle effects on hands during Rage (like Mafia Muscle) |
| `HandsParticleEmissionRate` | Single | `20` | Particle emission rate (particles per second) |
| `HandsParticleSize` | Single | `4` | Particle size multiplier |
| `HandsParticleColorR` | Single | `1` | Particle color Red (0-1) |
| `HandsParticleColorG` | Single | `0` | Particle color Green (0-1) |
| `HandsParticleColorB` | Single | `0.15` | Particle color Blue (0-1) |
| `WingsEnable` | Boolean | `true` | Tier 3 Rage: enable demon wings sprite loop on kubi bone |
| `WingsDurationSeconds` | Single | `0` | Tier 3 wings: loop duration in seconds. 0 = until Rage ends (recommended). Positive = auto-destroy after N seconds. |
| `WingsFps` | Single | `24` | Tier 3 wings: animation speed (frames per second) |
| `WingsScale` | Single | `1` | Tier 3 wings: local scale multiplier |
| `WingsOffsetX` | Single | `-0.05` | Tier 3 wings: local X offset from kubi bone (bone space) |
| `WingsOffsetY` | Single | `0` | Tier 3 wings: local Y offset from kubi bone (bone space) |

## RickEnemyMod

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `AssetsPath` | String | (empty) | Path to RickEnemyMod folder (relative to game root). Empty = sources/HellGate_sources/RickEnemyMod. Shared Fatality Logo: Fatality Logo/FatalityDeath.png. Per-enemy fatality folders: Butcher/, etc. |

## SavePoints

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `TrappedSavePoints` | Boolean | `false` | Using the respawn save point after leaving will result in a gameover scene |
| `ShrinesRetoreVirginity` | Boolean | `false` | Activating a shrine will restore virginity |

## SlowMoVisualEffects

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `EdgeBarsColorR` | Single | `0.3` | SlowMo edge bars (top/bottom) red (0-1) |
| `EdgeBarsColorG` | Single | `0.6` | SlowMo edge bars green (0-1) |
| `EdgeBarsColorB` | Single | `1` | SlowMo edge bars blue (0-1) |
| `EdgeBarsMaxAlpha` | Single | `0.5` | SlowMo edge bars max alpha (0-1) |
| `BoneGlowEnable` | Boolean | `true` | Enable blue glow on bones (bone3, bone8) during TimeSlowMo |
| `BoneGlowColorR` | Single | `0.3` | SlowMo bone glow red (0-1) |
| `BoneGlowColorG` | Single | `0.6` | SlowMo bone glow green (0-1) |
| `BoneGlowColorB` | Single | `1` | SlowMo bone glow blue (0-1) |
| `BoneGlowAlpha` | Single | `0.85` | SlowMo bone glow alpha (0-1) |
| `BoneGlowSizePx` | Single | `48` | SlowMo bone glow size in pixels |

## SoundOnomatopoeia

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `SoundTimeout` | Single | `10` | Timeout in seconds between onomatopoeia displays for one sound |

## SpawnTemplates

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `EnablePreloadScenes` | Boolean | `false` | Deprecated. Additive scene preload is disabled — it caused rain/VFX leaks and hitches. Traps cache from visited scenes only. |
| `PreloadScenes` | String | (empty) | Deprecated — ignored. Leave empty. |
| `DumpAvailableCatalog` | Boolean | `true` | Write cached trap template keys to HellGateSpawnPoint/AVAILABLE_SPAWN_TEMPLATES_RUNTIME.txt when the catalog grows. |
| `EnablePersistentCache` | Boolean | `true` | Save discovered spawn keys to SPAWN_TEMPLATE_DISK_CACHE.txt and restore them on next launch (after leaving title menu). |
| `PreloadDiskCacheDuringSplash` | Boolean | `true` | While the HELLGATE disclaimer/splash is visible, preload spawn template scenes in the background so gameplay entry does not hitch. |
| `WhitelistSceneLoad` | Boolean | `false` | Deprecated — use persistent disk cache instead. Additive whitelist scene load breaks Gametitle and is off by default. |
| `EnableEnemyPrefabDiskCache` | Boolean | `true` | Save discovered boss/scene-locked enemy keys to ENEMY_PREFAB_DISK_CACHE.txt and restore them on demand. |
| `EnableWhitelist` | Boolean | `true` | Pre-cache keys from SPAWN_TEMPLATE_WHITELIST.txt via Resources scan (scene keys are saved to disk cache when visited). |

## StruggleDifficulty

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `HpDifficultyPercent` | Single | `100` | Linear multiplier for HP deficit during struggles. Use 0-100 for percent scaling or 0-10 for short scale. |
| `PleasureDifficultyPercent` | Single | `100` | Linear multiplier for Pleasure contribution during struggles. Use 0-100 for percent scaling or 0-10 for short scale. |

## TakeVengeance

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `MindBrokenReduceFraction` | Single | `0.9` | On Take Vengeance (death/BadEnd respawn): reduce MindBroken by this fraction of current value (0.9 = 90% reduction, e.g. 90% -> 9%) |
| `RageBonusPercent` | Single | `10` | On Take Vengeance (respawn): flat Rage added after optional drain. Default 10 = +10% on the bar. |
| `RageDrainFractionOfCurrent` | Single | `0` | On Take Vengeance: remove this fraction of *current* Rage before RageBonusPercent is applied (0 = no drain, 0.5 = lose half of current Rage, 1 = reset Rage to 0 before bonus). |
| `RageMaxPercentAfter` | Single | `10` | After Take Vengeance (after drain + bonus): clamp Rage to at most this value (10 = keep 10% or less). Use -1 to disable the cap. |
| `BadEndRespawnEnemies` | Boolean | `true` | On Take Vengeance from BadEnd: respawn enemies at spawn points |
| `BadEndEnemyRespawnDelay` | Single | `1.2` | Delay in seconds before enemy respawn after Take Vengeance from BadEnd (default 1.2) |
| `LethalTrapShockSoundEnable` | Boolean | `true` | After Take Vengeance from lethal trap death: play MindShock.wav + HeartBeat.wav from sources/HellGate_sources/CustomDeath. |
| `LethalTrapShockMindShockVolume` | Single | `1` | Volume for MindShock.wav during lethal-trap vengeance shock (0 = mute, 1 = full). |
| `LethalTrapShockHeartBeatVolume` | Single | `1` | Volume for HeartBeat.wav (vengeance shock loop + lethal trap proximity thoughts; 0 = mute, 1 = full). |

## TouzokuAggression

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `SpeedMultiplier` | Single | `1.5` | Touzoku speed multiplier (1.0-3.0). Affects movement and attack speed. 1.5 = +50% speed. |
| `AttackRangeMultiplier` | Single | `1.4` | Touzoku attack range multiplier (1.0-2.5). Affects attack distance. 1.4 = +40% range. |

## VengeanceStrike

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `EnableAssets` | Boolean | `true` | Load optional strike presentation assets from [Game]/sources/HellGate_sources/VengeanceStrike/ (portable path, same as other HellGate_sources content). |
| `SoundFile` | String | `fatality.wav` | WAV filename inside the VengeanceStrike folder (empty = skip loading). |
| `PlayOnStab` | Boolean | `true` | When true and WAV loaded, play once at the start of Stab_fun (parry follow-up stab). |
| `HandGlowLikeRage` | Boolean | `true` | Fire particles on hands during parry stab (bone3 = left color, bone8 = right color; see Hands* settings below). |
| `HandsParticleSizeMult` | Single | `7` | Multiplier for particle size during Vengeance hands (1 = same base as Rage fire). Default 7 ≈ 4× prior 1.75. |
| `HandsEmitterAreaMult` | Single | `12` | Multiplies spawn circle radius (base 0.06 world units). Bigger = fire fills a larger area around the hand bone; try 8–20. |
| `HandsEmissionMult` | Single | `2.25` | Multiplier for particles/sec during Vengeance hands. |
| `HandsMaxParticles` | Int32 | `48` | Max simultaneous particles per hand during Vengeance (higher = denser fire). |
| `HandsParticleLifetimeMin` | Single | `0.14` | Vengeance hand particles: min lifetime (seconds). Shorter = briefer trails. |
| `HandsParticleLifetimeMax` | Single | `0.36` | Vengeance hand particles: max lifetime (seconds). If max < min, values are swapped. |
| `HandsParticleSpeedMin` | Single | `0.22` | Vengeance hand particles: min outward speed (lower = shorter reach). |
| `HandsParticleSpeedMax` | Single | `0.62` | Vengeance hand particles: max outward speed. If max < min, values are swapped. |
| `HandsLeftColorR` | Single | `1` | bone3 hand red 0–1 (default same red fire as right). |
| `HandsLeftColorG` | Single | `0.15` | bone3 hand green 0–1. |
| `HandsLeftColorB` | Single | `0.12` | bone3 hand blue 0–1. |
| `HandsRightColorR` | Single | `1` | bone8 hand red 0–1 (default red fire). |
| `HandsRightColorG` | Single | `0.15` | bone8 hand green 0–1. |
| `HandsRightColorB` | Single | `0.12` | bone8 hand blue 0–1. |
| `HandsCoreEnable` | Boolean | `true` | Visible additive orb (nucleus) under the hand particle cloud; tinted HandsLeft/Right colors. |
| `HandsCoreScaleMult` | Single | `1` | Multiplier for orb diameter vs emitter radius (HandsEmitterAreaMult). 1 ≈ ~1.8× spawn circle radius. |
| `SlowMoDuringStab` | Boolean | `true` | On parry stab start: apply slow-mo for SlowMoDurationSeconds (real time), then restore. New stabs during that window do not extend it. |
| `SlowMoTimeScale` | Single | `0.1` | World Time.timeScale during Vengeance window (clamped 0.01–1). Values below 0.01 are raised to 0.01: true 0 freezes Spine animation (no deltaTime) and can softlock the stab combo. 0.1 = strong slow-mo, 1 = no change. |
| `SlowMoDurationSeconds` | Single | `2` | How long slow-mo lasts in real seconds (not tied to stab animation length). Another stab starting during this time does not get a new slow-mo window. |
| `SpineBoostDuringStab` | Boolean | `true` | Multiply player Spine timeScale during stab so the strike anim stays snappy while the world is slowed. |
| `SpineMultiplier` | Single | `2` | Multiplier on SkeletonAnimation.timeScale during stab (after vanilla Update). Default 2. |
| `SpineCompensateTimeScale` | Boolean | `false` | When true, multiply further by 1/Time.timeScale while world is slowed. Default false (calmer anim than compensated mode). |
| `BlockGrabDuringStab` | Boolean | `true` | While parry stab (Vengeance) is active (_stabnow), enemy cannot grab Aradia (collision elite grab + grab-via-attack). |
| `RageCostEnable` | Boolean | `true` | Require Rage (see RageCostPercent) to perform parry follow-up stab (Stab_fun). If Rage mode is off, cost is skipped. |
| `RageCostPercent` | Single | `15` | Rage consumed when Vengeance stab executes (0–100). If current Rage is below this, Stab_fun is blocked. |

## VisualIndicators

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `DisableStruggleCameraShake` | Boolean | `true` | Disable camera shake during struggle (Hellachaz/NoREroMod original) |
| `EnableStruggleVisualIndicators` | Boolean | `true` | Shows visual indicators during struggle |
| `ShowDifficultyIndicator` | Boolean | `true` | Shows difficulty indicator bar |
| `ShowProgressIndicator` | Boolean | `true` | Shows struggle progress bar |
| `ShowCriticalChanceIndicator` | Boolean | `true` | Shows critical chance indicator |

## WeaponAnimations

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `WitchGreatsword.DuplicateLastTwoRounds` | Int32 | `0` | Append duplicate ground strike pairs (WeaponKind 1 / wp_bigwitch). 0 = auto until AtkMotion has 9 rows; 1-16 = fixed rounds. Re-equip after change. |
| `WitchExtendedGroundComboRequiresRage` | Boolean | `true` | Require Rage (IsActive) for extended ground hits 5-8. False allows full list without Rage (vanilla atk_fun covers 0-4 only). |

## WolfMod

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `AssetsPath` | String | (empty) | Path to Wolf Mod Spine folder (relative to game root). Empty = use default: sources/HellGate_sources/Wolf Mod Spine. MUST contain Enemy/WolfE.png and ERO/Wolf.png! |

