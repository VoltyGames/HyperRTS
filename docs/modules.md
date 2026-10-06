# Module reference

What each engine module owns, the components you author or read, and the systems that run it. Simulation modules
live in `Packages/com.hyperrts.engine/Simulation/<Module>/` (namespace `HyperRTS.Simulation.<Module>`); client-only code is in
`Input/` and `Presentation/`. For the frame order, see [`world-setup.md`](world-setup.md).

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

Shared identity for units and buildings.

| Type | Role |
| --- | --- |
| `GameEntityAuthoring` | Base of `UnitAuthoring`/`BuildingAuthoring`: name, icon, owner, health, vision, cost, build time, prerequisites, death spawn, counts-for-victory |
| `EntityInfo` | `TypeId` (hash of the display name: instances of one prefab share it), `Name`, `Icon` |
| `Producible` | Build time and population cost, read from prefabs |
| `Prerequisite` | Buffer of building `TypeId`s required before producing |
| `GameEntitySetup` + `IComponentSink` | The component set, written once and used by bakers, tests and tools (`BakerSink`, `EntityManagerSink`) |

## Match (players, teams, victory)

| Type | Role |
| --- | --- |
| `MatchAuthoring` | One per map: map size, nav/fog cell sizes, fog toggle, player slots, AI tuning per difficulty |
| `MapSettings` | Singleton: playable bounds and grid resolutions |
| `Player` | Per player entity: faction number, team, name, colour. Also holds `ResourceStock`, `Population`, `PlayerCommand`, `Defeated` |
| `LocalPlayer` / `AIPlayer` | Who drives the player |
| `Faction` | Owner faction on every ownable entity |
| `FactionRelations` | Singleton: team per faction |
| `MatchState` | Singleton: `Playing`/`Ended` and the winning team |
| `VictoryCritical`, `PopulationProvider` | Flags read by the systems below |

Systems: `PopulationSystem` recounts used/cap each frame. `VictorySystem` defeats players who lost every
`VictoryCritical` entity they had and ends the match when one team is left. Players are never defeated before they
first own something, so SubScene streaming at startup is safe.

## Orders

| Type | Role |
| --- | --- |
| `PlayerCommand` / `CommandType` | Smart, Move, AttackMove, Attack, Gather, Build, Stop, SetStance, PlaceBuilding, Produce, CancelProduction, SetRallyPoint, Sell, Repair, Capture, Enter, Unload, UseAbility, UsePower, `Custom`+ |
| `Order` / `OrderType` | Move, AttackMove, Attack, Gather, Build, Repair, Capture, Enter, UseAbility, `Custom`+; `Argument` carries order data such as the ability id |
| `ActiveOrder`, `QueuedOrder` | Current order and shift-queue |
| `OrderWriter` | `Issue(unit, order, queue)` and `Stop(unit)` |
| `PlayerCommands` | `Any(mask)`: lets command systems skip their job sync on frames without their command types |

Systems: `UnitCommandSystem` resolves unit commands through `OrderResolver` (Smart: hostile → Attack, node → Gather,
unfinished allied building → Build, damaged allied building → Repair, allied container with room → Enter,
capturable building → Capture, otherwise Move) and spreads group moves into a box `Formation`. `OrderDispatchSystem` promotes
queued orders. `MoveOrderSystem` runs Move/AttackMove and completes them on arrival or when a crowded unit stalls
near its goal. `PlayerCommandClearSystem` clears commands at the end of the order phase.

## Navigation and Units

| Type | Role |
| --- | --- |
| `UnitAuthoring` | Move speed, radius, population (on top of the common fields) |
| `MoveDestination` | Enableable XZ goal |
| `NavAgent`, `PathWaypoint`, `PathState` | Radius, remaining path corners, request bookkeeping |
| `NavObstacleAuthoring` / `NavObstacle` | Blocked XZ box (buildings add their footprint automatically) |
| `NavGrid` | Singleton walkability grid with `IsAreaFree`, `HasLineOfSight`, `TryFindNearestWalkable` |

Systems: `NavGridSystem` stamps obstacles into the grid when they change. `PathfindingSystem` runs Burst grid A*
(8-way, no corner cutting) with line-of-sight smoothing, at most 48 searches per frame, and skips A* when the goal
is directly visible. `MovementSystem` follows waypoints, separates overlapping units and never steps into blocked
cells. Movement is on the XZ plane; units keep their Y.

**Decision:** grid A* + smoothing + separation. Flow fields were deferred because every unit in a formation has its
own goal. Measured: 500 units crossing a 200 × 200 map with obstacles averaged 0.26 ms per simulation tick.

## Spatial

