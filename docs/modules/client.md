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
| `WorldPointer` | Cursor → target through `PickRanking`, and the ground point; placement casts for ground only |
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
  `HyperRTSSilhouette` paints a team-colour fill and rim where the unit is hidden. `UnitRenderingLayerBakingSystem`
  puts every rendered part of a `UnitTag` entity on the layer at bake time.
- **Fog**: `FogVisibilitySystem` turns `FogHidden` and `Inside` into `DisableRendering`. Map buildings never get
  `FogHidden`, so `BuildingFog.IsHidden` hides their flags, dials and pips under the local fog.
- **Flags** (`Flags/`): `FlagDisplay.Resolve` picks the shown flag and hoist (an owned building's flag lowers over
  the first half of a capture and the capturer's rises over the second; a neutral one's rises over the whole).
  `FlagSystem` writes `FlagState` (flag layer = the shown player's `PlayerSide`, hoist, shown) and `FlagColor` (hoist
  band). `HyperRTS/Flag` samples the game's flag `Texture2DArray` and waves the `FlagMesh` cloth downwind.
- **Destruction**: `DamageStageSystem` shows the stage model for the health and bursts its effect rig;
  `SpawnEffectSystem` bursts spawn effect rigs once (both from the effect pool; a prefab without `EffectRig` logs an
  error once); `ToppleSystem` tips felled remains (`Felling`) over with ease-in
  and a damped bounce, or lays them down at once when the fall was out of sight. It writes only their
  `LocalToWorld` (and their children's) after the transform systems, timed by its own `FallClock`, so the
  simulation keeps the final pose. `Animation/MachineSystem` poses
  machine parts.
- **Environment**: `HyperRTS/Water` (depth colour, refraction, ripples, sky reflection, capped glint; vertex A thins
  fords, G streaks foam; needs Depth and Opaque Textures), `HyperRTS/Foliage` (alpha-cut, wind sway,
  translucency) and `HyperRTS/Bark` (opaque URP Lit: base, normal, optional mask map R metallic G AO A smoothness).
- **Wind**: `Wind` (on the scene's directional `WindZone`) publishes `_HyperRTSWindDirection` (xz downwind),
  `_HyperRTSWindStrength` (windMain), `_HyperRTSWindGust` (x windPulseMagnitude, y windTurbulence, z gust tiles per
  metre), `_HyperRTSWindGustMap` (procedural tileable noise), `_HyperRTSWindGustOffset` (xy this frame's scroll in
  tiles, zw last frame's; it moves downwind windPulseFrequency tiles a second) and `_HyperRTSWindDetail`
  (the component's `vegetation` `WindDetail`: 0 `None` keeps vegetation still, 1 `Sway`, 2 + `Branches`, 3 +
  `Flutter`; a climate picks it, flags fly regardless). `Wind.hlsl` turns them into motion; every pass of Foliage and
  Bark (forward, shadow, depth, depth-normals, motion vectors) uses the same position, and both crossfade LODs.
  - Foliage with `_TREE_WIND` off (grass, crops, details): sway about the origin with height (`_WindStrength` m per m,
    `_WindSpeed`) and `_Flutter` m of leaf flutter. On, and always on Bark: tree meshes carry wind data in Unity object
    space, metres: TEXCOORD2.xy branch pivot xy, TEXCOORD3.x branch pivot z, TEXCOORD3.y card pivot x, TEXCOORD4.xy
    card pivot yz (TEXCOORD1 unused); COLOR r crown AO, g branch flex (0 at the level-1 branch pivot, 1 at its tip, 0
    on the trunk), b stiffness (0 limb, 1 twig), a flutter weight (1 on leaf cards). Cards flutter about their pivot
    (`_CardFlutter` rad), branches turn about theirs (`_BranchSway` rad), and the trunk bends on an (h / `_TreeHeight`)²
    curve keeping its length (`_WindStrength` m per m). Bark and leaves of one tree need the same `_WindStrength`,
    `_WindSpeed`, `_BranchSway`, `_TreeHeight` and `_WindScale` (0 = still, for fallen trees).
- **Interaction maps** (`InteractionMap` on the rig, needs the `Hidden/HyperRTS/Interaction Brush` shader): two
  render textures over the map rect, faded and stamped on the GPU each frame (`InteractionBrush`: instanced
  procedural quads; fades go in whole 8-bit steps). `_HyperRTSTrample` (RGBA8, 0.5 m texels on Full): RG travel
  direction * 0.5 + 0.5, B grass flatten (recovers over 60 s), A crop damage (never recovers). `_HyperRTSTracks`
  (RG8, 0.2 m texels): R rut depth, G churned soil (fade over 4 min). `_HyperRTSInteractionRect` (x min, z min,
  1 / width, 1 / depth; zero = no maps, callers skip the reads) and `_HyperRTSTracksTexel` (xy texel counts, zw metres
  per texel). `InteractionStampSystem`
  stamps vehicles with `TrackMarks` that are on the ground and not hidden from the local player, seen building
  footprints (`NavObstacle`) and ground impacts of 3 m splash or more. `InteractionMap.Quality` (Off, Low = half
  resolution, Full) is set by the game; Off frees the textures. Presentation only, so clients may differ.
  `Trample.hlsl` reads them: the terrain darkens ruts, soil and flattened grass and dents its normal along the ruts;
  Foliage without Tree Wind leans plants flat along the travel direction (`_Trample`: Off, Grass by B, Crop by A),
  and `TrampleBend` serves any plant shader.
- **Row crops** (`Environment/RowCrops/`, `RowCropRenderer` added to map scenes by the map tools): a `RowCrop` asset
  holds a crop's model variants (`RowCropVariant`: full, simple and card meshes, one submesh each, origin at the root,
  facing +Z), its `HyperRTS/RowCrop` materials, row and plant spacing, jitter, scale and turn ranges, lean (inside and
  at the field edge), row wander, patch scale, edge margin and the two LOD distances. `RowCropFields` (baked per map) holds a field-id picture (R low byte, G high byte, 1 m texels), each
  field's lattice (`RowCropField`: origin, along and across axes, crop, seed), the crops and the 16 m tiles holding
  any. The renderer runs in edit mode too: on `RenderPipelineManager.beginCameraRendering` it culls tiles against that
  camera's frustum and `RowCropRenderer.Distance` (Game and Scene view cameras of its scene that see its layer;
  reflection and preview cameras are skipped), and
  `RowCrops.compute` (`Plant` kernel, one thread per lattice spot of one tile of one field) grows the plants: a spot
  belongs to the tile it falls in and the field the picture names there (`RowCropLattice` is the same maths in C#),
  `RowCropRenderer.Density` thins by hash, the trample map's A channel bends crushed plants or drops them, the hash
  picks the plant's variant (one dispatch per variant), slow value noise makes rows wander together and scale vary in
  patches, plants lean a few degrees (more where probes 1.5 m out leave the field), and each
  plant goes into its variant's full, simple or card buffer by camera distance, shrinking away over the last 15 % of the draw
  distance. The bands are drawn with `Graphics.RenderMeshIndirect` for that camera only (GPU buffers are per renderer, reused by
  each camera in turn); `RowCrop.shader` reads the plants by
  `SV_InstanceID` and has forward, shadow, depth, depth-normals and motion-vector passes, lit like Foliage with plant
  wind. Regrowing every visible tile per camera needs no cache when the camera moves or crops get trampled. The
  game sets density and distance from its vegetation quality.
- **Effects playback** (`Effects/`): an `EffectRig` prefab root holds world-space child particle systems (anything
  else is an error naming the prefab), `maxDistance`, a `scale` range, `scalePerRadius` and `alignToGround`; author it
  +Y up, +Z forward (impacts turn +Y to the ground normal). `EffectPool` (owned by `EffectPoolSystem`, ticked first) keeps one DontSave instance per prefab, never
  instantiating per event. On first use each system's authored bursts and rates are read and its own emission is
  switched off: `Burst` replays the bursts (timed ones later, at the same pose; sub-emitters still fire), `Stream`
  emits rate × time + rate over distance along a path with a carried fraction. `EffectDetail.Level` thins particles
  (Low 0.35, Medium 0.7, High 1); High adds `EffectLight` flashes from 8 shared shadowless lights; Low halves loop
  range. `EffectPlaybackSystem` bursts seen `EffectEvent`s (the type's slot rig, facing `Direction`) and impacts of
  shooters with `EntityImpactEffects` (`SurfaceEffects.RigFor` the event's material, else `GroundMaterials`; on the
  terrain, facing up or the ground normal). `LoopEffectSystem`, `TrailSystem` and `MotionDustSystem` stream drawn
  entities (no `DisableRendering`): loops at sockets with health gate and timed fade, trails along the frame's path,
  dust by ground material and speed / `FullSpeed`. `EffectCatalog` caches each type's effects like the impact marks.
- **Audio**: `SoundPlaybackSystem` plays the `SoundQueue` through 32 pooled sources; `SoundListener` follows the
  camera's ground focus.
- **Minimap**: the baked `MinimapImage`, local fog, blips and the camera view.
