# Changelog

Notable changes per release. The plugin version lives in
`Core/PluginInfo.cs` (`PLUGIN_VERSION`); the public API is versioned
separately (see [`docs/development/API.md`](docs/development/API.md)).

Versions 1.2.2–1.2.3 were internal iterations folded into 1.2.4.

## Unreleased

## 1.2.8 — 2026-11

### Added

- **Demon Tentacle** (`DemonTentacleModCustom`, spawn key `DemonTentacle`):
  AngelStatue clone with a disk-loaded battle skeleton from
  `sources/HellGate_sources/Enemies/Demon_tentacle/`. Vanilla flying / shooting AI;
  takes regular weapon and magic damage instead of dying to any single hit.
  `[DemonTentacle] MaxHp` (default 800 on Normal, scaled by game difficulty),
  `ScaleMultiplier` (default 0.35 of the vanilla statue import scale). The projectile
  spawns from bone `bone21` (also the charge-glow point), fallback `bone26`,
  instead of the vanilla fixed offset. Default faction **demons** (spawn-line
  `|faction=` still wins). Fear entry in `Fear/FearEnemies.json` (aura 1.0/s).
  Combat AI (`DemonTentacleCombatAiPatch`, `CombatAi.json` `DemonTentacle*`):
  faster attack timer, 2-shot bursts, Ground/EnemyGround wall blocking, enrage
  below 40% HP (faster attacks and movement, +1 shot, red tint).
  Projectiles (`DemonTentacleProjectile`) damage hostile-faction enemies on
  contact and, during a committed faction brawl, home on the faction target
  instead of the player (vanilla homing script disabled; player damage unchanged).
- **Post-escape grab immunity** (`PostEscapeGrabImmunity`,
  `[GrabSystemNG] PostEscapeGrabInvulSeconds`, default **3** unscaled): on
  struggle / H escape (`StruggleSystem.startGrabInvul`) arms the same coverage
  as Rage grab immunity — GrabViaAttack, collision `CanEliteGrabPlayer`, and
  power-knockdown→DOWN grab are blocked; grab-chance UI shows **0%** while
  active. Docs: [`docs/modules/GRAB_AND_HANDOFF.md`](docs/modules/GRAB_AND_HANDOFF.md).
- **Fear combat layers** (`[FearSystem]` + `FearEnemies.json`): light hit through
  armor (`HitTakenMorale`, default −2) vs heavy / power (`HeavyHitMorale` /
  JSON `heavyHitFear`) vs knockdown / knockout (`KnockdownMorale`, stronger;
  stacks only the extra over a just-applied heavy). Per-enemy `grabFear` by
  size / threat (≈×2 vs early draft: goblin ~10 … bosses ~28–34). Boss
  attackers: aura ×`BossAuraMult` (2.5), combat hits ×`BossCombatFearMult`
  (2.25), KO at least last heavy + `BossKnockdownBonus` (8). Crowd aura
  mults, intermittent HeartBeat at −100, expanded `FearEnemies.json` roster /
  elite pressure. Doc: [`docs/modules/FEAR.md`](docs/modules/FEAR.md).
- **Undead death SFX pack** path
  `sources/HellGate_sources/AttackSounds/Undead/Death/BoneDeath` (`.wav`;
  unloadable `.mp3` alone leaves the category empty — undead stay silent
  rather than using human voices).
- **VagrantUndead gold drop** (`HellGateJson/Economic/GoldDropTable.json`
  `EnemyOverrides`): 50% chance, 2–5 gold (same chance style as faction rules).
- Splash credits: **Smkuzuy** (HellGate Animator), **Calibri**.

### Fixed

- **Undead / human death voice mix-up**: skeleton reskins (`*Undead`,
  `UndeadSkeletonsRuntime` marker) and undead base classes (`BigMerman` /
  `Bigoni` / `Pilgrim`, plus `Sisiruirui` / `SkeltonOoze` name gates) no longer
  roll `AttackSounds/Human/Death` (partial `Vagrant` name match was the culprit).
- **Death SFX “folder loop”**: `DEATH` re-fired every frame / after the old
  cooldown and stacked `PlayOneShot` voices. Now rising-edge + lifetime latch
  per enemy, dedicated death `AudioSource` (`Stop`/`Play`), short global gap.
- **Re-grab / double H right after potion or struggle escape**: missing
  HellGate post-escape shield (vanilla invul alone was not enough for
  GrabViaAttack / elite collision / knockdown grab); fixed via
  `PostEscapeGrabImmunity`.
