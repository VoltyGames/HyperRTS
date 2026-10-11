# Architecture

## Assemblies

The engine is split so the simulation runs headless (dedicated server, EditMode tests) with no rendering, input
or UI.

| Assembly | Folder | Holds |
| --- | --- | --- |
| `HyperRTS.Core` | `Core/` | Phase groups, world filters, menu / icon / doc paths |
| `HyperRTS.Simulation` | `Simulation/` | Components, systems, authoring and bakers, `*Setup` helpers, client UI state (`Interaction/`) |
| `HyperRTS.Network` | `Network/` | Session, join, command RPCs, replication, fog relevancy, sound forwarding |
| `HyperRTS.Presentation` | `Presentation/` | HUD, overlays, fog rendering, team colours, audio playback |
| `HyperRTS.Input` | `Input/` | Camera, input actions, input → `PlayerCommand` |
| `HyperRTS.Editor` | `Editor/` | Inspectors, validation, templates, catalog, debug draw, cheats, scene wizard |
| `HyperRTS.*.Tests` | `*/Tests/` | EditMode tests |

```text
                 Core
                  ▲
              Simulation
        ▲         ▲          ▲
   Network   Presentation   Input
        ▲         ▲          ▲
             Editor, Tests
```

Rules:

- `HyperRTS.Simulation` never references `Unity.Entities.Graphics`, `Unity.InputSystem` or `UIElements`. It may use
  Physics and NetCode attributes (`[GhostField]`, ghost authoring).
- Network, Presentation and Input never reference each other. Data they share lives in Simulation.
- Engine code never depends on anything under `Assets/`.

## Modules

Simulation is split into modules, one folder and namespace each (`HyperRTS.Simulation.<Module>`). Modules are
layered, lowest first:

```text
Common < Navigation < Stats < Power < Vision < Spatial < Selection < Orders < Audio < Combat < Transport < Air
  < Units < Resources < Match < Production < Upgrades < Buildings < Abilities < Fields < Veterancy < Capture
  < AI < GameEntities < Commands < Interaction < Replays
```

- A module uses only modules before it. A type two modules need goes in `Common` or the lower module.
- To order two systems, put `[UpdateBefore]`/`[UpdateAfter]` on the one in the higher module.
- `*Authoring.cs` files may use any module; they hold only the authoring class and its `Baker`.
- `ModuleLayoutTests` enforces the order, namespace = folder, and the authoring file rule. Its back-edge
  allowlist has one entry, `Production → Upgrades`, and may only shrink.

Per-module reference: [modules](modules.md).

## Gameplay contract

| Rule | Detail |
| --- | --- |
| Intent is a command | Input, AI and network append `PlayerCommand`s to the player entity. Consumed in `OrderSystemGroup`, cleared at its end |
| Orders go through `OrderWriter` | `Issue(unit, order, queue)` and `Stop(unit)`. The system that owns an order type disables `ActiveOrder` when done |
| Move by destination | Set and enable `MoveDestination`. Navigation disables it on arrival |
| Combat owns movement | While `AttackTarget` is enabled, combat writes `MoveDestination` |
| Ownership | `Faction` on every ownable entity (0 = neutral). `FactionRelations` decides hostility by team |
| Damage goes through the queue | Append a `DamageEvent` with `DamageWriter`, so armor, splash and kill credit apply |
| Spawn baked prefabs | `ecb.Instantiate(prefab)`, then set `LocalTransform` and `Faction` |

## Worlds

| World | Exists in | Runs |
| --- | --- | --- |
| Local | Single player, replays | Everything |
| Server | Dedicated server, host | Gameplay, AI, commands, relevancy |
| Client | Client, host | Selection, input, fog view, presentation, HUD |

- Systems default to the authoritative worlds (`SimulationWorlds.Authoritative`: local and server).
- Client-side systems opt in with `[WorldSystemFilter(SimulationWorlds.Presented)]` (local and client) or
  `SimulationWorlds.All`.
- Single player needs `OverrideAutomaticNetcodeBootstrap` in the scene (it is on `RTSWorld.prefab`); otherwise
  Netcode creates client and server worlds instead of one local world.
