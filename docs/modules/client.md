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
| `OverlayRenderer` | Instanced selection rings (an ellipse along a capsule unit's facing), health bars, placement ghost, rally markers, capture dials, garrison pips |

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

### Vertex animation

`HyperRTS/LitTeam` with the `_VAT` keyword on plays baked vertex animation textures (VAT), so crowds of animated
infantry draw as instanced static meshes. Every pass, shadows and silhouettes included, reads the same pose. A VAT
set is:

- `<Model>.fbx`: the rest pose. UV1 holds each vertex's texel, u = (column + 0.5) / width and v = row within the
  frame, so Unity may split vertices at seams. A model with `VAT/<Model>.json` beside it imports uncompressed,
  without generated lightmap UVs, and with UV1 kept as 32-bit floats. VAT renderers can't be lightmapped.
- The position and normal textures the bake info file names: RGBA half float, Unity object space in metres. Each frame
  takes `rowsPerFrame` rows and every clip is stacked, so frame f's texel is (column, f x rowsPerFrame + v). They
  import exact: linear, point filtered, clamped, no mipmaps or compression.
- `VAT/<Model>.json`: `positions`, `normals`, `width`, `height`, `rowsPerFrame`, `vertexCount`, `clips` (each with
  `name`, `startRow` as its first frame, `frameCount`, `fps` and `loop`), `boundsMin` and `boundsMax` over every
  frame. A looping clip does not repeat its first frame at the end.

Put the textures in the material's VAT slots, set its rows per frame and turn on `_VAT` (the Debug inspector shows
material keywords). A `VATClipTable` asset imports the bake info file through **Import Bake Info** in its context menu, then
stays hand-editable. `VATAnimatedAuthoring` beside the renderer bakes its start clip into `VATClip` and widens the
render bounds to cover every frame. It fails the bake when the material and table disagree.

Game code plays clips with `VATClip.Play(range, now)`, using `SystemAPI.Time.ElapsedTime` as `now`.
`VATClockSystem` feeds the same clock to the shader, so pausing and replay speed apply to the animation.

### Motion rig

`MotionRigAuthoring` on a unit prefab root moves the model's pivots on the client. Its slots list turrets, barrels
and launchers that follow a `WeaponPose` index, barrels that recoil per shot, wheels, sprockets and idlers that
roll, rotors that spin up in flight and down while `Docked`, and a tilt root (the hull) that pitches and rolls with
the ground under its footprint. On a unit with `FlightAuthoring` the tilt root leans with acceleration instead.
Ground vehicles also sway on a `Suspension` spring over the ground tilt: they squat as they speed up, dive as they
brake, roll out of turns and rock back from each recoil shot, by 1 to 3 degrees. The rig's sway, spring and recoil
kick fields tune it. **Fill from
model** finds the parts by their art names: `Hull`, `Turret`, `Barrel`, `Launcher`, `Wheel_L01`, `Sprocket_L`,
`Idler_L`, `Rotor_Main` and `Rotor_Tail`. It keeps slots it already lists, measures wheel radii and the footprint
from the meshes, and gives `Launcher` weapon 1 when the unit has an extra weapon. `MotionRigRule` checks every slot
names a child of the rig and a weapon the unit has.

The baker stores a `MotionRig` and a `MotionRigPart` buffer on the root. `MotionRigSystem` runs in
`PresentationSystemGroup` on the interpolated unit and writes only child `LocalTransform`s, never the ghost root.
A rig with rotors also gets a `RotorSpin`, the fastest rotor's speed from 0 to 1, which `LoopSoundSystem` reads
after it for the rotor loop.
Like `MachineSystem`, the poses render a frame later, once the transform systems have run.

#### Squad figures

