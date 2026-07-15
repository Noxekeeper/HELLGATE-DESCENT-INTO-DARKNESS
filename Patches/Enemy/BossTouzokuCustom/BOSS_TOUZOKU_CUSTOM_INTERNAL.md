# BossTouzokuCustom — внутренняя документация

> **Актуальная документация:** [`docs/BossTouzokuCustom/README.md`](../../../docs/BossTouzokuCustom/README.md)  
> Этот файл — устаревший черновик (v9, H не стабильна). Не использовать как source of truth.

**Статус (устарело):** work-in-progress, H-сцена на поле **не стабильна** (визуальные артефакты / ранний выход).  
**Текущая версия патча (устарело):** `field-mob-v9plus-r21-grab-proximity`  
**Плагин:** `NoR_HellGate.dll` (BepInEx)  
**Дата документа:** 2026-05-30  
**Аудитория:** только внутренняя разработка HellGate

---

## 1. Цель

`BossTouzokuCustom` — **полевой вариант** ванильного `BossTouzoku` (глава «盗賊頭» / Boss Touzoku):

| Нужно | Не нужно |
|-------|----------|
| Обычный бой на карте (движение, атаки, урон, дроп золота) | Арена-босс: intro-катсцены, `BossStartFlag`, камера-стены |
| H-сцена через `EroBOSSTouzoku` + `erodata` | Сюжетные флаги победы, `treasureNumSet`, `OnDestroy` arena flow |
| Спавн через HellGate Spawn System | Super-elite / NoREroMod boss multipliers |

Тот же prefab / класс `BossTouzoku`, отличие — **имя объекта** + маркер + Harmony-патчи.

---

## 2. Конфигурация спавна

### 2.1 Точка спавна

```
BepInEx/plugins/HellGateJson/HellGateSpawnPoint/HellGateSpawn_FirstMap.txt
RANDOM,0.9,-135.93,-134.55,BossTouzokuCustom,1
```

### 2.2 Реестр prefab

```
BepInEx/plugins/HellGateJson/HellGateSpawnPoint/ENEMY_PREFAB_DISK_CACHE.txt
BossTouzokuCustom|BOSS_Touzoku|BossTouzoku|BossTouzoku
```

- **Disk key:** `BossTouzokuCustom`
- **Сцена-источник:** `BOSS_Touzoku`
- **Prefab / компонент:** `BossTouzoku`

### 2.3 Экономика

```
BepInEx/plugins/HellGateJson/Economic/GoldDropTable.json
{ "EnemyType": "BossTouzokuCustom", "Chance": 1.00, "MinAmount": 50, "MaxAmount": 90 }
```

### 2.4 Параметры field-mob

| Параметр | Значение | Файл |
|----------|----------|------|
| `ObjectNameKey` | `BossTouzokuCustom` | `BossTouzokuCustomStats.cs` |
| `FieldMobMaxHp` | 1200 (ванilla arena = 2000) | `BossTouzokuCustomStats.cs` |
| `VanillaMaxTough` | 3 | `BossTouzokuCustomRuntime.cs` |

---

## 3. Идентификация экземпляра

```csharp
// BossTouzokuCustomStats.IsCustom(boss)
1. Есть компонент HellGateBossTouzokuCustomMarker
2. ИЛИ gameObject.name содержит "BossTouzokuCustom"
```

Маркер вешается **до** `SetActive(true)` в `PrepareSpawnedInstance`.

---

## 4. Жизненный цикл (от спавна до уничтожения)

