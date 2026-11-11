# Economy modules

[Docs](../README.md) ▸ [Simulation](README.md) ▸ Resources, Buildings, Power, Production, Capture, Airdrops

How players earn and spend resources, put up buildings and train units.

## Resources

Players spend resources to build and train. Each resource is a `ResourceType` asset with a name, colour and icon.

| Authoring | Earns by |
| --- | --- |
| `HarvesterAuthoring` | Gathering from a `ResourceNodeAuthoring` and delivering to a `ResourceDropOffAuthoring` |
| `ResourceIncomeAuthoring` | Steady income while owned and online, plus a first-owner bonus |
| `CasualtyIncomeAuthoring` | A share of each dying unit's cost |
| `ReserveRefillAuthoring` | Refilling the owner's finite reserves every interval |
| `DeliverySourceAuthoring` | Carriers that drive in from the map edge and pay on arrival (a killed carrier pays nothing) |
| `AirdropReceiverAuthoring` | A crate dropped from an aircraft every interval, see [airdrops](#airdrops) |

Nodes are finite or regrow, and harvesters move on when one runs dry. Steady income, refills and casualty income scale
with the player's `Income` stat. `ResourceStagesAuthoring` on a node swaps child models by the share of its starting
amount left, and steps back up as a renewable node regrows.

A harvester works a node or drop-off from any side. `TargetReach.Approach` sends it to the nearest reachable point on
the footprint's edge, and it works once its hull touches the edge, whichever way it faces. A full harvester picks one
drop-off per trip (`HarvestState.DropOff`), the one with the smallest edge gap plus the turn needed to face it, and
keeps it until it delivers or the drop-off is lost, unfinished or changes owner, so a truck never flips between two.

## Airdrops

`AirdropReceiverAuthoring` on a building sends a crate every `interval` while the building is complete, owned and not
`Offline`, so low power pauses the timer. Cap it at one per player with `BuildLimitAuthoring`.

- The `aircraft`, an unarmed unit with `FlightAuthoring`, spawns on the map edge nearest its owner's `PlayerSpawn`,
  flies a straight `DropRoute` over the building and on past the far edge, then despawns.
- It is a normal air unit, so enemy anti-air can shoot it down; the delivery is then lost. If the building dies or
  changes owner first, the aircraft turns away without dropping.
- When its release point (`FlightAuthoring.releasePoint`) passes over the `landingPoint` (the building's origin
  without one), it spawns the `crate`, a ghost prefab of the cargo under parachutes. The crate pays the owner
  `amount` of `type`, scaled by `Income`, when it lands.
- Around the release, `DropRun.Active` replicates, so every client opens the aircraft's [doors](../presentation/animation.md#machines).

## Power

A building with positive **Power** generates it, negative draws it. While demand exceeds supply, consumers go
offline: weapons hold fire, detectors go blind and production slows to `MatchRules.LowPowerProductionRate`.
`Offline` replicates, so every client can stop machines and dim [lights](../presentation/rendering.md#building-lights) with it.

## Buildings

`BuildingAuthoring` sets the footprint, population, power and whether it starts under construction. A unit with
`BuilderAuthoring` places sites, builds them up and repairs; its options sit on the command card as
[command specs](../presentation/hud.md#hud). A squad with `BuilderAuthoring` builds at its rate for each living member. Selling
refunds `MatchRules.SellRefund`. `FlagMountAuthoring` flies the owner's flag.

`BuildingAuthoring.openFootprint` makes the `NavObstacle` open: placement keeps the box clear but units cross it. A
`Destructible` crush rating still closes it to vehicles that cannot crush that much (tank traps); none leaves it open
(trenches). `TrenchAuthoring` adds a client-side ditch.

### Placement

`BuildingSpot` (centre, yaw, unturned footprint, surface) is the one placement value for the ghost, the server and the
AI. `BuildingSpot.IsValid` decides where it may go: inside the map, with every cell under the turned box free on its
surface in `NavGrid.Unbuilt`, the cells as ground, areas and blockers leave them before obstacle footprints close
them. Obstacles (buildings, solid ruins, scenery with a `block` size such as walls) are checked as exact boxes by
`BuildingSpot.Fits` instead, so neighbours set flush never trip on a cell both of them touch. Open scenery (trees,
shrubs, crushable fences, small props) doesn't block. A hostile ground or naval unit reaching into the box rules the
spot out too; the ghost counts only enemies the player sees, the server counts hidden ones as well.

The server's `SiteClearingSystem` marks open scenery under a site's turned box as cleared (trees by their canopy),
and `DeathSystem` removes it silently. Multiplayer sends this as a `SceneryChange` with `Cleared` set.

`MapSettings.BuildingGap`, baked from `MatchAuthoring.buildingGap` (0 turns it off), keeps lanes vehicles can drive:
`BuildingSpot.Fits` fails a spot that overlaps a `PlacedFootprint` or sits closer than the gap to one it doesn't
touch. Line pieces keep no gap from each other: walls are barriers by design, and their bends would fail it.
`BuildingSpot.CanPlace` joins `Fits` with `IsValid` for the server (`PlaceBuildingSystem`) and the ghost alike. The
ghost turns red; under fog the server's answer wins. The skirmish AI pads each spot by `AITuning.buildingGap`.
`BuildingSpot.Snap` lands a quarter-turn yaw on cell boundaries and centres other yaws on the half-cell lattice.

### Facing

A placed building's front is its prefab's +Z: doors, the producer's `spawnOffset` and the units it releases. Each
placement starts facing the match camera (`PlacementMath.CameraYaw`). `PlacementRotation` owns the ghost's yaw:
dragging after the click aims the front at the cursor, holding the rebindable `RotateBuilding` action (R) turns it with
sideways mouse motion, a tap of R adds 45 degrees, and Shift rounds turns to 15 degrees and keeps the yaw for the next
copy. `PlaceBuilding` carries the yaw in `PlayerCommand.Yaw`, and the server refuses a non-finite one. Map buildings
keep their authored rotation. A start kit item turns with its spawn unless it sets `facesCamera`.

### Line placement

A building with `placedInLines` set, such as a wall, trench or wire, is drawn as a wall of legs at any angle. A click
starts it, each further click lays the leg to the cursor and turns a corner there, and Command (right-click) or Escape
finishes the wall and leaves placement on for the next. Shift snaps a leg to 15 degrees. `PlacementInputSystem` drives it, `PlacementHint` shows the keys and `LineCost` totals the leg's
valid segments.

`PlacementLine` lays `BuildingSpot`s along a leg, each one footprint long. A wall's first leg snaps its first segment
to the grid; each later leg is a `PlaceLine` command with `LineStart.Joined` in its `Argument`, starting exactly at
`PlacementLine.JoinedStart`: on the line through the last leg's end, just far enough along to clear its last segment,
so the corner touches and stays closed. The server rebuilds each leg and places every free segment as its own site,
charged per segment, until the player can't afford one or a `BuildLimit` refuses. Legs after the first queue, ordered
from where the builders' earlier orders leave them.

A builder that finishes a line piece with nothing queued goes on to the nearest unfinished piece of the same wall,
the owner's line pieces joined edge to edge (`ConstructionSystem`), so a Build order on any one segment builds the
whole wall. The skirmish AI places lines only through `AIFieldWorkAuthoring`.

### Dressing

- `ModelVariantsAuthoring` lists variant child models. One shows, picked by a hash of the position, so a wall doesn't
  repeat a panel and every client agrees. A variant lists all its children, damage stage models included.
- `GarrisonPartAuthoring` on a mesh under a container draws it only while one of its `shownFor` passengers is aboard,
  such as a bunker's machine gun. `GarrisonPartRule` flags one with no container or no passengers.
- `ConstructionSiteAuthoring` dresses a building while it is built: a `site` child (fencing, crane) shown only under
  construction, a work rig streaming from `Work_01` onward empties while progress rises, and a complete effect that
  plays once for a client that watched the building finish. Pieces rising one by one are the
  [build reveal](../presentation/rendering.md#build-reveal).

## Production

`ProducerAuthoring` gives a building a training and research queue with a rally point. Units, buildings and research
share `ProducibleAuthoring`: build time and population.

- A `Prerequisite` is met by a finished building or research.
- `PurchaseRules` is the one buy rule, shared by orders, placement, the AI and the HUD.
- `BuildLimitAuthoring` caps how many of a type a player may have, counting sites and queued items.
- A unit trained with a rally point set spawns with a `RallyOrder`, which `RallyOrderSystem` turns into a Move.
- Every queued prefab needs `Producible` and every player `Population`.
- When an entity changes owner, its queue is refunded to the old owner.
- `Producer.Released` replicates, which opens the [production doors](../presentation/animation.md#machines).

## Capture

Units with `CapturerAuthoring` take over buildings with `CapturableAuthoring` after `captureTime` seconds in reach.
A capturer can be used up by it. A garrisoned building can't be captured.
