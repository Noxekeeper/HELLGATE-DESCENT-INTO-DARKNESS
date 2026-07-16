# Adding an Enemy

Procedure for making an enemy spawnable and integrating it with handoff and
the custom-pack conventions.

## 1. Spawn registration

1. Add a spawn key → prefab mapping in `Systems/Spawn/EnemyPrefabRegistry`.
   For a custom variant, the key maps to the vanilla prefab that will be
   cloned.
2. If spawn behavior is non-standard (activation, cloning, component setup),
   add a branch in `Systems/Spawn/SpawnConfigExecutor`.
3. Reference the key from a spawn pack line in
   `HellGateJson/HellGateSpawnPoint/HellGateSpawn_<Zone>.txt` and verify the
   spawn in game. Use `Patches/Spawn/SpawnPointAnalyzer` (F11 coordinates,
   RMB pack hot-reload) for placement.

## 2. Pass/handoff logic

1. Copy `Patches/Enemy/_Template/EnemyNamePassLogic.cs` into a new folder
   under `Patches/Enemy/<EnemyName>/` and adapt it. The template is not
   compiled; your copy must be added to the `.csproj`.
2. Reuse `Patches/Enemy/Base/BaseEnemyPassPatch` plumbing.
3. Register the new patch type in `SetUpPatches()`.
4. Test the full handoff chain: entry, forced-mid entry after the first
   handoff, escape, and scene-change reset (see
   `../modules/GRAB_AND_HANDOFF.md`).

## 3. Custom variant conventions

For a visual/behavioral variant of a vanilla enemy (see the pack table in
`../modules/CUSTOM_ENEMIES.md`):

- Keep the pack self-contained in `Patches/Enemy/<PackName>/`.
- Identity: mark clones with a marker/identity component and detect by
  object naming — do not invent replacement ERO component classes
  (the removed `BigoniBrotherERO` path is the cautionary precedent).
- Visual replacement: load Spine/texture assets from the external asset tree
  through dedicated `*TextureLoader` / `*SkeletonLoader` types; follow
  `RickEnemyModShared` for the fatality/icon pattern.
- Stats overrides get their own `*Stats` type and cfg section.
- If the variant must not participate in faction boss logic, verify against
  `FactionBossDetection`.

## 4. Verification

- Spawn from a pack line in at least two zones (normal load + door
  transition).
- H-scene entry, escape, and handoff to and from a neighboring enemy.
- Faction assignment (default and `|faction=` override).
- With the relevant cfg gate disabled, the enemy must be absent and no
  patches may misfire.