- **RequiemKnight faction damage** (`FactionProjectilePatches`): `SlashDamage`
  light pillars (AOE pulse + EnemyCol) and `NomalMoveMagic` shadow bolts now
  apply hostile-faction damage. `BLADE` counts for melee pulse; `ATK_LIGHT*`
  skips melee pulse to avoid double-hitting with pillars.
- Fear projectile attribution / ranged coverage and HeartBeat intermittency at
  max fear (play 3–6 s, pause 5–10 s).

### Changed

- Fear HUD markers follow config thresholds; Fear / Grab docs updated for the
  new combat / boss / grabFear / post-escape rules.
- Difficulty presets (EASY / MEDIUM / HARD) list the new 1.2.8 keys
  (`HitTakenMorale`, `KnockdownMorale`, `BossAuraMult` / `BossCombatFearMult` /
  `BossKnockdownBonus`, `CrowdFear*`, `PostEscapeGrabInvulSeconds`,
  `[DemonTentacle]`) with shared defaults — Fear stays flat across difficulties
  (same as 1.2.7); MindBroken / grab chances still carry the difficulty curve.
- Log noise cut for shipping: `Factions.json` `ActivationDiag` default off
  (FactionAct SEES/ENGAGE spam), Fear per-delta Info, Death SFX/skip Info,
  Post-escape “armed” Info, repetitive GIMMICK open/close / spine-only / lever
  ignored, AttackSound null-mp3 Warnings demoted.
- Splash / plugin display version `1.2.7` → `1.2.8`.

## 1.2.7 — 2026-10

### Added

- **Hide cursor in combat and H-scenes** (`[General] HideCursorInGameplay`,
  default `true`): splash Options toggle (live, no restart; preserved across
  Easy/Medium/Hard). Cursor stays visible on the title screen, in the pause
  menu, shops / altar (including the teleport list) / chests, dialogs, the defeat menu (Vengeance /
  Nightmare) and the F11 Spawn Editor
  (`Systems/UI/GameplayCursorHider.cs`).
- **Custom title music** (`[TitleMusic] Enable`, default `true`): main menu
  theme replaced by a random `.wav` / `.ogg` from
  `sources/HellGate_sources/Audio/Main`, looped (no immediate repeat on the next
  title visit). Tracks ship as `.ogg`; empty folder → vanilla theme.
  Hooked at the start of `Awake` so it wins over the Gametitle BGM during cache
  preload (`Systems/Audio/TitleMusicSystem.cs`).
- **Demon Gorotuki** (`DemonGorotukiModCustom`, spawn key `Demon_gorotuki`):
  new Spine skeleton + texture on a Gorotuki clone, loaded from
  `sources/HellGate_sources/Gorutoki/Gorutoki/` (battle) and `Gorutoki ERO/`.
  Spine 3.7/3.8 exports are converted to the 3.4 runtime on load
  (`SpineJsonLegacyConverter`); fixed 0.008 scale (vanilla Gorotuki), no
  auto-scaling. Battle set matches vanilla animation / event names (STEP2 restored).
  Doc: [`docs/modules/CUSTOM_ENEMIES.md`](docs/modules/CUSTOM_ENEMIES.md).
- **Undead skeletons** (spawn keys `BigMermanUndead`, `BigoniUndead`,
  `PilgrimUndead`, `VagrantUndead`): vanilla combat prefab with a disk-loaded
  skeleton reskin from `sources/HellGate_sources/Enemies/UndeadSkeletons/<Key>/`.
  No H-scenes (they approach a downed player but never grab). Per-variant
  `[UndeadSkeletons.<Key>]`: HP ×0.25 of the vanilla base enemy, vanilla move
  and animation speed (`Patches/Enemy/UndeadSkeletonsModCustom/`).
- **Fear / Morale** (`[FearSystem] Enable`, default `true`): one axis
  −100 (panic) … +100 (confidence) with a bipolar bar at the bottom of the HUD.
  Enemy auras, heavy hits, projectiles, grabs, H-scenes and H-phase beats push
  toward fear; blocks, counters, parries, dodges, hits, kills, Rage and
  successful struggles push toward confidence. Panic raises dash SP cost,
  confidence boosts idle SP regen, Morale scales struggle / QTE SP per click
  (−100 → ×0.75, +100 → ×1.25). HeartBeat cues on fear steps, loop at max fear.
  Take Vengeance after a lethal trap or fatality: fear spikes to −100, then
  settles to −30; ordinary death → −10; altar rest → 0. Per-enemy values in
  `HellGateJson/Fear/FearEnemies.json` (hot-reloaded every 2 s), saved per slot.
  Doc: [`docs/modules/FEAR.md`](docs/modules/FEAR.md).
