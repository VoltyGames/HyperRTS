# Presentation

[Docs](../README.md) ▸ Presentation

Everything a player sees and touches. None of it loads on a headless server or changes the simulation, so two clients
may look slightly different. This page covers interaction, input, audio and quality, and indexes the presentation pages.

## Interaction

UI state that input, the HUD and overlays share, created once per presented world, in `Presentation/Interaction/`.
`DefaultWorld`, `LiveQuery` and `LiveSingleton<T>` (in `Presentation/Common/`) give MonoBehaviours the presented world,
a query that survives world swaps and a singleton read (`TryGet`) and write (`Set`). A scene object that systems need,
such as `Wind` or `FieldFireSettings`, writes a `SceneSingleton<T>` every frame and removes it when disabled; it lands
in each new match world on its own. A new HUD-to-input channel is a struct, one line in `ClientSingletonSystem` and its
readers.

| Singleton | Role |
| --- | --- |
| `PlacementState` | Building placement: prefab, ghost position, yaw, validity |
| `PendingCommand` | A targeted command waiting for a click, such as attack-move |
| `PointerState` | Whether the pointer is over UI, so world clicks are ignored |
| `HoveredEntity` | The commandable entity under the pointer |

## Input

`MatchInputActions` holds the Selection, Commands and Camera action maps ([bindings](../getting-started.md#play)),
shared through `InputActionsProvider` (`Suspend` and `Resume`). Each input system enables its own map while it runs.
`PlacementInputSystem` handles [facing](../simulation/economy.md#facing) and
[line placement](../simulation/economy.md#line-placement).

`CameraController` orbits a ground focus. Its height is above the terrain at the focus, which eases along the
`TerrainHeight`; each scroll notch changes the height by `zoomFraction` of it, and the pitch runs linearly from
`pitchAtMinZoom` to `pitchAtMaxZoom`. The camera never sits closer than `terrainClearance` to the ground beneath it.
Delete and Page Down turn it at `turnSpeed`, a middle-button drag yaws it, and Home returns to `defaultYaw` and
`defaultHeight` over the same focus. A match opens on `startFocus`, then on the local player's buildings once found.
`CameraController.View` is the one pose rule, which map view captures share.

At close heights the camera also dithers away tree pixels (Bark, and Foliage with Tree Wind) within
`treeFadeCameraRadius` of the camera, within `treeFadeLineRadius` of the line from it to the focus and on
`treeFadeBeyondFocus` past it, so squads behind and under trees show. The fade is full at or below
`treeFadeFullBelowHeight`, gone at or above `treeFadeNoneAboveHeight`, and shadows are never faded. It reaches the
shaders as the globals `_HyperRTSFadeCamera`, `_HyperRTSFadeFocus`, `_HyperRTSFadeReach` and `_HyperRTSFadeAmount`, and
only a camera at the controller's position fades, so scene views and capture cameras do not.

Clicks over the HUD, while placing, or while a command is pending never reach selection. Escape undoes the topmost
client mode through `InteractionState.Cancel`: placement, then a pending command, then the card page. A front end that
handles Escape itself sets `FrontEndOwnsCancel` and calls `MatchView.CancelInteraction()` first.

## Presentation

`MatchView` is a once-per-frame read model for MonoBehaviours: local player, relations and colours.

| Page | Covers |
| --- | --- |
| [HUD and overlays](hud.md) | HUD panels, command card, cursors, ground markers and health bars |
| [Animation](animation.md) | Bone and vertex animation, machines, motion rig, squad figures |
| [Rendering](rendering.md) | Team colour, visibility, fog of war, destruction visuals, shader conventions |
| [Environment](environment.md) | Atmosphere, fog, wind, water, trample maps and craters, field fires, ambient life |
| [Vegetation](vegetation.md) | Row crops and the shared scatter host |
| [Effects](effects.md) | Effect rigs, generated VFX graphs, pools, loop lights |

## Audio

`SoundPlaybackSystem` plays cue sounds and `LoopSoundSystem` entity loops, heard from `SoundListener` at the camera's
ground focus. `AnnouncerSystem` speaks alerts.

## Quality

The game fills one `PresentationQuality` and calls `PresentationQuality.Apply`, again on each scene load. It covers
effect and ground detail, grass and crop density, water caustics and render scale. `CloseDetail` (0 to 1) scales a
`GrassPlant` by its `PlantKind`: `Close` plants draw while it is above 0, `Clutter` plants grow at that share of their
density, `Ground` plants ignore it.

Shadows and other pipeline features come from the game's quality levels, each with its own URP asset, so set the
quality level before `Apply`, which writes render scale onto that level's asset. `Apply` throws naming the first knob
out of range before it writes anything. Systems read their knobs from `PresentationQuality.Current`, which resets to
full detail on play.
