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
| `DockAuthoring` | Bays where vehicles load and unload, see [docks](economy.md#docks) |

- Flooded cells become water and steep ones blocked.
- Obstacles restamp the grid only when their footprints change. A building that just changes archetype, such as
  gaining or losing a fog tag, keeps the grid and every path as they are.
- Each restamp records the box of cells it changed (`NavChanges`). A path re-plans only when one of its legs passes
  within its unit's diameter or wall margin, whichever is more, plus a cell of a changed box, so a building placed
  across the map leaves it alone. A path whose goal was snapped re-plans on any change, and one older than 64
  restamps always does.
- Each unit has a `MovementClass`: its layer, scenery crush limit and exact radius. `NavFieldSystem` registers the
  class of every `NavAgent` prefab and unit, up to 16 per match, in server and local worlds only.
- `NavField` holds the signed distance in metres from each cell centre to the nearest wall, per layer and crush
  limit: positive in the open, negative inside buildings, about 0 on their edge, capped at 8 m. Off the map counts
  as wall. It is rebuilt per 16 by 16 cell sector over a window with an 8 m halo (Felzenszwalb's lower envelope),
  one job per dirty sector and key, so a full build is every sector dirty. Restamps and scenery mark only the
  sectors round the cells they changed, so wrecks far apart in one frame leave the sectors between them alone.
- A cell is open to a class when the field at its centre is at least its radius (`NavAccess`). A*, the nearest
  cell search and line of sight all use that one rule, so a gap exactly two radii wide lets a unit through.
- Each class has a sector graph (`NavSectors`), rebuilt right after the field in the same worlds. Inside each 16 by
  16 sector the open cells split into 4-connected components, each with a representative cell near its centroid.
  Components of neighbouring sectors that touch across the border share an edge, and union-find over the edges gives
  region ids. A dirty sector relabels alone, then the edges along its borders and the region ids follow.
- A goal the start's region can't reach snaps to the nearest open cell it can, within 64 cells, so a click across a
  river stops on this bank. With none that near, the move ends where the unit stands, with no search.
  `NavGrid.IsReachable(agent, from, to)` asks the same of any two points, a closed end counting from the open cell
  nearest it. Regions leave scenery out.
- A unit in a closed cell starts its path at the nearest open cell it can squeeze to (`NavAccess.TryFindStart`):
  every cell on the straight line there reads above 0 and at least the smaller of the class radius and the field where
  it stands, so a dozer in a gap narrower than its class drives along the gap but never through water or a wall. It
  takes such a cell in the goal's region first (the region of the open cell nearest the goal), then in any region,
  then the nearest open cell, which is how a unit inside a new footprint gets out. The first leg runs from the unit
  to that cell.
- A path query snaps the start and goal (`GridPathfinder.Prepare`) and goes straight when the end is in sight.
  Otherwise an abstract A* over the sector graph (`SectorSearch`) finds the sectors from the start's component to
  the goal's, and Burst A* runs over cell centres inside them plus a ring of sectors (`NavCorridor`). It always
  succeeds: the corridor's components connect by 4-connected steps, which A* allows. Its records are sized to the
  corridor, slot x 256 plus the local cell, so searches run in parallel. Each step costs its length times
  1 + 2 x max(0, margin - distance) / margin, where the margin is the agent's `WallMargin`: its radius, 2 m, and a
  quarter of its half hull segment, set at bake. Paths so keep room off walls where there is room; turning room
  is the drive planner's to find.
- Each frame takes requests oldest first until their estimated expansions spend the budget of 30,000, so it runs
  over by one search at most, then serves them at once, one job each. A search's estimate is half its corridor's
  cells. The rest wait a frame, and their units brake and turn toward the goal. Each search runs to the end, so no
  path is ever partial. `PathStats` counts searches by cause (`PathCause`: order, chase, grid change, replan, and
  broken group traces) and both the estimated and the real expansions.
- A goal set by `ReachMath.MoveTo`, as chases, walk-ups and escorts do, is a chase (`MoveDestination.Chase`). When it
  moves, the path's last waypoint follows it if the corner before it, or the unit on its last leg, still sees it and
  it moved no more than a quarter of the path left. Otherwise the unit keeps its path and searches again at most
  every 0.5 s. A new order always plans at once.
- A formation move's members of one class share a flow field (`GroupFields`): a Dijkstra with the same step costs
  from the cell nearest the centre of their slots, over the union of their corridors. Each member traces it downhill,
  leaves it at its first cell near the goal that sees its own slot, and is pulled tight with line of sight, so it
  still gets ordinary waypoints. A replan while the field is valid is a trace again, with no search. A field goes when
  a grid change reaches its corridor, within a second of no moving unit using it, or after 30 s idle. Over 60,000
  cells, members split by start sector into several fields. A member alone, or one whose slot lies out of sight of the centre or
  over 48 m from it, searches by itself, as do chases and walk-ups.
- Smoothing and corner skipping use line of sight, which walks every cell the line crosses (Amanatides and Woo) and
  needs each open.
- Movement holds the whole capsule off walls (`NavCapsule`). Its depth sums how far points half a radius apart
  along its core sink below the radius in the field. A unit already overlapping may move or turn as long as the
  depth doesn't grow, so it can always drive or back off. A move into a wall slides along it, round the field's
  gradient at the deepest point, so rotated buildings and diagonal walls never snag it. A long hull keeps its
  facing when turning would swing an end into a wall.
- Destructible scenery with a crush rating (trees, fences) marks its cell on a scenery layer with that rating. A
  unit drives through scenery rated up to its `SceneryCrushLimit`: its `Crusher` size, or all of it with **Walks
  Through Scenery** (infantry). Rated higher, scenery blocks it like a closed cell for paths and movement. Felled
  scenery reopens its cell without invalidating paths.
- Layers that share a surface share cells, so ships pass under a bridge that tanks cross.
- `TargetReach` walks a unit up to a target to build, repair, capture, board or gather. It heads for the point just
  outside the target's footprint edge nearest the unit, and counts it there when the capsule, hull edge to footprint
  edge, is within its slack plus a nav cell, so a long truck arriving nose first stops at the wall. A building's
  footprint is its `NavObstacle` and a unit's, such as a transport, the box round its `NavAgent` hull. The job hands it a
  `MoveControl`: the destination, its enabled bit and the `Arrival` bit, written together.
- The extra cell stays: a footprint closes every cell it overlaps, and a rotated one's closed cells are no box, so
  one cell bounds them without asking the grid.
- A walk-up whose move locomotion ends by itself short of the target, given up or at the end of a path that can't
  get there, is `WalkUp.Stuck`, and build, repair, capture, board and gather finish their order. A target that moved
  off is walked to again. A new order clears the last one's `Arrival`.
- `SpatialIndex` answers fast range queries over living entities.

### Drive planner

`DrivePlanner` checks every fresh path of a unit with a turn radius or a hull longer than it is wide, in the same
serve job, after the route. Easy paths run no search. Paths with a stretch to search go to one serial `DriveJob`,
which spends at most `PathfindingSystem.DriveBudget` (one capped search, 6,000 expansions) a frame, in task order. The
frame's first such path plans within it and leaves any stretch past it as drawn (`PathState.PartlyPlanned`), planned
once its planned part is driven. Later paths wait a frame (`PathStats.DeferredDrives`).

- **Start.** Wheels sweep the forward curve from the unit's pose to the pose `2 x TurnRadius + hull length` along
  the path. Tracks sweep a turn in place to the first leg, sliding off walls as locomotion does, or that curve. When
  walls held the unit as it asked for a replan (`PathState.Stuck`, copied to `PathRequest.Stuck`), the start is
  always searched.
- **Corners.** Each corner sweeps the forward curve from a fillet length plus a hull before it to as far after. Tracks
  may pivot on the corner instead.
- Curves are `DriveCurve`s: the shortest forward turn, straight, turn (Dubins) at a little over the turn radius (85%
  of full lock, so the follower has room to correct), or one hull length for tracks, with `TryFit` to a pose and
  `TryFitPoint` to a point. Sweeps sample the hull every 0.5 m and count a stretch blocked once it sinks deeper than
  0.05 m into walls, grown by 0.25 m of room.
- **Search.** A blocked stretch runs a `DriveSearch`, Hybrid A* over the rear axle's pose in a window round it (its
  ends plus two turn radii and a hull, at most 128 m across). It copies the window's wall distances once
  (`FieldWindow`), so its hull checks read them unchecked. Steps are 1.5 m arcs: wheels at none, half and 85% of
  full lock either way, tracks straight or on arcs of one hull length, plus 22.5 degree pivots; forwards and, within
  `ReverseRun`, backwards. A reverse run ends only after a hull length forwards, so a nudge never buys a longer one.
  Where the hull centre has half a hull and 1 m of room to walls the ground is open: steps are 3 m and forwards only,
  and poses key on 2 m cells and 15 degree bins, elsewhere on 1 m cells and 5 degree bins. A pose, and the middle of
  the step to it, may overlap walls only while the overlap shrinks, so a wedged start plans its way out. Steps cost
  their length times the wall-margin cell cost, twice in reverse, plus two hull lengths per gear change and a little
  per change of steer.
- **Guide.** A `DriveGuide` gives each window cell its cost to the goal for a disc the hull's width, by Dijkstra over
  buckets half a cell wide. A turn where walls crowd the hull (within a turn radius of them) costs its arc up to 16
  times over, so the guide prefers routes the hull can turn on, and a pose facing off the way its cell's route leaves
  pays a quarter of that turn. A pose goal in a lane, walls on both sides within two hull lengths, has its front
  closed, so the guide leads in from behind, as the hull must arrive. Cells it doesn't reach read the straight
  distance, and a pose goal reads no less than the forward curve to it. The guide is weighted 1.5. Every 10
  expansions, and every third near the goal, a clear forward curve to the goal ends the search. Goals are a pose
  within 0.75 m and 10 degrees, or the path's end within the arrival tolerance. A pose goal whose hull centre sits in
  a wall, as at the end of an attack path into a building, fails at once instead of spending the cap.
- A search stops after 6,000 expansions (`DriveSearch.MaxExpansions`). A stretch that fails tries once more
  reaching twice as far along the path, where the hull may find room to turn, then stays as drawn.
- The result replaces the stretch with `PathWaypoint`s every 1.5 m, each `Tight`; `Reverse` marks a leg driven
  backwards, and a gear change is a waypoint where it flips. `PathStats.Drive` (`DriveCounts`) counts the drive
  searches, their expansions and their failures.
- `PathState.SkipCorners` never cuts a tight waypoint: it passes once touched or behind the unit, and a gear change
  only on arrival or once overshot. `TryFollowGoal` and the small goal move never move a tight last waypoint; a goal
  that moves searches again.
- **Pose ends.** A move with `MoveDestination.Facing` set, as into a [dock bay](economy.md#docks), ends as a pose:
  the path's last stretch becomes the forward curve, or a search, to the pose `RunIn` metres before the goal, then
  the run-in straight, all tight. The run-in shrinks to what lies ahead of the unit, and a route cut short of its goal
  ends as drawn. `MoveDestination.ExitRun` starts the path with that many metres straight ahead, tight, and routes
  from its end.
- The Paths debug layer draws tight legs in orange, reverse legs dashed, gear changes as dots, each wheeled
  vehicle's rear axle and dock bays.

## Units

`UnitAuthoring` sets speed, radius, hull length, spacing, nav layer, whether it walks through scenery, and
population. Units follow their path and
stop on arrival. A unit with a hull length is a capsule that long along its forward and two radii wide; 0 keeps a
circle. A moving unit steers by sampling (`Steering`). It scores candidate velocities against the nearest 8 units
within reach (`CrowdVisitor`) and the walls' distance field, and picks the cheapest:

- Candidates: a vehicle tries what its handling reaches within 0.5 s, 7 headings across its reachable turn at half,
  three quarter and full speed, plus standing and backing straight up when it can reverse. An instant unit tries 12
  directions at 3 speeds and small swerves off its planned direction. Both also try the planned velocity and last
  frame's choice. With no neighbour in reach a pivot plans to stand, not to roll on as it brakes, so a lone tank
  pivots into a gap rather than orbiting it.
- Cost: how much deeper footprints would overlap at their closest approach in each of 0 to 0.3, 0.3 to 0.75 and
  0.75 to 1.5 s, sooner counting more, under straight-line prediction. Capsules count as capsules. A moving
  neighbour is scored RVO-style: the mover takes only its share of the change. The wall term samples the field at
  both capsule ends, only as far as the next corner. Straying from the planned velocity and from last frame's choice
  costs, standing still costs a little extra, except for a lone tracked hull whose leg lies beyond its reachable
  headings, where stopping to turn is the way on, and swerving left costs a little more than right, so a head-on pair
  both keep right.
- The planned velocity is taken without sampling when it hits nothing, or when no neighbour is in reach and no
  wall within the wall margin. Parked units never steer. A three-point turn or a back-off skips steering, and so
  does a planned manoeuvre: it stands while driving on would run deeper into a neighbour within the 1.5 s horizon
  than standing would, and one held too long plans again, so two waiting on each other can't deadlock.

Velocities are last frame's (`Velocity`). The chosen velocity goes back to `Locomotion` as the heading and speed to
drive, which wheels turn into the curvature they steer for, so a vehicle turns as its chassis can rather than
sliding sideways. A wheeled vehicle's reachable turn grows as its wheels steer over at their steer rate. Told to go back, it backs straight up
if it can reverse and brakes otherwise. Layers that can't block each other
ignore each other, aircraft included. The same query feeds the overlap fallback, which pushes neighbours apart
along the line between their closest points, half each, until the larger of their two spacings lies between them
(0.6 m by default, less for infantry). The push only moves spacing about; hulls never meet. Each unit's whole move
(step, push and way out of walls) closes on each neighbour by at most half the room between their hulls
(`HullRoom`), so a pair can't meet however the pushes of a packed queue add up. Two parked units, and a mover
against an idle ally that gives way, keep the soft push alone, so a packed crowd still flows. A move a wall's slide
turns back into a neighbour stands. Each formation slot is
sized by its own unit's footprint, so a tank column doesn't pile up nose to tail and infantry beside it stay close.
A row is as deep as its deepest member. A slot in a building or off the unit's ground moves to the nearest spot the
unit fits and can reach; one only across water or a wall is left to the pathfinder's snap into the unit's region.

- Two movers share the avoiding half each, unless they go one way: the directions to their goals under 60 degrees
  apart, so a pair crossing at right angles still shares it. Then the one further back along their shared heading
  gives way: it keeps its whole spacing, so it backs off, and takes the push beyond half the spacing. The one ahead
  keeps half the spacing and shares only the push inside it, so an abreast pair at a one-lane bridge never waits on
  each other and a follower pressed from both sides is never driven into.
- An idle parked unit steps aside for a moving unit of its own side (`GivingWay`). The mover doesn't steer round it,
  so it never stops short in a gap. The idle unit shares the push and moves out of the mover's lane, toward the mover's left head-on, so a unit in a one-unit
  gap or on a bridge makes way. It stays where it was pushed.
- A busy parked unit (one with an order, a target or Hold Position), a landed aircraft and an enemy hold their ground. To the
  mover it is a fixed obstacle: the mover takes the whole avoiding and the whole push, so a truck never shoves a dozer that
  is building. A unit standing on the mover's goal and an arrived groupmate are not avoided, so the mover waits
  against the one and arrives by touching the other.
- A unit sunk in walls, as when a building or wreck lands on it, parked or moving, drives out at its own top
  speed on top of its planned step. `NavCapsule.WayOut` heads down the field's slope at its sunk points, or toward
  the nearest open cell where the capped field reads flat. The wall constraint still never lets it go deeper.
- A formation move gives its units one group (`MoveDestination.Group`) and the span of its slots
  (`MoveDestination.Extent`, twice the farthest slot's distance from the goal). A unit within that span of its own
  slot that touches an arrived groupmate lying toward its goal has arrived too, so arrival passes through a crowded
  group by contact. Farther out a parked groupmate is only traffic. A groupmate that itself stopped that way
  (`ArrivalReason.AtGroupmate`) doesn't count, so a group crossing a bridge never stops in a chain back onto the deck.
- A move ends by itself at the goal, against an arrived groupmate, or when the unit stops getting on. Then
  `MoveDestination` is disabled and `Arrival` enabled, its `Reason` saying which. `MoveOrderSystem`
  finishes its order on `Arrival`. If anything else halts the unit, such as combat letting go, it points the unit
  at the goal again.
- `MoveProgress` is the one stuck measure. Progress is the path left shrinking by the arrival tolerance or the hull
  turning about 20 degrees, so a pivot or a three-point turn counts. Without progress for 1 s the whole path is
  planned again from where the unit stands, then after waits doubling up to 4 s. A unit gives up after 3 s within
  four radii of its path's end, where the goal is taken, or anywhere while it is pinned in walls it can't leave.
  Before giving up there, a vehicle that reverses backs out for 3 s (`MoveProgress.TryTurnOut`), tracks too, and
  tries again only after it gets closer.
  On a planned manoeuvre, which stands to change gear and steer, it plans again only while walls hold it, and it
  never turns out. A hull creeping under 2 mm a frame against walls counts as held.
  Farther out a jam is traffic, so a column queued at a bridge keeps its order. A goal it can't reach snaps into its region, so it stops there. A new path is measured
  afresh but the clock runs on, and a pending path isn't measured.

Every unit follows its path through `Locomotion`. Without its own, a unit gets `Locomotion.Instant`: full speed and
any facing at once, never reversing, as infantry do. `LocomotionAuthoring` gives a ground vehicle handling, and its
`LocomotionState` keeps its speed between frames. Each frame a `Driver` bundles the handling, the chassis, the
radius, the top speed and the state, and drives the leg: it turns its facing and returns the hull's move.
`PathState` caches the path's length past the next corner.
Its facing is its rotation, and the crowd's push never turns the hull. A parked unit with an enabled `FacingGoal`,
as a hull-fixed weapon sets, turns toward it standing at its turn rate, wheeled ones too.

`Locomotion` holds the dynamics:

| Field | Does |
| --- | --- |
| Acceleration, Braking | Speed gained and shed per second |
| Turn Rate | Fastest the hull yaws, in degrees per second: tracks turn at it, wheels hold it as their grip at speed |
| Steer Rate | How fast the steered wheels turn, in degrees per second |
| Reverse Speed | Top speed backing up |

`NavAgent.Chassis` says how the hull moves, for locomotion and the path planner alike. `UnitAuthoring` bakes it
from the `LocomotionAuthoring` beside it:

| Field | Does |
| --- | --- |
| Wheelbase | Metres from the steering axles' midpoint to the rear axle; 0 for tracks |
| Rear Axle | Metres the rear axle (the fixed axles' midpoint) sits behind the hull centre; 0 for tracks |
| Turn Radius | Tightest circle of the rear axle. 0 turns at any speed |
| Pivots | Stops and turns in place about the centre when more than 60 degrees off its heading, as tracked vehicles do |
| Reverse Run | Backs up to a goal behind it when the path left is at most this long (about 6 m wheeled, 10 m tracked) and turning round would clearly take longer; 0 never reverses |

A hull with a turn radius that doesn't pivot is wheeled. Each frame a wheeled vehicle:

- aims its rear axle at the point its look-ahead reaches along the path, round the next corner, and wants the arc
  through it: curvature `2 sin(angle) / distance` (pure pursuit). A point behind takes full lock.
- steers `LocomotionState.Steer` (signed curvature in 1/m) toward it, the steer angle `atan(wheelbase x curvature)`
  changing at most at the steer rate, clamped to the turn radius and to the turn rate over speed.
- rolls the rear axle along that arc (`AxlePose.Advance`) and puts the centre `RearAxle` ahead of it, so the centre
  drifts slightly inward in a turn and the front axle runs a wider circle than the rear.

Tracks and instant units turn their facing about the centre at the turn rate (`Chassis.Rotate`) and move along it.
**Fill From Wheels**, on the Locomotion's context menu, measures the wheelbase and rear axle from the model's
`Wheel_L01`... parts, front to back: the first Steering Axles axles steer and the rest make the rear axle.
`LocomotionRule` checks a wheeled unit has a wheelbase and both axles inside its hull.

- It cuts corners within its look-ahead (twice its radius, half a second of travel and half its turn radius) once
  the next corner is in sight.
- It slows to the speed at which its turn rate can hold the arc to its next corner, brakes ahead of sharp
  corners, and brakes to stop at the goal.
- A target inside its turning circle can't be reached on any arc, so it makes a three-point turn: it goes the
  other way, turning toward the target, until the target is clear of the circle. A goal under the hull inside the
  circle counts as reached. So it never orbits.
- A wheeled vehicle starts a turn as its look-ahead point rounds the corner, before it reaches the corner.
- Wheels can't turn standing, so a wheeled leg that runs head-on into a wall, or that steering stops short of one,
  drives the other way for 1.5 s (`LocomotionState.OtherWay`): it backs off to turn, or takes a three-point turn's
  forward leg. This is a manoeuvre timer, not a stuck test.
- A moving hull whose turn would swing an end into a wall slides off the wall by the end's sweep, as a track
  pivots off-centre. An overlapping hull never moves or turns deeper into a wall.
- A wall that stops it head-on takes its speed with it. A glancing slide or the crowd's push costs none.

On a planned manoeuvre (a `Tight` leg, `PathLeg.Tight`) none of the reflexes above run. The vehicle follows the legs
in their gear: its pursuit aims 2 m ahead standing, up to 6 m at speed (`Locomotion.TightLookAhead`), plus the rear
axle's offset in reverse, and never past the next gear change (`PathLeg.Aim`). Wheels pick the rear axle's arc whose
circle through the hull centre also passes the aim, so the planned centre line is followed either way, and hold
their angle in the last metre before a gear change. It brakes to each gear change as to the goal
(`PathLeg.ToGearChange`), changes gear standing, creeps until its wheels are within 3 degrees of the angle it wants,
and keeps to 3 m/s forwards (`Locomotion.TightSpeed`) and its reverse speed backwards.

`BlockerAuthoring` makes remains such as a wreck solid: a capsule of radius and hull length along its forward that
never moves. It closes the land cells it covers, so paths go round it and `NavCapsule` keeps units out, and nothing
can be built on it. Water and decks stay, so ships pass under a wreck on a bridge. A unit caught in its cells as it
appears drives out, since its path first steps out to open ground and `NavCapsule` lets it move while its overlap
shrinks. A `Decay` wreck stops blocking once it starts to be buried, and its cells open again.

## Air

`FlightAuthoring` makes a unit an aircraft with an altitude and a loiter radius, flown by `FlightMovementSystem`. A radius of 0 hovers. Its optional
`releasePoint`, a child such as a ramp or bomb bay, is where airdrop crates and strike bombs leave it.
`AirfieldAuthoring` gives a building landing pads, one aircraft each, each a `PadKind`: `Helipad`, `Shelter` or
`Any`. `FlightAuthoring.padKind` says which pads an aircraft takes, and `Any` on either side matches every kind. An
airfield only builds a pad-using aircraft when a pad of its kind is free, and Return to Base only claims one. An aircraft flies home once every weapon with limited rounds (its `Ammo` and mounts with
rounds) is empty, so a gunship with an unlimited gun still returns when its missiles are spent. It is sent home
once, so a player order given after that holds. It reloads while docked. Its remains fall to the ground under gravity. Fuel is left to game code.

## Transport

`ContainerAuthoring` lets a vehicle or building carry units with `PassengerAuthoring`. It sets the capacity, the
largest passenger and whether passengers fire out or survive its death. Passengers step out at its `exit`, such as
a bunker's back door, or around it when it has none. They get out or die in the frame the container dies, and dying
passengers credit its killer.

A passenger is hidden and takes no orders. A neutral garrison building belongs to whoever is inside and turns
neutral when empty.
