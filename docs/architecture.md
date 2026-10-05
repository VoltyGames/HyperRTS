# Architecture: layered assemblies

HyperRTS code is split into **layered assemblies** under `Assets/Modules/`, for one reason: the **simulation must
run headless**. A dedicated server (roadmap phase 10) and the EditMode tests run gameplay with **no rendering,
input or UI**.

## The assemblies

| Assembly | Folder | Holds | May reference |
| --- | --- | --- | --- |
| `HyperRTS.Core` | `Core/` | Contracts only: phase `SystemGroups`, `HyperRTSMenu`/`Icons`/`Docs` | Entities, Transforms, Mathematics, Collections, Burst |
| `HyperRTS.Simulation` | `Simulation/` | Components, systems, authoring + bakers, setup helpers. The headless gameplay layer | Core, Entities(.Hybrid), Transforms, Mathematics, Collections, Burst, **Physics** |
| `HyperRTS.Presentation` | `Presentation/` | Team colours, overlays, fog rendering, UI Toolkit HUD | Core, Simulation, Entities, **Entities.Graphics**, Transforms |
| `HyperRTS.Input` | `Input/` | Camera, input → command bridge, input actions | Core, Simulation, Entities, Transforms, **Physics**, **InputSystem** |
| `HyperRTS.Editor` | `Editor/` | Validator, inspectors and handles, templates, catalog, Play-mode debug and cheats, scene wizard | Core, Simulation, Presentation, Entities, Scenes, NetCode, `Editor` platform |
| `HyperRTS.Simulation.Tests` | `Simulation/Tests/` | EditMode tests through `TestWorld` | Core, Simulation |
| `HyperRTS.Presentation.Tests` | `Presentation/Tests/` | EditMode tests for presentation helpers and systems | Core, Simulation, Presentation |
| `HyperRTS.Editor.Tests` | `Editor/Tests/` | Validation rules, templates, and a project-wide "no validation errors" check | Core, Simulation, Editor |

```text
            Core            (contracts; no graphics/input/UI)
             ▲
        Simulation          (headless gameplay; Physics OK)
          ▲      ▲
  Presentation   Input       (client only: URP/UI, InputSystem)
          ▲      ▲
        Editor (+ Tests)
```

## The invariant

> `HyperRTS.Simulation` must never reference `Unity.Entities.Graphics`, `Unity.InputSystem` or `UIElements`.

A headless world loads `Core` + `Simulation` only. System discovery is global, so client systems (team colours,
input bridge) simply don't exist when their assemblies are absent.

```sh
# expect: no matches
grep -rn "UnityEngine.InputSystem\|Unity.Rendering\|UnityEngine.UIElements" Assets/Modules/Simulation
```

## Crossing the boundary: data down, rendering and input up

Data that the simulation owns but clients read, or that two client layers share, lives in `Simulation`.
Presentation and Input never reference each other.

- **Commands.** Input (and the AI) write `PlayerCommand`s to the player entity. This is the only way intent enters
  the simulation, and it is the message a networked build would send.
- **Selection.** `SelectionInputSystem` (Input) writes the `SelectionInput` gesture; `SelectionSystem` (Simulation)
  toggles `Selected`; the HUD and overlays read `Selected`.
- **Placement and targeted commands.** The HUD starts placement by writing `PlacementState` (or arms attack-move
  via `PendingCommand`); `PlacementInputSystem` moves the ghost and confirms with a `PlaceBuilding` command; the
  overlay draws the ghost. `PlacementMath` holds the rules both sides use.
- **UI hit-testing.** The HUD writes `PointerState.OverUI`; input ignores world clicks while it is set.
- **Visuals from data.** Team colour comes from `Player.Color`, fog hiding from `FogOfWar`, health bars from `Health`.
  A headless world never adds any render component.

## Namespaces

Namespaces follow the assembly: `HyperRTS.<Layer>.<Module>` (`HyperRTS.Simulation.Combat`,
`HyperRTS.Presentation.Fog`, `HyperRTS.Input.Cameras`). `HyperRTS.Core` stays flat. A feature that spans layers
(Selection) spans the matching layer namespaces.

## Adding code

- Gameplay component, system or authoring → `Simulation/<Module>/`, namespace `HyperRTS.Simulation.<Module>`.
- Needs rendering or UI → `Presentation/`. Needs InputSystem → `Input/`. Both reference `Simulation`, never the
  reverse.
- If presentation or input needs a value the simulation produces, put the **data component** in `Simulation` and
  read it from above.
- A game built on the engine adds its own assemblies on top with the same layering. See
  [`getting-started.md`](getting-started.md#9-adding-your-own-mechanics).
