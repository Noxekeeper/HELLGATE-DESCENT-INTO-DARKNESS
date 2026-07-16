# Data Roots and Formats

Where runtime data lives, how localization resolves, and which files are
generated at runtime.

## Source of truth vs runtime

The repository tracks shipped data under:

```text
HellGateAssets/BepInEx/plugins/HellGateJson/
```

At runtime, loaders read from the game installation:

```text
<NorGameRoot>/BepInEx/plugins/HellGateJson/
```

Keep the two in sync manually when testing; releases ship the repository
tree.

## Data roots

| Root | Content | Owner module |
|------|---------|--------------|
| `{LANG}/` | dialogue JSON, `GrabThreatsData.json`, `QTEReactionData.json`, splash labels | Dialogue, QTE, UI |
| `CombatAi/*.json` | enemy reaction tuning | Combat AI |
| `CombatAi/Factions.json` | faction definitions (language-agnostic, hot-reloaded ~2 s) | Factions |
| `HellGateSpawnPoint/HellGateSpawn_*.txt` | per-zone spawn packs + authoring cheatsheets | Spawn |
| `EventCore/**` | manifest, definitions, 10-language string packs, `event_trap_registry.json`, `reinforcement_registry.json` — schemas in [EVENTCORE_DATA.md](EVENTCORE_DATA.md) | EventCore |
| `Economic/Economy.json`, `Economic/GoldDropTable.json` | economy gate/tuning, gold drop tables | Economy |
| `DropSystem/*.json` | weighted item drop tables | Rewards |
| `Diagnostics/*.json` | opt-in investigation toggles (`Enable: false` by default) | Diagnostics |

Supported languages: `EN RU JP KR CN DE FR ES PT BR` (EventCore uses
`Ru En Cn Jp Kr De Pt Br Es Fr` folder naming).

## Localization policy

- The active language follows the HellGate language configuration.
- Most loaders fall back to **EN** when a key or folder is missing; the
  exact behavior is loader-specific.
- **EventCore is stricter**: string pools fail closed — a missing key is an
  error, never a silent cross-language fallback. New EventCore content must
  ship complete for every language folder it claims.

## Generated per-slot saves

Modules persist per-save-slot state as JSON inside the runtime data tree,
written through the game's save/load hooks:

| File | Module |
|------|--------|
| `CombatAi/PlayerReputation_Slot{NN}.json` | faction reputation |
| `Economic/PlayerGold_Slot{NN}.json` | gold wallet |
| `PlayerState/PlayerRageMindBroken_Slot{NN}.json` | rage + MindBroken |
| `Pregnancy/PlayerPregnancy_Slot{NN}.json` | offspring / pregnancy lineage state |
| `Pregnancy/PlayerCurrentPregnancy_Slot{NN}.json` | current gestation state |
| `Pregnancy/ShelterAttack_Slot{NN}.json` | shelter-attack state |

`{NN}` is the one-based save slot padded to two digits (`01`..`03`).

Rules:

- generated slot files are local runtime state — never copy them into
  `HellGateAssets/`;
- persistence goes through save/load hook patches only; gameplay code must
  not flush to disk directly;
- when adding a per-slot store, follow the existing `SlotStore` +
  save/load-hook pattern and document the file here.

## Authoring rules

- JSON parsing targets .NET 3.5-compatible code paths — keep formats simple
  and test malformed-file behavior (loaders should log and disable, not
  throw).
- Spawn pack line syntax is documented in `../modules/SPAWN.md` and in the
  cheatsheets next to the packs.
- Resolve all paths relative to the game root; never absolute paths.
