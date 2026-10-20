# Movement modules

[Modules](../modules.md) ▸ Navigation, Units, Air, Transport

## Navigation

Units find paths on a grid over the map. Each cell is land, water, deck or blocked. Each unit moves on one layer:
Ground, Naval, Amphibious or Air. To move a unit, set and enable its `MoveDestination`.

| Authoring | Does |
| --- | --- |
| `TerrainHeightAuthoring` | A baked heightfield. Without one the ground is flat |
| `NavObstacleAuthoring` | Blocks a box. Buildings block their footprint |
| `NavAreaAuthoring` | Marks a box as water, blocked, or a bridge deck with water below |

- Flooded cells become water and steep ones blocked.
- Pathfinding is Burst A* with smoothing, at most 48 searches a frame.
- Layers that share a surface share cells, so ships pass under a bridge that tanks cross.
- `TargetReach` walks a unit up to a target to build, repair, capture, board or gather.
- `SpatialIndex` answers fast range queries over living entities.

## Units

`UnitAuthoring` sets speed, radius, nav layer and population. Units follow their path, push apart when they
overlap, and stop on arrival.

## Air

`FlightAuthoring` makes a unit an aircraft with an altitude and a loiter radius. A radius of 0 hovers.
`AirfieldAuthoring` gives a building landing pads, one aircraft each. An airfield only builds a pad-using aircraft
when a pad is free. Aircraft with no ammo fly home and reload while docked. Fuel is left to game code.

## Transport

`ContainerAuthoring` lets a vehicle or building carry units with `PassengerAuthoring`. It sets the capacity, the
largest passenger and whether passengers fire out or survive its death.

A passenger is hidden and takes no orders. A neutral garrison building belongs to whoever is inside and turns
neutral when empty.