`SpatialIndex` is a singleton uniform-grid hash of every living entity with `Faction` + `Health`, rebuilt at the
start of the movement phase. Query it with a struct `ISpatialVisitor`. Targeting, separation and the AI use it.

## Combat

| Type | Role |
| --- | --- |
| `Health`, `Dead`, `SpawnOnDeath` | Hit points, death marker (enabled the frame before destruction), wreck prefab |
| `WeaponAuthoring` / `Weapon` | Range, damage, cooldown, damage type, splash radius and edge damage, friendly fire, projectile prefab, acquire range |
| `DamageEvent`, `DamageWriter` | A queued hit (target and/or splash, origin, source); every damage and heal goes through the `DamageQueue` singleton |
| `ArmorFacing` | Front/side/rear multipliers from the Armor component's Directional fields |
| `LastAttacker` | Who last damaged the entity, read on death for kill credit |
| `CombatStance` / `Stance` | Aggressive, Defensive (returns to anchor), HoldPosition, Passive |
| `AttackTarget` | Enableable current target |
| `DamageType` (asset), `ArmorAuthoring` / `ArmorModifier` | Damage multipliers per type |
| `Projectile` | Shot in flight carrying its `DamageEvent` |

Systems in order: `AttackOrderSystem` (explicit Attack orders) → `TargetAcquisitionSystem` (idle, attack-moving
and holding units, plus towers, pick the nearest hostile, scanning every 4th frame) → `EngagementSystem` (leash,
chase or stop in range) → `WeaponFireSystem` (queue an instant hit or spawn a projectile) → `ProjectileSystem` (home in, queue
the hit on impact) → `DamageSystem` (last in the phase: damage-type armor × facing armor × the DamageTaken stat, splash
falloff, `LastAttacker`; negative amounts heal, and healing splash reaches allies only). Games deal damage by appending
to the queue with a `DamageWriter`. `DeathSystem` runs in the lifecycle phase.

Not built in: rotating turrets. Stealth and detection are in Vision.

## Stats and Veterancy

| Type | Role |
| --- | --- |
| `StatModifier` / `Stat` | Buffer on every unit and building: `(base + Add) × (1 + ΣPercent)` for MaxHealth, Damage, Range, FireRate, MoveSpeed, VisionRange, DamageTaken, BuildRate, ProductionSpeed; `Source` identifies who added it |
| `BaseStats`, `StatMath` | Unmodified values (captured on first change); evaluation and `RemoveSource` |
| `VeterancyAuthoring` / `Experience`, `VeterancyRank`, `VeterancyBonus` | Experience thresholds and per-rank bonuses |
| `ExperienceValue` | What the killer earns (`experienceValue` on every unit and building) |

Systems: `StatSystem` (last in the lifecycle phase) rewrites live values when a modifier buffer changes, keeping the
health fraction. `KillCreditSystem` pays a dead entity's experience to its last hostile attacker; `VeterancySystem`
promotes and swaps the rank bonuses. Upgrades and area fields add modifiers the same way.

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
`MatchRules.LowPowerProductionRate`, and games read `Unpowered` for anything else (radar).

## Repair, sell and capture

| Type | Role |
| --- | --- |
| `MatchRules` | Match singleton: low-power production rate, sell refund |
| `SellSystem` | Sell command: refunds `SellRefund` of a finished building (all of a site) plus its queue, removes it without a wreck |
| `RepairSystem`, `RepairRules` | Builders restore an allied finished building at their build speed |
| `CapturableAuthoring` / `Capturable`, `CaptureProgress` | Building others can take over in `captureTime` seconds |
| `CapturerAuthoring` / `Capturer` | Unit that captures, optionally used up on completion |
| `CaptureRules` | Capture-target check shared by orders and `CaptureSystem` |

`CaptureSystem` advances progress while capturers are in reach (another player starting over resets it), then
changes the owner at the end of the frame, refunds the building's queue to the old owner and clears its target and
selection. A garrisoned building can't be captured until its passengers are out.

## Transport (garrisons and transports)

| Type | Role |
| --- | --- |
| `ContainerAuthoring` / `Container`, `Cargo` | Capacity, largest passenger size, passengers fire out, passengers survive its death |
| `PassengerAuthoring` / `Passenger`, `Inside` | Size; `Inside` is enabled while aboard and remembers the stance to restore |
| `Boarding`, `TransportRules` | Who may board what (shared by orders and `BoardingSystem`); `IsInside` |

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
| `FogHidden`, `LocalFogView` | Hostile roots the local player can't see; who the local player views as |

