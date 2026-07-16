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
| `Systems/Spawn/`, spawn packs | Scene transition pass |
| Save/load hooks, slot stores | Persistence pass |
| `Systems/Pregnancy/` | Pregnancy checks + persistence pass |
| `Systems/CombatAi/`, factions | Faction checks |
| `Systems/Economy/`, `Systems/Rewards/` | Economy checks + persistence pass |
| `NoREroModScaffoldConfigPush`, reflection into NoREroMod | Smoke + startup probe log |
| cfg bindings in `SetUpConfigs()` | Smoke + regenerate `CONFIGURATION.md` |

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
  cleanly, bad-end path triggers at threshold when enabled.
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
