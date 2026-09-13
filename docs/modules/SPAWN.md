# Spawn Pipeline

Data-driven world spawning. Location content is described by text packs and
executed at scene load; no per-map C# spawn code exists anymore. Includes the
F11 **Spawn System Editor V2.0** for placing, editing, and deleting pack
lines in-game.

Code: `Systems/Spawn/` · Authoring: `Systems/Spawn/Authoring/` · Entry:
`Patches/Spawn/SpawnPointAnalyzer` · Data: `HellGateJson/HellGateSpawnPoint/` ·
Config: `[SpawnTemplates]`

## Documentation boundary

Spawn owns **where and how** content is placed in a zone pack (coordinates,
count, flip / rotation / sort, authoring links). It does **not** own trap
gameplay, death clips, EventTrap encounters, or reinforcement waves:

| Content | Placement (this doc) | Behavior / runtime |
|---------|----------------------|--------------------|
| Enemies / custom packs | pack lines + F11 Enemies | [CUSTOM_ENEMIES.md](CUSTOM_ENEMIES.md), combat modules |
| Scene traps / props (`TRAP` / `OBJECT` / `DECOR`) | template lines + F11 Trap / Decorations | vanilla / template components; lethal families → [HELL_TRAPS.md](HELL_TRAPS.md) |
| Lethal HellTraps | same `TRAP` placement + F11 Lethal | [HELL_TRAPS.md](HELL_TRAPS.md) |
| EventCore modal NPCs | enemy lines with `\|ec_event=` + F11 EventCore | [EVENT_CORE.md](EVENT_CORE.md) |
| EventTrap / Reinforcement anchors | `EVENTTRAP` / `REINFORCEMENT` lines + F11 EventTrap | [EVENT_CORE.md](EVENT_CORE.md) |

When documenting or changing a trap family, put runtime, cfg, assets, and
death-session rules in that family's module doc. Update this file only if
**line syntax**, **template catalog caching**, or **F11 placement UI**
changes.

## Ownership and flow

1. A scene loads (or the player passes a door / rests at an altar).
2. A refresh patch triggers `HellGateLocationSpawnRefresh`.
3. `HellGateSpawnSceneHints` maps the active zone to its spawn pack
   (`HellGateSpawn_<Zone>.txt`).
4. `SpawnConfigExecutor` parses the pack line by line and instantiates
   enemies, templates (traps/objects/decor/hostages), and encounter anchors.
5. Each HellGate-managed instance receives a `SpawnManagedInstance` with an
   **authoring link** (pack path, line index, source line, XY, key, flip /
   rot / depth) so F11 edit/delete can rewrite the same line later.

Refresh patches, in order of responsibility:

| Patch | Trigger |
|-------|---------|
| `SceneLoadSpawnRefreshPatch` | primary — after `LoadSceneAndWait` |
| `SceneMoveTransitionSpawnPatch` | door transitions (wipes previous spawns) |
| `SpawnRespawnAfterAltarPatch` | altar rest respawn |
| `LocationTransitionSpawnController` | safety net when other triggers miss |

## Line formats

`SpawnConfigExecutor` supports these directives per line (`#` / empty =
comment; unparsed lines log `Skipped unparsed spawn line` and are skipped):

### Enemies

- Fixed: `X,Y,Key[,Count][,flip][,rot…][,near|far|…]`
- Random: `RANDOM,chance,X,Y,Key[,Count][,flip…]`
- `RANDOM_GROUP` … `END` — pick N lines from a group
- `POOL[Type1,Type2,…]` inside the key field — pick one type at spawn time
- Key metadata pipes: `|faction=…`, `|elite=1`, `|ec_event=` / `|ec=` /
  `|ec_pool=` / `|ec_chance=` / `|ec_p=` (EventCore host). `|ec_chance=` is
  **dialogue attach** probability (NPC still spawns). Omit it (or `1`) to
  always attach. Empty `|faction=` on an EventCore spawn becomes
  `eventcore_encounter`. F11 Enemies Place never writes EventCore pipes;
  use catalog **C**. See [EVENT_CORE.md](EVENT_CORE.md).

