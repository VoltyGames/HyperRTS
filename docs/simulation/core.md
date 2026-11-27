# Core modules

[Docs](../README.md) ▸ [Simulation](README.md) ▸ Common, Stats, GameEntities, MatchStart, Match, Orders, Commands, Selection

The modules every other module builds on: who owns what, how a match is set up, and how player intent becomes unit
orders.

## Common

The base tier: ownership, identity, health and shared helpers.

| Type | Role |
| --- | --- |
| `Owner` | Owner of an entity. 0 is neutral, the maximum is 29 |
| `OwnerRelations` | Singleton with each owner's team. Answers `IsHostile`, `IsAllied` and `GetRelation` |
| `Player`, `PlayerRegistry` | A player's owner, name and colour. The registry maps owner to player |
| `PlayerSide` | The game's army or nation id. Picks the player's start kit and AI build order |
| `LocalPlayer`, `Defeated` | The player this client controls. `Defeated` is enabled once a player loses |
| `PlayerStats` | Replicated totals: trained, built, delivered, lost, destroyed |
| `MapSettings` | Singleton: playable bounds, cell sizes, water level, max slope |
| `CellGrid` | The layout every map grid shares: `Min`, `CellSize`, `Size`, with `Cell`, `Center`, `Index`, `Contains` and `Clamp` |
| `CellRange`, `CellCircle`, `NeighbourOffsets`, `GridRay` | Burst-friendly enumerators over a box of cells, a circle, a cell's neighbours, and the cells a line crosses |
| `UnitTag`, `BuildingTag` | A movable unit. A static structure |
| `Health`, `Dead` | Hit points; alive is `Health.IsAlive`. `Dead` marks this frame's deaths, from `DeathSystem` until the end-of-frame destruction; never replicated |
| `Destructible` | Scenery anyone can wreck. Splash hits it and fog never hides it |
| `ConstructionProgress` | 0 to 1. An unfinished building doesn't produce, fire or count for prerequisites |
| `Offline` | Out of action, with a reason mask. Games add reasons from `OfflineReason.Custom` |
| `GameEvents` | This frame's unit produced, building completed, research completed, owner changed and damaged events |
| `OwnerChanged.Transfer` | The one way to change an owner. Raises `OwnerChanged` |
| `EntityInfo`, `PrefabRegistry` | Type id, name and icon. The registry maps type id to prefab |
| `EntityWriter` | Writes component sets, so bakers and tests build the same entities |
| `PrefabSpawner` | Instantiates a baked prefab from a job and sets its `Owner`. `SpawnExtensions` does the same on an `EntityManager` |
| `EventWriter<T>`, `EventQueues` | Appends to a per-frame queue singleton (damage, status, cues, game events) from a job |
| `IntervalTimer` | A repeating timer that holds while offline |
| `AuthoringBehaviour` | Base of all authoring components |

An entity's type id is a hash of its display name, so two prefab types must not share a name.

## Stats

