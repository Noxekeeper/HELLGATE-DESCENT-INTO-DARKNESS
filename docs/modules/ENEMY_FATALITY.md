# EnemyFatality

Shared combat-fatality system: a qualifying enemy hit can force vanilla death,
hide Aradia, play a PNG clip + SFX + scarlet UI blink + vanilla
`FatalityDeathIcon`, freeze nearby AI, and hold the last frame until Take
Vengeance / `Death_flag`.

Code: `Systems/EnemyFatality/` · Config: `[EnemyFatality]` + per-enemy
`[EnemyFatality.*]` · Assets: external under
`sources/HellGate_sources/CustomDeath/Fatality/` (not shared with HellTraps /
DeadArmor loaders)

Requires `General.EnableGoreContent` (splash **HellGate Gore Content**).

This is **not** a grab / H-scene fatality and is unrelated to
`StartZoom.SkipEnemyFatality` (RequiemKnight death-fatality camera skip only).

---

## Architecture (shared + profiles)

| Layer | Responsibility |
|-------|----------------|
| Shared | Harmony patches, session lock, AI freeze, death force, clip player, flash, audio, sprite cache, path resolve |
| Profile | Enemy match (`Matches`), cfg section, default clip folder, tuning (HP%, chance, slow-mo, …) |

Flow: `EnemyFatalityPatches` arms a hit →
`EnemyFatalityRegistry.FindEnabledMatch` → gates →
`EnemyFatalityPlayback.Trigger(profile)`.

Only **one** combat fatality session can run at a time
(`EnemyFatalitySession`).

### Registered profiles

| Profile id | Enemy type | Cfg section | Default clip |
|------------|------------|-------------|--------------|
| `WhiteInquisitor` | `InquisitionWhite` | `[EnemyFatality.WhiteInquisitor]` | pool: `lost_leg` / `lost_head` |
| `CrawlingCreatures` | `CrawlingCreatures` | `[EnemyFatality.CrawlingCreatures]` | `…/Fatality/StillAlive` |
| `Bigoni` | `Bigoni` (not brother) | `[EnemyFatality.Bigoni]` | same pool |
| `BigoniBrother` | `Bigoni` + brother marker | `[EnemyFatality.BigoniBrother]` | same pool |
| `BlackOoze` | `BlackOoze_Monster` | `[EnemyFatality.BlackOoze]` | same pool |
| `Cocoonman` | `Cocoonman` | `[EnemyFatality.Cocoonman]` | same pool |
| `Gorotuki` | `Gorotuki` | `[EnemyFatality.Gorotuki]` | same pool |
| `HighInquisitionFemale` | `HighInquisition_famale` | `[EnemyFatality.HighInquisitionFemale]` | same pool |
| `Minotaurosu` | `Minotaurosu` | `[EnemyFatality.Minotaurosu]` | same pool |
| `Slaughterer` | `Slaughterer` | `[EnemyFatality.Slaughterer]` | same pool |
| `SlaveBigAxe` | `SlaveBigAxe` | `[EnemyFatality.SlaveBigAxe]` | same pool |
| `TouzokuNormal` | `TouzokuNormal` | `[EnemyFatality.TouzokuNormal]` | same pool |
| `Goblin` | `goblin` | `[EnemyFatality.Goblin]` | same pool |
| `GobBigAlter` | `GobBigAlter` | `[EnemyFatality.GobBigAlter]` | same pool |
| `GobRider` | `GobRider` | `[EnemyFatality.GobRider]` | same pool |
| `CrawlingSisterKnight` | `CrawlingSisterKnight` | `[EnemyFatality.CrawlingSisterKnight]` | **HeavyCritical** (LightMagic only) |
| `InquisitionRED` | `InquisitionRED` | `[EnemyFatality.InquisitionRED]` | **HeavyCritical** (Fireball SPELL only) |
| `Snailshell` / `NormalSnailshell` | `Snailshell` | `[EnemyFatality.Snailshell]` | **HeavyCritical** (WaterBall ATK2 only) |
| `Pilgrim` | `Pilgrim` | `[EnemyFatality.Pilgrim]` | **HeavyCritical** (HomingMissileConst MAGIC) |
| `SisterKnight` | `Sisterknight` | `[EnemyFatality.Sisterknight]` | **HeavyCritical** (LightMagic only) |
| `SkeltonOoze` | `SkeltonOoze` | `[EnemyFatality.SkeltonOoze]` | **HeavyCritical** (WaterBall / BoundMoveMagic / TargetRotationEnemy) |
| `Tyoukyousi` | `Tyoukyoushi` | — | **Excluded** from HeavyCritical (unreliable projectile arm) |