Count: `X,Y,Key` without a fourth field defaults to count `1`.

### Template placement (`TRAP` / `OBJECT` / `DECOR` / `HOSTAGE`)

Spawn only places a catalog template at XY with optional facing / rotation /
depth. Shortcut forms (same placement extras after count):

```text
TRAP,Key,X,Y,Count[,flip][,rot90|rot180|rot270][,near|far|z±|y±|sort±|layer:…]
OBJECT,Key,X,Y,Count[,…]
DECOR,Key,X,Y,Count[,…]
HOSTAGE,Key,X,Y,Count[,…]
SPAWN,Trap|Object|…,Key,X,Y,Count[,…]
```

Spike / damage-prop packs also accept the enemy-shaped form (this is what
existing maps and F11 spike Place write):

```text
X,Y,trapnormal,1[,flip][,rot180][,sort±N]
```

`SpawnConfigExecutor.TryParseEnemyStyleTemplateLine` routes that to the
template path (not `EnemyPrefabRegistry`).

- **Key** must exist in `SpawnTemplateCatalog` (or be registered by another
  module at boot — e.g. lethal keys from HellTraps).
- What the instance **does** after spawn is not defined here.
- `EVENTTRAP` / `REINFORCEMENT` lines are anchors only; encounter logic is
  EventCore.
- Also: `gold=` pickups, `RANDOM_HOSTAGE`.

Key lists / personal notes next to packs
(`SPAWN_TRAP_OBJECT_CATALOG.txt`, `SPAWN_CONTENT_CATALOG.txt`, …) are
authoring aids, not runtime specs.

### Placement extras (shared)

Parsed by `HellGateSpawnLineFormat.ParseOptionalPlacementFields` via
`SpawnFlipUtility` / `SpawnRotationUtility` / `SpawnDepthUtility`:

| Token | Effect |
|-------|--------|
| `flip` / `mirror` / `left` / `-1` | horizontal facing left |
| `rot90` / `rot180` / `rot270` (aliases `90`, `180`, `upside`, …) | Z rotation in place (prefab pivot; no XY remount) |
| `near` / `front` | **legacy read**: player layer, sorting +10 |
| `far` / `back` | **legacy read**: sorting −10 |
| `sort±N` | SortingOrder delta (what F11 writes) |
| `z±` / `y±` / `layer:Name` | fine depth overrides |

F11 never writes `near`/`far`. Old pack lines with those tokens still load.

## Registries and caches

- `EnemyPrefabRegistry` — spawn key → prefab name, including custom enemy
  clones. Every spawnable enemy must be registered here.
- `SpawnTemplateCatalog` (+ `SpawnTemplateDiskCache`, `EnemyPrefabDiskCache`) —
  template prefabs (traps/objects/decor/hostages) with throttled disk I/O.
  `TrySpawn(..., out GameObject firstSpawned)` returns the first instance for
  authoring links. Catalog registration of lethal templates is owned by
  HellTraps; Spawn only caches and instantiates.
- Disk-cache rules: do not overwrite a real scene token with `__resources__`;
  hydrate fallback may use whitelist `key@Scene` (e.g.
  `trap_mokubaenemy@Prison`). Decor typo alias `InchchurchSlave` →
  `InchurchSlave`.
- `SpawnDecorCatalog`, `SpawnTemplateWhitelist`.
- Keys marked `(?)` in the F11 template list are not in the live catalog yet —
  visit a map that contains that template (or rely on disk cache / whitelist).

## Hostages and `|faction=`

Spawned hostages can carry `|faction=` for the rescue spawn. Inheritance
patches keep that faction on the freed combatant:

- `SlaveSideLookFactionInheritPatch`
- `EnemyMobCrowSlaveBackFactionInheritPatch` /
  `EnemyMobCrowSlaveStandupFactionInheritPatch`
- ColCreateObj paths for `gob_look` / `Look_Dorei`

See also [FACTIONS_AND_COMBAT_AI.md](FACTIONS_AND_COMBAT_AI.md).

## Support components

