# Module reference

What each engine module owns, the components you author or read, and the systems that run it. Simulation modules
live in `Packages/com.hyperrts.engine/Simulation/<Module>/` (namespace `HyperRTS.Simulation.<Module>`); client-only code is in
`Input/` and `Presentation/`. For the frame order, see [`world-setup.md`](world-setup.md).

Modules are layered. `Common` is the base: it uses no other module, and every other module may use it. A component
two modules both need goes in `Common` or in the lower of the two, so dependencies stay one-way. Authoring files
are exempt, since they assemble prefabs from every module's components. `ModuleLayoutTests` (in `Editor/Tests/`)
checks this, that each namespace matches its folder, and that `*Authoring.cs` files hold only the authoring
class and its `Baker` (runtime components live in their own files). Its allowlist of existing two-way pairs
should only shrink: a new pair usually means a shared type belongs lower.

## How the modules talk

```text
Input / AI ──PlayerCommand──▶ command consumers ──OrderWriter──▶ ActiveOrder / QueuedOrder
                              (OrderSystemGroup)                     │
                                                                     ▼
              behaviours (move, attack, gather, build) ──MoveDestination──▶ pathfinding + movement
                                                       ──AttackTarget────▶ engagement + weapons
```

Five rules hold everything together:

1. **Commands in.** Player intent is a `PlayerCommand` on the player entity. `Unit = Entity.Null` means "the
   player's selected entities". Commands live for one order phase.
2. **Orders.** `ActiveOrder` (enableable; disabled = idle) is the order being executed, `QueuedOrder` the
   shift-queue. Issue and stop orders only through `OrderWriter`. The system that owns an order type disables
   `ActiveOrder` when it is done, and `OrderDispatchSystem` starts the next queued one.
3. **Locomotion.** To move a unit, set `MoveDestination` and enable it. Navigation disables it on arrival.
4. **Combat owns movement while `AttackTarget` is enabled.**
5. **Ownership.** `Faction` (0 = neutral) on everything ownable; `FactionRelations.IsHostile/IsAllied` decides
   who fights whom (players on different teams are hostile).

## Common

The base module: ownership, identity, health and the helpers every other module uses.

| Type | Role |
| --- | --- |
| `Faction` | Owner faction on every ownable entity (0 = neutral) |
| `FactionRelations`, `Relation` | Singleton: team per faction. `IsHostile`/`IsAllied`, and `RelationOf` (Own, Ally, Enemy, Neutral) for colour-coding |
| `Player`, `PlayerLookup` | Player entity identity (faction, name, colour; team from `FactionRelations.TeamOf`); faction → player entity, rebuilt per frame for jobs |
| `LocalPlayer`, `Defeated` | The player this client controls; enabled once a player is defeated |
| `MapSettings` | Singleton: playable bounds, grid resolutions, water level, flooding, max slope |
| `UnitTag`, `BuildingTag` | Marks a movable, orderable unit; marks a static structure |
| `Inside` | Enabled while a passenger is aboard its `Container` (Transport): hidden, untargetable, carried along |
| `Health`, `Dead` | Hit points (`Health.IsAlive` treats a missing entity as dead); death marker enabled the frame before destruction |
| `ConstructionProgress` | Enableable 0..1; unfinished buildings don't produce, provide population, accept cargo, fire or satisfy prerequisites |
| `EntityInfo` | `TypeId` (hash of the display name: instances of one prefab share it), `Name`, `Icon` |
| `PrefabRegistry` | Singleton `TypeId` → prefab map (`PrefabRegistrySystem`, rebuilt only when prefabs load); resolves network commands, replicated buffers and sounds. `PrefabLookup` builds such a map from any query |
| `IComponentSink` | Target of every `*Setup` helper: `BakerSink` for bakers, `EntityManagerSink` for tests and tools |
| `AuthoringBehaviour` | Base class of every authoring component, engine or game; the inspector, entity summaries and validation find components through it |
| `EnabledExtensions` | `lookup.HasEnabled(entity)` (also on `EntityManager`): has the component and it is enabled. Use it for optional toggles such as `Unpowered`, `Inside`, `Docked` and `Stealthed` |
| `DefaultWorld`, `LiveQuery` | For MonoBehaviours: the world they present, and an entity query recreated when that world is replaced |
| `[Owner]`, `[RequiresAuthoring]` | Owner dropdown of the scene Match's players; an authoring component needed alongside (warns, unlike `[RequireComponent]`) |

