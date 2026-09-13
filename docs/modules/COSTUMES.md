# Costumes

QoL module that unlocks the two alternate illusory outfits in the vanilla
**costume-change** menu without requiring Trade purchase.

Code: `Systems/Costumes/` · Config: `[Costumes]` · No external assets

## Scope

| Slot (`_cosskin`) | Outfit | Flag |
|-------------------|--------|------|
| `0` | Default witch costume (魔女の衣装) | always available (vanilla) |
| `1` | Silver-haired gunner (銀髪銃撃士の幻衣装) | `_CosSkinflag[1]` |
| `2` | Vendetta / ageless witch (不老の魔女の幻衣装) | `_CosSkinflag[2]` |

When `[Costumes] Enable = true`, the module forces flags `[1]` and `[2]` to
`true` on both `StaticMng.CosSkinflag` and the live `game_fragmng._CosSkinflag`.

### Explicitly out of scope

- Does **not** set `_Tradeflag[0]` (Trade NPC stays locked until vanilla unlock).
- Does **not** set `_Tradeflag[1]` / `[9]` (Trade costume rows can still be
  purchased; buying is redundant once flags are set).
- Does **not** change Spine assets, descriptions, or costume-break behavior.

## Vanilla reference

Unlock path in the base game:

1. Open Trade via Illusive (`_Tradeflag[0]`, `EvBigAxeTalkMng` after the
   Rodenia church event — see [ILLUSIVE_RODENIA_EVENT.md](ILLUSIVE_RODENIA_EVENT.md)).
2. Buy costume rows in `Trade_menu` (`TradeSelectItem`, `kind == 3`):
   - gunner → `Getitem_ID: 1`, `tradeflagnum: 1`;
   - Vendetta → `Getitem_ID: 2`, `tradeflagnum: 9`.
3. `getitemSet` writes `_CosSkinflag[Getitem_ID] = true`.
4. `CostumeChange` reads those flags in `skinflagcheck` / `SkinChange1` /
   `SkinChange2`; locked slots show `??????`.

Flags persist through ES2 (`SaveFile` / `LoadFile` tag `_CosSkinflag`).

## Behavior

1. **Postfix** `game_fragmng.fun_DataLoad` — after save flags are copied from
   `StaticMng`, re-apply unlocks so load always sees both alts.
2. **Prefix** `CostumeChange.Start` — unlock before `skinflagcheck` so the UI
   never blanks unlocked slots as `??????`.

If the module is later disabled, flags already written into a save remain
unlocked (vanilla persistence). Fresh sessions with `Enable = false` keep
vanilla lock behavior.

## Config

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enable` | Boolean | `true` | Unlock alt costumes in the costume-change menu without Trade purchase |

## Code map

| Type | Role |
|------|------|
| `CostumesConfig` | `[Costumes]` Bind + `IsEnabled` |
| `CostumesUnlock` | Sets CosSkinflag indices 1 and 2 on StaticMng + frag |
| `CostumesPatches` | Harmony: `fun_DataLoad` postfix, `CostumeChange.Start` prefix |

Init: `CostumesConfig.Initialize()` next to other module configs in boot;
`CostumesPatches.Apply(harmony)` beside `DeadArmorPatches.Apply` in
`SetUpPatches()`.

## Related

- Settings: [CONFIGURATION.md](../development/CONFIGURATION.md) `[Costumes]`
- Manual check: [TESTING.md](../development/TESTING.md) (`Systems/Costumes/`)
