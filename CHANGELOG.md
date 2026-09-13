# Changelog

Notable changes per release. The plugin version lives in
`Core/PluginInfo.cs` (`PLUGIN_VERSION`); the public API is versioned
separately (see [`docs/development/API.md`](docs/development/API.md)).

Versions 1.2.2–1.2.3 were internal iterations folded into 1.2.4.

## Unreleased

-

## 1.2.6 — 2026-09

Public F95-style notes (player-facing) match the bullets below; this file keeps
dev detail and doc links.

### Added

- **Simple QTE / Free Struggle** (`[QTEFreeStruggle] Enable`, default `false`):
  splash Options toggle (live, no restart; preserved across Easy/Medium/Hard).
  Struggle: WASD windows always open; each WASD = mouse/E click SP; no
  yellow/red W/S or cooldown wrong-key penalties. Compact **36px** d-pad
  cross (default QTE stays **56px** row).
  Doc: [`docs/modules/QTE_STRUGGLE_AND_GAMEPLAY.md`](docs/modules/QTE_STRUGGLE_AND_GAMEPLAY.md).
- **EnemyFatality** (combat): PNG death clip + SFX + FatalityDeathIcon +
  localized killer taunts on qualifying hits. Requires Gore. Shared profile
  registry (`IEnemyFatalityProfile`); clips `lost_leg` / `lost_head` /
  `StillAlive` / `HeavyCritical`; enemies include White Inquisitor,
  CrawlingCreatures, Goblin line, Bigoni family, and others (see dossiers).
  Docs: [`docs/modules/ENEMY_FATALITY.md`](docs/modules/ENEMY_FATALITY.md),
  [`ENEMY_FATALITY_DOSSIERS.md`](docs/modules/ENEMY_FATALITY_DOSSIERS.md).
- **Spawn System Editor V2.0** (F11): full authoring overlay — catalogs
  Enemies / Hostage / Trap / Lethal / Decor / Gold / EventTrap / EventCore
  **C** / Favorites, precise world pick, box clipboard, undo, overview
  camera, gameplay pause while editing, F1 Help in ten languages. Banner
  title is the editor version. Doc: [`docs/modules/SPAWN.md`](docs/modules/SPAWN.md).
- **Illusive / Rodenia** church `SlaveBigAxe` event isolation (skip HellGate
  MeatArmor / H dialogue / DeadArmor / Fatality / FIN black-bg / Church
  faction). World axes unchanged.
  Doc: [`docs/modules/ILLUSIVE_RODENIA_EVENT.md`](docs/modules/ILLUSIVE_RODENIA_EVENT.md).
- **Costumes**: `[Costumes] Enable` unlocks gunner + Vendetta outfits in the
  costume menu without Trade purchase.
  Doc: [`docs/modules/COSTUMES.md`](docs/modules/COSTUMES.md).
- **LostSounds** scaffold (`LostSoundsAudio.Play`; default off; no jump/dash
  hooks yet). Doc: [`docs/modules/LOST_SOUNDS.md`](docs/modules/LOST_SOUNDS.md).
- **Remappable combat hotkeys** (`ConfigEntry<KeyCode>` in
  `NoREroMod_HellGate.cfg`):
  - `[RageMode] ActivationHotkey` (default `G`) — Rage toggle + QTE grab-escape
  - `[RageMode] TimeSlowMoHotkey` (default `T`) — Bullet-Time / Time Slow-Mo
  - `[CombatCamera] ToggleHotkey` (default `V`) — combat camera zoom cycle  
  Survives Easy/Medium/Hard preset apply (same preference path as Simple QTE).
  Spawn Editor (F11) keeps its own hardcoded G/T/V.

### Fixed

- Illusive lose path: handoff to `EvuChurchSP` after vanilla black fade
  (vanilla never loaded the next scene).
- Sisterknight / CrawlingSisterKnight GrabViaAttack → immediate H intro
  (no grab→release→walk).
- Lethal HellTrap / EnemyFatality death clips: freeze nearby combat AI;
  block stacked grabs and further lethal hits for the session.
- Difficulty preset copy preserves language / gore / Simple QTE / splash /
  font prefs; strips orphan `HellGateLanguage` outside `[General]`.
- Spawn-cache hydrate no longer permanently mutes world `AudioSource`s —
  H-scene SFX (plap/cum) play again after cache load (`SpawnCacheWeatherGuard`).
- Boot tips: `[Key]` highlight no longer breaks bold titles (minor).
- Options / Language labels use `_XUAIGNORE` (AutoTranslator).
- Death soul (`IdeaFall`) rise height 3.5 → 4.5.

### Changed

- Splash / plugin display version `1.2.5` → `1.2.6`.
- Faction bone markers (enemy faction icons): **35px → 20px**.
- Splash support row: **Boosty** beside Ko-fi / Patreon.
- Options panel layout compacted (JP adult-warning clearance); Language
  warning copy clarified (“restart after selecting”).
- EASY preset HP / QTE SP retuned; `allowPotionEasyEscape = true` on all
  shipped difficulty presets (and active cfg when applied).

## 1.2.5 — 2026-08

### Added

- **Early Boot tips + illustrated tutorial guide**: localized loading tips during
  cache hydrate, then a video guide (text + mute clips, Back/Pause/Forward,
  Done → splash). Locale JSON for all 10 picker languages.
- Splash supporters list: **KiRoSin43**.
- Module doc: [`docs/modules/BOOT_TIPS_AND_GUIDE.md`](docs/modules/BOOT_TIPS_AND_GUIDE.md).
- **DeadArmor**: PNG death clips when SlaveBigAxe / OtherSlavebigAxe girl armor
  breaks; armored grab-via-attack → short hold + knockback throw.
- Public read-only API `0.1.0` (`HellGateApi`): Rage / MindBroken / reputation /
  Gold / Pregnancy snapshots + events; GPL-3.0 + full docs tree.
- Hostage `|faction=` inherit on rescue spawn (SlaveSideLook hostages, Crow
  slaves, `gob_look` / `Look_Dorei` via ColCreateObj).
- Splash **Gore Content** checkbox (`General.EnableGoreContent`): gates DeadArmor
  death clips and CustomDeath lethal traps; armored grab-throw stays separate.

### Changed

- Splash / plugin display version `1.2.4` → `1.2.5`.
- Boot guide Done / nav buttons: IMGUI hover scale (matches Start feel).
- Faction bubble: activation distance tunable (`ActivationDistanceFromPlayer`,
  diag logs); bandits ↔ mafia (and `bandits_mafia`) are friendly.
- Spawn: `X,Y,Key` without `,1` defaults count to 1; bad lines warn in log;
  disk-cache harden (no `__resources__` wipe of real scenes; whitelist
  `key@Scene` hydrate fallback); `InchchurchSlave` → `InchurchSlave` alias;
  `trap_mokubaenemy` sourced from Prison.

### Fixed

- Struggle Out / QTE lockouts: respect `_easyESC` and struggle level 10; no
  15s force-unlock; QTE via `QTEStruggleWindowManager`.
- H-scene (WallHip / trap): faction AI freezes properly — no more void-punching
  while `erodown` + `eroflag`.
- `blackoozetypeb` re-arms after struggle escape (`trapflag` reset).
- Spawn template misses: mokuba / InchurchSlave / hostage faction overrides
  after CreateMonster Instantiate.
- MindBroken Bad End countdown: discard post-minimize `unscaledDeltaTime`
  hitch; advance only while Unity player window is foreground
  (`MindBrokenRealtimeGate`).

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
