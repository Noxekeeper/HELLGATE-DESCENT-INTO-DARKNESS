# Pregnancy

Conception, trimester progression, offspring companions, bloodlines, and
shelter raid events. The largest single module in the codebase.

Code: `Systems/Pregnancy/` · Config: `[Pregnancy]` + `[Pregnancy.*]` subsections · Save: per-slot store via `PregnancySlotStore`

Master gate: `PregnancyConfig.Enable`. `PregnancyConfig.Initialize` runs early
in plugin `Awake`, before patch registration.

## Conception and tracking

- Nakadashi events are captured from `EnemyDate.Nakadasi` and polled by
  `WombMeterNakadashiPoller`.
- `SemenValueMultiplier` scales accumulation (`[Pregnancy.SemenValue]`).
- `PregnancySourceResolver` identifies the father enemy type;
  `PregnancyConceptionApplier` commits conception into `WitchPregnancyState`.
- `WombMeterHud` (+ shared `WombMeterHudLayout`) renders the meter and
  suppresses the vanilla creampie value UI.

## Progression

- `TrimesterProgression` advances trimesters over play time;
  `TrimesterVisualEffects` and `PregnancyVanillaScaleSuppressionPatch` own the
  visual side; `TrimesterPhysicsPatch` applies movement/physics modifiers.
- `FactionTrimesterModifier` lets faction standing alter progression.
- Blocking options (`[Pregnancy.Blocking]`) can restrict actions while
  pregnant.

## Offspring

- `OffspringArchetype/` rolls a weighted archetype from JSON
  (`OffspringArchetypeCatalog`, `OffspringArchetypeRoll`) and resolves a
  companion prefab (`OffspringPrefabResolver`,
  `OffspringEnemyCompanionSetup`).
- `WitchOffspring*` patches control spawn setup, visuals, transformation,
  combat rules, and friendly fire toward the player.
- Offspring spawn at the hideout (`OffspringHideoutSpawner`,
  `HideoutSceneUtility`); birth spawning is overridden by
  `BirthSpawnOverridePatch` (slime capture special case:
  `BirthSlimeCapturePatch`).
- Bloodlines: `OffspringBloodlineBonuses` and `BloodlineRageBonus` grant
  permanent bonuses per father lineage (`[Pregnancy.Bloodline]`).

## Shelter attacks

`ShelterAttack/` schedules hostile raids against the shelter: wave definitions
parsed from JSON (`ShelterAttackWavesJsonParser`), a spawn scheduler, scene
poller/guard, outcome + presentation, timer HUD, phrases, and a per-slot store.
Gate: `[Pregnancy.ShelterAttack]`.

## Safety and recovery

Pregnancy touches vanilla death/recovery flows, which historically caused
soft locks. The guard set is mandatory:

- `PregnancyBirthGuardPatch` (nested pre/postfixes on birth flow);
- `BirthRecoveryJigoPatch` + `BirthRecoveryStruggleState`
  (`Patches/Player/`) — post-birth player recovery;
- altar, vengeance, and runtime cleanup (`PregnancyAltarCleanup`,
  `PregnancyVengeanceCleanup`, `PregnancyRuntimeCleanup`);
- `WhiteFadeInNullSafePatch` — null-safe fade during recovery.

Do not modify birth or recovery behavior without regression-testing altar
rest, vengeance respawn, and H-scene escape while pregnant.

## Persistence

`PregnancySlotStore` + `PregnancyPersistenceHooks` save and load per-slot
pregnancy state alongside the game's save slots.
