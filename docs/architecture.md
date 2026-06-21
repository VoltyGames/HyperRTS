# Architecture — layered assemblies

HyperRTS first-party code is split into **layered assemblies** under `Assets/Modules/`. The split
exists for one reason: the **simulation must run headless** — a Netcode for Entities server (roadmap
phase 10) and the edit-mode tests both need to run gameplay with **no rendering, input, or UI**.

## The assemblies

| Assembly | Folder | Holds | May reference |
| --- | --- | --- | --- |
| `HyperRTS.Core` | `Core/` | Contracts only: phase `SystemGroups`, `IEntityFactory`, `HyperRTSMenu`/`Icons`/`Docs`. | Entities, Transforms, Mathematics, Collections, Burst |
| `HyperRTS.Simulation` | `Simulation/` | Components, systems, factories, authoring + bakers, `ResourceManager`. The headless gameplay layer. | Core + Entities(.Hybrid), Transforms, Mathematics, Collections, Burst, **Physics** |
| `HyperRTS.Presentation` | `Presentation/` | Rendering (URP base-colour) and UI Toolkit. | Core, Simulation, Entities, **Entities.Graphics** |
| `HyperRTS.Input` | `Input/` | Camera, the input→ECS bridge, generated input actions. | Core, Simulation, Entities, Mathematics, Collections, **InputSystem** |
| `HyperRTS.Editor` | `Editor/` | Editor-only tooling (icon generator, resource editor, custom inspectors, the `GameObject ▸ HyperRTS` rig-prefab menus). | Core, Simulation, Presentation, Collections — `Editor` platform |
| `HyperRTS.Simulation.Tests` | `Simulation/Tests/` | Edit-mode tests. | Core, Simulation — `Editor` platform |

```
            Core            (contracts; no UnityEngine-graphics/input/UI)
             ▲
        Simulation          (headless gameplay — Physics OK)
          ▲      ▲
  Presentation   Input       (client-only: URP/UI, InputSystem)
          ▲      ▲
        Editor (+ Tests → Core, Simulation)
```

The graph is acyclic; topological order `Core → Simulation → {Presentation, Input} → Editor/Tests`.

## The invariant

> `HyperRTS.Simulation` must never reference `Unity.Entities.Graphics`, `Unity.InputSystem`, or
> `UIElements`.

This is what makes a headless server world possible: it loads `Core` + `Simulation` only. Because
DOTS system discovery is global, any client-only system (highlighting, the input bridge) simply
isn't present when those assemblies are absent.

Verify the boundary holds after changes:

```sh
# expect: no matches
grep -rn "UnityEngine.InputSystem\|Unity.Rendering\|UnityEngine.UIElements" Assets/Modules/Simulation
```

## Crossing the boundary: data down, rendering/input up

Components that the simulation **writes** but presentation/input **read** live in `Simulation` (so the
dependency points the right way). Only the actual render override / UI / input bridge live above it.

- **Selection highlight.** `SelectionHighlightColors` (the desired colours, plain `float4`) is set by
  the factory/baker in `Simulation`. `SelectionHighlightColorInitSystem` (Presentation) reactively
  adds the `URPMaterialPropertyBaseColor` render override; `SelectionHighlightSystem` (Presentation)
  drives it from the enableable `Selected` state. A headless server never adds the render component.
- **Drag box.** `SelectionInputSystem` (Input) publishes the `SelectionDragState` singleton (defined
  in `Simulation`); `SelectionDragBoxUI` (Presentation) reads it to draw the marquee. The two client
  layers communicate through ECS data, not a direct assembly reference — there is no Presentation→Input edge.

## Namespaces

Namespaces follow the assembly: `HyperRTS.<Layer>.<Module>` (e.g. `HyperRTS.Simulation.Health`,
`HyperRTS.Presentation.Selection`, `HyperRTS.Input.Cameras`). `HyperRTS.Core` stays flat. A feature
that spans layers (Selection) spans the matching layer namespaces.

## Adding code

- A new gameplay component/system/authoring → `Simulation/<Module>/`, namespace `HyperRTS.Simulation.<Module>`.
- Needs rendering or UI → `Presentation/`. Needs InputSystem → `Input/`. Both reference `Simulation`,
  never the reverse.
- If presentation/input needs a value the simulation produces, put the **data component** in
  `Simulation` and read it from above — don't pull a client package into `Simulation`.
