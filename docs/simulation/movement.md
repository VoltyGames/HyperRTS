# Movement modules

[Docs](../README.md) ▸ [Simulation](README.md) ▸ Navigation, Movement, Air, Transport

Pathfinding, how units follow paths and share space, aircraft and containers. Vehicle handling is in [driving](driving.md).

## Navigation

Units find paths on a grid over the map. Each cell is land, water, deck or blocked. Each unit moves on one layer:
Ground, Naval, Amphibious or Air. To move a unit, set and enable its `MoveDestination`.

The code is split by folder, namespace `HyperRTS.Simulation.Navigation.<Folder>`: `Grid`, `Areas`, `Fields`, `Sectors`,
`Paths`, `Agents`, `Drive`, `Reach`, `Spatial` and `Heights`.

| Authoring | Does |
| --- | --- |
| `TerrainHeightAuthoring` | A baked heightfield. Without one the ground is flat |
| `NavObstacleAuthoring` | Blocks a box. Buildings block their footprint |
| `NavAreaAuthoring` | Marks a box as water, blocked, or a bridge deck with water below |

- **Grid.** Flooded cells become water and steep ones blocked. Obstacles restamp the grid only when their footprints
  change, and each restamp records the changed box (`NavChanges`). A path re-plans only when one of its legs passes
  near a changed box; a path whose goal was snapped re-plans on any change.
- **Movement class.** Each unit has a `MovementClass`: its layer, scenery crush limit and exact radius.
  `NavFieldSystem` registers the class of every `NavAgent` prefab and unit, up to 16 per match.
- **Distance field.** `NavField` holds the signed distance in metres from each cell centre to the nearest wall, per
  class: positive in the open, negative inside buildings. It is rebuilt per 16 by 16 cell sector, only for sectors a
  change touched. A cell is open to a class when the field there is at least its radius (`NavAccess`). A*, the
  nearest-cell search and line of sight all use that rule.
- **Sector graph.** Each class has a sector graph (`NavSectors`) of connected open components, rebuilt with the
  field, giving region ids. A goal the start's region can't reach snaps to the nearest open cell it can, so a click
  across a river stops on this bank. `NavGrid.IsReachable(agent, from, to)` asks the same of any two points.
- **Start snap.** A unit in a closed cell starts its path at the nearest open cell it can squeeze to
  (`NavAccess.TryFindStart`), never through water or a wall, so a unit inside a new footprint gets out.
- **Path query.** `GridPathfinder.Prepare` snaps the ends and goes straight when the goal is in sight. Otherwise an
  abstract search over the sector graph picks a corridor and Burst A* runs inside it (`NavCorridor`). It always
  succeeds, so no path is partial. Steps cost more near walls by the agent's `WallMargin`, so paths keep room off walls
  where there is room.
- **Budget.** `PathPlanner` takes requests oldest first until a per-frame expansion budget is spent. The rest wait a
  frame while their units brake and turn toward the goal. `PathStats` counts searches by `PathCause`.
- **Chases.** A goal set by `MoveDestination.MoveTo`, as chases, walk-ups and escorts do, is a chase
  (`MoveDestination.Chase`). When it moves, the path's last waypoint follows it if still in sight, else the unit keeps
  its path and searches again at most every 0.5 s. A new order always plans at once.
- **Formations.** A formation move's members of one class share a flow field (`GroupFields`) from the centre of their
  slots. Each member traces it downhill and is pulled tight with line of sight, so it still gets ordinary waypoints.
  A member alone, or one whose slot is out of sight or far from the centre, searches by itself, as do chases.
- **Capsule.** Movement holds the whole capsule off walls (`NavCapsule`). A unit already overlapping may move as long
  as the overlap doesn't grow, so it can always back off. A move into a wall slides along it, so rotated buildings
  never snag it. Line of sight walks every cell the line crosses (`GridRay`).
- **Scenery.** Destructible scenery with a crush rating (trees, fences) marks its cell on a scenery layer. A unit
  drives through scenery up to its `SceneryCrushLimit`: its `Crusher` size, or all of it with **Walks Through
  Scenery** (infantry). Higher-rated scenery blocks like a closed cell. Felled scenery reopens its cell without
  invalidating paths.
