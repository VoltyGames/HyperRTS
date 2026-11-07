# Rendering and shaders

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Rendering

Model shading, visibility, fog of war, destruction visuals and the shader conventions. For artists making materials and
developers writing shaders.

## Team colour

`HyperRTS/LitTeam` is URP Lit that repaints the albedo in the owner's colour where the `_TeamMask` red channel allows
(UV0), inside the `TeamStripAuthoring` height band if there is one. `OwnerPalette` holds this client's colours;
`TeamColorOverride` swaps them, for colour-blind players or custom own, ally and enemy colours. The validator flags units
and buildings without a team colour material.

LitTeam carries only opaque or alpha-tested metallic workflow with normal, metallic-gloss, occlusion and emission maps.
Its keywords are `_NORMALMAP`, `_METALLICSPECGLOSSMAP`, `_OCCLUSIONMAP`, `_EMISSION`, `_ALPHATEST_ON`, `_BUILD`,
`_BONES` and `_VAT_RIGID`. Its inspector, `LitTeamGUI`, keeps the keywords in step with the maps like URP's Lit
inspector, and model hooks write the same. `Rendering/SurfaceMaps.hlsl` holds the map accessors LitTeam and Bark share.

Ground vehicles with `GrimeAuthoring` collect grime. `GrimeSystem` advances each unit's `Grime` from distance driven,
the `GroundMaterials` cell, `Wetness.Level` and health (dust, mud, wet, soot) and writes `_Grime` and `_GrimeBand` to its
meshes. `LitTeamGrime.hlsl` lays dust and mud low on the hull in the `_HyperRTSDustColor` and `_HyperRTSMudColor` that
the weather `Atmosphere` publishes. Units only.

Units hidden behind cover still show: `UnitSilhouetteFeature` draws a team-coloured fill and rim where a unit is hidden.
Every rendered part of a unit is put on rendering layer 8 at bake time for this. Grass and small plants are not cover:
`FoliageGrass` marks stencil bit 64 and the silhouette skips it.

## Build reveal

Structure materials on LitTeam turn on `_BUILD`, and the model rises piece by piece with its `ConstructionProgress`.
`BuildRevealSystem` copies the progress to `_BuildProgress` on every mesh under the building and clears it when
construction completes. UV3 holds each piece's build order (x from 0 first to 1 last, y 1 where build data exists). A piece
appears at its turn and drops in by `_BuildDropHeight` over `_BuildDropSlice`; a one-piece model such as a wall panel
sets a long slice and a big height. Pieces below `_BuildDropFrom` are laid in place, as slabs are. Unreached pieces
collapse to the object origin in the vertex stage, so every pass skips them. Meshes without UV3 data, materials without
`_BUILD` and finished buildings draw as authored.

## Building lights

`_LightsOn` on LitTeam multiplies emission from 0 to 1 and defaults to 1. `BuildingLightsSystem` writes it to every mesh
under a building from its replicated `Offline` state: dark while offline or under construction, lit otherwise, with a
short flicker (`BuildingLights.FlickerSeconds`) on each change. Windows and floodlights glow from the emission map, so
they go dark on low power.

## Visibility

A mesh draws while its `HiddenBy` has no `HideReason`. Baking gives every mesh and gameplay root one. `RenderSwitch` sets
a reason on a root's group or one branch of it and flips `MaterialMeshInfo`'s enable bit, so hiding moves no chunks.
Each reason has its own bit, so revealing a unit never redraws a model another reason hides:

