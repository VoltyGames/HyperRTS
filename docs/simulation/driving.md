# Vehicle driving

[Docs](../README.md) ▸ [Simulation](README.md) ▸ Locomotion, Chassis, reflexes

How vehicles move as vehicles: handling, wheels and tracks, and the reflexes that get them round corners and out of
walls. Paths, steering and arrival are in [movement](movement.md).

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

`NavAgent.Chassis` says how the hull moves, for locomotion and the path request alike. `UnitAuthoring` bakes it from the
`LocomotionAuthoring` beside it:

| Field | Does |
| --- | --- |
| Wheelbase | Metres from the steering axles' midpoint to the rear axle; 0 for tracks |
| Rear Axle | Metres the rear axle (the fixed axles' midpoint) sits behind the hull centre; 0 for tracks |
| Turn Radius | Tightest circle of the rear axle. 0 turns at any speed |
| Pivots | Stops and turns in place when far off its heading, as tracked vehicles do |
| Reverse Run | Longest reverse leg of a reflex; 0 never reverses, even on a reverse move |

A hull with a turn radius that doesn't pivot is wheeled. It aims its rear axle at a look-ahead point on the path and
steers an arc through it (pure pursuit), with the steer angle changing at most at the steer rate. The point is never
further than the look-ahead, even toward a goal in sight, as one arc to a far goal turns twice its bearing. The centre drifts
slightly inward in a turn. Tracks and instant units turn about the centre at the turn rate and move along it. The
Locomotion's **Fill from wheels** button measures wheelbase and rear axle from the model's `Wheel_L01`... parts, front
to back: the first Steering Axles axles steer and the rest make the rear axle. `LocomotionRule` checks a wheeled
unit has a wheelbase and both axles inside its hull.

Reflexes:

- It cuts corners within its look-ahead once the next corner is in sight, slows to hold the arc to the next corner
  and brakes to stop at the goal.
- A corner behind it, or inside its turning circle where no arc reaches, is turned onto in the other gear: it backs
  on full lock until the corner is round to its front, then drives on (a three-point turn), so it never drives a
  ring to turn round. With a unit behind it, it tries forwards for a moment.
- Wheels can't turn standing, so a leg that runs into a wall drives the other way for a moment
  (`LocomotionState.OtherWay`). This is a manoeuvre timer, not a stuck test.
- Tracks whose pivot the walls block, standing in a slot too narrow to turn, back straight out with their tail along
  the path until they can turn (`Driver.BackOutOfPivot`, which starts the same back-out as `Locomotion.TurnOut`).
- A hull whose turn would swing an end into a wall slides off it. An overlapping hull never moves or turns deeper in.
  A wall that stops it head-on takes its speed; a glancing slide costs none.

## Backing up

A vehicle with a reverse run drives a whole route backwards, front toward where it came from, in two cases:
`MoveDestination.Backward` is set, as the reverse move order (`CommandType.ReverseMove`, `OrderType.ReverseMove`,
trait `DrivesBackward`) does, or the route is at most three hull lengths and starts behind it, so a truck backs from a
drop-off to a field beside it rather than turning round. `PathRequest.DrivesBackward` decides when the route is
served. The route is driven as drawn, every waypoint marked `Reverse`, at the reverse speed with the rear axle
leading. A goal close behind is backed onto, never three-point turned. Units
without a reverse run, foot units and aircraft take a reverse move as a plain move.