## GameEntities

Shared identity for units and buildings.

| Type | Role |
| --- | --- |
| `GameEntityAuthoring` | Base of `UnitAuthoring`/`BuildingAuthoring`: name, icon, owner, health, vision, cost, build time, prerequisites, death spawn, counts-for-victory |
| `GameEntitySpec` + `GameEntitySetup` | The shared values and the component set every unit and building carries, written once and used by bakers, tests and tools |
| `ProducibleBaking` | Bakes the cost and prerequisite lists, shared with upgrades |

## Match (players, teams, victory)

| Type | Role |
| --- | --- |
| `MatchAuthoring` | One per map: map size, nav/fog cell sizes, fog toggle, player slots, AI tuning per difficulty, replay recording |
| `PlayerSlot`, `PlayerControl` | One slot on the Match: name, team, colour, control (LocalHuman, AI, Remote), starting resources, AI difficulty and build order. Its list position + 1 is the faction number |
| `PlayerSetup` | Adds what every player entity carries: `Player`, `Population`, `PowerGrid`, `Defeated`, `PlayerCommand`, `PlayerCommandSubject`, `ResearchedUpgrade` and `ResourceStock` |
| `MatchState` | Singleton: `Playing`/`Ended` and the winning team |
| `MatchRules` | Match singleton: low-power production rate, sell refund, replay recording |
| `VictoryCritical`, `PopulationProvider`, `Population` | Flags and the per-player used/cap count read by the systems below |

Systems: `PopulationSystem` recounts used/cap each frame. `VictorySystem` defeats players who lost every
`VictoryCritical` entity they had and ends the match when one team is left. Players are never defeated before they
first own something, so SubScene streaming at startup is safe.

## Orders

| Type | Role |
| --- | --- |
| `PlayerCommand` / `CommandType` | Smart, Move, AttackMove, Attack, Gather, Build, Stop, SetStance, PlaceBuilding, Produce, CancelProduction, SetRallyPoint, Sell, Repair, Capture, Enter, Unload, UseAbility, UsePower, Patrol, Escort, ReturnToBase, `Custom`+ |
| `Order` / `OrderType` | Move, AttackMove, Attack, Gather, Build, Repair, Capture, Enter, UseAbility, Patrol, Escort, ReturnToBase, `Custom`+; `Argument` carries order data such as the ability id |
| `ActiveOrder`, `QueuedOrder` | Current order and shift-queue |
| `OrderWriter` | `Issue(unit, order, queue)` and `Stop(unit)` |
| `PlayerCommandSubject` | Player buffer: the units a group command lists (as a networked command does), each command owning a range |
| `PlayerCommands` | `Any(mask)` lets command systems skip their job sync on frames without their command types; `Collect` lists who a command addresses: its listed group, else `Unit`, else the selection |

The order and command contracts, plus the generic move and escort behaviours. Systems: `OrderDispatchSystem`
promotes queued orders. `MoveOrderSystem` runs Move/AttackMove/Patrol and completes them on arrival or when a
crowded unit stalls near its goal (`NavTolerances.Crowded`). Patrol is an
attack-move whose finished leg rejoins the back of the queue; the command queues the leg back to where each unit
stood, so units loop between the two points (shift adds more points to the loop). `EscortSystem` keeps an escort
within `FollowDistance` of its friendly ward while combat auto-acquires around it, and ends the order when the ward
dies. `PlayerCommandClearSystem` clears commands at the end of the order phase.

## Commands

Turns unit `PlayerCommand`s into orders. `UnitCommandSystem` resolves each command through `OrderResolver` (Smart:
hostile the weapon can hit → Attack, node → Gather, unfinished allied building → Build, damaged allied building →
Repair, allied container with room → Enter, capturable building → Capture, own airfield → ReturnToBase for pad
users, otherwise Move) and spreads group moves into a box `Formation`. Producer, ability and building commands are
handled by their own modules.

