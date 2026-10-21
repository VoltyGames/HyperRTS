# Economy modules

[Modules](../modules.md) ▸ Resources, Buildings, Production, Capture

## Resources

Players spend resources to build and train. Each resource is a `ResourceType` asset with a name, colour and icon.

| Authoring | Earns by |
| --- | --- |
| `HarvesterAuthoring` | Gathering from a `ResourceNodeAuthoring` and delivering to a `ResourceDropOffAuthoring` |
| `ResourceIncomeAuthoring` | Steady income while owned and online, plus a first-owner bonus |
| `CasualtyIncomeAuthoring` | A share of each dying unit's cost |
| `ReserveRefillAuthoring` | Refilling the owner's finite reserves every interval |
| `DeliverySourceAuthoring` | Carriers that drive in from the map edge and pay on arrival |

`ResourceStagesAuthoring` on a node swaps child models by the share of its starting amount left, ordered from full
to nearly spent, and steps back up as a renewable node regrows. `ResourceStageSystem` shows them on the client.

Nodes are finite or regrow. Harvesters move on when a node runs dry. A killed delivery carrier pays nothing. Steady
income, refills and casualty income scale with the player's `Income` stat.

## Power

Part of `Buildings`. A building with positive **Power** generates it, negative draws it. While demand exceeds
supply, consumers go offline: weapons hold fire and production slows to `MatchRules.LowPowerProductionRate`.

## Buildings

`BuildingAuthoring` sets the footprint, population, power and whether it starts under construction. A unit with
`BuilderAuthoring` places sites and builds them up. Builders also repair. Selling refunds `MatchRules.SellRefund`.
`FlagMountAuthoring` flies the owner's flag.

## Production

`ProducerAuthoring` gives a building a training and research queue with a rally point. Units, buildings and research
share `ProducibleAuthoring`: build time and population.

- A `Prerequisite` is met by a finished building or research.
- `PurchaseRules` is the one buy rule, shared by orders, placement, the AI and the HUD.
- `BuildLimitAuthoring` caps how many of a type a player may have, counting sites and queued items.
- When an entity changes owner, its queue is refunded to the old owner.

## Capture

Units with `CapturerAuthoring` take over buildings with `CapturableAuthoring` after `captureTime` seconds in reach.
A capturer can be used up by it. A garrisoned building can't be captured.
