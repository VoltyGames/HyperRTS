# Selection & input (Phase 1)

Mouse selection for rendered entities. It spans the layered assemblies (see
[`architecture.md`](architecture.md)): the sim core (system, math, components) in
[`Simulation/Selection/`](../Assets/Modules/Simulation/Selection/) (`HyperRTS.Simulation.Selection`),
the input bridge in [`Input/Selection/`](../Assets/Modules/Input/Selection/) (`HyperRTS.Input.Selection`),
and rendering/UI in [`Presentation/Selection/`](../Assets/Modules/Presentation/Selection/)
(`HyperRTS.Presentation.Selection`).

## Architecture

`Camera`/Input are managed; the per-entity work is Burst. They meet through a singleton:

```
SelectionInputSystem (managed)  ──writes──▶  SelectionInput  ──read──▶  SelectionSystem (Burst)
   Camera + RTSInputActions                                            raycast / box-test → Selected
SelectionHighlightSystem (Burst)  ─reads Selected─▶  URPMaterialPropertyBaseColor
```

Because input is a plain data singleton, selection is testable by injecting `SelectionInput` directly
(no camera) — see `SelectionSystemTests`.

## Components

| Component | Purpose |
|---|---|
| `Selectable` | tag — entity can be selected |
| `Selected` | enableable — membership is the enabled bit; query with `WithAll<Selected>()` |
| `SelectableType` | `SelectableKind` (Unit / Building) for double-click select-all-of-type |
| `SelectionHighlightColors` | selected vs. deselected colours |

Authored via `SelectableAuthoring`; factories add the set through `SelectionComponents.AddTo` (command-buffer
first, with an immediate `EntityManager` overload). `SelectionSystem` toggles `Selected` with `EnabledRefRW` over a
`WithPresent<Selected>` query; `SelectionHighlightSystem` is a parallel job with a change filter on `Selected`, so it
only rewrites (and Entities Graphics only re-uploads) chunks whose selection changed.
Factory entities have no mesh/collider yet (Phase 6), so they are box-selectable and queryable but not
click-hittable or highlighted.

## Input

`Input/RTSInputActions.inputactions` (codegen on, wrapper `HyperRTS.Input.RTSInputActions`) has three maps:

| Map | Bindings |
|---|---|
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

## Scene wiring (manual)

1. Attach `SelectableAuthoring` (kind = Unit) to the Demo Unit in `EntitiesSubScene.unity`.
2. Add `SelectionDragBoxUI` to a scene GameObject; assign a `PanelSettings` asset to its `UIDocument`
   (Create ▸ UI Toolkit ▸ Panel Settings Asset, default scale mode).
3. Camera must be tagged `MainCamera`.

## Verify

- Tests: Test Runner → `HyperRTS.Simulation.Tests`.
- Play `SampleScene`: click tints, ground-click clears, box selects, Shift/Ctrl modify, double-click
  selects all Units on screen.

## Known limitation

The Demo Unit's collider bakes as a **static** body. Fine for Phase 1 (units don't move); once Phase 2
movement drives `LocalTransform`, moving selectables must be kinematic-dynamic or click-raycast hits
their baked positions. Box-select is unaffected.