When `ClipPath` is empty, each trigger builds an **eligible** list from the
profile pool using **per-clip** cfg (`[EnemyFatality.Clip.*]`): `Enable`,
`HpThresholdPercent`, `Chance` (relative weight). Disabled / HP-fail /
weight-0 clips are skipped; among the rest the pick is weighted. Set
`ClipPath` to force one folder (still gated by that clip’s section when
registered).

**HeavyCritical** profiles are **magic-projectile only**: body
`OndamageSend` never arms them. Projectiles are tagged at spawn and arm on
`playerDAMAGEcol` via `fun_damage` (not `fun_damage_Improvement`).

Enemy dossiers (spawn key, type, RU name, caveats):
[ENEMY_FATALITY_DOSSIERS.md](ENEMY_FATALITY_DOSSIERS.md).

Catalog code: `EnemyFatalityProfileCatalog` (all except White).
White remains `WhiteInquisitorFatalityConfig`.
Clip gates: `EnemyFatalityClipDefs`.

Today every damaging `playerDAMAGEcol` hit from a matching enemy can arm the
fatality (then clip HP / weight / per-enemy trigger Chance / guard / airborne
gates apply). Attack-pool whitelists (specific states / moves) are not wired
yet; add them on the profile / arm path when needed.

**Production defaults:** clip `HpThresholdPercent = 0.2` (below 20% HP),
per-enemy `Chance = 0.5` (50% trigger), clip `Chance = 1` (equal weight in
shared pools).

---

## Remaining work

| Item | Notes |
|------|--------|
| Attack-pool whitelists | Still “any damaging hit” for bisect / StillAlive; wire specific attack states when needed |
| Tyoukyoushi / TyoukyoushiRed | Excluded on purpose (unreliable HomingMissileConst arm) — revisit only with a reliable hook |
| More bisect clips | Append folders to `SharedBisectClipPool` when new art ships |
| Existing player cfgs | BepInEx keeps saved values; live + EASY presets were set to 0.2 / 0.5 — other difficulty copies may still have test `1` / `1` |

---

## Enable gates

| Gate | Meaning |
|------|---------|
| `[EnemyFatality] Enable` | Master switch for all combat fatalities |
| `[EnemyFatality.<Enemy>] Enable` | Per-profile switch |
| `[EnemyFatality.Clip.<id>] Enable` | Per-clip switch (`lost_leg`, `lost_head`, `StillAlive`, `HeavyCritical`) |
| `General.EnableGoreContent` | Splash Gore checkbox |

- Runtime: `profile.IsEnabled` = master + per-enemy + gore.
- Clip pick: enabled clips that pass their own HP gate, weighted by clip
  `Chance`; then per-enemy `Chance` rolls whether the fatality fires.
- Patch registration / preload: `IsModuleEnabled` = master + per-enemy
  (ignores gore). Triggers still no-op when gore is off.

---

## Trigger pipeline

1. **`EnemyDate.OndamageSend` prefix** — tag `playerDAMAGEcol` →
   `FindEnabledMatch`; while a session is active, further
   `playerDAMAGEcol` sends are blocked (`return false`).
2. **`playercon.fun_damage_Improvement` prefix** — snapshot guard,
   `erodown`, HP / max HP **before** damage.
3. **Same method postfix** — evaluate gates; on success
   `EnemyFatalityPlayback.Trigger`.
4. **`playercon.Death_flag` postfix** — clip / flash / icon cleanup,
   session end, AI restore, player visuals restored (clears damage-red
   spine tint).

### Gate order (all must pass)

