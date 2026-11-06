# Client modules

[Modules](../modules.md) ▸ Interaction, Input, Presentation

Everything a player sees and touches. None of it loads on a headless server, and none of it changes the
simulation, so two clients may look slightly different.

## Interaction

UI state that input, the HUD and overlays share, created once per presented world. It lives in `Presentation/Interaction/`.
`DefaultWorld`, `LiveQuery` and `LiveSingleton<T>` (in `Presentation/Common/`) give MonoBehaviours the presented world, a query that survives world swaps and a singleton read (`TryGet`) and write (`Set`) that does the same. `MatchView` owns the placement, pending command and command page. A new HUD-to-input channel is a struct, one line in `ClientSingletonSystem` and its readers.

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
| `RTSInputActions` | The Selection, Commands and Camera action maps. [Bindings](../getting-started.md#play) |
| `InputActionsProvider` | The one shared actions instance, with `Suspend` and `Resume` |
| `InputMapSystem` | Base of the systems below: enables its input map while it runs |
| `SelectionInputSystem` | Mouse gestures to `SelectionInput` |
| `CancelInputSystem` | Escape undoes the topmost client mode through `InteractionState.Cancel`: placement, then a pending targeted command, then the card page |
| `CommandInputSystem` | Right-click, hotkeys and targeted commands to `PlayerCommand` |
| `PlacementInputSystem` | Moves and [turns](economy.md#facing) the placement ghost through `PlacementRotation` (drag, hold or tap R, Shift to snap) and confirms it on release, or drags and releases a [line](economy.md#line-placement) |
| `WorldPointer` | What the cursor points at, and the ground point |
| `CameraController` | Pan, edge scroll, eased zoom, rotate, map bounds. Pitch follows zoom height, flatter up close. Starts at the home base |

Clicks over the HUD, while placing, or while a command is pending never reach selection. A front end that handles
Escape itself sets `FrontEndOwnsCancel` and calls `MatchView.CancelInteraction()` first. That method, `CancelInputSystem` and the
command card's right click all use the one order in `InteractionState.Cancel`.

## Presentation

Rendering, HUD, effects and audio. `MatchView` is a once-per-frame read model for MonoBehaviours: local player,
relations and colours. It and other presenters read singletons through `LiveSingleton` and re-read only when a
`ChangeWatch` sees a watched component written or an entity count move.

`Environment/` is split by topic, each folder its own namespace: `Ground` (terrain, roads, craters), `Trampling`,
`Fire`, `Weather` (atmosphere, sky, wetness, wind, gusts, haze, cloud shadows), `Water`, `Vegetation` (`Grass`,
`RowCrops`, `Scatter`, `Shading`), `Horizon` and `Ambient`. `EdgeNoise.png`, which several of them share, stays at the top; their cell grids
are the simulation's `CellGrid`.
`Quality/` holds the knobs the game sets from its graphics options.
`Effects/` is split into `Rig` (the rig component, its graphs, pools and playback), `Streams` (graph streams, trails,
dust and rotor wash, and loop effects), `Lights` (effect and loop lights, smoke shadows) and `Shaders` (the Custom HLSL
includes and the two effect shader graphs). In `Rendering/`, the instance batches and `EntityExtent` sit in `Overlays/`,
the fog of war renderer feature and its passes in `Fog/`, and the hide, model variant, garrison part and paint core in
`Rendering/Parts/` (namespace `HyperRTS.Presentation.Rendering.Parts`). `Rendering/Materials/` holds the `LitTeam`
shader with its team colour, faction palette, grime and building lights; contact shadows and the unit silhouette stay
at the `Rendering/` root.

### HUD

`HUD.prefab` holds a UI Toolkit HUD. Its `HUDPanelLayouts` asset gathers the HUD tree (`HUD.uxml`), the row templates
(UXML beside each script), the command button parts (`CommandButton.uxml`) and the drag box. `HUD.uxml` holds each
panel shell as a template instance in stacking order. `HUDController` clones the tree, unwraps the instances, hands each
shell to its panel, styles it with `HUD.uss` for layout and `HUDTheme.uss` for looks and keeps it in sync with the
simulation. Code fills in only per-frame data. Games add or replace panels by overriding `CreatePanels` (`Adopt` for a
shell already in the tree, `Add` for a new one). `HUDContext` re-reads the selection and the purchase buttons only when
their `ChangeWatch` fires.

`HUD/` holds the controller, the `HUD*` building blocks and the sheets. `Commands/` (the card, its buttons and the
`ICommandSource`s), `Map/` and `Panels/` (resource bar, selection, production queue) each have their own namespace
and keep each UXML beside its script. `SelectionDragBoxUI` is in `HUD/` too.

| Type | Role |
| --- | --- |
| Panels | `ResourceBar`, `SelectionPanel`, `CommandCard`, `Minimap`, `GameOverBanner`, each built from its shell in `HUD.uxml`; `LineCost` drives the `line-cost` row in `HUD.uxml` |
| Elements | `HUDIcon`, `HUDBar`, `CostRow`, `CommandButton`, `HUDTooltip`, `HUDSweep`, `HUDGlyph`, `HUDFade`: UXML elements filled through `Bind` or properties |
| `ICommandSource` | Decides which [command specs](#hud) apply to the selection and shows their live state: purchases, abilities, `OrderCommands` (stances, return, unload, deploy, pack up, sell) and squad retreat and reinforce. Placement, labels and hotkeys are the spec's, not the source's |
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
| `command.retreat`, `command.reinforce` | Squad commands, hotkeys R and T in the game's specs |
| `squad.<posture>`, `squad.heavy-cover`, `squad.light-cover`, `squad.dead` | A soldier card's state |
| `squad.steady`, `squad.suppressed`, `squad.pinned`, `squad.retreating`, `squad.reinforcing` | A squad's suppression level and status |
| `tooltip.retreat`, `tooltip.reinforce`, `tooltip.reinforce-time`, `tooltip.no-retreat-point`, `tooltip.no-reinforce-point`, `tooltip.full-strength`, `tooltip.cannot-afford` | Squad command tooltips |
| `outcome.victory`, `outcome.defeat`, `outcome.draw` | Game over banner |
| `tooltip.build-time`, `tooltip.population`, `tooltip.power`, `tooltip.requires`, `tooltip.seconds` | Tooltip labels |
| `entity.<name>`, `resource.<asset>`, `ability.<name>` | Names, kebab-cased: "War Factory" is `entity.war-factory` |
| `entity.<name>.description` | Optional tooltip line under the name; `HUDText.Description` returns null when there is none |

The command card is a grid of 72 px cards, five across, laid out in `HUDCommands.uss`. A build, train, research or
ability card is a picture: the icon fills the button, scaled and cropped, with the hotkey top left and the cost in a
strip along the bottom. Its name is in the tooltip. Order buttons (stances, unload, sell) show their label key as a caption unless the spec has an icon. Every button is built from
`CommandButton.uxml`, whose parts (picture, caption, hotkey, cooldown, badges, cost strip) are hidden by the `hrts-hidden`
class until a source shows them.

One selected squad adds a `SquadView` to the selection panel: a `SquadMemberCard` per soldier (number, health, the
cover it holds or its posture, greyed when dead), the squad's suppression bar with its level, and while it
retreats or reinforces, that status with the current soldier's progress. `SquadCommands` adds Retreat and Reinforce
to the card. A refused one stays and its tooltip names the reason: no retreat
point, no reinforce point in range, full strength or too poor (the price of one soldier is shown). The reason is a
`SquadRefusal` from `SquadOrderRule`, shown by `CommandButton.ShowSquadRefusal` as `hud-command--no-point`,
`--full-strength` or `--too-poor`. Clients read the `Retreating` and `Reinforcing` enable bits and the reinforcing
progress, which are replicated.
A card names the soldier's weapon: its member's own `Designation` (baked, never sent), or the designation of the upgraded mount it fires (`WeaponStats.designation`, baked into
`WeaponPose.Designation`, never sent) shown through `HUDText.WeaponName`; a digging member reads "Digging", and a squad
at work shows "Building" with the site's progress (`SquadWork.Progress`, replicated). The build tooltip lists the cap (`tooltip.limit`).

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

The card is data. A `CommandSpec` asset describes one button: what it is for (`CommandTarget`: a prefab to build, train or
research, an ability by name, or an order with its argument), the card it sits on (a builder or producer prefab, empty for
every card), a label key and icon (empty takes the prefab's or ability's own), a `Key` hotkey, and its page and slot.
A `CommandCatalogue` on `HUDController` lists them. A spec with a card beats one without for the same target, which is how
one faction differs from another. Sources look specs up by target and card, log an error for a gap and skip the button.

The card places entries by slot (0 to 14, left to right then top to bottom) and fills the gaps with blanks. A slot already taken moves the
entry to the next free one, and past the last slot to the next page. With more than one page the last slot of every page is
the page card: More to the next page, Back from the last to the first. A key press fires the one entry at that key on the
shown page. `CommandSpecRule` rejects two specs that share a key on one page of a card, a key an input action already
uses (except the building rotation, which only acts while placing, when card hotkeys are off) and a spec missing its target.

Escape, a right click on the card or a new selection turns back. The page is the `CommandCardPage` client singleton;
`InteractionState.Cancel` undoes a placement, else a targeted order, else the page, for `CancelInputSystem`, for a front end
that owns Escape through `MatchView.CancelInteraction`, and for the card's right click.

`StrategicCountdowns` fills the `strategic-countdowns` slot at the top centre of `HUD.uxml` with a row per player
holding a [strategic asset](support.md#strategic-assets): the name and `m:ss` to the charge, or `hud.strategic-ready`,
in the owner's colour. Observers see it too.

The minimap shows the baked map image, local fog, unit and structure markers and the camera view. Their colours, line widths, marker radii and fog alphas are `--minimap-*` custom properties in `HUD.uss`, which a theme overrides; the view outline repaints only when its corners move.

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
rounded rectangle round a vehicle's hull, a circle round infantry and squads (wide enough for their living members) and
corner brackets round a building's box, turned with it like its `NavBox`. Selection draws a full line with a soft inner fill and settles in from a
larger, faded start over `selectSeconds`; hover draws a thinner, translucent line. Colours follow the relation:
own, ally, enemy, neutral. Markers read `LocalToWorld`, the pose the model is drawn with, so they stay on moving
units, and they are depth tested, so hulls and plants hide what lies behind them. Ships and bridge traffic keep the
marker no more than 2 m below them, and aircraft mark the ground.

Health bars are screen-aligned quads of a fixed pixel size (reference pixels at 1080p times `OverlayRenderer.UIScale`)
above the model's top: a dark backing, a fill cut into segments of `healthPerSegment` (one per member for squads)
and coloured from healthy to critical, with one squad member pip per member on the left, dark once dead, and veterancy rank pips on the right.
`OverlayRenderer.HealthBars` picks who shows one: everyone, damaged units, or only selected ones. Selected and
hovered units always do. Both overlays are one draw each through `InstanceBuffer<T>`. The bars set the style's
material values only when the style, its edit count or the pixel scale changes, so Play mode edits to the asset
show at once. A squad's bar also has a suppression strip below it (grey, yellow while suppressed, red while pinned,
blue while retreating) and a small shield after its member pips for the best cover its living soldiers hold: green
for heavy, yellow for light (`OverlayStyle` Squads colours, `SquadBarState`).

`CoverPreview` draws rings on the cover spots a move would use when squads are selected and the pointer is over open
ground (`HoveredEntity.Ground`): spots within `CoverRules.Reach` of the point that face the way the squads would walk
(`CoverRules.Faces`, the same test `SquadCoverSystem` uses), green for heavy and yellow for light, or one red ring
when there are none. Craters are server only, so the preview does not show them.

### Team colour

`HyperRTS/LitTeam` is URP Lit that repaints the albedo in the owner's colour. It paints where the `_TeamMask`
texture's red channel allows, on UV0, and inside the `TeamStripAuthoring` height band if there is one.
`FactionPalette` holds this client's colours. `TeamColorOverride` swaps them, for example for colour-blind players
or own, ally and enemy colours. The validator flags units and buildings without a team colour material.

Ground vehicles with `GrimeAuthoring` collect grime. `GrimeSystem` advances each unit's `Grime` from the distance it drove, the
`GroundMaterials` cell under it, `Wetness.Level` and its health: dust on dry Soil, Gravel and Sand, mud on Mud and on wet Soil
and Grass, wet in Water (drying over a minute, washing dust and some mud off), soot from damage and never falling. It
writes `_Grime` and `_GrimeBand` to every mesh under the unit, and only once a level has moved by 0.02. LitTeam's
forward pass (`LitTeamGrime.hlsl`) lays dust and mud low on the hull by noise, up to the band heights, in the
`_HyperRTSDustColor` and `_HyperRTSMudColor` the weather `Atmosphere` publishes. Units only, never structures.

LitTeam carries only what materials use: opaque or alpha tested, metallic workflow, normal, metallic gloss, occlusion
and emission maps, no detail maps, parallax or transparency. Its keywords are `_NORMALMAP`, `_METALLICSPECGLOSSMAP`,
`_OCCLUSIONMAP`, `_EMISSION`, `_ALPHATEST_ON`, `_BUILD`, `_BONES` and `_VAT_RIGID`. Its inspector, `LitTeamGUI`, shows the
properties as plain fields and keeps each map's keyword on while the map is set, `_EMISSION` while the emission colour
isn't black, and the alpha-test queue while alpha clipping is on, as URP's Lit inspector does; the model hooks write
the same. `Rendering/SurfaceMaps.hlsl` holds the base colour and normal map accessors that LitTeam and Bark share.

Units hidden behind cover still show. `UnitSilhouetteFeature`, a URP renderer feature, draws a team-coloured fill
and rim where a unit is hidden. Every rendered part of a unit is put on rendering layer 8 at bake time for this.
Grass and small plants are not cover: `FoliageGrass` marks stencil bit 64 and the silhouette skips those pixels, so a
unit driving through grass shows no team-coloured patches.

### Bone animation

`HyperRTS/LitTeam` with the `_BONES` keyword skins a mesh in the vertex stage from a bone animation texture, so crowds
of animated infantry draw as instanced static meshes. Every pass, shadows and silhouettes included, reads the same
pose. The source is a `BoneAnimationSet` asset (an editor-only ScriptableObject, so the rig model and its clips never
reach a build):

- `rig`: a model imported with a Generic or Humanoid rig. `skeleton` names its node holding the skinned mesh whose
  bones and bind poses form the skeleton; any number of bones and any names work, they are data. The rig also holds the
  socket nodes.
- `clips`: a name, an AnimationClip (from the rig's own file or another model's), loop, `inPlace` and speed. Sampled at
  `fps` (30), a looping clip does not repeat its first frame at the end. A Generic clip finds its bones by path from
  the parent of `rootBone`, so a clip file and the rig must name the same bones under the same parent (the Mixamo
  skeleton exported from Blender under an `Armature` node does, while a Humanoid rig is untried). `inPlace` removes the
  root bone's ground travel, the straight line from its first to its last position, so locomotion with root motion
  plays on the spot and still loops; height and sway stay. The table records that travel as the clip's `groundSpeed`
  in metres per second (0 for clips not in place, or made without travel).
- `rootBone`: the top bone of the skeleton, such as the hips.
- `upperRoot`: the bone that roots the upper body, which plays its own clip; the bones below it join it.
- `parts`: meshes to export, each one or more `nodes` merged into one mesh of one submesh (a soldier's body, head and
  gear at one level of detail, so a figure draws as few renderers), from the rig or from the part's own `model`, so one
  set, and one texture, serves every kit and weapon file on the skeleton. A skinned node keeps its weights; its bones match the skeleton's by name
  and its bind poses may differ only by one move of the whole mesh (its node's axis conversion), which the bake applies.
  A plain node with a `socket` is a weapon or gear weighted wholly to one bone: its origin sits on the socket's node,
  a skeleton bone or an empty under one (`sockets`, such as a rifle grip in the right hand), so weapons are swappable
  meshes modelled about their grip.

**Bake** (the asset's context menu, `HyperRTS/Animation/Bake Bone Animation Set`, or the `bone bake [set]`
[command](../editor.md#commands)) writes `Generated/` beside the set, labelled Generated:

- `<Set>_Bones`, a half-float RGBA Texture2D, point filtered, 3 texels a bone wide and one row per frame of every clip
  stacked (at most 16384). Bone b's texels at columns 3 b to 3 b + 2 are the top three rows of its skin matrix: the
  bone's pose times its inverse bind pose, mapping the skeleton mesh's rest vertices to the posed ones in the rig
  model's root space.
- `<Set>_Clips`, a `BoneClipTable`: the bones in texture order, the rows, the clips (`startRow`, `frameCount`, `fps`,
  `loop`, `groundSpeed`) and the bounds over every frame of every part.
- `<Set>_<Part>`, a plain Mesh with no skin: UV6 holds four bone indices and UV7 the four weights, negative for a bone
  of the upper body. Parts of one set share the texture; a skinned part whose bind pose differs from the skeleton's fails
  the bake.

The material turns on `_BONES` and points `_BoneTex` at the texture. Per instance (Entities Graphics properties),
`LowerClip` and `UpperClip` hold a `PlayingClip` packed as x start row, y rows, z signed fps (negative plays once) and w
start time, and `LowerFade` and `UpperFade` a `ClipFade`: the frozen row pair, its blend and the switch time. A vertex
takes up to four bones, each reading the clip of its half (the sign of its weight), blended between rows and, for
`_BoneFadeTime` (0.2 s) after a switch, from the frozen pose. That is 6 texel fetches a bone, 24 at most a vertex, plus 24
more during a fade. `BoneAnimatedAuthoring` on a renderer bakes the start clip into all four and widens the render
bounds to every row (`ClipBoundsBakingSystem`); `ClipSubmeshBakingSystem` gives a multi-material renderer's extra
entities the same. A figure's body (at three levels of detail in a LODGroup) and weapon are separate renderers that
game code writes the same clips to.

Game code plays clips with `PlayingClip.Switch` on `LowerClip.Clip` and the matching fade, using `SystemAPI.Time.ElapsedTime`
as the time. `ClipClockSystem` feeds the same clock to the shader as `_ClipTime`, so pausing and replay speed apply.

### Rigid vertex animation

`_VAT_RIGID` moves whole pieces instead of vertices (a sectioned building's walls and roofs breaking). The bake info
(`VAT/<Model>.vat.json`, written by the structure tool) names a `positions` and a `rotations` texture: a column per piece (the `width`), one row per frame and row 0 the frame every piece rests in. A mesh vertex
holds its piece's column in UV1's u. The material has the two textures in its `_VATPositions` and `_VATRotations` slots:
the vertex moves to the piece's position for the frame, turned by its rotation quaternion about where the rest row has
it, and normals only turn. The bake info lists `clips` (`name`, `startRow`, `frameCount`, `fps`, `loop`) and
`boundsMin` and `boundsMax` over every frame; a `VATClipTable` imports it through **Import Bake Info**, and
`VATAnimatedAuthoring` beside the renderer bakes its start clip into `PlayingClip` and the bounds. A model with
`VAT/<Model>.vat.json` beside it imports uncompressed, without generated lightmap UVs and with UV1 as 32-bit floats;
the textures import exact (linear, point filtered, clamped, no mipmaps or compression). There is no fade between clips.
`ClipSubmeshBakingSystem` gives a multi-material renderer's extra entities the clip.

### Machines

Animates machines such as pumpjacks, wind turbines, radar antennas and cranes. Put `MachineAuthoring` on the root and
`MachinePartAuthoring` on each moving child; a part with no machine above it fails the bake. A part can `Spin`, `Swing` in step with the spin, `Slide` to and fro
along its axis (a crane on its rail, amplitude in metres), act as a `Link` rod between two transforms, or turn
`Downwind` about its axis so its forward, plus `offset` degrees, follows the scene's `Wind` (a windsock). `Raise`
turns a part about its axis, `Extend` moves it along its axis by `amplitude` degrees or metres and `Stretch` scales it
to `openScale` about its own pivot as the machine speeds up, eased in and out: a launcher raising to its rest elevation, an outrigger jack lowering. Each moves over its
own span of the speed, `from` to `to`, so a launcher's jacks can finish lowering before its box starts to raise, and
packing up runs the stages in reverse. `Point` turns a part about its axis so its `aim` transform keeps the bearing it
had at rest, which makes a hydraulic ram and its rod track each other as the box they hold rises. `MachinePartRule`
flags a span that ends before it starts and a `Point` part with no aim, and `MachineRule` a ramp of 0 seconds. A part a machine moves is left out of the
Motion Rig's fill, and a weapon with no barrel slot fires from the `Barrel` or `Launcher` its machine raises.

A machine's `runs` says when its clock turns: `Always`, `Powered` while its building is complete and not `Offline` (a
radar stops on low power), `Producing` while its building's queue has work that isn't halted (a crane runs only during
production), or `Firing` for `keepRunningSeconds` after any of its building's weapons or interceptor fired (a gatling's
barrels), read from the replicated `WeaponPose` shot counts. The clock eases to a stop and back over `rampSeconds` rather
than snapping. `Deployed` skips the ramp and takes its speed from the replicated `Deployable.Progress` of the unit or
building above it, so `Raise` and `Extend` parts unfold over the deploy time and fold over the pack-up time. A machine
first seen mid-match starts at the speed it should have.

A door is a machine on its own: `Releasing` runs for `keepRunningSeconds` after the replicated `Producer.Released`
count of the producer above it rises, so every client sees it, enemies included (the owner-only production cue can't
drive it). Set `rampSeconds` to the time to open and `keepRunningSeconds` to that plus the time it stays open. The
part is `Raise` for a hinged door, `Extend` for one that slides and `Stretch` for a roll-up curtain pivoted at the top
of its opening, whose `openScale` of (1, 0.05, 1) coils it up toward the pivot; a `Stretch` part bakes a
`PostTransformMatrix`. A producer first seen mid-match adopts the count without opening, and a release while the door
closes sends it back up from where it stands. Under an aircraft with `FlightAuthoring`, a cargo ramp or bomb bay runs
`Dropping`, open while the replicated `DropRun.Active` is set over an airdrop or strike. A `Releasing` machine with no
`ProducerAuthoring` above it, or a `Dropping` one with no `FlightAuthoring`, fails the bake.

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
from the meshes, and gives `Launcher` weapon 1 when the unit has an extra weapon (an interceptor is one). Numbered parts
aim their own weapon: `Turret_01` and `Launcher_01` weapon 0, `Turret_02` and `Launcher_02` weapon 1. It also fills the
weapons' turret and barrel pivots. Same-named parts of every model under the rig, such as a
damaged model beside the intact one, all follow, and parts a Machine moves are left to it. With Damage Stages, the
other stage's matching body becomes the `tiltFollower`, which tilts and sways with the tilt root. A building's body never
tilts, so it gets no tilt root.
`MotionRigRule` checks every slot names a child of the rig and a weapon the unit has. Traverse and elevation rates and limits are weapon data, so the
rig only smooths the simulated pose.

Road wheels (`Wheel_` parts, marked `followsGround`; sprockets and idlers are not) follow the ground: each frame a
wheel's lift is the `TerrainHeight` under its footprint minus where that point of the tilted, swaying body is, held to
the wheel's `travel` (0.12 m) and eased over 0.06 s, along its parent's up. Tracks (`Track_L` paired with the `Wheel_L`
road wheels of the same model) bend with them: each track mesh carries a `TrackMeshAuthoring`, which bakes its
`_TrackSpan` in the mesh's object space, and the rig interpolates its wheels' lifts into 8 stations along the track,
written to `_TrackLift0` and `_TrackLift1` only when one moves by more than 5 mm. `LitTeamTrack.hlsl` lifts the lower
run by the stations around each vertex's z, fading from the axle height to the wheel tops so the return run stays put.
The track meshes must sit unrotated and unscaled against the rig.

The baker stores a `MotionRig` and a `MotionRigPart` buffer on the root. `MotionRigSystem` runs in
`PoseSystemGroup` on the interpolated unit and writes only child `LocalTransform`s, never the ghost root.
A rig with rotors also gets a `RotorSpin`, the fastest rotor's speed from 0 to 1, which `LoopSoundSystem` reads
after it for the rotor loop.
`PoseSystemGroup` runs first in `TransformSystemGroup` on presented worlds, so rig, machine and fall poses render the
same frame as the unit. Machine links still aim at last frame's matrices.

#### Squad figures

`SquadFiguresAuthoring` on a squad unit draws it as hand-placed bone-animated figures: children of its `Model`, each
with `BoneAnimatedAuthoring` renderers (body levels of detail in a LODGroup, a weapon) that share one `BoneClipTable`,
listed in the order of the squad's members, each with a `FigureClipSet` (a ScriptableObject) that picks its clips from
that table by name: legs for idle, walk and run in each posture, a fire clip per posture, a whole-body clip per
`MemberAction` (except None) and a death per posture for a shot and for a blast from each side. Every entry is required:
an unset one or a name the table lacks fails the bake, naming the set and the entry, and nothing falls back to the
standing clips. The authoring also holds the run speed and how long a fallen figure stays down. `FigureClipSet.Build`
resolves a set against a table into a `FigureClips` blob of ranges; the baker adds it to the blob store, so figures that
play the same set on the same table share one, and each `SquadFigure` and `FigureModel` keeps only the reference.
Figure i shows `SquadMember` i, so the count of
figures equals the count of members (a validation rule checks it). `SquadFigurePlacementSystem`, in the `PoseSystemGroup`,
eases each living figure toward its member's `Offset` (world axes) and turns it to its cover, composing the squad's own
`LocalTransform` with the local transforms down to the figure's parent, so figures never lag the squad by a frame. The
clips are `SquadFigureSystem`'s, in the presentation group, since cues and shots land after the transform group. The
baker lists each model's clip-playing renderers (levels of detail, weapon, material children) in a `FigureRenderer`
buffer on the squad, each part keyed by the model (base or other) it plays for. The legs play the clip for the member's `Posture` and its own
horizontal speed (a member catching up runs while the rest stand); the upper body plays the fire clip of the posture
once after each change of the `WeaponPose.Shots` of the weapon its member carries (a new shot restarts it) and keeps
firing for the clip's own length, else it plays what the legs play, in step with them. A member who died plays the
fall of its posture once on both halves: the shot fall, or when its `Death` is `Blast` the fall for the side (front,
back, left or right) the member's replicated `BlastHeading` gives relative to the figure's facing. Then the figure hides
after the hold. A squad spawned with dead members hides their figures without the fall. A figure gets new clips only
when its state, posture or firing changes, written to every renderer under it, and looping clips start at a per-figure
phase so the squad doesn't move in step. While the legs play a clip with a `groundSpeed` (walk, run, crawl), the system
plays it at the figure's averaged speed over that, from 0.5 to 1.6 times, so planted feet don't slide. A rate that moves
more than 3% rewrites the clip's start time to keep the frame showing, and the upper body takes the same rate while it
plays the legs' clip.

A figure may name an `upgradedRoot` with its own `upgradedClips` set and `upgradedWeaponName`, baked as a second figure entity
kept in `SquadFigure.Other`. When the member's `Weapon` differs from its `BaseWeapon` (an upgrade is in hand) the system
swaps the two: the new model is placed and posed afresh, the old one hides, and the card's name follows the model shown.

A figure may instead name an `emplacedRoot` (with `emplacedClips`), kept in
`SquadFigure.Other` with `FigureVariant.Reason` `Emplaced`: it shows while the squad's `Deployable` is `Emplaced` and the
member takes a crew place (a gunner with empty hands behind the deployed gun), and the carried model returns on pack-up.
A figure has at most one other model, so `SquadFiguresRule` errors when both roots are set or the figures and squad members differ in count.

`deployedModel` is a child of the unit (a gun on its tripod) baked as a render entity, hidden by `HideReason.Figure` and
shown while the squad's `Deployable` is `Emplaced`, from the start of deploying until packing up begins. A member with
`takesCrewPlace` then walks to its crew place and takes its posture in the simulation (see
[combat](combat.md#squads-and-members)), and its figure follows `SquadMember.Offset` and `Posture` like any other, using the
posture's idle and fire clips (for example `ProneFire` for a gunner 0.6 m behind the gun).

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

Passengers inside a container stop rendering at once. An enemy the local player saw fades out over 0.25 s once it
is in the fog (`FogVisibilitySystem.FadeTime`), then stops rendering; one coming into sight fades in. The fade is
`RevealFade`, `_Reveal` on `HyperRTS/LitTeam`, which drops pixels in a 4 by 4 dither in the forward and silhouette
passes; shadows stay until the unit hides. An enemy first spotted in the fog is hidden at once.

`FogOfWarRenderer` publishes the local fog grid and its look as shader globals. Each restamp sets a target opacity
per cell, and the shown grid eases toward it over `fadeTime` (0.3 s), so the edge moves smoothly; the shader blurs it
over about a cell, so it reads round. `FogOfWarFeature`, on the URP renderer with its shader set to
`Hidden/HyperRTS/FogOfWar`, copies the camera colour through the fog before the transparents, from the depth buffer:
fogged ground loses `desaturation` of its colour and darkens toward the fog colour, so terrain and buildings fog where
they stand while health bars, markers and the placement ghost stay clear. The fog colour is a neutral dark, so fogged
ground reads as dim daylight rather than night. Water and six-way smoke drain and darken the same way through
`SceneFog.hlsl`; other transparents only darken.

While the Selection map's `SightShape` key (Alt) is held, `SightShapeView` is shown and `SightShape` paints the
ground the selected units see, from their `SightArea`es, into a mask the fog pass outlines in `sightShapeColor`. Water and effect
graphs apply it themselves through `SceneFog.hlsl` and `GraphFogOfWar.hlsl`. Map buildings stay visible, but their flags, dials and
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

A piece at rest moves to `LastingDebris` and stays for the match. `LastingDebris.CapAt` sets the cap: 20,000
pieces on High, 8,000 on Medium and none on Low. Past the cap the oldest piece buries into the ground over 5 s and its
slot is reused; up to 512 bury at once. `LastingDebrisDraws` draws the layer through a `BatchRendererGroup` from GPU
buffers written only when a piece settles, buries or goes. In each view `LastingDebrisCulling`, a Burst job, tests every
piece's bounding sphere against each split's planes (the camera, or every shadow cascade), so pieces off screen and
outside a cascade are not drawn; a run of one mesh's pieces that share the same splits is one draw. It reads the matrices through the material's DOTS instancing path, so debris materials use a
shader with DOTS instancing, such as `LitTeam`. Changing effect detail starts the layer over at the new cap. Fogged
pieces draw as flying ones do.

A building with `SectionsAuthoring` ([combat](combat.md#sections)) gives each section models (`SectionModel`): intact,
up to 3 holed variants, rigid pieces, a stub and a heap, or just a broken model. Everything but the intact model is
saved hidden at scale 0 so editor views show the standing building, and `SectionsBakingSystem` bakes them back at
full size, hidden by `HideReason.Section`. `SectionViewSystem` shows each section in one of four states, per client
and cosmetic, following the simulation's broken mask, which is the same on every client:

- **Standing:** the intact model, or the holed variant whose hole is nearest the latest impact on it. Only impact cues
  the local player sees count (`CueVisibility`), so fog hides holes as well.
- **Breaking:** a break this client saw hides the intact model and plays the pieces' clip once beside the stub: blown
  in when the last impact (within 2 s) came from outside the section's `Outward` side, blown out when from inside,
  and a collapse when no impact is known, as when it falls from lost supports. The building's effect, debris and, for
  a ground floor section, its dust wave play with it. When the clip ends the pieces hide and the heap shows.
- **Broken:** the stub and heap, with no clip, for a break nobody saw or a building first seen
  (`ShownSections.Seen` cleared by the scenery writer, which also resets the holes). A stub whose supports are all
  broken is hidden, since it would hang in the air, and shows again once one is repaired.
- **Repair:** clearing the bit shows the intact model again and hides the rest.

The clip rows come from the `clips` field of `SectionsAuthoring` (`SectionClips`, shared by every section), and per-section sight state
is the `SectionLook` buffer.

### Shaders

| Shader | Use |
| --- | --- |
| `HyperRTS/Water` | Refraction through a bed darkened to wet brightness (`_BedDarkening`) and per-channel absorption toward a scatter colour (`_Absorption` per metre, `_ScatterColor`), caustics on beds under 1.2 m of water, three scales of ripples from the wind-wave spectrum `WaterRipples.png` (an FFT in `WaterSpectrum`), flow-mapped downstream and faster mid-channel than at the banks, Schlick Fresnel with a smoothness that falls from `_Smoothness` to `_SmoothnessFar` over `_SmoothnessFarDistance`, so far water blurs to the sky instead of aliasing, screen-space scene reflection over the sky, a sun glint clamped at `_GlintPeak` (just past the bloom threshold), a soft depth fade at the shore (`_ShoreFadeDepth`), foam patches below crossings and swells at banks, unit wakes (`SampleWake`). Needs Depth and Opaque Textures |
| `HyperRTS/Foliage` | Alpha-cut plants and tree leaves with wind, trample, translucency and dry and green patches. Lit by the sun, Forward+ point and spot lights (per vertex on scattered grass) and Surface Cache GI. Sunlight also passes through to a leaf's side facing away from the sun whatever the view, tinted by its colour twice and partly through its own shadow, so the far side of a crown glows from above; a normalised Blinn-Phong sheen with Fresnel replaces a plain glint. `_ClusterVariation` lightens, darkens or dries each leaf card of a tree by its pivot, so a crown reads as clumps |
| `HyperRTS/Bark` | Opaque tree bark with wind. Mask map: R metallic, G AO, A smoothness |
| `HyperRTS/RowCrop` | GPU-placed field crops. See [row crops](#row-crops) |
| `HyperRTS/Bird` | Unlit alpha-cut bird quads, each picking its wing pose from a 2x2 flipbook |

Instances built on the CPU each frame (overlays, health bars, ground markers, birds) go through `InstanceBuffer<T>`: a
structured buffer the shader reads as `_Instances[SV_InstanceID]`, drawn in one indirect call and doubled when full.
Each shader declares the struct beside its C# twin (`OverlayInstance`, `HealthBarInstance`, `MarkerInstance`,
`BirdInstance`) and builds its own object-to-world from it. Flying debris keeps `RenderMeshInstanced`, since its
`HyperRTS/LitTeam` material reads the instanced transforms in every pass and needs last frame's for motion vectors.

Water vertex colours shape the flow: alpha thins fords, green streaks rapids foam, and red trails foam lines
downstream of fords and bridges. Open water carries no streaks; banks get their own foam band.

`WorldRect.hlsl` holds `RectUV` and `SampleHeightmap`, which the terrain height and the undented ground read.

Every hand-written shader and compute shader compiles with DXC, which reads HLSL 2021: `#pragma use_dxc` sits beside
its `#pragma target`, in `HLSLINCLUDE` when the passes share one. It is per shader because Unity's own shaders stay on
FXC: URP's `DynamicGISkyOcclusion` fails under project-wide DXC. `#pragma hlsl_version 2021` adds nothing, since DXC
already defaults to it and FXC ignores it. Shader Graphs and VFX Graphs take no pragmas, so the includes they pull
in (`Effects/Shaders/`, and the fog, lighting and depth files those include) keep to what FXC also compiles.

URP keyword lists shared across passes live in `Presentation/Rendering/Keywords/` and are pulled in with
`#include_with_pragmas`: `LightingKeywords` (main and additional lights, shadows, screen space effects, decals and
cookies, for Road), `ForwardLightingKeywords` (that plus probes, fog and rendering layers, for the full Lit model),
`ObjectLightingKeywords` and `LightmapKeywords` (LitTeam and Bark add falloff, box projection, exposure, debug and probe
volumes, and LitTeam its lightmaps), `MainLightKeywords` (Water), `SimpleLightingKeywords` (that plus screen space
occlusion and the Forward+ cluster loop, for plant and flag forward passes), `ShadowCasterKeywords`,
`DepthNormalsKeywords`, `InstancingKeywords` (GPU and Entities Graphics instancing, for every pass drawn both ways) and
`LitTeamVertexKeywords` (LitTeam's rigid VAT, bone and build keywords, in every pass that moves its vertices). An include holds
only lines identical in every pass that uses it, so a pass needing a different line keeps its own; other material
keywords (`shader_feature_local`) stay in each shader.

Shaders that move their vertices share their prepasses. `Rendering/DisplacedPasses.hlsl` gives Foliage, RowCrop,
Flag and Bark's motion vectors their shadow, depth, depth-normals and motion vector entries from one `Displace` hook
taking a `WindFrame` and an object matrix: motion vectors call it with last frame's wind and matrix, so sway and
toppling both show. Grass (`FoliageGrass`) and RowCrop already use it, so their blades reproject under TAA. Their `_MipBias` (0 to -1,
default 0) samples the cutout atlas sharper; set about -0.5 on the plant materials for thin stalks. `Rendering/DisplacedLitPasses.hlsl` wraps whichever URP pass file came before it in one
`DisplacedLitVertex`, for LitTeam (`LitTeamPasses.hlsl`) and Bark (`BarkPasses.hlsl`), and hides the field names that
change from pass to pass. `Rendering/Hash.hlsl` holds the one PCG hash, `Random` and value noise, used by wind,
plant tints, grime, terrain hex tiling and the scatter compute shaders.

### Atmosphere

An `Atmosphere` component sits on the root of a weather's atmosphere prefab, together with its sun, post-processing
volume and `Wind`. When enabled it sets the skybox, ambient light, reflection cubemap and wetness, and turns Unity
fog off. A missing skybox or reflection is an error. Skyboxes use `HyperRTS/Sky`, Unity's cubemap sky whose
photographed ground gives way below the horizon to the sky's own colour just above it, blurred: haze down to half its
`Horizon Band` (20 degrees), fading back to the photograph by its end. A long view that runs out of land meets haze
instead of a close-up field, and the ground straight below, which lights the scene from beneath, stays as
photographed.

`CloudShadows` on the sun draws the sun's cookie every frame into a 2048 by 2048 bilinear R8 render texture that wraps around
the camera over 640 m of ground: the tileable cloud picture, drifting with its wind, times the shadow of each smoke
column `SmokeShadows` hands it (its foot-to-top segment seen from the sun, widening and thinning as it rises). The
texture repeats, so each texel takes the copy of the ground nearest the camera and only the far seam, 320 m away,
moves with it, and its 0.3 m texels match the old picture's detail. Its `smoke` toggle leaves the columns out. The
shader (`Hidden/HyperRTS/Cloud Cookie`, a Resources shader) draws the clouds as one full-screen triangle, then
multiplies in each column as a quad over its footprint, four copies so it wraps across the edges. URP's volumetric fog samples the same cookie where
the profile's `Fog` turns on light cookies, so cloud and smoke shadows cut shafts through the haze.

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
the bottom it refracts, so it fogs once.

### Horizon

`Horizon` draws farmland round the map out to its `width` (800 to 1200 m), for a camera tilted toward the horizon. A
map build adds it when its spec has a `horizonWidth`. It builds everything when enabled, in edit mode too, keeps it in
memory only and submits it to each game and Scene view camera before it renders (`SceneCameras.Draws`). Nothing in it
collides, navigates or casts a shadow. It builds the ring (`HorizonRing`), the field layout and its texture
(`HorizonFields`), the belt mesh (`HorizonBelts`) and the sky fade (`HorizonSky`).

- **Ground.** `HorizonRing` builds loops of vertices round the terrain's rectangle, rounded at the corners. The first
  loop lies 4 m in under the terrain's edge, and the loops spread further apart with distance, so a 2500 m ring takes
  about 20 thousand vertices. Heights follow the terrain's edge, then blend over `relief.blend` into open ground swelling
  with `HorizonRelief`'s noise. `HyperRTS/HorizonGround` paints it with the map terrain material's layer arrays.
  `HorizonFields` lays the fields on a grid of cells the farmland kit's field size, turned to the map's farmland. Each
  row splits in two across and each cell splits in two along or merges with its pair, all by hash, so fields vary in
  length and width; each field grows one crop picked by weight, with a margin along its edges. Dirt tracks in the
  margin layer, as wide as the kit's track road class, run along a share of field ends and row edges, and blocks of
  three by three cells share a broad shade. It alone lays them out and hands the shader a small texture of them
  (`HorizonFields.hlsl` reads it). Where a pixel spans much of a field, fields fade to the crops' and margins'
  weighted mean, so the far ground doesn't sparkle.
- **Sky fade.** Over the ring's outer 40 % (`HorizonSky.fadeShare`) the ground and belts take the sky's horizon
  colour at each bearing, so the ring meets the sky's photographed tree line without a band. `Horizon` reads
  `RenderSettings.skybox` each render and reports a skybox that isn't `HyperRTS/Sky`; both shaders share the sky's
  horizon maths through `SkyHorizon.hlsl`.
- **Belts.** A belt kit's `horizonStrip` material (`HyperRTS/HorizonBelt`, a side render of the belt with `_StripSize`
  metres along and tall) stands as two staggered layers of upright quads along a share of row edges without tracks,
  from 150 m past the terrain out to the ring's end, with gaps where tracks cross. Each stretch picks its strip by
  hash. A climate whose kits have no strip draws no belts.

### Contact shadows

`ContactShadowsFeature` gives small things (fences, poles, wheels, grass clumps, infantry) short, sharp sun shadows
where they meet the ground, which a soft cascaded shadow map seen from the match camera loses. It needs URP's Screen
Space Shadows feature on the same Forward or Forward+ renderer, and its shader set to `Hidden/HyperRTS/ContactShadows`;
the validator checks all three. Right after URP resolves the sun's shadows into its screen-space texture, the pass
traces the depth buffer from each pixel toward the sun (`length` 1 m in up to `steps` 12, one every 3 pixels on screen,
jittered per pixel), counts a hit when the trace passes less than `thickness` behind a surface, and registers a darkened
copy under the same global. Every opaque shader that reads the sun's screen-space shadow then gets contact shadows
without changes; transparents keep the cascades. `strength` scales with the sun light's shadow strength, so diffuse
weathers show none. They fade out between `fadeStart` and `fadeEnd` of view depth (60 to 80 m), where a 1 m trace
is under a pixel and only streaks dense crop rows. The feature runs from `lowestQualityLevel` up (1, so the Low level skips it), and only in game and
scene view perspective cameras.


### Sharpening

`SharpenFeature`, with its shader set to `Hidden/HyperRTS/Sharpen`, runs AMD's contrast-adaptive sharpening on the
finished frame after post-processing and any upscaler: each pixel is pushed away from its four neighbours by up to a
fifth of each at `strength` 1, less where the neighbourhood is already contrasty, so edges firm up without halos. It
keeps the frame in a texture past post-processing to read it. It runs from `lowestQualityLevel` up, in game and scene
view cameras, and the validator offers to add it to a renderer that lacks it.

### Wind

`Wind` sits on the scene's directional `WindZone` and publishes its strength, gusts and direction to shaders. Its
`vegetation` setting picks how much plants move: `None`, `Sway`, `Branches` or `Flutter`. Flags fly regardless.
It also keeps a `WindState` singleton (downwind direction and strength) in the presented world for the machine, dust
devil, battle haze and field fire systems; without a `Wind` there is no singleton and the air is still.
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

### Trample maps

Vehicles leave ruts and flatten grass and crops, shells leave craters and scorch, and units moving through water
leave wakes, and the ground keeps rubble, ash, fuel and wet mud. `TrampleMap` on the rig keeps four GPU textures
over the map and stamps them each frame. It needs
the `Hidden/HyperRTS/Trample Brush` shader.

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
sources (collapsing buildings, field fires) call `TrampleStamps.AddRemains` with a centre, a radius and the four
amounts, after `TrampleStampSystem` has run. Units with a `NavAgent` riding water cells stamp foam
behind them. A replay seeking back clears the maps, as it clears the decals.

Shaders read the maps through `Trample.hlsl` (trample, tracks, craters and wake), which builds on
`TrampleGlobals.hlsl`: the map globals, `TrampleMapsOn`, `TrampleUV`, `OnTrampleMaps` and the crater depth
`HYPERRTS_MAX_CRATER_DEPTH`. The terrain darkens ruts and flattened grass, bends its normal down
the crater's slope so the sun lights the bowl, darkens and dampens the bowl's soil, and blackens scorched ground. `TerrainRemains.hlsl` then lays the terrain
material's tiled rubble, ash and wet mud textures (`_Remains*`) and an untextured dark, glossy fuel slick with a faint
thin-film sheen over what the remains map covers, with a noisy fringe; ground without remains skips it after one
sample. Ash from 0.6 up turns to untextured char over the ash texture: the terrain material's `_RemainsCharColor`,
near black and shaded in 11 m patches, with a few soft grey ash smudges a few metres long where two scales of the
noise map's smooth channel, blended, pass `1 - _RemainsCharStreaks`; the ground's bumps are mostly smoothed away.
A rut is stamped as a ramp that falls linearly to the tyre's edge, and `RutCoverage` cuts it at `RUT_EDGE` with a
`fwidth` anti-aliased edge, so two clean bands show between 0.2 m texels instead of a soft blob; as the ramp fades the
band narrows and goes. Foliage leans plants along the travel direction, and water reads `SampleWake`. The quality setting picks Full, Low
at half resolution, or Off, which frees the textures.

`TrenchStampSystem` stamps each built `Trench` building the player sees into the scar map once (a flat-bottomed ditch with steep sides
along its long side, `TrampleStamps.AddTrench`), so the same dent follows; the simulation's ground never changes.

Craters also dent the drawn ground. In presented worlds `CraterTerrainSystem` redraws the active Unity Terrain's
heightmap under each new bowl on the GPU, with the `Hidden/HyperRTS/Crater Height` shader from the package's
`Resources`: the heights copied at the start, less the tracks map's crater depth. It reads the heights back at most
every 0.5 s, on a frame with no new crater, so Unity refines the terrain mesh there. A replay seek, new or removed
maps, another terrain and the world's end put the copied heights back, so dented heights never reach the
TerrainData asset. `CraterSink.hlsl` samples the same depth: the `LitTeam`, `Foliage` (grass included), `Bark` and
`RowCrop` vertex stages, shadow and depth passes too, lower the whole object by the depth at its origin. The sink
fades with the origin's height above the undented ground (`_HyperRTSUndentedHeights`, published by `CraterTerrain`):
full within 0.5 m, none from 2 m up, so aircraft, bridge decks and rooftops stay put. A `LitTeam` material's
`_CraterSink` scales it: 0 for a model that brings its own dent, such as a trench, and 1 for everything else. Squad
figures sink with it, so soldiers stand lower in a trench or crater with no figure code. The grass and row crop kernels,
which get the tracks map bound by `CraterTerrain.BindCraters` for each camera, place plants on the undented ground
first. The simulation's ground never changes; a crater's cover comes from the spots of its `Crater` entity.

### Field fires

Shells that land on dry fields set them alight. A `FieldFireSettings` component in the scene, one per climate (the
game puts it on the atmosphere prefab), names the rig, the dry terrain layers, the wetness limit, the smallest blast,
the ignite chance, how long a cell burns, each fire's reach, the clearance round buildings and how many fires play. Without one, or without an active terrain,
`FieldFireSystem` idles. The component publishes itself as a `FieldFireSource` singleton, and the system keeps a
`FieldFireState` singleton counting burnt cells. The fires are client visuals only and harm nothing.

- `FieldFires` is a 1 m grid over the map. A cell is dry where the listed terrain layers hold over half the
  alphamap's weight and the `GroundMaterials` cell isn't water; roads, water and bare soil stay bare. Ground within
  the clearance (20 m) of any building's footprint, fogged or not, turns bare too, so bases and pads never burn. It is
redone whenever a building appears, goes or moves, before that frame's ignitions.
- An impact the local player sees lights the dry cell it hit when its splash is at least the smallest blast, it hit
  open ground, `Wetness.Level` is under the limit (0.3) and the impact's seed passes the ignite chance.
- Every 0.25 s each burning cell may light its eight neighbours, faster downwind (`Wind`) and slowly against it. Each
  ignition gets a reach from its seed (5 to 15 m); the odds fall off as the cube of the share of it left, so a front
  dies out by itself and a shell leaves a blotch of tens to a few hundred square metres. A cell burns 20 to 60 s,
  then stays burnt. Fronts stop at bare and burnt cells, and at most 8,192 cells burn at once.
- Every random choice hashes the impact's landing spot and source, the cell and its age, so clients that see the same
  impacts burn closely the same fields. A replay seeking back puts the fires out.
- Fires burn and spread in fog too, so clients agree, but only ground the local player sees shows them
  (`CueVisibility.IsSeen(position)`), so they never reveal where enemy shells land.
- Burning cells gather in 8 m squares. Each seen square feeds `BattleHaze.Add` a light smudge (a full square about a
  fiftieth of a shell's puff each second), and the nearest seen squares stream
  the rig, up to the cap; a square that falls back into fog drops its stream.
- A seen cell that burns out stamps a soft 2.6 m disc of full ash into the remains map and of scorch into the tracks
  map (`TrampleStamps.AddScorch`), nudged up to 0.35 m off the grid, so neighbours overlap into one char. The grass and row crop kernels get the remains map bound by
  `TrampleSurfaces.RemainsGlobal` and drop plants where ash lies (`BurntPlants.hlsl`), so burnt fields stay bare;
  a crater's lighter ash only thins them. A still camera's grass and crops regrow when `FieldFireState.BurntCells` changes (`ScatterView` tracks it).

### Building footprints

`BuildingFootprintSystem` draws every building's turned obstacle box into an R8 mask over the map at nav-cell
resolution and publishes it as `_HyperRTSFootprints`, with `_HyperRTSFootprintRect` (zero without a map). Like the
ground picture, the trample maps, the undented heights and the fog grid, it goes through a `MapTextureGlobal`: a
texture global and its rect, cleared to black and a zero rect, which shaders read as nothing published. Cells
within half a cell of a wall count too. Grass and row crops skip plants where it is set, whatever the
trample-map quality, through `BuildingFootprints.hlsl`. It redraws only when buildings appear or go and their
boxes differ, and each redraw bumps `BuildingFootprintMask.Version` so cameras rescatter their plants. With no map
loaded it holds no texture.

`LocalGustSystem` also sends up to 8 radial gusts to `Wind.hlsl`: downwash under a seen rotorcraft within its rotor
wash height of the ground (25 m without one), and the blast of each seen impact with a splash of 1 m or more, which
spreads out and relaxes over about a second. Plants lean away from them and tree trunks bend as in wind. Trample quality Off turns them off.

### Row crops

Fields of crops in rows, placed and drawn on the GPU, so no crop is an object. The map tools add a `RowCropRenderer`
to each map scene and bake its `RowCropFields`: which field covers each metre, and each field's crop, row
direction and tramlines. A field's `RowCropTramlines` are pairs of wheel lines along its rows where no plant grows;
the map tools paint the same lines into the terrain.

A `RowCrop` asset describes one crop: model variants with full and simple meshes, a material, row and plant spacing,
random scale, turn and lean, lodging and two LOD distances. Variant meshes face +Z with the origin at the root.
Each row slides along its line by its own share of a plant spacing, so the gaps between plants in neighbouring rows
don't line up into stripes across the field. `stand` leaves gaps where sown spots never came up, and `edgeGrowth`
shrinks plants at the field's edge, where they lean the most. `edgeMargin` keeps a field off ground that grows no
crop or another one; two fields of one crop, such as a field and the headland sown across its rows, meet without a
gap, and neither counts the other as its edge. Plants ripen in the same dry patches as the ground
and grass round them, and `_WindSheen` lightens their tops under a passing gust, so its band rolls across the field.
Its `height` also shades the field: light falls off below the heads to `_CanopyShade` at the roots, so rows show
dark gaps between lit tops. That canopy term is the crop's ambient occlusion, so `_ScreenOcclusion` (0.2 by
default) keeps screen-space AO from darkening it again: GTAO reads a dense field of blades as a deep crevice and
turns shaded wheat into a flat dark patch. Foliage has the same property at 1.
Foliage's `_Wrap` (default 1, also on `FoliageGrass`) blends leaf lighting to an opaque solid: at 1 plants keep wrapped
diffuse, translucency, transmission and sheen; at 0 they take plain Lambert light, for pebbles and mounds scattered
like grass.

A camera regrows the plants of its visible tiles when it moves, the quality changes or new ash falls, and every frame
while a trample map is published (`RowCropRenderer.RegrowsEveryFrame`; grass doesn't read that map). Trampled plants bend or disappear, and none grow under a building's footprint. Plants
shrink away over the last 15 % before `lod1Distance` or the draw distance, whichever is nearer. Each plant switches model at its own distance, up to 20 % either
side of the LOD distances, so the models mix over a stretch rather than meeting at a ring that sweeps across the field
as the camera zooms. It also runs in edit mode.

A crop thins out from `thinStart`: the share of plants kept falls from 1 to `farShare` at
`lod1Distance` and holds, picked by the same hash as the density so the kept plants are the same ones each frame, and the
kept ones grow to `farScale` to cover the gaps. Plants cost their triangles in every pass, so this is the lever on
far fields: a field costs about the square of its reach. Crops cast shadows only from the bands that end within
`PresentationQuality.CropShadowDistance` (`RowCropBands.CastsShadows`).

A crop's `lodging` share of 30 m lattice cells holds a patch `lodgedSize` across, ragged and laid one way, flattened
by wind and rain (`Lodging` in `RowCrops.compute`): its plants bend over toward it.

#### Scatter host

Row crops and grass (see [maps](../maps.md)) share one host, built to take more scattered things such as debris:

- `ScatterRenderer<TBatch>`, the base of `RowCropRenderer` and `GrassRenderer`, holds the `compute` and `terrain`
  fields, builds and releases the inputs, keeps one `ScatterCameraState` (the camera's batches and the view they
  were grown for) per camera in `CameraStates`, culls tiles through `ScatterTiles`, regrows a camera when its view
  changed and, in edit mode, rebuilds after `OnValidate` or a map rebuild. A subclass makes its inputs and batches,
  names its `ScatterField`, queues each visible tile's jobs and says when its batches went stale. Density and
  distance come from `PresentationQuality.Current`.
- `ScatterField`, the base of `RowCropFields` and `GrassField`, holds the baked `area` and the `tiles` (`ScatterTile`: column
  and row from the area's corner, and the `first` and `count` of the tile's run in the field's own per-tile list). It
  makes the `ScatterTiles` culling boxes and checks the area has a size.
- `ScatterBatch`, the base of `RowCropBatch` and `GrassBatch`, holds one camera's slots: a mesh and property
  block each, the `ScatterJob`s queued for the grow kernel (one visible tile of one plant or field) and the `ScatterDraws` indirect
  draws. `Generate` reserves room,
  binds the jobs, grows in one dispatch and counts; `Draw` draws each slot with room. A subclass sizes its slots and
  owns its material, culling box and shadows.
- `ScatterSlots` is the room: one 48-byte instance buffer per batch, cut end to end into a slot per draw, and a table
  of each slot's offset and capacity that the grow and count kernels and the draws (`_SlotOffset`) read. Grass slots
  are (plant, model, band); a crop's are variant times two LOD bands, and each lattice spot picks its variant in the
  kernel, so a crop grows in one dispatch. A crop sizes every slot to its share of the tiles' worst case.
- `ScatterComputeInputs`, the base of `GrassComputeInputs` and `RowCropComputeInputs`, finds the grow and `Count`
  kernels, uploads the baked items and binds what both kernels read: the tile size (`ScatterTiles.Size`), the
  heightmap, and each camera's position with the building mask, crater depth and ash. Its `Stale` catches a map
  rebuild that changed the items, the subclass's own inputs or the heightmap. `ScatterSettings` carries density, distance and shadow
  distance (crops pass 0), and a density of 0 draws nothing for both.
- `Scatter.hlsl` holds the job struct, hashes, noise, band edges and slot counting; `ScatterHash` is the C# twin of
  the hashes and `ScatterBands` of the band edges. `BandRange` moves the range a plant picks its band at (a crop's LOD,
  grass casting shadows or not) by up to 20 % from its hash, and `ScatterBands` widens each band's reach to match.
  The kernels share its slot table and `Claim` and `CloseSlots`.

### Ambient life

An `AmbientLife` component in the scene, one per climate, adds birds and dust devils. They live on the client only,
so clients may differ. A count of zero turns a feature off. Both are skipped at Low effect detail.

- Bird flocks circle over the map. A nearby impact scatters them, and they settle again at least 150 m away. Every
  bird draws in one call on `HyperRTS/Bird`, with no cap on their number.
- Dust devils form near the camera on the chosen ground and drift downwind.

### Effects

An `EffectRig` is a prefab whose root holds child `VisualEffect` graphs. Author it +Y up and +Z forward.
Impacts turn +Y to the ground normal. The rig sets a max distance, a scale range, scale per splash radius and ground
alignment. The validator reports a broken rig, naming the prefab.

No two plays need look the same:

- **Variants**: a rig may list alternative rigs. Each burst plays the rig or one variant at random. A loop or trail
  keeps one per entity. Each variant is its own pooled rig and has no variants of its own.
- **Random spin** turns each burst about the rig's +Y. It is off by default, so muzzles keep their aim. Impacts
  always get a random yaw.
- **Loops** play at a scale inside the rig's range, and with random spin at a yaw, both stable per entity.

`EffectGraph` sends each graph its `Burst` event with
the pose as event attributes: `position`, `direction` (the rig's +Y), `axisZ` (its +Z, random spin included) and a
uniform `scale` (sent per axis, `scaleX` to `scaleZ`), so bursts on one frame keep their own poses. A graph drops events until it has run once, so bursts in the frame the pool spawned it wait for the next. The graph's exposed float `Density` takes the effect
detail share. Include files serve graph authors as Custom HLSL blocks, among them `GraphBurstPose.hlsl` (Place At Burst,
the end of every Initialize, wired to those source attributes; it keeps the origin in `targetPosition` for ground
planes) and `GraphFogOfWar.hlsl` (fog of war per particle in an output). The rig check reports a graph without an
asset, one that plays on its own (an Initial Event Name) or one without `Density`.

Loops and trails play graphs too. A graph that exposes `Speed` streams: `GraphStreamPool` on the rig keeps copies of
its stream graphs, one `GraphStream` per playing stream, held by the `StreamState`. Each frame the stream sets
`From`, `To`, `Up`, `Forward`, `Scale`, `Speed` (metres a second) and `Density` (strength times effect detail); the
first frame after the copy has run it sends `Play`. When `EffectStreams` drops a stream, or its variant changes, the
copy gets `Stop` and returns to the pool once `HasAnySystemAwake` says its particles are gone, so stopping fades
rather than cuts.

Graphs are generated from a `<Graph>.effect.asset` recipe beside them by `EffectGraphBuilder` (Editor/Effects): a list of
layers, each one system of a kind (`glow`, `puff`, `sixway`, `ring`, `debris`, `spark`, `strip`, `distort`,
`source`) with plain fields for its bursts or rates, start ranges, shape, forces, wind, curves over life and its
textures and meshes. `EffectGraphInputs` holds the events and properties every system reads, and `EffectLayerEmission`
writes what spawns a layer and where it lands: bursts on `Burst` at Place At Burst, a Constant Spawn Rate between
`Play` and `Stop` at Place Along Path (`GraphStreamPose.hlsl`), or a GPU event from a parent layer's particles
(Trigger Event over time) at their position. Every layer gets Emit From Shape (`GraphEmitShape.hlsl`, which also
scales the shape per axis, turns its +Y onto the layer's `axis` and leaves the ground plane's height for Place At
Burst), `Density`, World-space slots and the fog of war; debris with `ground` adds Bounce On Ground
(`GraphGroundBounce.hlsl`), sparks Stretch Along Velocity (`GraphStretch.hlsl`), layers with drag Slow Down
(`GraphDrag.hlsl`, a constant deceleration, or one growing with the speed squared
for `dragBySpeed`), windy layers Drift With Wind (`GraphWind.hlsl`, reading `Wind`'s globals), `noise` Stir With
Noise (`GraphNoise.hlsl`, crossed sine waves scrolling over time), `orbit` Orbit Spawn Point (`GraphOrbit.hlsl`, about
the vertical through each particle's spawn point) and a `randomStart` flipbook Loop Frames (`GraphLoopFrames.hlsl`).
A glowing six-way layer with `minHeatOverLife` lerps the two heat curves by a constant random per particle. Lit
puffs receive no shadows, and debris may take a base colour `texture`. A
`distort` layer draws through `HyperRTS/EffectDistort`, a Shader Graph with VFX support whose Custom Function
(`EffectDistort.hlsl`) refracts the opaque texture along a normal map, weighted by the particle's stored alpha,
at queue offset -50 (before other transparents, since the opaque texture lacks them and would erase smoke and flames
behind it). The strength is in metres, so the screen offset shrinks with distance, and it fades where it meets the
ground and inside the fog of war. It needs the camera's opaque and depth textures. `sixway` and `ring` layers draw through `HyperRTS/EffectSixWay`, a premultiplied Shader Graph
whose Custom Function (`EffectSixWay.hlsl`) shades with `SixWayLighting.hlsl`: the sun
with its shadows and cookie, clustered lights, sky, ground and horizon ambient, URP's fog and the fog of war per pixel,
and the ramp glow, from the particle's stored colour, alpha and flipbook frame, blended along the motion map
(`FlipbookMotion.hlsl`: RG centred on 0.5 hold each texel's move to the next frame along U and V, scaled so 0 and 1
mean 1/64 of the sheet; the current frame is pushed forward and the next pulled back by the blend, so a slow flipbook
flows instead of stepping). The
Unlit target declares the lighting and fog keywords itself. All reflection
into VFX Graph's internal editor model sits in `GraphModel`, so an Editor upgrade breaks that file alone. The builder
stamps the graph importer's `userData` with a hash of the JSON, line endings ignored; `EffectGraphBuilder.Problem`
names a graph that is missing or stale, and `EffectGraphBuilder.ShaderErrors` compiles a built graph's compute
shaders and each output pass's default variant, so tests catch a shader error before the graph first draws. Recipes
are written in `JsonFormat`'s layout. See [editor](../editor.md#effect-graphs) for building a graph, the checks on
a recipe, the recipe inspector, the preview and the capture.

`EffectPool` keeps one instance per prefab and replays it for every event, never instantiating per shot. It reads
the camera position once a frame, in `EffectPoolSystem`, for every range check, and exposes it as `Eye`. Each streaming
system holds an `EffectStreams` built on the pool, whose `Step` finds the rig, checks its range and moves the stream. Effects play for cues the client
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
| Medium | 70 % | Smoke shadows |
| High | 100 % | Smoke shadows, light flashes from 8 shared lights, fire lights from 8 more |

Loops take part in the scene's light through two parts on a rig's root, both fed by `LoopEffectSystem` with every
loop it streams this frame:

- **`LoopLight`**: a flickering point light (colour, range, intensity, height above the socket, flicker depth and
  speed). On High, `LoopLights` gives its 8 shadowless lights to the nearest such loops each frame, scaled by the
  loop's strength, with a flicker phase per stream so neighbouring fires don't pulse together. Burning wrecks and
  buildings carry it on their fire rigs.
- **`SmokeShadow`**: a smoke column's radius, height, opacity and grow time. The column grows in over the grow time
  since the entity was first seen, as the billows do, and scales with the loop's strength times the effect density,
  so it fades with the loop and thins with the smoke. From Medium up, `SmokeShadows` writes the 32 nearest
  columns as shader globals (`_HyperRTSSmokeStarts`, `_HyperRTSSmokeEnds`, `_HyperRTSSmokeCount`), and
  `CloudShadows` draws them into the sun's cookie (see [Atmosphere](#atmosphere)).

### Audio

`SoundPlaybackSystem` plays cue sounds through 32 pooled sources. `SoundListener` follows the camera's ground focus.
`LoopSoundSystem` plays entity loops through 24 more, and `AnnouncerSystem` speaks alerts through one 2D source.

### Quality

The game fills one `PresentationQuality` and calls `PresentationQuality.Apply`. It covers effect and ground detail,
trample maps, grass and row crop density and distance, close detail, water caustics and render scale.
`CloseDetail` (0 to 1) scales a `GrassPlant` by its `PlantKind`: `Close` plants draw while it is above 0, `Clutter`
plants grow at that share of their density, `Ground` plants ignore it. `WaterCaustics` off sets the global keyword
`_WATER_NO_CAUSTICS`, which the water shader compiles the bed pattern out with. Apply sets them all, and again on each
scene load. Shadows and the other pipeline features come from the game's quality levels, each with its own URP
asset, so the game sets the quality level before Apply, which writes render scale onto that level's asset.
Apply throws naming the first knob out of range before it writes anything. `Current` holds the last settings and
resets to full detail on play. Systems read their knobs from `Current`, and nothing else mirrors them. The types live in
`Presentation/Quality/`, which depends on no other Presentation folder.
