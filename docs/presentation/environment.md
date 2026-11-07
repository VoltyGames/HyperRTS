# Environment

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Environment

Sky, fog, weather, wind, water, horizon, and the ground that remembers what happened on it. Plants have their own page,
[vegetation](vegetation.md). For game developers building a climate and artists authoring tree and water meshes.

## Atmosphere

An `Atmosphere` component sits on the root of a weather's atmosphere prefab, with its sun, post-processing volume and
`Wind`. When enabled it sets the skybox, ambient light, reflection cubemap and wetness, and turns Unity fog off. A
missing skybox or reflection is an error. Skyboxes use `HyperRTS/Sky`, a cubemap sky whose photographed ground gives way
below the horizon to a blurred haze of the sky's own colour (`Horizon Band`), so a long view meets haze instead of a
close-up field.

`CloudShadows` on the sun draws its cookie every frame into a render texture that wraps around the camera: the tileable
cloud picture drifting with the wind, times the shadow of each smoke column `SmokeShadows` hands it. The `smoke` toggle
leaves the columns out. The shader is `Hidden/HyperRTS/Cloud Cookie` (a Resources shader). URP's volumetric fog samples
the same cookie where the profile's `Fog` turns on light cookies, so cloud and smoke shadows cut shafts through the haze.

Scene fog is URP's: its `Volumetric Fog` renderer feature draws the profile's `Fog` override over the opaques after the
skybox. URP compiles it only under the `VOLUMETRIC_FOG` scripting define, so a project using it sets that define; the
engine references its types only in `BattleHazeSystem.Fog.cs` under the same define. URP skips orthographic cameras
without resetting its fog globals, so `FogOfWarFeature` clears them there with `ClearAtmosphereFogPass`, keeping captures
such as the minimap clear.

**Battle haze.** `BattleHazeSystem` thickens the volumetric fog over a fight while `PresentationQuality.BattleHaze` is
on. `BattleHaze` is a coarse grid over the map: seen impacts, burning wrecks, damaged buildings and ruin collapses add
haze, which drifts downwind, spreads and thins over time, up to a multiple of the weather's own density.
`BattleHaze.Add(position, radius, amount)` is open to any other source, as field fires use it. The grid becomes a
`Texture3D` and a global volume above the weather's overrides only the `Fog` density mode, density texture and tiling.
The tiling's floor caps the haze to maps up to 1000 m across, and it shows only within the fog's cutoff depth.

## Horizon

`Horizon` draws farmland round the map out to its `width`, for a camera tilted toward the horizon. A map build adds it
when its spec has a `horizonWidth`. It builds in memory only, in edit mode too, and submits to each game and Scene view
camera before it renders (`SceneCameras.Draws`). Nothing in it collides, navigates or casts a shadow.

- **Ground.** `HorizonRing` builds a mesh ring round the terrain, heights following the terrain edge then blending into
  noise relief (`HorizonRelief`). `HyperRTS/HorizonGround` paints it with the map terrain material's layer arrays.
  `HorizonFields` lays fields on a grid at the farmland kit's field size, picks a crop per field by weight, and runs
  tracks and shade blocks, all by hash, handing the shader a small texture (`HorizonFields.hlsl`). Far fields fade to
  the weighted mean so the ground doesn't sparkle.
- **Sky fade.** Over the ring's outer share (`HorizonSky.fadeShare`) ground and belts take the sky's horizon colour per
  bearing (`SkyHorizon.hlsl`). `Horizon` reports a skybox that isn't `HyperRTS/Sky`.
- **Belts.** A belt kit's `horizonStrip` material (`HyperRTS/HorizonBelt`) stands as two staggered layers of quads along
  row edges, with gaps where tracks cross. A climate whose kits have no strip draws no belts.

## Wind

