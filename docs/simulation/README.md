# Simulation

[Docs](../README.md) ▸ Simulation

The engine's gameplay is split into modules in `Simulation/<Module>/`, each owning a set of components and the systems
that run them. They run on the server and in single player, headless too. This page shows how they fit together; each
group of modules has its own page. For assemblies, worlds and frame order see [architecture](../architecture.md).

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
| Base | Common, Stats |
| Foundation | Navigation, Vision, Selection, Orders, Cues |
| Gameplay | Combat, Destruction, Transport, Air, Movement, Resources, Production, Upgrades, Buildings, Power, Abilities, Auras, Veterancy, Capture, Match, Status, Airdrops, Squads, Death, Strikes |
| Orchestration | AI, GameEntities, MatchStart, Commands, Replays |

## Pages

| Page | Modules |
| --- | --- |
| [Core](core.md) | Common, Stats, GameEntities, MatchStart, Match, Orders, Commands, Selection |
| [Movement](movement.md) | Navigation, Movement, Air, Transport |
| [Driving](driving.md) | Locomotion, Chassis, Drive planner |
| [Combat](combat.md) | Combat, Destruction, Stats, Veterancy, Upgrades, Auras, Abilities, Status, Vision |
| [Weapons](weapons.md) | Trajectories, Salvos, Ammo, Aim, Deploying, Interception, Remains |
| [Squads](squads.md) | Squads, Suppression, Retreat, Reinforce, Cover |
| [Economy](economy.md) | Resources, Buildings, Power, Production, Capture, Airdrops |
| [Support](support.md) | AI, Cues (audio and effects), Replays, Strikes |
