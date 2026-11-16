# Fear / Morale

Combat-facing confidence ↔ panic axis. Separate from MindBroken (H/corruption/Bad End)
and Rage (spendable resource).

Code: `Systems/Fear/` · Config: `[FearSystem]` · JSON: `HellGateJson/Fear/FearEnemies.json` ·
Save: `HellGateJson/PlayerState/PlayerFear_Slot0N.json`

## Data split

| Layer | Owns |
|-------|------|
| `[FearSystem]` cfg | Kill-switch, UI, combat bursts, soft combat FX, respawn values, HeartBeat, fallbacks |
| `FearEnemies.json` | `Default` + `enemies[]`: pressure stats and H-phase beats |

Enemy stats: `fearPerSecond` (aura), `heavyHitFear`, `projectileFear`, `hSceneFearPerSecond`,
`grabFear` (absolute |ΔMorale| on GrabViaAttack; size/threat graded, ×2 vs early draft —
goblin ~10, human ~14–20, large ~22–24, bosses ~28–34). Override field `0` = inherit from
`Default`. Enemy key = C# type name (or object name without `(Clone)`). The file is re-read at
most every 2 s, so values can be tuned while the game runs.

### `hPhases` (H-phase beats)

```json
{ "animation": "START2", "spikeMorale": -60 }
{ "animation": "ERO2", "spikeMorale": -100, "absolute": true }
{ "animation": "ERO", "moralePerSecond": -1.2 }
```

- `spikeMorale` — flat ΔMorale on entering the phase (positive values are flipped). Same beat is ignored for 1.5 s.
- `absolute: true` — `spikeMorale` is the Morale value to set (−100 = max Fear).
- `moralePerSecond` — drain while the clip plays and the player is in H.

Code (`FearHPhaseHooks.cs`) only detects the moment; values come from JSON:
- Spine-event enemies (`FearHPhaseCueHooks`): `animation` = event name — goblin, InquisitionRED, BigMerman, Gorotuki, Minotaurosu, Kakash (`next1` → `start2`).
- Clip-change enemies (`FearHPhaseTrackerHooks` → `FearAnimPhaseTracker`): `animation` = clip name — TouzokuNormal, TouzokuAxe, Inquisition, Sisiruirui, Kakash, SlaveBigAxe, SinnerslaveCrossbow (Dorei), Slaughterer (+ Butcher), SkeltonOoze, MummyMan, Coolmaiden, Undead, MummyDog (+ Wolf), WolfBoss (`GABOSS2Fatality`).
- `HighInquisition_famale`: ramp to −100 (`HighInquisitionFemaleFearControl`); JSON order = phase order, |`spikeMorale`| = step weight; the last phase sets −100.
- A new enemy needs a hook in code; new phases for an already hooked enemy need only JSON.

## Core

- Single axis `Morale ∈ [-100, +100]` (`FearSystem`). Negative = fear, positive = confidence.
- `ApplyDelta(ΔMorale, reason)`: when `Morale > 0`, negative Δ is scaled by PrideFall:
  `delta *= 1 + (Morale/100) * PrideFallMult`.
- Flat (no PrideFall): H-phase beats, High Inquisition ramp, respawn / altar / trauma sets.
- Kill-switch: `[FearSystem] Enable` (default **true**). When off: no ticks, no deltas, no bar,
  no HeartBeat, no save/load.

## Sources

Ticked from `PlayerConUpdateDispatcher` (`FearAuraTicker`) with the MindBroken realtime delta clamp.

- **H-scene / trap H** (`eroflag && erodown`): `FearCatalog.ResolveHSceneFearPerSecond()` — the eroflag enemy's
  `hSceneFearPerSecond`, else JSON `Default`, else cfg `HSceneFearPerSecond`. Aura, soft decay and combat effects are skipped.
- **Aura** (outside H): sum of the strongest nearby enemy auras (`AuraStrongestN`: 1 = strongest only,
  2+ = two strongest), linear falloff to 0 at `AuraFalloffMeters` (12). Rescan every 0.25 s.
  Crowd multiplier (`CrowdFearEnable`): ×1 / ×1.3 / ×1.4 / ×1.55 / ×1.85 for 1 / 2 / 3 / 4 / 5+
  threatening enemies in range (`CrowdFearMult2`…`CrowdFearMult5Plus`).
