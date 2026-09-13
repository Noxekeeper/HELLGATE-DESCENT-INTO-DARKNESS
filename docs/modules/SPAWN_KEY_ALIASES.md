# Spawn key aliases

F11 lists **one canonical key per prefab**. Old / compact / typo names still
work in pack txt. Do not add them back to the Trap picker.

## Trap

| Canonical (F11) | Aliases (txt only) |
|-----------------|--------------------|
| `trapthreadofspider` | `trapspider` |
| `woodwana` | `wana_start`, `wanastart`, `forest_long`, `forestlong` |
| `trap_button` | `trap_button_ironmaiden`, `trapbutton`, `trapgun` … `trapgun6` (same cached assembly as `trapshot` on many maps) |
| `trapshot` | `trapgun2` |
| `spearthrowtrap` | `spear` |
| `magictrap` | `firecharge` |
| `magictrapcreateobject` | `magic2` |
| `picturetrap` | `picture_trap` |
| `ivy_trap` | `ivytrap` |
| `ivy_monster` | `ivy`, `ivymonster` |
| `blackoozetraptypeb` | `blackoozetypeb` |
| `trapnormal` | `trap`, `trap_hari`, `traphari` — same `Trap_hari` prefab. Use `trapnormal`. |
| `spiketrap` | `Spike Trap`, `spike trap` |
| `impactdamage` | `lightimpactnormal` |
| `ironmaidendamage` | `ironmaiden_damage`, `trap_ironmaiden` |
| `tent_ironmaiden` | `trap_tentacleironmaiden` |
| `trapmachine` | `trap_machine` |
| `trap_mokubaenemy` | `trap_rockinghorse` |
| `pictureeronon` | `pictureero_non` |

`impactdamagebox` is a **different** prefab (`body` collider), not an alias of `impactdamage`.

## Lethal (F11 Lethal tab only)

| Canonical | Aliases |
|-----------|---------|
| `lethal_magictrap` | `lethalmagictrap`, `letal_magictrap`, `letalmagictrap` |
| `lethal_cocoontrap` | `lethalcocoontrap` |
| `lightningTrap_button` | `lethallightningbutton`, `lightingTrap_button`, `lightningtrap_button` |

## Decorations

| Canonical | Aliases |
|-----------|---------|
| `box_brk` | `boxbrk` |
| `movebox_brk` | `moveboxbrk` |
| `taru_brk` | `tarubrk` |
| `taru_exp` | `taruexp` |
| `taruro_exp` | `taruroexp` |
| `wall_stone1` | `wallstone1` |
| `cow1` | ranch cow prop (Decorations). Spine scenes: Hostage `CowSlavespine` |
| `mob_death1` | `mobdeath1` |
| `mob_death2` | `mobdeath2` |

## Hostage & OtherScenes (F11 Hostage tab)

| Canonical (F11) | Aliases (txt only) |
|-----------------|--------------------|
| `gob_look` | `goblook` |
| `gob_look2` | `goblook2` |
| `Look_Dorei` | `lookdorei` |
| `Look_mutude` | `lookmutude` |
| `mob_sister` | `mobsister` |
| `CowSlavespine` | `cowslavespine` |
| `ChainCowSlavespine` | `chaincowslavespine` |

`mob_death1` / `mob_death2` stay in Decorations (static corpses).

`breakobjct` / `taruro_exp_layer` / `taruroexplayer` bind to the **huge**
`taruRO_exp_Layer` — do not pick; use `taruro_exp` or `taru_exp`.

## Enemies (not Trap)

| Use this | Not a trap |
|----------|------------|
| `CocoonmanStart` | `cocooncreat`, `Cocoonman` (registry label — often fails) |

## Hidden from F11 (broken / nothing)

`help`, `meatshieldhelp`, `npcslaveenable`, `npcslaveflag`,
`wavespike`, `wavespikeguard` (do not work as placed traps),
generic tokens `slave` / `stand`,
and scene child names (`spine gameobject (tyoukyousi_spineero)`,
`mob_death1 (1)`, `Arrow (1)`, …).
`chaincowslavespine` / `cowslavespine` are hidden from Trap; they appear
under Hostage & OtherScenes.
