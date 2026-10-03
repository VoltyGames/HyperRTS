# Selection and input

Mouse and keyboard selection of rendered entities. It spans the layered assemblies (see
[`architecture.md`](architecture.md)): the simulation part (system, math, components) is in
[`Simulation/Selection/`](../Assets/Modules/Simulation/Selection/), the input bridge in
[`Input/Selection/`](../Assets/Modules/Input/Selection/), and the visuals (rings, marquee, HUD panel) in
[`Presentation/`](../Assets/Modules/Presentation/).

## Pipeline

`Camera` and input are managed; the per-entity work is Burst. They meet through a singleton:

```text
SelectionInputSystem (Input)  ──writes──▶  SelectionInput  ──read──▶  SelectionSystem (Simulation, Burst)
   Camera + RTSInputActions                                           raycast / box test / groups → Selected
OverlayRenderer, HUD (Presentation) ──read Selected──▶ rings, health bars, selection panel, command card
```

Because input is plain data, selection is tested by injecting `SelectionInput` directly (no camera): see
`SelectionSystemTests` and `SelectionRulesTests`.

## Components

| Component | Purpose |
| --- | --- |
| `Selectable` | Tag: the entity can be selected. Added to every unit and building by `GameEntitySetup` |
| `Selected` | Enableable: membership is the enabled bit, so selecting causes no structural change |
| `EntityInfo.TypeId` | Double-click selects every on-screen entity with the clicked entity's type |
| `ControlGroup` | Bit mask of control groups, added on first assignment |
| `SelectionDragState` | Marquee rectangle for the drag-box UI |

`SelectionSystem` toggles `Selected` with `EnabledRefRW` over a `WithPresent<Selected>` query. Click-picking uses a
Unity Physics raycast against the baked colliders, so selectable prefabs need a collider.

## Input

`Input/RTSInputActions.inputactions` (codegen on, wrapper `HyperRTS.Input.RTSInputActions`) has three maps:

| Map | Bindings |
| --- | --- |
| Selection | `Select` left click/drag, `Additive` Shift, `Subtract` Ctrl, `Group1-5` keys 1-5, `AssignGroup` Ctrl |
| Commands | `Command` right click, `Confirm` left click, `AttackMove` A, `Stop` S, `HoldPosition` H, `Queue` Shift, `Cancel` Esc |
| Camera | `Pan` arrow keys, `Zoom` scroll, `Rotate` middle-drag (`Look`), `Reset` Home |

Short press = click; drag past a threshold = box; two quick clicks = double-click. Shift adds, Ctrl removes, no
modifier replaces. A drag box prefers the local player's units, then its buildings, then everything in the box;
a click selects anything (enemies for info).

Control groups: Ctrl+N stores the local player's selected entities in group N (a `ControlGroup` bit mask
component added on first assignment), N recalls the group, Shift+N adds it to the selection.

Clicks over the HUD (`PointerState.OverUI`), in placement mode (`PlacementState.Active`) or with a targeted
command armed (`PendingCommand`) never reach selection; `CommandInputSystem` and `PlacementInputSystem` turn them
into `PlayerCommand`s instead.

## Verify

- Tests: Test Runner ▸ EditMode ▸ `SelectionSystemTests`, `SelectionRulesTests`.
- Play `SampleScene`: click shows a ring and the selection panel, ground-click clears, box selects your units,
  Shift/Ctrl modify, double-click selects every unit of that type on screen, Ctrl+1 / 1 store and recall.
