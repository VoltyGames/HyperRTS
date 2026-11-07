# Rendering and shaders

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Rendering

Model shading, visibility, fog of war, destruction visuals and the shader conventions. For artists making materials and
developers writing shaders.

## Team colour

`HyperRTS/LitTeam` is URP Lit that repaints the albedo in the owner's colour where the `_TeamMask` red channel allows
(UV0), inside the `TeamStripAuthoring` height band if there is one. `OwnerPalette` holds this client's colours;
`TeamColorOverride` swaps them. The validator flags units and buildings without a team colour material. LitTeam takes
opaque or alpha-tested metallic workflow only, and `LitTeamGUI` keeps its keywords in step with the maps.

Ground vehicles with `GrimeAuthoring` collect grime (`GrimeSystem`). `UnitSilhouetteFeature` outlines units hidden
behind cover.

## Build reveal

Structure materials on LitTeam turn on `_BUILD`, and the model rises piece by piece with its `ConstructionProgress`
(`BuildRevealSystem`). UV3 holds each piece's build order. A one-piece model such as a wall panel sets a long
`_BuildDropSlice` and a big `_BuildDropHeight`; pieces below `_BuildDropFrom`, such as slabs, are laid in place. Meshes
without UV3 data, materials without `_BUILD` and finished buildings draw as authored.

## Building lights

`_LightsOn` on LitTeam multiplies emission from 0 to 1. `BuildingLightsSystem` writes it from the replicated `Offline`
state: dark while offline or under construction, with a short flicker on each change.

## Visibility

A mesh draws while its `HiddenBy` has no `HideReason`. Baking gives every mesh and gameplay root one. `RenderSwitch` sets a
reason on a root's group or one branch of it and flips `MaterialMeshInfo`'s enable bit, so hiding moves no chunks. Each
reason has its own bit. The reasons are `Fog`, `Inside`,
`Stage`, `Part`, `Figure`, `Site`, `Variant` (see [model variants](../simulation/economy.md#line-placement)) and
`Section`. Effects, sounds and grass stamps that skip unseen units test `FogHidden` and `Inside` on the root.

## Fog of war

An enemy leaving sight fades out over `FogVisibilitySystem.FadeTime` then stops rendering; one coming into sight fades in
(`RevealFade`, `_Reveal` on LitTeam). Passengers inside a container stop rendering at once.

`FogOfWarFeature`, on the URP renderer with its shader
set to `Hidden/HyperRTS/FogOfWar`, fogs the camera colour before the transparents, so terrain and buildings fog where they
stand while health bars and markers stay clear. A transparent shader fogs itself: water and smoke through `SceneFog.hlsl`,
effect graphs through `GraphFogOfWar.hlsl`.

A client remembers enemy buildings where it last saw them as a visual copy that nothing can pick or count.

## Flags

Buildings fly their owner's flag. During a capture the owner's flag lowers and the capturer's rises; a building under
construction flies none. `HyperRTS/Flag` samples the game's flag `Texture2DArray`, one layer per `PlayerSide`.

## Destruction

An entity shows the damage stage model for its health and bursts that stage's effect. Felled remains, such as trees, tip
over as a rod pivoting at the stump, bounce once and settle, or lie down at once if the fall was out of sight. The
tipping turns child models recorded in a `FallPart` buffer; the root keeps the simulation's final pose.

**Debris.** `DebrisAuthoring` on a ruin throws a `DebrisSet`'s pieces once when the ruin appears; on a building with
breakable parts it throws them from each part that breaks. `DebrisSystem` flies them on the client and lays them flat.
The set's material needs GPU instancing (checked at bake).

A piece at rest moves to `LastingDebris` and stays for the match, up to `LastingDebris.CapAt` per quality level; past the
cap the oldest buries into the ground. `LastingDebrisDraws` draws it through a `BatchRendererGroup`, which reads
matrices through the DOTS instancing path, so debris materials use a DOTS-instancing shader such as LitTeam.

**Sections.** A building with `SectionsAuthoring` ([combat](../simulation/combat.md#sections)) gives each section models
(`SectionModel`): intact, holed variants, rigid pieces, a stub and a heap, or just a broken model. Models other than the
intact one are saved hidden and baked back at full size, hidden by `HideReason.Section`. `SectionViewSystem` shows each
section in one of four cosmetic states following the simulation's broken mask:

- **Standing:** the intact model, or the holed variant nearest the latest impact the local player saw.
- **Breaking:** a break this client saw plays the pieces' clip once (`SectionClips`), blown in or out by the side the
  last impact came from, then the heap shows.
- **Broken:** stub and heap with no clip, for a break nobody saw.
- **Repair:** the intact model returns.

## Shaders

| Shader | Use |
| --- | --- |
| `HyperRTS/Water` | See [environment](environment.md#water) |
| `HyperRTS/Foliage` | Alpha-cut plants and tree leaves with wind, trample and translucency |
| `HyperRTS/Bark` | Opaque tree bark with wind. Mask map: R metallic, G AO, A smoothness |
| `HyperRTS/RowCrop` | GPU-placed field crops. See [row crops](vegetation.md#row-crops) |
| `HyperRTS/Bird` | Unlit alpha-cut bird quads with a wing flipbook |

Instances built on the CPU each frame (overlays, health bars, ground markers, birds) go through `InstanceBuffer<T>`, a
structured buffer the shader reads as `_Instances[SV_InstanceID]`.

**Conventions.**

- Every hand-written shader and compute shader compiles with DXC: `#pragma use_dxc` beside `#pragma target`. Shader Graphs
  and VFX Graphs take no pragmas, so the includes they pull in must also compile under FXC.
- URP keyword lists shared across passes live in `Presentation/Rendering/Keywords/` and are pulled in with
  `#include_with_pragmas`. An include holds only lines identical in every pass that uses it.
- Shaders that move vertices share prepasses through `Rendering/DisplacedPasses.hlsl`: write one `Displace` hook and get
  the shadow, depth, depth-normals and motion vector entries, so plants reproject under TAA.
- A transparent shader applies both fogs with `ApplySceneFog` from `SceneFog.hlsl` and needs
  `#pragma multi_compile_fragment _FOG_ANALYTIC _FOG_VOLUMETRIC`. One that samples the opaque texture first removes URP's
  fog with `RemoveAtmosphereFog`, as water does, so it fogs once.

## Contact shadows

`ContactShadowsFeature` gives small things (fences, wheels, grass clumps, infantry) short sharp sun shadows where they meet
the ground. It needs URP's Screen Space Shadows feature on the same renderer and its shader set to
`Hidden/HyperRTS/ContactShadows`; the validator checks all three. Opaque shaders that read the screen-space shadow get
the result unchanged; transparents keep the cascades.

## Sharpening

`SharpenFeature`, with its shader set to `Hidden/HyperRTS/Sharpen`, sharpens the finished frame after post-processing and
any upscaler. The validator offers to add it to a renderer that lacks it.
