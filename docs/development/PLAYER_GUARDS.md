# Player Guards

`Patches/Player/` is the safety layer around the vanilla player controller
(`playercon`). Every guard here encodes a fix for a real, reproduced failure —
usually a soft lock. This document maps each guard to the failure it prevents,
so nobody removes or "simplifies" one without understanding what comes back.

Two execution models are used:

- **Per-frame guards** run from `PlayerConUpdateDispatcher`, the single
  postfix on `playercon.Update` (see `ARCHITECTURE.md`). Order inside the
  dispatcher matters.
- **Event guards** are ordinary Harmony patches on specific vanilla methods.

Any change in this directory requires the H-scene recovery and guarded-flow
passes from [TESTING.md](TESTING.md), including the pregnancy variants.

## H-scene escape and recovery chain

Escape from an H-scene must flow through this chain; adding a parallel escape
path is the classic way to reintroduce every bug below at once.

| Guard | Failure it prevents |
|-------|---------------------|
| `HSceneEscapeStateCleanup` | Stuck black background, running MindBroken overlay ticks, invisible player Spine, and frozen `timeScale == 0` after Bad End Vengeance or struggle escape. Central cleanup: stops overlay systems, restores combatant visuals, clears stale down-state, dismisses dialogue bubbles. |
| `TimeScaleResetOnEscapePatch` | Permanent 0.2x slow motion. Enemies set `timeScale = 0.2` at H-scene start and schedule an `Invoke` to restore it; a fast escape deactivates the ERO object before the `Invoke` runs. Detects the `eroflag` true→false edge per frame and forces `timeScale` back to 1. |
| `PlayerCombatControlRecovery` | Player escapes but cannot attack: many vanilla ERO `Start()` paths set `PlayerStatus._SOUSA = false` and never restore it, and `atk_fun` ignores attack input without it. Also routes the escape edge into the right cleanup: full cleanup normally, visuals-only when `erodown != 0` (handoff intentionally leaves the heroine lying down — full cleanup would make her pop upright). Skips additive EV scenes and solo-pleasure states, which legitimately hold control. |
| `StruggleEscapeCombatRecoveryPatch` | Runs with `Priority.Last` after all type-specific escape postfixes on `StruggleSystem.startGrabInvul`, so combat recovery happens once, after every other cleanup — and never during a pregnancy-birth overlay. |
| `StruggleInvulnPatch` | Two fixes on the same hook: extends post-escape invulnerability by 2 s (instant re-grab chains), and resets `EnemyHandoffSystem` global state so the next enemy starts its scene from the beginning instead of resuming mid-animation. |
| `PlayerEnemyGrabStruggleSupport` | Unescapable grabs: vanilla struggle and HellGate QTE both require `_SOUSA` during a grab, but many ERO paths and field bosses leave it false, silently eating struggle input. Re-arms `_SOUSA` per frame during grabs, while respecting intentional locks (EventCore consent locks, birth recovery). |

## Vanilla flow guards (scene, cutscene, input)

| Guard | Failure it prevents |
|-------|---------------------|
| `VanillaAltarCatalog` | Not a patch but the shared altar registry (savepoint token → scene → coordinates, sourced from `Savepoint_menu.place_move*`). Prefers `_re_savepoint` over `_re_Scenename`, because the raw scene name can leak from ordinary walking. Both guards below depend on it. |
| `VanillaCutsceneSceneGuard` | Void fall after cross-zone Take Vengeance: vanilla `Restart` applies checkpoint coordinates in the *current* zone even when the owning altar is in another map. Forces a real scene load instead. Also classifies Insomnia-bar EV scenes correctly (`InsomniaTownB` is normal gameplay, not an EV) so spawn refresh does not misfire. |
| `VanillaEvSceneExitPatch` | Same void-fall class of bug on the EV-scene exit path (`REstrat` calls `savepoint()` in the current zone while `_checkpoint` targets another map): schedules the altar-map load when leaving the bar EV. |
| `VanillaStoryEventInputGuard` | Sword swings and combat input during story cutscenes: vanilla story EVs reuse `eroflag` and hide the HUD without clearing `_SOUSA`, so the click that advances dialogue leaks into Rewired Attack. Distinguishes "fake" story-event `eroflag` from a real enemy H-scene and suppresses combat input only for the former. |