```
SpawnConfigExecutor.SpawnSingle
  └─ Instantiate(prefab BossTouzoku)
  └─ BossTouzokuCustomRuntime.PrepareSpawnedInstance(spawned)
       ├─ SetActive(false) кратковременно
       ├─ Add HellGateBossTouzokuCustomMarker
       ├─ name = "BossTouzokuCustom"
       ├─ DisableStoryIntroComponents (флаги, dialog, walls)
       ├─ EnsureDeathSafeReferences (stub movecameraWall)
       ├─ Add HellGateBossTouzokuCustomActivator
       ├─ Add HellGateBossTouzokuCustomVisibilityDriver
       └─ Canvas.SetActive(false)
  └─ spawned.SetActive(true)

BossTouzoku.Start (vanilla + patches)
  └─ BossTouzokuCustomStartPrefixPatch — CancelIntroInvokes, HideExclamation
  └─ vanilla Start (может бросить — finalizer ловит)
  └─ BossTouzokuCustomStartPatch — EnsureCombatReferences, ApplyFieldMobCombat
  └─ BossTouzokuCustomStartHpScalePatch — ApplyFieldMobHpScale(fullHeal)

HellGateBossTouzokuCustomActivator (coroutine +1 frame)
  └─ ApplyFieldMobCombat
  └─ BeginEroScriptWarmUp

Каждый кадр: BossTouzoku.Update (vanilla, НЕ заменён полностью)
  └─ UpdatePrefixPatch — EnsureFieldMobActive, EnsureBossUiRefs
  └─ vanilla Update (StateMachine, fun_animekind, setanimation, eroanime*)
  └─ UpdatePostfixPatch / Finalizer — RunFieldMobUpkeep

* eroanime для Custom перехвачен → RunSafeEroAnime

HellGateBossTouzokuCustomVisibilityDriver.LateUpdate
  └─ ForceSpineMeshRefresh (пропускается во время H)

OnDestroy / death
  └─ CustomDeathEventPatch — Destroy GO при death event + hp<=0
  └─ BossTouzokuCustomOnDestroyPatch — блок arena OnDestroy side effects
```

---

## 5. Bootstrap боя (`RunBattleStartBootstrap`)

Урезанная версия `BossTouzoku.BattleStart()`:

1. `CancelIntroInvokes` — отмена `next`, `BattleStart` invoke
2. `KeepPlayerControl` — `_SOUSA = true`
3. `BOSSflag = true`, `DameCol.enabled = true`, `Rigidbody2D.simulated = true`
4. Скрыть boss UI / exclamation / Canvas HP
5. `InvokeSousa`, `TransitionToIdle`, `ForceIdleAnimation`
6. `EnsureEroReferences` → `ApplyEroStartSet`
7. `TryActivateWeapon`, `EnsureHostileToPlayer`

`BlockBattleStartPatch` подменяет vanilla `BattleStart()` на этот bootstrap.

---

## 6. Бой и урон

### 6.1 Почему custom damage path

Vanilla `BossTouzoku.Update` на field-spawn часто **падает с NRE** (NoREroMod SuperBoss hooks, null refs intro-полей, `JPname` и т.д.). Finalizer ловит исключение, но `fun_enedamage` / цепочка урона может не отработать.

**Решение:** prefix на `fun_enedamage` / `getdame_fun` / `getMGdame_fun` → `TryApplyCustomWeaponHit` / `TryApplyCustomMagicHit`.

### 6.2 Видимость

Проблема: mesh boss'а иногда **невидим** на поле (sorting layer / пустой mesh).

- `EnsureVisible` — включает все `MeshRenderer`, сортировка через `XWeaponTrail` (`effect/0`)
- `ForceSpineMeshRefresh` — `SkeletonAnimation.Initialize` + reflection `LateUpdate`
- `HellGateBossTouzokuCustomVisibilityDriver` — LateUpdate refresh каждый кадр **вне H**

### 6.3 NoREroMod совместимость

| Патч | Эффект |
|------|--------|
| `StripSuperEliteTag` | Убирает `<SUPER>` из `JPname` (иначе NRE каждый кадр в `fun_animekind`) |
| `BlockNoREroModSuperBossSpawnPatch` | Не спавнит super boss поверх field instance |
| `BlockNoREroModBossHpMultiPatch` | Не умножает HP arena-логикой |
| `BlockNoREroModSuperBossSpeedPatch` / `SuperEnemySpeed` | Не ломает Update |
| `BlockBossEnemyFovPatch` / `UpdateFOV` | FOV arena не применяется |

---

## 7. H-сцена — ванильная модель

### 7.1 Ключевые классы (декompile)

**`BossTouzoku`** (`REZERVNIE COPY/Decompiled/Assembly-CSharp/BossTouzoku.cs`):