`SquadFiguresAuthoring` on a squad unit draws it as hand-placed VAT figures: children of its `Model`, each with
`VATAnimatedAuthoring`, listed in the order they stay. It names the idle, walk, run, fire and die clips, the run
speed and how long a fallen figure stays down. Each figure looks those names up in its own clip table, so figures
baked separately (another VAT width, other rows) mix in one squad; the bake fails when a table lacks one of the
five. Each `SquadFigure` carries its own `FigureClips`. `SquadFigureSystem` shows ceil(figures x
health fraction), at least one while alive. A figure health drops plays die once, then scales to zero after the
hold, leaving fog's rendering toggles alone. A squad spawned damaged hides its lost figures without the fall. The
rest walk or run by the unit's horizontal speed and fire for 0.6 s after each change of the primary weapon's
`WeaponPose.Shots`. A figure gets a new `VATClip` only when its state changes, and looping clips start at a
per-figure phase so the squad doesn't move in step.

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
Felled remains, such as trees, tip over as a rod pivoting at the stump, or lie down at once if the fall was out of
sight. `FallClock` steps the fall at a fixed rate from the `Topple` height, so a 15 m tree takes about 2.5 s and a
sapling under 1 s, and a crusher at the stump pushes it faster. The canopy bounces it once and it settles. The
tipping only moves the rendered pose, and the simulation keeps the final one. A felled tree's spawn effect plays
where the crown lands, as it lands.

`DebrisAuthoring` on a ruin throws a `DebrisSet`'s pieces once when the ruin appears; on a building with breakable
parts it throws them from each part that breaks. `DebrisSystem` flies them on the client in a pool of 160, bounces
them off the terrain height, lays them flat and shrinks them away after 10 to 20 s. Medium effects throw 70 % of the
pieces and Low none. Pieces from one place fly the same on every client.

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
| `HyperRTS/Foliage` | Alpha-cut plants and tree leaves with wind, trample, translucency and dry and green patches |
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

Vehicles with `TrackMarks` stamp while on the ground and visible, as do building footprints. At Full quality each
walking squad figure leans the plants along its path. Every ground impact
whose `ImpactMark` has a `craterDepth` or `scorch` stamps a round bowl half the mark's size across, inside a
slightly wider scorch disc, and flattens the plants over it. Units with a `NavAgent` riding water cells stamp foam
behind them. A replay seeking back clears the maps, as it clears the decals.

Shaders read the maps through `Trample.hlsl`. The terrain darkens ruts and flattened grass, bends its normal down
the crater's slope so the sun lights the bowl, darkens and dampens the bowl's soil, and blackens scorched ground.
Foliage leans plants along the travel direction, and water reads `SampleWake`. The quality setting picks Full, Low
at half resolution, or Off, which frees the textures.

`LocalGustSystem` also sends up to 8 radial gusts to `Wind.hlsl`: downwash under a seen rotorcraft within 25 m of
the ground, and the blast of each seen impact with a splash of 1 m or more, which spreads out and relaxes over about
a second. Plants lean away from them and tree trunks bend as in wind. Interaction quality Off turns them off.

### Row crops

Fields of crops in rows, placed and drawn on the GPU, so no crop is an object. The map tools add a `RowCropRenderer`
to each map scene and bake its `RowCropFields`: which field covers each metre, and each field's crop and row
direction.

A `RowCrop` asset describes one crop: model variants with full, simple and card meshes, materials, row and plant
spacing, random scale, turn and lean, and two LOD distances. Variant meshes face +Z with the origin at the root.
Its `height` also shades the field: light falls off below the heads to `_CanopyShade` at the roots, so rows show
dark gaps between lit tops.

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

`EffectPool` keeps one instance per prefab and replays it for every event, never instantiating per shot. It reads
the camera position once a frame, in `EffectPoolSystem`, for every range check. Effects play for cues the client
can see:

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
`LoopSoundSystem` plays entity loops through 24 more, and `AnnouncerSystem` speaks alerts through one 2D source.

### Quality

The game fills one `PresentationQuality` and calls `PresentationQuality.Apply`. It covers effect and ground detail,
interaction maps, grass and row crop density and distance, antialiasing and shadow map size. Apply sets them all,
and again on each scene load. The shadow map size changes in memory only and is restored on quit. `Current` holds
the last settings and resets to full detail on play.
