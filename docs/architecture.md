# Architecture

How the engine is split, the rules every system follows, which world runs what, and the order of a frame.

## Assemblies

The simulation has no rendering, input or UI, so it runs on a dedicated server and in EditMode tests.

| Assembly | Holds |
| --- | --- |
| `HyperRTS.Simulation` | Components, systems, authoring, phase groups. Includes `Core/` |
| `HyperRTS.Network` | Sessions, join, command RPCs, replication, fog relevancy |
| `HyperRTS.Presentation` | HUD, overlays, fog rendering, team colours, audio. Includes `Client/` |
| `HyperRTS.Input` | Camera and input, turned into `PlayerCommand`s |
| `HyperRTS.Editor` | Tools window, inspectors, validation, debug tools, Map Editor |

`Network` and `Presentation` build on `Simulation`. `Input` builds on `Presentation`. `Editor` and the tests sit on
top. A server build leaves out client systems by world filter, not by assembly.

## Modules

The simulation is split into modules. Each is one folder with the namespace `HyperRTS.Simulation.<Module>`, and sits
in one of the [tiers](modules.md#tiers).

- A module uses its own tier or lower ones, never in a cycle. Put a shared type in `Common` or the lower module.
- To order two systems, put `[UpdateBefore]` or `[UpdateAfter]` on the one whose module uses the other's.
- `*Authoring.cs` files may use any module. They hold only the authoring class and its `Baker`.

`ModuleLayoutTests` enforces these rules. Per-module reference: [modules](modules.md).

## Gameplay contract

| Rule | How |
| --- | --- |
| Intent is a command | Input, AI and network append `PlayerCommand`s to the player entity |
| Orders go through `OrderWriter` | The system owning an order type disables `ActiveOrder` when done |
| Move by destination | Set and enable `MoveDestination`. It is disabled on arrival |
| Combat owns movement | While `AttackTarget` is enabled, combat writes `MoveDestination` |
| Ownership | `Faction` on every ownable entity, 0 for neutral. `FactionRelations` decides hostility |
| Damage goes through the queue | `DamageWriter`, so armor, splash and kill credit apply |
| Spawn baked prefabs | `ecb.Spawn(prefab, prefabTransform, stretches, position, rotation, faction)` |

## Worlds

| World | Exists in | Runs |
| --- | --- | --- |
| Local | Single player, replays | Everything |
| Host | A player-hosted match | Everything a server and a client run, but `[RemoteClientOnly]` systems |
| Server | Dedicated server | Gameplay, AI, commands, relevancy |
| Client | A joining player | Selection, input, fog view, presentation, HUD |

Systems run in local and server worlds by default. A client-side system adds
`[WorldSystemFilter(SimulationWorlds.Presented)]`, unless it sits under `PresentationSystemGroup`, which already
defaults to the local and client worlds. Single player needs `OverrideAutomaticNetcodeBootstrap` in the
scene, which `RTSWorld.prefab` carries. `NetworkSession` creates and replaces worlds at runtime. See
[networking](networking.md).

## Frame order

Every gameplay system sits in a phase group from `Core/SystemGroups.cs`, never in `SimulationSystemGroup` directly.
Only network plumbing such as join and relevancy sits there.

| Group | Runs |
| --- | --- |
| `OrderSystemGroup` | Match setup, selection, `PlayerCommand`s, AI, orders, power |
| `MovementSystemGroup` | Spatial index, nav grid, pathfinding, movement |
| `ReplaySystemGroup` | Replay playback |
| `CombatSystemGroup` | Fog, targeting, weapons, abilities, fields. `DamageSystem` last |
| `ProductionSystemGroup` | Construction, repair, capture, gathering, production, upgrades, boarding |
| `LifecycleSystemGroup` | Death, victory, veterancy. Stats, replay recording and cue sending last |
| `PoseSystemGroup` | Presented worlds, first in `TransformSystemGroup`: rig, machine and falling tree poses |
| `PresentationSystemGroup` | Team colours, fog visibility, sound playback |

- A system that reads `PlayerCommand`s must be in `OrderSystemGroup`.
- Movement runs before transforms, so a move shows the same frame.
- All damage lands before `DeathSystem` marks entities `Dead`. They are destroyed at the end of the frame.

**Window ▸ Entities ▸ Systems** shows the full live order.

## Entities at startup

- Gameplay objects live in a SubScene and bake into entities. Prefabs they reference bake as entity prefabs, so
  runtime spawns render like placed ones.
- SubScenes stream in over the first frames. Guard on singletons with `RequireForUpdate`, and never treat "nothing
  exists yet" as a game state.
- Never add a system that spawns gameplay entities on its own. It would also run in test worlds. Spawn from a
  command or an explicit call.
