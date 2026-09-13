# Splash Options and Difficulty Presets

Splash **Options** panel on the HellGate disclaimer screen: Gore Content toggle,
Simple QTE toggle, Easy / Medium / Hard balance presets, Language submenu
(2-column grid), Done / Exit. Compact layout so the Options label stays below
the adult-content warning (especially JP wrap).

Code: `Systems/UI/HellGateSplashOptionsMenu.cs`,
`Systems/Difficulty/HellGateDifficultyPresetModule.cs`, wire-up in
`Systems/UI/LoadingScreenSystem.cs` (`HellGateSplashScreen`) · Labels:
`Systems/UI/SplashScreenUILabels.cs` · Runtime data:
`BepInEx/config/EASY|MEDIUM|HARD/`, selection sidecar
`BepInEx/config/HellGateDifficulty.selection` · Author guide (install tree):
`BepInEx/config/DIFFICULTY_PRESETS_GUIDE.txt`

Fonts go through `HellGateFontProvider`. Options UI is procedural
`UnityEngine.UI` (text + solid Image buttons); designer PNG frames can replace
the rectangles later without changing preset logic.

## Player flow

```text
HellGate splash (after boot tips / guide)
        │
        ├── Start          → title menu (unchanged)
        │
        └── Options        plain red label above Start; hover scale ~1.15
                │
                ▼
         Options panel     Start hidden; panel hangs under Options label
           Gore checkbox   General.EnableGoreContent (live; no restart)
           Simple QTE      QTEFreeStruggle.Enable (live; no restart)
           EASY MEDIUM HARD  copies both cfg presets; restart required
           Done | Language | Exit
             │
             └── Language  centered submenu (hides Options panel)
                   warning: «restart the game after selecting a language»
                   2-column grid (EN/JP, RU/CN, …); current lang highlighted
                   Cancel → back to Options
                   pick other lang → write HellGateLanguage + Config.Save + Quit
                                     (player must launch the game again)
```

Vanilla in-game difficulty (Easy / Normal / Hard / Very Hard) is **separate**.
These presets only swap HellGate + NoREroMod balance config files.

## Preset apply

On Easy / Medium / Hard click:

1. Snapshot player prefs from the active HellGate cfg + live session
   (`HellGateLanguage`, `EnableGoreContent`, `ShowSplashScreenOnStartup`,
   font family keys, and `[QTEFreeStruggle] Enable`).
2. `File.Copy` from `BepInEx/config/{EASY|MEDIUM|HARD}/`:
   - `NoREroMod.cfg` → `BepInEx/config/NoREroMod.cfg`
   - `NoREroMod_HellGate.cfg` → `BepInEx/config/NoREroMod_HellGate.cfg`
3. Upsert prefs **under `[General]`** and strip orphan duplicate
   `HellGateLanguage` / gore / splash lines outside that section (orphans
   re-triggered the first-run language picker). Also restore
   `[QTEFreeStruggle] Enable` so Easy/Medium/Hard do not wipe Simple QTE.
4. Write selection sidecar `HellGateDifficulty.selection` (`EASY` / `MEDIUM` /
   `HARD`). Sidecar avoids fighting BepInEx `Config.Save()` which would rewrite
   in-memory HellGate values over the just-copied preset.
5. Show localized restart notice. **Balance keys need restart** — systems that
   cached Awake values. After copy, HellGate `Config.Reload()` runs so a later
   `Config.Save()` cannot wipe the preset with stale RAM. Re-clicking the same
   difficulty re-applies (repairs a wiped disk). On exit, the selected preset is
   re-asserted once if it was applied this session.

| Folder | Role (summary) |
|--------|----------------|
| `EASY` | Default on first install (no selection sidecar). Soft pressure, generous QTE / bloodline, potion escape |
| `MEDIUM` | Mid HellGate pressure |
| `HARD` | Tougher enemies, grabs through block, stronger MindBroken |

Key value comparison lives in `DIFFICULTY_PRESETS_GUIDE.txt` next to the
folders (HP bands, dash SP, grab chances, MindBroken / pleasure / QTE).

Same on all three presets: `allowPotionEasyEscape = true` (HP potion / Q can
break struggle). Code bind default is also `true` (EASY-aligned).

**First run:** after language pick and before `SetUpConfigs`, if
`HellGateDifficulty.selection` is missing, the active cfgs are copied from
`config/EASY/` (language / gore / splash / fonts / Simple QTE preserved).
That avoids shipping a pre-filled HellGate cfg (which would skip the language
picker) while still landing on Easy balance.

Manual apply (no splash): copy both files from a preset folder into
`BepInEx/config/`, then restart.

## Language change

| Path | Behavior |
|------|----------|
| First boot | Full-screen flag picker if `HellGateLanguage` empty |
| Options → Language | Submenu; pick writes `General.HellGateLanguage`, quits app |
| Same language | Closes submenu only (no quit) |
| After quit | Player relaunches; splash / dialogue / tips load the new locale |