## Navigation and Units

| Type | Role |
| --- | --- |
| `UnitAuthoring` | Move speed, radius, nav layer, population (on top of the common fields) |
| `MoveDestination` | Enableable XZ goal |
| `ReachMath`, `NavTolerances` | Walk-up helpers for orders acting on a target (`InReach`, `Approach`, `MoveTo`); how close counts as arrived or crowded |
| `NavAgent`, `PathWaypoint`, `PathState` | Radius and `NavLayer` (Ground, Naval, Amphibious, Air), remaining path corners, request bookkeeping |
| `NavObstacleAuthoring` / `NavObstacle` | Blocked XZ box (buildings add their footprint automatically) |
| `NavAreaAuthoring` / `NavArea` | XZ box overriding the cells under it: Water, Blocked, or a walkable Deck (bridge) at its height with water kept below for ships |
| `TerrainHeightAuthoring` / `TerrainHeight` | Terrain heights baked into a blob singleton; `Height(xz)` is bilinear and `Raycast` hits the ground, both flat y = 0 without one (`RaycastPlane` for any horizontal plane) |
| `NavGrid` | Singleton grid of `NavSurface` flags (Land, Water, Deck; 0 = blocked) with layer-aware `IsWalkable`, `IsAreaFree`, `HasLineOfSight`, `TryFindNearestWalkable`, plus `SurfaceHeight` / `HeightFor(layer)` |
| `NavSetup`, `ViewGround` | Components for nav areas, obstacles and the terrain; ground points along a camera's view rays (camera, minimap, audio listener) |

`MatchAuthoring` sets the water level (the surface ships ride at, used by Water areas), an opt-in flood toggle
(`MapSettings.FloodTerrain`: terrain below the water level becomes water too; off by default) and an optional max slope
(steeper terrain is blocked). An agent may enter cells that share a flag with its layer, so ships pass under a deck that
tanks drive over.

Systems: `NavGridSystem` classifies each cell once from terrain, slope and, when flooding is on, the water level, then stamps areas (decks
last) and obstacles over that whenever any are added or removed, bumping the version so units re-path (a destroyed
bridge). `PathfindingSystem` runs Burst grid A* (8-way, no corner cutting) over the agent's layer with line-of-sight
smoothing, at most 48 searches per frame, and skips A* when the goal is directly visible. `MovementSystem` follows
waypoints, separates overlapping units of layers that share a surface and never steps into cells closed to the
agent's layer. Units steer on XZ; with a grid, Y follows the ground, deck or water level of the agent's layer. Air
units skip all of that: no A*, straight lines over obstacles, water and slopes, and they only separate from other
aircraft (see Air).

**Decision:** grid A* + smoothing + separation. Flow fields were deferred because every unit in a formation has its
own goal. Measured: 500 units crossing a 200 × 200 map with obstacles averaged 0.26 ms per simulation tick.

## Spatial

`SpatialIndex` is a singleton uniform-grid hash of every living entity with `Faction` + `Health`, rebuilt at the
start of the movement phase. Query it with a struct `ISpatialVisitor`. Targeting, separation and the AI use it.
`Closest` is a running nearest-candidate search whose ties go to the lower entity index, so every machine picks the
same entity.

## Combat

| Type | Role |
| --- | --- |
| `SpawnOnDeath` | Wreck prefab spawned on death (`Health` and `Dead` are in Common) |
| `WeaponAuthoring` / `Weapon` | Range, damage, cooldown, damage type, splash radius and edge damage, friendly fire, projectile prefab, acquire range, `WeaponTargets` (Surface, Air or both; default Surface) |
| `AmmoAuthoring` / `Ammo` | Optional rounds: each shot uses one, an empty weapon holds fire (drops its target and attack order), reloads while `Docked` |
| `DamageEvent`, `DamageWriter` | A queued hit (target and/or splash, origin, source); every damage and heal goes through the `DamageQueue` singleton |
| `ArmorFacing` | Front/side/rear multipliers from the Armor component's Directional fields |
| `LastAttacker` | Who last damaged the entity, read on death for kill credit |
| `CombatStance` / `Stance` | Aggressive, Defensive (returns to anchor), HoldPosition, Passive |
| `AttackTarget` | Enableable current target |
| `DamageType` (asset), `ArmorAuthoring` / `ArmorModifier` | Damage multipliers per type |
| `Projectile` | Shot in flight carrying its `DamageEvent` |