`Wind` sits on the scene's directional `WindZone` and publishes strength, gusts and direction to shaders. Its
`vegetation` setting picks how much plants move: `None`, `Sway`, `Branches` or `Flutter`. Flags fly regardless. It also
keeps a `WindState` singleton (downwind direction and strength) for the machine, dust devil, battle haze and field fire
systems; without a `Wind` there is no singleton and the air is still. Every shader pass of Foliage and Bark uses the same
wind, so shadows match.

Grass and crops sway about their origin with gusts rolling downwind. Trees bend at the trunk, sway their branches and
flutter their leaf cards. Bark and leaves of one tree need the same `_WindStrength`, `_WindSpeed`, `_BranchSway`,
`_TreeHeight` and `_WindScale`; set `_WindScale` to 0 for fallen trees.

Tree meshes for Foliage with **Tree Wind** on, and for Bark, carry wind data in object space, in metres:

| Channel | Data |
| --- | --- |
| TEXCOORD2.xy, TEXCOORD3.x | Branch pivot |
| TEXCOORD3.y, TEXCOORD4.xy | Leaf card pivot |
| COLOR r | Canopy ambient occlusion |
| COLOR g | Branch flex: 0 at the pivot and on the trunk, 1 at the tip |
| COLOR b | Stiffness: 0 limb, 1 twig |
| COLOR a | Flutter weight, 1 on leaf cards |

`LocalGustSystem` adds up to 8 radial gusts to `Wind.hlsl`: downwash under a seen rotorcraft and the blast of each seen
impact of 1 m splash or more. Plants lean away and tree trunks bend. Trample quality Off turns them off.

## Water

