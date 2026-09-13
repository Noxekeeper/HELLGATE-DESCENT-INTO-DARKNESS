# HellGate Documentation Index

Developer documentation for the HellGate plugin. All documents target mod
developers and contributors; there is no end-user documentation in this tree.

Only this index plus `docs/modules/` and `docs/development/` are canonical
public documentation. Other local files/directories under `docs/` are
gitignored historical research or design notes; do not update or cite them as
the current implementation.

Start with [`../ARCHITECTURE.md`](../ARCHITECTURE.md) for the plugin's layers,
startup order, patching model, and subsystem boundaries. The documents below
describe individual subsystems and development procedures in depth.

## Module references (`docs/modules/`)

| Document | Subsystem |
|----------|-----------|
| [SPAWN.md](modules/SPAWN.md) | Zone pack pipeline + F11 Spawn System Editor V2.0 (not trap gameplay) |
| [SPAWN_KEY_ALIASES.md](modules/SPAWN_KEY_ALIASES.md) | Canonical F11 spawn keys vs txt aliases |
| [EVENT_CORE.md](modules/EVENT_CORE.md) | Modal encounters (`|ec_event=` / F11 **C**), event traps, reinforcements |
| [FACTIONS_AND_COMBAT_AI.md](modules/FACTIONS_AND_COMBAT_AI.md) | Enemy factions, reputation, combat AI |
| [PREGNANCY.md](modules/PREGNANCY.md) | Pregnancy, offspring, bloodlines, shelter attacks |
| [ECONOMY_AND_REWARDS.md](modules/ECONOMY_AND_REWARDS.md) | Gold economy, drops, reward tables |
| [QTE_STRUGGLE_AND_GAMEPLAY.md](modules/QTE_STRUGGLE_AND_GAMEPLAY.md) | QTE 3.0, struggle, weapon animations, VengeanceStrike |
| [RAGE.md](modules/RAGE.md) | Rage tiers, combos, slow motion, persistence |
| [GRAB_AND_HANDOFF.md](modules/GRAB_AND_HANDOFF.md) | Grab-via-attack, death-session grab blocks, handoff |
| [MIND_BROKEN.md](modules/MIND_BROKEN.md) | MindBroken state, recovery, Bad End countdown gate, presentation |
| [HELL_TRAPS.md](modules/HELL_TRAPS.md) | Lethal traps, death clips, combat freeze during clip |
| [DEAD_ARMOR.md](modules/DEAD_ARMOR.md) | SlaveBigAxe NikuArmor break clips + armored grab-throw |
| [LOST_SOUNDS.md](modules/LOST_SOUNDS.md) | Scaffold for custom / restored SFX (load + Play by name) |
| [COSTUMES.md](modules/COSTUMES.md) | Unlock alt costumes in change menu without Trade |
| [ENEMY_FATALITY.md](modules/ENEMY_FATALITY.md) | Combat fatality system (shared profiles; White + catalog) |
| [ENEMY_FATALITY_DOSSIERS.md](modules/ENEMY_FATALITY_DOSSIERS.md) | Per-enemy fatality dossiers (types, cfg, caveats) |
| [MEAT_ARMOR.md](modules/MEAT_ARMOR.md) | SlaveBigAxe post-fade Aradia_armor swap + EROWALK patrol |
| [ILLUSIVE_RODENIA_EVENT.md](modules/ILLUSIVE_RODENIA_EVENT.md) | Illusive / Rodenia SlaveBigAxe event: HellGate isolation + lose→SP handoff |
| [CUSTOM_ENEMIES.md](modules/CUSTOM_ENEMIES.md) | Custom enemy packs and pass/handoff integration |
| [PRESENTATION.md](modules/PRESENTATION.md) | Dialogue, camera, UI, audio, effects |
| [BOOT_TIPS_AND_GUIDE.md](modules/BOOT_TIPS_AND_GUIDE.md) | Early-boot loading tips + illustrated tutorial guide |
| [SPLASH_OPTIONS_AND_DIFFICULTY.md](modules/SPLASH_OPTIONS_AND_DIFFICULTY.md) | Splash Options: Gore, Easy/Medium/Hard presets, Language submenu |

## Development guides (`docs/development/`)

| Document | Topic |
|----------|-------|
| [BUILDING.md](development/BUILDING.md) | Environment, build, deploy, runtime verification |
| [CONFIGURATION.md](development/CONFIGURATION.md) | Generated reference of every cfg section and setting |
| [TESTING.md](development/TESTING.md) | Manual regression matrix: change area → required in-game checks |
| [PLAYER_GUARDS.md](development/PLAYER_GUARDS.md) | Each player guard and the soft lock or failure it prevents |
| [DIAGNOSTICS.md](development/DIAGNOSTICS.md) | Logging conventions and the JSON-gated diagnostic kits |
| [EVENTCORE_DATA.md](development/EVENTCORE_DATA.md) | JSON schemas for EventCore events, steps, language packs, traps, reinforcements |
| [RELEASE_PROCESS.md](development/RELEASE_PROCESS.md) | Version bump, freeze, packaging, fresh-install verification, publishing |
| [ADDING_FEATURES.md](development/ADDING_FEATURES.md) | Feature module conventions and checklist |
| [ADDING_ENEMIES.md](development/ADDING_ENEMIES.md) | Adding spawnable and custom enemies |
| [DATA_FORMATS.md](development/DATA_FORMATS.md) | JSON/text data roots, localization, per-slot saves |
| [COMPATIBILITY.md](development/COMPATIBILITY.md) | NoREroMod boundary and known hazards |
| [API.md](development/API.md) | Public API contract for other BepInEx plugins |

## Conventions

- Documents describe verifiable current behavior; concepts and history belong
  in maintainer notes, not here.
- Use relative repository paths and class names, never machine-specific
  absolute paths.
- Update the relevant module document in the same change that alters the
  subsystem it describes.