```csharp
// EROstartset — привязка ero-компонентов, erodata выключается
public void EROstartset() {
    this.ero = erodata.GetComponent<EroBOSSTouzoku>();
    this.erospine = erodata.GetComponent<SkeletonAnimation>();
    if (erodata.activeSelf) erodata.SetActive(false);
}

// Переход к подходу за даунed игроком (Update)
if (player.erodown != 0 && player.m_Grounded &&
    state in { IDLE, WALK, FASTWALK })
    state = EROWALK;

// Grab (OnTriggerStay2D) — ВСЕ условия одновременно:
!player.eroflag && !eroflag &&
state == EROWALK &&
player.state == "DOWN" &&
collision.tag == "playerDAMAGEcol"
→ eroflag=true, erodata.SetActive(true), erospine START

// Каждый кадр при eroflag:
eroanime() → myspinerennder.enabled=false; UI off;
if (player.erodown == 0) → выход из H, erodata off, tough-=999, ...
```

**`EroBOSSTouzoku`** (`EroBOSSTouzoku.cs`):

```csharp
void Start() {
    myspine.state.Event += OnEvent;  // spine events → START/ERO/JIGO/...
    oya.EROstartset();               // ⚠ снова выключает erodata!
}
void OnEnable() { Skinset("NORMAL"); mosaic off; }
// OnEvent — вся цепочка H-анимаций через SetAnimation на erospine
```

### 7.2 Критический баг EROstartset

При **первом** `erodata.SetActive(true)`:

1. Unity вызывает `EroBOSSTouzoku.Start()`
2. `Start()` → `oya.EROstartset()` → **сразу `erodata.SetActive(false)`**

На arena это OK (Start уже был на prefab load). На field spawn **первый grab** ломает H, если не патчить.

**Патч `BossTouzokuCustomEroStartSetPatch`** (обязательно в `Plugin.cs`!):

```csharp
ApplyEroStartSet(boss):
  ero = erodata.GetComponent<EroBOSSTouzoku>()
  erospine = erodata.GetComponent<SkeletonAnimation>()
  if (!boss.eroflag && erodata.activeSelf) erodata.SetActive(false)
```

**Warm-up** (`BeginEroScriptWarmUp`): один раз активировать `erodata` до боя → `Start()` отработает → `OnEvent` зарегистрирован → erodata снова off.

### 7.3 Grab path (текущий r21)

Vanilla `OnTriggerStay2D` **ненадёжен** на field spawn: boss подходит (`EROWALK`, dist≈0.5), но триггер `playerDAMAGEcol` **не пересекается** → `eroflag` не ставится.

**Текущая схема:**

```
TryStartFieldGrab (distance-based):
  state == EROWALK
  !eroflag, player.erodown != 0, player.m_Grounded
  |distance| <= 1.25, |distance_y| <= 2.5
  → PreparePlayerForFieldGrab (state = "DOWN")
  → StartFieldHScene

Точки вызова:
  1. BossTouzokuCustomGrabPatch — OnTriggerStay2D prefix (если trigger сработал)
  2. RunFieldMobUpkeep — каждый кадр при EROWALK && !eroflag (proximity fallback)
```

**StartFieldHScene** (зеркало vanilla grab body):

1. `EnsureEroReferences`
2. `player.eroflag = true`, `boss.eroflag = true`
3. `erodata.SetActive(true)`, `erospine SetAnimation(0, "START", false)`
4. `HideCombatVisuals` — off combat mesh + `mySpine.enabled = false`
5. `RunSafeEroAnime`, `EnsureEroPresentation`
6. `camera_GetComponent`, `ero_camera_1`, BGM / 2

### 7.4 RunSafeEroAnime (замена vanilla eroanime)

Prefix `BossTouzokuCustomEroAnimePatch` **полностью заменяет** vanilla `eroanime` для Custom:

**Пока H активна (`eroflag && erodown != 0`):**
- `HideCombatVisuals` + `EnsureEroPresentation` каждый кадр
- `myspinerennder.enabled = false`, UI off

