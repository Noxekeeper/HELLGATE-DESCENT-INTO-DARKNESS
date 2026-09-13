# LostSounds

Scaffold module for **replacing or adding** SFX that vanilla Night of Revenge
does not cover well (or at all). Isolated from `Systems/Audio/` (AttackSounds /
DeathSounds).

Code: `Systems/LostSounds/` · Config: `[LostSounds]` · Assets:
`sources/HellGate_sources/LostSounds/`

**Status:** foundation only — loads WAVs and exposes `Play(cueId)`. No Harmony
hooks ship yet; enable when you add cues and wire patches.

## Why “LostSounds”

Name = restored / previously missing sounds (and a place for future overrides).
Vanilla example: `playercon.Jump_fun` never calls `MasterAudio` for jump
(including double-jump). Dodge already has `snd_step` / `snd_step1`; custom
layers or replacements can live here later.

## Layout

```text
Systems/LostSounds/
  LostSoundsConfig.cs      # [LostSounds] binds
  LostSoundsPaths.cs       # relative resolve (DeadArmor-style candidates)
  LostSoundsAudio.cs       # load all *.wav + Play(cueName)
  LostSoundsBootstrap.cs   # preload when Enable

sources/HellGate_sources/LostSounds/
  *.wav                    # cue id = file stem (jump1.wav → "jump1")
  README.txt               # author note
```

No absolute / machine-specific paths. Resolution matches DeadArmor:
game root → parent → `BepInEx/plugins/NoR_HellGate/`.

## Runtime API

```csharp
// After bootstrap (Enable = true), play a loaded cue:
LostSoundsAudio.Play("jump1");           // uses MasterVolume
LostSoundsAudio.Play("dash", 0.8f);      // extra scale × MasterVolume
LostSoundsAudio.HasCue("jump1");         // false if file missing / not loaded
```

Playback uses `GoldAudioPlayer.Play2D` (full 2D, no spatial falloff).

## How to add a new cue

1. Drop `myCue.wav` into `sources/HellGate_sources/LostSounds/`.
2. Set `[LostSounds] Enable = true`.
3. Add a thin Harmony patch (new `LostSoundsPatches.cs` or feature-owned patch)
   that calls `LostSoundsAudio.Play("myCue")` at the right moment.
4. Register the patch class in `Plugin.SetUpPatches()` via
   `LostSoundsPatches.Apply(harmony)` or `BootPatch(...)`.
5. Document the cue in this file’s “Planned / shipped cues” table.

Keep hooks **event-based** (`Jump_fun`, `step_fun`, etc.) — do not add another
`playercon.Update` patch (`ARCHITECTURE.md` §5.2).

### Example hooks (not shipped)

| Cue file | Intended hook | Notes |
|----------|---------------|--------|
| `jump1.wav` | `playercon.Jump_fun` Prefix | Grounded jump (`m_Grounded`) |
| `jump2.wav` | same | Air / double-jump (`!m_Grounded`, `jumpcount > 0`) |
| `dash.wav` | `playercon.step_fun` Prefix | Layer on dodge start; vanilla `snd_step` may stay |

Private fields (`stepkind`, `airstepcount`) → `Traverse` (see AirGuard).

## Init order

1. `LostSoundsConfig.Initialize()` early in boot (with DeadArmor / Costumes).
2. `LostSoundsBootstrap.Initialize(host)` in `RunBootAssetPreloadImmediate`
   (loads WAVs only if `Enable`).

## Config

| Key | Default | Description |
|-----|---------|-------------|
| `Enable` | `false` | Master switch; off until cues exist |
| `AssetsPath` | (empty) | Override folder; empty = `sources/HellGate_sources/LostSounds` |
| `MasterVolume` | `1` | Global volume for all LostSounds plays |
| `DebugLogging` | `false` | Verbose BepInEx logs |

Also listed in [CONFIGURATION.md](../development/CONFIGURATION.md) § LostSounds.

## Boundaries

- **Does:** path resolve, WAV preload, 2D play-by-name.
- **Does not:** AttackSounds / enemy death VO (`Systems/Audio/`), DeadArmor /
  EnemyFatality / HellTraps clip SFX, MasterAudio bus editing.
- When replacing a vanilla MasterAudio cue, decide explicitly whether to
  suppress the original (StopAllOfSound / Prefix skip) or layer on top.

## Planned / shipped cues

| Cue id | File | Status |
|--------|------|--------|
| *(none)* | — | Scaffold — add rows when hooks ship |