- `NetworkSession` creates and replaces worlds at runtime. See [networking](networking.md).

## Frame order

Every gameplay system sits in one of the phase groups from `Core/SystemGroups.cs`, never in
`SimulationSystemGroup` directly. The exception is network plumbing that must run whatever the phase state:
`ClientJoinSystem`, `ServerJoinSystem`, `NetworkStatusSystem`, `FogRelevancySystem`, `ResourceIdSystem`.

```text
InitializationSystemGroup
 └─ PrefabRegistrySystem                    TypeId → prefab map
SimulationSystemGroup
 ├─ OrderSystemGroup
 │   ├─ first   ClientSingletonSystem, MatchSetupSystem, PlayerGhostSystem, LocalGhostActivationSystem,
 │   │          MatchStartSystem, ReferenceResolveSystem, SoundClearSystem, SoundReceiveSystem,
 │   │          SelectionInputSystem, SelectionSystem, CommandInputSystem, PlacementInputSystem,
 │   │          CommandReceiveSystem, SkirmishAISystem
 │   ├─         UnitCommandSystem, AbilityCommandSystem, PlaceBuildingSystem, ProductionCommandSystem,
 │   │          SellSystem, UnloadSystem, SurrenderSystem, AcknowledgementSystem,
 │   │          OrderDispatchSystem, MoveOrderSystem, EscortSystem, RearmSystem, PadSystem
 │   └─ last    PowerSystem, CommandSendSystem, PlayerCommandClearSystem
 ├─ MovementSystemGroup                     before TransformSystemGroup
 │   ├─ first   SpatialIndexSystem, NavGridSystem
 │   └─         PathfindingSystem, MovementSystem, CargoFollowSystem
 ├─ ReplaySystemGroup                       local world; replay playback only
 ├─ CombatSystemGroup
 │   ├─ first   FogOfWarSystem, LocalFogViewSystem
 │   ├─         AttackOrderSystem, TargetAcquisitionSystem, EngagementSystem, WeaponFireSystem,
 │   │          ProjectileSystem, StealthSystem, AbilitySystem, AbilitySoundSystem, AreaFieldSystem
 │   └─ last    DamageSystem
 ├─ ProductionSystemGroup
 │   ├─ first   PopulationSystem
 │   └─         ConstructionSystem, RepairSystem, CaptureSystem, GatherSystem, ResourceNodeSystem,
 │              ProductionSystem, UpgradeSystem, BoardingSystem
 └─ LifecycleSystemGroup
     ├─         DeathSystem, VictorySystem, CasualtyStatsSystem, KillCreditSystem, VeterancySystem,
     │          ContainerDeathSystem, DeathSoundSystem
     └─ last    *StatSystem (MaxHealth, Weapon, MoveSpeed, Vision, BuildRate, ProductionSpeed),
                ReplayRecorderSystem, SoundSendSystem
PresentationSystemGroup
 └─ TeamColorSystem, FogVisibilitySystem, SoundPlaybackSystem   (+ HUD and overlay MonoBehaviours)
```

Order within a row follows each system's `[UpdateBefore]`/`[UpdateAfter]`. Window ▸ Entities ▸ Systems shows the
live order.

- Movement runs before transforms, so a move shows the same frame.
- Lifecycle runs last, so all damage lands before `DeathSystem` marks entities `Dead`. They are destroyed at the
  end of the frame.
- A system that reads `PlayerCommand`s must be in `OrderSystemGroup`.

## Entities at startup

- Gameplay objects (the `Match`, units, buildings, nodes, obstacles) live in a SubScene and bake into entities.
- Prefabs referenced from authoring (production and build options, projectiles, death spawns) bake as entity
  prefabs. Runtime spawns instantiate them, so they render like placed ones.
- SubScenes stream in over the first frames. Guard on singletons with `RequireForUpdate`, and never treat
  "nothing exists yet" as a game state (`VictorySystem` ignores players that never owned anything).
- Never add a system that spawns gameplay entities on its own: discovery is global, so it would also run in test
  worlds. Spawn from a command or an explicit call.
