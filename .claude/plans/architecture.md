# Architecture plan

Findings from the October 2026 architecture review and the refactors they call for. Working notes, not
documentation: update or delete items as they land.

## Keep

- One intent channel (`PlayerCommand`) for input, AI and network, with server-side ownership checks.
- `OrderWriter`, the damage queue and the stat-modifier pipeline: one path per concern.
- `*Setup` helpers + `IEntityWriter`: bakers and tests build identical entities.
- `ModuleLayoutTests` with a shrinking back-edge allowlist.
- Discovered validation rules and debug layers.

## Done

- Promoted queued orders reset movement and targets (`OrderHalt`).
- Fog holds 15 teams; `MatchSetupSystem` throws past that.
- Editor UI on UI Toolkit; inspectors validate every selected object.
- `RTSSceneBuilder.Build(RTSSceneSpec)` behind the scene wizard.
- Captured entities drop old upgrades by source kind, even when the old owner is gone.
- Phase A (E1, E4–E9):
  - TypeSet (256 types) replaces 64-bit command masks; OrderTraits.Register for custom orders;
    SmartOrderRule buffer for game right-click behaviour.
  - TargetReach shared by build, repair, capture, board and gather.
  - Offline + OfflineReason (construction, power, game reasons such as EMP) replaces six filters.
  - GameEvents (UnitProduced, BuildingCompleted, OwnerChanged) with ReadySoundSystem, BuildStatsSystem
    and OwnerChangeSystem; Ownership.Transfer is the one owner change.
  - PurchaseRules + Buyer shared by orders, placement, AI and HUD.
  - Research lives in Production; prerequisites accept research; back-edge allowlist empty.
- Phase B (E17–E19, E21, E22) and E14, E15:
  - HyperRTS.Client assembly holds Interaction/, DefaultWorld, LiveQuery.
  - Command card built from ICommandSources; entries take grid slots and letter/digit hotkeys.
  - Observers: MatchView ready without a local player; player-only panels hide; outcome Ended.
  - Hosting opens the port before tearing anything down; no fallback world; StatusChanged removed (poll Status).
  - IPrefabReference + PrefabReferences for game buffers.
  - PlayerRegistry singleton; one Faction.Max.
- Declined: E20 (each interaction write is a different action in its natural owner), explicit session phases
  (NetworkStatus covers what the UI needs), E2 (a per-frame present-command set; scanning a few buffers is cheap) and E3 (the contract already
  gives steering to the active order, and to combat only while AttackTarget is enabled).

## Simulation

| # | Finding | Fix |
| --- | --- | --- |
| E10 | No player-level modifiers: `IncomeMultiplier`, low-power rate and power are special cases | `StatModifier` buffer on players; low power as a modifier source |
| E11 | Ability definition and cooldown in one 120-byte replicated element; closed targeting enum; no cost | Definition in a blob or prefab, runtime `{Id, Cooldown}`; optional `ResourceCost`; effect prefabs |
| E12 | AI is one system with a fixed pipeline, omniscient on every difficulty | `AISystemGroup`: scheduler, blackboard snapshot, one system per behaviour; fog-honest option |
| E13 | Damage and sound writers force single-threaded scheduling | `NativeStream` writers, sorted in the consumer |
| E16 | `TestWorld` loads only engine systems | `TestWorld(params Assembly[] extra)`; test kits as an assembly |

## Client, network, editor

| # | Finding | Fix |
| --- | --- | --- |
| E25 | CI validation never opens map scenes | `ProjectValidator.Scenes(paths)` opening each scene and SubScene |
| E26 | Templates, catalog columns and cheats are hard-coded | Discovery like rules: `EntityTemplate`, `[CatalogColumn]`, `CheatSection` |
| E27 | Cheats bypass systems; replays after cheats desync | Complete through the systems; flag cheated recordings |

## Phases

| Phase | Work | Size |
| --- | --- | --- |
| C | Player modifiers, abilities split, AI behaviour systems, parallel writers (E10–E13) | Large |
| D | CI with scene validation, `TestWorld` for game assemblies, determinism test, editor discovery (E16, E25–E27) | Medium |
