# Editor UX & authoring conventions

How HyperRTS components present themselves in the Unity Editor, and the helper tools that
remove manual wiring. The goal: anyone building a game on the engine gets a clean, discoverable
Add Component menu and inspector, with new components inheriting the same polish for free.

## Add Component menu

First-party authoring components appear under a single **`HyperRTS`** root, grouped by module:

```text
Add Component ▸ HyperRTS ▸ Attack ▸ Attack Target
                         ▸ Health ▸ Health
                         ▸ Selection ▸ Selectable
                         ▸ Units ▸ Movement Speed
                         …
```

This replaces Unity's default namespace grouping (`HyperRTS.Simulation.Attack`, …).

## The convention (apply to every authoring MonoBehaviour)

```csharp
[AddComponentMenu(HyperRTSMenu.Health + "Health")]   // category + friendly name
[Icon(HyperRTSIcons.Health)]                          // per-module icon
[HelpURL(HyperRTSDocs.WorldSetup)]                    // "?" opens the docs
[DisallowMultipleComponent]                           // one per GameObject
public class HealthComponentAuthoring : MonoBehaviour
{
    [Tooltip("Starting health.")] public int currentHealth = 100;
    [Tooltip("Maximum health capacity.")] public int maxHealth = 100;
}
```

- **`[Tooltip]`** on every serialized field; **`[Header]`** to group when a component has many
  (e.g. `CameraController` → Movement / Zoom / Default View).
- **`[RequireComponent]`** for hard dependencies (`CameraController` → `Camera`,
  `SelectionDragBoxUI` → `PanelRenderer`).
- **Paths are centralised** in [`Assets/Modules/Core/HyperRTSMenu.cs`](../Assets/Modules/Core/HyperRTSMenu.cs)
  (`HyperRTSMenu`, `HyperRTSIcons`, `HyperRTSDocs`). Const-string concatenation keeps categories spelled
  identically everywhere (compile-time consts are required for the attributes, so they can't be reflection-
  derived). A new module adds one line to each of `HyperRTSMenu` / `HyperRTSIcons`, plus an accent colour in
  the icon generator. Never hardcode a menu/icon path on a component - reference these constants.

## Tools

- **`HyperRTS ▸ Tools ▸ Generate Component Icons`** - regenerates the per-module icons under
  `Assets/Modules/Editor/Icons/` (one accent-coloured rounded square per module). Run it once after
  cloning, or whenever you add a module / change an accent colour
  ([`ComponentIconGenerator.cs`](../Assets/Modules/Editor/Icons/ComponentIconGenerator.cs)).
- **`GameObject ▸ HyperRTS ▸ RTS World`** / **`Selection UI`** - drop the camera + UI rig, or just the
  selection UI, into the scene ([`HyperRtsObjectMenu.cs`](../Assets/Modules/Editor/HyperRtsObjectMenu.cs)).

## Prefab library

Engine-shipped prefabs in [`Assets/Modules/Prefabs/`](../Assets/Modules/Prefabs/) - drag one in instead of
hand-assembling authoring. Prefabs **bake**, so a dragged-in unit/building renders and is selectable with no
manual mesh wiring.

- **`Unit.prefab`** / **`Building.prefab`** - mesh + collider + authoring mirroring the factories.
- **`RTSWorld.prefab`** - the playable rig: a `MainCamera` (+ `CameraController`) and a nested `SelectionUI`.
  Drop into an empty scene via `GameObject ▸ HyperRTS ▸ RTS World` (it ships its own Main Camera).
- **`UI/SelectionUI.prefab`** (+ engine `PanelSettings`) - the selection-UI rig.

## Custom inspectors

[`SelectableAuthoringEditor`](../Assets/Modules/Editor/Selection/SelectableAuthoringEditor.cs) adds a live
Selected/Base colour-swatch preview to `SelectableAuthoring`.

## UI Toolkit: PanelRenderer

Selection UI uses **`PanelRenderer`** (Unity 6.5+), the successor to the legacy `UIDocument` component.
`PanelRenderer` does not expose `rootVisualElement`; obtain the root via
`RegisterUIReloadCallback((panelRenderer, root) => …)` and rebuild content idempotently inside the callback
(it can fire more than once, and content persists across disable/enable). See
[`SelectionDragBoxUI.cs`](../Assets/Modules/Presentation/Selection/SelectionDragBoxUI.cs).
