# HellTraps

Lethal trap content pack: two trap families that kill the player and play a
custom PNG death clip, followed by a vengeance shock sequence.

Code: `Patches/HellTraps/` · Config: `[HellTraps]` · Assets: external `HellTraps/` death clips + audio

## Trap families

Both families share the same structure — runtime, registry/template
registration, asset loader, paths, Harmony patches, death context, death
tuning, death display:

- **LethalMagicTrap** — projectile-based; adds `LethalMagicTrapBulletMarker`
  (bullet ownership) and `LethalMagicTrapEroSuppression`, which reflects into
  NoREroMod `EnemyDatePatch` to suppress the ERO path during a lethal hit.
- **LethalCocoonTrap** — contact-based; adds scene markers and
  `HellGateLethalCocoonTrapTracker`.

Trap templates are registered into the spawn template catalog during plugin
`Awake` (before patch registration) and placed through `TRAP` /
template lines in spawn packs (see `SPAWN.md`).

## Shared death infrastructure

`LethalTrap*` types are common to both families:

- `LethalTrapHitGate` — decides whether a hit is lethal;
- `LethalTrapDeathCommon` + `LethalDeathClipPlaybackProfile` +
  `LethalTrapDeathSpriteLoader` — PNG clip playback;
- `LethalTrapDeathBlackScreen`, `LethalTrapDeathCleanup` — presentation and
  state cleanup;
- `LethalTrapHeartBeatLoop` — danger heartbeat audio;
- `LethalTrapDangerThoughts` + `LethalTrapThoughtPhrases` — proximity warning
  thoughts.

## Vengeance shock

After a lethal trap death, the vengeance respawn plays a shock sequence:
`LethalTrapVengeanceShockSession` + tuning + audio, and
`LethalTrapVengeanceMindBrokenShock` applies a MindBroken hit
(see `MIND_BROKEN.md`).

## Extension notes

To add a trap family, mirror the family structure (paths → loader → registry
→ runtime → patches → death context/tuning/display) and reuse the shared
`LethalTrap*` death kit rather than duplicating it. Register templates in
`Awake` before `SetUpPatches()` so spawn packs can reference them.