- **Shared surfaces.** Layers that share a surface share cells, so ships pass under a bridge that tanks cross.
- **Target reach.** `TargetReach` walks a unit up to a target to build, repair, capture, board or gather. It stands
  on open ground a hull's length off the footprint, so it is in reach at any heading. `StandSpotSearch` tries spots
  round the footprint and takes the cheapest to drive to from the unit's pose: the distance plus the turn onto it,
  backing on a short way behind. A spot past the footprint costs the way round it, and one inside the turning
  circle, which no arc reaches, a full circle. Units sharing a target pass `ReachClaims`, the spots the others hold
  (harvesters do, `GatherClaimJob`): spots stay a hull's length and width apart, so units spread round the target.
  A goal another unit came to hold, or one a push left inside the turning circle, is chosen again. With every spot
  held a unit waits two spacings off the footprint, clear of where a unit leaving its spot backs to.
  A building's footprint is its `NavObstacle`; a unit's is the box round its `NavAgent` hull. The job hands it a
  `MoveControl`: destination, enabled bit and `Arrival` bit, written together. A walk-up whose locomotion ends short
  of the target is `ApproachState.Stuck`, and the order finishes; a gather order walks up again instead. A target
  that moved off is walked to again. A new order clears the last `Arrival`.
- **Spatial index.** `SpatialIndex` answers range queries over living entities from a grid of 8 m cells rebuilt each
  frame, meeting neighbours in the same order every run. Entries wider than a cell go in a short `Oversized` list.

## Movement

`UnitAuthoring` sets speed, radius, hull length, spacing, nav layer, whether it walks through scenery, and population.
Units follow their path and stop on arrival. A unit with a hull length is a capsule that long along its forward and two
radii wide; 0 keeps a circle.

A moving unit steers by sampling (`Steering`): it scores candidate velocities against the nearest 8 units in reach
(`CrowdVisitor`) and the wall field, and picks the cheapest.

- **Candidates.** A vehicle tries speeds and headings its handling reaches within half a second, including a crawl.
  Avoidance never changes its gear. An instant unit tries directions,
  speeds and its last choice. Both can wait.
- **Cost.** Predicted footprint overlap over the next 1.5 s, sooner counting more, with moving neighbours scored
  RVO-style, plus the wall term and small penalties for straying, standing still and swerving left, so a head-on pair
  both keep right.
- **Skipping.** The planned velocity is taken without sampling when it hits nothing. Parked units never steer. A
  three-point turn or a back-off skips steering, yet stands if driving on would run deeper into a
  neighbour than standing.

The chosen velocity goes to `Locomotion` as the heading and speed to drive ([driving](driving.md)). Layers that can't
block each other ignore each other: aircraft meet only aircraft, and only those near their own height. The same query feeds the overlap push, which moves neighbours
apart along the line between their closest points until the larger of their spacings lies between them. Hulls never
meet: each unit's whole move closes on a neighbour by at most half the room between hulls (`HullClearance`). Each
formation slot is sized by its own unit's footprint, and a slot in a building or off the unit's ground moves to the
nearest spot the unit fits and can reach.

`RightOfWay` answers every "who moves" question. Allies are teammates (`OwnerRelations`), and `UnitAuthoring.mass`,
in tonnes, sets each unit's weight.

- **Giving way.** Two movers share the avoiding unless they go one way; then the one further back gives way and the
  one ahead shares only the inner push, so an abreast pair at a one-lane bridge never waits on each other. A mover
  that another outweighs by half again takes all the avoiding on itself. Allied vehicles crossing paths or meeting
  head-on choose one winner: a substantially heavier vehicle goes first, otherwise entity index breaks the tie. The
  other takes all the avoiding, while hull clearance still prevents contact for both.