`FogOfWarSystem` restamps vision and detection 10 times per second; with fog disabled every cell stays visible but
detection still runs, so stealth works with fog off. `StealthSystem` (after `WeaponFireSystem`, which restarts the
reveal timer on each shot) sets `Stealthed`. `LocalFogViewSystem` publishes the local player's `LocalFogView` and
tags hostile entities they can't see (fog or undetected stealth) with `FogHidden` once per restamp; selection,
picking, the HUD and rendering all skip tagged entities. `TargetLookup.IsValidTarget` rejects undetected stealthed
targets, so auto-acquisition, attack orders, engagement and command resolution all ignore them; the skirmish AI skips
them and `FogRelevancySystem` doesn't replicate them. Splash still hits them. Showing own stealthed units as
stealthed (e.g. translucent) is left to games, which read `Stealthed`.

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

## Buildings and production

| Type | Role |
| --- | --- |
| `BuildingAuthoring` | Footprint, population provided, starts-under-construction |
| `ConstructionProgress` | Enableable 0..1; unfinished buildings don't produce, provide population, accept cargo, fire or satisfy prerequisites |
| `BuilderAuthoring` / `Builder`, `BuildOption` | Build rate and placeable building prefabs |
| `ProducerAuthoring` / `Producer`, `ProductionOption`, `ProductionQueueItem`, `RallyPoint` | Unit training queue |
| `CompletedBuildings` | Prerequisite checks shared by the simulation, the AI and the HUD |

Systems: `PlaceBuildingSystem` validates placement (`PlacementMath`: map bounds, free nav cells, no overlap), charges
the cost, spawns a site and orders the builders to it. `ConstructionSystem` advances sites only while builders work
on them. `ProductionCommandSystem` handles Produce / Cancel (refund) / rally points. `ProductionSystem` trains the
queue head when population allows and sends new units to the rally point.

## AI

| Type | Role |
| --- | --- |
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
| `ReplayRecorder` | `Start(world, sampleRate, keyframeInterval)`, `Snapshot`, `Stop` in the authoritative world; `MatchAuthoring.recordReplay` starts one with the match |
| `Replay`, `ReplaySerializer` | Header (scene, sample rate, duration, players, result) plus samples; `Save`/`Load` as a GZip file |
| `ReplayEntity`, `ReplayFrame`, `ReplayStream` | Quantized entity state (key, type, faction, position, yaw, health, construction); a keyframe or delta; the native sample lists |
| `ReplayViewer` | `Begin(world, replay)` / `Stop` in a local world that loaded `Replay.ScenePath` |
| `ReplayPlayback` | Singleton for a HUD: time, duration, speed, playing; writing the time seeks |

`ReplayRecorderSystem` (last in the lifecycle phase) samples every entity with `EntityInfo` + `Faction` at 10 Hz,
writing a keyframe every 10 s and, in between, only spawned or changed entities and removed keys. Replays store
observed state, not commands, so playback never simulates: `ReplayViewer` turns the five gameplay phases off, and
`ReplayPlaybackSystem` takes over the scene's entities (same type, nearest position), spawns later ones from baked
prefabs (types without one are skipped with a warning), destroys removed ones, interpolates transforms between
samples and shows everything (no fog). Seeking rebuilds from the nearest keyframe at or before the time.
**HyperRTS ▸ Replays** saves the Play-mode recording or plays a replay file.

## Selection and Interaction (client state shared with the simulation)

`Selectable`/`Selected` (enableable), `SelectionInput` (gesture written by input), `SelectionDragState`,
`ControlGroup`. `SelectionSystem` applies clicks, drag-boxes (preferring the local player's units),
double-clicks (same `TypeId` on screen) and control groups. `PlacementState`, `PendingCommand` and `PointerState`
are singletons that input and the HUD share. `PlacementMath` is the single source of placement rules.

## Input (client)

`RTSInputActions` (Selection, Commands and Camera maps), `SelectionInputSystem`, `CommandInputSystem` (right-click
Smart, A/S/H, targeted commands), `PlacementInputSystem` (ghost + PlaceBuilding), `WorldPointer` (Unity Physics
raycast + ground plane), `CameraController` (pan, edge scroll, zoom, rotate, map clamp, `FocusOn`).

## Presentation (client)

`HUD.prefab` bundles the UI Toolkit `HUDController` (resource bar with power, selection panel with queue, command
card with research, abilities and support powers, unload and sell, minimap, game-over banner), `OverlayRenderer` (selection rings, health bars, placement ghost, rally markers via
`Graphics.RenderMeshInstanced`), `FogOfWarRenderer` (overlay shader) and the drag-box marquee. `TeamColorSystem`
tints owned meshes with the owner's colour through `URPMaterialPropertyBaseColor`, so use URP Lit materials.
`FogVisibilitySystem` mirrors `FogHidden` and `Inside` onto `DisableRendering` for the entity and its child meshes.
