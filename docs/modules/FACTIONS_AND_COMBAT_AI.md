# Factions and Combat AI

Enemy-vs-enemy combat, faction identity, and player reputation.

Code: `Systems/CombatAi/` (+ `Factions/`) · Data: `HellGateJson/CombatAi/` · Save: `CombatAi/PlayerReputation_Slot{NN}.json`

## Combat AI

JSON-driven reaction tuning for vanilla enemies:

- `EnemyDateDistanceFunPatch`, `EnemyDateOndamageSendPatch` — distance and
  on-damage behavior driven by `CombatAi/*.json`;
- `DoreiCombatAiConfig` — Dorei-specific profile;
- `SinnerslaveCrossbowCombatAiPatch` — ranged special case.

## Faction runtime

- `EnemyFactionsConfig` loads `CombatAi/Factions.json` and hot-reloads it
  (~2 s polling), so faction balance is editable without restart.
- `EnemyFactionRuntime` assigns factions at `EnemyDate` bootstrap
  (`EnemyDateFactionBootstrapPatch`); spawn lines can override via
  `|faction=` (`SpawnFactionOverride`).
- Identity/visuals: `FactionIds`, `FactionStyle`, bone marker attachment,
  marker visibility, enemy tint (`EnemyDateFactionColorPatch`).

## Inter-faction combat

- All-vs-all damage routing, including projectiles: `FactionProjectileOwner`
  tags projectiles with their shooter's faction and `FactionProjectilePatches`
  routes hits.
- Activation radius around the player limits how much of the map fights.
- Target distance cap prevents cross-map aggro.
- **Combat commit**: once two enemies engage, the fight continues even if the
  player leaves (`EnemyDateFactionUpdateSustainPatch`).
- Friendly fire within a faction is a config toggle.
- Faction fighting freezes while the player is in an H-scene.

## Player reputation

- `PlayerFactionReputation` tracks per-faction standing; persisted per save
  slot through `PlayerFactionReputationSaveHook`.
- Reputation moves from kills, avoided attacks
  (`PlayerAvoidedAttackTriggerPatch`), H-scene events
  (`FactionHSceneReputationBridge`, driven from `PlayerConUpdateDispatcher`),
  and provocation — including player magic, attributed via `mgname` ownership
  (`EnemyDateFactionProvocationPatch`).
- Sign-based aggro: hostile reputation makes a faction attack on sight;
  positive reputation de-prioritizes the player as a target
  (`EnemyDateFactionTargetingPatches`, vision override, FOV compatibility).
- Reputation scales enemy speed/vision (`FactionReputationBehavior`,
  `FactionReputationDynamics`).
- **Mercy de-escalation**: `FactionDeescalationRuntime` +
  `MercyEventUISystem` let a losing enemy surrender instead of dying.
- `FactionReputationHud` is a toggleable HUD panel.

## Boss policy

`FactionBossDetection` identifies bosses via the vanilla BOSS flag, name
prefixes, and JSON lists. Rage and MindBroken kill logic share this detector —
change it in one place only.
