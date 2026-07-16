# HellGate API

Public integration surface for other BepInEx plugins.

Namespace: `NoREroMod.HellGate.Api` · Entry point: `HellGateApi` · Current API: **0.1.0 (experimental)**

## Contract

The API is deliberately smaller than the internal codebase:

- consumers receive immutable snapshots, never live subsystem instances;
- API types do not expose Harmony patches, BepInEx config entries, mutable
  Unity objects, or internal persistence types;
- API events isolate subscribers — one failing plugin is logged and does not
  prevent other subscribers from running;
- `ApiVersion` is independent from the HellGate plugin version;
- API 0.x may change between minor releases. Version 1.0 will begin the stable
  compatibility contract.

## Discovery and readiness

Reference `NoR_HellGate.dll`, then use:

```csharp
using NoREroMod.HellGate.Api;

if (HellGateApi.IsReady)
{
    string pluginVersion = HellGateApi.PluginVersion;
    string apiVersion = HellGateApi.ApiVersion;
}
```

`HellGateApi.Initialize()` runs at the end of `Plugin.Awake`, after configs,
patches, and subsystems initialize. If your plugin can load earlier, declare a
BepInEx dependency on `NoREroMod_HellGate` or subscribe to `ApiReady` before
querying state.

Always check `ApiMajorVersion` before consuming a future stable API.

## Read-only queries

| Method | Snapshot |
|--------|----------|
| `GetRageState()` | enabled, percent (0..100+), active flag, active tier, Tier-3 readiness |
| `GetMindBrokenState()` | enabled, fraction (0..1), bad-end countdown, scripted sequence flag |
| `GetFactionReputation(HellGateFaction)` | faction ID, score (-100..100), relation (`hostile` / `neutral` / `friendly` / `native`) |
| `GetFactionReputation(int)` | same, for data-defined/custom faction IDs |
| `GetGoldState()` | economy enabled, balance, bound one-based slot (0 = unbound) |
| `GetPregnancyState()` | gate, current/pending pregnancy, factions, elapsed/total/progress, trimester |

Snapshots are immutable and represent the instant of the call. Query again
after an event; do not cache a snapshot as live state.

## Events

```csharp
HellGateApi.ApiReady += OnApiReady;
HellGateApi.SceneChanged += OnSceneChanged;       // scene name
HellGateApi.RageChanged += OnRageChanged;         // RageStateSnapshot
HellGateApi.MindBrokenChanged += OnMindChanged;   // MindBrokenStateSnapshot
HellGateApi.GoldChanged += OnGoldChanged;         // GoldStateSnapshot
```

Unsubscribe when your plugin unloads. The API automatically detaches its
internal bridges when HellGate shuts down.

Factions and Pregnancy are query-only in API 0.1 because their current
internals do not publish a reliable change event. An API event will be added
only after the owning subsystem gains one canonical notification path.

## Faction identifiers

`HellGateFaction` exposes stable built-in IDs: Neutral,
EventCoreEncounter, Bandits and loyal variants, Church, Demons, Mafia,
Undead, Monsters, Witch. Custom IDs remain queryable through the `int`
overload.

## Not in API 0.1

No public mutation or registration is exposed yet. In particular:

- changing Rage, MindBroken, gold, reputation, or pregnancy;
- registering spawn keys/custom enemies/trap templates;
- EventCore handler registration;
- faction, dialogue, drop-table, or HUD providers.

These operations affect persistence and lifecycle invariants. They will be
designed as explicit registration contracts after the read-only surface has
been used and stabilized. Consumers must not reflect into HellGate internals
as a substitute.

## Versioning policy

- API version follows semantic versioning independently of the plugin.
- `0.x`: experimental; minor versions may break.
- `1.x`: stable; additions are minor, breaking changes require a new major.
- Deprecated members remain for at least one stable minor release before
  removal.

The plugin version describes the entire mod; the API version describes only
the external integration contract.
