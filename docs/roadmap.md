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
| 9b | Generals-style mechanics | ✅ | Power, upgrades, veterancy, repair/sell, splash, facing armor, capture, area fields, abilities, transports |
| 10 | Multiplayer (Netcode) | 🟡 | 2+ clients play one match in sync |
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
- ✅ Repair, Capture, Enter and Unload orders; Smart right-click picks them from the target (Phase 9b)

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
- ✅ Data-driven prerequisites (`Prerequisite` + `CompletedBuildings`, also used by the HUD)
- ✅ Upgrades/research, building sell/repair, power (Phase 9b)

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
- ✅ Splash damage, kill credit and veterancy (Phase 9b)
- ⬜ Micro AI (kiting, focus fire), AI base building

## Phase 8: Fog of war & vision ✅

- ✅ Per-team visible/explored bit grid (`FogOfWar`), restamped 10 times per second
- ✅ Hostile entities outside vision are hidden; fog overlay shader with soft edges; minimap respects fog
- ✅ Fog can be disabled per match
- ✅ Stealth (revealed by firing, optionally only when still) and per-team detection, also with fog off
- ⬜ "Last seen" building ghosts, line-of-sight occlusion

## Phase 9: Factions & players ✅

- ✅ **Decision:** `Faction` introduced early on every ownable entity (0 = neutral)
- ✅ Player entities from `MatchAuthoring`: team, colour, local/AI/remote control, starting resources
- ✅ `FactionRelations` (team-based hostility) used by targeting, commands and fog
- ✅ Population used/cap; victory/defeat from `VictoryCritical` entities

## Phase 9b: Generals-style mechanics ✅

Generic mechanisms a modern-warfare RTS needs, all data-driven and server-side state only (ready for Phase 10).

- ✅ **Decision:** one damage queue (`DamageEvent` + `DamageSystem`) for weapons, projectiles, abilities and fields,
  so armor, splash, facing and kill credit apply everywhere
- ✅ **Decision:** one stat-modifier buffer (`StatModifier`, `(base + add) × (1 + Σpercent)`) shared by upgrades,
  veterancy and area fields; sources are removable ids
- ✅ Splash with falloff and optional friendly fire; directional armor (front/side/rear); `LastAttacker` kill credit
- ✅ Veterancy ranks with stat bonuses, paid by `experienceValue`
- ✅ Upgrades researched through the production queue, applied to current and future units, swapped on capture
- ✅ Power per player; consumers go `Unpowered` (weapons hold fire, production slows by `MatchRules`)
- ✅ Sell (share refund, full for sites) and builder repair
- ✅ Capturable buildings and capturer units
- ✅ Area fields: presence per field id, stat bonuses, heal and damage over time, no stacking
- ✅ Cooldown abilities on units and buildings, player-level support powers, activation events for game code
- ✅ Garrisons and transports: board, carry, fire out, unload, eject or die with the container
- ✅ HUD: power readout, research state, ability and power buttons with cooldowns, unload and sell
- ⬜ Upgrades as prerequisites, ability targeting cursor and range preview, selection panel cargo slots

## Phase 10: Multiplayer (Netcode for Entities) 🟡

- ✅ **Decision:** server-authoritative Netcode for Entities over deterministic lockstep. Burst doesn't promise
  cross-platform float determinism, Netcode is already in the stack, and the simulation is already headless-ready.
- ✅ Groundwork: input reaches the simulation only through `PlayerCommand`, which maps to an RPC
- ✅ Simulation systems default to server/local worlds; selection, input and fog view opt into clients
  ([`networking.md`](networking.md))
- ✅ Ghosts for units, buildings, nodes, projectiles and the match (validator fix); players become ghosts at runtime
- ✅ Commands as RPCs carrying the selected units as ghost ids; server keeps only owned units
- ✅ Connection flow: host, dedicated server, client (menu and command line); slot binding, observers, rejoin
- ✅ Per-client fog through ghost relevancy (no map hack)
- ⬜ Measured in a real match: 500-unit bandwidth test, importance and send rate tuning
- ✅ Relay transport for player-hosted matches (game supplies the allocation), lobby connection status
- ⬜ "Last seen" enemy buildings, command feedback before the server confirms
- ✅ **Decision:** replays record observed state (keyframes + deltas), not commands. The simulation isn't
  deterministic and entity indices differ between worlds, so re-simulating a command log would desync.
- ✅ Replays: recording in the authoritative world, GZip files, local playback with speed control and keyframe seek
- ⬜ Replays: projectiles, resource nodes, passengers aboard, player stats and HUD controls

## Phase 11: Hardening, tooling & sample game 🟡

- ✅ EditMode tests per module through `TestWorld` (simulation) plus presentation tests
- ✅ Sample game in `Assets/Demo`: a two-base Generals-style skirmish against the AI, built only from engine
  components
- ✅ Editor tooling: **Create RTS Scene** wizard, GameObject templates, inspector warnings, gizmos
- ✅ Editor DX: project validator (inspector, window, Play-mode check, test), draggable Scene handles, role prefab
  templates, Match inspector, catalog and tech tree, Play-mode debug overlay and cheats ([`editor-ux.md`](editor-ux.md))
- ✅ Docs: [`getting-started.md`](getting-started.md), [`modules.md`](modules.md)
- ⬜ CI (`unity test` in a pipeline), PlayMode integration tests, profiler markers and per-system budgets, a
  stress scene

## Cross-cutting rules

- **Determinism:** no `UnityEngine.Random` or managed state in simulation; ties broken by entity index.
- **Burst/job safety:** unmanaged components, ECB for structural changes, lookups for cross-entity access,
  `SystemAPI.Time`.
- **Data-driven:** authoring components and ScriptableObject assets over hard-coded values.
- **Performance:** check the 500-unit scenario when touching movement, combat or the spatial index.
