# Modules

What each module owns: the components you author or read, and the systems that run them. Layer order, contract
and frame order: [architecture](architecture.md).

Simulation modules live in `Simulation/<Module>/` (namespace `HyperRTS.Simulation.<Module>`). Client-only code is
in [Input](#input) and [Presentation](#presentation). Multiplayer replication of each module:
[networking](networking.md).

```text
Input / AI / network ─PlayerCommand─▶ command systems ─OrderWriter─▶ ActiveOrder / QueuedOrder
                                      (OrderSystemGroup)                  │
                                                                           ▼
             order behaviours (move, attack, gather, build...) ─MoveDestination─▶ pathfinding, movement
                                                                ─AttackTarget───▶ engagement, weapons
```

## Common

Base module: ownership, identity, health, shared helpers.

| Type | Role |
| --- | --- |
| `Faction` | Owner on every ownable entity (0 = neutral, up to `Faction.Max` = 29). `Faction.Bit` for 32-bit faction sets |
| `FactionRelations`, `Relation` | Singleton: team per faction. `IsHostile`, `IsAllied`, `GetRelation` (Own, Ally, Enemy, Neutral) |
| `Player`, `PlayerRegistry` | Player entity identity (faction, name, colour); singleton faction → player entity, rebuilt first each frame; jobs take a copy and index it by faction |
| `LocalPlayer`, `Defeated` | The player this client controls; enabled once a player is defeated |
| `PlayerStats` | Replicated match totals: units trained, buildings finished, resources delivered, units and buildings lost and destroyed |
| `MapSettings` | Singleton: playable bounds, grid cell sizes, water level, flooding, max slope |
| `UnitTag`, `BuildingTag` | Movable orderable unit; static structure |
| `Inside` | Enabled while a passenger is aboard a container |
| `Health`, `Dead` | Hit points; death marker enabled the frame before destruction |
| `Destructible` | Map scenery anyone can wreck (fences, trees, houses, bridges): splash from every side hits it, explicit attack orders may target it, fog never hides it. `Crush` is the smallest `Crusher` size that flattens it |
| `Crusher` | A vehicle that flattens destructible scenery it drives over (size compared with `Destructible.Crush`) |
| `ConstructionProgress` | Enableable 0..1. Unfinished buildings don't produce, provide population, fire or satisfy prerequisites |
| `GameEvents`: `UnitProduced`, `BuildingCompleted`, `OwnerChanged` | This frame's gameplay events on one singleton, cleared first in the order phase by `GameEventSystem`. Sounds, stats and cleanup react to them; so can games |
| `Ownership.Transfer` | The one way to change an owner: sets `Faction` through the ECB and raises `OwnerChanged` |
| `Offline`, `OfflineReason` | Enableable "out of action" state with a reason mask: `UnderConstruction`, `Unpowered`, and game reasons from `Custom` up. Offline entities don't fire, cast, run fields or produce. Each source sets and clears its own reason with `Offline.Set`. Buildings carry it; add it to units a game can disable |
| `AttackTarget` | Enableable current target (here so orders can halt attacks) |
| `EntityInfo` | `TypeId` (hash of the display name), `Name`, `Icon` |
| `PrefabRegistry` | Singleton `TypeId` → prefab map; resolves network commands, replicated buffers and sounds |
| `IEntityWriter` | Target of every `*Setup` helper: `BakerWriter` in bakers, `EntityManagerWriter` in tests and tools |
| `AuthoringBehaviour` | Base of every authoring component, engine or game |
| `EnabledExtensions` | `lookup.HasEnabled(entity)`: has the component and it is enabled |
| `[Owner]`, `[RequiresAuthoring]` | Player dropdown in the inspector; an authoring component needed alongside |

## GameEntities

Shared identity for units and buildings, and the player entity's component set.

| Type | Role |
| --- | --- |
| `GameEntityAuthoring` | Base of `UnitAuthoring` / `BuildingAuthoring`: name, icon, owner, health, vision, cost, build time, prerequisites, death spawn, experience value, counts-for-victory |
| `GameEntitySpec`, `GameEntitySetup` | Values and component set every unit and building carries |
| `ProducibleBaking` | Bakes cost and prerequisite lists (shared with upgrades) |
| `PlayerSetup` | Player entity: `Player`, `PlayerSide`, `PlayerStats`, `Population`, `PowerGrid`, `Defeated`, `PlayerCommand`, `PlayerCommandSubject`, `CompletedResearch`, `ResourceStock` |
| `MatchSetupSystem` | Applies a runtime `MatchSetup` once per world (see [Match](#match)) |

## Match

Players, teams, match rules, victory.

| Type | Role |
| --- | --- |
| `MatchAuthoring` | One per map: map size, nav and fog cell sizes, fog toggle, player slots, AI tuning per difficulty, replay recording, join timeout |
| `PlayerSlot`, `PlayerControl` | Slot: name, team, colour, control (LocalHuman, AI, Remote), starting resources, AI difficulty, build order. List index + 1 = faction |
| `MatchState` | Replicated singleton: `Phase` (`MatchPhase`), winning team, `Started` (set once every human joined) |
| `MatchRules` | Low-power production rate, sell refund, replay recording, join timeout |
| `MatchSetup`, `SlotSetup`, `MatchSetupRequest` | Runtime configuration from a game's lobby or skirmish screen. Set the request before the map loads |
| `LocalGameSpeed` | Pause and speed in single player (`Time.timeScale`); camera and HUD use unscaled time |
| `VictoryCritical`, `PopulationProvider`, `Population` | Victory flag; population cap source; per-player used / cap |
| `AIDifficulty`, `AIBuildOrder` | Difficulty presets (Easy, Normal, Hard, Expert, Brutal); build order asset |

Systems:

- `MatchSetupSystem` (first in the order phase) applies `MatchSetup` the same way on server and clients: removes
  closed slots and what they own, sets names, colours, teams and `PlayerSide`, adds or removes `AIPlayer`, scales
  starting stock, overrides fog.
- `PopulationSystem` recounts used and cap each frame.
- `VictorySystem` defeats players who lost every `VictoryCritical` entity they had; ends the match when one team
  is left. A player who never owned anything is not defeated.
- `SurrenderSystem` defeats a player who sends `CommandType.Surrender`.
- `CasualtyStatsSystem` (Combat) credits losses and kills to `PlayerStats`.

## Orders

| Type | Role |
| --- | --- |
| `PlayerCommand`, `CommandType` | Smart, Move, AttackMove, Attack, Gather, Build, Stop, SetStance, PlaceBuilding, Produce, CancelProduction, SetRallyPoint, Sell, Repair, Capture, Enter, Unload, UseAbility, UsePower, Patrol, Escort, ReturnToBase, Surrender; games from `Custom` |
| `Order`, `OrderType` | Move, AttackMove, Attack, Gather, Build, Repair, Capture, Enter, UseAbility, Patrol, Escort, ReturnToBase; games from `Custom`. `Argument` carries extra data (ability id) |
| `ActiveOrder`, `QueuedOrder` | Current order (enableable; disabled = idle) and shift-queue |
| `OrderWriter` | `Issue(unit, order, queue)`, `Stop(unit)` |
| `PlayerCommandSubject` | Player buffer: the units a group command lists, each command owning a range |
| `PlayerCommands` | `Any(types)` skips frames without a system's command types; `Collect` lists who a command addresses (listed group, else `Unit`, else the selection) |
| `TypeSet` | Set of command types, custom ones included: `TypeSet.Of(...)`, `Range`, `With`, `Contains` |
| `OrderTraits`, `OrderTrait` | `Register(type, traits)` lets a custom order engage while moving, attack-move or use formations, like the built-in ones |

Systems:

- `OrderDispatchSystem` starts the next queued order when `ActiveOrder` is disabled.
- `MoveOrderSystem` runs Move, AttackMove and Patrol. Completes on arrival, or when a crowded unit stalls near
  its goal. Patrol re-queues the finished leg, so units loop.
- `EscortSystem` keeps a unit near a friendly ward; ends when the ward dies.
- `PlayerCommandClearSystem` clears commands at the end of the order phase.

## Commands

`UnitCommandSystem` turns unit commands into orders through `OrderResolver`. Smart resolves, in order: hostile the
weapon can hit → Attack, resource node → Gather, unfinished allied building → Build, damaged allied building →
Repair, allied container with room → Enter, capturable building → Capture, own airfield → ReturnToBase,
otherwise Move. A unit's `SmartOrderRule`s come first: each names a target component (by stable type hash) and
the relations it applies to, and gives its order. Group moves spread into a box `Formation`.

Producer, ability, placement, sell and unload commands are handled by their own modules.

## Navigation

| Type | Role |
| --- | --- |
| `NavAgent`, `NavLayer` | Radius and layer: Ground, Naval, Amphibious, Air |
| `MoveDestination` | Enableable XZ goal |
| `PathWaypoint`, `PathState` | Remaining path corners, request bookkeeping |
| `NavGrid`, `NavSurface` | Singleton grid of Land / Water / Deck flags (0 = blocked): `IsWalkable`, `IsAreaFree`, `HasLineOfSight`, `TryFindNearestWalkable`, `HeightFor(layer)` |
| `NavObstacleAuthoring` / `NavObstacle` | Blocked XZ box (buildings add their footprint), turned with the entity |
| `NavAreaAuthoring` / `NavArea` | Box overriding cells: Water, Blocked, or a walkable Deck (bridge) with water kept below; turned with the entity |
| `NavBox` | A box turned about Y: world `Extents`, `Contains`. `NavGrid.GetBoxArea` / `InBox` stamp it (any overlap counts; 90° turns stamp exactly) |
| `NavGround` | How cells get their surface (`Classify`: flooded terrain is water, too steep is blocked; `Stamp`: what an area leaves). Map tools measure maps with the same rules |
| `TerrainHeightAuthoring` / `TerrainHeight` | Baked heightfield: `Height(xz)`, `Raycast`. Flat y = 0 without one |
| `PlacementMath`, `PlacementSurface` | The building placement rules, shared by simulation, ghost, rally points and AI |
| `TargetReach` | Walks a unit to a target's footprint for orders that act on it (build, repair, capture, board, gather): `Approach`, `Position`, `Exists`. Systems pass it `MapQuery` so "at it" follows the map's nav cell size |
| `ReachMath`, `NavTolerances` | The math under it (`Reach`, `InReach`, `Approach`, `MoveTo`); arrival tolerances. A unit parks on the nearest open cell, up to 1.5 cells from the footprint, so coarse grids widen the reach |
| `ViewGround` | Ground points along a camera's view rays (camera, minimap, audio listener) |

Systems:

- `NavGridSystem` classifies cells from terrain, slope and water, stamps areas and obstacles, and bumps a version
  when they change so units re-path.
- `PathfindingSystem`: Burst grid A* (8-way, no corner cutting) on the agent's layer, line-of-sight smoothing,
  at most 48 searches per frame, skipped when the goal is directly visible. A search expands at most
  `NavGrid.MaxSearchExpansions` cells, then heads for the closest cell found; the map report warns when a
  base-to-base path needs more.

Units on layers that share a surface flag share cells: ships pass under a deck that tanks drive over.

## Units

| Type | Role |
| --- | --- |
| `UnitAuthoring` | Move speed, radius, nav layer, population |

`MovementSystem` follows waypoints, separates overlapping units, keeps Y on the ground, deck or water of the
agent's layer, and disables `MoveDestination` on arrival. Aircraft fly straight at altitude (see [Air](#air)).

## Spatial

`SpatialIndex` is a singleton uniform-grid hash of living entities with `Faction` + `Health`, rebuilt at the start
of the movement phase. Query it with a struct `ISpatialVisitor`. `Closest` picks the nearest candidate; ties go to
the lower entity index.

## Combat

| Type | Role |
| --- | --- |
| `WeaponAuthoring` / `Weapon` | Range, damage, cooldown, damage type, splash radius and edge damage, friendly fire, projectile prefab, acquire range, `WeaponTargets` (Surface, Air, both) |
| `AmmoAuthoring` / `Ammo` | Rounds per load; empty weapons hold fire; reloads while `Docked` |
| `ArmorAuthoring` / `ArmorModifier`, `ArmorFacing` | Multiplier per `DamageType` asset; front / side / rear multipliers |
| `DamageEvent`, `DamageWriter`, `DamageQueue` | Every hit and heal is queued here (negative = heal) |
| `CombatStance`, `Stance` | Aggressive, Defensive (returns to anchor), HoldPosition, Passive |
| `LastAttacker` | Who last damaged the entity (kill credit) |
| `Projectile` | Shot in flight carrying its `DamageEvent` |
| `SpawnOnDeath` | Wreck prefab spawned on death |
| `TargetLookup` | `IsValidTarget`: hostile, alive, on a layer the weapon hits, not hidden by stealth. `IsAttackable` also accepts living `Destructible` scenery, for explicit attack orders only |
| `DestructibleAuthoring` | Marks a building or unit `Destructible`, or makes standalone scenery: health, crush rating, remains, optional blocked box (`DestructibleSetup.AddScenery`) |
| `DamageStagesAuthoring` / `DamageStage`, `ShownDamageStage` | Child models shown by health fraction (intact, damaged), with an effect per step; presentation swaps them |
| `SpawnEffectAuthoring` / `SpawnEffect` | Effect played once where an entity appears (the dust of a ruin, a fallen tree) |

Systems in order:

1. `AttackOrderSystem` runs explicit Attack orders.
2. `TargetAcquisitionSystem`: idle, attack-moving, patrolling, escorting and holding units and towers pick the
   nearest valid hostile (each entity scans every 4th frame).
3. `EngagementSystem` chases, stops in range, or releases (target invalid, out of ammo, or leashed by stance).
4. `WeaponFireSystem` queues an instant hit or spawns a projectile.
5. `ProjectileSystem` homes in and queues the hit on impact.
6. `CrushSystem` (before damage) queues a lethal, uncredited hit on scenery under a moving crusher.
7. `DamageSystem` (last) applies armor × facing × `DamageTaken` stat, splash falloff, `LastAttacker`. Splash
   reaches enemies, allies with friendly fire, and `Destructible` scenery.

Not built in: turrets that rotate independently.

## Air

| Type | Role |
| --- | --- |
| `FlightAuthoring` / `Flight` | Makes a unit an aircraft (Air layer): altitude, climb speed, loiter radius (0 hovers), uses pads |
| `AirfieldAuthoring` / `LandingPad` | Pad offsets on a building, one aircraft each |
| `HomePad` | The aircraft's airfield and pad |
| `Docked` | Enableable, replicated: landed on its pad |
| `AirfieldRules` | `IsOwnedAirfield`: finished, owned building with pads |

- An airfield producing a pad user waits for a free pad and spawns it docked.
- `PadSystem` claims and frees pads, drops homes of destroyed or captured airfields, undocks moving aircraft, runs
  ReturnToBase.
- `RearmSystem` sends empty aircraft home and reloads one round per `ReloadTime` while docked.
- Fuel and pad repair are game code reading `Docked`.

## Stats

| Type | Role |
| --- | --- |
| `StatModifier`, `Stat` | Buffer: `(base + Add) × (1 + ΣPercent)` for MaxHealth, Damage, Range, FireRate, MoveSpeed, VisionRange, DamageTaken, BuildRate, ProductionSpeed; games from `Stat.Custom` (128) |
| `StatSource`, `StatSourceKind` | Who added a modifier: Upgrade, Veterancy, Field; games from `Custom` (128) |
| `BaseStat`, `StatMath` | Unmodified values captured on first apply; `Evaluate`, `Apply`, `RemoveSource` |

Each module applies its own stats last in the lifecycle phase, only when the modifier buffer changes:
`MaxHealthStatSystem` (keeps the health fraction), `WeaponStatSystem`, `MoveSpeedStatSystem`, `VisionStatSystem`,
`BuildRateStatSystem`, `ProductionSpeedStatSystem`. `DamageSystem` reads DamageTaken.

## Veterancy

| Type | Role |
| --- | --- |
| `VeterancyAuthoring` / `Experience`, `VeterancyRank`, `VeterancyBonus` | Thresholds and per-rank bonuses |
| `ExperienceValue` | What the killer earns |

`KillCreditSystem` pays a dead entity's experience to its last hostile attacker. `VeterancySystem` promotes and
swaps the rank modifiers.

## Upgrades

| Type | Role |
| --- | --- |
| `UpgradeAuthoring` / `UpgradeEffect` | Research prefab with stat bonuses per target type (none = all): name, icon, cost, time, prerequisites |
| `AppliedUpgrades` | How many of the owner's finished research an entity carries |

`UpgradeSystem` adds the effects of the owner's `CompletedResearch` to matching owned entities, catches up later
spawns, and swaps sets when an entity changes owner (dropping every `StatSourceKind.Upgrade` modifier).

## Power

`BuildingAuthoring.power` adds a `PowerSupply` (positive generates, negative draws). `PowerSystem` (end of the
order phase) totals each player's `PowerGrid` and sets `OfflineReason.Unpowered` on consumers while demand exceeds
supply.
Unpowered weapons hold fire; unpowered producers run at `MatchRules.LowPowerProductionRate`.

`OfflineSystem` (right after) recomputes the engine's `OfflineReason`s, keeps any game reasons, and enables
`Offline` while one holds. A building offline for a game reason neither supplies nor draws power.

## Resources

| Type | Role |
| --- | --- |
| `ResourceType` (asset) | Name, colour, icon |
| `ResourceCost`, `ResourceStock`, `ResourceMath` | Prefab price; player stockpile; arithmetic |
| `ResourceNodeAuthoring` / `ResourceNode` | Neutral deposit, finite or regrowing |
| `ResourceDropOffAuthoring` | Where harvesters deposit |
| `HarvesterAuthoring` / `Harvester`, `HarvestState` | Capacity, gather rate, trip state |
| `ResourceIncomeAuthoring` / `ResourceIncome` | Passive income for the owner (an oil derrick) plus a one-time bonus to its first owner; neutral pays nothing, `Offline` pauses it |
| `OccupantIncome` | Opt-in (Per Occupant): the income pays its amount once per `Cargo` passenger, nothing while empty |
| `ResourceReserve` | Opt-in (Reserve): finite stock the income pays out of; at 0 it pays nothing but stays to be refilled |
| `ReserveRefillAuthoring` / `ReserveRefill` | While owned and online, adds an amount every interval to each same-owner reserve of its type, up to capacity |
| `CasualtyIncomeAuthoring` / `CasualtyIncome` | While owned and online, pays the owner a share (rounded down) of the `ResourceCost` of each own or hostile unit that dies; several earners each pay |
| Player `Stat.Income` | Delivered resources are scaled by the owner's `StatModifier`s on `Stat.Income` (an AI difficulty bonus, a game's faction perk) |

`GatherSystem` runs node → fill → nearest drop-off → deposit, switching to a nearby node of the same type when one
runs dry. `ResourceNodeSystem` regrows or removes nodes. `ReserveRefillSystem` tops up reserves just before
`ResourceIncomeSystem` pays incomes; `CasualtyIncomeSystem` pays casualty income after
`DeathSystem`. All three count their payments in `PlayerStats.ResourcesGathered`.

### Delivery

`DeliverySourceAuthoring` / `DeliverySource` on a building sends `Count` carriers (a baked unit prefab: a supply
truck, a cargo plane) every `Interval` while the building is owned and not `Offline`; a neutral source restarts its
timer. `DeliverySystem` spawns each wave on the playable-bounds point (`MapSettings`) of the ray from the map
centre through the building, so the entry point is deterministic, spread `Spacing` apart across the ray, then sets
`LocalTransform`, `Faction` and an enabled `MoveDestination` on the building (aircraft fly there the same way). A
`DeliveryCarrier` within `ArrivalRange` of its source pays its owner `Amount`, scaled by `Stat.Income` and counted in
`PlayerStats.ResourcesGathered`, and is destroyed. A killed (`Dead`) carrier pays nothing; when the source is
destroyed or dies, its carriers are destroyed too. Give carriers no weapons, or combat takes over their movement.

## Buildings

| Type | Role |
| --- | --- |
| `BuildingAuthoring` | Footprint, population provided, power, starts under construction |
| `BuilderAuthoring` / `Builder`, `BuildOption` | Build rate and placeable building prefabs |
| `BuildingPlacement` | Surface the footprint must cover (land, water, shoreline) |
| `BuildingRules` | `IsAlliedSite`, `NeedsRepair`, `IsSellable` |

- `PlaceBuildingSystem` validates placement, charges the cost, spawns a site and orders builders to it.
- `ConstructionSystem` advances sites while builders work on them.
- `RepairSystem`: builders restore allied finished buildings at their build speed.
- `SellSystem` refunds `MatchRules.SellRefund` of a finished building (all of a site) plus its queue. A `NeutralGarrison` is never sellable (`BuildingRules.IsSellable`, also what the HUD's Sell button asks).

## Production

| Type | Role |
| --- | --- |
| `ProducerAuthoring` / `Producer`, `ProductionOption`, `ProductionQueueItem`, `RallyPoint` | Unit training and research queue |
| `Producible` | Build time and population cost |
| `Prerequisite`, `CompletedBuildings` | Required `TypeId`s, met by a completed building or finished research; check shared by simulation, AI and HUD |
| `ProducibleAuthoring` | Base of unit, building and research authoring: `DisplayName` and the `TypeId` prerequisites name |
| `PurchaseRules`, `Buyer` | The one buy rule for orders, placement, AI and HUD: `IsUnlocked` (prerequisites, research once, build limit), `CanBuy`, `TryBuy` |
| `Research`, `CompletedResearch`, `ResearchRules` | A producible that is researched once instead of spawned; the player's finished research (replicated by `TypeId`); queue-once checks |
| `ProductionRules` | `HasOption`, `Refund`, `RefundQueue` |
| `BuildLimitAuthoring` / `BuildLimit`, `BuildCounts` | Optional cap per player on a unit or building type: standing (sites too) plus queued may not exceed `Max`; `PurchaseRules.IsUnlocked` refuses past it, so orders, placement, the AI and the HUD buttons all honour it |

`ProductionCommandSystem` handles Produce, Cancel (refund) and rally points. `BuildStatsSystem` counts
`UnitProduced` and `BuildingCompleted` into `PlayerStats`; `OwnerChangeSystem` refunds a transferred entity's
queue to the old owner, stops its orders and attack, and drops its rally point and selection. `ProductionSystem`
trains the queue head when population allows and sends units to the rally point; a finished `Research` is added
to the owner's `CompletedResearch` instead.

## Capture

| Type | Role |
| --- | --- |
| `CapturableAuthoring` / `Capturable`, `CaptureProgress` | Building others take over in `captureTime` seconds |
| `CapturerAuthoring` / `Capturer` | Unit that captures, optionally used up |
| `CaptureLookup` | `CanCapture`, shared by orders and `CaptureSystem` |

`CaptureSystem` advances progress while capturers are in reach, then changes the owner, refunds the queue to the
old owner and clears target and selection. A garrisoned building can't be captured.

## Transport

| Type | Role |
| --- | --- |
| `ContainerAuthoring` / `Container`, `Cargo` | Capacity, largest passenger, passengers fire out, passengers survive its death |
| `NeutralGarrison` | A neutral building anyone may board; it belongs to its passengers while occupied (`NeutralGarrisonSystem`) and is neutral again when empty; each change goes through `Ownership.Transfer`, so the building drops its selection and the team tint is cleared |
| `PassengerAuthoring` / `Passenger`, `PassengerStance` | Size; stance restored on exit |
| `Boarding` | `CanBoard`, `Board`, `IsInside` |

Aboard, a passenger is out of the spatial index, takes no orders, is hidden, and fires out or stays passive.
`BoardingSystem` runs Enter, `CargoFollowSystem` carries passengers, `UnloadSystem` handles Unload,
`ContainerDeathSystem` ejects or kills them when the container dies.

## Fields

`AreaFieldAuthoring` / `AreaField`, `AreaFieldBonus`: an aura with radius, `FieldTargets` (own, allies, enemies,
neutral × units, buildings), stat bonuses, heal or damage per second. Every 0.25 s `AreaFieldSystem` rewrites
each entity's `FieldPresence` (one entry per field name, never stacking). Games read `FieldPresence` for their own
effects.

## Abilities

| Type | Role |
| --- | --- |
| `AbilityAuthoring` / `Ability` | Cooldown ability: target (none, point, entity) and filter, range (0 = unlimited), required building, built-in effects (spawn prefab, area damage or heal) |
| `AbilityActivation`, `AbilityEvents` | This frame's uses, for game systems |

- `AbilityCommandSystem` handles UseAbility (self-targeted: every ready selected caster; aimed: nearest ready
  caster walks into range) and UsePower (`Ability` buffer on the player entity).
- `AbilitySystem` ticks cooldowns and runs walk-into-range casts.

## Vision

| Type | Role |
| --- | --- |
| `VisionRange` | Sight radius |
| `FogOfWar` | Singleton grid, one bit per team for visible, explored and detected. `CanSee`, `IsHiddenFrom`, `IsCloakedFrom`. Teams 1–15 |
| `StealthAuthoring` / `Stealth`, `Stealthed` | Reveal time after firing, still-only option; result enabled while hidden from hostile teams |
| `DetectorAuthoring` / `Detector` | Detection radius |
| `FogHidden`, `LocalFogView` | Hostile roots the local player can't see; who the local player views as (`RevealAll` for replays) |

- `FogOfWarSystem` restamps vision and detection 10 times per second. With `TerrainHeight`, hills block sight.
  With fog off every cell is visible, but detection still runs.
- `StealthSystem` sets `Stealthed`; firing restarts the reveal timer.
- `LocalFogViewSystem` tags hostile entities the local player can't see with `FogHidden`. Selection, picking, HUD
  and rendering skip them. Splash still hits them. `Destructible` map structures are never hidden, whoever holds
  them.

## Animation

Machines in the map (pumpjacks, turbines). `MachineAuthoring` / `MachineClock` on the root (optionally running only
while owned); `MachinePartAuthoring` / `MachinePart` on moving children: `Spin` about an axis, `Swing` in step with
the spin around an offset, or `Link` (sit on one transform, aim the up axis at another, for connecting rods).
Presentation's `MachineSystem` poses the parts from the clock, so parts of one machine stay in step.

## Audio

| Type | Role |
| --- | --- |
| `SoundCue` (asset) | Clips, volume and pitch ranges, max instances, priority, mixer group, 2D / 3D, distance |
| `EntitySoundsAuthoring` / `EntitySound` | Cues per `SoundSlot`: Fire, Impact, Death, Ability, and owner-only Ready, Select, Move, Attack; games from `Custom` |
| `SoundEvent`, `SoundWriter`, `SoundQueue` | This frame's sounds by `TypeId` + slot + position + owner |
| `SoundRules` | `IsAudible`: voices reach only their owner, other sounds whoever sees the source |

- `SoundClearSystem` empties the queue at the start of each frame.
- Writers: `WeaponFireSystem` (Fire, instant Impact), projectiles (Impact), `DeathSoundSystem`,
  `AbilitySoundSystem` and `ReadySoundSystem` (Ready, from `UnitProduced` and `BuildingCompleted`). Games append with `SoundWriter`
  before the end of the lifecycle phase.
- `AcknowledgementSystem` (client, single player) plays one selected or commanded unit's voice, at most once a
  second.
- Playback is client-only: see [Presentation](#presentation).

## AI

| Type | Role |
| --- | --- |
| `AIPlayer`, `AITuning` | Marks a computer player; think interval, wave size, ability use, income multiplier, fog honesty |
| `AIDifficultyTuning` | Every difficulty's tuning, baked on the Match |
| `AIBuildStep` | One build-order entry: building, unit or upgrade prefab and count |
| `AIPlayerSetup` | Adds `AIPlayer` and its build order to a player entity |
| `AIPlacement` | Ring search for a free building spot around the base |
| `AIMapHintsAuthoring`, `AIMapHints` | The map's bot hints, one singleton per match: per slot the resource fields by walking distance from its base (`AISlotHint`) and defence points; attack lanes between enemy slots (`AILaneHint`); positions in `AIHintPoint` ranges. Read through `AIMapHintsView`; map builders write them |
| `AIRoutes` | Seeded, integer-only choice of an attack lane |
| `AIMapHintsCheckSystem` | Logs one error naming the map when AI players run on a map without hints (or without their slot) |

`SkirmishAISystem` drives each `AIPlayer` through `PlayerCommand`s only, and only on a map that carries
`AIMapHints`. Each think:

1. Start the first unmet build-order step it can afford.
2. Finish abandoned sites with idle builders; send idle harvesters to the first field of the slot's expansion order
   that still has resources.
3. After the build order: train and research round-robin at idle producers.
4. Use ready abilities and player powers on the nearest suitable target.
5. Once enough idle combat units exist, pick the nearest hostile building (else unit) among the enemies the map has
   lanes to, and attack-move them along one lane to that enemy: shift-queued attack-moves through the lane's
   waypoints from the one nearest the wave, then the target. The lane is a seeded choice (`AIPlayer.RouteSeed`,
   the faction, the enemy, `AIPlayer.Attacks`), so bots and successive attacks vary their route. `MatchSetupSystem` sets `RouteSeed` from `MatchSetup.Seed` and the faction (`AIRoutes.SeedFor`); replays record the seed. With
   `LimitedByFog` it only targets units its team sees and buildings it has explored, and sweeps the map centre when
   it knows of none.

Ties break by entity index. To replace it, disable the system and drive your own `PlayerCommand`s.

## Replays

| Type | Role |
| --- | --- |
| `ReplayRecorder` | `Start`, `Snapshot`, `Stop` in the authoritative world |
| `Replay`, `ReplaySerializer` | Header (scene, sample rate, duration, players, result) and samples; GZip file |
| `ReplayViewer` | `Begin(world, replay)` / `Stop` in a local world that loaded `Replay.ScenePath` |
| `ReplayPlayback` | Singleton for a HUD: time, duration, speed, playing; writing the time seeks |

- `ReplayRecorderSystem` samples every entity with `EntityInfo` + `Faction` at 10 Hz: keyframes every 10 s,
  deltas between.
- Replays store observed state, not commands. Playback turns the gameplay phases off, reveals the fog, matches
  scene entities to recorded ones, spawns later ones from baked prefabs and interpolates transforms.

## Selection

| Type | Role |
| --- | --- |
| `Selectable` | Tag on every unit and building |
| `Selected` | Enableable; selection causes no structural change |
| `SelectionInput` | Singleton gesture written by input: click, box, double-click, modifiers, group keys |
| `ControlGroup` | Bit mask of groups, added on first assignment |

`SelectionSystem`:

- Click selects anything under the cursor (enemies for info). Picking uses a Unity Physics raycast, so selectable
  prefabs need a collider.
- Box prefers the local player's units, then its buildings, then anything.
- Double-click selects every on-screen entity with the same `TypeId`.
- Shift adds, Ctrl removes, no modifier replaces.
- Ctrl+N stores group N, N recalls it, Shift+N adds it.

## Interaction (HyperRTS.Client)

Client UI state shared by input, HUD and overlays, in the `HyperRTS.Client` assembly. `ClientSingletonSystem`
creates the singletons once per presented world. `DefaultWorld` and `LiveQuery` (in `Client/Common`) give
MonoBehaviours the presented world and a query recreated when that world is replaced.

| Singleton | Role |
| --- | --- |
| `PlacementState` | Building placement in progress: prefab, ghost position, validity |
| `PendingCommand` | Targeted command waiting for a click (attack-move, patrol, escort, aimed ability) |
| `PointerState` | `OverUI`: the HUD is under the cursor, so world clicks are ignored |
| `SelectionDragState` | Marquee rectangle |
| `CameraFocusRequest` | Minimap click → camera |
| `HotkeyPresses` | Letters and digits pressed while gameplay input is live, for command card hotkeys |

## Input

Client only (`HyperRTS.Input`).

| Type | Role |
| --- | --- |
| `RTSInputActions` | Selection, Commands and Camera maps ([bindings](getting-started.md#play)) |
| `InputActionsProvider` | The one shared actions instance; `Suspend` / `Resume` per owner; `FrontEndOwnsCancel` |
| `SelectionInputSystem` | Mouse gestures → `SelectionInput` |
| `CommandInputSystem` | Right-click Smart, hotkeys (A, S, H, P, E), targeted commands → `PlayerCommand` |
| `PlacementInputSystem` | Moves the ghost, confirms with `PlaceBuilding` |
| `WorldPointer` | Cursor → world: physics raycast, then terrain, then the ground plane |
| `CameraController` | Pan, edge scroll, zoom, rotate, map clamp, `FocusOn`, `speedMultiplier`, start at home base |

Clicks over the HUD, in placement mode or while a command is pending never reach selection.

A front end that reads Escape itself (pause menu) sets `InputActionsProvider.FrontEndOwnsCancel` and calls
`MatchView.CancelInteraction()` first, which cancels placement or targeting and returns whether anything was
pending.

## Presentation

Client only (`HyperRTS.Presentation`). `HUD.prefab` holds:

| Component | Role |
| --- | --- |
| `HUDController` | UI Toolkit HUD on a `PanelRenderer`. Builds its `IHUDPanel`s in `CreatePanels` and refreshes them every frame from a `HUDContext` |
| Panels | `ResourceBar` (resources, power, population), `SelectionPanel` (with queue), `CommandCard`, `Minimap`, `GameOverBanner` |
| `CommandCard`, `ICommandSource`, `CommandEntry` | The card asks each source for entries when the selection changes and refreshes them every frame. Defaults: `PurchaseCommands` (build, train, research), `AbilityCommands` (abilities, powers), `StanceCommands`, `UnitActionCommands` (return, unload, sell). An entry can take a grid `Slot` and a letter or digit `Hotkey`, fed by `HotkeyInputSystem` through the `HotkeyPresses` singleton |
| `HUDText` | All HUD strings by key. `Get(key)` for fixed text, `Name(key, authored)` for entity, resource and ability names. Set `HUDText.Current` to translate |
| `OverlayRenderer` | Selection rings, health bars, placement ghost, rally markers (instanced meshes) |
| `FogOfWarRenderer` | Fog drawn as a box around the map that the shader projects onto the depth buffer, so hills, trees and buildings are fogged where they stand; scenery past the playable edge stays dim |
| `SelectionDragBoxUI` | Marquee |

`HUDText` keys:

| Kind | Keys |
| --- | --- |
| Fixed | `hud.pop`, `hud.power`, `command.return`, `command.unload`, `command.sell`, `stance.<stance>`, `outcome.victory`, `outcome.defeat`, `outcome.draw` |
| Names | `entity.<name>`, `resource.<asset name>`, `ability.<name>`, kebab-cased by `HUDText.Key` ("War Factory" → `entity.war-factory`) |

Other client pieces:

- `MatchView`: once-per-frame read model for MonoBehaviours (local player or observer, relations, match, colours),
  `CancelInteraction`, `GetColor`.
- `TeamColorSystem` tints owned meshes through `URPMaterialPropertyBaseColor` (use URP Lit). A
  `TeamColorOverride` singleton shows factions in other colours (colour-blind palettes, own / ally / enemy).
- `FogVisibilitySystem` mirrors `FogHidden` and `Inside` onto `DisableRendering`.
- `SoundPlaybackSystem` plays the `SoundQueue` through a pool of 32 sources with per-cue limits and priority.
  `SoundListener` keeps the `AudioListener` on the camera's ground focus.
- `Rendering/`: `OverlayMeshes`, `InstanceBatch`, `RenderHierarchy`, `EntityExtent`.
- `Minimap` draws the scene's `MinimapImage` (a top-down picture map tools bake), the local fog of war, blips
  (squares for structures) and the camera view.
- `Destruction/`: `DamageStageSystem` shows the stage model matching health (others scaled to zero, so fog's
  rendering toggles stay untouched) and plays the step's effect; `SpawnEffectSystem` plays spawn effects once.
- `Animation/MachineSystem` poses machine parts.
- `Environment/`: `HyperRTS/Water` (depth-based colour and clarity, refraction, downstream ripples along the mesh's
  V, sun glint, sky reflection, shore foam; needs the Depth and Opaque Textures) and `HyperRTS/Foliage` (alpha-cut,
  two-sided, wind sway, translucency, hue variation; instanced and DOTS-instanced), with `WaterRipples.png`.
