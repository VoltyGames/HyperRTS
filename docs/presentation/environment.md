# Environment

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Environment

Sky, fog, weather, wind, water, field fires and ambient life. Ruts, craters and stains are in [ground](ground.md),
plants in [vegetation](vegetation.md). For game developers building a climate and artists authoring tree and water
meshes.

## Atmosphere

An `Atmosphere` component sits on the root of a weather's atmosphere prefab, with its sun, post-processing volume and
`Wind`. When enabled it sets the skybox, ambient light, reflection cubemap, and turns Unity fog off. It writes the ground wetness each frame as the `GroundWetness` singleton and `_HyperRTSWetness`, and its `mist` as
the `GroundMist` singleton; `WetnessGroup` shows its children above a threshold of `_HyperRTSWetness`, so puddles also
show in edit mode. A missing skybox or reflection is an error. Skyboxes use `HyperRTS/Sky`, which blends its photographed ground into haze
below the horizon.

`CloudShadows` on the sun draws a drifting cloud cookie that includes smoke column shadows (`SmokeShadows`).

Scene fog is URP's `Volumetric Fog` renderer feature. URP compiles it only under the `VOLUMETRIC_FOG` scripting define, so
a project using it sets that define. `FogOfWarFeature` keeps orthographic captures such as the minimap clear.

**Battle haze.** While `PresentationQuality.BattleHaze` is on, `BattleHazeSystem` thickens the fog over a fight. Seen
impacts, burning wrecks and damaged buildings add haze to a coarse grid; it drifts downwind and thins. One blast's
haze is capped in reach and amount, so a very large one smokes its own ground and no more. Other sources call
`BattleHaze.Add(position, radius, amount)`. Each step rewrites the density texture on worker threads from a copy of
the grid and uploads it the next frame. Still, clear air skips both: the grid clears once its
thickest cell is under `BattleHaze.Faintest`.

Mist carries its own density, divided by the weather's volumetric fog density (`FogShape`, read from the volume stack
each step, with the fog's height falloff) before both go in the half-float density texture. The weather's density is
tiny, so a multiple of it would stay faint.

- **Mist.** `Atmosphere.mist` (0 to 1) lays ground mist in hollows (cells at least 5 m under their 60 m surroundings)
  and on banks within 2.5 m of the water. It rests on the ground and fades linearly to nothing 6 m up, with an
  extinction of `BattleHaze.MaxMistExtinction` times `mist`. The texture has 4 m layers up the y axis; with no mist it has two.

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

`HyperRTS/Water` draws rivers and ponds: the bed refracted through the water, a body lit by the sun and sky, the sky
mirrored at grazing angles, a sun glint and foam. It needs the Depth and Opaque Textures.

- **Body.** `Shallow Color` and `Deep Color` are albedos lit like the ground and blended by the depth of the bed, so
  a river shades from bank to channel and follows each weather's light. Caustics play on a shallow bed; `WaterCaustics`
  off in [quality](README.md#quality) compiles them out.
- **Surface.** Three scales of wind-wave ripples (`WaterRipples.png`, made by `WaterSpectrum`) scroll down the river
  at one speed, so they never shear. Broad blobs in `WaterLace.png` drift calm slicks down the current and sweep
  wind gusts across it.
- **Foam.** Each source gives an amount and the strongest is drawn through the lace (`FoamLace`): a little foam is a
  few soft flecks, a lot is whole. The sources are the shallowest water along banks and round hulls, a ford's fast
  water, unit wakes (`SampleWake`) and bridge piers.
- **Vertex colours** come from the map build: alpha thins the water over a ford, green marks its fast water, which
  ripples harder and carries flecks of foam, and blue marks still water, where nothing flows.
- **Piers.** `WaterPiers` on the water object lists up to 16 bridge piers per map: the shader draws a cushion
  upstream of each, a short wake below it and rougher water round the wake.

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
