# Movement modules

[Modules](../modules.md) ▸ Navigation, Units, Air, Transport

## Navigation

| Type | Role |
| --- | --- |
| `NavAgent`, `NavLayer` | Radius and layer: Ground, Naval, Amphibious, Air |
| `MoveDestination` | Enableable XZ goal |
| `PathWaypoint`, `PathState` | Remaining corners; request bookkeeping |
| `NavGrid`, `NavSurface` | Singleton grid of Land / Water / Deck flags (0 = blocked): `IsWalkable`, `IsAreaFree`, `HasLineOfSight`, `TryFindNearestWalkable`, `HeightFor(layer)` |
| `NavObstacleAuthoring` / `NavObstacle` | Blocked box, turned with the entity (buildings add their footprint) |
| `NavAreaAuthoring` / `NavArea` | Box overriding cells: Water, Blocked, or a Deck (bridge) with water below |
| `NavGround` | How cells get their surface (flooded = water, too steep = blocked); map tools use the same rules |
| `TerrainHeightAuthoring` / `TerrainHeight` | Baked heightfield: `Height`, `Raycast`; flat y = 0 without one |
| `PlacementMath`, `PlacementSurface` | Building placement rules for simulation, ghost, rally points and AI |
| `TargetReach`, `ReachMath` | Walks a unit to a target's footprint (build, repair, capture, board, gather); it parks up to 1.5 cells out |
| `ViewGround` | Ground points along camera rays (camera, minimap, listener) |

- `NavGridSystem` classifies cells from terrain, slope and water, stamps areas and obstacles, and bumps a version
  so units re-path.
- `PathfindingSystem`: Burst 8-way A* on the agent's layer with line-of-sight smoothing, at most 48 searches a
  frame. A search stops after `NavGrid.MaxSearchExpansions` cells and heads for the closest one found.

Layers that share a surface flag share cells: ships pass under a deck that tanks cross.

## Units

`UnitAuthoring`: speed, radius, nav layer, population. `MovementSystem` follows waypoints, separates overlapping
units, keeps them on their layer's ground, deck or water, and disables `MoveDestination` on arrival.

## Spatial

Part of `Navigation`.

`SpatialIndex`: a uniform-grid hash of living entities with `Faction` + `Health`, rebuilt at the start of the
movement phase. Query it with a struct `ISpatialVisitor`; `Closest` breaks ties by entity index.

## Air

| Type | Role |
| --- | --- |
| `FlightAuthoring` / `Flight` | Makes a unit an aircraft: altitude, climb speed, loiter radius (0 hovers), uses pads |
| `AirfieldAuthoring` / `LandingPad`, `HomePad` | Pads on a building, one aircraft each; an aircraft's pad |
| `Docked` | Enableable, replicated: landed on its pad |

An airfield waits for a free pad before producing a pad user. `PadSystem` claims and frees pads and runs
ReturnToBase; `RearmSystem` sends empty aircraft home and reloads them while docked. Fuel is game code.

## Transport

| Type | Role |
| --- | --- |
| `ContainerAuthoring` / `Container`, `Cargo` | Capacity, largest passenger, fire out, passengers survive its death; `Cargo` replicates |
| `NeutralGarrison` | A neutral building anyone may board; it belongs to its passengers and turns neutral when empty |
| `PassengerAuthoring` / `Passenger` | Size; stance restored on exit |
| `Boarding` | `CanBoard`, `Board`, `IsInside` |

A passenger aboard is hidden, out of the spatial index, takes no orders, and fires out or stays passive.
`BoardingSystem` runs Enter, `CargoFollowSystem` carries passengers, `UnloadSystem` unloads, and
`ContainerDeathSystem` ejects or kills them when the container dies.
