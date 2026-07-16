# Grab System and Enemy Handoff

Two related mechanics: probabilistic grabs triggered by enemy attacks, and
chained H-scene handoffs between enemies.

## GrabSystem NG

Code: `Systems/GrabSystem/` · Config: `[GrabSystemNG]`

- `GrabViaAttackPatch` — converts a successful enemy attack into a grab with
  a computed probability.
- `GrabChanceCalculator` — chance math from player/enemy state.
- `DamageSourceClassifier` + `MeleeAttackerContextPatches` +
  `RangedDamageFlagPatches` — classify the damage source (melee vs ranged)
  so ranged hits do not teleport-grab.
- UI: the current grab chance is shown by `GrabChanceRageUILabel`, hosted on
  the Rage overlay canvas.
- Slow-mo interaction: when `[HSceneEffects] StartZoom.Enable` is on, the
  grab's slow-motion defers to `HSceneStartZoomEffect`.

## Enemy handoff

Code: `Systems/Handoff/` · Config: `[HandoffSystem]`, `[EnemyPass]`

After an H-scene, a nearby compatible enemy can take over ("pass") instead of
the scene ending.

- `EnemyHandoffSystem` — the global coordinator. Keeps one handoff counter
  across **all** enemy types: after the first handoff, subsequent entries are
  forced to mid-scene; the counter resets on escape or scene change.
- `DelayedHandoffScript` — real-time handoff delay, independent of slow
  motion.
- `EnemyHandoffPlayerHelper` — player state helpers for handoff entry.

Per-enemy execution lives in `Patches/Enemy/` as `*PassPatch` / `*PassLogic`
types (one per enemy family); `Base/BaseEnemyPassPatch` carries the shared
plumbing and `_Template/EnemyNamePassLogic.cs` is the scaffold for new
enemies. See `CUSTOM_ENEMIES.md` for the enemy roster and
`ADDING_ENEMIES.md` for the procedure.

Special cases: MummyMan has an extra handoff grab-block/state pair, Kakasi
hides during handoff (`KakasiHandoffHide`), Goblin has a hardcore struggle
spawn variant.
