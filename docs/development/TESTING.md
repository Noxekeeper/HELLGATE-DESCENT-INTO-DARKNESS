# Testing and Regression Matrix

There is no automated test suite: the plugin targets a live Unity game, so
validation is a clean Release build plus targeted in-game regression. This
document maps change areas to the manual checks they require.

A clean compile is **not** validation. Most historical regressions (soft
locks, invisible player, stuck timescale, lost saves) built without warnings.

## Change area → required checks

| You changed | Run |
|-------------|-----|
| `Systems/Gameplay/` (QTE, struggle, escape) | Smoke + H-scene pass |
| `Systems/Effects/`, `Systems/Camera/`, `Systems/HSceneEffects/` | H-scene pass (visual/state integrity) |
| `Systems/Dialogue/` | H-scene pass (bubble cleanup) + dialogue checks |
| `Patches/Player/` guards | H-scene pass + guarded-flow checks, including while pregnant |
| Handoff / `*Pass*` patches | H-scene pass with handoff chains |
| `Systems/Spawn/`, spawn packs, F11 authoring | Scene transition pass + Spawn authoring spot check |
| `Systems/EventCore/` | Spawn/EventCore content + EventCore F11 catalog + modal (with F11 off) |
| Save/load hooks, slot stores | Persistence pass |
| `Systems/Pregnancy/` | Pregnancy checks + persistence pass |
| `Systems/CombatAi/`, factions | Faction checks |
| `Systems/Economy/`, `Systems/Rewards/` | Economy checks + persistence pass |
| `NoREroModScaffoldConfigPush`, reflection into NoREroMod | Smoke + startup probe log |
| `Patches/HellTraps/` | HellTraps death clips followed by vengeance shock and respawn |
| `Systems/DeadArmor/` | SlaveBigAxe armor-break clip + armored grab-throw (hold then knockback; no H snap while NikuArmor) |
| `Systems/Costumes/` | Costume-change menu: with `[Costumes] Enable`, gunner + Vendetta slots visible/selectable without Trade; with Enable off, locked saves still show `??????` |
| `Systems/EnemyFatality/` | Shared combat-fatality profiles (White Inquisitor first): PNG + gates + AI freeze until Death_flag |
| `HellGateBootTips` / early-boot in `LoadingScreenSystem` | Fresh boot: loading tips rotate; `controls` fully visible above caching line; guide clips + Done → splash; one non-EN locale |
| `HellGateSplashOptionsMenu` / `HellGateDifficultyPresetModule` | Options open/close (label clear of adult warning); Gore toggle; Easy/Medium/Hard copies both cfg files + restart notice; Language submenu (2 cols, Cancel, pick → Quit); Done restores Start; Exit quits |
| cfg bindings in `SetUpConfigs()` / module `*Config.Initialize()` | Smoke + regenerate `CONFIGURATION.md` |

## Smoke pass (every build)

1. Deploy the DLL, start the game, load a save.
2. Check `BepInEx/LogOutput.log`: plugin banner present, no exceptions, no
   `RunNoREroModCompatibilityProbe` warnings, no Harmony patch failures.
3. Move, jump, attack, take damage; open the menu; transition one scene.
4. Save and reload the slot.

## H-scene / player recovery pass

The highest-risk area. For each tested enemy or trap:

1. Enter the H-scene 5 times in a row.
2. Struggle / complete QTE input during each run; escape via QTE at least
   twice.
3. After each escape verify full recovery:
   - player visible, can move/jump/attack;
   - no lingering partial H-state (`eroflag`/`erodown` back to normal);
   - no leftover dialogue bubbles (cleared via
     `DialogueFramework.DismissAllVisible`);
   - camera, background/fade, and timescale restored.
4. Re-enter immediately after escape at least once.

Expected: no invisible player or scene, no frozen controls, no soft lock.

Prioritize the historically fragile scenarios first:

- tentacle scenes (`Tentacle`, `Trap_TentacleIronmaiden*`);
- multi-phase looping scenes (Goblin, Vagrant, Undead, Pilgrim, Mutude);
- handoff/gangbang chains (Inquisition variants, Kakasi, Bigoni, Dorei):
  verify each phase transition and final release;
- HellTraps death clips followed by vengeance shock and respawn.
  With an enemy standing next to WebSpike (`lethal_cocoontrap`): during the
  PNG clip the enemy must not attack or grab; AI restores after Take
  Vengeance. Same freeze expectation for magic / lightning lethal clips.
  See [HELL_TRAPS.md](../modules/HELL_TRAPS.md).
