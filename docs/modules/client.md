# Client modules

[Modules](../modules.md) ▸ Interaction, Input, Presentation

Everything a player sees and touches. None of it loads on a headless server, and none of it changes the
simulation, so two clients may look slightly different.

## Interaction

UI state that input, the HUD and overlays share, created once per presented world. It lives in `Client/`.
`DefaultWorld` and `LiveQuery` give MonoBehaviours the presented world and a query that survives world swaps.

| Singleton | Role |
| --- | --- |
| `PlacementState` | Building placement: prefab, ghost position, yaw, validity, the held drag's start. Its `PlacementSegment` buffer holds a line's ghost segments |
| `PendingCommand` | A targeted command waiting for a click, such as attack-move or an aimed ability |
| `PointerState` | Whether the pointer is over UI, so world clicks are ignored |
| `SelectionDragState` | The selection box being dragged |
| `HoveredEntity` | The commandable entity under the pointer, written by `HoverInputSystem` |
| `CameraFocusRequest` | A minimap click moving the camera |
| `HotkeyPresses` | Keys pressed this frame, for command-card hotkeys |
| `ScrollEdge` | The screen edge the pointer is scrolling against, written by `CameraController` |
| `CursorIntent` | The cursor to show, a `CursorKind`, written by `CursorIntentSystem` |

## Input

