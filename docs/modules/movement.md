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
- Obstacles restamp the grid only when their footprints change. A building that just changes archetype, such as
  gaining or losing a fog tag, keeps the grid and every path as they are.
- Each restamp records the box of cells it changed (`NavChanges`). A path re-plans only when one of its legs passes
  within its unit's diameter plus a cell of a changed box, so a building placed across the map leaves it alone. A
  path that ended short of its goal re-plans on any change, and one older than 64 restamps always does.
- Pathfinding is Burst A* with smoothing. Searches run oldest request first until the frame has expanded 40,000
  cells (looking along a straight path counts its length); the rest wait a frame. A* keeps its records in flat
  arrays sized to the grid and reused, each search stamping them with its own generation.
- Every restamp labels connected regions per layer for footprints 1 to 4 cells wide (`NavRegions`). A goal the
  start's region can't reach snaps to the nearest open ground it can, within 64 cells, so a click across a river
  stops on this bank. With none that near, the move ends where the unit stands, with no search.
  `NavGrid.IsReachable(agent, from, to)` asks the same of any two points. Regions leave scenery out, and wider
  footprints read the 4-cell regions, which may promise a way that is too narrow.
- A clearance field gives each cell, per layer, the side of the largest open square above it. A unit needs a
  square as wide as its diameter rounded up to whole cells, so a 3.7 m tank on 2 m cells never paths through a
  one-cell gap. Units one cell wide path as before. The field is rebuilt with every restamp, one job per layer.
- Smoothing and corner skipping keep the unit's radius from closed cells, capped to fit the gap its square fits.
  Line of sight walks every cell the line crosses (Amanatides and Woo) and checks the closed cells near each against
  that clearance exactly, so no gap slips between samples. A unit that can't see past its next corner looks again
  only once it enters another cell or the grid changes.
- Movement holds the whole capsule off closed cells (`NavCapsule`). A unit already overlapping may move or turn
  as long as the summed overlap doesn't grow, so it can always drive or back off. A move into a wall slides along
  it, round the wall's normal taken from how the overlap grows, so rotated buildings and diagonal walls never snag
  it. A long hull keeps its facing when turning would swing an end into a closed cell.
- A unit that stops getting on keeps its path for 0.75 s, then re-plans only the way back to its next corner and
  keeps the rest, so it goes on round the side it was taking. An unreachable corner gets a whole new path.
- Destructible scenery with a crush rating (trees, fences) marks its cell on a scenery layer with that rating. A
  unit drives through scenery rated up to its `SceneryCrushLimit`: its `Crusher` size, or all of it with **Walks
  Through Scenery** (infantry). Rated higher, scenery blocks it like a closed cell for paths and movement. Felled
  scenery reopens its cell without invalidating paths.
- Layers that share a surface share cells, so ships pass under a bridge that tanks cross.
- `TargetReach` walks a unit up to a target to build, repair, capture, board or gather. It heads for the point just
  outside the target's footprint edge nearest the unit, and counts it there when the capsule, hull edge to footprint
  edge, is within its slack plus a nav cell, so a long truck arriving nose first stops at the wall. A building's
  footprint is its `NavObstacle` and a unit's, such as a transport, the box round its `NavAgent` hull.
- The extra cell stays: a footprint closes every cell it overlaps, and a rotated one's closed cells are no box, so
  one cell bounds them without asking the grid.
- A walk-up whose move locomotion ends by itself short of the target, given up or at the end of a path that can't
  get there, is `WalkUp.Stuck`, and build, repair, capture, board and gather finish their order. A target that moved
  off is walked to again. A new order clears the last one's `Arrival`.
- `SpatialIndex` answers fast range queries over living entities.

## Units

