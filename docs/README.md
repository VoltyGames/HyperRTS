# HyperRTS docs

Start with [getting started](getting-started.md) for a first skirmish, then [architecture](architecture.md) for how
the engine fits together. The folders follow the engine's layers.

## Guides

| Page | For |
| --- | --- |
| [Getting started](getting-started.md) | A first playable skirmish in the editor, no code |
| [Architecture](architecture.md) | Assemblies, worlds, frame order, the gameplay contract |
| [Extending](extending.md) | Writing game code: mechanics, HUD, AI, front end |
| [Networking](networking.md) | Sessions, join, replication, relay, dedicated server |
| [Testing](testing.md) | `TestWorld`, test kits, validation in CI |
| [Roadmap](roadmap.md) | Status and open work |

## Simulation

Gameplay modules, server-side and headless. [Overview and tiers](simulation/README.md).

| Page | Modules |
| --- | --- |
| [Core](simulation/core.md) | Common, Stats, GameEntities, MatchStart, Match, Orders, Commands, Selection |
| [Movement](simulation/movement.md) | Navigation, Movement, Air, Transport |
| [Driving](simulation/driving.md) | Locomotion, Chassis, Drive planner |
| [Combat](simulation/combat.md) | Combat, Destruction, Veterancy, Upgrades, Auras, Abilities, Status, Vision |
| [Weapons](simulation/weapons.md) | Trajectories, Salvos, Ammo, Aim, Deploying, Interception, Remains |
| [Squads](simulation/squads.md) | Squads, Suppression, Retreat, Reinforce, Cover |
| [Economy](simulation/economy.md) | Resources, Buildings, Power, Production, Capture, Airdrops |
| [Support](simulation/support.md) | AI, Cues (audio and effects), Replays, Strikes |

## Presentation

What a player sees and touches, on clients only. [Interaction, input, audio and quality](presentation/README.md).

| Page | Covers |
| --- | --- |
| [HUD](presentation/hud.md) | HUD, cursors, unit overlays |
| [Animation](presentation/animation.md) | Bone and vertex animation, machines, motion rig, squad figures |
| [Rendering](presentation/rendering.md) | Team colour, build reveal, visibility, fog of war, flags, destruction, shaders |
| [Environment](presentation/environment.md) | Atmosphere, horizon, wind, water, trample maps, field fires, ambient life |
| [Vegetation](presentation/vegetation.md) | Row crops, the scatter host, foliage lighting |
| [Effects](presentation/effects.md) | Effect rigs, VFX Graph conventions, pools, loop lights |

## Editor and maps

| Page | Covers |
| --- | --- |
| [Editor](editor/README.md) | Menus, Tools window, commands, validation, debug tools |
| [Authoring](editor/authoring.md) | Authoring conventions, inspectors, Hierarchy and Project rows |
| [Maps](maps/README.md) | Workflow, spec format, map library |
| [Map Editor](maps/editor.md) | Editing specs |
| [Map build](maps/build.md) | Build passes, ground rendering, checks, outputs |
| [Map code](maps/code.md) | The code behind the builder, Map Editor and library |