- **Low SP:** at SP ≤ `LowSpAuraThreshold` (30%) aura and H pressure are ×`LowSpAuraMult` (2).
- **Soft decay** toward 0 (`SoftDecayPerSecond`) when no aura and not in combat; paused during trauma.
- **H-phase beats:** JSON `hPhases`, see above.
- **White / Crow / Mutude:** mirror their MindBroken H stages (White `ERO_START2`, Crow `IKI`/`IKI2`,
  Mutude `DRINK`/`ERO3–5`) via `ApplyHPhaseFearMbRate` (MB %/s → −Morale/s).
- **Combat (cfg):** block (+Morale), counter within `CounterWindowSeconds` after block, block expiring without
  counter (heavy / light), perfect parry, dash avoid, every landed weapon/magic hit, optional combo every 5
  hits, kill (normal / boss via `FactionBossDetection`), light hit through (`HitTakenMorale`), heavy / power
  hit (`HeavyHitMorale` / JSON `heavyHitFear`), knockdown / knockout (`KnockdownMorale`, stronger; stacks only
  the extra over a just-applied heavy), projectile hit (JSON `projectileFear`), struggle QTE success.
- **Boss pressure:** when `FactionBossDetection` marks the attacker/aura source as boss — aura ×`BossAuraMult`
  (2.5), combat hits ×`BossCombatFearMult` (2.25); boss knockdown target is at least last heavy +
  `BossKnockdownBonus` (8). Elite JSON rows (WolfBoss, Requiem, Minotaurosu, …) also use higher base
  `fearPerSecond` / `heavyHitFear`.
- **Grab / H start:** GrabViaAttack uses JSON `grabFear` (else `GrabLandMorale`), ×`BossCombatFearMult`
  for bosses; `HSceneStartMorale` on H start (skipped within 0.5 s of GrabLand).
- **FIN climax:** `FinClimaxMorale` on black-background `Activate` and the Kakasi FIN path (1.2 s dedupe).
- **Rage:** activation (`RageActivationMorale`) and VengeanceStrike spend (`VengeanceStrikeMorale`) only.
- **Lethal trap / fatality Take Vengeance:** trauma rises to −100, holds, settles to `LethalRespawnFloor` (−30);
  same latch as MB shock FX. Vanilla H-fatalities (`SpDeath` while a `*Fatality` script is active) arm it too.
- **Ordinary death Take Vengeance:** Morale set to `NormalRespawnMorale` (−10).
- **Altar rest:** trauma cancelled, Morale set to 0.

## Effects

- Panic band (≤ `PanicBandThreshold`, −50): dash SP cost up to +`DashCostPanicMax` (40%) at −100.
- Confidence band (≥ `ConfidenceBandThreshold`, +50): idle SP regen up to +`StaminaRegenConfidenceMax` (25%) at +100.
- Struggle / QTE / SimpleQTE SP per click: ×`1 + (Morale/10) * StruggleSpMultPerTenMorale` (−100 → ×0.75, +100 → ×1.25), in `QTESPCalculator`.
- Combat effects above are gated by `EnableCombatEffects`.
- HeartBeat (`sources/HellGate_sources/Fear/HeartBeat.wav`, fallback `CustomDeath/`): one-shot on each fear step
  10/20/…/90% (−Morale), skipped while a one-shot is playing; at Morale ≤ −100 an intermittent pulse
  (play 3–6 s, pause 5–10 s, repeat); muted during trauma (shock owns the heartbeat) and when the player is dead.
- UI: bipolar bar (`FearUISystem`) bottom-center (Mercy mirror inset Y=32) + `BarOffsetX/Y`, `BarOpacity`.
  Visibility via `HudVisibilityGate` (hides on title / inventory like Rage). Own canvas — not under Rage.
  Panic / confidence marker positions follow `PanicBandThreshold` / `ConfidenceBandThreshold`.

## Difficulty presets

EASY / MEDIUM / HARD ship the 1.2.8 Fear keys (`HitTakenMorale`, `KnockdownMorale`,
`BossAuraMult` / `BossCombatFearMult` / `BossKnockdownBonus`, `CrowdFear*`) with
**identical** defaults. Fear pressure is not the Easy→Hard lever; that remains
MindBroken, grab chances, HP, and QTE. See
[SPLASH_OPTIONS_AND_DIFFICULTY.md](SPLASH_OPTIONS_AND_DIFFICULTY.md) and
`BepInEx/config/DIFFICULTY_PRESETS_GUIDE.txt`.

## Persistence

`FearSlotStore` writes `HellGateJson/PlayerState/PlayerFear_Slot0N.json` on the
game save/load hooks. Load clears runtime caches (combo, dash-cost latch, kill
dedupe, heartbeat) before restoring Morale.
