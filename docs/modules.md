# Module reference

What each engine module owns, the components you author or read, and the systems that run it. Simulation modules
live in `Packages/com.hyperrts.engine/Simulation/<Module>/` (namespace `HyperRTS.Simulation.<Module>`); client-only code is in
`Input/` and `Presentation/`. For the frame order, see [`world-setup.md`](world-setup.md).

## How the modules talk

```text
Input / AI ──PlayerCommand──▶ command consumers ──OrderWriter──▶ ActiveOrder / QueuedOrder
                              (OrderSystemGroup)                     │
                                                                     ▼
              behaviours (move, attack, gather, build) ──MoveDestination──▶ pathfinding + movement
                                                       ──AttackTarget────▶ engagement + weapons
```

Five rules hold everything together:

1. **Commands in.** Player intent is a `PlayerCommand` on the player entity. `Unit = Entity.Null` means "the
   player's selected entities". Commands live for one order phase.
2. **Orders.** `ActiveOrder` (enableable; disabled = idle) is the order being executed, `QueuedOrder` the
   shift-queue. Issue and stop orders only through `OrderWriter`. The system that owns an order type disables
   `ActiveOrder` when it is done, and `OrderDispatchSystem` starts the next queued one.
3. **Locomotion.** To move a unit, set `MoveDestination` and enable it. Navigation disables it on arrival.
4. **Combat owns movement while `AttackTarget` is enabled.**
5. **Ownership.** `Faction` (0 = neutral) on everything ownable; `FactionRelations.IsHostile/IsAllied` decides
   who fights whom (players on different teams are hostile).

## Common

Shared identity for units and buildings.

| Type | Role |
| --- | --- |
| `GameEntityAuthoring` | Base of `UnitAuthoring`/`BuildingAuthoring`: name, icon, owner, health, vision, cost, build time, prerequisites, death spawn, counts-for-victory |
| `EntityInfo` | `TypeId` (hash of the display name: instances of one prefab share it), `Name`, `Icon` |
| `Producible` | Build time and population cost, read from prefabs |
| `Prerequisite` | Buffer of building `TypeId`s required before producing |
| `GameEntitySetup` + `IComponentSink` | The component set, written once and used by bakers, tests and tools (`BakerSink`, `EntityManagerSink`) |

## Match (players, teams, victory)

| Type | Role |
| --- | --- |
| `MatchAuthoring` | One per map: map size, nav/fog cell sizes, fog toggle, player slots, AI tuning |
| `MapSettings` | Singleton: playable bounds and grid resolutions |
| `Player` | Per player entity: faction number, team, name, colour. Also holds `ResourceStock`, `Population`, `PlayerCommand`, `Defeated` |
| `LocalPlayer` / `AIPlayer` | Who drives the player |
| `Faction` | Owner faction on every ownable entity |
| `FactionRelations` | Singleton: team per faction |
| `MatchState` | Singleton: `Playing`/`Ended` and the winning team |
| `VictoryCritical`, `PopulationProvider` | Flags read by the systems below |

Systems: `PopulationSystem` recounts used/cap each frame. `VictorySystem` defeats players who lost every
`VictoryCritical` entity they had and ends the match when one team is left. Players are never defeated before they
first own something, so SubScene streaming at startup is safe.

## Orders

| Type | Role |
| --- | --- |
| `PlayerCommand` / `CommandType` | Smart, Move, AttackMove, Attack, Gather, Build, Stop, SetStance, PlaceBuilding, Produce, CancelProduction, SetRallyPoint, `Custom`+ |
| `Order` / `OrderType` | Move, AttackMove, Attack, Gather, Build, `Custom`+ |
| `ActiveOrder`, `QueuedOrder` | Current order and shift-queue |
| `OrderWriter` | `Issue(unit, order, queue)` and `Stop(unit)` |

Systems: `UnitCommandSystem` resolves unit commands (Smart: hostile → Attack, node → Gather, unfinished allied
building → Build, otherwise Move) and spreads group moves into a box `Formation`. `OrderDispatchSystem` promotes
queued orders. `MoveOrderSystem` runs Move/AttackMove and completes them on arrival or when a crowded unit stalls
near its goal. `PlayerCommandClearSystem` clears commands at the end of the order phase.

## Navigation and Units

| Type | Role |
| --- | --- |
| `UnitAuthoring` | Move speed, radius, population (on top of the common fields) |
| `MoveDestination` | Enableable XZ goal |
| `NavAgent`, `PathWaypoint`, `PathState` | Radius, remaining path corners, request bookkeeping |
| `NavObstacleAuthoring` / `NavObstacle` | Blocked XZ box (buildings add their footprint automatically) |
| `NavGrid` | Singleton walkability grid with `IsAreaFree`, `HasLineOfSight`, `TryFindNearestWalkable` |

Systems: `NavGridSystem` stamps obstacles into the grid when they change. `PathfindingSystem` runs Burst grid A*
(8-way, no corner cutting) with line-of-sight smoothing, at most 48 searches per frame, and skips A* when the goal
is directly visible. `MovementSystem` follows waypoints, separates overlapping units and never steps into blocked
cells. Movement is on the XZ plane; units keep their Y.

**Decision:** grid A* + smoothing + separation. Flow fields were deferred because every unit in a formation has its
own goal. Measured: 500 units crossing a 200 × 200 map with obstacles averaged 0.26 ms per simulation tick.