| Type | Role |
| --- | --- |
| `RTSInputActions` | The Selection, Commands and Camera action maps. [Bindings](../getting-started.md#play) |
| `InputActionsProvider` | The one shared actions instance, with `Suspend` and `Resume` |
| `SelectionInputSystem` | Mouse gestures to `SelectionInput` |
| `CommandInputSystem` | Right-click, hotkeys and targeted commands to `PlayerCommand` |
| `PlacementInputSystem` | Moves and [turns](economy.md#facing) the placement ghost through `PlacementRotation` (drag, hold or tap R, Shift to snap) and confirms it on release, or drags and releases a [line](economy.md#line-placement) |
| `WorldPointer` | What the cursor points at, and the ground point |
| `CameraController` | Pan, edge scroll, eased zoom, rotate, map bounds. Pitch follows zoom height, flatter up close. Starts at the home base |

Clicks over the HUD, while placing, or while a command is pending never reach selection. A front end that handles
Escape itself sets `FrontEndOwnsCancel` and calls `MatchView.CancelInteraction()` first.

## Presentation

Rendering, HUD, effects and audio. `MatchView` is a once-per-frame read model for MonoBehaviours: local player,
relations and colours.

### HUD

`HUD.prefab` holds a UI Toolkit HUD. Its `HUDPanelLayouts` asset gathers the HUD tree (`HUD.uxml`), each panel's
shell and row templates (UXML beside each script) and the drag box. `HUDController` clones the tree, places its
panels in the tree's named slots, styles it with `HUD.uss` for layout and `HUDTheme.uss` for looks, hands the asset
to every panel and keeps it in sync with the simulation. Code fills in only per-frame data. Games add or replace panels by overriding
`CreatePanels`.

| Type | Role |
| --- | --- |
| Panels | `ResourceBar`, `SelectionPanel`, `CommandCard`, `Minimap`, `GameOverBanner`, each built from a `HUDPanelLayouts`; `LineCost` drives the `line-cost` row in `HUD.uxml` |
| Elements | `HUDIcon`, `HUDBar`, `CostRow`, `CommandButton`, `HUDTooltip`, `HUDSweep`, `HUDGlyph`, `HUDFade`: UXML elements filled through `Bind` or properties |
| `ICommandSource` | Adds command-card entries: purchases, abilities, stances, unit actions. Each has a slot and hotkey |
| `HUDText` | Every HUD string, by key. Set `HUDText.Current` to translate |
| `OverlayRenderer` | Instanced ground markers, health bars, placement footprint outline with blocked cells, rally markers, capture dials, garrison pips. Colours and sizes come from an `OverlayStyle` asset |
| `PlacementGhosts` | The placed building's real model as visual-only copies (`VisualPrefab`) in the `HyperRTS/Placement Ghost` material, tinted valid or invalid per entity, one per spot or line segment |

`HUDText` keys:

| Key | Text |
| --- | --- |
| `hud.pop`, `hud.power` | Resource bar labels |
| `hud.strategic-ready` | A charged strategic asset in the countdowns |
| `command.return`, `command.unload`, `command.deploy`, `command.pack-up`, `command.sell` | Fixed commands |
| `command.more`, `command.back` | Command card page cards |
| `stance.<stance>` | Stance names |
| `outcome.victory`, `outcome.defeat`, `outcome.draw` | Game over banner |
| `tooltip.build-time`, `tooltip.population`, `tooltip.power`, `tooltip.requires`, `tooltip.seconds` | Tooltip labels |
| `entity.<name>`, `resource.<asset>`, `ability.<name>` | Names, kebab-cased: "War Factory" is `entity.war-factory` |
| `entity.<name>.description` | Optional tooltip line under the name; `HUDText.Description` returns null when there is none |

The command card is a grid of 72 px cards, five a row, laid out in `HUDCommands.uss`. A build, train, research or
ability card is a picture: the icon fills the button, scaled and cropped, with the hotkey top left and the cost in a
strip along the bottom. Its name is in the tooltip. Caption buttons (stances, unload, sell) stay text.

While a line is dragged, `LineCost` shows its total, the valid `PlacementSegment`s times the per-segment cost, above and right of
the line's end. It turns `hud-costs--short`, the unaffordable card's colour, when the stock can't cover it.
While a single building is placed, `PlacementHint` shows `hud.rotate-hint` below and right of its ghost.

`PurchaseCommands` asks `PurchaseRules.FindRefusal` why a purchase is refused and shows it as a class on the card:

| Class | When | Look |
| --- | --- | --- |
| `hud-command--unaffordable` | `CannotAfford` | Cost in the warning colour, picture dimmed |
| `hud-command--locked` | `MissingPrerequisite` | Greyscale, dark, a small padlock badge top right |
| `hud-command--maxed` | `AtLimit` | Dimmed, the `count/max` badge in the warning colour |
| `hud-command--researched` | `Researched` | Greyscale |
| `hud-command--queued` | The selected producers have it queued | Count badge; the head shows a clockwise sweep |

A lightning badge marks an option whose producer is offline for low power. A refused card ignores presses and
hotkeys but still shows its tooltip: name, description, cost, build time, population, power and each missing
prerequisite in the warning colour. The tooltip shows on hover and on keyboard or gamepad focus, above the card.

The card has one level of paging, as in StarCraft II, so grid positions and hotkeys stay put. A `CommandEntry` with
`Page` 1 goes on the second page: `PurchaseCommands` puts a builder's advanced options (`BuildOption.Advanced`) there,
and every other source's entries stay on the first. When a second page exists, a More card with a chevron takes the
last of the 15 slots, after blank fillers, and opens it; there a Back card takes the same slot. Escape, a right click
on the card or a new selection turns back. The page is the `CommandCardPage` client singleton, so Escape reaches it
through `HotkeyInputSystem` or, for a front end that owns Escape, `MatchView.CancelInteraction`, which first cancels
a placement or targeted order.

`StrategicCountdowns` fills the `strategic-countdowns` slot at the top centre of `HUD.uxml` with a row per player
holding a [strategic asset](support.md#strategic-assets): the name and `m:ss` to the charge, or `hud.strategic-ready`,
in the owner's colour. Observers see it too.

The minimap shows the baked map image, local fog, unit and structure markers and the camera view.

### Cursors

`CursorIntentSystem` picks the `CursorKind` each frame, first match wins:

1. A scroll edge shows the matching Scroll cursor.
2. The pointer over UI shows Default.
3. Building placement shows Build, or Invalid on blocked ground.
4. A pending command shows its own cursor: attack-move and patrol AttackMove, rally Rally, unload Unload,
   abilities and powers Target.
5. Otherwise it asks `OrderResolver` what a right-click on the hovered entity gives each selected unit, so game
   `SmartOrderRule`s count too. Attack, Capture, Enter (Garrison), Repair, Gather and Build map to cursors,
   game orders to Target, and the highest wins in a mixed selection. A plain move keeps Default.
6. With no unit acting, a selectable entity shows Select, ground with only producers selected shows Rally, and
   anything else Default.

`CursorSet` is a ScriptableObject with one `CursorAnimation` per kind: fps, loop, and frames plus hotspot at each
pixel size. Default is required and stands in for a kind left out. `CursorDisplay` animates the hardware cursor
with `Cursor.SetCursor` and picks the drawn size closest to `baseSize` times its `Scale`. `CursorDisplaySystem`
shows the world's intent through a display the game hands it. Without one the OS pointer stays.

### Unit overlays

Selected and hovered entities get a ground marker draped on the terrain heightmap by `GroundMarker.shader`: a
rounded rectangle round a vehicle's hull, a circle round infantry and squads (wide enough for their figures) and
corner brackets round a building's box, turned with it like its `NavBox`. Selection draws a full line with a soft inner fill and settles in from a
larger, faded start over `selectSeconds`; hover draws a thinner, translucent line. Colours follow the relation:
own, ally, enemy, neutral. Markers read `LocalToWorld`, the pose the model is drawn with, so they stay on moving
units, and they are depth tested, so hulls and plants hide what lies behind them. Ships and bridge traffic keep the
marker no more than 2 m below them, and aircraft mark the ground.

Health bars are screen-aligned quads of a fixed pixel size (reference pixels at 1080p times `OverlayRenderer.UIScale`)
above the model's top: a dark backing, a fill cut into segments of `healthPerSegment` (one per member for squads)
and coloured from healthy to critical, with squad member pips on the left and veterancy rank pips on the right.
`OverlayRenderer.HealthBars` picks who shows one: everyone, damaged units, or only selected ones. Selected and
hovered units always do. Both overlays are one instanced draw each through `InstanceDataBatch`. The bars set the style's
material values only when the style, its edit count or the pixel scale changes, so Play mode edits to the asset
show at once.

### Team colour

`HyperRTS/LitTeam` is URP Lit that repaints the albedo in the owner's colour. It paints where the `_TeamMask`
texture's red channel allows, on UV0, and inside the `TeamStripAuthoring` height band if there is one.
`FactionPalette` holds this client's colours. `TeamColorOverride` swaps them, for example for colour-blind players
or own, ally and enemy colours. The validator flags units and buildings without a team colour material.

Units hidden behind cover still show. `UnitSilhouetteFeature`, a URP renderer feature, draws a team-coloured fill
and rim where a unit is hidden. Every rendered part of a unit is put on rendering layer 8 at bake time for this.
Grass and small plants are not cover: `FoliageGrass` marks stencil bit 64 and the silhouette skips those pixels, so a
unit driving through grass shows no team-coloured patches.

### Vertex animation

`HyperRTS/LitTeam` with the `_VAT` keyword on plays baked vertex animation textures (VAT), so crowds of animated
infantry draw as instanced static meshes. Every pass, shadows and silhouettes included, reads the same pose. A VAT
set is:

- `<Model>.fbx`: the rest pose. UV1 holds each vertex's texel, u = (column + 0.5) / width and v = row within the
  frame, so Unity may split vertices at seams. A model with `VAT/<Model>.vat.json` beside it imports uncompressed,
  without generated lightmap UVs, and with UV1 kept as 32-bit floats. VAT renderers can't be lightmapped.
- The position and normal textures the bake info file names: RGBA half float, Unity object space in metres. Each frame
  takes `rowsPerFrame` rows and every clip is stacked, so frame f's texel is (column, f x rowsPerFrame + v). They
  import exact: linear, point filtered, clamped, no mipmaps or compression.
- `VAT/<Model>.vat.json`: `positions`, `normals`, `width`, `height`, `rowsPerFrame`, `vertexCount`, `clips`
  (each with `name`, `startRow` as its first frame, `frameCount`, `fps` and `loop`), `boundsMin` and `boundsMax`
  over every frame. A looping clip does not repeat its first frame at the end.

Put the textures in the material's VAT slots, set its rows per frame and turn on `_VAT` (the Debug inspector shows
material keywords). A `VATClipTable` asset imports the bake info file through **Import Bake Info** in its context menu, then
stays hand-editable. `VATAnimatedAuthoring` beside the renderer bakes its start clip into `VATClip` and widens the
render bounds to cover every frame. It fails the bake when the material and table disagree.

Game code plays clips with `VATClip.Play(range, now)`, using `SystemAPI.Time.ElapsedTime` as `now`.
`VATClockSystem` feeds the same clock to the shader, so pausing and replay speed apply to the animation.

### Build reveal

Structure materials on `HyperRTS/LitTeam` turn on the `_BUILD` keyword, and the model then rises piece by piece with
its `ConstructionProgress`. `BuildRevealSystem` copies the progress to `_BuildProgress` on every mesh under the
building and takes it off when construction completes. UV3 holds each piece's build order: x from 0 (first) to 1
(last), y 1 where the mesh has build data, with UV1 and UV2 blank if unused. A piece appears once progress reaches
its turn and drops into place: `_BuildDropHeight` metres (0.5 by default) over `_BuildDropSlice` of the progress (0.05).
A one-piece model such as a wall panel sets a long slice and a big height, so it is lowered across most of its build.
Pieces whose order is below `_BuildDropFrom` (0 by default) are laid in place, as slabs and beds are.
Pieces not reached yet collapse in the vertex stage to the object origin, so every pass, shadows included, skips them
without alpha clipping. Meshes without UV3 data, materials without `_BUILD` and finished buildings draw as authored.

### Building lights

`_LightsOn` on `HyperRTS/LitTeam` multiplies emission, from 0 dark to 1 lit, and defaults to 1, so other meshes
glow as authored. `BuildingLightsSystem` writes it to every mesh under a building from its replicated `Offline`
state: dark while offline or under construction, lit otherwise. Each change flickers for
`BuildingLights.FlickerSeconds` before settling. A building first seen takes its state without a flicker. Windows,
floodlights and mast lights glow from the emission map, so they go dark on low power.

### Motion rig

`MotionRigAuthoring` on a unit or building prefab root moves the model's pivots on the client. Its slots list turrets, barrels
and launchers that ease onto a `WeaponPose` index, barrels that recoil per shot, wheels, sprockets and idlers that
roll, rotors that spin up in flight and down while `Docked`, and a tilt root (the hull) that pitches and rolls with
the ground under its footprint. On a unit with `FlightAuthoring` the tilt root leans with acceleration instead.
Ground vehicles also sway on a `Suspension` spring over the ground tilt: they squat as they speed up, dive as they
brake, roll out of turns and rock back from each recoil shot, by 1 to 3 degrees. The rig's sway, spring and recoil
kick fields tune it. **Fill from
model** finds the parts by their art names: `Hull`, `Turret`, `Barrel`, `Launcher`, `Wheel_L01`, `Sprocket_L`,
`Idler_L`, `Rotor_Main`, `Rotor_Tail` and propellers `Prop_01` on, which spin about the nose axis. It keeps slots it already lists, measures wheel radii and the footprint
from the meshes, and gives `Launcher` weapon 1 when the unit has an extra weapon or an interceptor. Numbered parts
aim their own weapon: `Turret_01` and `Launcher_01` weapon 0, `Turret_02` and `Launcher_02` weapon 1. It also fills the
weapons' and the interceptor's turret and barrel pivots. Same-named parts of every model under the rig, such as a
damaged model beside the intact one, all follow, and parts a Machine moves are left to it. With Damage Stages, the
other stage's matching body becomes the `tiltFollower`, which tilts and sways with the tilt root. A building's body never
tilts, so it gets no tilt root.
`MotionRigRule` checks every slot names a child of the rig and a weapon the unit has. Traverse and elevation rates and limits are weapon data, so the
rig only smooths the simulated pose.

The baker stores a `MotionRig` and a `MotionRigPart` buffer on the root. `MotionRigSystem` runs in
`PoseSystemGroup` on the interpolated unit and writes only child `LocalTransform`s, never the ghost root.
A rig with rotors also gets a `RotorSpin`, the fastest rotor's speed from 0 to 1, which `LoopSoundSystem` reads
after it for the rotor loop.
`PoseSystemGroup` runs first in `TransformSystemGroup` on presented worlds, so rig, machine and fall poses render the
same frame as the unit. Machine links still aim at last frame's matrices.

#### Squad figures

`SquadFiguresAuthoring` on a squad unit draws it as hand-placed VAT figures: children of its `Model`, each with
`VATAnimatedAuthoring`, listed in the order they stay. It names the idle, walk, run, fire and die clips, the run
speed and how long a fallen figure stays down. Each figure looks those names up in its own clip table, so figures
baked separately (another VAT width, other rows) mix in one squad; the bake fails when a table lacks one of the
five. Each `SquadFigure` carries its own `FigureClips`. `SquadFigureSystem` shows ceil(figures x
health fraction), at least one while alive. A figure health drops plays die once, then hides after the hold. A
squad spawned damaged hides its lost figures without the fall. The
rest walk or run by the unit's horizontal speed and fire for 0.6 s after each change of the primary weapon's
`WeaponPose.Shots`. A figure gets a new `VATClip` only when its state changes, and looping clips start at a
per-figure phase so the squad doesn't move in step.

### Visibility

A mesh draws while its `HiddenBy` has no `HideReason`. Baking gives every mesh and gameplay root one.
`RenderSwitch` sets a reason on a root's group or on one branch of it and flips `MaterialMeshInfo`'s enable bit, so
hiding moves no chunks and leaves no zero-scale matrices. The reasons are:

- `Fog` and `Inside`, from `FogVisibilitySystem`, on the whole group;
- `Stage`, on every damage or resource stage model but the one on screen;
- `Part`, on the intact or broken piece of a breakable part;
- `Figure`, on a squad figure out of play;
- `Site`, on a finished building's construction site dressing;
- `Variant`, on every [model variant](economy.md#line-placement) but the picked one, from `ModelVariantSystem`.

Each reason has its own bit, so revealing a unit never redraws a model another reason hides. Effects, sounds and
grass stamps that skip unseen units test `FogHidden` and `Inside` on the root.

### Fog

Entities hidden by fog, and passengers inside a container, stop rendering. `FogOfWarRenderer` publishes the local
fog grid and its colour as shader globals. `FogOfWarFeature`, on the URP renderer with its shader set to
`Hidden/HyperRTS/FogOfWar`, blends it full screen from the depth buffer before the transparents, so terrain and
buildings fog where they stand while health bars, markers and the placement ghost stay clear. Water and particles
apply it themselves through `SceneFog.hlsl` and `ParticlesFog.hlsl`. Map buildings stay visible, but their flags, dials and
pips hide under the local fog.

A client remembers enemy buildings where it last saw them. The memory is a visual copy of the building, team
coloured, that nothing can pick, select or count. The minimap shows memories as dimmed squares.

### Flags

Buildings fly their owner's flag. During a capture the owner's flag lowers over the first half and the capturer's
rises over the second. A neutral building's flag rises over the whole capture. A building under construction flies
no flag until it is complete. `HyperRTS/Flag` samples the game's flag `Texture2DArray`, one layer per `PlayerSide`,
and waves the cloth downwind.

### Destruction

An entity shows the damage stage model for its health and bursts that stage's effect. Spawn effects burst once.
Felled remains, such as trees, tip over as a rod pivoting at the stump, or lie down at once if the fall was out of
sight. `FallClock` steps the fall at a fixed rate from the `Topple` height, so a 15 m tree takes about 2.5 s and a
sapling under 1 s, and a crusher at the stump pushes it faster. The canopy bounces it once and it settles. The
tipping turns the remains' child models about the stump, recorded at rest in a `FallPart` buffer, and the root
keeps the simulation's final pose. A felled tree's spawn effect plays
where the canopy lands, as it lands.

`DebrisAuthoring` on a ruin throws a `DebrisSet`'s pieces once when the ruin appears; on a building with breakable
parts it throws them from each part that breaks. `DebrisSystem` flies them on the client in a pool of 160, bounces
them off the terrain height and lays them flat. Medium effects throw 70 % of the pieces and Low none. Pieces from one
place fly the same on every client. Flying pieces draw in one instanced call per material and mesh, so a set's
material needs GPU instancing on, which the set checks at bake.

A piece at rest moves to `LastingDebris` and stays for the match. `EffectDetail.LastingDebrisAt` sets the cap: 20,000
pieces on High, 8,000 on Medium and none on Low. Past the cap the oldest piece sinks into the ground over 5 s and its
slot is reused; up to 512 sink at once. `LastingDebrisDraws` draws the layer through a `BatchRendererGroup`, one
indirect draw per mesh and material, from GPU buffers written only when a piece settles, sinks or goes, so pieces at
rest cost no CPU. It reads the matrices through the material's DOTS instancing path, so debris materials use a
shader with DOTS instancing, such as `LitTeam`. Changing effect detail starts the layer over at the new cap. Fogged
pieces draw as flying ones do.

A building with `BreakablePartsAuthoring` lists its parts, each an intact and a broken model. **Find Parts By Name**
in its context menu fills the list from `<Part>_Intact` children and their `<Part>_Broken` siblings, which it saves
hidden at scale 0 so editor views show the standing building. `BreakablePartsBakingSystem` bakes them back at full
size, hidden. Health picks
how many break: round(parts x `breakShare` x (1 - health fraction)). The next to break is the intact part nearest the
latest impact on the building, else the first in bake order, and it bursts the part effect outward from the centre.
Repair restores the latest broken part first. A building first seen, or changed out of sight, shows its breakage
without effects. It is presentation only: the simulation never reads it.

### Shaders

| Shader | Use |
| --- | --- |
| `HyperRTS/Water` | Depth colour, refraction, ripples, screen-space scene reflection over the sky, foam and swells at banks, unit wakes (`SampleWake`). Needs Depth and Opaque Textures |
| `HyperRTS/Foliage` | Alpha-cut plants and tree leaves with wind, trample, translucency and dry and green patches. Lit by the sun, Forward+ point and spot lights (per vertex on scattered grass) and Surface Cache GI |
| `HyperRTS/Bark` | Opaque tree bark with wind. Mask map: R metallic, G AO, A smoothness |
| `HyperRTS/RowCrop` | GPU-placed field crops. See [row crops](#row-crops) |

Water vertex colours shape the flow: alpha thins fords, green streaks rapids foam, and red trails foam lines
downstream of fords and bridges. Open water carries no streaks; banks get their own foam band.

URP keyword lists shared across passes live in `Presentation/Rendering/Keywords/` and are pulled in with
`#include_with_pragmas`: `ForwardLightingKeywords` (forward passes on the full Lit model), `SimpleLightingKeywords`
(plant and flag forward passes), `ShadowCasterKeywords` and `DepthNormalsKeywords`. An include holds only lines
identical in every pass that uses it, so a pass needing a different line keeps its own; material keywords
(`shader_feature_local`) stay in each shader.

### Atmosphere

An `Atmosphere` component sits on the root of a weather's atmosphere prefab, together with its sun, post-processing
volume and `Wind`. When enabled it sets the skybox, ambient light, reflection cubemap and wetness, and turns Unity
fog off. A missing skybox or reflection is an error.

The scene fog is URP's: its `Volumetric Fog` renderer feature draws the profile's `Fog` override (height fog, and
volumetric fog where the override turns it on) over the opaques after the skybox, before the opaque texture is
copied. URP compiles it only under the `VOLUMETRIC_FOG` scripting define, so a project using it sets that define;
the engine references its types only in `BattleHazeSystem.Fog.cs`, under the same define. URP skips orthographic cameras without resetting its fog globals,
so `FogOfWarFeature`, after URP's feature on the renderer, clears them there with `ClearAtmosphereFogPass` and
captures such as the minimap stay clear.

`BattleHazeSystem` thickens the volumetric fog over a fight while `PresentationQuality.BattleHaze` is on. Its
`BattleHaze` is a 128 by 128 grid over the map's square (the longer side): seen impacts add haze by splash, burning
wrecks (`Decay` remains with `LoopEffect`s) and damaged buildings (`ShownDamageStage` past intact) add it each
second, and a ruin's collapse (`SpawnEffect`, not a falling tree) adds a puff. Four times a second it drifts at twice
the `Wind` strength in m/s downwind, evens 20 % a second out with its neighbours and thins to half every 90 s, up to
four times the weather's own density. `BattleHaze.Add(position, radius, amount)` is open to any other source, such
as a field fire. The grid becomes a 128x2x128 R16F `Texture3D` holding one plus the haze, and a global volume above
the weather's (priority 100) overrides only the `Fog` density mode (Texture), the density texture and its tiling, so
the rest stays the weather's.

URP samples the density texture at the voxel's world position times the tiling, repeating from world zero, so the
tiling is 1 / side and the texels roll by the map's minimum in cells (a minimum off the 4 m or so cell grid is out by
under a cell). The two y layers match, so height comes only from the fog's own falloff. The tiling's floor of 0.001
caps the haze to maps up to 1000 m across. The volumetric fog fills view depth (eye depth, not ray length) up to its
cutoff, 100 m on the game's levels, and the height fog alone draws past it, so the haze shows over the near and
middle ground and fades toward the top of a far-zoomed screen. Each step is one Burst job and a 64 KB texture
upload, inside a budget of 0.05 ms of CPU; the GPU samples the texture inside the fog's own 1 ms.

Transparent shaders apply both fogs in the full-screen order with `SceneFogAt` and `ApplySceneFog` from
`SceneFog.hlsl`, and need `#pragma multi_compile_fragment _FOG_ANALYTIC _FOG_VOLUMETRIC`. A shader that samples the
opaque texture takes URP's fog out of it first with `AtmosphereFogAt` and `RemoveAtmosphereFog`, as water does for
the bottom it refracts, so it fogs once. `HyperRTS/Particles` and `HyperRTS/ParticlesUnlit` let URP's particle pass
apply its fog (`_TRANSPARENT_RECEIVE_FOG`) and add only the fog of war.

### Contact shadows

`ContactShadowsFeature` gives small things (fences, poles, wheels, grass clumps, infantry) short, sharp sun shadows
where they meet the ground, which a soft cascaded shadow map seen from the match camera loses. It needs URP's Screen
Space Shadows feature on the same Forward or Forward+ renderer, and its shader set to `Hidden/HyperRTS/ContactShadows`;
the validator checks all three. Right after URP resolves the sun's shadows into its screen-space texture, the pass
traces the depth buffer from each pixel toward the sun (`length` 1 m in up to `steps` 12, one every 3 pixels on screen,
jittered per pixel), counts a hit when the trace passes less than `thickness` behind a surface, and registers a darkened
copy under the same global. Every opaque shader that reads the sun's screen-space shadow then gets contact shadows
without changes; transparents keep the cascades. `strength` scales with the sun light's shadow strength, so diffuse
weathers show none. The feature runs from `lowestQualityLevel` up (1, so the Low level skips it), and only in game and
scene view perspective cameras.

### Wind

`Wind` sits on the scene's directional `WindZone` and publishes its strength, gusts and direction to shaders. Its
`vegetation` setting picks how much plants move: `None`, `Sway`, `Branches` or `Flutter`. Flags fly regardless.
Every shader pass of Foliage and Bark uses the same wind, so shadows match.

Grass, crops and other details sway about their origin, mostly with the gusts rolling downwind and a little in their
own phase; a row crop's normal tilts with it, so its sheen rolls too. Trees bend at the trunk, sway their branches and flutter
their leaf cards. Bark and leaves of one tree need the same `_WindStrength`, `_WindSpeed`, `_BranchSway`,
`_TreeHeight` and `_WindScale`. Set `_WindScale` to 0 for fallen trees.

Tree meshes for Foliage with **Tree Wind** on, and for Bark, carry wind data in object space, in metres:

| Channel | Data |
| --- | --- |
| TEXCOORD2.xy, TEXCOORD3.x | Branch pivot |
| TEXCOORD3.y, TEXCOORD4.xy | Leaf card pivot |
| COLOR r | Canopy ambient occlusion |
| COLOR g | Branch flex: 0 at the branch pivot and on the trunk, 1 at the tip |
| COLOR b | Stiffness: 0 limb, 1 twig |
| COLOR a | Flutter weight, 1 on leaf cards |

### Interaction maps

Vehicles leave ruts and flatten grass and crops, shells leave craters and scorch, and units moving through water
leave wakes, and the ground keeps rubble, ash, fuel and wet mud. `InteractionMap` on the rig keeps four GPU textures
over the map and stamps them each frame. It needs
the `Hidden/HyperRTS/Interaction Brush` shader.

| Texture | Channels |
| --- | --- |
| `_HyperRTSTrample` | RG travel direction, B flattened grass that recovers over 60 s, A crop damage that never recovers |
| `_HyperRTSTracks` | R rut depth and G churned soil, which fade over 4 minutes. B scorch and A crater depth (up to 2 m), which last the match |
| `_HyperRTSWake` | R foam, which fades over 5 s |
| `_HyperRTSRemains` | R rubble, G ash, B fuel, A wet mud, at 0.4 m (0.8 m at Low). They only build up and last the match |

Vehicles with `TrackMarks` stamp while on the ground and visible, as do building footprints. At Full quality each
walking squad figure leans the plants along its path. Every ground impact
whose `ImpactMark` has a `craterDepth` or `scorch` stamps a round bowl half the mark's size across, inside a
slightly wider scorch disc, and flattens the plants over it. Its `rubble`, `ash` and `fuel` cover the scorch's
disc in the remains map. A death cue the local player sees on the ground stamps the type's `DeathRemains` (from
`DeathRemainsAuthoring`: a radius and the four amounts), such as fuel and wet mud under a vehicle's hull. Other
sources (collapsing buildings, field fires) call `InteractionStamps.AddRemains` with a centre, a radius and the four
amounts, after `InteractionStampSystem` has run. Units with a `NavAgent` riding water cells stamp foam
behind them. A replay seeking back clears the maps, as it clears the decals.

Shaders read the maps through `Trample.hlsl`. The terrain darkens ruts and flattened grass, bends its normal down
the crater's slope so the sun lights the bowl, darkens and dampens the bowl's soil, and blackens scorched ground. `TerrainRemains.hlsl` then lays the terrain
material's tiled rubble, ash and wet mud textures (`_Remains*`) and an untextured dark, glossy fuel slick with a faint
thin-film sheen over what the remains map covers, with a noisy fringe; ground without remains skips it after one
sample.
Foliage leans plants along the travel direction, and water reads `SampleWake`. The quality setting picks Full, Low
at half resolution, or Off, which frees the textures.

Craters also dent the drawn ground. In presented worlds `CraterTerrainSystem` redraws the active Unity Terrain's
heightmap under each new bowl on the GPU, with the `Hidden/HyperRTS/Crater Height` shader from the package's
`Resources`: the heights copied at the start, less the tracks map's crater depth. It reads the heights back at most
every 0.5 s, on a frame with no new crater, so Unity refines the terrain mesh there. A replay seek, new or removed
maps, another terrain and the world's end put the copied heights back, so dented heights never reach the
TerrainData asset. `CraterSink.hlsl` samples the same depth: the `LitTeam`, `Foliage` (grass included), `Bark` and
`RowCrop` vertex stages, shadow and depth passes too, lower the whole object by the depth at its origin. The sink
fades with the origin's height above the undented ground (`_HyperRTSUndentedHeights`, published by `CraterTerrain`):
full within 0.5 m, none from 2 m up, so aircraft, bridge decks and rooftops stay put. The grass and row crop kernels,
which get the tracks map bound by `CraterTerrain.BindCraters` for each camera, place plants on the undented ground
first. The simulation's ground never changes, so craters
are not cover.

### Building footprints

`BuildingFootprintSystem` draws every building's turned obstacle box into an R8 mask over the map at nav-cell
resolution and publishes it as `_HyperRTSFootprints`, with `_HyperRTSFootprintRect` (zero without a map). Cells
within half a cell of a wall count too. Grass and row crops skip plants where it is set, whatever the
interaction-map quality, through `BuildingFootprints.hlsl`. It redraws only when buildings appear or go and their
boxes differ, and each redraw bumps `BuildingFootprintMask.Version` so cameras rescatter their plants. With no map
loaded it holds no texture.

`LocalGustSystem` also sends up to 8 radial gusts to `Wind.hlsl`: downwash under a seen rotorcraft within its rotor
wash height of the ground (25 m without one), and the blast of each seen impact with a splash of 1 m or more, which
spreads out and relaxes over about a second. Plants lean away from them and tree trunks bend as in wind. Interaction quality Off turns them off.

### Row crops

Fields of crops in rows, placed and drawn on the GPU, so no crop is an object. The map tools add a `RowCropRenderer`
to each map scene and bake its `RowCropFields`: which field covers each metre, and each field's crop, row
direction and tramlines. A field's `RowCropTramlines` are pairs of wheel lines along its rows where no plant grows;
the map tools paint the same lines into the terrain.

A `RowCrop` asset describes one crop: model variants with full, simple and card meshes, materials, row and plant
spacing, random scale, turn and lean, and two LOD distances. Variant meshes face +Z with the origin at the root.
Its `height` also shades the field: light falls off below the heads to `_CanopyShade` at the roots, so rows show
dark gaps between lit tops. That canopy term is the crop's ambient occlusion, so `_ScreenOcclusion` (0.2 by
default) keeps screen-space AO from darkening it again: GTAO reads a dense field of blades as a deep crevice and
turns shaded wheat into a flat dark patch. Foliage has the same property at 1.

A camera regrows the plants of its visible tiles when it moves, the quality changes or a trample map is published,
which regrows them every frame. Trampled plants bend or disappear, and none grow under a building's footprint. Plants
shrink away over the last 15 % of the draw distance. It also runs in edit mode.

#### Scatter host

Row crops and grass (see [maps](../maps.md)) share one host, built to take more scattered things such as debris:

- `ScatterRenderer<TState>`, the base of `RowCropRenderer` and `GrassRenderer`, holds the `compute` and `terrain`
  fields, builds and releases the inputs, keeps one state per camera in `CameraStates`, culls tiles through
  `ScatterTiles` and, in edit mode, rebuilds after `OnValidate` or a map rebuild. A subclass makes its inputs, tiles
  and camera states, says when its data went stale and grows and draws each camera. Its `Density` and `Distance`
  stay its own, written by `PresentationQuality`.
- `ScatterBatch<TJob>`, the base of `RowCropBatch` and `GrassBatch`, holds one camera's slots: a mesh and property
  block each, the jobs queued for the grow kernel and the `ScatterDraws` indirect draws. `Generate` reserves room,
  binds the jobs, grows and counts; `Draw` draws each slot with room. A subclass owns its slots' room, buffers,
  material, culling box and shadows.
- `Scatter.hlsl` holds the kernels' hashes, noise and slot counting; instances are 48 bytes.

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
- **Loops**: effects at sockets while health is in range, such as fires on a damaged building. A building
  under construction streams none.
- **Trails** along a projectile's path.
- **Motion dust** by ground material and speed, measured from the drawn motion along the facing so it rises on
  network clients and sideways crowd pushes raise little.

| Effect detail | Particles | Extra |
| --- | --- | --- |
| Low | 35 % | Loop range halved, no ambient life |
| Medium | 70 % | |
| High | 100 % | Light flashes from 8 shared lights |

Effect materials use the engine's particle shaders so transparent smoke and flames take URP's fog and fog of war
like the ground beneath them. `HyperRTS/Particles` and `HyperRTS/ParticlesUnlit` are URP's Simple Lit and Unlit
particles with the same properties and inspector. Additive particles fade to black under fog, the rest toward the
fog colour. `HyperRTS/ParticlesSixWay` lights smoke from six baked directions.

### Audio

`SoundPlaybackSystem` plays cue sounds through 32 pooled sources. `SoundListener` follows the camera's ground focus.
`LoopSoundSystem` plays entity loops through 24 more, and `AnnouncerSystem` speaks alerts through one 2D source.

### Quality

The game fills one `PresentationQuality` and calls `PresentationQuality.Apply`. It covers effect and ground detail,
interaction maps, grass and row crop density and distance, and render scale. Apply sets them all, and again on each
scene load. Shadows and the other pipeline features come from the game's quality levels, each with its own URP
asset, so the game sets the quality level before Apply, which writes render scale onto that level's asset.
`Current` holds the last settings and resets to full detail on play.