- DeadArmor: break SlaveBigAxe / OtherSlavebigAxe NikuArmor (physical and
  magic) and confirm the PNG clip + sound; while armored on SlaveBigAxe,
  trigger grab-via-attack and confirm hold → knockback (no H snap), then
  after armor break confirm normal grab/H returns.
- Illusive / Rodenia (`EvuChurch`): win path unchanged; lose → H → fade →
  `EvuChurchSP` loads (log `[Illusive Lose]`). No Church marker / MeatArmor on
  the scripted pair during the event. See
  [ILLUSIVE_RODENIA_EVENT.md](../modules/ILLUSIVE_RODENIA_EVENT.md).
- EnemyFatality: White Inquisitor grounded hit → **Lost_head** (`lost_head`)
  PNG + scarlet blink + FatalityDeathIcon + SFX; guard / airborne skip; after
  Take Vengeance confirm player visible (no red tint) and AI restored.
  During the fatality clip: no enemy grab and no lethal trap death stacking
  (e.g. WebSpike). After last frame + `TauntDelaySeconds`: taunt appears
  above defeat text (muted profiles stay silent). FatalityDeathIcon sits on
  the QTE WASD row screen point (−70px). Same Lost_head roster includes
  Goblin / GobBigAlter / GobRider. CrawlingCreatures uses `StillAlive`
  clip (flip by facing, +1.5 Y, killer hidden) and
  `phrases_CrawlingCreatures.json` (not shared bisect lines). See
  [ENEMY_FATALITY.md](../modules/ENEMY_FATALITY.md).

Escape must flow through the existing cleanup patches
(`HSceneEscapeStateCleanup`, `TimeScaleResetOnEscapePatch`,
`PlayerCombatControlRecovery`, `StruggleEscapeCombatRecoveryPatch`) — see
[COMPATIBILITY.md](COMPATIBILITY.md). If your change added a new escape or
recovery path, that is the bug.

## Guarded vanilla flows

The guards in `Patches/Player/` encode fixes for real soft locks
(guard-by-guard map: [PLAYER_GUARDS.md](PLAYER_GUARDS.md)). After
touching them (or player state logic broadly), verify each affected flow —
and repeat the pregnancy-related ones while pregnant:

| Flow | Guard(s) |
|------|----------|
| Altar interaction and respawn | `VanillaAltarCatalog`, altar-related pregnancy guards |
| Cutscene / story-event input | `VanillaCutsceneSceneGuard`, `VanillaStoryEventInputGuard` |
| Additive EV scene exit | `VanillaEvSceneExitPatch` |
| Knockdown recovery | `VanillaKnockdownRecoveryPatch` |
| Death while downed | `DownedDeathGuard` |
| Birth recovery mid-struggle | `BirthRecoveryJigoPatch`, `BirthRecoveryStruggleState`, `PregnancyBirthGuardPatch` |

## Scene transition pass

1. Walk through at least three scene transitions in both directions.
2. Enter an additive EV scene (story event) and exit it.
3. Verify after each transition: spawn pack content appears (enemies, traps,
   pickups), no duplicate spawns, no orphaned HellGate objects, caches reset
   (no stale references in the log).
4. Die and respawn at an altar; verify the scene reloads with correct spawns
   and the gold lost pile appears in the death scene.

## Persistence pass

All per-slot state lives in JSON files written through the game's save/load
hooks (full list in [DATA_FORMATS.md](DATA_FORMATS.md)). For the module you
touched:

1. Change the state in-game (gain rage, change reputation, get pregnant,
   gain/lose gold).
2. Save to a slot, quit to the main menu, reload the slot — state must match.
3. Restart the game entirely and reload — state must still match.
4. Load a *different* slot — state must not leak between slots.
5. Delete the module's slot file and load — module must fall back to
   defaults without exceptions.

## Module spot checks

- **QTE/Struggle** — button layout matches cfg (`ButtonPositionX/Y`), window
  duration honors cfg after restart, potion escape works when enabled.
- **Rage** — gain, tier activation, Tier-3 readiness, slow-mo enters *and
  exits*, HUD meter matches state after reload.
- **MindBroken** — accumulation, recovery path, visual effects toggle off
  cleanly, bad-end path triggers at threshold when enabled. At 100%,
  minimize / Alt+Tab must not dump the pause into the countdown (no instant
  Bad End on restore; see `MindBrokenRealtimeGate`).
