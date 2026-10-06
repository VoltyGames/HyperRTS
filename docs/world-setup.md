# World setup: how entities enter play and the frame pipeline

There is **no custom bootstrapper**. HyperRTS uses Unity's default DOTS world plus five ordered phase groups.

## The default world

On Play, `DefaultWorldInitialization` creates the default `World`, discovers every system in every loaded
assembly and adds each to its `[UpdateInGroup]` target. Discovery is global, so a headless build that leaves out
`Presentation`/`Input` just has fewer systems.

Netcode for Entities is installed but its automatic client/server bootstrap is turned off by the
`OverrideAutomaticNetcodeBootstrap` component on `RTSWorld.prefab`. Keep that rig (or the component) in
single-player scenes, or Netcode creates client and server worlds instead of the default one.

## Entities from the SubScene

Everything gameplay-relevant (the `Match`, units, buildings, nodes, obstacles) sits in a **SubScene** and bakes
into entities. Prefabs referenced from authoring (producer options, build options, projectiles, death spawns) bake
as **entity prefabs** (`Prefab` tag) that systems instantiate at runtime, so spawned units render like placed ones.

SubScenes stream in over the first frames. Systems that need match data use `RequireForUpdate` on the singleton,
and `VictorySystem` never defeats a player that hasn't owned anything yet.

## Frame order

Defined in [`SystemGroups.cs`](../Packages/com.hyperrts.engine/Core/SystemGroups.cs). Every system lives in one of these groups,
never in `SimulationSystemGroup` directly.

```text
InitializationSystemGroup
 └─ PrefabRegistrySystem                               — TypeId → prefab map, rebuilt when prefabs load
SimulationSystemGroup
 ├─ OrderSystemGroup
 │   ├─ (first) SelectionInputSystem → SelectionSystem → CommandInputSystem → PlacementInputSystem,
 │   │          SkirmishAISystem                       — gestures and AI become PlayerCommands
 │   ├─ UnitCommandSystem, PlaceBuildingSystem,
 │   │  ProductionCommandSystem                        — commands become orders, sites, queue items
 │   ├─ OrderDispatchSystem → MoveOrderSystem          — next queued order; move goals
 │   ├─ RearmSystem → PadSystem, EscortSystem           — rearm cycle, pads, return to base; escorts follow
 │   └─ (last) PlayerCommandClearSystem
 ├─ MovementSystemGroup                                (before TransformSystemGroup)
 │   ├─ (first) SpatialIndexSystem, NavGridSystem
 │   └─ PathfindingSystem → MovementSystem
 ├─ ReplaySystemGroup                                  (local world, before TransformSystemGroup)
 │   └─ ReplayPlaybackSystem                           — replay playback; the gameplay phases are off meanwhile
 ├─ CombatSystemGroup
 │   ├─ (first) FogOfWarSystem
 │   └─ AttackOrderSystem → TargetAcquisitionSystem → EngagementSystem → WeaponFireSystem → ProjectileSystem,
 │      StealthSystem (after WeaponFireSystem)
 ├─ ProductionSystemGroup
 │   ├─ (first) PopulationSystem
 │   └─ ConstructionSystem, GatherSystem → ResourceNodeSystem, ProductionSystem
 └─ LifecycleSystemGroup
     ├─ DeathSystem → VictorySystem
     ├─ (last) MaxHealthStatSystem, WeaponStatSystem, MoveSpeedStatSystem, VisionStatSystem,
     │         BuildRateStatSystem, ProductionSpeedStatSystem — re-apply stats whose modifiers changed
     └─ (last) ReplayRecorderSystem                    — samples the frame while recording
PresentationSystemGroup
 └─ TeamColorSystem, FogVisibilitySystem               (+ HUD/overlay MonoBehaviours reading ECS)
```

Movement runs before `TransformSystemGroup`, so a move shows the same frame. Lifecycle runs last, so all damage
lands before `DeathSystem` marks entities `Dead` and queues their destruction on
`EndSimulationEntityCommandBufferSystem`.

**New systems pick a phase group** with `[UpdateInGroup(typeof(<Phase>SystemGroup))]`. A system that consumes
`PlayerCommand`s must be in `OrderSystemGroup` (commands are cleared at its end).

## Creating entities from code

Instantiate baked prefabs through a command buffer and set the owner and position:

```csharp
var unit = ecb.Instantiate(prefab);                 // real entity in Entities 6.6, usable right away
ecb.SetComponent(unit, LocalTransform.FromPosition(position));
ecb.SetComponent(unit, new Faction { Value = owner });
```

To order it around, append a `PlayerCommand` (`Unit = unit`) to its owner's buffer, or use `OrderWriter` from a
system.

For tests and tools without baking, `GameEntitySetup`, `UnitSetup` and `BuildingSetup` write the exact baked
component set through an `IEntityWriter` (`EntityManagerWriter`). Those entities have no mesh, so they only show up
in **Window ▸ Entities**.

## Don't auto-spawn on startup

Don't add an auto-running system that spawns gameplay entities: discovery is global, so it would also run in test
worlds and duplicate SubScene content. Make code spawning opt-in (a command, a `[DisableAutoCreation]` system, or a
static helper called explicitly).

## Tests

`TestWorld` (in `Simulation/Tests/`) builds an isolated world with every `HyperRTS.Simulation` system, and has
helpers: `CreateMatch(teams...)`, `SpawnUnit`, `SpawnBuilding`, `MakePrefab`, `Command`, `Tick`, `Run(seconds)`.
Write tests end-to-end through systems:

```csharp
using var world = new TestWorld();
world.CreateMatch(1, 2);
var unit = world.SpawnUnit(1, float3.zero);
world.Command(1, new PlayerCommand { Type = CommandType.Move, Unit = unit, Position = new float3(10, 0, 0) });
world.Run(3f);
```

## Verify

- **Window ▸ Entities ▸ Systems** shows the tree above.
- **Window ▸ General ▸ Test Runner ▸ EditMode**: run all.
