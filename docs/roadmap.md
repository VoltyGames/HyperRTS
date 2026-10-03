# HyperRTS: development roadmap

The phased plan for **HyperRTS**, a reusable RTS engine on Unity DOTS. Games are built on top of it, so every
phase favours generic, data-driven, Burst-safe pieces over game-specific code.

> Keep this current. When a deliverable lands, tick it and note the commit. When scope shifts, edit the phase
> instead of letting it drift.

- **Engine:** Unity 6000.6.4f1 (6.6), URP 17.6, Input System
- **DOTS stack:** Entities 6.6, Entities Graphics, Unity Physics, Netcode for Entities, Collections, Burst,
  Mathematics
- **Status legend:** ✅ done · 🟡 partly done · ⬜ not started

## Milestone overview

| Phase | Theme | Status | Exit criteria |
| ----: | --- | :---: | --- |
| 0 | Foundation | ✅ | Layered assemblies, phase groups, tests, demo scene |
| 1 | Selection & input | ✅ | Click/box/double-click select, control groups |
| 2 | Commands & orders | ✅ | Right-click Smart commands, order queue, stop/hold, attack-move |
| 3 | Pathfinding & steering | ✅ | Grid A* around obstacles, separation, formations, re-path on new buildings |
| 4 | Economy & resources | ✅ | Harvest → deposit → stockpile loop, finite and regrowing nodes |
| 5 | Production & buildings | ✅ | Placement, builder construction, production queues, rally points, prerequisites |
| 6 | Rendering spawned entities | ✅ | Runtime spawns are instantiated baked prefabs and render like placed ones |
| 7 | Combat depth & AI | ✅ | Auto-acquire, stances, projectiles, armor, skirmish AI |
| 8 | Fog of war & vision | ✅ | Per-team visible/explored grid, hidden enemies, fog overlay |
| 9 | Factions & players | ✅ | Players, teams, relations, population, victory/defeat |
| 10 | Multiplayer (Netcode) | ⬜ | 2+ clients play one match in sync |
| 11 | Hardening & tooling | 🟡 | Tests, docs, sample game, scene wizard done; CI and perf budgets remain |

## Phase 0: Foundation ✅

- ✅ Layered assemblies `Core ← Simulation ← {Presentation, Input}` + Editor/Tests ([`architecture.md`](architecture.md))
- ✅ Ordered phase groups Order → Movement → Combat → Production → Lifecycle ([`world-setup.md`](world-setup.md))
- ✅ `TestWorld` fixture running every simulation system in an isolated world
- ✅ Unified authoring: `UnitAuthoring` / `BuildingAuthoring` on a shared `GameEntityAuthoring` base; bakers and
  tests share `*Setup` helpers through `IComponentSink` (replaced the granular authoring components and factories)

## Phase 1: Selection & input ✅

- ✅ `Selectable`/`Selected` (enableable), Unity Physics click-picking, drag-box, Shift/Ctrl modifiers
- ✅ Double-click selects all on-screen entities of the same `EntityInfo.TypeId`
- ✅ Drag-box prefers the local player's units, then its buildings
- ✅ Control groups (Ctrl+N assign, N recall, Shift+N add)
- ✅ Input via the `RTSInputActions` asset (Selection, Commands, Camera maps), see [`selection.md`](selection.md)

## Phase 2: Commands & orders ✅

- ✅ `PlayerCommand` buffer on each player: the single entry point for human and AI intent
- ✅ `ActiveOrder` + `QueuedOrder` with shift-queue and `OrderWriter`
- ✅ Smart right-click: hostile → attack, node → gather, unfinished building → build, ground → move; producers →
  rally point
- ✅ Stop, hold position (stance), attack-move (A + click)
- ⬜ Garrison / enter-building order (game-side via `OrderType.Custom`)

## Phase 3: Pathfinding & steering ✅

- ✅ **Decision:** grid A* (8-way, no corner cutting) + line-of-sight smoothing + separation steering. Flow fields
  deferred: formation slots give every unit its own goal, so a shared field saves little.
- ✅ `NavGrid` from `MapSettings`, stamped from `NavObstacle` boxes (building footprints included), rebuilt when
  obstacles change; units re-path when the grid version changes
- ✅ Per-frame search cap (48, oldest first); direct line of sight skips A*
- ✅ Box formations for group moves
- ✅ Budget check: 500 units across a 200 × 200 map with obstacles, 0.26 ms average / 1.6 ms worst simulation tick
- ⬜ Moving obstacles and terrain height (units move on the XZ plane)

