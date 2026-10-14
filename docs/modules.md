# Modules

What each module owns: the components you author or read and the systems that run them. Assemblies, contract and
frame order: [architecture](architecture.md). Replication: [networking](networking.md).

Simulation modules live in `Simulation/<Module>/` (namespace `HyperRTS.Simulation.<Module>`). Input and
presentation are client only.

```text
Input / AI / network ─PlayerCommand─▶ command systems ─OrderWriter─▶ ActiveOrder / QueuedOrder
                                      (OrderSystemGroup)                  │
                                                                           ▼
             order behaviours (move, attack, gather, build...) ─MoveDestination─▶ pathfinding, movement
                                                                ─AttackTarget───▶ engagement, weapons
```

## Layers

A module uses only modules before it; `Common` is the base. `ModuleLayoutTests.Layers` holds the order:

Common, Animation, Navigation, Stats, Power, Vision, Spatial, Selection, Orders, Audio, Effects, Cues, Combat, Transport,
Air, Units, Resources, Match, Production, Upgrades, Buildings, Abilities, Fields, Veterancy, Capture, AI,
GameEntities, Commands, Replays.

## Pages

| Page | Modules |
| --- | --- |
| [Core](modules/core.md) | Common, GameEntities, Match, Orders, Commands, Selection |
| [Movement](modules/movement.md) | Navigation, Units, Spatial, Air, Transport |
| [Combat](modules/combat.md) | Combat, Stats, Veterancy, Upgrades, Fields, Abilities, Vision |
| [Economy](modules/economy.md) | Resources, Power, Buildings, Production, Capture |
| [Support](modules/support.md) | AI, Audio, Effects, Cues, Replays, Animation |
| [Client](modules/client.md) | Interaction (`HyperRTS.Client`), Input, Presentation |
| [Map tools](modules/map-tools.md) | The map builder, Map Editor and map library (`Editor/Maps/`) |
