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
- A clearance field gives each cell, per layer, the side of the largest open square above it. A unit needs a
  square as wide as its diameter rounded up to whole cells, so a 3.7 m tank on 2 m cells never paths through a
  one-cell gap. Units one cell wide path as before. The field is rebuilt with every restamp.
- Smoothing and corner skipping keep the unit's radius from closed cells, capped to fit the gap its square fits.
- Movement holds the whole capsule off closed cells (`NavCapsule`). A unit already overlapping may move or turn
  as long as the summed overlap doesn't grow, so it can always drive or back off. A long hull keeps its facing
  when turning would swing an end into a closed cell.
- A unit walled off from its next corner keeps its path for 0.75 s, then re-plans only the way back to that corner
  and keeps the rest, so it goes on round the side it was taking. An unreachable corner gets a whole new path.
- Destructible scenery with a crush rating (trees, fences) marks its cell on a scenery layer with that rating. A
  unit drives through scenery rated up to its `SceneryCrushLimit`: its `Crusher` size, or all of it with **Walks
  Through Scenery** (infantry). Rated higher, scenery blocks it like a closed cell for paths and movement. Felled
  scenery reopens its cell without invalidating paths.
- Layers that share a surface share cells, so ships pass under a bridge that tanks cross.
- `TargetReach` walks a unit up to a target to build, repair, capture, board or gather. It heads for the point just
  outside the target's footprint edge nearest the unit, and counts it there when the capsule, hull edge to footprint
  edge, is within its slack plus a nav cell, so a long truck arriving nose first stops at the wall.
- `SpatialIndex` answers fast range queries over living entities.

## Units

`UnitAuthoring` sets speed, radius, hull length, spacing, nav layer, whether it walks through scenery, and
population. Units follow their path and
stop on arrival. A unit with a hull length is a capsule that long along its forward and two radii wide; 0 keeps a
circle. `SeparationVisitor` pushes neighbours apart along the line between their closest points, half each, until
the larger of their two spacings lies between them (0.6 m by default, less for infantry). Formation slots are
spaced by the same footprint, so a tank column doesn't pile up nose to tail.

Without more, a unit steps straight at its next corner at full speed and faces where it steps, as infantry do.
`LocomotionAuthoring` gives a ground vehicle handling, and its `LocomotionState` keeps its speed between frames.
Its facing is its rotation, and it moves only along it, so the crowd's push never turns the hull.

| Field | Does |
| --- | --- |
| Acceleration, Braking | Speed gained and shed per second |
| Turn Rate | Fastest the hull turns, in degrees per second |
| Turn Radius | Tightest circle it drives. The hull turns only as it rolls, at most speed over radius. 0 turns at any speed |
| Pivots | Stops and turns in place when more than 60 degrees off its heading, as tracked vehicles do |
| Reverse Speed, Reverse Distance | Backs up to a goal behind it when the path left is at most this long (about 6 m wheeled, 10 m tracked) and turning round would clearly take longer |

- It cuts corners within its look-ahead (twice its radius, half a second of travel and half its turn radius) once
  the next corner is in sight.
- It slows to the speed at which its turn rate can hold the arc to its next corner, brakes ahead of sharp
  corners, and brakes to stop at the goal.
- A target inside its turning circle can't be reached on any arc, so it makes a three-point turn: it goes the
  other way, turning toward the target, until the target is clear of the circle. A goal under the hull inside the
  circle counts as reached. A reversing leg that runs into a wall drives the other leg for 1.5 s. So it never
  orbits.
- A wall that stops it head-on takes its speed with it. A glancing slide or the crowd's push costs none.
- `MoveOrderSystem` restarts its stuck timer when a unit turns about 20 degrees, so a pivot never counts as stuck.

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
