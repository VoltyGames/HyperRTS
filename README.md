# HyperRTS

A reusable real-time strategy engine for Unity, built on DOTS (Entities, Burst, Jobs). HyperRTS gives you the
systems every RTS needs, so you can spend your time on your game's units, factions and maps instead of
reinventing selection boxes and pathfinding.

Everything is authored in the Editor with components and prefabs, and the simulation runs headless, so it's ready
for tests and a dedicated server.

## What's in the box

- **Selection and commands:** click, drag-box and double-click selection, control groups, right-click smart
  commands, shift-queued orders, attack-move, stop, hold position.
- **Pathfinding:** Burst grid A* with path smoothing, unit separation and group formations. Measured at 0.26 ms
  per tick for 500 units.
- **Economy:** any number of resource types, harvesters, drop-offs, finite or regrowing deposits.
- **Bases:** building placement with a snapped preview, builder-driven construction, production queues, rally
  points, prerequisites, population cap.
- **Combat:** auto-targeting, stances, instant-hit and projectile weapons, damage types and armor, death hooks.
- **Fog of war:** per-team vision with explored and visible states, an overlay shader, hidden enemies.
- **Players:** teams, colours, human or AI control, victory and defeat.
- **Skirmish AI:** gathers, trains and attacks through the same commands a player uses.
- **HUD:** resource bar, selection panel, command card with costs and queue, minimap, game-over banner (UI
  Toolkit).
- **Editor tools:** a one-click RTS scene wizard, GameObject templates, inspector warnings, gizmos.

## Try it

1. Open the project in **Unity 6000.6.4f1**.
2. Open `Assets/Demo/Scenes/SampleScene.unity` and press **Play**.
3. You start in the south-west base against an AI opponent in the north-east. Select the Dozer to build, put the
   Supply Trucks to work on the yellow supply piles (right-click), train Rangers at the Barracks, and defend.

| Input | Action |
| --- | --- |
| Left click / drag | Select |
| Right click | Move, attack, harvest, build, or set rally point |
| Shift | Add to selection / queue orders |
| A + click, S, H | Attack-move, stop, hold position |
| Ctrl+1-5, 1-5 | Control groups |
| Arrow keys, screen edge, scroll, middle drag | Camera |

## Build your own game

Start with **[docs/getting-started.md](docs/getting-started.md)**: it walks from **HyperRTS ▸ Create RTS Scene**
to a playable skirmish with your own units and buildings, and shows how to add mechanics in code.

| Doc | Read it for |
| --- | --- |
| [getting-started.md](docs/getting-started.md) | Your first game, step by step |
| [modules.md](docs/modules.md) | Every module's components and systems |
| [architecture.md](docs/architecture.md) | Assembly layering and the headless rule |
| [world-setup.md](docs/world-setup.md) | Frame order, spawning, tests |
| [selection.md](docs/selection.md) | Selection and input bindings |
| [editor-ux.md](docs/editor-ux.md) | Menus, templates and authoring conventions |
| [roadmap.md](docs/roadmap.md) | What's done and what's next (multiplayer) |

## Layout

```text
Assets/Modules/   the engine: Core, Simulation, Presentation, Input, Editor, Prefabs
Assets/Demo/      the sample game (prefabs, data, scene)
docs/             documentation
```

## Requirements

Unity 6000.6.4f1, URP 17.6, Entities 6.6, Entities Graphics, Unity Physics, Input System. New to DOTS? Start with
the [Entities manual](https://docs.unity3d.com/Packages/com.unity.entities@latest).
