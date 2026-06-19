# Selection & input (Phase 1)

Mouse selection for rendered entities. Lives in
[`Assets/Modules/Core/Selection/`](../Assets/Modules/Core/Selection/) (`HyperRTS.Core.Selection`).

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

Authored via `SelectableAuthoring`; factories add the set through `SelectionComponents.AddTo`.
Factory entities have no mesh/collider yet (Phase 6), so they are box-selectable and queryable but not
click-hittable or highlighted.

## Input

`Selection/Input/RTSInputActions.inputactions` (codegen on), one **Selection** map: `Point`, `Select`
(left), `Additive` (Shift), `Subtract` (Ctrl). Short press = click; drag past a threshold = box; two
quick clicks = double-click. Shift adds, Ctrl removes, no modifier replaces.

## Scene wiring (manual)

1. Attach `SelectableAuthoring` (kind = Unit) to the Demo Unit in `EntitiesSubScene.unity`.
2. Add `SelectionDragBoxUI` to a scene GameObject; assign a `PanelSettings` asset to its `UIDocument`
   (Create ▸ UI Toolkit ▸ Panel Settings Asset, default scale mode).
3. Camera must be tagged `MainCamera`.

## Verify

- Tests: Test Runner → `HyperRTS.Core.Tests`.
- Play `SampleScene`: click tints, ground-click clears, box selects, Shift/Ctrl modify, double-click
  selects all Units on screen.

## Known limitation

The Demo Unit's collider bakes as a **static** body. Fine for Phase 1 (units don't move); once Phase 2
movement drives `LocalTransform`, moving selectables must be kinematic-dynamic or click-raycast hits
their baked positions. Box-select is unaffected.
