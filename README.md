# HyperRTS

A real-time strategy engine for Unity DOTS: Entities, Burst, Jobs and Netcode for Entities. It provides the systems
an RTS needs, and a game supplies the units, sides, maps and rules. Content is authored with components and
prefabs. The simulation runs headless for tests and dedicated servers.

## Features

| Area | Included |
| --- | --- |
| Control | Selection, control groups, smart right-click, queued orders, attack-move, patrol, escort |
| Movement | Grid pathfinding, formations, ground, naval, amphibious and air units, bridges |
| Economy and bases | Any number of resources, harvesters, power, construction, production and research queues, prerequisites |
| Combat | Auto-targeting, stances, projectile and instant weapons, splash, damage types, directional armour, veterancy |
| Mechanics | Upgrades, auras, abilities, capture, garrisons, transports, stealth and detection |
| Vision | Per-team fog of war with terrain line of sight |
| Players | Teams, human, AI and remote slots, victory and defeat, skirmish AI with build orders and difficulties |
| Multiplayer | Server-authoritative: host, dedicated server, relay, rejoin, per-client fog of war |
| Presentation | UI Toolkit HUD, team colours, fog rendering, bone-animated infantry, pooled audio, replays |
| Maps | JSON map specs built into terrain, roads, rivers and scenes, with a balance report |
| Editor | Tools window, Map Editor, validator with quick fixes, Play-mode cheats and debug overlay |

## Try it

Open the project in **Unity 7000.0.0a16**, open `Assets/Demo/Scenes/SampleScene.unity` and press **Play**. You
start in the south-west base against an AI in the north-east. Controls are in
[getting started](docs/getting-started.md#play).

## Use it in a game

Add this repo as a git submodule, for example at `HyperRTS/`, and reference the package from
`Packages/manifest.json`:

```json
"com.hyperrts.engine": "file:../HyperRTS/Packages/com.hyperrts.engine"
```

Then follow [getting started](docs/getting-started.md).

## Docs

The [docs index](docs/README.md) covers it all. Start with [getting started](docs/getting-started.md) and
[architecture](docs/architecture.md); the [roadmap](docs/roadmap.md) has status and open work.

## Requirements

Unity 7000.0.0a16 (CoreCLR), URP 17.7, Entities 7.0, Entities Graphics, Netcode for Entities 7.2, Input System.

## License

[MPL-2.0](LICENSE). Changes to HyperRTS files stay open source. Games built on it can be closed source.