- Boss spawns: `HellGateBossSpawnBootstrap` / `HellGateBossSpawnRuntime`.
- Hostages: `HellGateHostageRuntime` + `HellGateSpawnedHostageMarker`.
- Anchor discovery for EVENTTRAP/REINFORCEMENT: `HellGateSpawnAnchorDiscovery`,
  `SpawnTrapAnchorLookup` (placement only; drivers are EventCore).
- Placement utilities: `SpawnFlipUtility`, `SpawnRotationUtility`,
  `SpawnDepthUtility`.
- `SpawnManagedInstance` — HellGate ownership + F11 pack-line link.
- `SpawnCacheWeatherGuard` — protects cached prefabs across weather changes.
- `SpawnParentInitializeGate` — defers execution until the spawn parent is
  initialized.

---

## F11 Spawn System Editor V2.0

In-game pack authoring for the **active zone pack** resolved by
`HellGateLocationSpawnRefresh.GetActiveSpawnConfigPath()`. Toggle with
**F11** (`SpawnPointAnalyzer` + `Systems/Spawn/Authoring/`).

V2.0 is the current editor: catalogs (including EventCore **C**), box
clipboard, undo, favorites, overview camera, precise world pick, F1 Help
in ten languages. It writes and rewrites pack lines only. Trap combat,
death clips, and EventCore dialogue stay in their own modules.

### Enable / pause

| Cfg (`[SpawnTemplates]`) | Default | Role |
|--------------------------|---------|------|
| `AuthoringUiEnable` | `true` | Show the overlay while F11 recording is on |
| `AuthoringPauseGameplay` | `true` | `timeScale=0` + lock player control while authoring |

Player attack input is blocked while recording so LMB only picks points
and instances.

UI chrome follows `HellGateLanguage` via `SpawnAuthoringLoc` (EN base;
overlays RU JP CN KR FR DE PT BR ES). Prefab keys, pack tokens
(`RANDOM`, `EVENTTRAP`, `|ec_event=`), and the banner title
**Spawn System Editor V2.0** are not translated. F1 Help lists one
command per line.

### Catalogs

| Hotkey / UI | Catalog | Source |
|-------------|---------|--------|
| **E** Enemies | combat enemy keys | `SpawnAuthoringEnemyCatalog` + `EnemyPrefabRegistry` (+ exclude list) |
| **H** Hostage & OtherScenes | hostages + Spine look scenes | `SpawnAuthoringHostageCatalog` (`Look_Dorei`, `gob_look`, `gob_look2`, …) |
| **T** Trap | one canonical key per trap prefab | `SpawnAuthoringTrapCatalog` (built-in + cache, then unique-prefab filter) |
| **R** Lethal | three lethal keys | `lethal_magictrap`, `lethal_cocoontrap`, `lightningTrap_button` |
| **N** Decorations | boxes, barrels, static corpses, scene props | `SpawnDecorCatalog` / `DECOR_CATALOG.txt` |
| **G** Gold | pile amount presets | `GOLD,X,Y,amount,1` or `RANDOM,chance,X,Y,gold=…` |
| **M** EventTrap | registry pack folders | `EVENTTRAP,folder,X,Y[,count=][,dist=][,sides=][,faction=][,max=][,r=][,delay=]` (RMB reload) |
| **C** EventCore | modal event ids from the manifest | `EventCoreDefinitionRegistry.GetAuthoringEventIds()` — not an Enemies option |
| **B** Favorites | any catalog preset | `SPAWN_AUTHORING_FAVORITES.txt` |

**F1** (while F11 is on) opens the Help panel. Cheat-menu F1 is blocked for
that duration (`SpawnAuthoringHotkeyGuard`). **Q** closes catalogs (and Help).
**Esc** closes Help → region box → Edit → selection.

While an Edit panel is open on a trap, **R** is **+30°**, not Lethal.
**C** without Ctrl is EventCore; **Ctrl+C** is still copy.

