# Logging and Diagnostics

How HellGate logs, and how to use the built-in diagnostic kits when a bug
does not reproduce cleanly.

## Regular logging

Everything goes through the BepInEx logger (`Plugin.Log`) and lands in
`BepInEx/LogOutput.log`. Every HellGate line carries a bracketed module tag
(`[TentacleDiag]`, `[TrapBodyDiag]`, `[BAR EXIT TRACE]`, …), so a log can be
filtered per subsystem with a plain text search.

What to check first on any report:

1. The plugin banner and subsystem init lines are present (plugin loaded).
2. No `RunNoREroModCompatibilityProbe` warnings (companion fork matches).
3. No Harmony patch exceptions during startup.
4. No repeated per-frame exceptions (these also destroy performance).

Rules for new code: log through `Plugin.Log` with a module tag, log state
*transitions* rather than per-frame values, and never leave unconditional
per-frame logging in a shipped path.

## Diagnostic kits

`Systems/Diagnostics/` contains three purpose-built monitors for the
historically hardest H-scene soft locks. They are **always compiled and
initialized, but disabled by default**: each is gated by its own JSON file
under `BepInEx/plugins/HellGateJson/Diagnostics/` and is a per-frame no-op
while `Enable` is `false`.

The JSON configs are **hot-reloaded every 2 seconds** — you can toggle a kit
mid-game, mid-repro, without restarting. If the JSON file is missing, safe
defaults (disabled) apply.

All kits share the same design:

- a hidden `DontDestroyOnLoad` MonoBehaviour host polls state in
  `LateUpdate` (after all gameplay systems have ticked);
- output is tagged and capped by `MaxLogsPerSession` so a looping soft lock
  cannot produce a runaway log file;
- a periodic heartbeat line distinguishes "nothing happened" from "nothing
  was polled";
- no gameplay changes — observation only.

### Tentacle kit — `TentacleDiagnostics.json`

Target: invisible-scene / soft-lock reproductions in `Tentacle` and
`Trap_TentacleIronmaiden*` scenes. Tag: `[TentacleDiag]`, output to the main
BepInEx log.

Polls every active tentacle actor and logs any state-field transition while
the H-scene is active: `erodata` going inactive, `erospine` animation jumps,
HP drops, player `erodown` changes.

| Setting | Default | Meaning |
|---------|---------|---------|
| `Enable` | `false` | master switch |
| `HeartbeatSec` | `0.5` | heartbeat interval |
| `LogStackTraceOnErodataDeactivate` | `true` | stack trace when something deactivates `erodata` mid-scene — identifies the culprit patch |
| `LogStackTraceOnDestroyDuringHScene` | `true` | stack trace when the actor is destroyed mid-scene |
| `MaxLogsPerSession` | `500` | hard log cap |

### TrapBody kit — `TrapPlayerBodyDiagnostics.json`

Target: player body sinking/teleporting and camera offset bugs during
`Trapdata` H-scenes. Tag: `[TrapBodyDiag]`; writes both to the BepInEx log
and a dedicated file `BepInEx/LogOutput/HellGate_TrapPlayerBodyDiag.log`.

Tracks player Y position, physics simulation state, `eroflag`/`erodown`/
`_SOUSA` transitions, and camera offsets during trap scenes.

| Setting | Default | Meaning |
|---------|---------|---------|
| `Enable` | `false` | master switch |
| `HeartbeatSec` | `0.25` | heartbeat interval |
| `YDropWarnThreshold` | `0.05` | warn when the body drops more than this per tick |
| `LogStackTraceOnSimulatedEnable` | `true` | stack trace when something re-enables physics simulation mid-scene |
| `LogStackTraceOnStruggleInvul` | `true` | stack trace on `startGrabInvul` during the scene |
| `WatchAllTraps` | `true` | watch every trap, not just the active H-scene |
| `MaxLogsPerSession` | `2000` | hard log cap |

### Kinoko kit — `KinokoMushroomEroDiagnostics.json`

Target: `MushroomERO` / `GAMushroomERO` scenes freezing between Spine events
(the START6 freeze class of bug — see `EnemyLibraryEroStatusGuard` in
[PLAYER_GUARDS.md](PLAYER_GUARDS.md)). Tag: `[KinokoEroDiag]`; writes to the
BepInEx log and `BepInEx/LogOutput/HellGate_KinokoMushroomEroDiag.log`.

Harmony patches log every `OnEvent` (before and after, with `se_count` /
`count` / animation), and catch exceptions inside the handler without
altering gameplay. The monitor warns when a watched clip has waited longer
than `StuckWarnSec` for its next event.

| Setting | Default | Meaning |
|---------|---------|---------|
| `Enable` | `false` | master switch |
| `HeartbeatSec` | `0.5` | heartbeat interval |
| `StuckWarnSec` | `1.5` | seconds without an event before a stuck warning |
| `LogAllEvents` | `true` | log every Spine event |
| `LogInterestingOnly` | `false` | restrict to START/ERO/SE events |
| `MaxLogsPerSession` | `3000` | hard log cap |

## Workflow

1. Reproduce (or set up to reproduce) the failure.
2. Create/edit the kit's JSON with `Enable: true` — no restart needed; the
   monitor confirms in the log that it is armed.
3. Reproduce the bug, save the tagged log excerpt (and the dedicated
   `HellGate_*.log` file for TrapBody/Kinoko).
4. Attach the excerpt to the bug entry using the reporting template in
   [TESTING.md](TESTING.md).
5. **Set `Enable` back to `false` after verification.** Heartbeat polling of
   all actors every frame is not free; kits must never stay enabled in
   normal play.

## Adding a new kit

Follow the existing pattern: standalone JSON config with hot reload and safe
defaults, hidden host object polling in `LateUpdate`, tagged output with a
session cap, zero gameplay side effects, and registration in `Plugin.Awake`
(init) plus `PatchType(...)` for any Harmony hooks. Keep it compiled-in but
JSON-disabled — the value of these kits is that a player who hits a rare
soft lock can arm them without a special build.
