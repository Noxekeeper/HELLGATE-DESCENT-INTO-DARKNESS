# Economy and Rewards

Gold currency with drops, loss mechanics, HUD, and persistence, plus a
generic weighted drop system for items.

Code: `Systems/Economy/`, `Systems/Rewards/` · Data: `HellGateJson/Economic/`, `HellGateJson/DropSystem/` · Save: `Economic/PlayerGold_Slot0N.json`

## Gating and configuration

The economy is gated and tuned by JSON, not by cfg:
`EconomicConfig.Initialize` reads `Economic/Economy.json` during plugin
`Awake`. When `Enable` is false, no gold patches are registered.

## Core components

- `GoldWallet` — the balance. Persists **only** through the game's save/load
  hooks (`GoldWalletSaveHookPatch`, `GoldWalletLoadHookPatch`); gameplay code
  never flushes to disk directly.
- `GoldStaticMng` — static access point for the active wallet.
- `GoldDropTable` + `GoldDropAwarder` + `EnemyDeathGoldDropPatch` — enemy
  death rewards from `Economic/GoldDropTable.json`.
- `GoldPickup` — world pickup objects (also spawnable via `gold=` spawn
  lines).
- `DropMultiplierBus`, `EconomicFactionUtil` — modifier plumbing.

## Loss mechanics

- Combat loss: chance to scatter gold on taking damage
  (`PlayerCombatGoldLossLegacyPatch`,
  `PlayerCombatGoldLossImprovedPatch`); knockdown loss is processed by
  `CombatGoldLossRuntime` from `PlayerConUpdateDispatcher`.
- Death drop: a configured percentage drops on death
  (`PlayerDeathGoldDropPatch`) into a Souls-style **lost pile**
  (`GoldLostPileSceneLoader` restores it in the scene where the player died).
  `PlayerRespawnGoldArmPatch` arms the post-respawn restoration path.

## Presentation

- `GoldHud` (anchored via `Economy.json → Hud.*`), bootstrapped by
  `GoldHudBootstrapPatch` and `GoldHudBadstatusBootstrapPatch`, and gated by
  `HudVisibilityGate`.
- `GoldPopupSystem` — floating gain/loss popups.
- `GoldAudioPlayer` + `GoldAssetLoader` — coin art and sounds from the
  external asset tree (`Economic/` art).

## Bridges

`GoldHSceneEarningsBridge` awards gold from H-scene events; it is driven from
`PlayerConUpdateDispatcher`, not from its own update patch.

## Rewards / DropSystem

`Systems/Rewards/DropSystem` is a standalone weighted item-drop engine fed by
`HellGateJson/DropSystem/*.json` (used by biscord, among others).
`VanillaEnemyDropWiring` binds drop tables to vanilla enemy types. It is
independent of the gold economy and works with the economy disabled.
