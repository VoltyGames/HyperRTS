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
- Over the building it spawns the `crate`, a ghost prefab of the cargo under parachutes, which sinks at `fallSpeed`
  and pays the owner `amount` of `type`, scaled by `Income`, when it lands.

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

### Facing

A placed building's front is its prefab's +Z: doors, the producer's `spawnOffset` and the units it releases. By
default it faces the match camera, toward -Z. While placing, the rebindable `RotateBuilding` action (R) turns the
ghost a quarter clockwise per press, a low mark off the ghost shows its front, and an odd turn swaps the footprint's X and Z. `PlaceBuilding` carries the turn,
0 to 3, in `PlayerCommand.Turn`. The server refuses any other value and places at `PlacementMath.Facing`. Children,
work points and the spawn point turn with the building. The skirmish AI places at turn 0, line placement keeps its
own turn, map buildings keep their authored rotation, and a start kit item turns with its spawn unless it sets
`facesCamera`, which lays its offset and yaw out facing the camera as a placed building is.

### Line placement

A building with `placedInLines` set, such as a wall, is placed by dragging: press at the start, drag, release to
confirm. `PlacementLine` lays segments along the nearer grid axis, X or Z, each one footprint X long. A line along Z
turns each segment a quarter, so its footprint swaps. Diagonal lines are not supported yet. The ghost draws every
segment, valid or not by the same `PlacementMath` rules, and `PlacementState.ValidSegments` gives the HUD's `LineCost` the
line's total cost.

The command is `PlaceLine`, sent as a start, an end and a prefab. `PlaceBuildingSystem` rebuilds the same line and
places each free segment from the start as its own construction site, charged per segment. It skips blocked
segments and stops at the first one the player can't afford or its `BuildLimit` refuses. The builders get Build
orders on the sites from the end nearer them, the rest shift-queued. The skirmish AI skips line buildings for now.

`ModelVariantsAuthoring` lists variant child models. Presentation shows one, picked by a hash of the position, so a
wall doesn't repeat one panel and every client shows the same. A variant lists all its children, damage stage
models included.

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
