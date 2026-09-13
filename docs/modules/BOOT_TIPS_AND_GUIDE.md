# Boot Tips and Tutorial Guide

Early-boot presentation for first-run / every launch hydrate: rotating **loading
tips** while Forest and spawn caches warm, then an **illustrated guide** (text +
mute MP4) before the HellGate splash unlocks Start.

Code: `Systems/UI/HellGateBootTips.cs`, early-boot path in
`Systems/UI/LoadingScreenSystem.cs` (`HellGateSplashScreen`) · Data:
`BepInEx/plugins/HellGateJson/{LANG}/BootLoadingTips.json` · Media:
`sources/HellGate_sources/Tutorial Guide source/*.mp4`

Fonts go through `HellGateFontProvider` (Western / Asian). Guide video uses
`VideoAudioOutputMode.None` (no clip audio over title BGM).

## Player flow

```text
Language gate (if HellGateLanguage empty)
        │
        ▼
 Early Boot LOADING          tips every intervalSeconds (default 10s)
   IMGUI tips + ticker       last tip may holdUntilDone until cache done
   + progress bar            Forest harvest + splash spawn/enemy hydrate
        │
        ▼
 Early Boot GUIDE            title + left text + right video
   |◀  ||/▶  ▶| under video  auto-advance loops; Back/Forward reset timer
   Done → splash             Done always exits (not “next clip”);
                             Done / nav use IMGUI hover scale (~1.12)
        │
        ▼
 HellGate splash / title menu
```

`Plugin` boot and `SplashLoadingGateRoutine` own progress and phase flip;
`HellGateBootTips` owns tip content, timers, and locale reload.

## Phases

| Phase | Tips source | Advance | Video |
|-------|-------------|---------|-------|
| `loading` | `phase: "loading"` | Auto every `intervalSeconds`; skip advance on `holdUntilDone` until hydrate finishes | none |
| `guide` | `phase: "guide"` | Auto every `intervalSeconds` (wraps); Back / Forward / arrows; Space pause; Enter / Done exit | optional `video` path |

Guide icon row sits under the **last drawn video frame** (column fallback while
`Prepare()` has no texture) so controls do not flash to screen-center on clip
change.

## Locale JSON

Path (runtime): `BepInEx/plugins/HellGateJson/{LANG}/BootLoadingTips.json`  
Packaging copy: `HellGateAssets/BepInEx/plugins/HellGateJson/{LANG}/…`

Languages match the language picker: `EN`, `RU`, `JP`, `CN`, `KR`, `FR`, `DE`,
`PT`, `BR`, `ES`. Missing file → EN → short built-in stubs.

### UI block

| Key | Role |
|-----|------|
| `intervalSeconds` | Tip / clip auto-advance (seconds) |
| `cachingMessage` | Pulsing label above the early-boot log |
| `doneButton` | Bottom guide exit label (legacy key `nextButton` still accepted) |
| `tutorialTitle` | Guide header (e.g. `TUTORIAL`, `ТУТОРИАЛ`, `ANLEITUNG`) |

### Tip entry

| Field | Role |
|-------|------|
| `id` | Stable id (`parry`, `qte2`, …) — for authors / diffs, not runtime lookup |
| `phase` | `loading` or `guide` |
| `text` | Rich text: `<b>`, `<color=#RRGGBB>`; `\n\n` = paragraph breaks |
| `holdUntilDone` | Loading only: keep tip until splash hydrate completes |
| `video` | Guide only: path under `sources/HellGate_sources/` |

`[Key]` tokens in tip text are highlighted at display time (`StyleKeyHints`) —
warm cream color; bold is added only when the key is not already inside a
`<b>…</b>` span (Unity 5.6 IMGUI rich text cannot nest `<b>` and will print
tags as plain text). Prefer writing `[G]`, `[ЛКМ]`, etc. in JSON; bare
`[…]` is enough. Keys already wrapped as `<b>[G]</b>` stay cream + bold.
Headings that include a key at the end (`<b>Rage Mode [G]</b>`) keep the
outer bold span; only the key gets the cream color.

Semantic colors used in QTE tips: yellow `#E8C84A`, red `#E05050`.

### Tip inventory (canonical EN order)

**Loading (6):** `manifesto`, `menu_note`, `rage_meter`, `mindbroken`,
`rage_tiers`, `controls` (`holdUntilDone`).

**Guide (12):** `parry`, `rage`, `timeslomo`, `healthpotion`, `ambushthoughts`,
`block`, `rageescape`, `qte`, `qte2`, `dash`, `combatcamera`, `hcameracontrol`
— each with a matching MP4 under `Tutorial Guide source/`.

## Media layout

```text
sources/HellGate_sources/
  Tutorial Guide source/
    Parry.mp4
    Rage.mp4
    TimeSloMo.mp4
    HealthPotion.mp4
    AmbushThoughts.mp4
    Block.mp4
    RageEscape.mp4
    QTE.mp4
    QTE2.mp4
    Dash.mp4
    CombatCamera.mp4
    H-CameraControl.mp4
```

Folders are outside the git tree; ship with the game install. Missing clips log
a warning and leave the right panel empty.

## Code map

| Type | Role |
|------|------|
| `HellGateBootTips` | Load/parse JSON, phase, timer, nav epoch, key highlighting, media path resolve |
| `HellGateSplashScreen` (in `LoadingScreenSystem`) | Early-boot IMGUI, language gate, video host, guide chrome, splash gate |
| `HellGateFontProvider` | OS fonts for tip / title / Done label |
| `SpawnCacheWeatherGuard` | Suppresses rain / AudioSources / Ero buses during additive hydrate (keeps title BGM clean) |

Early-boot Canvas root is a full-screen black **input block** only; tip text,
log, and bar are IMGUI (Canvas labels do not paint reliably during
NoRSceneLoader scene flood).

Loading tips that exceed the tip band (e.g. Basic controls) auto-shrink font
and keep a reserved stack for caching label + log + progress bar so lines are
not clipped by the wait message.

## Authoring checklist

1. Keep tip `id` / `phase` / `video` paths identical across all languages.
2. Translate `text` + UI strings; leave on-screen game labels (`Struggle Out`,
   `MindBroken`, `EASY`) as players see them in HUD when intentional.
3. Sync `HellGateAssets/.../BootLoadingTips.json` when editing plugins JSON.
4. After locale or layout changes, verify: loading tip rotation, `controls`
   fully visible above caching line, guide clip switch without icon jump,
   Done → splash, one non-EN language (fonts).

## Related

- UI / fonts overview: [PRESENTATION.md](PRESENTATION.md)
- Locale roots: [DATA_FORMATS.md](../development/DATA_FORMATS.md)
- Splash hydrate / spawn cache: [SPAWN.md](SPAWN.md)
