# DeadArmor

Isolated presentation module for **SlaveBigAxe** / **OtherSlavebigAxe** girl
armor (`NikuArmor`): PNG break clips plus an armored grab-throw that replaces
GrabViaAttack's H-scene snap while the armor is still up.

Code: `Systems/DeadArmor/` · Config: `[DeadArmor]` · Assets: external
`sources/HellGate_sources/DeadArmor/` (not shared with HellTraps)

## Scope

| Enemy | Armor-break clip | Armored grab-throw |
|-------|------------------|--------------------|
| `SlaveBigAxe` | yes | yes |
| `OtherSlavebigAxe` | yes | no |

The module does **not** share state or loaders with `Patches/HellTraps/`.
Clip playback uses unscaled time so hit-stop / slow-mo do not stall the overlay.

`Enable` controls clip + sound preload/playback. `ArmoredGrabThrowEnable`
controls the grab-throw path independently — sensitive players can turn
`Enable` off and keep the throw.

Clip playback also requires `General.EnableGoreContent` (splash **HellGate
Gore Content**). `DeadArmorConfig.IsEnabled` is `Enable && IsGoreContentEnabled`.
Armored grab-throw does **not** require gore.

## Armor-break clip

On physical/stab or magic damage that clears `NikuArmor` (flag and/or Niku
skin), `DeadArmorPatches` snapshots before the hit and `DeadArmorPlayback`
spawns a one-shot PNG sequence:

- frames from `PhisicDamage/` or `MagicDamage/` (`w1.png` …);
- optional random death WAV from `DeathSounds/`;
- spawn on a Spine bone (`BoneName`, default `bone2`), flipped by enemy `DIR`;
- fall distance from `FallDistances` (random pick), speed via
  `FallSpeedMultiplier`, frame timing via `FrameSeconds`, last-frame hold via
  `HoldLastFrameSeconds`.

Paths resolve relative to the game root (same candidate pattern as other
HellGate asset loaders). Override folders with `PhysicalClipPath` /
`MagicClipPath` / `DeathSoundsPath`.

### Clip defaults (release tuning)

| Key | Default | Notes |
|-----|---------|-------|
| `FallDistances` | `0,0.1` | Random pick each break; `0` = almost no drop |
| `FallSpeedMultiplier` | `4.5` | Reaches fall end in ~1/4.5 of clip length |
| `FrameSeconds` | `0.087` | ~11.5 FPS |
| `HoldLastFrameSeconds` | `10` | Last frame linger, then destroy |
| `DisplayScale` | `1` | World scale |
| `SortingOrder` | `80` | Fallback if enemy has no `MeshRenderer` |

## Armored grab-throw (SlaveBigAxe only)

While `NikuArmor` is active, `GrabViaAttackPatch` calls
`SlaveBigAxeArmoredThrow.TryThrowInsteadOfGrab` instead of
`EliteGrabPlayer`:

1. Optional dedicated slow-mo (`ArmoredGrabThrowSlowmo*`) — **no** camera zoom;
   independent of GrabViaAttack / StartZoom slow-mo gates.
2. Short hold: pull beside the slave (`HoldPull`) and lift (`HoldLift`).
3. Vanilla knockdown pipeline: `ImmediatelyERO` + `damedir` + private
   `nockbackspeed` so `fun_nowdamage_move` drives the slide; SP drained so the
   player cannot stand immediately.
4. `UpVelocity = 0` keeps a flat horizontal throw (gravity briefly zeroed
   during the slide).

After armor breaks, normal grab-via-attack / H flow returns.

### Throw defaults (release tuning)

| Key | Default | Notes |
|-----|---------|-------|
| `ArmoredGrabThrowAwayVelocity` | `24` | Horizontal `nockbackspeed` (vanilla KO ~16) |
| `ArmoredGrabThrowUpVelocity` | `0` | Flat throw |
| `ArmoredGrabThrowHoldSeconds` | `0.7` | Hold before throw |
| `ArmoredGrabThrowHoldPull` | `0.85` | Pull beside slave |
| `ArmoredGrabThrowHoldLift` | `0.5` | Lift during hold |
| `ArmoredGrabThrowSlowmoTimeScale` | `0.7` | 70% speed |
| `ArmoredGrabThrowSlowmoDuration` | `0.7` | Real seconds |

## Asset layout

```text
sources/HellGate_sources/DeadArmor/
  PhisicDamage/     w1.png … (physical / stab break)
  MagicDamage/      w1.png … (magic break)
  DeathSounds/      *.wav (random pick)
```

Folders are outside the git tree; ship them with the game install or point
path overrides at custom folders.

## Code map

| Type | Role |
|------|------|
| `DeadArmorConfig` | `[DeadArmor]` bindings + `PickFallDistance` / `LogDebug` |
| `DeadArmorBootstrap` | preload sprites + audio after patches |
| `DeadArmorPatches` | Harmony on `getdame_fun` / stab / magic damage |
| `DeadArmorPlayback` | bone resolve, sorting, spawn clip |
| `DeadArmorClipPlayer` | unscaled frame + fall MonoBehaviour |
| `DeadArmorSpriteCache` / `DeadArmorAudio` / `DeadArmorPaths` | assets |
| `SlaveBigAxeArmoredThrow` | hold → knockback; hooked from GrabSystem |

Init order (see `ARCHITECTURE.md`): config in `Awake` with other module
configs; patches via `DeadArmorPatches.Apply` in `SetUpPatches()`; bootstrap
(preload) immediately after HellTraps preload.

## Related

- Separate combat fatalities: [ENEMY_FATALITY.md](ENEMY_FATALITY.md)
- Grab entry point: [GRAB_AND_HANDOFF.md](GRAB_AND_HANDOFF.md)
- Separate lethal overlays: [HELL_TRAPS.md](HELL_TRAPS.md)
- External asset root: [DATA_FORMATS.md](../development/DATA_FORMATS.md)
- Settings: [CONFIGURATION.md](../development/CONFIGURATION.md) `[DeadArmor]`
  and `[General] EnableGoreContent`
