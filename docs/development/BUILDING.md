# Building and Deploying

## Prerequisites

1. A compatible *Night of Revenge* installation.
2. BepInEx installed in the game directory.
3. The HellGate-compatible `NoREroMod.dll` in `BepInEx/plugins/`.
4. MSBuild or the .NET SDK able to build a .NET Framework 3.5 project
   (`References/` provides the local framework reference path via
   `FrameworkPathOverride`).

## Path model

`NoREroMod_HellGate.csproj` derives every game reference from `NorGameRoot`,
which defaults to two directory levels above the project file. If your
checkout lives elsewhere, pass the game root explicitly:

```powershell
dotnet build .\NoREroMod_HellGate.csproj -c Release `
  -p:NorGameRoot="C:\Path\To\NightofRevenge"
```

Never commit a machine-specific absolute path.

## Build

```powershell
dotnet build .\NoREroMod_HellGate.csproj -c Release
```

Output: `bin/Release/NoR_HellGate.dll`.

The compile list is explicit: a new `.cs` file builds only after it is added
as `<Compile Include="...">` in the `.csproj`. A build that succeeds while
your new file is missing from the manifest is a silent failure — check the
manifest first when a change appears to have no effect.

## Deploy

1. Copy `NoR_HellGate.dll` to `<NorGameRoot>/BepInEx/plugins/`.
2. Ensure runtime data is present at
   `<NorGameRoot>/BepInEx/plugins/HellGateJson/` (synchronized from
   `HellGateAssets/BepInEx/plugins/HellGateJson/`).
3. Ensure the external asset tree exists at
   `<NorGameRoot>/sources/HellGate_sources/` for asset-dependent features.

## Runtime verification

Start the game and check the BepInEx console/log for, in order:

1. plugin banner with the expected version (`Core/PluginInfo.cs`);
2. Harmony patch registration — any `PatchTypeWithLog` failure names the
   offending type;
3. subsystem initialization messages;
4. the NoREroMod compatibility probe — a missing-symbol warning means the
   companion build does not match and must be resolved, not ignored.

A clean build is not a behavior test. Anything touching scene transitions,
H-scene escape, persistence, or spawned prefabs must be exercised in game
(see the high-risk list in the root `README.md`).
