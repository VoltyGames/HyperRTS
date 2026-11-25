# Ground

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Ground

The ground that remembers what happened on it: ruts, craters, scorch, wakes and stains, building decals and the
footprint mask plants and roads read. For game developers wiring units and blasts to the ground, and artists
authoring terrain and road materials. Sky, water and weather are in [environment](environment.md).

## Trample maps

Vehicles leave ruts, shells leave craters and scorch, units in water leave wakes, and the ground keeps rubble and ash.
`TrampleMap` on the rig keeps four GPU textures over the map, drawn with the `Hidden/HyperRTS/TrampleBrush` shader.

| Texture | Channels |
| --- | --- |
| `_HyperRTSTrample` | RG travel direction, B flattened grass that recovers, A crop damage that never recovers |
| `_HyperRTSTracks` | R rut depth, G churned soil (both fade over minutes); B scorch, A ground level (lasts the match) |
| `_HyperRTSWake` | R foam, which fades in seconds |
| `_HyperRTSStains` | R rubble, G ash, B fuel, A wet mud; last the match |

Alpha holds a level round a bias: `TrampleStamps.GroundLevelBias` (40 of 255) is untouched ground, above it is depth
and below it lift, over `GroundRange` (10 m), so up to 8 m deep and 1.5 m raised. Shaders decode it with `GroundSunk` in
`TrampleGlobals.hlsl`: metres the ground sank, negative on a rim. Clears and resets set alpha to the bias, and the
fade passes leave it alone.

An `ImpactMark`'s decal projector leans with the slope measured across its whole width (`decalReach` included), so a
wide projector does not tip out of the ground on one side.

Ruts deepen with traffic. A lone pass stores a share of its depth (`RutLoneShare`), which readers divide back; the
`RutDeepen` pass adds `RutDeepening` of it per pass, drawn only where a stamp is new, so overlapping frames and a
wheeled vehicle's axles add once between them. The 8-bit channel tops out at `RutCap`, one ceiling for every vehicle.
Roads do not draw ruts.

`TrackMarks` on a vehicle, and the `GroundScar` of an `ImpactMark` or `DeathStainAuthoring` (crater depth, rim height,
burn reach, scorch, rubble, ash, fuel, mud), stamp the maps. Other sources, such as collapsing buildings, call
`TrampleStamps.AddStain`. Shaders read the maps through `Trample.hlsl`. The terrain tiles the rubble, ash and mud
textures over the stains with its layers' hex tiling (`TerrainStains.hlsl`). Ash lies in drifts (`AshDrift`): noise
thins it in patches so the char and soil under a wide burn show. Ash and rubble slide off slopes steeper than about 32
degrees and are gone past about 53.

**Buildings wear the ground round them.** A Building with a `wornGroundWidth` above 0 gets a `WornGround`. The first
time it is seen finished, `WornGroundStampSystem` stamps wet mud of its `wornGroundStrength` into the stain map round
its turned footprint (`TrampleStamps.AddWornGround`) as overlapping, jittered stain discs, so the mud reaches about the
width past the footprint with an uneven edge. The discs cover the inside too, so the plot is one worn patch when the
building is gone, and its remains keep the grass under them flat until they are buried. It lasts the match. The system forgets its buildings on a replay seek
back, as `TrenchStampSystem` does, so the cleared maps get the mud again. Plants ignore mud, and a `Trench` never gets
it.

**Craters dent and raise the drawn ground.** A scar stamp draws a blast bowl (`ScarShape.hlsl`): a flat floor and a
wall that eases up to the crest, with slumped earth part way up and value noise over it, deepest kept (Max). The same
stamps draw the lip with Min on alpha only, so a new lip also buries part of an older bowl: a sharp crest just outside
the bowl that thins with the cube of the distance, gone by twice the bowl radius or the stamp's edge, whichever is
nearer (1.3 times for a bare bowl). The lip and the outer scorch break into radial rays. `GroundScar.burnReach` sets how
far scorch and stains reach, as a multiple of the bowl radius.

**Craters show dug earth.** Where a scar dented the ground, `TerrainCraterGround.hlsl` lays the material's
`_StainCrater*` texture set over the char and ash, darker where scorched; lips keep the char and rays. It is hex tiled
from above and projected from the sides on steep ground, so a bowl wall keeps its texel size. An `ImpactMark`'s
`debris` sets are thrown across the bowl through `DebrisSystem` and stay where they land; their LitTeam material sinks
them into the bowl.

