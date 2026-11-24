# Rendering and shaders

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Rendering

Model shading, visibility, fog of war, destruction visuals and the shader conventions. For artists making materials and
developers writing shaders.

## Team colour

`HyperRTS/LitTeam` is URP Lit that repaints the albedo in the owner's colour where the `_TeamMask` red channel allows
(UV0), inside the `TeamStripAuthoring` height band if there is one. `OwnerPalette` holds this client's colours;
`TeamColorOverride` swaps them. LitTeam takes
the metallic workflow, opaque, alpha-tested or transparent, and `LitTeamGUI` keeps its keywords, blend, queue and passes
in step with the maps and toggles.

**Team markers.** A team marker is a child mesh (a roof panel, pennant, flag or placard) on a LitTeam material with a
white team mask, which `TeamColorSystem` paints like every mesh under the root. `TeamMarkerAuthoring` bakes nothing; it
lets `TeamColorRule` count markers against `TeamMarkerSettings.minMarkers` on vehicles and buildings that can take an
owner (not neutral, or capturable, or a neutral garrison), have no team strip and are no base prefab that variants
derive from; a variant's inherited markers count. A marker hides with the damage stage it sits under and dies with its entity, since
remains are their own prefab. A building's marker turns `_BUILD` on with UV3 at 1, or it shows before the building
rises.

**Glass.** The `_Transparent` toggle blends premultiplied, as URP Lit's Preserve Specular does: base alpha fades the
diffuse while reflections stay whole. A transparent material writes no depth, casts no shadow and skips the
prepasses, so whatever the model shows behind it, such as a cab, must be modelled. Opaque glass (periscopes, building windows) stays opaque with a high smoothness.

**Interiors.** The `_INTERIOR` toggle shows a room behind a window. The emission map is an atlas of front-on room photos
in an `_InteriorAtlas` grid, and each window's UVs fill one cell, less the cell margin. The shader traces the view ray
into a box room one window deep and projects the hit back into the photo, so the room shifts with the camera instead of
reading as a poster. Each cell's alpha is how much of the frame its back wall fills; 1 keeps a photo flat on the glass,
for curtains and blinds. Fresnel takes the room's light at grazing angles, where the reflection wins.

**Detail maps.** The `_DETAIL` toggle adds a grey detail albedo, imported as sRGB colour (0.5 as authored leaves the colour alone), and a detail normal,
projected in object space from three sides at `_DetailTiling` repeats per metre, so grain has the same density on every
island of an atlased mesh. Both fade out between `_DetailFadeStart` and `_DetailFadeEnd` metres from the camera. They
cost six texture samples in the forward pass only, nothing with the toggle off. The detail normal needs the normal path,
so the toggle turns `_NORMALMAP` on; the depth-normals buffer keeps the base normal.

**Weathering.** The `_WEATHERING` toggle dirties building materials from noise: dirt up wall bases to
`_WeatheringBaseHeight` above object y 0, streaks growing with height, and mottled, pooled roofs. Object space keeps
the Meta pass's GI dirt matching. Team paint and glass stay clean. Base dirt needs mesh pivots on the ground.

Ground vehicles with `GrimeAuthoring` collect grime (`GrimeSystem`), mud up to the road wheels' top and dust up to
the hull's top, both measured by **Fill from model**. After rain (`_HyperRTSWetness`, from the
`Atmosphere`) every LitTeam surface is wet where the sky reaches it, as the ground is: darker and glossier, metal
not darker.

**Silhouettes.** A unit hidden behind cover shows as a team-colour silhouette. Units bake onto the `UnitRenderingLayer`
(`RenderingLayerBakingSystem`). Two URP Render Objects features on the units layer draw LitTeam's `HyperRTSUnitMask`
pass, which marks the unit's visible pixels in stencil bit 128, then its `HyperRTSSilhouette` pass where the unit is
hidden. The validator adds both features in that order; the staging clean step turns them off with
`UnitSilhouettes.Hide`. Scattered grass and row crops write stencil bit
64 (ground cover), which the silhouette pass and the ground markers skip, so plants cover a marker line and hide no unit
behind them.

## Build reveal

Structure materials on LitTeam turn on `_BUILD`, and the model rises piece by piece with its `ConstructionProgress`
(`BuildRevealSystem`). UV3 holds each piece's build order. A one-piece model such as a wall panel sets a long
`_BuildDropSlice` and a big `_BuildDropHeight`; pieces below `_BuildDropFrom`, such as slabs, are laid in place. Meshes
without UV3 data, materials without `_BUILD` (glass too) and finished buildings draw whole. Unbuilt pieces collapse to
the origin, so `BuildPieceLods.KeepPiecesWhole`, called on import, splits triangles that import or Mesh LOD welded
across pieces.

## Building lights

`_LightLevel` on LitTeam multiplies emission from 0 to 1. `BuildingLightsSystem` writes it from the replicated `Offline`
state: dark while offline or under construction, with a short flicker on each change.

