# HellGate Documentation Index

Developer documentation for the HellGate plugin. All documents target mod
developers and contributors; there is no end-user documentation in this tree.

Start with [`../ARCHITECTURE.md`](../ARCHITECTURE.md) for the plugin's layers,
startup order, patching model, and subsystem boundaries. The documents below
describe individual subsystems and development procedures in depth.

## Module references (`docs/modules/`)

| Document | Subsystem |
|----------|-----------|
| [SPAWN.md](modules/SPAWN.md) | JSON/text-driven world spawn pipeline |
| [EVENT_CORE.md](modules/EVENT_CORE.md) | Modal encounters, event traps, reinforcements |
| [FACTIONS_AND_COMBAT_AI.md](modules/FACTIONS_AND_COMBAT_AI.md) | Enemy factions, reputation, combat AI |
| [PREGNANCY.md](modules/PREGNANCY.md) | Pregnancy, offspring, bloodlines, shelter attacks |
| [ECONOMY_AND_REWARDS.md](modules/ECONOMY_AND_REWARDS.md) | Gold economy, drops, reward tables |
| [QTE_STRUGGLE_AND_GAMEPLAY.md](modules/QTE_STRUGGLE_AND_GAMEPLAY.md) | QTE 3.0, struggle, weapon animations, VengeanceStrike |
| [RAGE.md](modules/RAGE.md) | Rage tiers, combos, slow motion, persistence |
| [GRAB_AND_HANDOFF.md](modules/GRAB_AND_HANDOFF.md) | Grab-via-attack and enemy handoff chains |
| [MIND_BROKEN.md](modules/MIND_BROKEN.md) | MindBroken state, recovery, presentation |
| [HELL_TRAPS.md](modules/HELL_TRAPS.md) | Lethal trap families and death sequences |
| [CUSTOM_ENEMIES.md](modules/CUSTOM_ENEMIES.md) | Custom enemy packs and pass/handoff integration |
| [PRESENTATION.md](modules/PRESENTATION.md) | Dialogue, camera, UI, audio, effects |

## Development guides (`docs/development/`)

| Document | Topic |
|----------|-------|
| [BUILDING.md](development/BUILDING.md) | Environment, build, deploy, runtime verification |
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
