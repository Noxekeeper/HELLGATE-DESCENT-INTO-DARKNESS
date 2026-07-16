# HellGate — Developer Repository

Modular BepInEx/Harmony overhaul framework for *Night of Revenge*.

> This repository is intended exclusively for mod developers, maintainers, and
> contributors. It is not an end-user installation or gameplay guide.

## Project identity

| Property | Value |
|----------|-------|
| Plugin | NoREroMod HellGate |
| Plugin GUID | `NoREroMod_HellGate` |
| Current version | `1.2.4` |
| Output assembly | `NoR_HellGate.dll` |
| Target framework | .NET Framework 3.5 |
| Runtime | Unity / BepInEx |
| Patching | Harmony |

HellGate extends *Night of Revenge* by patching the game's managed types
directly and by running independent gameplay services alongside them.

`NoREroMod.dll` is a required companion dependency, not HellGate's architectural
base. Most HellGate modules target vanilla game types. A smaller compatibility
layer consumes selected NoREroMod types and internal patch state. Where both
plugins implement overlapping behavior, HellGate explicitly disables or
reconfigures the NoREroMod path.

See [`ARCHITECTURE.md`](ARCHITECTURE.md) for the dependency boundaries, startup
order, patching model, data flow, and complete subsystem map.

## Repository scope

This repository contains:

- the C# source for `NoR_HellGate.dll`;
- Harmony patches and runtime feature modules;
- JSON and text content distributed with HellGate;
- project metadata and developer-facing documentation.

This repository does not contain:

- a complete game installation;
- proprietary game assemblies;
- the required NoREroMod binary;
- large production PNG, WAV, Spine, and other binary asset packs;
- end-user installation documentation;
- private research notes and local development tooling.

## Major subsystems

HellGate is divided into independently initialized modules:

- JSON-driven world spawn pipeline;
- EventCore modal encounters, event traps, and reinforcements;
- enemy factions, targeting, reputation, and de-escalation;
- pregnancy, offspring, bloodlines, and shelter attacks;
- economy, currency persistence, drops, and death-loss behavior;
- QTE 3.0 and struggle integration;
- rage, combo, slow-motion, and vengeance systems;
- MindBroken state, recovery, and visual presentation;
- lethal HellTraps and their death sequences;
- enemy handoff/pass chains;
- custom enemy variants and visual replacement packs;
- dialogue, camera, audio, HUD, portrait, and effect systems;
- opt-in diagnostic modules for reverse-engineering game behavior.

Subsystem details belong in architecture and module documents rather than in
this file.

## Repository layout

```text
Core/
  Plugin.cs                  BepInEx entry point, configuration, initialization
  PluginInfo.cs              plugin identity and version
  HellGateTypeResolver.cs    safe runtime type/member resolution

Systems/
  <Feature>/                 runtime services and feature-owned patches

Patches/
  Enemy/                     enemy integration and custom enemy packs
  Player/                    player state, escape, recovery, and safety hooks
  HellTraps/                 lethal trap integration
  Performance/               hot-path compatibility and cache patches
  Spawn/                     developer spawn tooling
  Trap/, UI/, Effects/       game-facing integration patches

HellGateAssets/
  BepInEx/plugins/HellGateJson/
                              version-controlled runtime JSON and text data

References/                  local .NET Framework 3.5 reference assemblies
Properties/                  assembly metadata
NoREroMod_HellGate.csproj    explicit compile and reference manifest
ARCHITECTURE.md              architectural source of truth
```

The project uses an explicit `<Compile Include="...">` list. Adding a `.cs` file
to the repository does not compile it automatically; every live source file
must be added to `NoREroMod_HellGate.csproj`.

## Development prerequisites

A development environment requires:

1. A compatible *Night of Revenge* installation.
2. BepInEx installed in the game directory.
3. The HellGate-compatible `NoREroMod.dll` in `BepInEx/plugins/`.
4. The game-managed assemblies in `NightofRevenge_Data/Managed/`.
5. MSBuild capable of compiling a .NET Framework 3.5 project.

The project references:

- `Assembly-CSharp.dll`;
- `Assembly-CSharp-firstpass.dll`;
- `UnityEngine.dll`;
- `UnityEngine.UI.dll`;
- `BepInEx.dll`;
- `0Harmony.dll`;
- `NoREroMod.dll`;
- `ES2.dll`;
- `Rewired_Core.dll`.

Do not commit any of these third-party or game-owned assemblies.

## Local path model

`NoREroMod_HellGate.csproj` defines `NorGameRoot`, which is expected to resolve
to the local game installation root. The current repository layout assumes the
project directory is two levels below that root:

```text
NightofRevenge107/
├─ BepInEx/
├─ NightofRevenge_Data/
└─ .../<HellGate repository>/
```

If the repository is stored elsewhere, override `NorGameRoot` for the build or
adjust the local project configuration without committing a machine-specific
absolute path:

```powershell
dotnet build .\NoREroMod_HellGate.csproj -c Release `
  -p:NorGameRoot="C:\Path\To\NightofRevenge"
