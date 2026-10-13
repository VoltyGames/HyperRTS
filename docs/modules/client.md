# Client modules

[Modules](../modules.md) ▸ Interaction, Input, Presentation. None of these load on a headless server.

## Interaction

UI state that input, HUD and overlays share (`HyperRTS.Client`), created once per presented world.
`DefaultWorld` and `LiveQuery` give MonoBehaviours the presented world and a query that survives world swaps.

| Singleton | Role |
| --- | --- |
| `PlacementState` | Building placement: prefab, ghost position, validity |
| `PendingCommand` | Targeted command waiting for a click (attack-move, patrol, aimed ability) |
| `PointerState` | `OverUI`: world clicks are ignored |
| `SelectionDragState`, `CameraFocusRequest` | Marquee; minimap click → camera |
| `HotkeyPresses` | Keys pressed while gameplay input is live, for command-card hotkeys |

## Input

| Type | Role |
| --- | --- |
| `RTSInputActions`, `InputActionsProvider` | Selection, Commands and Camera maps ([bindings](../getting-started.md#play)); the one shared instance, `Suspend` / `Resume`, `FrontEndOwnsCancel` |
| `SelectionInputSystem` | Mouse gestures → `SelectionInput` |
| `CommandInputSystem` | Right-click Smart, hotkeys, targeted commands → `PlayerCommand` |
| `PlacementInputSystem` | Moves the ghost, confirms with `PlaceBuilding` |
| `WorldPointer` | Cursor → target through `PickRanking`, and the ground point |
| `CameraController` | Pan, edge scroll, zoom, rotate, map clamp, `FocusOn`, starts at the home base |

Clicks over the HUD, while placing or while a command is pending never reach selection. A front end that handles
Escape sets `FrontEndOwnsCancel` and calls `MatchView.CancelInteraction()` first.

## Presentation

`HUD.prefab`:

| Component | Role |
| --- | --- |
| `HUDController` | UI Toolkit HUD; builds its `IHUDPanel`s and refreshes them from a `HUDContext` |
| Panels | `ResourceBar`, `SelectionPanel`, `CommandCard`, `Minimap`, `GameOverBanner` |
| `CommandCard`, `ICommandSource` | Entries from each source: purchases, abilities, stances, unit actions; `Slot` and `Hotkey` per entry |
| `HUDText` | Every HUD string by key; set `HUDText.Current` to translate (keys below) |
| `OverlayRenderer` | Instanced rings, health bars, placement ghost, rally markers, and `BuildingOverlays` (capture dial, garrison pips). Your selected units' rings and bars draw through buildings |
| `FogOfWarRenderer` | Fog projected onto the depth buffer, so terrain and buildings fog where they stand |

`HUDText` keys: fixed `hud.pop`, `hud.power`, `command.return|unload|sell`, `stance.<stance>`,
`outcome.victory|defeat|draw`; names `entity.<name>`, `resource.<asset>`, `ability.<name>`, kebab-cased by
`HUDText.Key` ("War Factory" → `entity.war-factory`).

Rendering and effects:

- **`MatchView`**: once-per-frame read model for MonoBehaviours (local player, relations, colours).
- **Team colour** (`TeamColors/`): `HyperRTS/LitTeam` is URP Lit that repaints albedo in the owner's colour where
  `_TeamMask` (R, UV0) and the `_TeamStrip` height band allow. `TeamColorSystem` writes `TeamColor` (alpha 0 =
  untinted) and `TeamStrip` on an owned entity's render parts. `FactionPalette` holds this client's colours;
  `TeamColorOverride` swaps them (colour-blind, own / ally / enemy). `TeamColorRule` flags units without it.
- **Silhouettes**: `UnitSilhouetteFeature` (Render Graph, after the skybox, game cameras) draws renderers on
  `UnitRenderingLayer` (8) through cover: LitTeam's `HyperRTSUnitMask` marks visible pixels in stencil, then
  `HyperRTSSilhouette` paints a team-colour fill and rim where the unit is hidden. `UnitRenderingLayerRule` flags
  unit renderers off the layer.
- **Fog**: `FogVisibilitySystem` turns `FogHidden` and `Inside` into `DisableRendering`. Map buildings never get
  `FogHidden`, so `BuildingFog.IsHidden` hides their flags, dials and pips under the local fog.
- **Flags** (`Flags/`): `FlagDisplay.Resolve` picks the shown flag and hoist (an owned building's flag lowers over
  the first half of a capture and the capturer's rises over the second; a neutral one's rises over the whole).
  `FlagSystem` writes `FlagState` (flag layer = the shown player's `PlayerSide`, hoist, shown) and `FlagColor` (hoist
  band). `HyperRTS/Flag` samples the game's flag `Texture2DArray` and waves the `FlagMesh` cloth downwind.
- **Destruction**: `DamageStageSystem` shows the stage model for the health and plays its effect;
  `SpawnEffectSystem` plays spawn effects once. `Animation/MachineSystem` poses machine parts.
- **Environment**: `HyperRTS/Water` (depth colour, refraction, ripples, sky reflection, capped glint; vertex A thins
  fords, G streaks foam; needs Depth and Opaque Textures), `HyperRTS/Foliage` (alpha-cut, wind sway,
  translucency), and `Wind`, which publishes `_HyperRTSWindDirection` and `_HyperRTSWindStrength` from the scene's
  `WindZone`.
- **Audio**: `SoundPlaybackSystem` plays the `SoundQueue` through 32 pooled sources; `SoundListener` follows the
  camera's ground focus.
- **Minimap**: the baked `MinimapImage`, local fog, blips and the camera view.