`HyperRTS/Water` refracts the bed (darkened to wet brightness) with per-channel absorption toward a scatter colour,
caustics on shallow beds, three scales of ripples from the wind-wave spectrum `WaterRipples.png` (an FFT in
`WaterSpectrum`), flow-mapped downstream, Fresnel with smoothness that falls with distance so far water blurs to the sky,
screen-space reflection, a sun glint clamped just past the bloom threshold, a soft shore fade, foam and unit wakes
(`SampleWake`). It needs Depth and Opaque Textures. Vertex colours shape the flow: alpha thins fords, green streaks
rapids foam, red trails foam lines downstream of fords and bridges. Open water carries no streaks; banks get their own
foam band. `WaterCaustics` off in [quality](README.md#quality) compiles the bed pattern out.

## Trample maps

Vehicles leave ruts and flatten grass and crops, shells leave craters and scorch, units in water leave wakes, and the
ground keeps rubble, ash, fuel and wet mud. `TrampleMap` on the rig keeps four GPU textures over the map and stamps them
each frame; it needs the `Hidden/HyperRTS/Trample Brush` shader.

| Texture | Channels |
| --- | --- |
| `_HyperRTSTrample` | RG travel direction, B flattened grass that recovers, A crop damage that never recovers |
| `_HyperRTSTracks` | R rut depth and G churned soil, which fade over minutes; B scorch and A crater depth, which last the match |
| `_HyperRTSWake` | R foam, which fades in seconds |
| `_HyperRTSRemains` | R rubble, G ash, B fuel, A wet mud. They only build up and last the match |

**Sources.** Vehicles with `TrackMarks` stamp while on the ground and visible, as do building footprints. At Full quality
walking squad figures lean plants along their path. A ground impact whose `ImpactMark` has a `craterDepth` or `scorch`
stamps a bowl inside a scorch disc and flattens plants; its `rubble`, `ash` and `fuel` fill the remains map. A death cue
the local player sees stamps the type's `DeathRemains` (`DeathRemainsAuthoring`: a radius and four amounts). Other
sources (collapsing buildings, field fires) call `TrampleStamps.AddRemains` after `TrampleStampSystem` has run.
`TrenchStampSystem` stamps each seen built `Trench` into the maps once, so the same dent follows. Units with a `NavAgent`
on water stamp foam. A replay seeking back clears the maps. Quality picks Full, Low at half resolution, or Off, which
frees the textures.

**Reading them.** Shaders include `Trample.hlsl`, which builds on `TrampleGlobals.hlsl` (`TrampleMapsOn`, `TrampleUV`,
`OnTrampleMaps`, `HYPERRTS_MAX_CRATER_DEPTH`). The terrain darkens ruts, bends its normal down a crater's slope and
blackens scorch; `TerrainRemains.hlsl` lays the terrain material's `_Remains*` textures, a fuel slick and char over what
the remains map covers. Foliage leans plants along the travel direction and water reads `SampleWake`.

**Craters dent the drawn ground.** In presented worlds `CraterTerrainSystem` redraws the active Unity Terrain's heightmap
under each new bowl on the GPU (`Hidden/HyperRTS/Crater Height`), reading back at most twice a second. A replay seek, new
or removed maps, another terrain and the world's end restore the copied heights, so dented heights never reach the
`TerrainData` asset. `CraterSink.hlsl` samples the same depth: the LitTeam, Foliage, Bark and RowCrop vertex stages lower
the whole object by the depth at its origin, fading with its height above the undented ground
(`_HyperRTSUndentedHeights`, published by `CraterTerrain`), so aircraft, bridge decks and rooftops stay put. A LitTeam
material's `_CraterSink` scales it: 0 for a model that brings its own dent, such as a trench, 1 otherwise. Squad figures
sink with it. The grass and row crop kernels, bound to the tracks map by `CraterTerrain.BindCraters`, place plants on the
undented ground first. The simulation's ground never changes.

## Building footprints

`BuildingFootprintSystem` draws every building's turned obstacle box into an R8 mask at nav-cell resolution and
publishes it as `_HyperRTSFootprints` with `_HyperRTSFootprintRect`. Like the ground picture, trample maps, undented
heights and fog grid, it goes through a `MapTextureGlobal`: a texture global and its rect, cleared to black and a zero
rect, which shaders read as nothing published. Grass and row crops skip plants where it is set (`BuildingFootprints.hlsl`),
whatever the trample quality. It redraws when buildings appear, go or differ, bumping `BuildingFootprintMask.Version` so
cameras rescatter.

## Field fires

Shells that land on dry fields set them alight, as client visuals only. A `FieldFireSettings` component in the scene, one
per climate, names the fire rig, the dry terrain layers, the wetness limit, the smallest blast, the ignite chance, how
long a cell burns, each fire's reach, the clearance round buildings and the cap on fires playing. Without one, or without
an active terrain, `FieldFireSystem` idles. It keeps a `FieldFireState` singleton counting burnt cells.

- `FieldFires` is a 1 m grid. A cell is dry where the listed layers dominate and the `GroundMaterials` cell isn't water;
  roads, water, bare soil and ground near any building stay bare.
- An impact the local player sees lights the dry cell it hit when its splash is big enough, it hit open ground,
  `Wetness.Level` is under the limit and the impact's seed passes the ignite chance.
- Burning cells light their neighbours on a timer, faster downwind, with each ignition's reach falling off so a front dies
  out by itself. A cell burns for a while then stays burnt.
- Every random choice hashes the impact's spot and source, the cell and its age, so clients that see the same impacts burn
  closely the same fields. A replay seeking back puts the fires out.
- Fires spread in fog too, so clients agree, but only ground the local player sees shows them
  (`CueVisibility.IsSeen`), so they never reveal where enemy shells land.
- Burning cells gather in squares; each seen square feeds `BattleHaze.Add` and the nearest ones stream the rig.
- A seen cell that burns out stamps ash into the remains map and scorch into the tracks map (`TrampleStamps.AddScorch`).
  The grass and row crop kernels drop plants where ash lies (`BurntPlants.hlsl`) and regrow when
  `FieldFireState.BurntCells` changes.

## Ambient life

An `AmbientLife` component in the scene, one per climate, adds birds and dust devils. They live on the client only, so
clients may differ. A count of zero turns a feature off; both are skipped at Low effect detail. Bird flocks circle over
the map, scatter from a nearby impact and draw in one call on `HyperRTS/Bird`. Dust devils form near the camera on the
chosen ground and drift downwind.