`UnitAuthoring` sets speed, radius, hull length, spacing, nav layer, whether it walks through scenery, and
population. Units follow their path and
stop on arrival. A unit with a hull length is a capsule that long along its forward and two radii wide; 0 keeps a
circle. `SeparationVisitor` pushes neighbours apart along the line between their closest points, half each, until
the larger of their two spacings lies between them (0.6 m by default, less for infantry). Each formation slot is
sized by its own unit's footprint, so a tank column doesn't pile up nose to tail and infantry beside it stay close.
A row is as deep as its deepest member. A slot in a building or off the unit's ground moves to the nearest spot the
unit fits and can reach; one only across water or a wall is left to the pathfinder's snap into the unit's region.

- An idle parked unit steps aside for a moving unit of its own side (`GivingWay`): it shares the push and moves
  out of the mover's lane, toward the mover's left head-on, so a unit in a one-unit gap or on a bridge makes way.
  It stays where it was pushed.
- A busy parked unit (one with an order, a target, a pad or Hold Position) and an enemy hold their ground. The
  mover takes the whole push and steers round them, keeping right head-on, or waits against one standing on its
  goal. So a truck never shoves a dozer that is building.
- A formation move gives its units one group (`MoveDestination.Group`). A unit touching an arrived groupmate that
  lies toward its goal has arrived too, so arrival passes through a crowded group by contact.
- A move ends by itself at the goal, against an arrived groupmate, or when the unit stops getting on. Then
  `MoveDestination` is disabled and `Arrival` enabled, with `Stalled` set when it gave up. `MoveOrderSystem`
  finishes its order on `Arrival`. If anything else halts the unit, such as combat letting go, it points the unit
  at the goal again.
- `MoveProgress` is the one stuck measure. Progress is the path left shrinking by the arrival tolerance or the hull
  turning about 20 degrees, so a pivot or a three-point turn counts. Without progress for 0.75 s the way to the
  next corner is re-planned, then after waits doubling up to 6 s. A unit gives up after 3 s only within four radii
  of its path's end, where the goal is taken. Farther out a jam is traffic, so a column queued at a bridge keeps
  its order. A goal it can't reach snaps into its region, so it stops there. A new path is measured afresh but the
  clock runs on.

Every unit follows its path through `Locomotion`. Without its own, a unit gets `Locomotion.Instant`: full speed and
any facing at once, never reversing, as infantry do. `LocomotionAuthoring` gives a ground vehicle handling, and its
`LocomotionState` keeps its speed between frames. `PathState` caches the path's length past the next corner.
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
  circle counts as reached. A reversing leg that runs into a wall drives the other leg for 1.5 s, a manoeuvre
  timer rather than a stuck test. So it never orbits.
- A wall that stops it head-on takes its speed with it. A glancing slide or the crowd's push costs none.

`BlockerAuthoring` makes remains such as a wreck solid: a capsule of radius and hull length along its forward that
never moves. It closes the land cells it covers, so paths go round it and `NavCapsule` keeps units out, and nothing
can be built on it. Water and decks stay, so ships pass under a wreck on a bridge. A unit caught in its cells as it
appears drives out, since its path first steps out to open ground and `NavCapsule` lets it move while its overlap
shrinks. A `Decay` wreck stops blocking once it starts to sink, and its cells open again.

## Air

`FlightAuthoring` makes a unit an aircraft with an altitude and a loiter radius. A radius of 0 hovers.
`AirfieldAuthoring` gives a building landing pads, one aircraft each. An airfield only builds a pad-using aircraft
when a pad is free. An aircraft flies home once every weapon with limited rounds (its `Ammo` and mounts with
rounds) is empty, so a gunship with an unlimited gun still returns when its missiles are spent. It is sent home
once, so a player order given after that holds. It reloads while docked. Its remains fall to the ground under gravity. Fuel is left to game code.

## Transport

`ContainerAuthoring` lets a vehicle or building carry units with `PassengerAuthoring`. It sets the capacity, the
largest passenger and whether passengers fire out or survive its death. They get out or die in the frame the
container dies, and dying passengers credit its killer.

A passenger is hidden and takes no orders. A neutral garrison building belongs to whoever is inside and turns
neutral when empty.
