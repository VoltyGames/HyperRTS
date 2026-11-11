# How a map is built

What the builder does with a [spec](README.md): the land passes, ground rendering, checks and outputs. It is for
readers extending the builder. The code is toured in [map tools](code.md).

## Land use

The land is a 1 m grid. Each cell is claimed once, by one region with a land kind, a surface and a row angle.
`MapLandPlanner` runs the passes in priority order, and each takes only what earlier ones left:

1. Water (rivers and ponds), roads, river banks and floodplain, then pond margins and dams.
2. Bases, prop pads and access tracks.
3. Ravines, settlements, spec tree rows and spec areas.
4. Woods, belts and farmland.
5. Meadow on everything left, then each surface's own lone trees and props.
6. War scars for the spec's `warAge`, from the climate's war kit.
7. Ground decals: each surface's decals per hectare, then one under every prop that names a `groundDecal`.

Passes decide at points in the authored sector and place the result in every copy, which keeps maps exactly
symmetric. A surface prop that lands in the backdrop is drawn as its `backdropModel`, or left out without one.

- **Farmland** splits free land into fields, picks a crop for each by the kit's weights and lays belts and tracks
  along the field ends. A track end near a road joins it; a track that leads nowhere is dropped. Rows turn a quarter
  at the headlands, as the plough does.
- **Settlements** fit plots along their streets: house, yard, outbuildings, garden, orchard, fences and street
  furniture, with worn paths between them. The report says why a plot didn't fit. A prefab with
  `StretchPartsAuthoring` keeps its end caps at their own size when stretched, and its remains need the component too.
- **War scars** (`MapWarScarsPlanner`) work on open land in the authored sector, clear of roads, bases and
  settlements: shredded trees swapped in, wrecks by belts and woods, trenches along belt edges, craters dug into the
  heightmap with decals, and burnt patches. Empty prop and tree lists skip their step.
- **Access tracks.** Nearby props asking for the same road class share one track.

`MapRefiner` reshapes the sector before the copies are made, and the file keeps the coarse lines: rivers meander but
stay straight at their ends and crossings, roads become curves, ravines smooth and fork, and a road end that stops
short of another is joined onto it.

## Ground and rendering

Heights come from relief, hills, river valleys, ponds and ravines. Roads of a levelling class sit on embankments,
with ramps up to bridges. Every surface sets an `edgeWidth` (the blend band) and an `edgeWander`; where two surfaces
meet the crisper one wins, and the wander turns with the symmetry.

| Output | What it is |
| --- | --- |
| Ground textures | Terrain layer weights. A surface's `mixes` paint other layers by symmetric noise; steep ground takes the climate's slope surface |
| Grass | A coverage slice per plant in `<Map>_GrassCoverage.asset` and a `GrassField` (`<Map>_Grass.asset`). Wild ground is clumped by noise; a plant with `stands` below 1 grows only in stands of its own. Nothing grows on roads or placed objects |
| Ground tint | Light, warmth, moisture and wetness per texel, set by the climate's `groundTint` section |
| Row map | Each cell's crop row direction, turned with its symmetry copy |
| Ground materials | A `GroundMaterialMap` of one `SurfaceMaterial` byte per cell, for impacts and wheel debris |
| Ground colour | The ground's colour from above, so foliage blends in at its roots |
| Ground decals | A URP `DecalProjector` per decal |
| Road ribbons | A mesh just above the ground, drawn by `HyperRTS/Road` |
| Weather | A `WeatherSet` holding every climate weather, with the spec's weather active. In play it swaps in the one `MatchSetup.Weather` names |

