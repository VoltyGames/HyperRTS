# Core modules

[Modules](../modules.md) ▸ Common, GameEntities, Match, Orders, Commands, Selection

The modules every other module builds on: who owns what, how a match is set up, and how player intent becomes
unit orders.

## Common

The base tier: ownership, identity, health and shared helpers.

| Type | Role |
| --- | --- |
| `Faction` | Owner of an entity. 0 is neutral, the maximum is 29 |
| `FactionRelations` | Singleton with each faction's team. Answers `IsHostile`, `IsAllied` and `GetRelation` |
| `Player`, `PlayerRegistry` | A player's faction, name and colour. The registry maps faction to player |
| `PlayerSide` | The game's army or nation id. Picks the player's start kit and AI build order |
| `LocalPlayer`, `Defeated` | The player this client controls. Enabled once a player loses |
| `PlayerStats` | Replicated totals: trained, built, delivered, lost, destroyed |
| `MapSettings` | Singleton: playable bounds, cell sizes, water level, max slope |
| `UnitTag`, `BuildingTag` | A movable unit. A static structure |
| `Health`, `Dead` | Hit points. `Dead` is enabled the frame before destruction |
| `Destructible` | Scenery anyone can wreck. Splash hits it and fog never hides it |
| `ConstructionProgress` | 0 to 1. An unfinished building doesn't produce, fire or count for prerequisites |
| `Offline` | Out of action, with a reason mask. Games add reasons from `OfflineReason.Custom` |
| `GameEvents` | This frame's unit produced, building completed and owner changed events |
| `Ownership.Transfer` | The one way to change an owner. Raises `OwnerChanged` |
| `EntityInfo`, `PrefabRegistry` | Type id, name and icon. The registry maps type id to prefab |
| `IEntityWriter` | Writes component sets, so bakers and tests build the same entities |
| `AuthoringBehaviour` | Base of all authoring components |

An entity's type id is a hash of its display name, so two prefab types must not share a name.

## GameEntities

| Type | Role |
| --- | --- |
| `GameEntityAuthoring` | Base of unit and building authoring: name, icon, owner, health, vision, cost, build time, prerequisites, death spawn |
| `PlayerSetup` | The components of a player entity: stats, population, power, commands, research, stock |
| `TeamStripAuthoring` | Height band where the owner's colour shows, for buildings painted in strips |

## Match

One `MatchAuthoring` per map sets its size, fog, player slots, sides, AI tuning, replays and join timeout. Each slot
has a name, team, colour, control type, starting stock, spawn point and side. Slot index + 1 is the player's
faction.

A side holds a start kit and an AI build order. At match start every player gets its side's kit, spawned around its
spawn point, and its side's build order. A side the Match doesn't list is an error.

| Type | Role |
| --- | --- |
| `MatchState` | Replicated phase, winning team, and `Started` once every human joined |
| `MatchRules` | Low-power production rate, sell refund, replays, join timeout |
| `MatchSetupRequest` | Lobby configuration. Set it before the map loads |
| `LocalGameSpeed` | Single-player pause and speed |
| `VictoryCritical` | A player who loses all of these is defeated |
| `PopulationProvider`, `Population` | Population sources, and used and cap per player |

`MatchSetupRequest` closes slots and removes what they own, sets names, colours, teams and sides, assigns AI, scales
starting stock and overrides fog. It applies the same way on the server and every client. Its `Weather` is read by
the scene when it loads. Leave it empty for the map's own weather.

Victory: a player who loses every `VictoryCritical` entity is defeated, and the match ends when one team is left. A
player who never owned one is never defeated by this.

## Orders

All player intent is a `PlayerCommand` on the player entity. Units carry orders.

| Type | Role |
| --- | --- |
| `PlayerCommand`, `CommandType` | Player intent: Smart, Move, Attack, Gather, Build, Produce, Capture, Enter, UseAbility, Surrender and more |
| `Order`, `OrderType` | A unit's order. `Argument` carries extra data such as an ability id |
| `ActiveOrder`, `QueuedOrder` | The current order and the Shift queue. Disabled `ActiveOrder` means idle |
| `OrderWriter` | `Issue(unit, order, queue)` and `Stop(unit)` |
| `PlayerCommands` | Which units a command addresses, and a fast check for idle frames |

Games add command and order types from `Custom`. The engine runs Move, AttackMove, Patrol and Escort. When the active
order finishes, the next queued one starts.

## Commands

Unit commands become orders. A Smart command, the default right-click, resolves to the first match:

1. A hostile target the unit's weapon can hit: Attack
2. A resource node: Gather
3. An unfinished allied building: Build
4. A damaged allied building: Repair
5. An allied container with room: Enter
6. The unit's own airfield: Return to base
7. A capturable building: Capture
8. Anything else: Move

A unit's `SmartOrderRule`s are checked before this list, so a game can add its own. Group moves spread into a box
formation.

## Selection

| Type | Role |
| --- | --- |
| `Selectable`, `Selected` | Marks a selectable entity. `Selected` is enableable |
| `SelectionInput` | The gesture from input: click, box, double-click, modifiers, group keys |
| `ControlGroup` | The control groups an entity belongs to |
| `PickRanking` | Which entity a click picks |

A click prefers a visible unit on the ray, then one within 10 px or its radius of the pointer, then the closest hit.
So a unit behind a building still wins. Selectable prefabs need a collider.

| Gesture | Selects |
| --- | --- |
| Box | Your units, else your buildings |
| Double-click | Every on-screen entity of that type |
| Shift / Ctrl | Add to / remove from the selection |
| Ctrl+N, N, Shift+N | Store group N, recall it, add it to the selection |
