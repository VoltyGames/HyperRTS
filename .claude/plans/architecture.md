# Architecture plan

Findings from the October 2026 architecture review and the refactors they call for. Working notes, not
documentation: update or delete items as they land.

## Keep

- One intent channel (`PlayerCommand`) for input, AI and network, with server-side ownership checks.
- `OrderWriter`, the damage queue and the stat-modifier pipeline: one path per concern.
- `*Setup` helpers + `IEntityWriter`: bakers and tests build identical entities.
- `ModuleLayoutTests` with an empty back-edge allowlist.
- Discovered validation rules and debug layers.

## Done

- Promoted queued orders reset movement and targets (`OrderHalt`).
- Fog holds 15 teams; `MatchSetupSystem` throws past that.
- Editor UI on UI Toolkit; inspectors validate every selected object.
- `RTSSceneBuilder.Build(RTSSceneSpec)` behind the scene wizard.
- Captured entities drop old upgrades by source kind, even when the old owner is gone.
- Phase A (E1, E4–E9):
  - `TypeSet` (256 types) replaces 64-bit command masks; `OrderTraits.Register` for custom orders;
    `SmartOrderRule` buffer for game right-click behaviour.
  - `TargetReach` shared by build, repair, capture, board and gather.
  - `Offline` + `OfflineReason` (construction, power, game reasons such as EMP) replaces six filters.
  - `GameEvents` (`UnitProduced`, `BuildingCompleted`, `OwnerChanged`) with `ReadySoundSystem`,
    `BuildStatsSystem` and `OwnerChangeSystem`; `Ownership.Transfer` is the one owner change.
  - `PurchaseRules` + `Buyer` shared by orders, placement, AI and HUD.
  - Research lives in Production; prerequisites accept research; back-edge allowlist empty.
- Phase B (E17–E19, E21, E22) and E14, E15:
  - `HyperRTS.Client` assembly holds `Interaction/`, `DefaultWorld`, `LiveQuery`.
  - Command card built from `ICommandSource`s; entries take grid slots and letter or digit hotkeys.
  - Observers: `MatchView` ready without a local player; player-only panels hide; outcome `Ended`.
  - Hosting opens the port before tearing anything down; no fallback world; `StatusChanged` removed (poll
    `Status`).
  - `IPrefabReference` + `PrefabReferences` for game buffers.
  - `PlayerRegistry` singleton; one `Faction.Max`.
- Phase C: player `StatModifier`s with `Stat.Income` (E10); fog-honest AI option (E12).
- E27: instant build completes through `ConstructionSystem` (full-progress sites finish like built ones); any cheat
  drops the running replay recording.

## Declined

| # | Why |
| --- | --- |
| E2 | A per-frame "command types present" singleton; scanning a few command buffers is cheap |
| E3 | The contract already gives steering to the active order, and to combat only while `AttackTarget` is enabled |
| E11 | Only the ability id and cooldown replicate already; a split saves chunk memory only, and a cost waits for a game design that needs one |
| E12 (rewrite) | The AI is already split by concern across partial files; an AI group with a blackboard adds structure without a need |
| E13 | No profile shows the single-threaded damage and sound writers matter; revisit with large-battle profiling |
| E20 | Each interaction write is a different action in its natural owner |
| E26 (deferred) | No game template, catalog column or cheat needs an extension point yet; add one per menu when the first does |
| E27 (rest) | Turning every cheat into a recorded command is a lot of machinery for an editor-only tool |
| Session phases | `NetworkStatus` covers what lobby and connection UI need |

## Open

| # | Finding | Fix |
| --- | --- | --- |
| E16 | `TestWorld` loads only engine systems | `TestWorld(params Assembly[] extra)`; test kits as an assembly |
| E25 | CI validation never opens map scenes | `ProjectValidator.Scenes(paths)` opening each scene and SubScene |

Phase D (CI with scene validation, `TestWorld` for game assemblies, a determinism test) covers these.
