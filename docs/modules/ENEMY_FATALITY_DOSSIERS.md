# EnemyFatality — enemy dossiers

Quick reference for enemies wired into the combat-fatality profile catalog.
Module contract: [ENEMY_FATALITY.md](ENEMY_FATALITY.md).

Most enemies randomly pick from the shared bisect pool
(`lost_leg` / `lost_head`) unless `ClipPath` forces one folder. Trigger = any
damaging `playerDAMAGEcol` (then HP / chance / guard / airborne gates).

| Spawn key | C# type | Cfg section | RU | Notes |
|-----------|---------|-------------|----|-------|
| `InquisitionWhite` | `InquisitionWhite` | `[EnemyFatality.WhiteInquisitor]` | Белый инквизитор | Reference profile; pool `lost_leg`/`lost_head` |
| `CrawlingCreatures` | `CrawlingCreatures` | `[EnemyFatality.CrawlingCreatures]` | Ползучее существо | **Clip:** `StillAlive` only. `ClipOffsetY` **1.5**. Killer hidden. Own taunt JSON. Facing flipX. |
| `Bigoni` | `Bigoni` | `[EnemyFatality.Bigoni]` | Большой демон/орк | Excludes HellGate **BigoniBrother** instances |
| `BigoniBrother` | `Bigoni` + brother marker | `[EnemyFatality.BigoniBrother]` | Кастомный мини-босс (слабый брат, HP ~1000, урон ~70%) | Same component as Bigoni; match via `BigoniBrotherIdentity.IsBrother` |
| `BlackOoze` | `BlackOoze_Monster` | `[EnemyFatality.BlackOoze]` | Чёрный слизень (бой) | Prefab `BlackOoze_Monster`. **Not** `BlackOozetrap` / TypeB traps |
| `Cocoonman` | `Cocoonman` | `[EnemyFatality.Cocoonman]` | Человек-кокон | Prefab root often `CocoonmanStart` |
| `Gorotuki` | `Gorotuki` | `[EnemyFatality.Gorotuki]` | Горотуки | Also covers **Demon_gorotuki** skin (same component) |
| `HighInquisitionFemale` | `HighInquisition_famale` | `[EnemyFatality.HighInquisitionFemale]` | Высокий инквизитор (жен.) | Vanilla typo in type name (`famale`) |
| `Minotaurosu` | `Minotaurosu` | `[EnemyFatality.Minotaurosu]` | Минотавр | Bull-headed demon |
| `Slaughterer` | `Slaughterer` | `[EnemyFatality.Slaughterer]` | Мясник | Elite butcher; spawn alias `Butcher` uses same type |
| `SlaveBigAxe` | `SlaveBigAxe` | `[EnemyFatality.SlaveBigAxe]` | Большой раб с топором | Separate from DeadArmor / MeatArmor presentation |
| `TouzokuNormal` | `TouzokuNormal` | `[EnemyFatality.TouzokuNormal]` | Обычный разбойник | Bandit swordsman; prefab object often named `Touzoku` |
| `Goblin` | `goblin` | `[EnemyFatality.Goblin]` | Гоблин | Combat type `goblin` (not H-scene `goblinero`) |
| `GobBigAlter` | `GobBigAlter` | `[EnemyFatality.GobBigAlter]` | Большой гоблин (Alter) | Large goblin variant |
| `GobRider` | `GobRider` | `[EnemyFatality.GobRider]` | Гоблин-наездник | Goblin rider |

## Per-enemy notes

### CrawlingCreatures
Spawn key and type are plural (`CrawlingCreatures`). Default fatality folder is
`sources/HellGate_sources/CustomDeath/Fatality/StillAlive/` (PNG only today —
add `Hit.wav` / `death sound.wav` / `Final.wav` in that folder when ready).
Spawn Y uses `ClipOffsetY` default **1.5**. Killer visuals hidden for the
session (`HideKillerDuringClip`). Mid-clip SFX: `bone-crack.wav` on frame
**15**. Taunts:
`HellGateJson/EnemyFatality/<Lang>/phrases_CrawlingCreatures.json` (all 10
langs). Not the same as `CrawlingDead` / `CrawlingSisterKnight`.

### Bigoni / BigoniBrother
Both use the `Bigoni` component. HellGate renames / marks brother spawns;
fatality profiles split so brother and full Bigoni can be toggled independently.

### BlackOoze
Spawn key `BlackOoze` loads the **monster** combat enemy. Trap families
(`BlackOozetrap`, `BlackOozeTrapTypeB`) do not inherit `EnemyDate` combat
`OndamageSend` the same way and are out of scope for this module.

### Gorotuki / Demon_gorotuki
`Demon_gorotuki` is a spine swap on `Gorotuki`. One fatality profile covers both.

### Slaughterer
RickEnemyMod may swap fatality **icon** art on erodata; combat fatality clip
is still HellGate PNG overlay on Aradia, not the enemy’s grab fatality scene.

### SlaveBigAxe
Combat fatality is independent of DeadArmor NikuArmor break clips and MeatArmor
walk. All three can coexist; this profile only reacts to player-damage hits.

### Goblin / GobBigAlter / GobRider
Same **shared bisect pool** as White / catalog (`lost_leg` / `lost_head` random).
Spawn keys: `Goblin` → type `goblin`; `GobBigAlter`; `GobRider`.
H-scene `goblinero` is not an `EnemyDate` combat match.

## Code

Profiles (except White) register in
`Systems/EnemyFatality/EnemyFatalityProfileCatalog.cs`.
White: `WhiteInquisitorFatality/WhiteInquisitorFatalityConfig.cs`.
