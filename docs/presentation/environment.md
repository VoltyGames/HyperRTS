# Environment

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Environment

Sky, fog, weather, wind, water, and the ground that remembers what happened on it. Plants have their own page,
[vegetation](vegetation.md). For game developers building a climate and artists authoring tree and water meshes.

## Atmosphere

An `Atmosphere` component sits on the root of a weather's atmosphere prefab, with its sun, post-processing volume and
`Wind`. When enabled it sets the skybox, ambient light, reflection cubemap and wetness, and turns Unity fog off. A
missing skybox or reflection is an error. Skyboxes use `HyperRTS/Sky`, which blends its photographed ground into haze
below the horizon.

`CloudShadows` on the sun draws a drifting cloud cookie that includes smoke column shadows (`SmokeShadows`).

Scene fog is URP's `Volumetric Fog` renderer feature. URP compiles it only under the `VOLUMETRIC_FOG` scripting define, so
a project using it sets that define. `FogOfWarFeature` keeps orthographic captures such as the minimap clear.

**Battle haze.** While `PresentationQuality.BattleHaze` is on, `BattleHazeSystem` thickens the fog over a fight. Seen
impacts, burning wrecks and damaged buildings add haze to a coarse grid; it drifts downwind and thins. Other sources call
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

`HyperRTS/Water` is a refracting water shader with absorption, caustics, wind-wave ripples (`WaterRipples.png`, made by
`WaterSpectrum`), reflection, foam and unit wakes (`SampleWake`). It needs Depth and Opaque Textures. Vertex colours shape
the flow: alpha thins fords, green streaks rapids foam, red trails foam lines downstream of fords and bridges.
`WaterCaustics` off in [quality](README.md#quality) compiles the caustics out.

## Trample maps

Vehicles leave ruts, shells leave craters and scorch, units in water leave wakes, and the ground keeps rubble and ash.
`TrampleMap` on the rig keeps four GPU textures over the map; it needs the `Hidden/HyperRTS/Trample Brush` shader.

| Texture | Channels |
| --- | --- |
| `_HyperRTSTrample` | RG travel direction, B flattened grass that recovers, A crop damage that never recovers |
| `_HyperRTSTracks` | R rut depth, G churned soil (both fade over minutes); B scorch, A crater depth (last the match) |
| `_HyperRTSWake` | R foam, which fades in seconds |
| `_HyperRTSRemains` | R rubble, G ash, B fuel, A wet mud; last the match |

`TrackMarks` on a vehicle, an `ImpactMark` with `craterDepth` or `scorch`, and `DeathRemainsAuthoring` on a unit type stamp
the maps. Other sources, such as collapsing buildings, call `TrampleStamps.AddRemains`.
Shaders read the maps through `Trample.hlsl`.

**Craters dent the drawn ground.** `CraterTerrainSystem` lowers the active Unity Terrain's heights under each new bowl,
without touching the `TerrainData` asset. `CraterSink.hlsl` sinks objects by the same depth in the LitTeam, Foliage, Bark
and RowCrop vertex stages. A LitTeam material's `_CraterSink` scales it: 0 for a model that brings its own dent, such as
a trench, 1 otherwise. The simulation's ground never changes.

## Building footprints

`BuildingFootprintSystem` draws every building's obstacle box into an R8 mask and publishes it as `_HyperRTSFootprints`
with `_HyperRTSFootprintRect`, through a `MapTextureGlobal`: a texture global and its rect, which shaders read as
nothing published when cleared. Grass and row crops skip plants where it is set (`BuildingFootprints.hlsl`), whatever the
trample quality.

## Field fires

Shells that land on dry fields set them alight, as client visuals only. A `FieldFireSettings` component in the scene, one
per climate, names the fire rig, the dry terrain layers and the limits on ignition, spread and fire count. Without one,
or without an active terrain, `FieldFireSystem` idles.

- `FieldFires` is a 1 m grid. Roads, water, bare soil and ground near buildings stay bare.
- A seen impact lights the dry cell it hit when it is big enough and `Wetness.Level` is low. Burning cells light their
  neighbours, faster downwind, and a front dies out by itself.
- Fires show only on ground the local player sees (`CueVisibility.IsSeen`), so they never reveal where enemy shells land.
- Burning cells feed `BattleHaze.Add`. A cell that burns out stamps ash and scorch, and the grass and row crop kernels drop
  plants where ash lies (`BurntPlants.hlsl`).

## Ambient life

An `AmbientLife` component in the scene, one per climate, adds bird flocks (`HyperRTS/Bird`) and dust devils. They live
on the client only. A count of zero turns a feature off.
