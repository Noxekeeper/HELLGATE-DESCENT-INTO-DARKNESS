# Contributing

HellGate is a hobby project, but the codebase runs inside a live game where
a careless patch means a soft lock in someone's save. The bar for merging is
therefore less about style and more about not breaking invariants.

By contributing you agree that your contribution is licensed under
**GPL-3.0** (see [`LICENSE`](LICENSE)).

## Before you start

1. Read [`ARCHITECTURE.md`](ARCHITECTURE.md) — layers, `Awake()` order,
   patching model.
2. Set up the environment per
   [`docs/development/BUILDING.md`](docs/development/BUILDING.md). The
   target is .NET Framework 3.5 / Unity 5.6.7 — modern C# runtime features
   are not available.
3. For anything touching player state, H-scenes, or scene transitions, read
   [`docs/development/COMPATIBILITY.md`](docs/development/COMPATIBILITY.md)
   and [`docs/development/PLAYER_GUARDS.md`](docs/development/PLAYER_GUARDS.md)
   first.

## Hard rules

These come from real incidents; violating them is grounds for rejection
regardless of how well the rest of the change works:

- No parallel H-scene escape/recovery paths — extend the existing cleanup
  chain.
- Per-frame player logic goes through `PlayerConUpdateDispatcher`, never a
  new `playercon.Update` patch.
- Persistence goes through the save/load hook patches, per-slot files only.
- Every feature ships with an off switch (cfg or JSON gate) and must be
  fully inert when disabled.
- Comments and identifiers in English; player-facing strings go through the
  localization data, never hardcoded.
- Do not reintroduce anything listed under "Incident-derived rules" in
  COMPATIBILITY.md.
- No reflection into NoREroMod internals beyond the existing contact
  surface.

## Validation

A clean Release build is necessary but not sufficient. Run the checks from
[`docs/development/TESTING.md`](docs/development/TESTING.md) for the change
area you touched, and say in the PR which passes you ran. Changes in
high-risk areas (H-scene flow, guards, save/load, scene transitions,
pregnancy) without stated in-game verification will not be merged.

## Content and localization

- JSON/data contributions follow the schemas in
  [`docs/development/DATA_FORMATS.md`](docs/development/DATA_FORMATS.md) and
  [`docs/development/EVENTCORE_DATA.md`](docs/development/EVENTCORE_DATA.md).
- EventCore string content must ship complete for every language folder it
  claims — string keys fail closed, they do not fall back across languages.
- New enemies/features: follow the checklists in
  [`docs/development/ADDING_FEATURES.md`](docs/development/ADDING_FEATURES.md)
  and [`docs/development/ADDING_ENEMIES.md`](docs/development/ADDING_ENEMIES.md).

## Documentation

Documentation lies only when it disagrees with the code, so keep them
moving together:

- If you change behavior described in `docs/modules/` or
  `docs/development/`, update the document in the same PR.
- If you add or change cfg bindings, note it in the PR;
  `docs/development/CONFIGURATION.md` is regenerated from the live cfg by
  maintainer tooling at release time.
- Add a line to the `Unreleased` section of [`CHANGELOG.md`](CHANGELOG.md)
  for anything player- or modder-visible.

## Pull requests

- One concern per PR; keep refactors separate from behavior changes.
- Describe what you tested in-game, on which map/enemy, and how many
  repetitions for H-scene-adjacent changes.
- Compatibility target is fixed: Night of Revenge 1.07, BepInEx 5.4.18, the
  bundled NoREroMod fork build. PRs adding compatibility layers for other
  versions or forks are out of scope.
