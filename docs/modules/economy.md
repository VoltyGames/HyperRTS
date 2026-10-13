# Economy modules

[Modules](../modules.md) ▸ Resources, Power, Buildings, Production, Capture

## Resources

| Type | Role |
| --- | --- |
| `ResourceType` (asset) | Name, colour, icon |
| `ResourceCost`, `ResourceStock`, `ResourceMath` | Price; player stockpile; arithmetic |
| `ResourceNodeAuthoring` / `ResourceNode` | Neutral deposit, finite or regrowing |
| `ResourceDropOffAuthoring`, `HarvesterAuthoring` | Where harvesters deposit; capacity, gather rate, trip state |
| `ResourceIncomeAuthoring` / `ResourceIncome` | Passive income for the owner (a derrick) plus a first-owner bonus; neutral or `Offline` pays nothing |
| `OccupantIncome`, `ResourceReserve` | Opt-ins: pay per `Cargo` passenger; pay out of a finite reserve |
| `ReserveRefillAuthoring` | Refills same-owner reserves of a type every interval |
| `CasualtyIncomeAuthoring` | Pays the owner a share of each dying unit's `ResourceCost` |

`GatherSystem` runs node → fill → drop-off → deposit and moves on when a node runs dry. `ResourceNodeSystem`
regrows or removes nodes. Refill, income and casualty income count into `PlayerStats` and scale by `Stat.Income`.

**Delivery.** `DeliverySourceAuthoring` on a building sends `Count` carriers (an unarmed unit prefab: a truck, a
cargo plane) every `Interval` while owned and online. `DeliverySystem` spawns them on the map edge along the ray
from the centre through the building and moves them to it; a carrier in `ArrivalRange` pays `Amount` and is
destroyed. Killed carriers pay nothing; a lost source takes its carriers with it.

## Power

`BuildingAuthoring.power` adds a `PowerSupply` (+ generates, − draws). `PowerSystem` totals each `PowerGrid` and
marks consumers `Unpowered` while demand exceeds supply: weapons hold fire, producers slow to
`MatchRules.LowPowerProductionRate`. `OfflineSystem` then enables `Offline` while any reason holds.

## Buildings

| Type | Role |
| --- | --- |
| `BuildingAuthoring` | Footprint, population, power, starts under construction |
| `BuilderAuthoring` / `Builder`, `BuildOption` | Build rate and placeable prefabs |
| `BuildingPlacement`, `BuildingRules` | Surface the footprint needs; `IsAlliedSite`, `NeedsRepair`, `IsSellable` |
| `FlagMountAuthoring` / `FlagMount` | Flies the owner's flag: the flag's render child at the pole top and how far it drops when lowered |

`PlaceBuildingSystem` validates, charges and spawns a site; `ConstructionSystem` and `RepairSystem` run builders;
`SellSystem` refunds `MatchRules.SellRefund` (a `NeutralGarrison` is never sellable).

## Production

| Type | Role |
| --- | --- |
| `ProducerAuthoring` / `Producer`, `ProductionQueueItem`, `RallyPoint` | Training and research queue |
| `ProducibleAuthoring`, `Producible` | Base of unit, building and research authoring; build time and population |
| `Prerequisite`, `CompletedBuildings` | Required `TypeId`s, met by a finished building or research |
| `PurchaseRules` | The one buy rule (orders, placement, AI, HUD): `IsUnlocked`, `CanBuy`, `TryBuy` |
| `Research`, `CompletedResearch` | Researched once instead of spawned; the player's finished research (replicated by `TypeId`) |
| `BuildLimitAuthoring` / `BuildLimit` | Cap per player on a type, sites and queue included |

`ProductionCommandSystem` handles Produce, Cancel and rally points; `ProductionSystem` trains the queue head when
population allows. `OwnerChangeSystem` refunds a transferred entity's queue to the old owner and clears its orders,
rally point and selection.

## Capture

| Type | Role |
| --- | --- |
| `CapturableAuthoring` / `Capturable`, `CaptureProgress` | Taken over in `captureTime` seconds; progress replicates |
| `CapturerAuthoring` / `Capturer` | Unit that captures, optionally used up |
| `CaptureLookup` | `CanCapture`, shared by orders and `CaptureSystem` |

`CaptureSystem` advances progress while capturers are in reach, then transfers the owner. A garrisoned building
can't be captured.