Systems in order: `AttackOrderSystem` (explicit Attack orders) → `TargetAcquisitionSystem` (idle, attack-moving,
patrolling, escorting and holding units, plus towers, pick the nearest hostile their weapon can hit, scanning every
4th frame) → `EngagementSystem` (leash,
chase or stop in range) → `WeaponFireSystem` (queue an instant hit or spawn a projectile) → `ProjectileSystem` (home in, queue
the hit on impact) → `DamageSystem` (last in the phase: damage-type armor × facing armor × the DamageTaken stat, splash
falloff, `LastAttacker`; negative amounts heal, and healing splash reaches allies only). Games deal damage by appending
to the queue with a `DamageWriter`. `DeathSystem` runs in the lifecycle phase.

Target layers: aircraft are air targets, everything else is surface. `TargetLookup.IsValidTarget(…, targets)` adds
the layer check, so acquisition, attack orders, engagement and Smart resolution all skip what the weapon can't hit,
and a weapon's splash only reaches its own layers. The skirmish AI keeps air-only units home. Ranges stay on XZ.

Not built in: rotating turrets. Stealth and detection are in Vision.

## Air (aircraft)

| Type | Role |
| --- | --- |
| `FlightAuthoring` / `Flight` | Makes a unit an aircraft (its `NavAgent` goes on the Air layer; a unit set to Air without it is flagged by the validator): cruise altitude, climb speed, loiter radius (0 hovers; above 0 the idle aircraft keeps circling, for jets), uses landing pads |
| `AirfieldAuthoring` / `LandingPad` | Pad offsets on a building, each holding one aircraft (`Aircraft`, null while free) |
| `PadHome` | The aircraft's airfield and pad; null while homeless |
| `Docked` | Enableable (replicated): landed on its pad; ammo reloads, games refuel or repair here |
| `AirfieldRules` | `IsAirfieldOf`: a finished, owned building with pads |

`MovementSystem` flies aircraft at their altitude above the surface below (terrain, water or deck), climbing at
`ClimbSpeed`, and lands them on the surface while `Docked`. An airfield producing a pad user waits for a free pad and
spawns it docked there. `PadSystem` (order phase) claims pads for new aircraft, frees pads whose aircraft died or moved
on, drops the home of aircraft whose airfield was destroyed or captured (they take off and hover), undocks aircraft
that move, and runs ReturnToBase: fly to the home pad, or first claim a free pad at the ordered own airfield, then
dock. `RearmSystem` gives empty aircraft with a home a ReturnToBase order and reloads one round per `ReloadTime` while
docked. Fuel, repair on the pad and pad rules beyond one aircraft each are game code reading `Docked`.

## Stats and Veterancy

| Type | Role |
| --- | --- |
| `StatModifier` / `Stat` | Buffer on every unit and building: `(base + Add) × (1 + ΣPercent)` for MaxHealth, Damage, Range, FireRate, MoveSpeed, VisionRange, DamageTaken, BuildRate, ProductionSpeed (FireRate is shots per second over the weapon cooldown). Values from `Stat.Custom` (128) up belong to games |
| `StatSource` | Who added a modifier: a `StatSourceKind` (Upgrade, Veterancy, Field; `Custom` 128+ for games) and an id unique within that kind |
| `BaseStat`, `StatMath` | Buffer of unmodified values, captured from the live component the first time a stat is applied; `Evaluate`, `Apply` and `RemoveSource` |
| `VeterancyAuthoring` / `Experience`, `VeterancyRank`, `VeterancyBonus` | Experience thresholds and per-rank bonuses |
| `ExperienceValue` | What the killer earns (`experienceValue` on every unit and building) |