`CraterTerrainSystem` lowers the active Unity Terrain's heights under each new bowl and raises them under its rim,
without touching the `TerrainData` asset. `CraterSink.hlsl` moves objects by the same signed depth in the LitTeam,
Foliage, Bark and RowCrop vertex stages. A LitTeam material's `_CraterSink` scales it: 0 for a model that brings its
own dent, such as a trench, 1 otherwise. A vehicle or wreck is wider than most bowls, so `SinkRadiusBakingSystem` gives
each of its meshes the body's half length as `SinkRadius`, and the mesh drops only as far as the shallowest ground on
that ring: a hull stands across a bowl narrower than itself instead of dropping into the ground beside it. The terrain darkens only where the ground sank. The simulation's ground never
changes.

**Buildings level the drawn ground.** `CraterHeight.shader` also levels the heights under every finished, seen building
that has a closed `NavObstacle` (neutral ones too; under construction or fog-hidden ones wait), so uneven ground never
covers a model's apron. A bridge, a building with a Deck `NavArea`, is left alone. Each building is a `BuildingLevel`,
its rotated rectangle and base height, uploaded as a structured buffer. A texel eases to the base,
`BuildingLevel.Drop` (3 cm) below it, with weight 1 inside the footprint and a smoothstep to 0 over
`BuildingLevel.Skirt` (2.5 m), then the crater depth comes off. When a building appears or changes,
`TrampleStamps.AddFill` stamps its footprint and skirt into the tracks map once, with Min and Max passes that put alpha
back to the bias, so no bowl or rim stays under it. `CraterTerrainSystem` re-dents the bounds plus the skirt then and a
frame later, once the fill is drawn, and `Restore` puts the originals back. Units on the skirt may sit a few
centimetres off the drawn ground.

**Buildings can open the drawn ground.** A `TerrainHoleAuthoring` building, once finished and seen, has
`TerrainHoleSystem` cut its rotated rectangle out of the active Terrain's holes, so a model that goes below ground (a
shaft, a stair) shows; the hole fills when the building goes. The terrain shader clips holes only in its
`_ALPHATEST_ON` variant, so the first cut gives the terrain a runtime copy of its material with that keyword. Release
puts back the `TerrainData` holes and the original material, so none are saved. Only the drawn terrain changes, though
`SetHoles` also opens the terrain collider there.

**Roads follow the craters.** `Road.shader` moves each vertex by the signed ground level at its own world position and
fades out where a bowl is deeper than about 0.3 m, so a crater breaks the road. It chars with the tracks map's scorch
and takes the ash, rubble, mud and crater earth through `TerrainCraterGround.hlsl`, with under half the terrain's ash
(`ROAD_ASH`) so it still reads as a road through a burn. A road material sets the same `_Stain*` properties as the
terrain's.

## Building decals

`BuildingDecalsAuthoring` on a Building lists decals for its apron, such as oil stains and tyre tracks at its doors:
each a material, a centre in the building's space at the apron's top, a width and length, and a yaw of the texture's
top from the building's front. `BuildingDecalSystem` shows them as pooled URP decal projectors while the building
stands finished and the local player sees it. Each projects straight down through a short `depth` (0.6 m by default)
and shares the impact marks' angle fade, so it marks the apron and the ground just past it, not the walls. A building's
meshes hide through `MaterialMeshInfo`, not `Disabled`, so a `DecalProjector` baked on a prefab child would keep
drawing in fog and under construction; use this list instead. LitTeam's forward pass applies the DBuffer as URP Lit
does, so decals land on model aprons as well as on the terrain.

## Building footprints

`BuildingFootprintSystem` draws every building's obstacle box into an R8 mask at 0.5 m texels and publishes it as
`_HyperRTSFootprints` with `_HyperRTSFootprintRect` through a `MapTextureGlobal`. Grass and row crops keep clear of
buildings, and road ribbons hide under them, through this mask. It is a soft cover: 1 inside the turned box, falling
linearly to 0 across `BuildingFootprintMask.Margin` (2 m) outside it. `BuildingFootprints.hlsl` reads it three ways.
`BuildingMargin` is the raw cover. `NearBuilding(position, random)` is true wherever the cover beats a little per-plant
jitter, so plants thin to a ragged edge at the margin's far side, whatever the trample quality. `BuildingCover` and
`UnderBuilding` cut the margin away and mean inside the box, for roads.