- `Fog` and `Inside`, from `FogVisibilitySystem`, on the whole group;
- `Stage`, on every damage or resource stage model but the one shown;
- `Part`, on the intact or broken piece of a breakable part;
- `Figure`, on a squad figure out of play;
- `Site`, on a finished building's construction site dressing;
- `Variant`, on every [model variant](../simulation/economy.md#line-placement) but the picked one (`ModelVariantSystem`);
- `Section`, on a section model that is not shown.

Effects, sounds and grass stamps that skip unseen units test `FogHidden` and `Inside` on the root.

## Fog of war

Passengers inside a container stop rendering at once. An enemy leaving sight fades out over `FogVisibilitySystem.FadeTime`
then stops rendering; one coming into sight fades in. The fade is `RevealFade`, `_Reveal` on LitTeam, a dither in the
forward and silhouette passes; shadows stay until the unit hides. An enemy first spotted in the fog is hidden at once.

`FogOfWarRenderer` publishes the local fog grid and its look as shader globals, easing the shown grid toward each
restamp over `fadeTime`. `FogOfWarFeature`, on the URP renderer with its shader set to `Hidden/HyperRTS/FogOfWar`, copies
the camera colour through the fog before the transparents, using the depth buffer: terrain and buildings fog where they
stand while health bars, markers and the placement ghost stay clear. Water and six-way smoke take the fog through
`SceneFog.hlsl`, effect graphs through `GraphFogOfWar.hlsl`; other transparents only darken.

While the Selection map's `SightShape` key (Alt) is held, `SightShape` paints the ground the selected units see into a
mask the fog pass outlines in `sightShapeColor`. Map buildings stay visible, but their flags, dials and pips hide under
the local fog. A client remembers enemy buildings where it last saw them: a team-coloured visual copy that nothing can
pick or count, drawn as a dimmed square on the minimap.

## Flags

Buildings fly their owner's flag. During a capture the owner's flag lowers over the first half and the capturer's rises
over the second; a neutral building's flag rises over the whole capture; a building under construction flies none.
`HyperRTS/Flag` samples the game's flag `Texture2DArray`, one layer per `PlayerSide`, and waves the cloth downwind.

## Destruction

An entity shows the damage stage model for its health and bursts that stage's effect. Felled remains, such as trees, tip
over as a rod pivoting at the stump (`FallClock` steps the fall at a fixed rate from the `Topple` height, a crusher
speeds it), bounce once and settle, or lie down at once if the fall was out of sight. The tipping turns child models
recorded at rest in a `FallPart` buffer; the root keeps the simulation's final pose.

**Debris.** `DebrisAuthoring` on a ruin throws a `DebrisSet`'s pieces once when the ruin appears; on a building with
breakable parts it throws them from each part that breaks. `DebrisSystem` flies them on the client in a pool, bounces
them off the terrain and lays them flat. Medium effects throw 70 % of the pieces and Low none; pieces from one place fly
the same on every client. Flying pieces draw instanced per material and mesh, so the set's material needs GPU
instancing (checked at bake).

A piece at rest moves to `LastingDebris` and stays for the match, up to `LastingDebris.CapAt` per quality level; past the
cap the oldest buries into the ground. `LastingDebrisDraws` draws the layer through a `BatchRendererGroup` from GPU
buffers written only on change, and `LastingDebrisCulling` (a Burst job) culls per view and shadow cascade. It reads
matrices through the DOTS instancing path, so debris materials use a DOTS-instancing shader such as LitTeam. Changing
effect detail restarts the layer at the new cap.

**Sections.** A building with `SectionsAuthoring` ([combat](../simulation/combat.md#sections)) gives each section models
(`SectionModel`): intact, up to 3 holed variants, rigid pieces, a stub and a heap, or just a broken model. All but the
intact model is saved hidden at scale 0 and baked back at full size, hidden by `HideReason.Section`. `SectionViewSystem`
shows each section in one of four cosmetic states following the simulation's broken mask:

- **Standing:** the intact model, or the holed variant nearest the latest impact. Only impact cues the local player sees
  count (`CueVisibility`).
- **Breaking:** a break this client saw plays the pieces' clip once beside the stub: blown in when the last impact (within
  2 s) came from outside the section's `Outward` side, blown out from inside, a collapse when none is known. The building's
  effect, debris and, for a ground floor section, its dust wave play with it; then the heap shows.
- **Broken:** stub and heap with no clip, for a break nobody saw or a building first seen. A stub whose supports are all
  broken is hidden.
- **Repair:** the intact model returns.

The clip rows come from the `clips` field of `SectionsAuthoring` (`SectionClips`); per-section sight state is the
`SectionLook` buffer.

## Shaders

| Shader | Use |
| --- | --- |
| `HyperRTS/Water` | Refraction, absorption, caustics, flow-mapped ripples, Fresnel, screen-space reflection, sun glint, shore fade, foam and unit wakes. Needs Depth and Opaque Textures. See [environment](environment.md#water) |
| `HyperRTS/Foliage` | Alpha-cut plants and tree leaves with wind, trample, translucency and dry and green patches; `_Wrap` blends lighting to an opaque solid (0 for pebbles); `_MipBias` sharpens thin stalks; `_ClusterVariation` varies leaf cards |
| `HyperRTS/Bark` | Opaque tree bark with wind. Mask map: R metallic, G AO, A smoothness |
| `HyperRTS/RowCrop` | GPU-placed field crops. See [row crops](vegetation.md#row-crops) |
| `HyperRTS/Bird` | Unlit alpha-cut bird quads with a 2x2 wing flipbook |

Instances built on the CPU each frame (overlays, health bars, ground markers, birds) go through `InstanceBuffer<T>`: a
structured buffer the shader reads as `_Instances[SV_InstanceID]`, one indirect draw, doubled when full. Each shader
declares the struct beside its C# twin. Flying debris keeps `RenderMeshInstanced`, since LitTeam needs the instanced
transforms and last frame's for motion vectors.

**Conventions.**

- Every hand-written shader and compute shader compiles with DXC: `#pragma use_dxc` beside `#pragma target`, per shader
  (URP's own shaders stay on FXC). Shader Graphs and VFX Graphs take no pragmas, so the includes they pull in
  (`Effects/Shaders/` and the fog, lighting and depth files those include) must also compile under FXC.
- URP keyword lists shared across passes live in `Presentation/Rendering/Keywords/` and are pulled in with
  `#include_with_pragmas` (lighting, forward lighting, object lighting, lightmaps, main light, simple lighting, shadow
  caster, depth normals, instancing and LitTeam's vertex keywords). An include holds only lines identical in every pass
  that uses it; a pass needing a different line keeps its own. Material `shader_feature_local` keywords stay in each shader.
- Shaders that move vertices share prepasses: `Rendering/DisplacedPasses.hlsl` gives Foliage, RowCrop, Flag and Bark their
  shadow, depth, depth-normals and motion vector entries from one `Displace` hook, motion vectors using last frame's
  wind and matrix so plants reproject under TAA. `DisplacedLitPasses.hlsl` wraps a URP pass file for LitTeam and Bark.
- `Rendering/Hash.hlsl` holds the one PCG hash and value noise. `WorldRect.hlsl` holds `RectUV` and `SampleHeightmap`.
- A transparent shader applies both fogs with `SceneFogAt` and `ApplySceneFog` from `SceneFog.hlsl` and needs
  `#pragma multi_compile_fragment _FOG_ANALYTIC _FOG_VOLUMETRIC`. One that samples the opaque texture first removes URP's
  fog with `AtmosphereFogAt` and `RemoveAtmosphereFog`, as water does, so it fogs once.

## Contact shadows

`ContactShadowsFeature` gives small things (fences, wheels, grass clumps, infantry) short sharp sun shadows where they meet
the ground. It needs URP's Screen Space Shadows feature on the same Forward or Forward+ renderer and its shader set to
`Hidden/HyperRTS/ContactShadows`; the validator checks all three. After URP resolves the sun shadows, the pass traces the
depth buffer toward the sun and registers a darkened copy under the same global, so every opaque shader that reads the
screen-space shadow gets them unchanged. Transparents keep the cascades. Strength follows the sun's shadow strength, so
diffuse weathers show none; they fade out with view depth. It runs from `lowestQualityLevel` up, in game and scene
view perspective cameras. Its trace length, steps, thickness and fade are the feature's inspector fields.

## Sharpening

`SharpenFeature`, with its shader set to `Hidden/HyperRTS/Sharpen`, runs contrast-adaptive sharpening on the finished
frame after post-processing and any upscaler. It runs from `lowestQualityLevel` up, in game and scene view cameras, and the
validator offers to add it to a renderer that lacks it.
