# Environment

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Environment

Sky, fog, weather, wind, water, and the ground that remembers what happened on it. Plants have their own page,
[vegetation](vegetation.md). For game developers building a climate and artists authoring tree and water meshes.

## Atmosphere

An `Atmosphere` component sits on the root of a weather's atmosphere prefab, with its sun, post-processing volume and
`Wind`. When enabled it sets the skybox, ambient light, reflection cubemap, and turns Unity fog off. It writes the ground wetness each frame as the `GroundWetness` singleton and `_HyperRTSWetness`; `WetnessGroup` shows its children from a threshold of `_HyperRTSWetness`, so puddles also show in edit mode. A
missing skybox or reflection is an error. Skyboxes use `HyperRTS/Sky`, which blends its photographed ground into haze
below the horizon.

`CloudShadows` on the sun draws a drifting cloud cookie that includes smoke column shadows (`SmokeShadows`).

Scene fog is URP's `Volumetric Fog` renderer feature. URP compiles it only under the `VOLUMETRIC_FOG` scripting define, so
a project using it sets that define. `FogOfWarFeature` keeps orthographic captures such as the minimap clear.

**Battle haze.** While `PresentationQuality.BattleHaze` is on, `BattleHazeSystem` thickens the fog over a fight. Seen
impacts, burning wrecks and damaged buildings add haze to a coarse grid; it drifts downwind and thins. One blast's
haze is capped in reach and amount, so a very large one smokes its own ground and no more. Other sources call
`BattleHaze.Add(position, radius, amount)`.

## Wind

`Wind` sits on the scene's directional `WindZone` and publishes strength, gusts and direction to shaders. Its
`vegetation` setting picks how much plants move: `None`, `Sway`, `Branches` or `Flutter`. Flags fly regardless. It also
keeps a `WindState` singleton for systems that need the downwind direction.

Bark and leaves of one tree need the same `_WindStrength`, `_WindSpeed`, `_BranchSway`, `_TreeHeight` and `_WindScale`;
set `_WindScale` to 0 for fallen trees. Tree meshes for Foliage with **Tree Wind** on, and for Bark, carry wind data in
object space, in metres:

| Channel | Data |
| --- | --- |
| TEXCOORD2.xy, TEXCOORD3.x | Branch pivot |
| TEXCOORD3.y, TEXCOORD4.xy | Leaf card pivot |
| COLOR r | Canopy ambient occlusion |
| COLOR g | Branch flex: 0 at the pivot and on the trunk, 1 at the tip |
| COLOR b | Stiffness: 0 limb, 1 twig |
| COLOR a | Flutter weight, 1 on leaf cards |

## Water