- **Gore Content** — Options → checkbox off: no DeadArmor death clips, no
  lethal CustomDeath traps, no EnemyFatality combat fatalities; armored
  grab-throw still works if enabled.
- **Difficulty presets** — Options → Easy/Medium/Hard: both active cfg files
  match the preset folder; `HellGateDifficulty.selection` updated; values
  apply only after full restart (not mid-session). Language / Gore prefs
  survive the copy. EASY HP/QTE numbers match
  `DIFFICULTY_PRESETS_GUIDE.txt`.
- **Language (Options)** — submenu Cancel returns; picking another language
  writes `HellGateLanguage` and quits; relaunch shows new locale on splash.
- **Potion escape** — with `allowPotionEasyEscape` (enabled in all presets),
  HP potion / Q breaks struggle as documented.
- **Factions** — inter-faction combat still targets correctly, player
  provocation (including by magic) shifts reputation, Mercy/deescalation
  triggers on dodge in combat only.
- **Economy** — combat/knockdown/death gold loss, lost-pile recovery in the
  death scene after respawn, HUD gating.
- **Pregnancy** — conception, trimester progression and visuals, birth
  recovery (see guarded flows), shelter attack trigger, offspring behavior.
- **Spawn/EventCore content** — after editing packs or JSON, reload the
  affected scene and check the log for parse warnings; malformed lines must
  be skipped with a warning, never crash the load.
- **Spawn System Editor V2.0 (F11)** — with `AuthoringUiEnable` on: toggle
  F11; banner reads **Spawn System Editor V2.0**; set Point; Place an
  enemy and a trap (`trapnormal` must write
  `X,Y,trapnormal,1[,rot90][,sort±]`, not `TRAP,Trap_hari,…`); Place a
  Decorations crate (`DECOR,…`); Hostage & OtherScenes (`HOSTAGE,…` or
  `DECOR,Look_Dorei` / `gob_look`); Hostage Random (`RANDOM_HOSTAGE`);
  Gold (`GOLD,…`); EventTrap **M** (`EVENTTRAP,folder,X,Y` — RMB reload);
  EventCore **C** (not Enemies Options): Place writes
  `TouzokuNormal|faction=eventcore_encounter|ec_event=<id>,1` with **no**
  `|ec_chance=` at p=1; F11 preview always has the host; RMB reload still
  has the host; walking next to the NPC **must not** open the modal while
  F11 is on; after F11 off, approach ~3.5 starts the event. Favorites ★
  from any catalog (kind `eventcore` for EventCore). Lethal tab only the
  three lethal keys. F1 Help (one command per line); Home camera; WASD /
  MMB move the camera; Alt nearby-pick; gold piles pick on the sprite,
  not the circle halo; Ivy / trap triggers stay clickable. LMB select a
  neighbor without stealing the other object; Edit rotation +30 / sort;
  EventCore Edit keeps `|ec_event=`; Save; Delete; Ctrl+C/X/V/D; RMB
  reload and confirm links still resolve (pasted EventCore lines keep the
  host). Trap list must not show decor / lethal aliases / `help`. Lethal
  **placement** tab is placement-only; lethal death / Gore checks belong
  to the HellTraps pass. Screenshots (F12) write under
  `HellGateScreenshots/`. See [SPAWN.md](../modules/SPAWN.md) and
  [EVENT_CORE.md](../modules/EVENT_CORE.md).
- **HellTraps** — death clips, combat freeze, vengeance shock: see the
  HellTraps row in the H-scene pass and [HELL_TRAPS.md](../modules/HELL_TRAPS.md).

## Config regression

Settings are read once at startup: every cfg check requires a full game
restart. When reports claim "cfg ignored", first verify the right file was
edited — HellGate reads `NoREroMod_HellGate.cfg`, while base enemy scaling
stays in `NoREroMod.cfg`.

## Reporting template

Record this when filing or fixing a regression:

- Enemy/trap and map/location;
- trigger method (normal grab / trap / handoff);
- reproduction rate (e.g. 2/10);
- last visible animation phase;
- input at the failure moment (QTE, key held, give-up);
- result (scene invisible, player invisible, no movement, stuck timescale);
- relevant `BepInEx/LogOutput.log` excerpt.

If a bug persists, enable the diagnostic kits for the affected area, capture
logs, and disable them after verification — see
[DIAGNOSTICS.md](DIAGNOSTICS.md).