Warning copy (all 10 UI langs) is imperative: restart **after** selecting —
not “the game will auto-restart.”

## Gore Content

Same gate as before, now inside Options:

| Surface | Behavior |
|---------|----------|
| Splash checkbox | Writes `General.EnableGoreContent` |
| When on | DeadArmor death PNG clips, CustomDeath lethal traps, and EnemyFatality combat fatalities may run |
| When off | Those presentations stay off |
| Not gated | Armored grab-throw (`[DeadArmor] ArmoredGrabThrowEnable`) |

After a preset copy (and when opening Options), the checkbox re-syncs from the
active cfg / ConfigEntry (`SyncGoreToggleFromActiveConfig`).

See [DEAD_ARMOR.md](DEAD_ARMOR.md) / [HELL_TRAPS.md](HELL_TRAPS.md) /
[ENEMY_FATALITY.md](ENEMY_FATALITY.md).

## Simple QTE

Splash checkbox for Free Struggle mode (`[QTEFreeStruggle] Enable`). Label:
localized **Simple QTE**. Applies immediately — `QTEFreeStruggleMode` reads the
live `ConfigEntry`; no restart. Still requires `[QTE] EnableQTESystem`.

| Surface | Behavior |
|---------|----------|
| Splash checkbox | Writes `QTEFreeStruggle.Enable` |
| Preset copy | Preference restored after Easy/Medium/Hard (not overwritten by preset file) |
| Sync | `SyncSimpleQteToggleFromActiveConfig` when opening Options / after preset |
| Button layout | Compact **36px** d-pad cross (W/A/S/D, inner pivots); default QTE stays 56px row — [QTE_STRUGGLE_AND_GAMEPLAY.md](QTE_STRUGGLE_AND_GAMEPLAY.md) |

See [QTE_STRUGGLE_AND_GAMEPLAY.md](QTE_STRUGGLE_AND_GAMEPLAY.md).

## Layout contract

| Constant (`HellGateSplashOptionsMenu`) | Role |
|----------------------------------------|------|
| `PanelHeight` (~286) | Keep Options label below adult warning (JP); fits Gore + Simple QTE |
| `PanelBottomMargin` | Keep panel bottom above screen edge |
| `PanelGapUnderLabel` | Gap between Options label and panel top |

`HellGateSplashScreen` places the Options label at
`max(above Start, PanelBottomMargin + PanelHeight + PanelGapUnderLabel)` so
the compact panel fits without overlapping the warning block.

Difficulty / language buttons use manual RectTransform placement (nested
`HorizontalLayoutGroup` previously collapsed children to zero size). Options
UI objects use `_XUAIGNORE` so AutoTranslator does not rewrite labels.

## Code map

| Type | Role |
|------|------|
| `HellGateSplashOptionsMenu` | Options label, panel, Gore, Simple QTE, difficulty, Language submenu, Done / Exit |
| `HellGateDifficultyPresetModule` | Parse/save selection, copy both cfg files, preserve prefs (General + QTEFreeStruggle), display colors |
| `HellGateSplashScreen` | Creates Options after Start; unlocks Options when loading gate finishes |
| `SplashScreenUILabels` | Localized Options / Simple QTE / Difficulty / Language / Cancel / restart notices / Done / Exit; preset ids stay `EASY`/`MEDIUM`/`HARD`; language grid uses native display names |

## Authoring checklist

1. Keep all three preset folders with **both** cfg filenames present.
2. After editing a preset, re-copy or re-select that difficulty on splash and
   restart before validating gameplay.
3. Do not call `Plugin.Config.Save()` immediately after `TryApplyPreset` — it
   would overwrite the copied HellGate cfg with stale in-memory values.
   Preference keys are restored by the module itself after copy.
4. Keep `DIFFICULTY_PRESETS_GUIDE.txt` in sync when changing Easy/Medium/Hard
   balance numbers.
5. Verify: Options open/close without covering the adult warning; Gore and
   Simple QTE persist across Medium/Hard; language survives a Medium/Hard
   switch; Language submenu Cancel works; pick other language quits with cfg
   saved; Done restores Start; Exit quits.

## Related

- Install-tree numbers: `BepInEx/config/DIFFICULTY_PRESETS_GUIDE.txt`
- UI / fonts overview: [PRESENTATION.md](PRESENTATION.md)
- Boot tips before splash: [BOOT_TIPS_AND_GUIDE.md](BOOT_TIPS_AND_GUIDE.md)
- Generated cfg keys: [CONFIGURATION.md](../development/CONFIGURATION.md)
  (`General.EnableGoreContent`, `General.HellGateLanguage`,
  `QTEFreeStruggle.Enable`, `allowPotionEasyEscape`)
- NoREroMod vs HellGate cfg ownership: [COMPATIBILITY.md](../development/COMPATIBILITY.md)
