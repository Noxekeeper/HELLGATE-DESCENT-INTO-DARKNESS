# QTE, Struggle, and Gameplay Systems

Player-facing combat mechanics owned by `Systems/Gameplay/`.

## QTE 3.0

HellGate's own QTE implementation, replacing the NoREroMod legacy path.

- `QTESystem` — session lifecycle, button pooling, per-session enemy
  resolution.
- `QTESPCalculator` — SP reward/penalty math.
- `QTEStruggleWindowManager` — window timing between struggle phases.
- `QTE/QTEFreeStruggleMode` — gate for Free Struggle (see below).
- Config: `[QTE]`; localized reactions come from `{LANG}/QTEReactionData.json`
  through `QTEReactionFramework` / `QTEReactionDatabase` (see
  `PRESENTATION.md`).
- Button layout: default QTE = horizontal row (56px, `[QTE] ButtonSpacing`);
  Free Struggle / Simple QTE = compact d-pad cross (36px, W/A/S/D, inner-edge
  pivots so arrows meet at the center). Shared HUD center via
  `[QTE] ButtonPositionX` / `ButtonPositionY` (1080p canvas ref). Combat
  **FatalityDeathIcon** reuses that center’s screen point (UI→world map in
  `QTESystem`, then −70px Y) — see [ENEMY_FATALITY.md](ENEMY_FATALITY.md).

**Ownership rule:** the NoREroMod QTE/struggle path stays disabled by
`QTEStruggleSystemDisabler`, `QTEStruggleHistoryDisabler`, and
`StruggleCameraShakeDisabler`. Do not re-enable both paths simultaneously.

### Default QTE SP (when Free Struggle is off)

| Input | Formula | Config |
|-------|---------|--------|
| Mouse / E click | `CalculateSPGainClick()` | `ClickSPGainBase` → `ClickSPGainMin` (by MindBroken) |
| A / D | `CalculateSPGain()` | `SPGainBase` → `SPGainMin` |
| Yellow W / S | random yellow range (MB-scaled) | `YellowButtonSPGainMin` / `Max` |
| Red W / S | SP → 0 + MindBroken penalty | `RedButtonMindBrokenPenalty` |
| Wrong key on cooldown | SP / MindBroken penalties | `SPPenaltyMultiplier`, etc. |

Windows open/close on independent A/D and W/S timers
(`WindowDuration*` / `CooldownDuration*`).

### Free Struggle (`[QTEFreeStruggle]`)

Optional alternate Struggle **mode inside QTE 3.0** — not a second QTE stack.

| Key | Default | Notes |
|-----|---------|--------|
| `Enable` | `false` | Requires `[QTE] EnableQTESystem = true`. Splash **Options → Simple QTE** (live; no restart). Preserved when applying Easy/Medium/Hard presets. |

When enabled during an active Struggle / QTE session:

1. **D-pad button layout** — WASD hints form a compact joystick-style cross
   that fits above the vanilla **Struggle Out!** label:
   - W up / A left / S down / D right
   - Button size `FreeStruggleButtonSize` = **36px** (default row stays
     `BUTTON_SIZE` = **56px**)
   - Rect pivots on the **inner** edge so triangle bases meet at the HUD
     center (`FreeStruggleCrossInset` ≈ 1px)
   - Ignores `[QTE] ButtonSpacing` (row-only)
2. **Always-open windows** — `UpdateWindowLeftRight` / `UpdateWindowUpDown`
   keep both windows open; cooldowns are cleared; close/reopen cycles stop.
3. **WASD = mouse click SP** — every A/D/W/S press calls
   `QTESPCalculator.CalculateSPGainClick()` (same curve as mouse/E via
   `ClickSPGainBase` / `ClickSPGainMin`). Implementation:
   `QTESystem.OnFreeStruggleKeyPress`.
4. **No yellow/red** — W/S stay white (like A/D); color cycle and red
   penalties are skipped; yellow combo / bonus SP do not apply.
5. **No cooldown wrong-key penalties** — `ProcessWrongInput` is not run.
6. **Mouse / E unchanged** — still go through `QTEStruggleSystemDisabler`
   + click SP (same formula as Free Struggle WASD).
7. **Lockouts still apply** — `_easyESC`, struggle level 10, BadEnd, death
   continue to hide Struggle Out / stop QTE via
   `QTEStruggleWindowManager` / `CheckStruggleOutVisibility`.

**Code map**

| Piece | Role |
|-------|------|
| `Systems/Gameplay/QTE/QTEFreeStruggleMode.cs` | `IsEnabled` (QTE on ∧ Free Struggle on) |
| `QTESystem.UpdateButtonPositions` | Cross @ 36px + inner pivots when Free Struggle; row @ 56px otherwise |
| `QTESystem.FreeStruggleButtonSize` / `FreeStruggleCrossInset` | Cross size / meet inset |
| `QTESystem.UpdateWindow*` | Always-open branch |
| `QTESystem.ProcessInput` | Free Struggle → all WASD via `OnFreeStruggleKeyPress`; else default windows |
| `QTESystem.OnFreeStruggleKeyPress` | Apply click SP + green flash |
| `Plugin.qteFreeStruggleEnable` | Config bind `[QTEFreeStruggle] Enable` (splash Simple QTE) |
| `HellGateSplashOptionsMenu` | Options checkbox → live `ConfigEntry` |

Config catalog: [CONFIGURATION.md](../development/CONFIGURATION.md) § QTE /
QTEFreeStruggle. Splash UI: [SPLASH_OPTIONS_AND_DIFFICULTY.md](SPLASH_OPTIONS_AND_DIFFICULTY.md).

## Struggle UX

- `StruggleVisualIndicators` — on-screen struggle feedback
  (`[VisualIndicators]`).
- Difficulty and pleasure/MindBroken interaction are tuned via
  `[StruggleDifficulty]`, `[Ero]`, `[PleasureStatus]`.
- Escape recovery invariants live in `Patches/Player/` — see
  `COMPATIBILITY.md` before touching escape flow.
- **Lockout parity with NoREroMod:** HellGate must not reopen Struggle Out /
  QTE during intentional closes. Vanilla `_easyESC` (NOTESCAPE / fatality
  fades) and NoREroMod struggle level 10 are authoritative; do not ignore
  `_easyESC` while QTE is active, and do not time-force level 10 back to
  `-1`. Visibility goes through `QTEStruggleWindowManager.IsWindowOpen()`.

## VengeanceStrike

Parry-stab presentation package (`Systems/Gameplay/VengeanceStrike/`):
runtime + content/paths (PNG/WAV from the external
`VengeanceStrike/` asset tree), hand-glow patch, stab presentation and sound
patches, a no-grab-during-stab guard, and a player-update patch. Config
`[VengeanceStrike]` covers slow-mo, hand glow, rage cost, and spine boost.

## WeaponAnimations

Extended weapon combos (`Systems/Gameplay/WeaponAnimations/`):

- witch greatsword: `WitchFineGreatswordPatch`,
  `WitchExtendedGroundSwordComboPatch`, `WitchGreatswordComboSequences`;
- light one-hand sword: `LightOneHand3HitExtendedComboEquipPatch`;
- combo profiles under `Profiles/`; shared mechanics in
  `WeaponAnimationMechanics`. Config: `[WeaponAnimations]`.

## AirGuard

`AirGuardPatch` enables guarding while airborne (`[AirGuard]`).

## Misc

- `EnemyConstantVisibilityPatch` — keeps enemies rendered when required.
- `PlayerEroContextUtility` — shared player ERO state queries used by several
  modules.
