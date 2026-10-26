# Economy modules

[Modules](../modules.md) ▸ Resources, Buildings, Production, Capture, Airdrops

## Resources

Players spend resources to build and train. Each resource is a `ResourceType` asset with a name, colour and icon.

| Authoring | Earns by |
| --- | --- |
| `HarvesterAuthoring` | Gathering from a `ResourceNodeAuthoring` and delivering to a `ResourceDropOffAuthoring` |
| `ResourceIncomeAuthoring` | Steady income while owned and online, plus a first-owner bonus |
| `CasualtyIncomeAuthoring` | A share of each dying unit's cost |
| `ReserveRefillAuthoring` | Refilling the owner's finite reserves every interval |
| `DeliverySourceAuthoring` | Carriers that drive in from the map edge and pay on arrival |
| `AirdropReceiverAuthoring` | A crate dropped from an aircraft every interval, see [airdrops](#airdrops) |

`ResourceStagesAuthoring` on a node swaps child models by the share of its starting amount left, ordered from full
to nearly spent, and steps back up as a renewable node regrows. `ResourceStageSystem` shows them on the client.

Nodes are finite or regrow. Harvesters move on when a node runs dry. A killed delivery carrier pays nothing. Steady
income, refills and casualty income scale with the player's `Income` stat.

## Airdrops

`AirdropReceiverAuthoring` on a building gets a crate every `interval` while it is complete, owned and not
`Offline`, so low power pauses the timer. Cap it at one per player with `BuildLimitAuthoring`.

- The `aircraft`, an unarmed unit with `FlightAuthoring`, spawns on the map edge nearest its owner's `PlayerSpawn`
  at `altitude` above the building, flies a straight `DropRoute` over it, then on past the far edge and despawns.
- It is a normal air unit, so enemy anti-air under its path can shoot it down. Killed before the drop, the delivery
  is lost. If the building dies or changes owner first, the aircraft turns away without dropping.
- When its release point, the aircraft's `FlightAuthoring.releasePoint` or its origin without one, comes over the
  `landingPoint`, or the building's origin without one, it spawns the `crate` there: a ghost prefab of the cargo under
  parachutes, which sinks at `fallSpeed` and pays the owner `amount` of `type`, scaled by `Income`, when it lands.
- From `DropRun.LeadSeconds` (3 s) of flight before the release until as long after, the aircraft's replicated
  `DropRun.Active` is set, so every client opens its [doors](support.md#animation) for the run.

Defaults for the balance pass: a truck carries 100 a trip, about 240 a minute on a worked field. The design asks
40 to 60% of that, so a receiver pays 180 every 90 s (120 a minute), with a 30 m cruise and a 6 m/s fall.

## Power

Part of `Buildings`. A building with positive **Power** generates it, negative draws it. While demand exceeds
supply, consumers go offline: weapons hold fire, detectors go blind and production slows to `MatchRules.LowPowerProductionRate`.
`Offline` replicates, so every client can stop machines and dim [lights](client.md#building-lights) with it.

## Buildings

`BuildingAuthoring` sets the footprint, population, power and whether it starts under construction. A unit with
`BuilderAuthoring` places sites and builds them up; its `advancedBuildOptions` go on the command card's second page.
Builders also repair. Selling refunds `MatchRules.SellRefund`.
`FlagMountAuthoring` flies the owner's flag.

`BuildingSpot` (centre, yaw, unturned footprint, surface) is the one placement value for the ghost, the server and
the AI. `BuildingSpot.IsValid` decides where it may go: inside the map, with every cell under the turned box free on
its surface, never a blocked cell. Anything with an obstacle blocks: buildings, map buildings, solid ruins and scenery
with a `block` size such as walls. Open scenery (trees, shrubs, crushable fences and pipes, small props) doesn't.
A hostile ground or naval unit whose capsule reaches into the box (`BuildingSpot.IsBlockedByEnemies`, through the
`SpatialIndex`) rules the spot out too, and friendly units drive off by themselves. The ghost (`SeenEnemies`) counts
only enemies the player sees. The server counts hidden ones as well, and that order fails like any invalid spot.
On the server, `SiteClearingSystem` marks `SceneryCleared` every piece of open scenery whose circle overlaps a site's
turned box, so a fence crossing the edge goes too, every tree whose crown (`Topple.Crown`, baked from its meshes) reaches
over it, and every open scenery remains lying on it. Scenery and sites never move, so scenery is checked again only
when a site or a piece of scenery appears; remains are checked every frame. `DeathSystem` destroys
cleared pieces the next frame with no remains, no `Dead` and so no cues. Multiplayer sends the clearing as a
`SceneryChange` with `Cleared` set, and clients remove the piece, or the remains they spawned for it, the same way.
The server also refuses overlap with standing obstacles and sites placed the same
frame, comparing turned boxes (`NavBox.Overlaps`), so neighbours at any angle may touch.

`BuildingSpot.Snap` keeps a yaw within 0.5° of a quarter turn exact and lands the turned edges on cell boundaries. At
any other yaw it centres the spot on the half-cell lattice.

### Facing

A placed building's front is its prefab's +Z: doors, the producer's `spawnOffset` and the units it releases. Each
placement starts facing the match camera, toward -Z (`PlacementMath.CameraYaw`). `PlacementRotation` owns the ghost's
yaw while placing:

- Holding the click pins the ghost, and dragging more than 2 m aims its front at the cursor, at any angle.
- Holding the rebindable `RotateBuilding` action (R) pins it while sideways mouse motion turns it, 0.5° a pixel.
  Edge scrolling pauses meanwhile through `ScrollEdge.Paused`.
- A tap of R, released with less than 3 px of motion, adds 45°.
- Shift, the `Queue` action, rounds every turn to 15° and keeps the yaw for the next copy.

The mouse wheel stays camera zoom, and the HUD's `PlacementHint` says how to turn. `PlaceBuilding` carries the yaw in
radians in `PlayerCommand.Yaw`. The server refuses a non-finite yaw. Children, work points and the spawn point turn
with the building. The skirmish AI tries the camera facing, then a quarter turn. Map buildings keep their authored
rotation, and a start kit item turns with its spawn unless it sets `facesCamera`, which lays its offset and yaw out
facing the camera as a placed building is.

### Line placement

A building with `placedInLines` set, such as a wall, is placed by dragging: press at the start, drag, release to
confirm. `PlacementLine` lays `BuildingSpot`s in any drag direction, each one footprint X long and yawed along the
line with its front toward the camera. Only the first spot snaps, so the rest follow gap-free. Shift rounds the drag
direction to 15°. The ghost draws every segment, valid or not by the same rules, and the HUD's `LineCost` counts the
valid ones for the line's total cost.

The command is `PlaceLine`, sent as a start, an end and a prefab. `PlaceBuildingSystem` rebuilds the same line and
places each free segment from the start as its own construction site, charged per segment. It skips blocked
segments and stops at the first one the player can't afford or its `BuildLimit` refuses. The builders get Build
orders on the sites from the end nearer them, the rest shift-queued. The skirmish AI skips line buildings for now.

`ModelVariantsAuthoring` lists variant child models. Presentation shows one, picked by a hash of the position, so a
wall doesn't repeat one panel and every client shows the same. A variant lists all its children, damage stage
models included.

`GarrisonPartAuthoring` on a mesh under a container draws it only while one of its `shownFor` passengers is aboard,
read from the replicated cargo, such as the machine gun a bunker shows for a rifle squad. `GarrisonPartRule` flags
one with no container above it or no passengers.

`ConstructionSiteAuthoring` dresses a building while it is built. Its `site` child (fencing, crane, pallets, work
lights) shows only under construction. Its work rig streams from the site's `Work_01` onward empties while progress
rose within the last half second, so it stops when the builders leave. Its complete effect plays once when a client
sees a construction it watched finish; a building first seen complete plays nothing. The owner already hears
`AnnouncementKind.ConstructionComplete`. Pieces rising one by one are the [build reveal](client.md#build-reveal).

## Production

`ProducerAuthoring` gives a building a training and research queue with a rally point. Units, buildings and research
share `ProducibleAuthoring`: build time and population.

- A `Prerequisite` is met by a finished building or research.
- `PurchaseRules` is the one buy rule, shared by orders, placement, the AI and the HUD.
- `BuildLimitAuthoring` caps how many of a type a player may have, counting sites and queued items.
- A unit trained with a rally point set spawns with a `RallyOrder`, which `RallyOrderSystem` issues as a Move
  through `OrderWriter` the next frame.
- Every queued prefab needs `Producible` and every player `Population`.
- When an entity changes owner, its queue is refunded to the old owner.
- `Producer.Released` counts the units a producer has released and replicates to every client, which opens its
  [production doors](support.md#animation).

## Capture

Units with `CapturerAuthoring` take over buildings with `CapturableAuthoring` after `captureTime` seconds in reach.
A capturer can be used up by it. A garrisoned building can't be captured.