Stats depends only on Common. Each module applies the stats of the components it owns, last in the lifecycle phase
and only when the modifier buffer changes: `MaxHealthStatSystem` (keeps the health fraction), `WeaponStatSystem`
(damage, range, fire rate), `MoveSpeedStatSystem`, `VisionStatSystem`, `BuildRateStatSystem` and
`ProductionSpeedStatSystem`; `DamageSystem` reads DamageTaken. A game applies its own `Stat.Custom` values in its
own system with `StatMath.Apply`. `KillCreditSystem` pays a dead entity's experience to its last hostile attacker;
`VeterancySystem` promotes and swaps the rank bonuses. Upgrades and area fields add modifiers the same way.

## Upgrades

| Type | Role |
| --- | --- |
| `UpgradeAuthoring` / `Upgrade`, `UpgradeEffect` | A research prefab: name, icon, cost, time, prerequisites, stat bonuses per target type (none = all) |
| `ResearchedUpgrade` | Player buffer of finished research |
| `AppliedUpgrades` | How many of the owner's upgrades an entity carries |

A `Producer`'s **Research Options** go into its production queue like units (cost, refund, HUD buttons). A finished
upgrade is recorded on the player instead of spawning; `UpgradeSystem` adds its modifiers to every matching owned
entity, catches up later spawns, and swaps sets when an entity changes owner. Each upgrade is researched once.

## Power

`BuildingAuthoring.power` adds a `PowerSupply` (positive generates, negative draws) to a completed building.
`PowerSystem` (end of the order phase) totals each player's `PowerGrid` and enables `Unpowered` on consumers while the
owner draws more than it generates. Unpowered weapons hold fire, unpowered producers run at
`MatchRules.LowPowerProductionRate`, and games read `Unpowered` (`lookup.HasEnabled`) for anything else (radar).

## Repair, sell and capture

| Type | Role |
| --- | --- |
| `SellSystem` | Sell command: refunds `MatchRules.SellRefund` of a finished building (all of a site) plus its queue, removes it without a wreck |
| `RepairSystem`, `BuildingRules.NeedsRepair` | Builders restore an allied finished building at their build speed |
| `CapturableAuthoring` / `Capturable`, `CaptureProgress` | Building others can take over in `captureTime` seconds |
| `CapturerAuthoring` / `Capturer` | Unit that captures, optionally used up on completion |
| `CaptureLookup` | `CanCapture`: capture-target check shared by orders and `CaptureSystem` |

`CaptureSystem` advances progress while capturers are in reach (another player starting over resets it), then
changes the owner at the end of the frame, refunds the building's queue to the old owner and clears its target and
selection. A garrisoned building can't be captured until its passengers are out.

## Transport (garrisons and transports)

| Type | Role |
| --- | --- |
| `ContainerAuthoring` / `Container`, `Cargo` | Capacity, largest passenger size, passengers fire out, passengers survive its death |
| `PassengerAuthoring` / `Passenger`, `PassengerStance` | Size; the stance to restore on exit (`Inside`, in Common, is enabled while aboard) |
| `Boarding` | `CanBoard`, `Board` and `IsInside`, shared by orders and `BoardingSystem` |

