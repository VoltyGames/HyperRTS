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

`UnitAuthoring` sets speed, radius, hull length, spacing, nav layer and population. Units follow their path and
stop on arrival. A unit with a hull length is a capsule that long along its forward and two radii wide; 0 keeps a
circle. `SeparationVisitor` pushes neighbours apart along the line between their closest points, half each, until
the larger of their two spacings lies between them (0.6 m by default, less for infantry). Formation slots are
spaced by the same footprint, so a tank column doesn't pile up nose to tail.

`BlockerAuthoring` makes remains such as a wreck solid: a capsule of radius and hull length along its forward that
never moves. Units take the whole push and are moved fully clear of it where they are headed, so they slide round
it. It doesn't close nav cells, so a unit beside a tank as it dies is never trapped. A `Decay` wreck stops blocking
once it starts to sink.

## Air

`FlightAuthoring` makes a unit an aircraft with an altitude and a loiter radius. A radius of 0 hovers.
`AirfieldAuthoring` gives a building landing pads, one aircraft each. An airfield only builds a pad-using aircraft
when a pad is free. An aircraft flies home once every weapon with limited rounds (its `Ammo` and mounts with
rounds) is empty, so a gunship with an unlimited gun still returns when its missiles are spent. It reloads while
docked. Its remains fall to the ground under gravity. Fuel is left to game code.

## Transport

`ContainerAuthoring` lets a vehicle or building carry units with `PassengerAuthoring`. It sets the capacity, the
largest passenger and whether passengers fire out or survive its death.

A passenger is hidden and takes no orders. A neutral garrison building belongs to whoever is inside and turns
neutral when empty.