## Phase 4: Economy & resources ✅

- ✅ `ResourceType` assets replace the static `ResourceManager`; per-player `ResourceStock`
- ✅ Finite or regrowing `ResourceNode`s; depleted finite nodes are removed
- ✅ Harvester loop: node → fill → nearest owned drop-off → deposit → repeat, switching to a nearby node when
  one runs dry
- ✅ HUD resource bar

## Phase 5: Production & buildings ✅

- ✅ Placement mode: snapped ghost, valid/invalid preview, shared `PlacementMath` rules, Shift to keep placing
- ✅ Construction progresses only while builders work; several builders stack
- ✅ Production queue with cost on enqueue, cancel with refund, population check, rally points
- ✅ Data-driven prerequisites (`Prerequisite` + `ProductionRules`, also used by the HUD)
- ⬜ Upgrades/research, building sell/repair, power (see the Generals mapping in
  [`getting-started.md`](getting-started.md))

## Phase 6: Rendering spawned entities ✅

- ✅ **Decision:** entity prefabs baked from authoring references (producer options, build options, projectiles,
  death spawns). Spawning instantiates the prefab, so mesh and materials come along. The mesh-less factories are
  gone.
- ✅ Team colours through `URPMaterialPropertyBaseColor`; selection rings and health bars drawn instanced
- ✅ Culling and instancing come from Entities Graphics (BatchRendererGroup); `LODGroup`s bake as usual

## Phase 7: Combat depth & AI ✅

- ✅ Target acquisition via the `SpatialIndex` (nearest hostile, staggered scans)
- ✅ Instant hits and homing projectiles; `DamageType` assets with `Armor` multipliers
- ✅ Attack-move; stances Aggressive / Defensive / Hold / Passive; leashing
- ✅ `Dead` marker frame + `SpawnOnDeath` hook before destruction
- ✅ Skirmish AI: harvesting, production and attack waves, only through `PlayerCommand`s
- ⬜ Splash damage, kill credit/veterancy, micro AI (kiting, focus fire), AI base building

## Phase 8: Fog of war & vision ✅

- ✅ Per-team visible/explored bit grid (`FogOfWar`), restamped 10 times per second
- ✅ Hostile entities outside vision are hidden; fog overlay shader with soft edges; minimap respects fog
- ✅ Fog can be disabled per match
- ⬜ Stealth/detection, "last seen" building ghosts, line-of-sight occlusion

## Phase 9: Factions & players ✅

- ✅ **Decision:** `Faction` introduced early on every ownable entity (0 = neutral)
- ✅ Player entities from `MatchAuthoring`: team, colour, local/AI/remote control, starting resources
- ✅ `FactionRelations` (team-based hostility) used by targeting, commands and fog
- ✅ Population used/cap; victory/defeat from `VictoryCritical` entities

## Phase 10: Multiplayer (Netcode for Entities) ⬜

- ✅ **Decision:** server-authoritative Netcode for Entities over deterministic lockstep. Burst doesn't promise
  cross-platform float determinism, Netcode is already in the stack, and the simulation is already headless-ready.
- ✅ Groundwork: input reaches the simulation only through `PlayerCommand`, which maps to an RPC
- ⬜ Simulation systems filtered to server/local worlds; ghosts for units, buildings, projectiles and players
- ⬜ Commands as RPCs carrying the selected unit list (selection is client-side), server-side validation
- ⬜ Connection flow, client → player binding, per-client fog and HUD on the client world
- ⬜ Latency handling, rejoin

## Phase 11: Hardening, tooling & sample game 🟡

- ✅ EditMode tests per module through `TestWorld` (simulation) plus presentation tests
- ✅ Sample game in `Assets/Demo`: a two-base Generals-style skirmish against the AI, built only from engine
  components
- ✅ Editor tooling: **Create RTS Scene** wizard, GameObject templates, inspector warnings, gizmos
- ✅ Docs: [`getting-started.md`](getting-started.md), [`modules.md`](modules.md)
- ⬜ CI (`unity test` in a pipeline), PlayMode integration tests, profiler markers and per-system budgets, a
  stress scene

## Cross-cutting rules

- **Determinism:** no `UnityEngine.Random` or managed state in simulation; ties broken by entity index.
- **Burst/job safety:** unmanaged components, ECB for structural changes, lookups for cross-entity access,
  `SystemAPI.Time`.
- **Data-driven:** authoring components and ScriptableObject assets over hard-coded values.
- **Performance:** check the 500-unit scenario when touching movement, combat or the spatial index.