- **Gates, ladders and levers** in spawn packs (`GIMMICK,Key,X,Y,open|closed`
  + `lever`, `radius=N`, `guard=N`, `auto`): iron gates (`gate_fence`, two
  variants), folding ladders (wooden up / stone down with built-in lever,
  mossy without lever) and a standalone lever that drives gates / ladders in
  its radius. `auto` gates stay shut while enemies remain in their guard zone.
  Templates load from their source maps (`Systems/Spawn/Gimmicks/`).
- **F11 Spawn Editor:** Gimmicks panel `[L]` (gate / ladder / lever, open
  state, radius, guard zone height and side, auto); Enemies `[E]` list has a
  separate "Custom / Mod enemies" section on top (undead skeletons, Demon
  Gorotuki, Hellish Touzoku, Wolf, Butcher, BossTouzokuCustom, BigoniBrother,
  MafiaBossCustom, biscord). Clear status when trying to delete a vanilla map
  object or an unlinked extra copy.
- **New spawn content:** `woodspike` (Dungeon spiked wooden posts, `rot180` =
  hanging from the ceiling), `MesgakiEroSpine` decor (Dungeon background
  jailer scene, no rescue), new pack `HellGateSpawn_RequiemBoss.txt` for the
  `requiemboss` arena (RequiemKnight, past the hidden Forest area).
- **AudioLeak diagnostics** (dev, `HellGateJson/Diagnostics/AudioLeakDiagnostics.json`,
  default off; read at boot — off means no patches): logs MasterAudio plays with caller, AudioSource start/stop,
  scene loads, TrapNormal hits and active DontDestroyOnLoad roots to
  `LogOutput/HellGate_AudioLeakDiag.log`.
- **Performance log** (dev, `[Diagnostics] PerfLog` / `PerfProfileMethods`,
  default off): every 5 s a `[PERF]` line with FPS, worst frame, hitches, GC,
  heap and enemy / Spine / culled counts; optionally the top Harmony-patched
  methods and Spine updates by ms per frame
  (`Systems/Diagnostics/PerfDiagnostics.cs`, `PerfMethodProfiler.cs`).

### Fixed

- Rage could be activated after Aradia was already killed (lethal trap death
  clip, death flag or zero HP).
- Dungeon Dorei ambush (`etrap_dorei`) never spawned: the pack line was saved
  as an enemy XY line. Fixed in the pack, and the F11 editor now always writes
  EventTrap folders (`etrap_*`) as `EVENTTRAP` lines.
- Lethal cocoon trap threw an error on every trigger (the hidden warning icon
  was still instantiated); Aradia's floating thoughts and other dialogue popups
  threw errors when a newer line replaced them mid-animation.
- Endless sword-clash / exploding barrels in sepia story cutscenes (up to
  Ragdum) and Nightmare replays: the zone spawn pack ran inside cutscene
  scenes — `WitchVillage` keeps the zone id `village_main`, and substring pack
  hints matched `WitchVillage` / `CathedralEV` / `FirstONE` / `VillageOne`, so
  enemies landed on spawned spikes and looped hit SFX. Story EV, Nightmare
  (`*ONE`), Gallery / PunishmentRoom / Credit scenes are now
  excluded by name and while loaded (`HellGateLocationSpawnRefresh`,
  `HellGateSpawnSceneHints.TryResolvePackForZone`).
- Title menu: stage-title jingle (`SceneStartMng`, e.g. from `miniGrave`)
  no longer plays over the menu music during spawn-cache preload (its
  AudioSource is disabled in every scene loaded while on the title).
- Title preload leaves no scenes behind: the first hydrated scene
  (`UndergroundLaboratory`) was kept because the "current level" probe matched
  the scene just loaded; side-loads pulled in by hydrated levels (`village`,
  `miniGrave`) are unloaded right away, one `GameoverAnime` is kept for the
  preload and unloaded at its end.
