# Modules

The engine's gameplay is split into modules, each owning a set of components and the systems that run them. This
page shows how they fit together. Each group of modules has its own page. For assemblies, worlds and frame order see
[architecture](architecture.md).

Simulation modules live in `Simulation/<Module>/`. Input and presentation run on clients only.

A player's intent flows through the modules in four steps:

1. Input, the AI or the network writes a `PlayerCommand` on the player entity.
2. Command systems turn it into unit orders through `OrderWriter`: an `ActiveOrder` and a `QueuedOrder` queue.
3. Order behaviours such as move, attack, gather and build carry the order out.
4. They set `MoveDestination` for pathfinding and movement, or `AttackTarget` for engagement and weapons.

## Tiers

Modules sit in tiers. A module may use modules in its own tier or a lower one, never a higher one, and the modules
of one tier never form a cycle. `ModuleLayoutTests` enforces this, and a new module needs a place in its `Tiers`.

| Tier | Modules |
| --- | --- |
| Base | Common |
| Foundation | Animation, Navigation, Vision, Selection, Orders, Cues |
| Gameplay | Combat, Transport, Air, Units, Resources, Production, Upgrades, Buildings, Abilities, Auras, Veterancy, Capture, Match, Status, Airdrops |
| Orchestration | AI, GameEntities, Commands, Replays |

## Pages

| Page | Modules |
| --- | --- |
| [Core](modules/core.md) | Common, GameEntities, Match, Orders, Commands, Selection |
| [Movement](modules/movement.md) | Navigation, Units, Air, Transport |
| [Combat](modules/combat.md) | Combat, Veterancy, Upgrades, Auras, Abilities, Status, Vision |
| [Economy](modules/economy.md) | Resources, Buildings, Production, Capture, Airdrops |
| [Support](modules/support.md) | AI, Cues, Replays, Animation, strategic assets (Airdrops) |
| [Client](modules/client.md) | Interaction, Input, Presentation |
| [Map tools](modules/map-tools.md) | The map builder, Map Editor and map library |