| Gate | Skip when |
|------|-----------|
| Armed hit | No enabled profile / not `playerDAMAGEcol` |
| Guard | Successful guard (`guard && !Noguard`, `erodown` unchanged) |
| Session | Another fatality session already active |
| HP | Pre-hit `Hp / AllMaxHP() >= HpThresholdPercent` |
| Chance (clip weight) | Registered clip `Chance` weight ≤ 0 |
| Trigger Chance | Per-enemy `Chance` roll fails (default `0.5`) |
| Airborne | `IsPlayerAirborne` (see below) |
| Knockdown recovering | Whitelisted **magic** hits ignored while `erodown != 0` and not in H — prefer approach-to-H |

### Airborne block

`EnemyFatalityPlayback.IsPlayerAirborne` is true if any of:

- `!player.m_Grounded`
- `player.jumpfrag`
- private `dashjumpfrag` (Traverse)
- `player.state` contains `JUMP` or `FALL` (covers `FALLSTART`)

---

## Fatality playback

Order inside `EnemyFatalityPlayback.Trigger`:

1. Load PNG frames for the profile; abort if empty.
2. Capture facing + bone world position **before** death (`dir` / pose may reset).
   Clip art faces right; `SpriteRenderer.flipX` when Aradia faced left.
3. Sorting from player `MeshRenderer` (+20 order), else profile `SortingOrder`.
4. **`ForceVanillaDeath`** — `Hp = 0`, `Death`, `tough = -999`, `erodown = 0`,
   clear ero/damage flags, `REstart_menu()`, cancel `timescale` /
   `colarrcovery`, spine `Color.white`, MasterAudio `"death1"`.
5. Begin session; pin player; settle + disable enemy AI.
6. Spawn vanilla **FatalityDeathIcon** (held until respawn).
7. Optional scarlet UI triple-blink (`WhiteFlashEnable`).
8. SFX: `Hit.wav` + `death sound.wav` together (bisect / StillAlive); **HeavyCritical**
   plays one random `Quick Death 1.wav` / `Quick Death 2.wav` at start instead and
   **mutes vanilla MasterAudio `death1`**; `Final.wav` on last PNG frame when present.
9. Spawn `EnemyFatalityClipPlayer` at bone world position (scale always `1`).

### Clip

- Scale always `1` (no `DisplayScale`).
- Frame timing uses **scaled** `Time.deltaTime` (world slow-mo slows the clip).
- Last frame **holds** until `Death_flag` (no timed auto-destroy like DeadArmor).
- Player renderers / Spine / UI Graphics hidden; restore always uses
  `Color.white` (never a damage-flash red snapshot).

### Slow-mo

`SlowMoEnable`, `SlowMoTimeScale` (clamped 0.05–1), `SlowMoDurationSeconds`
(realtime), `SlowMoStartFrame` (1-based). Vanilla death `Invoke("timescale")`
is cancelled and timescale is **re-asserted** each frame for the window.

### Scarlet blink

`EnemyFatalityFlash` — dedicated UI overlay (not orgasm `WhiteFadeIn`, not
CameraFilterPack manga). `ForceStop` also clears stuck manga Intensity on the
main camera.

### Vanilla FatalityDeathIcon

- Prefab from enemy `FatalityIcon`, prefer `HighInquisitionFemale` (then
  RequiemKnight / Candore / Sheepheaddemon), live `HighInquisition_famale`
  fallback.
- Rick skull logo markers refused.
- `ParticleSystemDestroyer` stripped; `EnemyFatalityIconHold` freezes START
  before out-fade so the icon stays opaque until respawn.
- **Screen placement (locked):** same point as the QTE WASD row.
  - QTE is **Screen Space Overlay** UI — not world space.
    Keys: `[QTE] ButtonPositionX` / `ButtonPositionY` (1080p canvas ref;
    default Y = **70** from top).
  - FatalityDeathIcon is a **world** Spine `MeshRenderer`, so HellGate maps
    a live QTE-canvas RectTransform probe → screen pixels → gameplay camera
    world via `QTESystem.GetButtonRowCenterWorldPosition`.
  - Extra nudge: `FatalityIconScreenYNudgePx = -70` (70px below the QTE row
    center). Change that constant if the logo needs a vertical tweak.
  - `EnemyFatalityIconHold` re-applies the world position every `LateUpdate`
    so camera motion cannot leave the logo stranded.
  - Draw order: sorting layer prefers `UI` / `Foreground` / topmost available,
    `sortingOrder` **8000**, and a slight pull toward the camera so the PNG
    clip / enemies cannot cover the logo.