**Выход (`erodown == 0`):**
- `ero_camerareset`, `eroflag = false`, `erodata off`
- **Без** `enmTough -= 999` (vanilla ломает field-mob poise)
- `RestoreCombatAfterEro` в postfix при `_wasEroFlag && !eroflag`

### 7.5 Блокировка combat spine во время H

- `BlockIntroAnimationPatch`: `setanimation` return false при `eroflag`
- `HideCombatVisuals`: все `MeshRenderer` кроме детей `erodata`, `mySpine.enabled = false`

### 7.6 Тройные H-оверлеи (историческая проблема)

**Симптом:** 2–3 замороженных копии + 1 рабочая анимация.

**Причины (накопленные):**
1. Двойной старт H (custom + vanilla OnTriggerStay2D + proximity) — частично исправлено r18
2. Combat body виден параллельно с `erodata` — `HideCombatVisuals` / `RunSafeEroAnime`
3. `EROstartset` выключал erodata на первом кадре — патч + warm-up
4. `EnsureVisible` включал combat mesh во время EROWALK

**Статус:** пользовательские тесты r18–r21 — **проблема не закрыта полностью**.

---

## 8. Карта файлов

```
Patches/Enemy/BossTouzokuCustom/
├── BOSS_TOUZOKU_CUSTOM_INTERNAL.md   ← этот документ
├── BossTouzokuCustomStats.cs         — IsCustom, HP constants
├── BossTouzokuCustomRuntime.cs       — ядро: bootstrap, combat, grab, ero, visibility
│                                       + inline Harmony (Start/Update/damage/reste)
├── BossTouzokuCustomCombatPatches.cs — damage, setanimation, BattleStart, FOV, visibility
├── BossTouzokuCustomIntroPatches.cs  — блок arena intro / death event / NoREroMod boss hooks
├── BossTouzokuCustomGrabPatch.cs     — OnTriggerStay2D → TryStartFieldGrab
├── BossTouzokuCustomEroPatches.cs    — EROstartset + eroanime → RunSafeEroAnime
├── BossTouzokuCustomSafetyPatches.cs — OnDestroy, treasure, wall blocks
├── HellGateBossTouzokuCustomMarker.cs — флаги состояния, OnDisable/OnDestroy log
├── HellGateBossTouzokuCustomActivator.cs — +1 frame bootstrap + warm-up coroutine
└── HellGateBossTouzokuCustomVisibilityDriver.cs — LateUpdate mesh refresh

Systems/Spawn/
├── SpawnConfigExecutor.cs            — PrepareSpawnedInstance для BossTouzokuCustom
├── EnemyPrefabDiskCache.txt          — disk key mapping
└── HellGateBossSpawnRuntime.cs       — generic boss spawn (не Custom-specific)

Core/Plugin.cs                        — регистрация всех Harmony PatchType (см. §9)

Decompiled reference:
└── REZERVNIE COPY/Decompiled/Assembly-CSharp/
    ├── BossTouzoku.cs
    └── EroBOSSTouzoku.cs
```

---

## 9. Регистрация Harmony-патчей (`Plugin.cs`)

**Обязательные для H-сцены (часто забывают):**

```csharp
PatchType(BossTouzokuCustomGrabPatch);
PatchType(BossTouzokuCustomEroStartSetPatch);   // ← был пропущен в r19, патч не работал
PatchType(BossTouzokuCustomEroAnimePatch);
```

**Полный список BossTouzokuCustom** (строки ~844–877):

| Patch class | Target |
|-------------|--------|
| StartPrefix / Start / StartHpScale | `BossTouzoku.Start` |
| UpdatePrefix / Update | `BossTouzoku.Update` |
| Reste | `BossTouzoku.reste` |
| Damage / MagicDamage | `getdame_fun`, `getMGdame_fun` |
| OnDestroy / Treasure / Wall | safety |
| IntroPatches (×9) | flagCall, next, death event, NoREroMod blocks |
| CombatPatches (×9) | fun_enedamage, setanimation, BattleStart, FOV, … |
| GrabPatch | `OnTriggerStay2D` |
| EroStartSetPatch | `EROstartset` |
| EroAnimePatch | `eroanime` |

---

