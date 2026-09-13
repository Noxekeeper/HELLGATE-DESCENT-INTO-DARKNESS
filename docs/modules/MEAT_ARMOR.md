# MeatArmor (SlaveBigAxe post-fade swap)

Post-fade presentation for **SlaveBigAxe** / **OtherSlavebigAxe** H-scenes:
after vanilla `JIGOTOFADE` / `ZGAMEOVER`, HellGate fades again, replaces the H
Spine with disk-loaded **Aradia MeatArmor**, then runs an `EROWALK` patrol until
struggle escape.

Code: `Patches/Enemy/SlaveBigAxe/MeatArmor/`  
Config: `[SlaveBigAxeMeatArmor]`  
Assets: `sources/HellGate_sources/SlaveBigAxeSource/` (external; not in git)

Related but separate: H floating dialogue (`Systems/Dialogue/SlaveBigAxeHSceneDialogues.cs`)
and combat **DeadArmor** (`Systems/DeadArmor/` — NikuArmor break clips / armored throw).

## Why a skeleton swap

The H skeleton (`s_DOREIBIG_ERO`) has no usable `EROWALK`. The church-event
asset `s_DOREIBIG_Aradia_armor` provides walk / idle / FIN plus Niku skins.
HellGate loads that trio from disk (Wolf-style) and swaps `myspine` for the
patrol window only.

## Flow

```
JIGOTOFADE | ZGAMEOVER  (vanilla still visible)
    → wait 5s
    → fade to black (~1.2s)
    → swap SkeletonDataAsset + random Niku skin + scale ×0.85
    → play EROWALK, start X patrol + camera follow
    → fade in
    → patrol until player erodown == 0 (struggle escape)
    → Prefix on SlaveBigAxe / OtherSlavebigAxe.eroanime:
         restore H asset, rebind OnEvent, move player/enemy to walk-end X
    → vanilla ero_camerareset / erodata disable (normal H exit)
```

### Patrol

| Behavior | Value |
|----------|--------|
| Speed | `1.1` world units / s |
| Route | random distance `{8,10,12,15,20}` out, then back to anchor |
| Stop | `5s` at each arrival |
| Odd stops | `IDLE` (loop) |
| Even stops | `FIN` once → queued `IDLE` |
| Mix | Spine `DefaultMix` ≈ `0.22s` |
| Visual scale | `0.85` × pre-swap magnitude |
| Skins | `Niku_JIGO`, `Niku_JIGO2`, `Niku_NORMAL` (random; not empty `Normal`) |

Facing: world `lossyScale.x` sign follows move `Dir`, compensating parent combat-body
flip so a second H after escape does not moonwalk. `Skeleton.FlipX` stays false.

### Camera

- Mute existing ProCamera2D target influences (restore on stop).
- Freeze `ProCamera2DZoomToFitTargets` (keep current zoom).
- Each `ProCamera2D.Move` postfix: snap to MeatArmor transform.
- Do **not** call `fun_cameramove` on escape — vanilla `ero_camerareset` owns combat cam.

### Escape invariants

`SkeletonAnimation.Initialize(true)` recreates `AnimationState` and drops
`myspine.state.Event += OnEvent` from `Start()`. Cleanup **must** rebind that
handler or the next grab freezes on the last frame of `START`.

While swapped, vanilla `OnEvent` is suppressed (Prefix) so FIN SE ticks cannot
restart H state machines, and `SlaveBigAxePassLogic` skips dialogue.

### Illusive / Rodenia event

Combat `SlaveBigAxe` in the Illusive church event must **not** use MeatArmor
(or other HellGate combat overlays). Full isolation + vanilla lose→`EvuChurchSP`
handoff: [ILLUSIVE_RODENIA_EVENT.md](ILLUSIVE_RODENIA_EVENT.md).

## Assets

Required files under AssetsPath (default
`sources/HellGate_sources/SlaveBigAxeSource`):

- `s_DOREIBIG_Aradia_armor.json`
- `s_DOREIBIG_Aradia_armor.atlas`
- `s_DOREIBIG_Aradia_armor.png`

Loader builds a runtime `SkeletonDataAsset`, registers it in `CustomAssets`, and
Harmony-prefixes `GetSkeletonData` / `GetAnimationStateData` (same pattern as Wolf).

Material template is taken from a live/prefab `SlaveBigAxe` Spine atlas when
possible.

## Config

| Section | Key | Default | Notes |
|---------|-----|---------|--------|
| `[SlaveBigAxeMeatArmor]` | `AssetsPath` | empty | Relative to game root, or absolute. Empty → default folder above. |

## Code map

| File | Role |
|------|------|
| `SlaveBigAxeMeatArmorRuntime.cs` | Sequence, patrol, camera, escape cleanup, Harmony hooks |
| `SlaveBigAxeMeatArmorSkeletonLoader.cs` | Disk load + `SkeletonDataAsset` prefix patch |
| `SlaveBigAxeMeatArmorTextureLoader.cs` | Atlas PNG → Material |
| `SlaveBigAxePassLogic.cs` | Skips H dialogue while `IsSwapped` |
| `SlaveBigAxeIllusiveEventGate.cs` | See [ILLUSIVE_RODENIA_EVENT.md](ILLUSIVE_RODENIA_EVENT.md) |
| `Core/Plugin.cs` | Config bind, BootPatch, `sceneLoaded` → `ResetAll` + `ResetCache` |

## Testing checklist

- Reach fade (`JIGOTOFADE` or `ZGAMEOVER`) → MeatArmor appears after second fade.
- Random Niku skin; walks forward (not moonwalk) on first and second full H cycles.
- Camera follows walker; zoom stays put.
- Struggle escape → combat camera/controls; player/enemy near walk-end X.
- Re-grab → `START` advances to `ERO` (OnEvent rebound).
- Scene change clears runtime + disk cache without leftover follow targets.
