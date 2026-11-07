# Presentation

[Docs](../README.md) ▸ Presentation

Everything a player sees and touches. None of it loads on a headless server, and none of it changes the simulation, so
two clients may look slightly different. This page covers interaction, input, audio and quality, and indexes the
presentation pages.

## Interaction

UI state that input, the HUD and overlays share, created once per presented world, in `Presentation/Interaction/`.
`DefaultWorld`, `LiveQuery` and `LiveSingleton<T>` (in `Presentation/Common/`) give MonoBehaviours the presented world, a
query that survives world swaps and a singleton read (`TryGet`) and write (`Set`) that does the same. `MatchView` owns the
placement, pending command and command page. A new HUD-to-input channel is a struct, one line in
`ClientSingletonSystem` and its readers.

| Singleton | Role |
| --- | --- |
| `PlacementState` | Building placement: prefab, ghost position, yaw, validity, the held drag's start. Its `PlacementSegment` buffer holds a line's ghost segments |
| `PendingCommand` | A targeted command waiting for a click, such as attack-move or an aimed ability |
| `PointerState` | Whether the pointer is over UI, so world clicks are ignored |
| `SelectionDragState` | The selection box being dragged |
| `HoveredEntity` | The commandable entity under the pointer, written by `HoverInputSystem` |
| `CameraFocusRequest` | A minimap click moving the camera |
| `HotkeyPresses` | `Key`s pressed this frame, for command-card hotkeys |
| `ScrollEdge` | The screen edge the pointer is scrolling against, written by `CameraController` |
| `CursorIntent` | The cursor to show, a `CursorKind`, written by `CursorIntentSystem` |

## Input

| Type | Role |
| --- | --- |
| `MatchInputActions` | The Selection, Commands and Camera action maps. [Bindings](../getting-started.md#play) |
| `InputActionsProvider` | The one shared actions instance, with `Suspend` and `Resume` |
| `InputMapSystem` | Base of the systems below: enables its input map while it runs |
| `SelectionInputSystem` | Mouse gestures to `SelectionInput` |
| `CancelInputSystem` | Escape undoes the topmost client mode through `InteractionState.Cancel`: placement, then a pending targeted command, then the card page |
| `CommandInputSystem` | Right-click, hotkeys and targeted commands to `PlayerCommand` |
| `PlacementInputSystem` | Moves and [turns](../simulation/economy.md#facing) the placement ghost through `PlacementRotation` (drag, hold or tap R, Shift to snap) and confirms it on release, or drags and releases a [line](../simulation/economy.md#line-placement) |
| `WorldPointer` | What the cursor points at, and the ground point |
| `CameraController` | Pan, edge scroll, eased zoom, rotate, map bounds. Pitch follows zoom height. Starts at the home base |

Clicks over the HUD, while placing, or while a command is pending never reach selection. A front end that handles Escape
itself sets `FrontEndOwnsCancel` and calls `MatchView.CancelInteraction()` first. That method, `CancelInputSystem` and the
command card's right click all use the one order in `InteractionState.Cancel`.

## Presentation

Rendering, HUD, effects and audio. `MatchView` is a once-per-frame read model for MonoBehaviours: local player, relations
and colours. It and other presenters read singletons through `LiveSingleton`; `MatchView` re-reads colours only when a
`ChangeWatch` sees a `Player` or colour override written or their count move.

| Page | Covers |
| --- | --- |
| [HUD and overlays](hud.md) | HUD panels, command card, cursors, ground markers and health bars |
| [Animation](animation.md) | Bone and vertex animation, machines, motion rig, squad figures |
| [Rendering](rendering.md) | Team colour, visibility, fog of war, destruction visuals, shader conventions, contact shadows, sharpening |
| [Environment](environment.md) | Atmosphere, fog, horizon, wind, water, trample maps and craters, field fires, ambient life |
| [Vegetation](vegetation.md) | Row crops and the shared scatter host |
| [Effects](effects.md) | Effect rigs, generated VFX graphs, pools, loop lights |

`Environment/` is split by topic, each folder its own namespace: `Ground` (terrain and roads, with `Stamps`, `Craters` and
`ImpactMarks`), `Fire`, `Weather`, `Water`, `Vegetation` (`Grass`, `RowCrops`, `Scatter`, `Shading`), `Horizon` and
`Ambient`. `EdgeNoise.png`, shared by several, stays at the top; their cell grids are the simulation's `CellGrid`.
`Effects/` is split into `Rig`, `Streams`, `Lights` and `Shaders`. In `Rendering/`, instance batches and `EntityExtent`
sit in `Overlays/`, the fog of war feature in `Fog/`, the hide, model variant, garrison part and paint core in
`Rendering/Parts/` (namespace `HyperRTS.Presentation.Rendering.Parts`), and the `LitTeam` shader with its team colour,
strip, palette, grime and lights in `Rendering/LitTeam/`. Contact shadows and the unit silhouette stay at the
`Rendering/` root.

## Audio

`SoundPlaybackSystem` plays cue sounds through 32 pooled sources. `SoundListener` follows the camera's ground focus.
`LoopSoundSystem` plays entity loops through 24 more, and `AnnouncerSystem` speaks alerts through one 2D source.

## Quality

The game fills one `PresentationQuality` and calls `PresentationQuality.Apply`. It covers effect and ground detail,
trample maps, grass and row crop density and distance, close detail, water caustics and render scale. `CloseDetail` (0 to
1) scales a `GrassPlant` by its `PlantKind`: `Close` plants draw while it is above 0, `Clutter` plants grow at that share
of their density, `Ground` plants ignore it. `WaterCaustics` off sets the global keyword `_WATER_NO_CAUSTICS`. Apply sets
them all, and again on each scene load. Shadows and other pipeline features come from the game's quality levels, each
with its own URP asset, so the game sets the quality level before Apply, which writes render scale onto that level's
asset. Apply throws naming the first knob out of range before it writes anything. `Current` holds the last settings and
resets to full detail on play; systems read their knobs from it and nothing else mirrors them. The types live in
`Presentation/Quality/`, which depends on no other Presentation folder.
