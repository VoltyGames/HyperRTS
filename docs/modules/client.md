# Client modules

[Modules](../modules.md) ▸ Interaction, Input, Presentation

Everything a player sees and touches. None of it loads on a headless server, and none of it changes the
simulation, so two clients may look slightly different.

## Interaction

UI state that input, the HUD and overlays share, created once per presented world. It lives in `Client/`.
`DefaultWorld` and `LiveQuery` give MonoBehaviours the presented world and a query that survives world swaps.

| Singleton | Role |
| --- | --- |
| `PlacementState` | Building placement: prefab, ghost position, validity |
| `PendingCommand` | A targeted command waiting for a click, such as attack-move or an aimed ability |
| `PointerState` | Whether the pointer is over UI, so world clicks are ignored |
| `SelectionDragState` | The selection box being dragged |
| `CameraFocusRequest` | A minimap click moving the camera |
| `HotkeyPresses` | Keys pressed this frame, for command-card hotkeys |

## Input

| Type | Role |
| --- | --- |
| `RTSInputActions` | The Selection, Commands and Camera action maps. [Bindings](../getting-started.md#play) |
| `InputActionsProvider` | The one shared actions instance, with `Suspend` and `Resume` |
| `SelectionInputSystem` | Mouse gestures to `SelectionInput` |
| `CommandInputSystem` | Right-click, hotkeys and targeted commands to `PlayerCommand` |
| `PlacementInputSystem` | Moves the placement ghost and confirms it |
| `WorldPointer` | What the cursor points at, and the ground point |
| `CameraController` | Pan, edge scroll, eased zoom, rotate, map bounds. Pitch follows zoom height, flatter up close. Starts at the home base |

Clicks over the HUD, while placing, or while a command is pending never reach selection. A front end that handles
Escape itself sets `FrontEndOwnsCancel` and calls `MatchView.CancelInteraction()` first.

## Presentation

Rendering, HUD, effects and audio. `MatchView` is a once-per-frame read model for MonoBehaviours: local player,
relations and colours.

### HUD

`HUD.prefab` holds a UI Toolkit HUD. `HUDController` builds its panels from `HUD.uss` for layout and
`HUDTheme.uss` for looks, and keeps them in sync with the simulation. Games add or replace panels by overriding
`CreatePanels`.

| Type | Role |
| --- | --- |
| Panels | `ResourceBar`, `SelectionPanel`, `CommandCard`, `Minimap`, `GameOverBanner` |
| `ICommandSource` | Adds command-card entries: purchases, abilities, stances, unit actions. Each has a slot and hotkey |
| `HUDText` | Every HUD string, by key. Set `HUDText.Current` to translate |
| `OverlayRenderer` | Instanced selection rings, health bars, placement ghost, rally markers, capture dials, garrison pips |

`HUDText` keys:

| Key | Text |
| --- | --- |
| `hud.pop`, `hud.power` | Resource bar labels |
| `command.return`, `command.unload`, `command.sell` | Fixed commands |
| `stance.<stance>` | Stance names |
| `outcome.victory`, `outcome.defeat`, `outcome.draw` | Game over banner |
| `entity.<name>`, `resource.<asset>`, `ability.<name>` | Names, kebab-cased: "War Factory" is `entity.war-factory` |

The minimap shows the baked map image, local fog, blips and the camera view.

### Team colour

`HyperRTS/LitTeam` is URP Lit that repaints the albedo in the owner's colour. It paints where the `_TeamMask`
texture's red channel allows, on UV0, and inside the `TeamStripAuthoring` height band if there is one.
`FactionPalette` holds this client's colours. `TeamColorOverride` swaps them, for example for colour-blind players
or own, ally and enemy colours. The validator flags units and buildings without a team colour material.

Units hidden behind cover still show. `UnitSilhouetteFeature`, a URP renderer feature, draws a team-coloured fill
and rim where a unit is hidden. Every rendered part of a unit is put on rendering layer 8 at bake time for this.

### Fog

Entities hidden by fog, and passengers inside a container, stop rendering. `FogOfWarRenderer` projects the fog onto
the depth buffer, so terrain and buildings fog where they stand. Map buildings stay visible, but their flags, dials
and pips hide under the local fog.

A client remembers enemy buildings where it last saw them. The memory is a visual copy of the building, team
coloured, that nothing can pick, select or count. The minimap shows memories as dimmed squares.

### Flags

Buildings fly their owner's flag. During a capture the owner's flag lowers over the first half and the capturer's
rises over the second. A neutral building's flag rises over the whole capture. `HyperRTS/Flag` samples the game's
flag `Texture2DArray`, one layer per `PlayerSide`, and waves the cloth downwind.

### Destruction

An entity shows the damage stage model for its health and bursts that stage's effect. Spawn effects burst once.
Felled remains, such as trees, tip over with a bounce, or lie down at once if the fall was out of sight. The tipping
only moves the rendered pose, and the simulation keeps the final one.

A building with `BreakablePartsAuthoring` lists its parts, each an intact and a broken model. **Find Parts By Name**
in its context menu fills the list from `<Part>_Intact` children and their `<Part>_Broken` siblings, which it saves
hidden at scale 0 so editor views show the standing building. Health picks
how many break: round(parts x `breakShare` x (1 - health fraction)). The next to break is the intact part nearest the
latest impact on the building, else the first in bake order, and it bursts the part effect outward from the centre.
Repair restores the latest broken part first. A building first seen, or changed out of sight, shows its breakage
without effects. It is presentation only: the simulation never reads it.

### Shaders

| Shader | Use |
| --- | --- |
| `HyperRTS/Water` | Depth colour, refraction, ripples, screen-space scene reflection over the sky, foam and swells at banks, unit wakes (`SampleWake`). Needs Depth and Opaque Textures |
| `HyperRTS/Foliage` | Alpha-cut plants and tree leaves with wind, trample and translucency |
| `HyperRTS/Bark` | Opaque tree bark with wind. Mask map: R metallic, G AO, A smoothness |
| `HyperRTS/RowCrop` | GPU-placed field crops. See [row crops](#row-crops) |

Water vertex colours shape the flow: alpha thins fords, green streaks rapids foam.

### Atmosphere

An `Atmosphere` component sits on the root of a weather's atmosphere prefab, together with its sun, post-processing
volume, `Wind` and `HeightFog`. When enabled it sets the skybox, ambient light, reflection cubemap and wetness, and
turns Unity fog off. A missing skybox or reflection is an error.

`HeightFog` adds fog that thins with height. `HeightFogFeature` must be on the URP renderer with its shader set to
`Hidden/HyperRTS/HeightFog`, and the validator checks this. It draws in game and scene view perspective cameras
only, so orthographic captures such as the minimap stay clear. Custom shaders apply the same fog with `HeightFogAt`
from `HeightFog.hlsl`, as water does.

### Wind

`Wind` sits on the scene's directional `WindZone` and publishes its strength, gusts and direction to shaders. Its
`vegetation` setting picks how much plants move: `None`, `Sway`, `Branches` or `Flutter`. Flags fly regardless.
Every shader pass of Foliage and Bark uses the same wind, so shadows match.

Grass, crops and other details sway about their origin. Trees bend at the trunk, sway their branches and flutter
their leaf cards. Bark and leaves of one tree need the same `_WindStrength`, `_WindSpeed`, `_BranchSway`,
`_TreeHeight` and `_WindScale`. Set `_WindScale` to 0 for fallen trees.

Tree meshes for Foliage with **Tree Wind** on, and for Bark, carry wind data in object space, in metres:

| Channel | Data |
| --- | --- |
| TEXCOORD2.xy, TEXCOORD3.x | Branch pivot |
| TEXCOORD3.y, TEXCOORD4.xy | Leaf card pivot |
| COLOR r | Crown ambient occlusion |
| COLOR g | Branch flex: 0 at the branch pivot and on the trunk, 1 at the tip |
| COLOR b | Stiffness: 0 limb, 1 twig |
| COLOR a | Flutter weight, 1 on leaf cards |

### Interaction maps

Vehicles leave ruts and flatten grass and crops, shells leave craters and scorch, and units moving through water
leave wakes. `InteractionMap` on the rig keeps three GPU textures over the map and stamps them each frame. It needs
the `Hidden/HyperRTS/Interaction Brush` shader.

| Texture | Channels |
| --- | --- |
| `_HyperRTSTrample` | RG travel direction, B flattened grass that recovers over 60 s, A crop damage that never recovers |
| `_HyperRTSTracks` | R rut depth and G churned soil, which fade over 4 minutes. B scorch and A crater depth (up to 2 m), which last the match |
| `_HyperRTSWake` | R foam, which fades over 5 s |

Vehicles with `TrackMarks` stamp while on the ground and visible, as do building footprints. Every ground impact
whose `ImpactMark` has a `craterDepth` or `scorch` stamps a round bowl half the mark's size across, inside a
slightly wider scorch disc, and flattens the plants over it. Units with a `NavAgent` riding water cells stamp foam
behind them. A replay seeking back clears the maps, as it clears the decals.

Shaders read the maps through `Trample.hlsl`. The terrain darkens ruts and flattened grass, bends its normal down
the crater's slope so the sun lights the bowl, darkens and dampens the bowl's soil, and blackens scorched ground.
Foliage leans plants along the travel direction, and water reads `SampleWake`. The quality setting picks Full, Low
at half resolution, or Off, which frees the textures.

### Row crops

Fields of crops in rows, placed and drawn on the GPU, so no crop is an object. The map tools add a `RowCropRenderer`
to each map scene and bake its `RowCropFields`: which field covers each metre, and each field's crop and row
direction.

A `RowCrop` asset describes one crop: model variants with full, simple and card meshes, materials, row and plant
spacing, random scale, turn and lean, and two LOD distances. Variant meshes face +Z with the origin at the root.

Each camera regrows the plants of its visible tiles every frame, so trampling and camera moves need no cache.
Trampled plants bend or disappear. Plants shrink away over the last 15 % of the draw distance. It also runs in edit
mode.

### Ambient life

An `AmbientLife` component in the scene, one per climate, adds birds and dust devils. They live on the client only,
so clients may differ. A count of zero turns a feature off. Both are skipped at Low effect detail.

- Bird flocks circle over the map. A nearby impact scatters them, and they settle again at least 150 m away.
- Dust devils form near the camera on the chosen ground and drift downwind.

### Effects

An `EffectRig` is a prefab whose root holds world-space child particle systems. Author it +Y up and +Z forward.
Impacts turn +Y to the ground normal. The rig sets a max distance, a scale range, scale per splash radius and ground
alignment. The validator reports a broken rig, naming the prefab.

No two plays need look the same:

- **Variants**: a rig may list alternative rigs. Each burst plays the rig or one variant at random. A loop or trail
  keeps one per entity. Each variant is its own pooled rig and has no variants of its own.
- **Random spin** turns each burst about the rig's +Y. It is off by default, so muzzles keep their aim. Impacts
  always get a random yaw.
- **Loops** play at a scale inside the rig's range, and with random spin at a yaw, both stable per entity.
- **Burst cycles**: a Shuriken burst's cycle count and interval repeat it, such as ammo cooking off. A burst that
  repeats forever is a broken rig.

`EffectPool` keeps one instance per prefab and replays it for every event, never instantiating per shot. Effects
play for cues the client can see:

- **Bursts**: fire, death, ability and impact cues. Impacts pick a rig by the surface material that was hit.
- **Loops**: effects at sockets while health is in range, such as fires on a damaged building.
- **Trails** along a projectile's path.
- **Motion dust** by ground material and speed.

| Effect detail | Particles | Extra |
| --- | --- | --- |
| Low | 35 % | Loop range halved, no ambient life |
| Medium | 70 % | |
| High | 100 % | Light flashes from 8 shared lights |

### Audio

`SoundPlaybackSystem` plays cue sounds through 32 pooled sources. `SoundListener` follows the camera's ground focus.

### Quality

The game fills one `PresentationQuality` and calls `PresentationQuality.Apply`. It covers effect and ground detail,
interaction maps, grass and row crop density and distance, antialiasing and shadow map size. Apply sets them all,
and again on each scene load. The shadow map size changes in memory only and is restored on quit. `Current` holds
the last settings and resets to full detail on play.
