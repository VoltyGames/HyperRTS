# HyperRTS

A real-time strategy engine for Unity DOTS: Entities, Burst, Jobs and Netcode for Entities. It provides the systems
an RTS needs, and a game supplies the units, factions, maps and rules. Content is authored with components and
prefabs. The simulation runs headless for tests and dedicated servers.

## Features

| Area | Included |
| --- | --- |
| Control | Click, box and double-click selection, control groups, smart right-click, queued orders, attack-move, patrol, escort |
| Movement | Grid A* pathfinding, formations, terrain height, ground, naval, amphibious and air units, bridges |
| Economy | Any number of resources, harvesters, finite or regrowing nodes, power |
| Bases | Building placement and construction, production and research queues, rally points, prerequisites, population, repair, sell |
| Combat | Auto-targeting, stances, instant and projectile weapons, splash, damage types, directional armour, ammo, veterancy |
| Mechanics | Upgrades, area fields, abilities and player powers, capture, garrisons, transports, aircraft, stealth and detection |
| Vision | Per-team fog of war with terrain line of sight |
| Players | Teams, colours, human, AI and remote slots, victory and defeat, skirmish AI with build orders and difficulties |
| Multiplayer | Server-authoritative: host, dedicated server, relay, join and rejoin, per-client fog of war |
| Presentation | UI Toolkit HUD, team colour shader, unit silhouettes through cover, fog rendering, water and foliage shaders, pooled audio |
| Replays | Recording, playback, seek |
| Maps | JSON map specs built into terrain, roads, rivers and scenes, symmetric by sector, with bot hints and a balance report |
| Editor | Tools window, Map Editor with live preview, unit templates, validator with quick fixes, Play-mode cheats and debug overlay |

## Try it

Open the project in **Unity 7000.0.0a11**, open `Assets/Demo/Scenes/SampleScene.unity` and press **Play**. You
start in the south-west base against an AI in the north-east. Controls are in
[getting started](docs/getting-started.md#play).

## Use it in a game

Add this repo as a git submodule, for example at `HyperRTS/` in the game's root. Then reference the package from
`Packages/manifest.json`:

```json
"com.hyperrts.engine": "file:../HyperRTS/Packages/com.hyperrts.engine"
```

Then follow [getting started](docs/getting-started.md).

## Docs

| Doc | For |
| --- | --- |
| [Getting started](docs/getting-started.md) | A first playable skirmish in the editor, no code |
| [Architecture](docs/architecture.md) | Assemblies, worlds, frame order, the gameplay contract |
| [Modules](docs/modules.md) | Components and systems of every module |
| [Extending](docs/extending.md) | Writing game code: mechanics, HUD, AI, front end |
| [Networking](docs/networking.md) | Sessions, join, replication, relay, dedicated server |
| [Editor](docs/editor.md) | Tools window, authoring conventions, validation, debug tools |
| [Maps](docs/maps.md) | Map Editor, spec format, build pipeline, map library |
| [Map tools](docs/modules/map-tools.md) | The map builder's code and extension points |
| [Testing](docs/testing.md) | `TestWorld`, test kits, validation in CI |
| [Roadmap](docs/roadmap.md) | Status and open work |

## Requirements

Unity 7000.0.0a11 (CoreCLR), URP 17.7, Entities 6.7, Entities Graphics, Unity Physics, Netcode for Entities 7.1, Input System.

## License

[MPL-2.0](LICENSE). Changes to HyperRTS files stay open source. Games built on it can be closed source.
