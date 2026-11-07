# Vehicle driving

[Docs](../README.md) ▸ [Simulation](README.md) ▸ Locomotion, Chassis, Drive planner

How vehicles move as vehicles: handling, wheels and tracks, and the planner that makes their paths drivable. Paths,
steering and arrival are in [movement](movement.md).

## Locomotion

Every unit follows its path through `Locomotion`. Without its own, a unit gets `Locomotion.Instant`: full speed and
any facing at once, never reversing, as infantry do. `LocomotionAuthoring` gives a ground vehicle handling, and its
`LocomotionState` keeps its speed between frames. A `Driver` bundles handling, chassis, radius, top speed and state and
drives each leg: it turns the facing and returns the hull's move. The crowd's push never turns the hull. A parked unit
with an enabled `FacingGoal`, as a hull-fixed weapon sets, turns toward it standing, wheeled ones too.

| Field | Does |
| --- | --- |
| Acceleration, Braking | Speed gained and shed per second |
| Turn Rate | Fastest the hull yaws, in degrees per second: tracks turn at it, wheels hold it as their grip at speed |
| Steer Rate | How fast the steered wheels turn, in degrees per second |
| Reverse Speed | Top speed backing up |

`NavAgent.Chassis` says how the hull moves, for locomotion and the planner alike. `UnitAuthoring` bakes it from the
`LocomotionAuthoring` beside it:

| Field | Does |
| --- | --- |
| Wheelbase | Metres from the steering axles' midpoint to the rear axle; 0 for tracks |
| Rear Axle | Metres the rear axle (the fixed axles' midpoint) sits behind the hull centre; 0 for tracks |
| Turn Radius | Tightest circle of the rear axle. 0 turns at any speed |
| Pivots | Stops and turns in place when far off its heading, as tracked vehicles do |
| Reverse Run | Backs up to a goal behind it when the path left is at most this long and turning round would take longer; 0 never reverses |

A hull with a turn radius that doesn't pivot is wheeled. It aims its rear axle at a look-ahead point on the path and
steers an arc through it (pure pursuit), with the steer angle changing at most at the steer rate. The centre drifts
slightly inward in a turn. Tracks and instant units turn about the centre at the turn rate and move along it. The
Locomotion's **Fill from wheels** button measures wheelbase and rear axle from the model's `Wheel_L01`... parts, front
to back: the first Steering Axles axles steer and the rest make the rear axle. `LocomotionRule` checks a wheeled
unit has a wheelbase and both axles inside its hull.

Reflexes, for a unit off a planned manoeuvre:

- It cuts corners within its look-ahead once the next corner is in sight, slows to hold the arc to the next corner
  and brakes to stop at the goal.
- A target inside its turning circle can't be reached on any arc, so it makes a three-point turn rather than orbit.
- Wheels can't turn standing, so a leg that runs into a wall drives the other way for a moment
  (`LocomotionState.OtherWay`). This is a manoeuvre timer, not a stuck test.
- A hull whose turn would swing an end into a wall slides off it. An overlapping hull never moves or turns deeper in.
  A wall that stops it head-on takes its speed; a glancing slide costs none.

On a planned manoeuvre (a `Tight` leg, `PathLeg.Tight`) none of the reflexes run. The vehicle follows the legs in
their gear, aiming a short way ahead and never past the next gear change (`PathLeg.Aim`), brakes to each gear change
as to the goal, changes gear standing, and keeps to a slow tight speed (`Locomotion.TightSpeed`).

## Drive planner

`DrivePlanner` checks every fresh path of a unit with a turn radius or a hull longer than it is wide, in the same serve
job as the route. Easy paths run no search. A path with a stretch to search goes to one serial `DriveJob`, which spends
a fixed budget a frame (`PathfindingSystem.DriveBudget`). The first such path plans within it and leaves any stretch
past it as drawn (`PathState.PartlyPlanned`), planned once its planned part is driven. Later paths wait a frame.

- **Start and corners.** The planner sweeps a `DriveCurve` (the shortest forward turn, straight, turn, or a turn in
  place for tracks) from the unit's pose to a point along the path, and the same around each corner. A `DriveHull`
  checks the sweep against the wall field. When walls held the unit as it asked for a replan (`PathState.Stuck`), the
  start is always searched.
- **Search.** A blocked stretch runs a `DriveSearch`, Hybrid A* over the rear axle's pose in a window round the
  stretch, forwards and, within `ReverseRun`, backwards. A `DriveHeuristic` guides it with wall-aware distances, so
  routes the hull can turn on are preferred. A pose that overlaps walls is allowed only while the overlap shrinks, so
  a wedged start plans its way out. A search stops at a fixed expansion cap (`DriveSearch.MaxExpansions`); a failed
  stretch tries once more reaching further along the path, then stays as drawn.
- **Result.** The stretch becomes `PathWaypoint`s every 1.5 m, each `Tight`. `Reverse` marks a leg driven backwards, and
  a gear change is a waypoint where it flips. `PathStats.Drive` counts searches, expansions and failures.
- **Following.** `PathState.SkipCorners` never cuts a tight waypoint, and goal moves never move a tight last waypoint
  (a moved goal searches again).
- **Pose ends.** A move with `MoveDestination.Facing` set, as into a [dock bay](economy.md#docks), ends as a pose: the
  last stretch becomes a curve or search to the pose `RunIn` metres before the goal, then a tight straight run-in.
  `MoveDestination.ExitRun` starts a path with that many metres straight ahead.
- The Paths debug layer draws tight legs in orange, reverse legs dashed, gear changes as dots and each wheeled
  vehicle's rear axle.