Pending options (Enemies): faction, elite, flip, random chance. EventCore is
**not** on that panel. Opening EventCore sets faction to `eventcore_encounter`
for that catalog only and may select an event id. Leaving EventCore
restores the previous faction and pending enemy key. Enemies / Hostage /
EventTrap pickers skip `eventcore_encounter`; Place on those catalogs will
not write it. Pending options (EventCore): faction (defaults to
`eventcore_encounter`), elite, flip, random NPC spawn chance, Event **p=**
(`|ec_chance=`; default **1** = always attach the dialogue). Broker / FSP
ids lock the prefab to `TouzokuNormal`. Pending options (Trap / Lethal /
Decorations): **placement only** — flip, rotation (degrees or +30), `sort±N`.
No Elite / faction on those templates. Hostage & OtherScenes also have
**faction** (`|faction=`), inherited onto the combatant after rescue /
look-attack, plus Random (`RANDOM_HOSTAGE`). Gold has Random. EventTrap Place
can override pack `config.json` per anchor (`count`, `dist`, `sides`,
`faction`, `max`, zone `r`, `delay`); empty fields keep the pack defaults.

Place writes the pack line (`SpawnAuthoringPackEdit.BuildTrapLine` /
`BuildEnemyLine`) and preview-spawns with an authoring link. F11 preview
always attaches `EventCoreHost` when an event id is selected; `|ec_chance=`
is applied on the next pack load / RMB reload.

#### What Place writes

| Catalog | Line |
|---------|------|
| Enemies | `X,Y,Key[|faction=…][|elite=1],1[,flip]` or `RANDOM,chance,…` (no EventCore pipes) |
| EventCore | `X,Y,TouzokuNormal\|faction=eventcore_encounter\|ec_event=<id>,1[,flip]` — add `\|ec_chance=` only when Event p= is below 1; `RANDOM,chance,…` is the NPC spawn roll |
| Spikes (`trapnormal`, `impactdamage`, `wavespike`, …) | `X,Y,Key,1[,flip][,rot90][,sort+N]` — same as existing zone packs |
| Decorations | `DECOR,Key,X,Y,1[,flip][,rot…][,sort±]` |
| OtherScenes looks | `DECOR,Key[|faction=…],X,Y,1[,flip][,rot…][,sort±]` |
| Hostages | `HOSTAGE,Key[|faction=…],X,Y,1[,flip][,rot…][,sort±]` or `RANDOM_HOSTAGE,chance,Key,X,Y,1` |
| Other traps / Lethal | `TRAP,Key,X,Y,1[,flip][,rot…][,sort±]` |
| Gold | `GOLD,X,Y,100-300,1` or `RANDOM,chance,X,Y,gold=100-300,1` |
| EventTrap | `EVENTTRAP,folder,X,Y[,count=1-2][,dist=6;8][,sides=both][,faction=bandits][,max=3][,r=8][,delay=1]` |

`trap` / `trap_hari` / `traphari` canonicalize to **`trapnormal`** on write.

Alias names stay valid in hand-edited txt. F11 lists canonical keys only —
see [SPAWN_KEY_ALIASES.md](SPAWN_KEY_ALIASES.md). Hidden junk (`help`,
`meatshieldhelp`, `npcslaveenable`, …) is not shown.

`(?)` on a row = template not in the live catalog yet (visit the vanilla
map / disk cache / whitelist).

### Camera

| Input | Behavior |
|-------|----------|
| WASD | move the camera |
| Hold MMB and drag | move the camera with the mouse |
| Mouse wheel | zoom (turns Overview on) |
| **V** | Overview / Default |
| **Home** | camera to the player (Overview stays on; zoom resets) |

### Mouse and placement

| Input | Behavior |
|-------|----------|
| LMB on empty ground | set **Point** |
| LMB-drag on empty ground | box-select HellGate objects and pack anchors |
| LMB on a sprite | select / lock (`SpawnAuthoringWorldPick`); opens Edit when linked |
| Hold LMB on an object | drag the selected managed spawn |
| Arrows | nudge the selection (**Shift+arrows** ×5) |
| Drag from preview or list onto the map | Place and save at the cursor |
| RMB | write F11 drag / nudge XY into the pack, then hot-reload |
| Space / Enter | Place and save the pending key at Point |
| **U** | Use position (copy live XY into Edit and save) |
| **F** | toggle pending / selected flip |
| **T** | Trap catalog (Rage Time Slow-Mo is blocked while F11 is on) |