`BoardingSystem` runs Enter orders. Aboard, a passenger is dropped from the spatial index (untargetable, no splash),
takes no orders, is deselected and hidden, and holds position (firing out from the container's edge) or goes
passive. `CargoFollowSystem` carries passengers along, `UnloadSystem` handles Unload, and `ContainerDeathSystem`
lets them out, or kills them with credit to the killer, when the container dies.

## Fields

`AreaFieldAuthoring` / `AreaField`, `AreaFieldBonus`: an aura with a radius, who it affects (`FieldTargets`:
own/allies/enemies/neutral × units/buildings), stat bonuses, and healing or damage per second. Every 0.25 s
`AreaFieldSystem` rewrites each entity's `FieldPresence` (one entry per field name, never stacking) and swaps the
bonuses when that changes. Games read `FieldPresence` for their own effects (jamming, network links, air cover).

## Abilities

| Type | Role |
| --- | --- |
| `AbilityAuthoring` / `Ability` | Cooldown abilities on units and buildings: target (none, point, entity) and filter, range (0 = unlimited), required building, built-in effects (spawn a prefab, damage or heal around the target) |
| `AbilityActivation`, `AbilityEvents` | This frame's uses, for game systems to react to |

`AbilityCommandSystem` handles UseAbility (self-targeted: every ready selected caster; aimed: the nearest ready one,
which walks into range, or fires at once if it can't move) and UsePower (an `Ability` buffer on the player entity,
for support powers bought with game rules such as Command Points). `AbilitySystem` ticks cooldowns and runs the
walk-into-range orders.

## Vision

| Type | Role |
| --- | --- |
| `VisionRange` | Sight radius on units and buildings |
| `FogOfWar` | Singleton grid, one bit per team for *visible now*, *explored* and *detected*; `CanSee`/`IsHiddenFrom` combine fog and stealth, `IsCloakedFrom` is the stealth half |
| `StealthAuthoring` / `Stealth` | Enableable (games toggle it at runtime): reveal time after firing, stealthed only when still |
| `Stealthed` | Enableable result (replicated): hidden right now from hostile teams without a detector on it |
| `DetectorAuthoring` / `Detector` | Detection radius, separate from vision |
| `FogHidden`, `LocalFogView` | Hostile roots the local player can't see; who the local player views as (`RevealAll`, set through `LocalFogViewSystem.SetRevealAll`, shows everything, as replays do) |

`FogOfWarSystem` restamps vision and detection 10 times per second; with a `TerrainHeight`, vision is occluded by
hills (`SightLines`: rays from the viewer's eye, 2 m up, keep the steepest sight line so far, so cost stays
proportional to the vision area; with flooding on, terrain below the water level is seen as the water surface). With fog disabled every cell stays visible but detection still runs, so stealth works with fog off. `StealthSystem` (after `WeaponFireSystem`, which restarts the
reveal timer on each shot) sets `Stealthed`. `LocalFogViewSystem` publishes the local player's `LocalFogView` and
tags hostile entities they can't see (fog or undetected stealth) with `FogHidden` once per restamp; selection,
picking, the HUD and rendering all skip tagged entities. `TargetLookup.IsValidTarget` rejects undetected stealthed
targets, so auto-acquisition, attack orders, engagement and command resolution all ignore them; the skirmish AI skips
them and `FogRelevancySystem` doesn't replicate them. Splash still hits them. Showing own stealthed units as
stealthed (e.g. translucent) is left to games, which read `Stealthed`.

## Audio

| Type | Role |
| --- | --- |
| `SoundCue` (asset) | Clips (random pick), volume and pitch ranges, max instances, priority, mixer group, 2D/3D and distance range |
| `EntitySoundsAuthoring` / `EntitySound` | Per-prefab cues by `SoundSlot`: Fire, Impact, Death, Ability, and the owner-only voices Ready, Select, Move, Attack; games add slots from `Custom` |
| `SoundEvent`, `SoundWriter` | This frame's sounds on the `SoundQueue` singleton, named by `EntityInfo.TypeId` + slot + position + owner, so a client resolves them from its own prefab |
| `SoundRules` | `IsAudible`: voices reach only their owner, other sounds whoever sees the source (the fog and stealth rule of `IsHiddenFrom`) |

`SoundClearSystem` empties the queue at the start of every frame in every world. Entities without a cue for a slot
write nothing. `WeaponFireSystem` plays Fire at the shooter and, for instant hits, Impact at the target;
projectiles carry the shooter's type and play Impact on arrival. `DeathSoundSystem` (after `DeathSystem`) plays
Death, `AbilitySoundSystem` plays the caster's Ability cue for each `AbilityActivation`, and `ProductionSystem` and
`ConstructionSystem` play Ready. Games append their own events with a `SoundWriter` (`Play(entity, slot, …)` or
`Add(typeId, slot, …)`) before the end of the lifecycle phase. `AcknowledgementSystem` (client and single player)
plays the Select, Move or Attack voice of one selected or commanded unit, at most once per second. With Netcode,
`SoundSendSystem` forwards each client the events it may hear as `SoundRpc`s (at most 16 per tick) and
`SoundReceiveSystem` queues them on the client. The server never plays audio.

Playback (client): `SoundPlaybackSystem` plays the queue through a pool of 32 audio sources (`SoundPool`: per-cue
instance limit, an important cue takes the voice of the least important one), skipping what the local player can't
hear and 3D sounds beyond the cue's range. `SoundListener` (on `RTSWorld.prefab`'s camera) keeps the only
`AudioListener` on the camera's ground focus point.