## 10. Маркер состояния (`HellGateBossTouzokuCustomMarker`)

| Поле | Назначение |
|------|------------|
| `CombatApplied` | Bootstrap выполнен один раз |
| `HpScaled` | Field HP 1200 применён |
| `EroRefsReady` | `ApplyEroStartSet` успешен |
| `EroScriptsWarmedUp` | `EroBOSSTouzoku.Start` прогрет до боя |
| `WasInEroScene` | Был в H (для RestoreCombatAfterEro) |
| `EroPresentationReady` | Ero mesh init once per H session |
| `DeathHandled` | Смерть обработана custom path |
| `BodySortLayer/Order` | Кэш sorting для visibility |
| `WeaponHitReactionGuard` | Guard duplicate hit frames |

---

## 11. История версий (`CombatPatchVersion`)

| Версия | Фокус | Результат тестов |
|--------|-------|------------------|
| field-mob-v9plus-r14 | Bootstrap, visibility, vanilla Update + guards | Бой OK, H не работала |
| r15–r16 | Grab path, PreparePlayer DOWN, EnsureEroReferences | Grab в логе, ero=True, H не стартовала визуально |
| r17 | Safe eroanime finalizer, RestoreCombatAfterEro, XwepFlag | Post-H: invisible, 0 damage, tough=-997 |
| r18 | Single grab path, HideCombatVisuals, block setanimation in ero | Одна строка grab в логе; triple overlay остался |
| r19 | EROstartset patch code + warm-up (**patch не в Plugin.cs!**) | Warm-up в логе; EROstartset патч **не применялся** |
| r20 | Vanilla OnTriggerStay2D only + RunSafeEroAnime + EROstartset в Plugin | **Grab не стартует** (нет trigger overlap) |
| **r21** | Distance grab + proximity в RunFieldMobUpkeep + r20 fixes | Grab в логе (`prox=0.26`), ero=True; визуал **всё ещё проблемный** |

---

## 12. Диагностика по логу

**Файл:** `BepInEx/LogOutput.log`  
**Префикс:** `[BossTouzokuCustom]`

### 12.1 Проверка версии DLL

```
Field mob combat enabled (field-mob-v9plus-r21-grab-proximity)
```

Если другая строка — старая DLL или не перезапущена игра.

### 12.2 Успешный bootstrap

```
EroBOSSTouzoku scripts warmed up.
Field mob combat enabled (...)
Vis st=IDLE mesh=True sort=effect/0 ...
```

### 12.3 Успешный grab

```
Field grab started (H-scene, prox=0.xx).
AI hb st=EROWALK ... ero=True ... pSt=DOWN ed=1
```

**Ожидание:** ровно **одна** строка `Field grab started` на один grab.

### 12.4 Grab не происходит

```
AI hb st=EROWALK ... ero=False ... pSt=DOWN ed=1
(нет Field grab started)
```

Причины: `TryStartFieldGrab` не прошёл distance check; boss не в EROWALK; `erodown==0`; instance пересоздан hot-reload.

### 12.5 Ранний выход из H

```
ero=True ... ed=1
→ ero=False ... ed=0   (erodown cleared, struggle / timer)
```

Vanilla и RunSafeEroAnime выходят при `erodown == 0`.

### 12.6 Ошибки Update

```
vanilla Update threw -> NullReferenceException ...
```

Обычно intro/null refs; finalizer продолжает RunFieldMobUpkeep.

### 12.7 Hot-reload шум

```
GameObject disabled while alive (state=... hp=1200).
Destroyed (state=... hp=1200).
[SPAWN HOT RELOAD] ...
```

Spawn hot-reload **уничтожает** instance mid-test. **Не тестировать с RMB hot-reload / Spawn Recorder ON.**

---

## 13. Протокол тестирования

1. **Полный перезапуск игры** после `dotnet build` / `compili.bat`
2. **F11 → Spawn Recorder OFF**
3. **Не использовать** RMB spawn hot-reload во время теста H
4. FirstMap → knockdown → дождаться `EROWALK` → подход boss'а
5. Сверить log (§12)
6. После H: boss виден, атаки наносят урон, `tough` не -997