### Edit and clipboard

| Input | Behavior |
|-------|----------|
| **F1** | Help panel (F11 only; cheat-menu F1 is blocked) |
| **Q** | close catalogs and Help |
| **Esc** | close Help, then box, then Edit, then selection |
| Ctrl+C / Ctrl+X / Ctrl+V | copy / cut / paste at Point (a boxed chunk also hits the OS clipboard) |
| Ctrl+D | duplicate selected at Point |
| Ctrl+Z | undo last append / replace / delete |
| Ctrl+S | save the open Edit panel |
| Ctrl+B | add the pending key to Favorites |
| Del | delete the selected linked pack line and live instance(s) (a box deletes everything inside) |
| **F12** | screenshot → `HellGateScreenshots/` under the game root |
| Shift+F12 | recording stats (while F11 is on) |

World pick (`SpawnAuthoringWorldPick`) is **precise by default**: sprite /
solid collider / `OverlapPoint`. Gold **skips** the pickup `CircleCollider2D`
halo. Combat enemies skip trigger colliders and oversized Spine mesh AABBs.
Trap / decor / hostage / lethal props (`Trapdata`, pack template lines) keep
trigger bodies and large meshes so Ivy-style vines are clickable. **Alt**
restores a nearby-radius grab (`SlackRadius` 0.45). EVENTTRAP /
REINFORCEMENT anchors use an F11-only labeled gizmo (`AnchorRadius` 1.0);
they stay invisible in play. Candidates are tagged `Enemy`, every live
`SpawnManagedInstance`, and `GoldPickup`. The smallest containing shape
wins.

LMB-drag of a linked object **writes XY to the pack on mouse-up**.
Vanilla scene props have no pack line and will not persist.

Edit/Delete require `HasAuthoringLink`. Place/Paste and pack load attach
the link; after changing older content, **RMB reload** once so links attach.
Ctrl+V / region paste keeps `|ec_event=` on enemy lines
(`ExtractEventCoreFromSourceLine`); stripping the key at `|` would spawn a
plain NPC.

### Edit panel

Opened from a locked linked instance (LMB on a HellGate-linked object).

- Enemy: key, faction, random, elite, flip, XY.
- EventCore NPC: event id, Event p= (`|ec_chance=`), faction, elite, flip,
  random, XY. Save keeps `|ec_event=` (it used to strip it). Cycling the
  event to None writes a plain enemy line.
- Template (trap / decor / lethal): key, flip, rotation (+30 or degrees),
  `sort±N`, XY. No faction / elite / random.
- Hostage: key, faction, flip, sort, XY (no rotation).
- Gold: amount key, random, XY.
- EventTrap / Reinforcement: folder key + XY (gizmo).

Save rewrites the resolved pack line via
`SpawnAuthoringPackEdit.TryReplaceLinkedLine` and applies live facing /
in-place Z rotation. `sort±` applies on the next pack reload (sorting is
not restacked on the live transform).

**Use current position** copies NOW XY into the fields **and saves** so the
SPAWN marker / pack line follow the object.

Preview thumbnails (`SpawnAuthoringEnemyPreview`) bake the pending
rotation and flip into the cache id.

### Rotation and sort (templates)

- Rotation is **in place** around the prefab pivot
  (`SpawnRotationUtility.ApplyAuthoringRotation`). Do **not** remount using
  `Renderer.bounds` AABB — that moved the root off the trigger (stub mesh,
  no damage).
- Under F11 pause (`timeScale=0`) do not rely on `Rigidbody2D.MoveRotation`.
- `sort±N` is a SortingOrder delta. It is not `near`/`far` (those are
  legacy player-layer presets still accepted when reading packs).
- `ApplyTrapHariFloorDefaults`: player Z for `trapnormal` aliases;
  player sorting layer +2 only on floor/ceiling (0°/180°). Wall angles keep
  native sorting.

