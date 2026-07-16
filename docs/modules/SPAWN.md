# Spawn Pipeline

Data-driven world spawning. Location content is described by text packs and
executed at scene load; no per-map C# spawn code exists anymore.

Code: `Systems/Spawn/` · Data: `HellGateJson/HellGateSpawnPoint/` · Config: `[SpawnTemplates]`

## Ownership and flow

1. A scene loads (or the player passes a door / rests at an altar).
2. A refresh patch triggers `HellGateLocationSpawnRefresh`.
3. `HellGateSpawnSceneHints` maps the active zone to its spawn pack
   (`HellGateSpawn_<Zone>.txt`).
4. `SpawnConfigExecutor` parses the pack line by line and instantiates
   enemies, traps, decor, hostages, and encounter anchors.

Refresh patches, in order of responsibility:

| Patch | Trigger |
|-------|---------|
| `SceneLoadSpawnRefreshPatch` | primary — after `LoadSceneAndWait` |
| `SceneMoveTransitionSpawnPatch` | door transitions (wipes previous spawns) |
| `SpawnRespawnAfterAltarPatch` | altar rest respawn |
| `LocationTransitionSpawnController` | safety net when other triggers miss |

## Line formats

`SpawnConfigExecutor` supports these directives per line:

- fixed enemy key at coordinates;
- `RANDOM` / `RANDOM_GROUP` / `POOL[...]` — randomized selection;
- `gold=` — gold pickup;
- `TRAP` — HellTraps template spawn;
- `DECOR` — decor catalog entry;
- `EVENTTRAP` / `REINFORCEMENT` — EventCore encounter anchors;
- `RANDOM_HOSTAGE` — hostage runtime spawn.

Line metadata suffixes: `|faction=` (faction override), `|ec_event=` / `|ec=` /
`|ec_pool=` (attach an `EventCoreHost`), plus facing and sort-order suffixes.
Authoring cheatsheets live next to the packs in `HellGateSpawnPoint/`.

## Registries and caches

- `EnemyPrefabRegistry` — spawn key → prefab name, including custom enemy
  clones. Every spawnable enemy must be registered here.
- `SpawnTemplateCatalog` (+ `SpawnTemplateDiskCache`, `EnemyPrefabDiskCache`) —
  template prefabs with throttled disk I/O.
- `SpawnDecorCatalog`, `SpawnTemplateWhitelist`.

## Support components

- Boss spawns: `HellGateBossSpawnBootstrap` / `HellGateBossSpawnRuntime`.
- Hostages: `HellGateHostageRuntime` + `HellGateSpawnedHostageMarker`.
- Anchor discovery for EVENTTRAP/REINFORCEMENT: `HellGateSpawnAnchorDiscovery`,
  `SpawnTrapAnchorLookup`.
- Placement utilities: flip, rotation, depth, fixed facing.
- `SpawnCacheWeatherGuard` — protects cached prefabs across weather changes.
- `SpawnParentInitializeGate` — defers execution until the spawn parent is
  initialized.

## Developer tooling

`Patches/Spawn/SpawnPointAnalyzer` records world coordinates on F11 and
hot-reloads the active pack on right mouse button; player attack input is
blocked while recording.

## Removed predecessors — do not reintroduce

- Per-map `HellGateSpawn_*.cs` files and `UnifiedSpawnManager` (replaced by
  this pipeline).
- `SpawnSceneTransitionFix` — overwrote the vanilla `_re_Scenename` field and
  broke additive EV scenes.