## Spatial

`SpatialIndex` is a singleton uniform-grid hash of every living entity with `Faction` + `Health`, rebuilt at the
start of the movement phase. Query it with a struct `ISpatialVisitor`. Targeting, separation and the AI use it.

## Combat

| Type | Role |
| --- | --- |
| `Health`, `Dead`, `SpawnOnDeath` | Hit points, death marker (enabled the frame before destruction), wreck prefab |
| `WeaponAuthoring` / `Weapon` | Range, damage, cooldown, damage type, projectile prefab, acquire range |
| `CombatStance` / `Stance` | Aggressive, Defensive (returns to anchor), HoldPosition, Passive |
| `AttackTarget` | Enableable current target |
| `DamageType` (asset), `ArmorAuthoring` / `ArmorModifier` | Damage multipliers per type |
| `Projectile` | Shot in flight |

Systems in order: `AttackOrderSystem` (explicit Attack orders) → `TargetAcquisitionSystem` (idle, attack-moving
and holding units, plus towers, pick the nearest hostile, scanning every 4th frame) → `EngagementSystem` (leash,
chase or stop in range) → `WeaponFireSystem` (instant hit or spawn a projectile) → `ProjectileSystem` (home in and
apply armor-scaled damage). `DeathSystem` runs in the lifecycle phase.

Not built in: stealth/detection, splash damage, kill credit, rotating turrets.

## Vision

`VisionRange` on units and buildings; `FogOfWar` singleton with one bit per team for *visible now* and *explored*.
`FogOfWarSystem` restamps it 10 times per second (or fills it once when the match disables fog).
`LocalFogViewSystem` publishes the local player's `LocalFogView` and tags hostile entities they can't see with
`FogHidden` once per restamp; selection, picking, the HUD and rendering all skip tagged entities.

## Resources

| Type | Role |
| --- | --- |
| `ResourceType` (asset) | A resource: name, colour, icon |
| `ResourceCost` / `ResourceStock` | Prefab price / player stockpile buffers; `ResourceMath` does the arithmetic |
| `ResourceNodeAuthoring` / `ResourceNode` | Neutral deposit, finite or regrowing |
| `ResourceDropOffAuthoring` | Building where harvesters deposit |
| `HarvesterAuthoring` / `Harvester` / `HarvestState` | Capacity, gather rate, trip state |

Systems: `GatherSystem` runs the node → fill → nearest drop-off → deposit loop and moves to a nearby node of the
same type when one runs dry. `ResourceNodeSystem` regrows or removes nodes.

## Buildings and production

| Type | Role |
| --- | --- |
| `BuildingAuthoring` | Footprint, population provided, starts-under-construction |
| `ConstructionProgress` | Enableable 0..1; unfinished buildings don't produce, provide population, accept cargo, fire or satisfy prerequisites |
| `BuilderAuthoring` / `Builder`, `BuildOption` | Build rate and placeable building prefabs |
| `ProducerAuthoring` / `Producer`, `ProductionOption`, `ProductionQueueItem`, `RallyPoint` | Unit training queue |
| `CompletedBuildings` | Prerequisite checks shared by the simulation, the AI and the HUD |

Systems: `PlaceBuildingSystem` validates placement (`PlacementMath`: map bounds, free nav cells, no overlap), charges
the cost, spawns a site and orders the builders to it. `ConstructionSystem` advances sites only while builders work
on them. `ProductionCommandSystem` handles Produce / Cancel (refund) / rally points. `ProductionSystem` trains the
queue head when population allows and sends new units to the rally point.

## AI

`SkirmishAISystem` drives every `AIPlayer` through `PlayerCommand`s only (the same path as a human): idle
harvesters gather, idle producers train affordable units round-robin, and once enough idle combat units exist they
attack-move to the nearest hostile building. Deterministic and easy to replace: disable it and write your own.

## Selection and Interaction (client state shared with the simulation)

`Selectable`/`Selected` (enableable), `SelectionInput` (gesture written by input), `SelectionDragState`,
`ControlGroup`. `SelectionSystem` applies clicks, drag-boxes (preferring the local player's units),
double-clicks (same `TypeId` on screen) and control groups. `PlacementState`, `PendingCommand` and `PointerState`
are singletons that input and the HUD share. `PlacementMath` is the single source of placement rules.

## Input (client)

`RTSInputActions` (Selection, Commands and Camera maps), `SelectionInputSystem`, `CommandInputSystem` (right-click
Smart, A/S/H, targeted commands), `PlacementInputSystem` (ghost + PlaceBuilding), `WorldPointer` (Unity Physics
raycast + ground plane), `CameraController` (pan, edge scroll, zoom, rotate, map clamp, `FocusOn`).

## Presentation (client)

`HUD.prefab` bundles the UI Toolkit `HUDController` (resource bar, selection panel with queue, command card,
minimap, game-over banner), `OverlayRenderer` (selection rings, health bars, placement ghost, rally markers via
`Graphics.RenderMeshInstanced`), `FogOfWarRenderer` (overlay shader) and the drag-box marquee. `TeamColorSystem`
tints owned meshes with the owner's colour through `URPMaterialPropertyBaseColor`, so use URP Lit materials.
`FogVisibilitySystem` mirrors `FogHidden` onto `DisableRendering` for the entity and its child meshes.