- Faster startup: the spawn catalog hooks `sceneLoaded` before the
  NoRSceneLoader Forest harvest and takes templates (incl. scene-pinned
  whitelist hostages) from those scenes; the splash preload reloads a scene
  only if some of its disk-cache keys are still missing (objects created in
  `Start`), waits two frames for them, and no longer scans the whole whitelist
  on every scene load (preload ~33 s → ~8 s).
- Boss disk cache no longer records the NoRSceneLoader runtime scene for
  `Sisterknight` (it tried to load a non-existent scene on every boot); broken
  entries are repaired on load.
- F11 Spawn Editor: rotation / flip of a placed trap (e.g. `trapnormal`) was
  lost on hot-reload — moving the trap rewrote the old pack line with new X,Y
  only. The saved line now carries the live `flip` / `rot` tokens.
- F11 Spawn Editor: clicking a child part of a spawn (e.g. the WoodWana trap,
  enemy body parts) now selects the spawn itself, so move / edit / delete work;
  the editor pause is re-applied every frame (enemy scripts could unfreeze it).
- Any living Crow Inquisition reset `Time.timeScale` to 1 every frame — it broke
  Slow-Mo and the F11 pause anywhere on the map; now only during its own H-scene.
- Dorei spectators: Dorei that arrive after the H-scene started now join as
  spectators too, and the spectator overlay keeps its facing (no flipping).
- Hostages / traps spawned by HellGate (e.g. `Look_Dorei`, `MobRosewarm`) showed
  a censor square that appeared when the player came close: vanilla
  `Mosaic*` / `Pixelation*` quads render as a hole on clones and are now
  switched off on every spawned template (same policy as the existing
  Rosewarm / Gorotuki mosaic patches).
- FIN black background no longer fires during an unrelated H-scene because of
  background hostage loops (`MesgakiEroSpine`, `Mob_rockinghorseERO`).
- Harmony parameter-name bugs that silently disabled patches at boot:
  `WitchOffspringFriendlyFirePatch` (`col` → vanilla `attack`),
  `HellGateForceElitePatch` and `EnemyConstantVisibilityPatch` (now bound by
  index).
- Difficulty presets: writing an empty cfg value (`Key = `) could swallow the
  line break and overwrite the next setting.
- Log spam: missing optional fatality sounds and spine-field lookups no longer
  print warnings.

### Changed

- **First location** (`FirstMap`) significantly reworked: automatic gates now
  guard the way to the boss and stay shut while enemies remain in front of them.
- **Dungeon** reworked for HellGate: spawn layout edited and fully redone
  (`HellGateSpawn_Prison.txt`, scene `Prison`).
- Take Vengeance after a combat fatality or a vanilla H-fatality now plays the
  same Mind Broken shock sequence as lethal traps (plus Fear trauma).
- Fatality clips without a spawn offset drop 0.7 toward the ground by default
  (`FallDistance`).
- Default / fallback language is English: EventCore defaults to EN, fatality
  taunts fall back EN → RU (was RU → EN).
- `DifferentBigMerman` removed from the enemy spawn list.
- Removed the unused `[HandoffSystem]` cfg section (`EnableEnemyHandoff`,
  `HandoffCooldownTime`, `EnableHandoffVisualEffects`): no code read these
  values. Enemy handoff is controlled by `[EnemyPass] EnableEnemyPassMechanic`.
- Difficulty presets (EASY / MEDIUM / HARD) now list the 1.2.7 settings
  (`[FearSystem]`, `[UndeadSkeletons.*]`, `[TitleMusic]`, `[Performance]`,
  `[Diagnostics]`, `HideCursorInGameplay`, G/T/V hotkeys) with default values;
  existing preset balance unchanged.
- White Inquisitor's JIGO handoff now respects `[EnemyPass]
  EnableEnemyPassMechanic` like the other enemies (BigoniBrother and
  MafiaBossCustom keep their scripted handoff).
- **Performance on dense HellGate maps**: Spine animation of HellGate-spawned
  enemies, hostages and decor far outside the camera is paused
  (`[Performance] CullOffscreenSpine`, default `true`); AI and physics keep
  running. Fewer per-frame scans for enemies and player renderer recovery; a
  disabled black-ooze trap diagnostic no longer scans the scene every frame;
  faction AI checks bosses once per enemy per frame (less garbage, fewer GC
  hitches); the HUD visibility check and the cursor hider no longer search the
  scene for the vanilla HUD, diary windows and altar menu several times a
  second.
- Splash supporters list: **♡Miko♡**.
- Splash / plugin display version `1.2.6` → `1.2.7`.

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
