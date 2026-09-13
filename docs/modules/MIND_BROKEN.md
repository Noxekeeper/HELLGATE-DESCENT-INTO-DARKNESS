# MindBroken

Corruption state accumulated through H-scenes, with recovery mechanics,
visual presentation, and a bad-end path.

Code: `Patches/UI/MindBroken/` · Config: `[MindBroken]`, `[MindBrokenRecovery]`, `[MindBrokenVisualEffects]`, `[CorruptionCaptions]`, per-enemy `[<X>MindBroken]` · Save: shared `PlayerState/PlayerRageMindBroken_Slot0N.json`

The module lives under `Patches/UI/` for historical reasons but is a
first-class feature, not a UI tweak.

## Core

- `MindBrokenSystem` — the corruption state machine.
- `H_scenesAllEnemiesCorruption` — universal corruption gain from H-scenes
  across all enemy types.
- `MindBrokenBadEndSystem` — bad-end flow at full corruption (countdown at
  100%, then Bad End Player / YOU LOSE).
- `MindBrokenRealtimeGate` — Windows focus + hitch gate for real-time ticks
  (see below).
- `GuardParryMindBrokenPatch` (`Patches/Player/`) — corruption interference
  with guard/parry.
- Struggle difficulty interaction is configured through the struggle cfg
  sections (see `QTE_STRUGGLE_AND_GAMEPLAY.md`).

## Bad End countdown and real-time ticks

At 100% MindBroken a countdown starts (`BadEndCountdownDuration`, default
180 s). The driver uses unscaled time so menu / SlowMo `timeScale` does not
freeze it — but progress must not dump the wall-clock gap after minimize.

`MindBrokenRealtimeGate`:

- Advances only while the Unity player window (`UnityWndClass`) is
  foreground and not iconic (BepInEx console focus does not count).
- Drops hitch frames where `unscaledDeltaTime` / `deltaTime` &gt; 0.1 s
  (Unity 5.6 often reports the full minimize pause as one delta on resume).
- APIs: `GetClampedUnscaledDelta()` (countdown, black-bg, Rage MB ticks),
  `GetClampedDeltaTime()` (H-scene continuous ticks).

Wired consumers: Bad End countdown coroutine, global + per-enemy H-scene
controllers, `HSceneBlackBackgroundSystem` MB tick, Rage active/overdrive /
high-rage passive MB.

## Recovery

- `MindBrokenRecoverySystem` — recovery rules and rates.
- `EnemyKillRecoveryPatch` — per-class `OnDestroy` kill tracking for
  recovery credit.
- `MindBrokenUniversalKillRecoveryPatch` — kill-based recovery with boss
  weighting; boss classification is the shared `FactionBossDetection`
  (see `FACTIONS_AND_COMBAT_AI.md`).

## Presentation

- `MindBrokenVisualEffectsSystem` — screen-level corruption effects.
- `CorruptionCaptionsSystem` — intrusive captions at high corruption.
- The player portrait switches to the Brainwash cycle at high corruption
  (priority order in `PortraitStateResolver`, see `PRESENTATION.md`).

## Per-enemy controls

Dedicated controllers adjust MindBroken behavior for specific enemies:
Mutude, CrowInquisition, InquisitionWhite, Pilgrim
(`<Enemy>MindbrokenControl.cs`, each with its own cfg section).

## External touchpoints

- HellTraps' vengeance shock sequence applies a MindBroken shock after a
  lethal trap death (`HELL_TRAPS.md`).
- Persistence is shared with Rage through `RageMindBrokenSlotStore`
  (`RAGE.md`).
- Rage-time MB gain also goes through `MindBrokenRealtimeGate` (`RAGE.md`).
