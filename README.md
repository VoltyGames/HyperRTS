# HyperRTS

A real-time strategy engine for Unity DOTS (Entities, Burst, Jobs, Netcode for Entities). It provides the systems
an RTS needs; a game supplies units, factions, maps and rules. Content is authored with components and prefabs,
and the simulation runs headless for tests and dedicated servers.

## Features

| Area | Included |
| --- | --- |
| Control | Click, box and double-click selection, control groups, smart right-click, queued orders, attack-move, patrol, escort, stop, hold |
| Movement | Grid A* with smoothing, separation, formations, terrain height, ground / naval / amphibious / air layers, bridges |
| Economy | Any number of resource types, harvesters, drop-offs, finite or regrowing nodes, power |
| Bases | Snapped placement, builder construction, production and research queues, rally points, prerequisites, population, repair, sell |
| Combat | Auto-targeting, stances, instant and projectile weapons, splash, damage types, directional armor, ammo, veterancy |
| Mechanics | Upgrades, area fields, cooldown abilities and player powers, capture, garrisons and transports, aircraft with airfields, stealth and detection |
| Vision | Per-team fog of war with explored state and terrain line of sight |
| Players | Teams, colours, human / AI / remote slots, victory and defeat, skirmish AI with build orders and difficulties |
| Multiplayer | Server-authoritative Netcode: host, dedicated server, relay, join and rejoin, per-client fog relevancy |
| Presentation | UI Toolkit HUD, team colours, overlays, fog rendering, pooled audio with unit voices |
| Replays | State recording, playback, seek |
| Editor | Scene wizard, role templates, validator with quick fixes, stats catalog and tech tree, Play-mode debug overlay and cheats |

## Try it

Open the project in **Unity 6000.6.4f1**, open `Assets/Demo/Scenes/SampleScene.unity` and press **Play**. You
start in the south-west base against an AI in the north-east. Controls:
[getting started](docs/getting-started.md#play).

## Use it in a game

Add this repo as a git submodule and reference the package from `Packages/manifest.json`:

```json
"com.hyperrts.engine": "file:../External/HyperRTS/Packages/com.hyperrts.engine"
```

Then follow [getting started](docs/getting-started.md).

## Docs

| Doc | For |
| --- | --- |
| [Getting started](docs/getting-started.md) | First playable skirmish in the editor, no code |
| [Architecture](docs/architecture.md) | Assemblies, worlds, frame order, the gameplay contract |
| [Modules](docs/modules.md) | Components and systems of every module |
| [Extending](docs/extending.md) | Game code: new mechanics, HUD, AI, front-end integration |
| [Networking](docs/networking.md) | Sessions, join, replication, relay, fog relevancy |
| [Editor](docs/editor.md) | Menus, authoring conventions, validation, debug tools |
| [Testing](docs/testing.md) | `TestWorld`, test kits, validation in CI |
| [Roadmap](docs/roadmap.md) | Status and open work |

## Requirements

Unity 6000.6.4f1, URP 17.6, Entities 6.6, Entities Graphics, Unity Physics, Netcode for Entities, Input System.

## License

[MPL-2.0](LICENSE): changes to HyperRTS files stay open source; games built on it can be closed source.