## Resources

| Type | Role |
| --- | --- |
| `ResourceType` (asset) | A resource: name, colour, icon |
| `ResourceCost` / `ResourceStock` | Prefab price / player stockpile buffers; `ResourceMath` does the arithmetic |
| `ResourceNodeAuthoring` / `ResourceNode` | Neutral deposit, finite or regrowing |
| `ResourceDropOffAuthoring` | Building where harvesters deposit |
| `HarvesterAuthoring` / `Harvester` / `HarvestState` | Capacity, gather rate, trip state |

Systems: `GatherSystem` runs the node → fill → nearest drop-off → deposit loop and moves to a nearby node of the
same type when one runs dry. `ResourceNodeSystem` regrows or removes nodes.

## Buildings

| Type | Role |
| --- | --- |
| `BuildingAuthoring` | Footprint, population provided, starts-under-construction (`ConstructionProgress` is in Common) |
| `BuilderAuthoring` / `Builder`, `BuildOption` | Build rate and placeable building prefabs |
| `PlacementMath`, `PlacementSurface` | The single source of placement rules, used by the simulation, the ghost and the AI |
| `BuildingRules` | Build and repair target checks shared by orders and behaviours: `IsAlliedSite`, `NeedsRepair` |

Systems: `PlaceBuildingSystem` validates placement (`PlacementMath`: map bounds, no overlap, and plain land, water or
shoreline cells per the prefab's `BuildingPlacement` surface), sets the site on the surface height, charges the
cost, spawns a site and orders the builders to it. `ConstructionSystem` advances sites only while builders work on
them.

## Production

| Type | Role |
| --- | --- |
| `ProducerAuthoring` / `Producer`, `ProductionOption`, `ProductionQueueItem`, `RallyPoint` | Unit training and research queue |
| `Producible` | Build time and population cost, read from prefabs |
| `Prerequisite`, `CompletedBuildings` | Buffer of building `TypeId`s required first; the check shared by the simulation, the AI and the HUD |
| `IPrefabOption`, `ProductionRules` | Option buffers naming prefabs; `Offers`, `Refund` and `RefundQueue`, shared by build, production, sell and capture |

Systems: `ProductionCommandSystem` handles Produce / Cancel (refund) / rally points. `ProductionSystem` trains the
queue head when population allows and sends new units to the rally point.

## AI

| Type | Role |
| --- | --- |
| `AIPlayer` | Marks a computer-controlled player and holds its tuning |
| `AIBuildOrder` (asset) / `AIBuildStep` | Opening per faction: building, unit or upgrade prefabs with a count, in order |
| `AIDifficulty`, `AITuning` | Easy / Normal / Hard presets on `MatchAuthoring`: think interval, attack wave size, ability use |
| `AIPlayerSetup` | Adds `AIPlayer` and its build-order buffer to a player entity |
| `AIPlacement` | Ring search for a free building spot around the AI base, leaving a gap (uses `PlacementMath`) |

`SkirmishAISystem` drives every `AIPlayer` through `PlayerCommand`s only (the same path as a human). Each think it
starts the first unmet build-order step it can afford (existing, queued and unfinished entities count): buildings
are placed near its base by an idle (else the nearest) builder, units and upgrades are queued at a producer. Once
every step is met, idle producers train and research round-robin. Idle builders finish abandoned sites, idle
harvesters gather, ready abilities and player powers fire at the nearest suitable target (hurt allies for heals,
in range unless unlimited; undetected stealth is skipped), and once enough idle combat units exist they attack-move
to the nearest hostile building. Deterministic (ties broken by entity index) and easy to replace: disable it and
write your own.

## Replays

| Type | Role |
| --- | --- |
| `ReplayRecorder` | `Start(world, sampleRate, keyframeInterval)`, `Snapshot(world, scenePath)`, `Stop(world, scenePath)` in the authoritative world; `scenePath` is the scene a viewer opens (the one holding the SubScene). `MatchAuthoring.recordReplay` starts one with the match |
| `Replay`, `ReplaySerializer` | Header (scene, sample rate, duration, players, result) plus samples; `Save`/`Load` as a GZip file |
| `ReplayEntity`, `ReplayFrame`, `ReplayStream` | Quantized entity state (key, type, faction, position, yaw, health, construction); a keyframe or delta; the native sample lists |
| `ReplayViewer` | `Begin(world, replay)` / `Stop` in a local world that loaded `Replay.ScenePath` |
| `ReplayPlayback` | Singleton for a HUD: time, duration, speed, playing; writing the time seeks |
| `ReplayPlaybackState`, `ReplaySceneMatch` | Playback bookkeeping (`Seek(frame)`); pairs recorded entities with the scene's |

`ReplayRecorderSystem` (last in the lifecycle phase) samples every entity with `EntityInfo` + `Faction` at 10 Hz,
writing a keyframe every 10 s and, in between, only spawned or changed entities and removed keys. Replays store
observed state, not commands, so playback never simulates: `ReplayViewer` turns the gameplay phases off and reveals
the fog (`LocalFogView.RevealAll`), and `ReplayPlaybackSystem` takes over the scene's entities (`ReplaySceneMatch`:
same type, nearest position, each used once), spawns later ones from baked prefabs (types without one are skipped
with a warning), destroys removed ones and interpolates transforms between samples. Seeking rebuilds from the
nearest keyframe at or before the time.
**HyperRTS ▸ Replays** saves the Play-mode recording or plays a replay file.

