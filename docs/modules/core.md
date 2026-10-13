# Core modules

[Modules](../modules.md) ▸ Common, GameEntities, Match, Orders, Commands, Selection

## Common

The base: ownership, identity, health and shared helpers.

| Type | Role |
| --- | --- |
| `Faction` | Owner of an ownable entity: 0 = neutral, up to `Faction.Max` (29). `Faction.Bit` for 32-bit sets |
| `FactionRelations`, `Relation` | Singleton team per faction: `IsHostile`, `IsAllied`, `GetRelation` (Own, Ally, Enemy, Neutral) |
| `Player`, `PlayerRegistry` | Player identity (faction, name, colour); faction → player singleton, rebuilt first each frame |
| `PlayerSide` | The game's army or nation id for a player; the engine never reads it |
| `LocalPlayer`, `Defeated` | The player this client controls; enabled once a player loses |
| `PlayerStats` | Replicated totals: trained, built, delivered, lost, destroyed |
| `MapSettings` | Singleton: playable bounds, cell sizes, water level, flooding, max slope |
| `UnitTag`, `BuildingTag` | Movable unit; static structure |
| `Health`, `Dead` | Hit points; death marker, enabled the frame before destruction |
| `Inside` | Enabled while a passenger is aboard |
| `Destructible` | Scenery anyone can wreck (fences, trees, houses, bridges): splash hits it, attack orders may target it, fog never hides it. `Crush` = smallest `Crusher` size that flattens it |
| `Crusher` | A vehicle that flattens destructible scenery it drives over |
| `ConstructionProgress` | Enableable 0..1; an unfinished building doesn't produce, fire, house population or meet prerequisites |
| `Offline`, `OfflineReason` | "Out of action" with a reason mask (`UnderConstruction`, `Unpowered`, games from `Custom`); set and cleared per reason with `Offline.Set`. Offline entities don't fire, cast, run fields or produce |
| `GameEvents` | This frame's `UnitProduced`, `BuildingCompleted`, `OwnerChanged`; cleared first in the order phase |
| `Ownership.Transfer` | The one way to change an owner: sets `Faction` through the ECB and raises `OwnerChanged` |
| `AttackTarget` | Enableable current target (here so orders can halt attacks) |
| `EntityInfo`, `PrefabRegistry` | `TypeId` (hash of the display name), name, icon; singleton `TypeId` → prefab |
| `IEntityWriter` | Target of every `*Setup` helper: `BakerWriter` in bakers, `EntityManagerWriter` in tests |
| `AuthoringBehaviour`, `[Owner]`, `[RequiresAuthoring]` | Base of all authoring; player dropdown; a required companion |
| `EnabledExtensions` | `lookup.HasEnabled(entity)` |

## GameEntities

| Type | Role |
| --- | --- |
| `GameEntityAuthoring` | Base of unit and building authoring: name, icon, owner, health, vision, cost, build time, prerequisites, death spawn, experience, counts for victory |
| `GameEntitySetup`, `ProducibleBaking` | The shared component set; cost and prerequisite lists |
| `PlayerSetup` | The player entity's components (stats, population, power, commands, research, stock) |
| `TeamStripAuthoring` / `TeamStripBand` | Height band (m, model space) where the owner's colour shows, for buildings painted in strips |

## Match

| Type | Role |
| --- | --- |
| `MatchAuthoring` | One per map: size, nav and fog cells, fog, player slots, AI tuning, replays, join timeout |
| `PlayerSlot`, `PlayerControl` | Name, team, colour, control (LocalHuman, AI, Remote), stock, AI difficulty, build order. Index + 1 = faction |
| `MatchState` | Replicated: `Phase`, winning team, `Started` (every human joined) |
| `MatchRules` | Low-power production rate, sell refund, replays, join timeout |
| `MatchSetup`, `MatchSetupRequest` | Runtime configuration from a lobby; set the request before the map loads |
| `LocalGameSpeed` | Single-player pause and speed (`Time.timeScale`) |
| `VictoryCritical`, `PopulationProvider`, `Population` | Victory flag; population source; used / cap per player |

- `MatchSetupSystem` (first in the order phase) applies `MatchSetup` alike on server and clients: drops closed slots
  and what they own, sets names, colours, teams and `PlayerSide`, adds `AIPlayer`, scales stock, overrides fog.
- `VictorySystem` defeats a player who lost every `VictoryCritical` entity (never one who owned none) and ends the
  match when one team is left. `SurrenderSystem` handles `CommandType.Surrender`.
- `PopulationSystem` recounts each frame; delivery carriers are free.

## Orders

| Type | Role |
| --- | --- |
| `PlayerCommand`, `CommandType` | Player intent: Smart, Move, Attack, Gather, Build, Produce, Capture, Enter, UseAbility, Surrender and more; games from `Custom` |
| `Order`, `OrderType` | A unit's order; `Argument` carries extra data (ability id); games from `Custom` |
| `ActiveOrder`, `QueuedOrder` | Current order (disabled = idle) and the shift queue |
| `OrderWriter` | `Issue(unit, order, queue)`, `Stop(unit)` |
| `PlayerCommands`, `PlayerCommandSubject` | `Any(types)` skips idle frames; `Collect` lists who a command addresses (group, `Unit`, else selection) |
| `TypeSet`, `OrderTraits` | Sets of command types; `Register` lets a custom order engage while moving or use formations |

`OrderDispatchSystem` starts the next queued order. `MoveOrderSystem` runs Move, AttackMove and Patrol (patrol
re-queues its leg). `EscortSystem` follows a friendly ward. `PlayerCommandClearSystem` clears commands last.

## Commands

`UnitCommandSystem` turns unit commands into orders through `OrderResolver`. Smart resolves to the first match:
hostile the weapon hits → Attack, resource node → Gather, unfinished allied building → Build, damaged one →
Repair, allied container with room → Enter, capturable → Capture, own airfield → ReturnToBase, else Move. A unit's
`SmartOrderRule`s (target component + relations → order) come first. Group moves spread into a box `Formation`.

## Selection

| Type | Role |
| --- | --- |
| `Selectable`, `Selected` | Tag on units and buildings; enableable selection (no structural change) |
| `SelectionInput` | Singleton gesture from input: click (ray + `Pointer`), box, double-click, modifiers, group keys |
| `ControlGroup` | Group bit mask |
| `PickRanking` | A visible unit on the ray, else the visible unit within max(10 px, its radius) of the pointer, else the closest hit; used by clicks and `WorldPointer` |

`SelectionSystem`: click picks through `PickRanking` (selectable prefabs need a collider), so a unit behind a
building wins; a box prefers your units, then your buildings; double-click takes every on-screen entity of that
`TypeId`. Shift adds, Ctrl removes; Ctrl+N stores group N, N recalls it, Shift+N adds it.
