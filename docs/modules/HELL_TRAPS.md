# HellTraps

Lethal trap content pack: trap families that kill the player and play a
custom PNG death clip, followed by a vengeance shock sequence.

Code: `Patches/HellTraps/` · Config: `[HellTraps]` · Assets: external
`sources/HellGate_sources/CustomDeath/` death clips + audio

## Trap families

Families share the same structure — runtime, registry/template
registration, asset loader, paths, Harmony patches, death context, death
tuning, death display:

- **LethalMagicTrap** — projectile-based; adds `LethalMagicTrapBulletMarker`
  (bullet ownership). Spawn key: `lethal_magictrap` (alias `lethalmagictrap`).
  Death PNG: `Exp_Death` (bone → floor fall profile).
- **LethalCocoonTrap** (WebSpike) — contact-based; adds scene markers and
  `HellGateLethalCocoonTrapTracker`. Spawn key: `lethal_cocoontrap`
  (alias `lethalcocoontrap`). Death PNG: `WebSpike_Death` (same shared
  playback runner as magic; cocoon tuning / trap-floor offset).
- **LethalLightningTrap** — `trap_button` trigger; warning `TargetIcon_4` at
  trap coords → delay → `Lightningstrike` VFX always fires at the trap;
  lethal ATK + death clip only if the player is still inside the button
  trigger. Death PNG (`LightningFatalDead`) is trap-anchored (not player
  bone) with the shared black-screen timing. Spawn key:
  `lightningTrap_button` (alias `lightingTrap_button`). Cooldown then
  re-arms.

All three families share **`LethalMagicTrapEroSuppression`** for the
death-clip session (name is historical; see below).

Trap templates are registered into the spawn template catalog during plugin
`Awake`, after `SetUpPatches()` and before scene spawn execution.

### Placement (owned by Spawn)

Pack lines and F11 Place only set **position / facing / rotation / depth**.
Syntax and authoring UI live in [SPAWN.md](SPAWN.md) (`TRAP,Key,X,Y,…` plus
optional `flip` / `rot90|180|270` / `sort±N`). Do not duplicate placement
rules here.

Runtime enablement: each family also requires `General.EnableGoreContent`
(`Plugin.IsLethal*TrapActive` = gore AND the family's `[HellTraps]` Enable
flag). With gore off, lethal templates are not placed/active; non-lethal
scene traps are unchanged (and are not part of this module).

---

## Death-clip session (combat freeze)

While any family has EroSuppression active
(`Lethal*TrapDeathContext.IsEroSuppressionActive` — set on pending lethal hit
and while the custom death clip runs):

| Guard | Behavior |
|-------|----------|
| Player pin | Rigidbody zeroed; `erodown` / damage / DOWN states neutralized |
| Collision grab | Reflective prefix on NoREroMod `CanEliteGrabPlayer` → false |
| GrabViaAttack | Early-out when `LethalMagicTrapEroSuppression.ShouldSuppress` |
| Enemy hits | `EnemyDate.OndamageSend("playerDAMAGEcol")` blocked |
| Nearby combat AI | Settle to IDLE, clear look/attack, zero velocity, `enabled = false` |
| AI restore | `RestoreCombatAi` when suppression ends / respawn cleanup |

`ShouldSuppress` covers **magic + cocoon + lightning** (all three contexts).

This matches the EnemyFatality combat-freeze model. Older behavior that only
cleared `EROWALK` / `EROIDLE` left enemies free to attack and grab the corpse
during WebSpike / other lethal clips — that gap is closed.

Upkeep: clip runner `ProcessDuringCustomDeath` + player `Update` fallback while
`HasActiveClip`. Full wipe: `LethalTrapDeathCleanup.ForceCleanupForRespawn`.

---

## Shared death infrastructure

`LethalTrap*` types are common to all families:

- `LethalTrapHitGate` — decides whether a hit is lethal / which family owns it;
- `LethalTrapDeathCommon` + `LethalDeathClipPlaybackProfile` +
  `LethalTrapDeathSpriteLoader` — PNG clip playback;
- `LethalMagicTrapDeathDisplay` — shared clip runner (magic / cocoon /
  lightning display wrappers feed frames + profile);
- `LethalMagicTrapEroSuppression` — pin, grab/hit block, combat AI freeze;
- `LethalTrapDeathBlackScreen`, `LethalTrapDeathCleanup` — presentation and
  state cleanup;
- `LethalTrapHeartBeatLoop` — danger heartbeat audio;
- `LethalTrapDangerThoughts` + `LethalTrapThoughtPhrases` — proximity warning
  thoughts.

Per-family contexts (`LethalMagicTrapDeathContext`,
`LethalCocoonTrapDeathContext`, `LethalLightningTrapDeathContext`) hold
pending / hit / custom-death / suppression flags and trap anchors.

---

## Vengeance shock

After a lethal trap death, the vengeance respawn plays a shock sequence:
`LethalTrapVengeanceShockSession` + tuning + audio, and
`LethalTrapVengeanceMindBrokenShock` applies a MindBroken hit
(see `MIND_BROKEN.md`).

---

## Cross-module notes

**Spawn** places lethal keys only ([SPAWN.md](SPAWN.md)). HellTraps owns
hit gates, death clips, combat freeze, vengeance shock, and `[HellTraps]`
cfg — keep those details in this document.

SlaveBigAxe / OtherSlavebigAxe girl-armor break overlays are a separate
module (`Systems/DeadArmor/`) — see [DEAD_ARMOR.md](DEAD_ARMOR.md).

Combat fatalities live under `Systems/EnemyFatality/` — see
[ENEMY_FATALITY.md](ENEMY_FATALITY.md). Do **not** route those clips through
the HellTraps death kit.

Mutual session immunity:

- While **EnemyFatality** is active: lethal cocoon / magic / lightning hits
  skip applying `fun_damage` / custom death (and grabs are blocked) so trap
  overlays cannot stack on the fatality body.
- While a **HellTrap** death session is active: EnemyFatality-style grab/hit
  blocks above apply via `ShouldSuppress`; fatality combat hits remain a
  separate module.

---

## Extension notes

To add a trap family, mirror the family structure (paths → loader → registry
→ runtime → patches → death context/tuning/display) and reuse the shared
`LethalTrap*` death kit rather than duplicating it. Wire the new context into
`LethalMagicTrapEroSuppression.ShouldSuppress` / knockback gates and
`LethalTrapDeathCleanup`. Register templates in `Awake` after the spawn
template catalog initializes and before any scene spawn pack executes.
Follow the current order in `Plugin.Awake()`.