---

## Session + enemy freeze

`EnemyFatalitySession` — ends on clip cleanup, `Death_flag`, or scene load.
While `IsActive`:

- Further enemy `playerDAMAGEcol` hits are blocked.
- Collision grabs (`CanEliteGrabPlayer`) and GrabViaAttack paths early-out
  (same idea as HellTraps death immunity).
- Lethal HellTrap hits (WebSpike/cocoon, magic arm bullets, lightning)
  skip applying `fun_damage` / custom death so they cannot stack on the
  fatality body.

`EnemyFatalityEroSuppression` (name is historical; behavior is combat freeze):

- Pin player rigidbody / clear DAMAGE·DOWN while active
  (`PlayerConUpdateDispatcher` + clip `LateUpdate`).
- Settle enemies to IDLE, clear look/attack, zero velocity, `enabled = false`.
- Restore on `RestoreCombatAi` when the session ends.

HellTraps use the same combat-freeze idea via shared
`LethalMagicTrapEroSuppression` (see [HELL_TRAPS.md](HELL_TRAPS.md)).

---

## Asset layout

Per-profile default folders under
`sources/HellGate_sources/CustomDeath/Fatality/`:

| Folder | Used by |
|--------|---------|
| `lost_leg/` | Shared bisect pool (random with `lost_head`) |
| `lost_head/` | Shared bisect pool (Lost_head; random with `lost_leg`) |
| `StillAlive/` | **CrawlingCreatures** only (30 PNG frames) |
| `HeavyCritical/` | Magic-projectile killers only (29 PNG) |

```text
<clip-folder>/
  1.png … N.png     sequential frames (1-based names)
  Hit.wav           with death sound at fatality start (optional; non-HeavyCritical)
  death sound.wav   with Hit at fatality start (optional; non-HeavyCritical)
  Quick Death 1/2.wav  HeavyCritical only — random one at clip start
  Final.wav         once on last PNG frame (optional)
```

**Facing:** clip art is authored facing **right**. No flip when Aradia looks
right; `SpriteRenderer.flipX` when she looks left (same for all profiles).

Override with per-profile `ClipPath` (relative to game root). Empty = that
profile’s `DefaultClipRelative`. `EnemyFatalityPaths` walks game root +
parents + `BepInEx/plugins` candidates. Missing wavs log a warning and skip
that cue; if `death sound.wav` is missing, audio may fall back to
`DeadArmor/DeathSounds/death sound.wav`.

Folders are outside the git tree; ship them with the game install.

---

## Config defaults

### `[EnemyFatality]`

| Key | Default | Notes |
|-----|---------|-------|
| `Enable` | `true` | Master; also needs gore |
| `DebugLogging` | `false` | Verbose BepInEx logs |
| `TauntEnable` | `true` | Master switch for post-clip taunts |
| `TauntDelaySeconds` | `2` | Realtime wait after last frame before taunt |

See **Post-clip taunts** below for JSON, mute list, and UI placement.

### `[EnemyFatality.WhiteInquisitor]` (standard profile keys)

| Key | Default | Notes |
|-----|---------|-------|
| `Enable` | `true` | Per-enemy switch |
| `HpThresholdPercent` | `0.2` | Pre-hit HP ratio must be **below** this (below 20%) |
| `Chance` | `0.5` | 0–1 (50% default) |
| `ClipPath` | (empty) | Override PNG/SFX folder |
| `BoneName` | `body` | Aradia Spine bone; sample once (no follow) |
| `ClipOffsetY` | `0` (`1.5` for CrawlingCreatures) | World Y added to bone spawn |
| `FallDistance` | `0` | World units down; `0` = stationary |
| `FallSpeedMultiplier` | `4.5` | Only if `FallDistance` &gt; 0 |
| `FrameSeconds` | `0.0625` | ~16 FPS (scaled time) |
| `SortingOrder` | `80` | Fallback if no player MeshRenderer |
| `SlowMoEnable` | `false` | World slow-mo during clip (off for now) |
| `SlowMoTimeScale` | `0.1` | 90% slowdown |
| `SlowMoDurationSeconds` | `0.5` | Realtime window |
| `SlowMoStartFrame` | `1` | 1-based frame when slow-mo starts |
| `WhiteFlashEnable` | `true` | Scarlet UI triple-blink |
| `SoundVolume` | `1` | Hit / death / Final / mid-clip |
| `MidClipSfxFile` | (empty; `bone-crack.wav` for CrawlingCreatures) | WAV name in clip folder |
| `MidClipSfxAfterFrame` | `0` (`15` for CrawlingCreatures) | 1-based frame that arms mid-clip SFX |
| `MidClipSfxDelaySeconds` | `0` | Realtime wait after arm frame |