```

All reference `HintPath` values are derived from `NorGameRoot`.

## Build

From the repository root:

```powershell
dotnet build .\NoREroMod_HellGate.csproj -c Release
```

Expected output:

```text
bin/Release/NoR_HellGate.dll
```

Deploy the assembly to the local development installation:

```text
<NorGameRoot>/BepInEx/plugins/NoR_HellGate.dll
```

Both `NoR_HellGate.dll` and the compatible `NoREroMod.dll` must be present for
runtime validation. Use the BepInEx log to verify plugin loading, Harmony patch
registration, subsystem initialization, and the NoREroMod compatibility probe.

The build is not a complete runtime test. Changes to Harmony targets, state
transitions, scene loading, H-scene cleanup, persistence, or spawned prefabs
must also be exercised in game.

## Runtime data

Version-controlled content is maintained under:

```text
HellGateAssets/BepInEx/plugins/HellGateJson/
```

For runtime testing, synchronize that tree to:

```text
<NorGameRoot>/BepInEx/plugins/HellGateJson/
```

The data tree includes localized dialogue, QTE reactions, EventCore content,
spawn packs, faction definitions, combat AI, economy configuration, drop
tables, and opt-in diagnostic configuration.

Some modules write per-save-slot state into the runtime data tree. Generated
slot state is local runtime data and must not be copied back into the shipped
source mirror.

## External binary assets

Large production assets are intentionally excluded from Git. Runtime loaders
expect the external asset tree under:

```text
<NorGameRoot>/sources/HellGate_sources/
```

This tree includes PNG, WAV, Spine, portrait, UI, trap, custom enemy, and effect
assets. It is distributed separately from the source repository.

The canonical download location and asset-pack version are not currently
declared in this repository. When an external package is published, document
its immutable version/checksum and compatibility range before referencing it
from developer setup instructions.

Never add machine-specific absolute paths to asset loaders or committed data.

## Engineering rules

- Keep game-facing Harmony hooks thin; place feature behavior in `Systems/`.
- Register patches explicitly through `Core/Plugin.cs`.
- Isolate patch failures so one incompatible target does not abort unrelated
  modules.
- Prefer `PlayerConUpdateDispatcher` to additional `playercon.Update` patches.
- Use centralized player, camera, and controller caches on hot paths.
- Put feature gates and developer-tunable values in BepInEx configuration.
- Put content definitions and data-driven balance in JSON or spawn text packs.
- Resolve runtime assets relative to the game root.
- Preserve cleanup invariants for H-scene escape, timescale, input, overlays,
  pregnancy state, and combat control.
- Treat NoREroMod compatibility-probe failures as actionable integration
  failures.
- Write C# comments and public documentation in professional English.
- Update architecture documentation when subsystem boundaries, startup order,
  data roots, persistence, or patch ownership changes.

## Change validation

The minimum validation sequence is:

1. Confirm every added or removed C# file is reflected in the project manifest.
2. Build the Release configuration without errors.
3. Deploy the new DLL with the expected NoREroMod build.
4. Check the BepInEx log for patch and compatibility failures.
5. Exercise the changed path in game.
6. Verify scene transitions and save-slot isolation when relevant.
7. Verify that disabled feature gates preserve vanilla/companion behavior.
8. Update the corresponding technical documentation.

High-risk areas require targeted regression testing:

- `playercon.Update` and player state recovery;
- scene and additive-event transitions;
- H-scene entry, handoff, escape, and cleanup;
- save/load hooks and per-slot files;
- pregnancy birth/altar recovery;
- faction targeting and projectile ownership;
- custom prefab cloning and visual asset replacement;
- reflection into NoREroMod internals.

## Documentation policy

All public repository documentation is written for mod developers and
maintainers. It should describe verifiable current behavior, contracts, and
extension points. It should not duplicate code line by line or provide
player-facing setup and feature explanations.

Current entry points:

- [`README.md`](README.md) — repository orientation and developer bootstrap;
- [`ARCHITECTURE.md`](ARCHITECTURE.md) — architectural source of truth;
- [`docs/README.md`](docs/README.md) — documentation index;
- [`docs/modules/`](docs/modules/) — per-subsystem technical references;
- [`docs/development/`](docs/development/) — build, extension, data-format,
  and compatibility guides.

## Compatibility status

HellGate currently targets the game and companion assemblies available through
the configured local development installation. The exact supported game build
and NoREroMod commit/version are not yet recorded in this repository.

Before accepting compatibility claims, record both identifiers and validate the
startup probe plus affected runtime paths. Compatibility with unrelated
NoREroMod releases or forks must not be assumed.

## License

HellGate is open source and will always be free. The source code is licensed
under the **GNU General Public License v3.0** (see [`LICENSE`](LICENSE)).
Copyleft is deliberate: any derivative of HellGate must remain open source and
free, and no fork may be relicensed into a paid or closed product.

The license covers HellGate source code and HellGate-authored data files only.
It does not cover:

- game-owned assemblies and content (*Night of Revenge*);
- third-party libraries (BepInEx, Harmony, NoREroMod, and others);
- externally distributed binary asset packs, which may include third-party or
  commissioned material with separate terms.