The base tier: live stats with modifiers, in `Simulation/Stats/`. `Stat`, `StatModifier` and `StatModifiers` define the value
formula, and `BaseStat` holds a stat's unmodified value. `StatFieldAttribute` marks the component field a stat drives;
the recompute is in [Combat](combat.md#stats).

## GameEntities

`GameEntityAuthoring` is the base of unit and building authoring: name, icon, owner, health, vision, cost, build time,
prerequisites and remains. `TeamStripAuthoring` sets the height band where the owner's colour shows, for buildings
painted in strips.

## MatchStart

Orchestration that applies the `MatchSetup` to the baked players, spawns start kits and enables prespawned
ghosts in single player.

| Type | Role |
| --- | --- |
| `MatchSetupSystem` | Applies the lobby request at match start |
| `PlayerSetup` | The components of a player entity: stats, population, power, commands, research, stock |
| `SideContentSystem` | Spawns each side's start kit and gives it its AI build order |
| `LocalGhostActivationSystem` | Enables prespawned ghosts such as the Match in single player |

## Match

One `MatchAuthoring` per map sets its size, fog, player slots, sides, AI tuning, replays and join timeout, and
references one `InfantryTuning` asset (baking fails without it). Each slot has a name, team, colour, control type,
starting stock, spawn point and side. Slot index + 1 is the player's owner.

A side holds a start kit and an AI build order. At match start every player gets its side's kit, spawned around its
spawn point, and its build order. A side the Match doesn't list is an error.

| Type | Role |
| --- | --- |
| `MatchState` | Replicated phase, winning team, and `Started` once every human joined |
| `MatchRules` | Low-power production rate, sell refund, experience per cost, join timeout and whether strategic assets run. The Match bakes it, but it sits in `Common` since modules of every gameplay tier read it |
| `MatchSetup` | Lobby configuration. `NetworkSession` writes it into every world of the session before the map loads |
| `GameSpeed` | Single-player pause and speed, applied to `Time.timeScale` by `GameSpeedSystem`, which resets it when the local world goes |
| `VictoryCritical` | A player who loses all of these is defeated |
| `PopulationProvider`, `Population` | Population sources, and used and cap per player |

`MatchSetup` closes slots and removes what they own, sets names, colours, teams and sides, assigns AI, scales
starting stock, overrides fog and sets the single-player speed. It applies the same way on the server and every client. Its `Weather` is read by the
scene when it loads; leave it empty for the map's own weather.

Victory: a player who loses every `VictoryCritical` entity is defeated, and the match ends when one team is left. A
player who never owned one is never defeated by this.

## Orders

All player intent is a `PlayerCommand` on the player entity. Units carry orders.

| Type | Role |
| --- | --- |
| `PlayerCommand`, `CommandType` | Player intent: Smart, Move, Attack, FireMission, Gather, Build, Produce, Capture, Enter, UseAbility, Surrender and more |
| `Order`, `OrderType` | A unit's order. `Argument` carries extra data such as an ability id |
| `OrderTraits`, `OrderTraitTable` | How an order runs. A singleton: fixed for engine types, registered for game ones |
| `ActiveOrder`, `QueuedOrder` | The current order and the Shift queue. Disabled `ActiveOrder` means idle; `ActiveOrder.Current` reads it, type None when idle |
| `OrderWriter` | `Issue(unit, order, queue)`, which rejects a game type with no registered traits and ignores passengers, and `Stop(unit)` |
| `OrderReader` | Read-only order queries (`CanReceiveOrders`, `Current`, `TryGetLastOrder`); `OrderWriter.Reader` gives the writer's |
| `OrderHalt` | The one list of what a new order resets: movement, arrival, attack and move bookkeeping |
| `CommandReader` | This frame's commands of some types from undefeated players, and the owned units each addresses |

Games add command and order types from `GameFirst` (128) up and register each order type's traits in
`OrderTraitTable`. The traits say whether the order spreads a group into a `Formation`, hands off to `MoveOrderSystem`
(`MovesToPosition`), repeats (`Loops`), drives there backwards (`DrivesBackward`) and whether combat picks targets on
the way (`EngagesWhileMoving`). `OrderTraitTable.RegisterCommand` makes a game command issue a registered game order.
The engine runs Move, ReverseMove, AttackMove, Patrol and Escort.

One system owns each order type and ends it with `ActiveOrder.Finish`, which also halts the walk, so the Shift queue
survives and `OrderDispatchSystem` starts the next queued order. `FinishLeavingMovement` ends an order whose movement
belongs to someone else, as an attack order hands the chase to combat. Only `OrderWriter.Stop` and a new unqueued
order clear the queue.

## Commands

`UnitCommandSystem` turns unit commands into orders through `OrderResolver`, which returns the whole order: its type,
position, target and argument. `CommandOrders.For` is the one table of what each engine command gives: a list of
order types, best first, and a fallback (Move, or None to ignore the command). A unit gets the first type it can take
(`OrderResolver.CanTake`), else the fallback. A Smart command, the default right-click, tries in order:

1. A hostile target the unit's weapon can hit: Attack
2. A resource node: Gather
3. An unfinished allied building: Build
4. A damaged allied building: Repair
5. An allied container with room: Enter
6. The unit's own airfield: Return to base
7. A capturable building: Capture
8. Anything else: Move

A unit's `SmartOrderRule`s are checked before this list, so a game can add its own. Explicit commands also accept
targets a right-click skips, such as destructible scenery for Attack. A unit whose weapon fires only on order takes
an attack as a fire mission at the target's position. A game command registered in `OrderTraitTable` gives its order
with no checks; the game's order system handles a bad target.

Orders with the `Formation` trait spread a group into a box formation (`FormationSlots`), front row toward the
group's travel. A faced command (`PlayerCommand.Faced`, with `Yaw`) turns the box to its facing instead, and
Smart, Move, AttackMove and Deploy carry the facing into `Order.Facing`; aircraft that can't hover get none. A `Loops` order adds a leg back to where the
unit would otherwise end up, and Shift-queued onto a looping leg it joins that loop instead.

## Selection

| Type | Role |
| --- | --- |
| `Selectable`, `Selected` | Marks a selectable entity. `Selected` is enableable |
| `SelectionInput` | The gesture from input: click, box, double-click, modifiers, group keys |
| `ControlGroup` | The control groups an entity belongs to |
| `PickBox` | The box a click must hit, in the entity's space so it turns with it |
| `PickRanking` | Which entity a click picks |

A click prefers a visible unit on the ray, then one near the pointer, then the closest hit, so a unit behind a building
still wins. Hidden entities and passengers are skipped. Selectable prefabs and resource nodes need a **Pick Box** on
their root, drawn as a box you can drag in the Scene view.

| Gesture | Selects |
| --- | --- |
| Box | Your units, else your buildings |
| Double-click | Every on-screen entity of that type |
| Shift / Ctrl | Add to / remove from the selection |
| Ctrl+N, N, Shift+N | Store group N, recall it, add it to the selection |