Full Bind descriptions: [CONFIGURATION.md](../development/CONFIGURATION.md).

New enemies reuse the same key set via `EnemyFatalityBoundProfile.Bind`.

---

## Post-clip taunts

After the PNG clip reaches its **last frame**, if `TauntEnable` is on and
the killer profile is not muted, HellGate waits `TauntDelaySeconds`
(realtime), then shows one random line from the resolved phrase pool.

| Piece | Path / behavior |
|-------|-----------------|
| Shared phrases | `HellGateJson/EnemyFatality/<Lang>/phrases.json` (lost_leg / default) |
| Per-clip phrases | `phrases_<clipFolder>.json` (e.g. `phrases_lost_head.json`) — used when that clip played |
| Per-profile phrases | `HellGateJson/EnemyFatality/<Lang>/phrases_<ProfileId>.json` (e.g. `phrases_CrawlingCreatures.json`) |
| Mute list | `HellGateJson/EnemyFatality/_shared/settings.json` → `muteProfiles` |
| Pool pick | Active clip file → else profile file → else shared `phrases.json` |
| Fallback lang | Dedicated: active lang → `RU` → `EN`. Shared: active lang → `EN` |
| UI | Screen overlay via `EnemyFatalityTaunts` (not bone speech bubble) |
| Placement | Horizontally centered; vertically screen-center **+120** UI px (above defeat text; below FatalityDeathIcon when icon is on the QTE band) |
| Style | ~14pt, scarlet + black outline |
| Lifetime | Stays until Take Vengeance / `Death_flag` cleanup (no timed fade) |

Shape (shared and per-profile):

```json
{
  "phrases": [
    "First taunt…",
    "Second taunt…"
  ]
}
```

`settings.json` mute example (profile ids from the registry table above):

```json
{
  "muteProfiles": [ "Minotaurosu", "BlackOoze", "Cocoonman" ]
}
```

Default mute: non-speaking killers (ooze / cocoon / minotaur).
**CrawlingCreatures** uses its own pool (`phrases_CrawlingCreatures.json`) in
all ten language folders. **lost_head** uses `phrases_lost_head.json`
(clip-keyed; not the shared leg lines). **HeavyCritical** uses
`phrases_HeavyCritical.json` (9 lines; RU authored, adapted for all langs).
**StillAlive** uses profile `phrases_CrawlingCreatures.json` (no dedicated
clip phrase file). Other killers on `lost_leg` keep the shared bisect pool.
Phrase JSON is read as UTF-8; changing HellGate language clears and reloads
shared + dedicated clip packs (`lost_head`, `HeavyCritical`).

Hook: `EnemyFatalityClipPlayer` last frame → `EnemyFatalityTaunts.ScheduleAfterClip`.

### CrawlingCreatures / StillAlive extras

| Option | Value |
|--------|--------|
| Default clip | `…/Fatality/StillAlive` |
| `ClipOffsetY` | `1.5` (world Y above bone) |
| `HideKillerDuringClip` | `true` (enemy mesh/spine hidden for the session) |
| Mid-clip SFX | `bone-crack.wav` on **frame 15** (no delay) |
| Facing | same flipX rule as other clips |
---

## Code map