### Сборка

```bat
cd "REZERVNIE COPY\HELLGATE for Git"
dotnet build NoREroMod_HellGate.csproj -c Release
copy bin\Release\NoR_HellGate.dll → BepInEx\plugins\NoR_HellGate.dll
```

Или `dev\compili.bat` (в конце `pause` — для CI/агента не ждать).

---

## 14. Известные проблемы (open)

| # | Проблема | Заметки |
|---|----------|---------|
| 1 | Triple H overlay / frozen copies | Combat + erodata + повторный START; не закрыто |
| 2 | Invisible boss at H start / after H | mesh/sorting/RestoreCombatAfterEro |
| 3 | Attacks hit visually but 0 damage after H | XwepFlag, tough, weapon trail desync |
| 4 | `vanilla Update threw` NRE on death | finalizer catches; death работает |
| 5 | Spawn hot-reload destroys instance | `[SPAWN HOT RELOAD]` + Destroyed mid-fight |
| 6 | Vanilla OnTriggerStay2D unreliable | r21 использует distance/proximity |
| 7 | r19 EROstartset patch not in Plugin | исправлено r20+, проверять регистрацию |
| 8 | `TouzokuNormalPassPatch START` в логе рядом с boss EROWALK | возможное пересечение grab-систем других врагов |
| 9 | Player escape during H (`ed=0` while `pSt=DOWN`) | QTE/struggle; нормальный vanilla exit |

---

## 15. Направления следующих работ

### P0 — Grab стабильно стартует

- [x] Distance grab + proximity (r21)
- [ ] Проверить collider grab zone на prefab (может потребовать enable child trigger)
- [ ] Лог `Grab diag blocked: ...` расширить при fail

### P1 — Одна H-анимация

- [ ] Убедиться что **только** `StartFieldHScene` ставит START (нет второго пути)
- [ ] На время H: `mySpine.gameObject.SetActive(false)` вместо только `enabled=false`
- [ ] Не вызывать `EnsureVisible` при `IsEroSceneActive` (аудит всех postfix)

### P2 — Post-H combat

- [ ] Аудит `RestoreCombatAfterEro` vs vanilla ero exit (weapon Activate, tough, enmATKnow)
- [ ] Проверить `BlockIntroAnimationPatch` не блокирует нужные anim после H

### P3 — Cleanup

- [ ] Убрать verbose `Vis` / `AI hb` после стабилизации (или config flag)
- [ ] Удалить dead code: `RunFieldMobFrame`, `InvokeVanillaEroAnime`, obsolete paths
- [ ] Suppress hot-reload disable warning для managed spawns

---

## 16. Связанные системы HellGate

| Система | Взаимодействие |
|---------|----------------|
| HellGate Spawn | SpawnConfigExecutor, disk cache, hot-reload |
| EconomicHG | GoldDropTable BossTouzokuCustom |
| CombatAi / Factions | `EnemyFactionsConfig`, boss detection excludes custom name patterns |
| GrabSystemNG | CanEliteGrabPlayer — boss grab отдельный path |
| HSceneBlackBackgroundTrigger | `EroBOSSTouzoku` в списке триггеров фона |
| NoREroMod | SuperBoss patches заблокированы для Custom |

---

## 17. Быстрая шпаргалка для нового разработчика

1. **Это не новый класс** — тот же `BossTouzoku`, патчи по `IsCustom`.
2. **Главный runtime** — `BossTouzokuCustomRuntime.cs` (~2500 строк, включая Harmony inline).
3. **H = erodata + EroBOSSTouzoku**, не combat spine. Combat spine нужно **глушить** на время H.
4. **`EROstartset` на eroflag не должен выключать erodata** — патч обязателен + warm-up.
5. **Grab на поле = distance**, не полагаться на vanilla trigger.
6. **Всегда проверяй `Plugin.cs`** после добавления нового patch class.
7. **Версия в логе** = единственный надёжный индикатор что DLL обновилась.

---

*Документ отражает состояние кодовой базы HellGate на 2026-05-30. Обновлять при смене `CombatPatchVersion` или закрытии пунктов §15.*