### Authoring types (`Systems/Spawn/Authoring/`)

| Type | Role |
|------|------|
| `SpawnAuthoringOverlayHost` | IMGUI layout, hotkeys, edit/save/delete |
| `SpawnAuthoringState` | session: catalog, pending/selected options, Point |
| `SpawnAuthoringPackWriter` | append enemy / template lines to active pack |
| `SpawnAuthoringPackEdit` | build/parse lines, resolve index, replace/delete |
| `SpawnAuthoringPreviewSpawn` | Place: write + spawn + link |
| `SpawnAuthoringWorldPick` | hover / lock / cycle (sprite/collider pick) |
| `SpawnAuthoringEnemyPreview` | RT thumbnail (enemies + templates; follows rot/flip) |
| `SpawnAuthoringEnemyCatalog` / `TrapCatalog` / `EventTrapCatalog` | list + readiness + unique-prefab filter |
| `EventCoreDefinitionRegistry` | F11 EventCore catalog ids + bound enemy key |
| `SpawnAuthoringClipboard` / `Undo` / `Favorites` | copy-paste, undo stack, favorites file |
| `SpawnAuthoringCamera` / `Screenshot` / `Drag` / `Nudge` | view, capture, move |
| `SpawnAuthoringOutline` / `ClickCue` / `Pause` / `UiSuppressor` | SPAWN/NOW markers, click toast, pause |

### Data files next to packs

| File | Role |
|------|------|
| `SPAWN_AUTHORING_FAVORITES.txt` | F11 favorites — any catalog (`Label|kind|Key|faction|elite|flip|count|random|chance|rot|sort|ec|ecChance`). `kind` includes `eventcore`. |
| `SPAWN_AUTHORING_ENEMY_EXCLUDE.txt` | enemies hidden from the authoring list |
| `SPAWN_TEMPLATE_KEYS.txt` | static `[TRAP]` / `[DECOR]` / `[HOSTAGE]` reference |
| `DECOR_CATALOG.txt` | Decorations keys + scene hints |
| `SPAWN_TEMPLATE_WHITELIST.txt` | pre-cache hints |
| `AVAILABLE_SPAWN_TEMPLATES_RUNTIME.txt` | dump of every cached alias (not the F11 list) |

---

## Cross-module notes

- [HELL_TRAPS.md](HELL_TRAPS.md) — lethal trap families, death clips, combat
  freeze, vengeance shock, Gore/`[HellTraps]` gates. Spawn only places their
  keys via `TRAP` lines / F11 Lethal.
- [EVENT_CORE.md](EVENT_CORE.md) +
  [EVENTCORE_DATA.md](../development/EVENTCORE_DATA.md) — modal EventCore
  NPCs (`|ec_event=` / F11 **C**) and EventTrap / Reinforcement after
  anchors are placed.
- [FACTIONS_AND_COMBAT_AI.md](FACTIONS_AND_COMBAT_AI.md) — `|faction=` on
  enemy lines.
- [CUSTOM_ENEMIES.md](CUSTOM_ENEMIES.md),
  [ADDING_ENEMIES.md](../development/ADDING_ENEMIES.md) — enemy keys.
- Cfg for this pipeline: [CONFIGURATION.md](../development/CONFIGURATION.md)
  `[SpawnTemplates]` (not `[HellTraps]`).
- Canonical vs alias keys: [SPAWN_KEY_ALIASES.md](SPAWN_KEY_ALIASES.md).

## Removed predecessors — do not reintroduce

- Per-map `HellGateSpawn_*.cs` files and `UnifiedSpawnManager` (replaced by
  this pipeline).
- `SpawnSceneTransitionFix` — overwrote the vanilla `_re_Scenename` field and
  broke additive EV scenes.
- AABB “mount / remount” on spike rotate (`Renderer.bounds` as floor
  contact) — shifts the root so `TrapNormal` no longer overlaps the sprite.
- F11 `near`/`far` depth presets as the authoring target (parser still
  reads them; UI writes `sort±N`).
- Dumping every compact cache alias into the F11 Trap list.