| Type | Role |
|------|------|
| `EnemyFatalityConfig` | `[EnemyFatality]` master + `LogDebug` |
| `IEnemyFatalityProfile` / `EnemyFatalityBoundProfile` | Per-enemy settings + `Matches` |
| `EnemyFatalityRegistry` | Register / `FindEnabledMatch` |
| `EnemyFatalitySession` | Single-session lock; scene-load End |
| `EnemyFatalityPatches` | Shared Harmony (hits, Death_flag, grab block) |
| `EnemyFatalityEroSuppression` | Pin player + disable enemy AI |
| `EnemyFatalityPlayback` / `EnemyFatalityIconHold` | Trigger, death, icon |
| `EnemyFatalityClipPlayer` | PNG advance, hide/restore, slow-mo |
| `EnemyFatalityFlash` | Scarlet triple-blink |
| `EnemyFatalityAudio` | Per-profile Hit+death; Final on last frame |
| `EnemyFatalityTaunts` | Post-clip killer taunt (JSON per lang + mute list) |
| `EnemyFatalityBootstrap` | Preload module-enabled profiles |
| `EnemyFatalityProfileCatalog` | Registers Bigoni…TouzokuNormal profiles |
| `EnemyFatalitySpriteCache` / `EnemyFatalityPaths` | Frames + disk resolve |
| `WhiteInquisitorFatalityConfig` | White profile: bind + register |

### Init order

See [ARCHITECTURE.md](../../ARCHITECTURE.md):

1. `EnemyFatalityConfig.Initialize()` + `WhiteInquisitorFatalityConfig.Initialize()`
   + `EnemyFatalityProfileCatalog.Initialize()` (register profiles) with other
   module configs in Awake.
2. `EnemyFatalityPatches.Apply(harmony)` in `SetUpPatches()`.
3. `EnemyFatalityBootstrap.Initialize(this)` after DeadArmor bootstrap.

---

## Extending (add another enemy)

Minimal recipe — no copy of patches/playback:

```csharp
// Systems/EnemyFatality/TouzokuFatality/TouzokuFatalityConfig.cs
internal static class TouzokuFatalityConfig
{
    internal static EnemyFatalityBoundProfile Profile { get; private set; }

    public static void Initialize()
    {
        Profile = EnemyFatalityBoundProfile.Bind(
            Plugin.Instance.Config,
            section: "EnemyFatality.Touzoku",
            id: "Touzoku",
            enableDescription: "Enable Touzoku combat fatality. Requires [EnemyFatality] Enable and General.EnableGoreContent.",
            defaultClipRelative: "sources/HellGate_sources/CustomDeath/Fatality/lost_head",
            match: enemy => enemy is Touzoku);

        EnemyFatalityRegistry.Register(Profile);
    }
}
```

Then in `Plugin.cs` next to White:

```csharp
TouzokuFatalityConfig.Initialize();
```

Also:

1. Add the `.cs` file to `NoREroMod_HellGate.csproj`.
2. Ship clip assets (or reuse `lost_head` / `lost_leg` / set `ClipPath`).
3. Document the row in **Registered profiles** above.
4. Add the cfg section to [CONFIGURATION.md](../development/CONFIGURATION.md).

Do **not** re-register Harmony or Bootstrap — shared stack already covers all
profiles.

---

## Manual test checklist

1. Gore on, master + White Enable on, clip HP `0.2`, enemy `Chance = 0.5`
   (or `1` for guaranteed testing).
2. White grounded hit below threshold → PNG + scarlet blink + FatalityDeathIcon
   + Hit/death SFX; enemies frozen; last frame holds.
3. Take Vengeance / respawn → player visible (no red tint), AI restored, icon
   gone, timescale normal.
4. Successful guard → no fatality.
5. Jump / fall hit → no fatality.
6. Gore off → no fatality presentation.
7. Optional: `DebugLogging = true` for arm / skip / trigger lines.

---

## Related

- Related dossiers: [ENEMY_FATALITY_DOSSIERS.md](ENEMY_FATALITY_DOSSIERS.md)
- Gore / splash Options: [SPLASH_OPTIONS_AND_DIFFICULTY.md](SPLASH_OPTIONS_AND_DIFFICULTY.md)
- Presentation overview: [PRESENTATION.md](PRESENTATION.md)
- Armor-break clips: [DEAD_ARMOR.md](DEAD_ARMOR.md)
- Lethal trap overlays: [HELL_TRAPS.md](HELL_TRAPS.md)
- External asset roots: [DATA_FORMATS.md](../development/DATA_FORMATS.md)
- Settings tables: [CONFIGURATION.md](../development/CONFIGURATION.md)
- Regression matrix: [TESTING.md](../development/TESTING.md)
