# Release Process

Checklist for shipping a HellGate release. Releases are always free
(see the License section of the [README](../../README.md)).

## 1. Version bump

- `Core/PluginInfo.cs` → `PLUGIN_VERSION` is the single source of the
  plugin version.
- If the public API surface changed, bump `Api/HellGateApi.cs` constants per
  the policy in [API.md](API.md) (0.x: additions bump minor, breaking
  changes bump major once 1.0 ships).

## 2. Code and documentation freeze

1. Clean Release build: **0 errors**, no new warnings in changed code.
2. Regression passes from [TESTING.md](TESTING.md) for every area changed
   since the last release; at minimum the smoke pass and the H-scene
   recovery pass on the historically fragile scenarios.
3. Regenerate the cfg by launching the game once, then regenerate
   [CONFIGURATION.md](CONFIGURATION.md) (maintainer tooling) and commit both
   together.
4. Move the `Unreleased` section of `CHANGELOG.md` under the new version
   heading with a date.
5. Verify module docs affected by the release are already updated (they
   should have moved with their PRs).

## 3. Package

Release archive layout mirrors the runtime install:

| Archive path | Source |
|--------------|--------|
| `BepInEx/plugins/NoR_HellGate.dll` | Release build output |
| `BepInEx/plugins/HellGateJson/**` | runtime data tree — **exclude** `BackUp/` and `Diagnostics/` files with `Enable: true` |
| `sources/HellGate_sources/**` | binary runtime assets (audio, portraits, UI), when bundled |
| Manifesto PDFs | regenerated maintainer docs (10 languages) |

Notes:

- The cfg is **not** shipped; BepInEx generates it on first launch (a
  shipped cfg was a recurring source of "mod ignores settings" reports).
- Per-slot save JSONs (`*_Slot{NN}.json`) must not leak into the archive.
- Large binary asset packs are hosted externally (see "External binary
  assets" in the README); verify the link, pack version, and checksum still
  match.

## 4. Fresh-install verification

On a clean game copy (correct NoR version + BepInEx + NoREroMod fork):

1. Unpack the archive, launch, pick a language.
2. Log check: banner, no probe warnings, no exceptions
   (see [DIAGNOSTICS.md](DIAGNOSTICS.md)).
3. Run the smoke pass from TESTING.md, plus one save/load cycle.
4. Confirm the cfg regenerated and a changed setting takes effect after
   restart.

## 5. Publish

1. Tag the release commit `v<version>` and push the tag; create the GitHub
   release with the changelog section as the body.
2. Attach or link the archive (GitHub Release asset or external host).
3. Announce on the distribution threads/channels with the compatibility
   line (game version, BepInEx version, NoREroMod fork build) and the
   short changelog.

## 6. After release

- Open the next `Unreleased` section in `CHANGELOG.md`.
- Triage incoming reports into the bug journal; remember the most common
  false report is editing the wrong cfg file
  (see Config regression in [TESTING.md](TESTING.md)).
