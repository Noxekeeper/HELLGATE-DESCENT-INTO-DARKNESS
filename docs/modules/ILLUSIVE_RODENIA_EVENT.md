# Illusive / Rodenia Church Event (SlaveBigAxe)

Vanilla story beat in **地下教区ロデニア** (`UndergroundChurch` → event scenes).
HellGate keeps the scripted SlaveBigAxe pair on the **vanilla** path and
optionally repairs a vanilla lose→scene gap.

Code: `Patches/Enemy/SlaveBigAxe/SlaveBigAxeIllusiveEventGate.cs`,
`SlaveBigAxeIllusiveLoseHandoffPatch.cs`  
Related: [MEAT_ARMOR.md](MEAT_ARMOR.md), [DEAD_ARMOR.md](DEAD_ARMOR.md),
[COSTUMES.md](COSTUMES.md), [FACTIONS_AND_COMBAT_AI.md](FACTIONS_AND_COMBAT_AI.md)

## Scene flow (vanilla)

| Scene | Role |
|-------|------|
| `EvuChurch` / `EVuChurch` | Talk (`EvBigAxeTalkMng`, dialogue **100306+**) → battle (`EvBigAxeBattleMng` + combat `SlaveBigAxe`) |
| `EvuChurchSP` | Lose / scripted H path (`EVslaveBigAxwMng`, EvSlaveBigAxe* spines, talk **100322+**) |
| `EvuChurchAfter` | Aftermath; Illusive reveals herself (**100410+**), sets `_Tradeflag[0]` → hideout Trade |

**Win:** both enemies dead → `BattleEnd` → talk → `MoveSceneObj` / `MoveSceneObjSP`
via `EvBigAxeTalkMng.SceneMove` / `SceneMove2`.

**Lose (vanilla):** combat `SlaveBigAxeEro` reaches `JIGOTOFADE` → `fadeevent()`
blacks `UIeffect`. Vanilla does **not** load `EvuChurchSP` here (`Flag` /
`Update` unused; no `LoadSceneWait` on the ero component). Win-only
`MoveSceneObjSP` stays inactive. Result without HellGate handoff: black screen
while the battle scene keeps ticking.

Dialogue text lives in `SubEventDialogue.asset` (not C#).

## HellGate isolation (`SlaveBigAxeIllusiveEventGate`)

While the Illusive event is active (scene name contains `EvuChurch` /
`EVuChurch`, or `Idea_Nowscene` matches, or `EvBigAxeTalkMng` /
`EvBigAxeBattleMng` / `EVslaveBigAxwMng` present), combat
`SlaveBigAxe` / `OtherSlavebigAxe` skip HellGate overlays:

| System | Behavior |
|--------|----------|
| MeatArmor | No post-fade Aradia_armor patrol |
| H dialogue (`SlaveBigAxePassLogic`) | Skipped |
| DeadArmor clips + armored throw | Skipped |
| EnemyFatality | Profile match fails |
| FIN HellGate black-bg | Early-return in `HSceneBlackBackgroundTriggerPatch` |
| EnemyFactions | Registered **Neutral**; Distance/vision/sustain/provocation/marker skipped |

World / spawn `SlaveBigAxe` outside this event keep normal HellGate behavior
(Church faction, MeatArmor, etc.).

Gate helpers:

- `ShouldSkipHellGateLogic()` — any Illusive event context
- `IsChurchBattleLoseHandoffScene()` — battle only (`EvuChurch` exact /
  `Idea_Nowscene`, or Talk+Battle managers); **not** when `EVslaveBigAxwMng`
  is already present (SP)

## Lose handoff (`SlaveBigAxeIllusiveLoseHandoffPatch`)

HellGate **workaround for the vanilla gap**, not a regression we introduced.

1. On battle scene: Spine `JIGOTOFADE` → reset shared `count` so vanilla
   `fadeevent` can run (`count == 1` branch is fragile).
2. After `fadeevent` (or forced call if still needed): wait ~2.5s realtime.
3. Clear HellGate FIN black-bg if any; prepare player (`eroflag` off, no
   `_SOUSA`).
4. Activate `EvBigAxeTalkMng.MoveSceneObjSP` → `SceneMove.SceneMOVE()` to
   **`EvuChurchSP`** (same object the win dialogue uses).
5. Fallback: `PlayerStatus.LoadSceneAndWait("Common", "EvuChurchSP")`.

Log tag: `[Illusive Lose]`.

### Out of scope / do not “fix” without proof

First load of `EvuChurchSP` may show static spines until vanilla
`Changenum` calls `AnimationSet` (and cold Spine warm-up). Second run often
looks perfect. Do **not** force AnimationSet/`timeScale` on SP without a
logged root cause — easy to break win path and intentional staging.

## Code map

| File | Role |
|------|------|
| `SlaveBigAxeIllusiveEventGate.cs` | Context detection + skip API |
| `SlaveBigAxeIllusiveLoseHandoffPatch.cs` | `JIGOTOFADE` / `fadeevent` → `EvuChurchSP` |
| `EnemyFactionRuntime.RegisterEnemy` | Neutral + AI skip hooks |
| `HSceneBlackBackgroundTriggerPatch` | No FIN black-bg for event SlaveBigAxe* |
| `Plugin` sceneLoaded | `Invalidate()` + `ResetHandoffLatch()` |

## Testing checklist

- [ ] **Win** on `EvuChurch` battle → post-battle talk → scene exit as vanilla.
- [ ] **Lose** → multi-stage H → fade → within a few seconds load `EvuChurchSP`
      (log: `[Illusive Lose] Scheduled` / `SceneMOVE`).
- [ ] During Illusive event: no Church faction marker / MeatArmor / HellGate
      Fatality on the scripted pair; world SlaveBigAxe elsewhere still Church.
- [ ] After SP → After: Illusive dialogue / `_Tradeflag[0]` Trade unlock still works.
- [ ] Optional: second consecutive lose→SP run (cold vs warm) for SP spine staging.