**Grass** is drawn by `GrassRenderer`, not terrain details. `Grass.compute` scatters each visible tile's plants on a
world-anchored lattice, so plants never swim as the camera moves, and draws them with `Graphics.RenderMeshIndirect`
on the `Hidden/HyperRTS/FoliageGrass` shader, so wind and trampling work as on any Foliage plant. Grass and row crops
share one scatter host: see [row crops](../presentation/vegetation.md#row-crops).

**Terrain material.** `HyperRTS/Terrain` blends up to 16 layers by texel height. Layers are hex-tiled and randomly
turned (`antiTiling`) to hide repeats, layers with `followsRows` turn with the row map, and a `plants` setting thins
scattered plants where a layer is painted. Close up it adds a micro-texture grain, a sideways-projected slope layer
and parallax; layer relief softens with distance so the ground reads by colour from the match camera. Its Meta pass
gives Surface Cache GI the ground's colour from above (`MapGroundColor`).

Each map's terrain material is a Material Variant of the climate's `terrain` base material that `MapTerrainMaterial`
writes, overriding only the layer arrays and the map's textures. `PresentationQuality.Apply` sets the shader quality
from the **Ground** level: Low samples each layer once, Medium adds hex tiling, High adds the far sample, micro
detail and triplanar slopes, and Ultra adds parallax. Edit mode sets none, so map captures show Ultra.

## Checks and measures

Checks run before anything is refined. `MapAssetCheck` covers the climate and palette, `MapSpecCheck` the spec, and
`MapFieldCheck` walks the spec, the climate, the palette and every item of them, checking each `[MapKey]` field and
each `[Above]` or `[Min]` number, and names the bad value by its path, such as
`belts[1].kind 'Hedge' is not a belt in climate 'Steppe'`. A key that may be empty sets `Optional = true`. After the checks, lookups throw, so a missed key fails
loudly. Once props are placed, `MapPlacements.CheckEntities` errors on any play-area prop prefab without HyperRTS
authoring, since the simulation must see it to clear or crush it. The backdrop draws `backdropModel`s instead.

The build then measures the map on the nav grid:

- **Roads.** Warnings for dead ends and bends tighter than half the class's curve radius.
- **Rush and paths.** Rush times between bases and the number of distinct attack paths, for a unit one cell wide that
  walks through scenery, as infantry do.
- **Bases.** Reach, buildable ground and resources per base. Resources within 2.5 base radii count as the base's.
- **Renewable resources.** A renewable node among a base's resources is an error, since base fields must run dry.
  Stacks within 20 m of each other form one field, and fewer fields than the rules' `minRenewableFields` per player
  is an error.
- **Decals.** More than the rules' `maxDecals` is a warning.
- **Composition.** The play area is tiled with camera frames (rules `compositionFrame`). A frame should hold at least
  `compositionMinKinds` of five kinds: a wood or belt edge, water, a field pattern, a built place and relief. The
  report stores the share of frames that do under `land.composition`; under `compositionTargetShare` is a warning.
- **Props.** The report lists props per hectare of each surface; a surface under its rules `propTargets` is a warning.

`MapHintWriter` turns the grid into bot hints (expansion order, attack lanes, defence points) that the skirmish AI
needs to play the map. `MapSpecRule` shows a built map's report errors on **HyperRTS ▸ Tools ▸ Validate**.

## Outputs

A preview writes `Logs/MapReports/<Map>.preview.png` and `.preview.json`. A build writes the report to
`Logs/MapReports/<Map>.json` and `.png`, and everything else to a `Generated/` folder beside the spec: the scene and
its `_Entities` SubScene, terrain, water and road assets, the ground tint, row, ground colour, ground material,
minimap and overview images, row crop data, baked lighting, and anything the game's `IMapBuildHook` adds.

Generated objects sit under `[Generated]` roots and are replaced on every build. The SubScene's prefab instances are
also listed in `Generated/<Map>_BuildObjects.txt`, so a build still finds them if a prefab change drops their name.
Put hand-placed objects under `[Dressing]`, which the build never touches. Errors before the scene stage stop the
build and leave the last good scene alone. Without a graphics device, a build skips lighting, minimap and views and
keeps the last ones; a map's first build needs graphics.

## Adding a kind of land use

1. Add a kit in `Kits/`, like `MapFarmlandKit`, held by `MapClimate`. Give it a library type, check its keys in
   `MapAssetCheck` and list them in `MapKeyReference`.
2. Add a `MapLandKind` if none fits, and the surfaces it paints.
3. Add a `MapLandPass` in `Planning/` that claims land through `MapPlanContext`. Decide in the authored sector and
   place in every copy, and slot it into `MapLandPlanner.Plan` at its priority.
4. Add a spec class in `Spec/` that derives from `MapFeatureSpec`, implements `IMapPoint` or `IMapLine` and carries
   `[MapFeature(name, order, colour)]`, with `[MapKey]` on its keys. Add its list to `MapSpec` and `MapLayout`, and
   one `Points` or `Lines` call in `MapSymmetryCopier`. The key check, the Map Editor and the schema pick it up from
   the attribute.
5. Write EditMode tests: the planner's output on a small layout, and a bad key failing loudly.