`HyperRTS/Water` is a refracting water shader with absorption, caustics, wind-wave ripples (`WaterRipples.png`, made
by `WaterSpectrum`), reflection, foam and unit wakes (`SampleWake`). It needs Depth and Opaque Textures. Its `Scatter
Color` is the silt's albedo, lit by the sun and sky like the ground, so deep water follows each weather's light.
Vertex colours shape the flow: alpha thins fords, green streaks rapids foam, red trails foam lines downstream of
fords. Bridge piers come from `WaterPiers` on the water object, up to 16 per map: the shader draws a cushion of foam
round each and a wake that widens and fades over `Pier Wake Length`.
`WaterCaustics` off in [quality](README.md#quality) compiles the caustics out.

## Trample maps

Vehicles leave ruts, shells leave craters and scorch, units in water leave wakes, and the ground keeps rubble and ash.
`TrampleMap` on the rig keeps four GPU textures over the map, drawn with the `Hidden/HyperRTS/TrampleBrush` shader.

| Texture | Channels |
| --- | --- |
| `_HyperRTSTrample` | RG travel direction, B flattened grass that recovers, A crop damage that never recovers |
| `_HyperRTSTracks` | R rut depth, G churned soil (both fade over minutes); B scorch, A ground level (lasts the match) |
| `_HyperRTSWake` | R foam, which fades in seconds |
| `_HyperRTSStains` | R rubble, G ash, B fuel, A wet mud; last the match |

Alpha holds a level round a bias: `TrampleStamps.GroundLevelBias` (alpha 40 of 255) is untouched ground, above it is
depth and below it lift, over `GroundRange` (10 m) across 0 to 1, so up to 8 m deep and 1.5 m raised. Every shader decodes it
with `GroundSunk` in `TrampleGlobals.hlsl`: metres the ground sank, negative on a rim. Clears and resets set alpha to the
bias, and the fade passes leave it alone.

An `ImpactMark`'s decal projector leans with the ground's slope measured across its whole width (`decalReach`
included); a slope sampled nearer the middle tips a wide projector out of the ground on one side.

`TrackMarks` on a vehicle, the `GroundScar` of an `ImpactMark` or of `DeathStainAuthoring` (crater depth, rim height,
burn reach, scorch, rubble, ash, fuel, mud) stamp the maps. Other sources, such as collapsing buildings, call `TrampleStamps.AddStain`.
Shaders read the maps through `Trample.hlsl`. Grass and row crops keep clear of buildings, and road ribbons hide under them, through
the `_HyperRTSFootprints` mask (`BuildingFootprints.hlsl`), so a road under a yard or a silo never shows through it. The terrain tiles the rubble, ash and mud textures over the stains with
the same hex tiling as its layers (`TerrainStains.hlsl`), so a burnt field never shows its tile. Ash lies in drifts
(`AshDrift`): noise thins its cover in patches, so the char and soil under a wide burn show through it. Ash and rubble
slide off slopes steeper than about 32 degrees and are gone past about 53, so a crater wall never shows them stretched.

**Buildings wear the ground round them.** A Building with a `wornGroundWidth` above 0 gets a `WornGround`. The first
time it is seen finished, `WornGroundStampSystem` lays wet mud of its `wornGroundStrength` into the stain map round
its turned footprint through `TrampleStamps.AddWornGround`: ragged stain discs about as wide as the band, overlapping
along each side and jittered from the building's position, so the mud reaches about the width past the footprint
with an uneven edge. Like every stain it lasts the match. The system keeps the buildings it stamped and forgets them
on a replay seek back, as `TrenchStampSystem` does, so the cleared maps get the mud again. Plants ignore mud. A
`Trench` never gets it: it is dug earth already.

**Craters dent and raise the drawn ground.** A scar stamp draws a blast bowl (`ScarShape.hlsl`): a flat floor and a wall
that eases up to the crest, no steeper than the heightmap draws cleanly, with slumped earth part way up and lumpy
value noise over it, deepest kept (Max). The same
stamps then draw the lip, with Min on alpha only, so a new lip also buries part of an older bowl: a sharp crest just
outside the bowl that thins with the cube of the distance, as thrown earth does, gone by twice the bowl radius or the
stamp's edge, whichever is nearer (1.3 times for a bare bowl). The lip and the outer scorch break into radial rays.
`GroundScar.burnReach` sets how far scorch and stains reach, as a multiple of the bowl radius, so a small deep bowl can
sit in a wide burn, and a wide burn leaves room for a long lip.

**Craters show dug earth.** Wherever a scar dented the ground, `TerrainCraterGround.hlsl` lays the material's
`_StainCrater*` texture set over the char and ash, darker where the ground was scorched; lips keep the burn's char and rays. It is hex tiled from above and
projected from the sides on steep ground, so a bowl wall keeps its texel size. An `ImpactMark`'s `debris` sets are thrown
from across the bowl through `DebrisSystem` and stay where they land; their LitTeam material sinks them into the bowl.
`CraterTerrainSystem` lowers the active Unity Terrain's heights under each new bowl and raises them under its rim,
without touching the `TerrainData` asset. `CraterSink.hlsl` moves objects by the same signed depth in the LitTeam, Foliage,
Bark and RowCrop vertex stages: down in a bowl, up on a rim. The terrain darkens only where the ground sank. A LitTeam material's `_CraterSink` scales it: 0 for a model that brings its own dent, such as
a trench, 1 otherwise. The simulation's ground never changes.

**Buildings level the drawn ground.** The same pass, `CraterHeight.shader`, levels the heights under every finished, seen
building that has a closed `NavObstacle` (neutral ones too; under construction or fog-hidden ones wait), so uneven ground
never covers the apron of a model. A bridge, a building with a Deck `NavArea`, is left alone: it spans the ground the map
shaped under it. Each building is a `BuildingLevel`: its rotated rectangle and base height, uploaded as a
structured buffer. A texel eases to the base, `BuildingLevel.Drop` (3 cm) below it, with weight 1 inside the footprint
and a smoothstep to 0 over `BuildingLevel.Skirt` (2.5 m), then the crater depth comes off. When a building appears or changes,
`TrampleStamps.AddFill` stamps its footprint and skirt into the tracks map once, with Min and Max passes that put alpha back
to the bias, so no bowl or rim stays under it. `CraterTerrainSystem` re-dents a building's bounds plus the skirt then and
again a frame later, once the fill is drawn, and `Restore` still puts the originals back. The undented heights sinking
shaders read are unchanged. Units walking the skirt may sit up to a few
centimetres off the drawn ground.

**Buildings can open the drawn ground.** A `TerrainHoleAuthoring` building, once finished and seen, has `TerrainHoleSystem` cut
its rotated rectangle out of the active Terrain's holes, so a model that goes below ground (a shaft, a stair) shows, and
fill it when the building goes. The terrain shader clips holes only in its `_ALPHATEST_ON` variant, so the first cut
gives the terrain a runtime copy of its material with that keyword, leaving the shared material asset untouched.
Release puts back the `TerrainData` holes and the original material, so none are saved. Only the drawn terrain changes: the simulation's ground and pathing do not, but `SetHoles` also opens the terrain collider there.

**Roads follow the craters.** `Road.shader` moves each vertex by the signed ground level at its own world position, since
a ribbon spans many craters, and fades out where a bowl is deeper than about 0.3 m, so a crater breaks the road. It chars
with the tracks map's scorch and takes the ash, rubble and mud stains and the crater earth through `TerrainCraterGround.hlsl`, as the terrain does
but with less than half the ash (`ROAD_ASH`), so it still reads as a road through a burn,
guarded like the terrain by whether the maps are published. A road material sets the same `_Stain*` properties as the terrain's.

## Building decals

`BuildingDecalsAuthoring` on a Building lists decals for its apron, such as oil stains and tyre tracks at its doors:
each a material, a centre in the building's space at the apron's top, a width and length, and a yaw of the texture's
top from the building's front. `BuildingDecalSystem` shows them as pooled URP decal projectors while the building
stands finished and the local player sees it, and takes them back when it is fogged, dies or goes. Each projects
straight down through a short `depth` (0.6 m by default) and shares the impact marks' angle fade, so it marks the
apron and the ground just past it but not the walls. A building's meshes hide through `MaterialMeshInfo` and not by
`Disabled`, so a `DecalProjector` baked on a prefab child as an Entities Graphics companion would keep drawing in
fog and under construction: building decals go through this list instead. LitTeam's forward pass applies the DBuffer
as URP Lit does, so decals land on model aprons as well as on the terrain.

## Building footprints

`BuildingFootprintSystem` draws every building's obstacle box into an R8 mask at 0.5 m texels, so a turned wall is not a
staircase, and publishes it as `_HyperRTSFootprints` with `_HyperRTSFootprintRect`, through a `MapTextureGlobal`: a
texture global and its rect, which shaders read as nothing published when cleared. The mask is a soft cover: 1 inside
the turned box, falling linearly to 0 across `BuildingFootprintMask.Margin` (2 m) outside it.
`BuildingFootprints.hlsl` reads it three ways. `BuildingMargin` is the raw cover. `NearBuilding(position, random)`
is true wherever the cover beats a little per-plant jitter, so grass and row crops thin out to a ragged edge at the
margin's far side, whatever the trample quality. `BuildingCover` and `UnderBuilding` cut the margin away and mean
inside the box, for roads.

## Field fires

Shells that land on dry fields set them alight, as client visuals only. A `FieldFireSettings` component in the scene, one
per climate, names the fire rig, the dry terrain layers and the limits on ignition, spread and fire count. Without one,
or without an active terrain, `FieldFireSystem` idles.

- `FieldFires` is a 1 m grid. Roads, water, bare soil and ground near buildings stay bare, and so does the burn reach of
  an `ImpactMark` whose `GroundScar.burnsOut` is set: a strike leaves nothing to burn in its crater.
- A seen impact lights the dry cell it hit when it is big enough and the `GroundWetness` singleton is low. Burning cells light their
  neighbours, faster downwind, and a front dies out by itself.
- Fires show only on ground the local player sees (`CueVisibility.IsSeen`), so they never reveal where enemy shells land.
- Burning cells feed `BattleHaze.Add`. A cell that burns out stamps ash and scorch, and the grass and row crop kernels drop
  plants where ash lies (`BurntPlants.hlsl`). Tree shaders read it too, sampled at the tree's root: `Foliage` collapses
  leaf cards one by one (by each card's pivot hash) and scorches the rest, a modelled shrub goes whole, and `Bark` chars
  to near black and matt. Standing, fallen and stripped trees all draw with these shaders, so all burn.

## Ambient life

An `AmbientLife` component in the scene, one per climate, adds bird flocks (`HyperRTS/Bird`) and dust devils. They live
on the client only. A count of zero turns a feature off.