## Visibility

A mesh draws while its `HiddenBy` has no `HideReason`. Baking gives every mesh and gameplay root one. `RenderSwitch` sets a
reason on a root's group or one branch of it and flips `MaterialMeshInfo`'s enable bit, so hiding moves no chunks. Each
reason has its own bit. The reasons are `Fog`, `Inside`,
`Stage`, `Part`, `Figure`, `Site`, `Variant` (see [model variants](../simulation/economy.md#line-placement)) and
`Section`. Effects, sounds and grass stamps that skip unseen units test `FogHidden` and `Inside` on the root.

## Level of detail

A model draws its levels of detail through Unity's Mesh LOD: every level is an index range over one vertex buffer, so
one renderer draws them all and the GPU picks the level. Every level switches by one size rule, `MeshLods.UseSizeRule`:
level 1 once the mesh's bounds span `MeshLods.FirstLevelPixels` (150) across on screen, level 2 at half that, and so
on. Unity's own curve keeps a level until its error is under a pixel, which in an RTS view means never. Each quality
level's `meshLodThreshold` scales the sizes. The Terrain ignores Mesh LOD and always draws level 0. A
`BoneAnimationSet`'s `meshLods` generates levels for its baked parts. `MeshLods.Pack` packs hand-made levels, such as
trees with leaf cards that generated levels would strip, into one mesh.

## Fog of war

An enemy leaving sight fades out over `FogVisibilitySystem.FadeTime` then stops rendering; one coming into sight fades in
(`RevealFade`, `_Reveal` on LitTeam). Passengers inside a container stop rendering at once.

`FogOfWarFeature` on the URP renderer fogs the camera colour before the transparents, so terrain and buildings fog where
they stand while health bars and markers stay clear. A transparent shader fogs itself: water and smoke through `SceneFog.hlsl`,
effect graphs through `ShadeInFogOfWar.hlsl`.

A client remembers enemy buildings where it last saw them as a visual copy that nothing can pick or count.

## Flags

Buildings fly their owner's flag. During a capture the owner's flag lowers and the capturer's rises; a building under
construction flies none. `HyperRTS/Flag` samples the game's flag `Texture2DArray`, one layer per `PlayerSide`.

## Destruction

An entity shows the damage stage model for its health and bursts that stage's effect. Felled remains, such as trees, tip
over as a rod pivoting at the stump, bounce once and settle, or lie down at once if the fall was out of sight. The
tipping turns child models recorded in a `FallPart` buffer; the root keeps the simulation's final pose.

**Model copies.** Models a root shows only now and then (every damage stage but the first, every section model but the
intact one) bake as entity prefabs (`ModelCopyBakingSystem`), so a whole tree or building costs only the models on
screen. `ModelCopies` copies one in under the part it was authored under while it shows and destroys it when it hides.
A copy takes the root's hide reasons and the values painters put on its meshes (team colour, fog fade, lights, build
progress, grime), and the root's `LinkedEntityGroup` lists it, so the root is destroyed whole.

**Debris.** `DebrisAuthoring` on a ruin throws a `DebrisSet`'s pieces once when the ruin appears; on a building with
breakable parts it throws them from each part that breaks, and an `ImpactMark` throws its `debris` sets out of its
crater. `DebrisSystem` flies them on the client and lays them flat.
The set's material needs GPU instancing (checked at bake).

A piece at rest moves to `LastingDebris` and stays for the match, up to `LastingDebris.CapAt` per quality level; past the
cap the oldest buries into the ground. `LastingDebrisDraws` draws it through a `BatchRendererGroup`, which reads
matrices through the DOTS instancing path, so debris materials use a DOTS-instancing shader such as LitTeam. One
entity per piece would be simpler, but at 20,000 pieces it cost 20% more main-thread time and about 8 ms of render
thread.

**Sections.** A building with `SectionsAuthoring` ([combat](../simulation/combat.md#sections)) gives each section models
(`SectionModel`): intact, holed variants, rigid pieces, a stub and a heap, or just a broken model. Models other than the
intact one are saved at scale 0, so editor views show the standing building, and bake as prefabs copied in while they
show (see model copies above). `SectionViewSystem` shows each section by the simulation's broken mask:

- **Standing:** the intact model, or the holed variant nearest the latest impact the local player saw.
- **Breaking:** a break this client saw plays the pieces' clip once (`SectionClips`), blown in or out by the side the
  last impact came from, and the pieces stay on its last frame (`ShownSections.Settled`), so nothing jumps.
- **Broken:** stub and heap with no clip, for a break nobody saw or a building first seen broken.
- **Resting debris:** pieces and heap hide while a section in `Section.Rests` is broken, and return on repair.
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

**Terms.** The words the shader files use, in plain terms:

| Term | Meaning |
| --- | --- |
| Pass | One way a shader draws a mesh, run at its own point of the frame: shadow, depth, colour (`UniversalForward`), motion |
| Keyword | A switch compiled into the shader, such as `_BONES`. Each combination is a separate variant, so a new keyword doubles them |
| `UnityPerMaterial` | The block of material values (colours, tiling, strengths). Every pass of a shader must declare the same block, or URP's SRP Batcher stops batching its draws |
| DOTS instancing | Values Entities Graphics writes per entity (team colour, build progress) instead of per material, through a `[MaterialProperty]` component |
| Depth and depth-normals | Prepasses that write each pixel's distance and facing. SSAO, contact shadows, screen-space reflections and soft particles read them |
| Motion vectors | How far each pixel moved since the last frame. DLSS, FSR and TAA rebuild the image from them, so anything that moves its vertices must report it |
| VAT | Vertex animation textures: an animation baked into a texture, a row per frame, that the shader plays. Bones skin infantry; rigid VAT moves building pieces |
| Six-way lighting | Smoke flipbooks baked lit from six sides; the shader blends them by where the light comes from, so smoke looks lit with no normals |
| Triplanar | A texture projected from three sides and blended by the surface's facing, so steep slopes don't stretch |
| Hex tiling | A tiled texture sampled in hexagonal cells, each turned and shifted, so the repeat never shows |
| Stencil | A per-pixel mark a pass can write and a later pass test, such as "a unit's visible pixel" for silhouettes |

**Conventions.**

- Every hand-written shader and compute shader compiles with DXC: `#pragma use_dxc` beside `#pragma target`. Shader Graphs
  and VFX Graphs take no pragmas, so the includes they pull in must also compile under FXC.
- Shaders that code draws with (full-screen passes, map painting, the cloud cookie) are found by name through
  `EngineShaders`, an `IRenderPipelineResources` that URP's global settings fill in and keep in player builds: a new
  one is a field there with its `ResourcePath`, and `EngineShadersTests` fails if a name finds nothing.
- Shader code that several shaders share lives in `Presentation/Rendering/ShaderLibrary/`. URP keyword lists shared
  across passes are in its `Keywords/` and pulled in with `#include_with_pragmas`. An include holds only lines
  identical in every pass that uses it.
- Custom lit shaders take the sun from `SunLight` and the ground's PBR from `GroundFragmentPBR` in `SceneLighting.hlsl`.
  Both pass the surface normal that URP's shadow receiver normal bias and soft (PCSS) shadows read, so those URP asset
  settings work on our shaders too. Flags passed to a URP function are named locals.
- A Meta pass feeds Surface Cache GI through `MetaPass.hlsl`. A cutout shader defines `META_ALPHA_CLIP` and
  `MetaAlpha` so its holes stay out of the GI.
- Shaders that move vertices share prepasses through `DisplacedPasses.hlsl`: write one `Displace` hook and get the
  shadow, depth, depth-normals and motion vector entries, so plants reproject under TAA.
- A transparent shader applies both fogs with `ApplySceneFog` from `SceneFog.hlsl` and needs
  `#pragma multi_compile_fragment _FOG_ANALYTIC _FOG_VOLUMETRIC`. One that samples the opaque texture first removes URP's
  fog with `RemoveAtmosphereFog`, as water does, so it fogs once.
- A struct a shader reads from a GPU buffer is a C# struct marked `[GenerateHLSL(needAccessors: false)]`. **Edit ▸
  Rendering ▸ Generate Shader Includes** writes `<Type>.cs.hlsl` beside it, which the shader includes (fields keep their C#
  names). Never edit that file; buffer strides are `UnsafeUtility.SizeOf<T>()`. Fields must be floats, ints, uints,
  `Vector2` to `Vector4` or `Matrix4x4`, so a struct of halves or packed bytes stays hand-written.

## Contact shadows

`ContactShadowsFeature` gives small things (fences, wheels, grass clumps, infantry) short sharp sun shadows where they meet
the ground. It needs URP's Screen Space Shadows feature on the same renderer and a forward rendering mode, which the
validator checks. Opaque shaders that read the screen-space shadow get
the result unchanged; transparents keep the cascades.

## Sharpening

`SharpenFeature` sharpens the finished frame after post-processing and
any upscaler. The validator offers to add it to a renderer that lacks it.

## Screen overlay

`ScreenOverlayFeature` draws every visible renderer's `HyperRTSScreenOverlay` pass into the camera's final target,
after URP's last post-processing pass and blit and before it draws the UI. That point is at the screen's resolution
whatever the upscaler: a temporal one (DLSS, FSR 3 and 4, STP) works before post-processing and a spatial one after
it. The pass draws with the camera's unjittered matrices, so a shape sized in screen pixels stays crisp and still at
any resolution scale, with its colours untouched by tonemapping. Health bars and small-unit pips draw there. The validator offers to
add the feature to a renderer that lacks it.