## Selection

`Selectable`/`Selected` (enableable), `SelectionInput` (gesture written by input), `ControlGroup`.
`SelectionSystem` applies clicks, drag-boxes (preferring the local player's units), double-clicks (same `TypeId` on
screen) and control groups.

## Interaction (client UI state)

`PlacementState`, `PendingCommand`, `PointerState`, `SelectionDragState` and `CameraFocusRequest` (minimap clicks)
are singletons that input, the HUD and overlays share. `ClientSingletonSystem` creates them, with `SelectionInput`,
once per presented world, so no layer depends on which starts first.

## Input (client)

`RTSInputActions` (Selection, Commands and Camera maps; `InputActionsProvider` holds the one shared instance and
counts map users), `SelectionInputSystem`, `CommandInputSystem` (right-click Smart, A/S/H, P patrol and E escort +
click, targeted commands), `PlacementInputSystem` (ghost + PlaceBuilding), `WorldPointer` (Unity Physics raycast,
then the baked terrain, then the ground plane), `CameraController` (pan, edge scroll, zoom, rotate, map clamp,
`FocusOn`; it also centres on a pending `CameraFocusRequest`).

## Presentation (client)

`HUD.prefab` bundles the UI Toolkit `HUDController` (resource bar with power, selection panel with queue, command
card with research, abilities and support powers, return to base, unload and sell, minimap, game-over banner with a
`MatchOutcome`). Each widget is an `IHUDPanel` (boxed ones derive from `HUDPanel`) refreshed every frame from a
`HUDContext`; a game adds or replaces panels by subclassing `HUDController` and overriding `CreatePanels`.
`HUDController` is a `PanelContent`, which rebuilds its tree whenever the `PanelRenderer` reloads. The prefab also
carries `OverlayRenderer` (selection rings, health bars, placement ghost, rally markers via
`Graphics.RenderMeshInstanced`), `FogOfWarRenderer` (overlay shader) and the drag-box marquee. `Rendering/` holds the
shared draw helpers (`OverlayMeshes`, `InstanceBatch`, `RenderHierarchy`, `EntityExtent`). `TeamColorSystem`
tints owned meshes with the owner's colour through `URPMaterialPropertyBaseColor`, so use URP Lit materials.
`FogVisibilitySystem` mirrors `FogHidden` and `Inside` onto `DisableRendering` for the entity and its child meshes.
