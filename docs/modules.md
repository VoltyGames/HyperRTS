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

## Tiers

A module uses modules in its own tier or a lower one, and the modules of one tier never use each other in a cycle.
`Common` is the base. `ModuleLayoutTests.Tiers` holds them, lowest first:

| Tier | Modules |
| --- | --- |
| Base | Common |
| Foundation | Animation, Navigation, Vision, Selection, Orders, Cues |
| Gameplay | Combat, Transport, Air, Units, Resources, Production, Upgrades, Buildings, Abilities, Auras, Veterancy, Capture, Match |
| Orchestration | AI, GameEntities, Commands, Replays |

## Pages

| Page | Modules |
| --- | --- |
| [Core](modules/core.md) | Common, GameEntities, Match, Orders, Commands, Selection |
| [Movement](modules/movement.md) | Navigation, Units, Air, Transport |
| [Combat](modules/combat.md) | Combat, Veterancy, Upgrades, Auras, Abilities, Vision |
| [Economy](modules/economy.md) | Resources, Buildings, Production, Capture |
| [Support](modules/support.md) | AI, Cues, Replays, Animation |
| [Client](modules/client.md) | Interaction (`Client/`), Input, Presentation |
| [Map tools](modules/map-tools.md) | The map builder, Map Editor and map library (`Editor/Maps/`) |