- **Idle units** step aside for an allied mover of their own kind and stay where pushed.
- **Foot units and vehicles.** A vehicle is a unit with `LocomotionAuthoring` (`NavAgent.IsVehicle`); every other
  ground unit is on foot. The push never moves a vehicle off a foot unit: the foot unit takes the whole overlap.
  A foot unit always gives way to an allied vehicle, whatever its order or stance, and steps out of a moving one's
  lane (its hull's width, out to its braking distance and a few metres more) before the vehicle reaches it. Only
  foot units in spatial cells the index marks near a moving vehicle (`SpatialIndex.IsNearVehicleLane`) look that far,
  so an idle crowd queries no more than its own neighbours. The
  vehicle slows to `RightOfWay.WalkingPace` while one stands in its lane and still stops at its hull. Held 2 s by
  allied foot units alone, it drives through them at a walk until it is clear of them.
- **Busy units.** A parked unit with an order, a target or Hold Position and an enemy hold their ground: to the
  mover they are fixed obstacles, so a truck never shoves a dozer that is building. A unit standing on the mover's
  goal and an arrived groupmate are not avoided. A landed aircraft is never pushed, and aircraft in flight are well
  over it.
- **Sunk units.** A unit sunk in walls, as when a building or wreck lands on it, drives out at its own top speed
  (`NavCapsule.WayOut`).
- **Groups.** A formation move gives its units one group (`MoveDestination.Group`) and the span of its slots
  (`Extent`). A unit near its slot touching an arrived groupmate toward its goal has arrived too, so arrival passes
  through a crowded group by contact. A groupmate that stopped that way (`ArrivalReason.AtGroupmate`) doesn't count,
  so a group crossing a bridge never stops in a chain onto the deck.
- **Arrival.** A move ends at the goal, against an arrived groupmate, or when the unit stops getting on.
  `MoveDestination` is then disabled and `Arrival` enabled, its `Reason` saying which, and `MoveOrderSystem` finishes
  its order. If anything else halts the unit, it points the unit at the goal again.
- **Stuck.** `MoveProgress` measures improvement in the shortest remaining path distance. Turning earns a bounded
  allowance for pivots and manoeuvres, without replacing that distance record. Circling cannot renew the allowance;
  only getting closer does. Without progress the whole path is planned again, with waits doubling. Near the path's end the unit gives up and
  the goal is taken; a reversing vehicle first backs out (`MoveProgress.TryTurnOut`). Farther out a jam is traffic, so
  a column queued at a bridge keeps its order.

`BlockerAuthoring` makes remains such as a wreck solid: a capsule that never moves. It closes the land cells it
covers, so paths go round it and nothing can be built on it. Water and decks stay, so ships pass under a wreck on a
bridge. A unit caught in its cells as it appears drives out. A `Decay` wreck stops blocking once it starts to be
buried.

## Air

`FlightAuthoring` makes a unit an aircraft with an altitude and a loiter radius, flown by `FlightMovementSystem`. A
radius of 0 hovers. Its optional `releasePoint`, a child such as a ramp or bomb bay, is where airdrop crates and strike
bombs leave it.

`AirfieldAuthoring` gives a building landing pads, one aircraft each, each a `PadKind`: `Helipad`, `Shelter` or `Any`.
`FlightAuthoring.padKind` says which pads an aircraft takes, and `Any` on either side matches every kind. An airfield
only builds a pad-using aircraft when a pad of its kind is free, and Return to Base only claims one. An aircraft flies
home once every weapon with limited rounds is empty, once, so a player order given after that holds. It reloads while
docked. Its remains fall to the ground under gravity. Fuel is left to game code.

## Transport

`ContainerAuthoring` lets a vehicle or building carry units with `PassengerAuthoring`. It sets the capacity, the
largest passenger and whether passengers fire out or survive its death. Passengers step out at its `exit`, or around it
when it has none. They get out or die in the frame the container dies, and dying passengers credit its killer.

A passenger boards once in reach of a container on the ground. One in the air is called down first
(`BoardingCall`): an aircraft that hovers and stands still lands for a waiting passenger and climbs back once no
one waits. An aircraft on the move, or one that can't hover, is not boarded in flight.

A container may list `firingPorts`, child transforms at windows and loopholes with the blue axis pointing out. A
passenger that fires from inside shoots from the port facing its target best: the Fire cue, projectile start and hit
origin all sit there. Without ports it fires from the centre. `ContainerRule` warns about ports that are empty or
outside the container.

A container with `protection` below 1 shelters its passengers rather than shutting them off. Each hit on it also
reaches them, shared evenly, as the hit times 1 minus `protection` times its health fraction before the hit, times the
damage type's `garrisonFactor`. A battered building protects less, and blast or fire beats a garrison faster than
small arms. The share is a hit of its own in the next frame, so each passenger's armor applies and kill credit goes to
the shooter. Weapons, abilities and strategic strikes carry the factor; field and status damage never reach a
garrison. Passengers killed inside free their slots. At the default `protection` of 1, as on transports, nothing gets
through.

A passenger is hidden and takes no orders. A neutral garrison building belongs to whoever is inside and turns neutral
when empty.
