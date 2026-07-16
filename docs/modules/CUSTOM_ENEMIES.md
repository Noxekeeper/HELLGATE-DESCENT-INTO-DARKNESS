# Custom Enemies and Enemy Integration

Per-enemy integration for vanilla enemies and HellGate's custom enemy packs.

Code: `Patches/Enemy/` · Registration: `EnemyPrefabRegistry` + `SpawnConfigExecutor`

## Vanilla enemy pass/handoff integration

Every supported enemy family has a `*PassPatch` / `*PassLogic` type
implementing handoff participation (see `GRAB_AND_HANDOFF.md`):

Touzoku (normal/axe), Inquisition (black/white/red), Vagrant, PrisonOfficer,
Librarian, MummyDog, MummyMan, Pilgrim, Undead, CrowInquisition, Goblin,
Kakasi, Dorei, Mutude (Six_hand).

Shared plumbing lives in `Base/BaseEnemyPassPatch`;
`_Template/EnemyNamePassLogic.cs` is the not-compiled scaffold for new
enemies. Notable extras: CrowInquisition ERO fix, Goblin hardcore struggle
spawn, Kakasi cross patch and handoff hide, Mutude effects and video position
tracker.

## Custom enemy packs

Custom enemies are cloned vanilla prefabs with swapped visuals and/or logic,
registered as spawn keys:

| Pack | Basis | Content |
|------|-------|---------|
| `HG_Mini_bose` — **BigoniBrother** | `Bigoni` + `StartBigoniERO` | identity/marker tagging, patch + pass logic, GameOver bypass; detection is name/identity-based — there is **no** custom ERO component |
| `MafiaBossCustom` | mafia_muscle | stats, grab patch, ERO patches, pass logic; intentionally **not** a faction boss |
| `BossTouzokuCustom` | Touzoku boss | field-spawn variant: runtime, stats/HP scale, intro/combat/safety/ERO patch sets, activator |
| `WolfModCustom` | MummyDog | Spine skeleton + texture replacement (`[WolfMod]`) |
| `HellishTouzokuModCustom` | Touzoku | skeleton/texture replacement + H-scene escape patch (`[HellishTouzoku]`) |
| `DoreiModCustom` | Dorei | skeleton/texture replacement + spectator idle patch (`[DoreiMod]`) |
| `ButcherModCustom` | Slaughterer | Rick-style fatality only (`[ButcherMod]`) |
| `RickEnemyModShared` | — | shared loaders for the Rick asset family: spine/texture, fatality logo and icons (`[RickEnemyMod]`) |
| `HeckGateEnemy` — **biscord** | `suraimu` | slime module (type-level `PatchAll`), visual profile, eyes attachment, struggle/escape patches; forced neutral faction; drop table via DropSystem |

Asset-replacement packs load PNG/Spine data from the external asset tree
(`RickEnemyMod/` and related folders) via their `*TextureLoader` /
`*SkeletonLoader` types.

## Registration contract

A spawnable enemy requires all of:

1. a spawn key → prefab mapping in `EnemyPrefabRegistry`;
2. a branch in `SpawnConfigExecutor` if spawn behavior is non-standard;
3. pass logic (from `_Template`) if it participates in handoff;
4. explicit `<Compile Include>` entries in `NoREroMod_HellGate.csproj`;
5. patch registration in `Core/Plugin.cs`.

The full procedure is in `../development/ADDING_ENEMIES.md`.

## Historical note

`BigoniBrotherERO` (a custom ERO component approach) was removed in 1.2.4.
The live implementation patches the vanilla `StartBigoniERO` and identifies
BigoniBrother by object naming and `BigoniBrotherIdentity`. Do not
reintroduce the component path.
