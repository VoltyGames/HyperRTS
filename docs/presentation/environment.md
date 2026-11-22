# Environment

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Environment

Sky, fog, weather, wind, water, field fires and ambient life. Ruts, craters and stains are in [ground](ground.md),
plants in [vegetation](vegetation.md). For game developers building a climate and artists authoring tree and water
meshes.

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
by `WaterSpectrum`), reflection, foam and unit wakes (`SampleWake`). It needs Depth and Opaque Textures. `Shallow Color`
and `Deep Color` are albedos lit by the sun and sky like the ground, blended by the depth of the bed over `Deep Depth`,
so the river shades from bank to channel and follows each weather's light. `WaterLace.png` (`FoamLace`) holds lace
strands, foam clumps and broad blobs: the blobs drift calm slicks down the current and sweep wind gusts across it,
and every foam source (banks, slick edges, fords, wakes, piers) is an amount drawn through the lace, so thin foam
breaks into strands rather than fading. Vertex colours shape the flow: alpha thins fords, green marks rapids, red
trails foam below fords; both draw white water as streaks down the current with open water between. Bridge piers come from `WaterPiers` on the water object, up to 16 per map: the shader draws
a cushion upstream of each, a short wake below it and rougher water round the wake.
`WaterCaustics` off in [quality](README.md#quality) compiles the caustics out.

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