## Knockdown and death

| Guard | Failure it prevents |
|-------|---------------------|
| `VanillaKnockdownRecoveryPatch` (+ `VanillaKnockdownRecoveryUtility`) | Player stuck lying on the ground: if any patch zeros `erodown` without running the vanilla stand-up physics, re-applies `act_downup` and `vspeed` after `fun_nowdamage`. Pure safety net around other mods'/patches' knockdown handling. |
| `DownedDeathGuard` | Zombie state at 0 HP: vanilla lets a downed player stand up whenever `_SOUSA` is set and struggle SP fills, checking neither HP nor `_Death`. A non-combat HP drain (no lethal `fun_damage` block) leaves `_Death` false, so the player "stands up" dead with no control. Forces a clean `SpDeath`, while deferring to bespoke sequences (EventCore pause, MindBroken bad end, lethal-trap cleanup) that drive HP themselves. |

## Pregnancy birth

Birth is a vanilla overlay sequence with its own escape lock; these three
guards keep HellGate's struggle systems from tearing it apart. All birth flows
must be regression-tested while pregnant (see TESTING.md).

| Guard | Failure it prevents |
|-------|---------------------|
| `PregnancyBirthGuardPatch` | Birth aborted mid-sequence: locks `_easyESC` for the whole birth so HellGate struggle prep cannot clear it (mash-escape would skip the sequence), keeps the main body hidden behind the birth overlay, and stops a premature `erodown = 0` from cancelling the birth. |
| `BirthRecoveryJigoPatch` | Input lock after birth: vanilla clears `_easyESC` on the Spine `JIGO` event but leaves the player in a state where standing is free or impossible. Hands off to the struggle state below at exactly that Spine event (both `BadstatusBirthMonster` variants). |
| `BirthRecoveryStruggleState` | Defines the post-birth recovery contract: SP resets and the player must struggle to full SP before standing. Deliberately does **not** re-lock `_easyESC`, because that would block NoREroMod/QTE input entirely. |

## Save data and cosmetics

| Guard | Failure it prevents |
|-------|---------------------|
| `EnemyLibraryEroStatusGuardPatch` | Frozen H-scene at START6 with Kinoko: old or truncated saves can load `_EnemyLibraryEROstatus` smaller than vanilla's `float[70,10]`; writing LibraryID 59 then throws `IndexOutOfRangeException` inside `Library_rape`, aborting `MushroomERO.OnEvent`. Grows the array (keeping `StaticMng` in sync) before any write. |
| `StrugglePotionEscapePatch` | Potion escape (Q) drinking the wrong potion: escape logic itself stays in NoREroMod's `PlayerConPatch`; HellGate only sets `_SOUSA` before `fun_nowdamage` (an Update postfix is too late), blocks vanilla `Item_use` during struggle so MP is not consumed after Q, and resyncs the potion HUD. Do not duplicate escape logic here. |
| `PlayerHitBloodCleanupPatch` | Blood particles permanently following the player: with HellGate loaded, vanilla `Blood7_*` sub-emitters can leave local-space dots parented to the player after damage. Schedules a cleanup pass after both damage entry points. |
| `GuardParryMindBrokenPatch` | Not a soft-lock guard — the gameplay hook granting Rage on perfect block (+1.5%) and Rage/MindBroken adjustment on parry (+5% / −1%). Lives here because it detects block/parry through `playercon` internals (`Acttext("PARRY!!")`, `guradcount`). |

## Rules for changing this layer

- Extend the existing cleanup chain; never add a second escape path.
- Per-frame logic goes through `PlayerConUpdateDispatcher` — no new
  `playercon.Update` patches.
- Before relaxing a guard condition, reproduce the original failure with the
  guard disabled; the incident is the specification.
- Every guard must defer to intentional locks (EventCore pause/consent,
  MindBroken bad end, birth overlay, lethal-trap death) — a guard that
  "recovers" the player out of a scripted sequence is itself a bug.
