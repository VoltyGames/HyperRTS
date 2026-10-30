# Maps

A map starts as a JSON spec, `<Map>.map.json`, that describes land use: farmland, settlements, woods, rivers,
ravines and roads. The builder lays the land out by rule and turns it into a scene, a SubScene and a report. You
say what goes where; the builder makes it look right and keeps it exactly symmetric.

The look of the land comes from the game's [map library](#map-library): terrain layers, plants, trees, kits and
props. Code lives in `Editor/Maps/` and runtime rendering in `Presentation/Environment/`. For a tour of the code,
see [map tools](modules/map-tools.md).

## Overview

You edit the spec, preview it in seconds, then build it. A build takes minutes and also captures screenshots from
the match camera.

| Stage | How to run it | Writes |
| --- | --- | --- |
| Edit | Any text editor. **HyperRTS ▸ Map Editor** shows the result | The spec |
| Preview | The Map Editor, `MapPreview.Run(spec)` or batch `.Preview` | Layout image, issues and land figures |
| Build | **Build** in the Map Editor or on **HyperRTS ▸ Tools ▸ Maps**, `MapBuilder.Build(spec)` or batch `.Build` | Scene, SubScene, assets, report, minimap, views |
| Views | Every build with graphics, or **Recapture views** in the Map Editor | Screenshots in `Logs/MapReports/<Map>/`, and their contrast in the report |

A preview needs no scene. It does not measure rush times or attack paths; only a build does.

The views' contrast is `viewLuminanceSpread`, the 5th to 95th percentile of luminance, and `viewLocalContrast`, the
mean distance of each 4 px cell from the 5 by 5 cells round it. Both average the ground views and leave out the
overview. Low figures mean flat, front-lit ground.

## Map Editor

**HyperRTS ▸ Map Editor** shows one spec on a top-down canvas. It is a viewer: you edit the JSON, and the editor
reloads the file when Unity imports it, and the canvas when a preview rewrites its image. Open a spec by
double-clicking a `.map.json` or with **Open** on **HyperRTS ▸ Tools ▸ Maps**. The editing tools were removed
while the spec format still changes often; the engine tag `map-editor-write-tools` keeps them.

| Part | Shows |
| --- | --- |
| Toolbar | Spec picker, **Ghosts** for the symmetry copies, **Auto preview**, **Preview** and **Build** |
| Feature list | Every feature grouped by kind. Click one to select it |
| Canvas | The latest preview under a metre grid, with bounds, sector edges, features and ghosts |
| Selection | Where the selected feature sits in the spec, such as `roads[3]` |
| Report panel | Issues, land figures and build metrics, with **Open scene** and **Recapture views** |

On the canvas, middle-drag or Space+drag pans, the wheel zooms and F fits the map. An issue that names a feature
has a **Show** button that frames it. **Build** saves open scenes, builds, reopens your scene and shows the report.

### Authoring rules

- **You author one sector.** The symmetry copies it into the others. A point outside the sector would overlap its
  own copies, so the editor rings it as a warning.
- **Shared features sit on the axis.** A feature with `"shared": true` is placed once, not copied. Put it on the
  mirror axis, or at the centre for a rotation.
- **Array order drives the randomness.** Each feature's random stream is seeded by the map seed and its index in
  its list. Deleting or reordering a feature reshuffles the detail of later features of its kind.
- **Points sit on a 0.5 m grid.**
- **Team modes need a two-copy symmetry**: `rotate-180`, `mirror-x` or `mirror-z`. `ffa` needs `rotate-3` to
  `rotate-8`, one sector per player.

Specs are saved in one fixed layout, `MapJsonFormat`, so files diff cleanly. A field the spec doesn't read, at any
depth, fails the load with its path, such as `hills[1].raduis`.

## Spec format

The spec is flat: one array per feature type, points as `{ "x", "z" }` objects. Units are metres. The origin is
the map centre, X points east, Z points north, and angles run clockwise from north.

| Header field | Holds |
| --- | --- |
| `name` | The map name. The file must be named `<name>.map.json` |
| `displayName` | The name the game shows |
| `mode` | `1v1` to `4v4`, or `ffa` |
| `mapType`, `status`, `notes` | `land`; work state; dated notes |
| `climate` | A library climate key |
| `weather` | Required. One of the climate's weathers. The scene is built and lit with it |
| `size` | Playable width (X) and depth (Z) |
| `symmetry` | `rotate-180`, `mirror-x`, `mirror-z`, or `rotate-3` to `rotate-8` |
| `seed` | Seeds every random stream and noise field |
| `terrain` | `height`, `relief`, `reliefScale`, `waterLevel`, `maxHeight`, `resolution`, `backdropWidth` |

Base `height` is lowered to world y = 0. The backdrop is unplayable land past the playable edge. Slots interleave
across the copies, so slots 1 and 2 always face each other.

| Feature | Fields | Builds |
| --- | --- | --- |
| `spawns` | `pos`, `yaw` | A slot's start position, levelled and kept clear |
| `resources` | `pos`, `kind`, `amount` | A resource node from the palette |
| `props` | `pos`, `yaw`, `kind` | A palette prefab, with its ground pad and access track |
| `rivers` | `points`, `width`, `depth`, `valleyWidth`, `meander`, `widthVariation`, `crossings` | A river with banks, floodplain and reeds |
| `ravines` | `points`, `width`, `depth`, `rim`, `branches`, `wooded` | A steep cut with side gullies |
| `hills` | `pos`, `radius`, `height`, `flatTop` | A smooth rise |
| `roads` | `points`, `kind` | A road of a library road class |
| `settlements` | `center`, `radius`, `kind`, `count` | Up to `count` plots along the roads within `radius` |
| `woods` | `points`, `kind` | Canopy, glades, shrubby edge, floor |
| `belts` | `points`, `kind` | Rows of trees and shrubs with gaps |
| `farmland` | `points`, `kind`, `angle` | Fields on a grid along `angle`. Empty `points` takes all free land |
| `areas` | `points`, `kind` | One surface painted by hand |
| `lines` | `points`, `kind`, `spacing` | A row of trees or props |
| `scatters` | `points`, `kind`, `count` | Props at random in a polygon |

Every feature except spawns and settlements also takes `shared`. A river crossing has `along` (0 to 1 down the river), `kind`
(`bridge` or `ford`), `width` and `prop`. Fords thin the water and streak it with foam; the climate's
`fordShallowsWidth` and `fordRapidsLength` set how far, and `crossingFoamLength` how far foam lines trail below
fords and bridges.

## How a map is built

### Land use

The land is a 1 m grid. Each cell is claimed once, by one region with a land kind, a surface and a row angle.
`MapLandPlanner` runs the land steps in priority order, and each takes only what earlier ones left:

1. Water, roads, river banks and floodplain.
2. Bases, prop pads and access tracks.
3. Ravines, settlements, spec tree rows and spec areas.
4. Woods, belts and farmland.
5. Meadow on everything left, then each surface's own lone trees and props.
6. Ground decals: each surface's decals per hectare, then one under every prop that names a `groundDecal`.

Land steps decide at points in the authored sector and place the result in every copy. That keeps maps exactly
symmetric. A surface prop that lands in the backdrop is drawn as its `backdropModel`, or left out without one.

**Farmland** splits free land into fields, picks a crop for each by the kit's weights and lays belts and tracks
along the field ends. A track end within the kit's `trackJoinDistance` of a road joins it. A track that still
leads nowhere is dropped. Within the kit's `headlandWidth` of where a field ends along its rows, the rows turn a
quarter to run along the end, as the plough turns there; a stretch too short to keep straight rows between its
headlands keeps them straight throughout.

**Settlements** fit plots along their streets: house, yard, outbuildings, garden, orchard, fences and street
furniture. The kit's `pathSurface` wears paths from the gate to the house and round it to the outbuildings and yard
extras. The report says why a plot didn't fit. Street pipes arch over driveways and crossings. A stretched
prefab with `StretchPartsAuthoring` keeps its end caps at their own size, and its remains need the component too.

**Access tracks.** Nearby props asking for the same road class share one track, within their
`sharedAccessDistance`.

### Refiners

Before the copies are made, `MapRefiner` reshapes the sector in memory; the file keeps the coarse lines. Rivers
meander but stay straight at their ends and crossings. Roads become curves. Ravines smooth and fork. A road end
that stops short of another road is joined onto it at 60° or more.

### Ground and rendering

Heights come from relief, hills, river valleys and ravines. Roads of a levelling class sit on embankments, with
ramps up to bridges.

Every surface sets an `edgeWidth`, the blend band in metres, and an `edgeWander`, how far the line wanders. Where
two surfaces meet the crisper one wins, so a ploughed edge stays sharp against a meadow. The wander turns with the
symmetry, so mirrored borders wander alike.

| Output | What it is |
| --- | --- |
| Ground textures | Terrain layer weights. Each surface's `mixes` paint other layers by symmetric noise (`layer`, `coverage` 0 to 1, `size` in metres). Steep ground takes the slope surface |
| Grass | A coverage slice per plant in `<Map>_GrassCoverage.asset` and a `GrassField` (`<Map>_Grass.asset`) naming the plants and the 16 m tiles that grow any. Clumped by noise; nothing grows on roads or placed objects |
| Ground tint | 2 texels per metre: light and warmth per field and wood, 20 to 150 m soil colour in light, warmth and moisture, darker hollows, wetness near water, lighter harvest swaths along the rows of surfaces with `swaths`, where plants also thin. Set by the climate's `groundTint` section |
| Row map | Each cell's crop row direction, turned with its symmetry copy |
| Ground materials | A `GroundMaterialMap` asset (`<Map>_GroundMaterials.asset`) holding one `SurfaceMaterial` byte per cell for impacts and wheel debris, baked into the `GroundMaterials` singleton |
| Ground colour | The ground's colour from above, so foliage blends in at its roots |
| Ground decals | A URP `DecalProjector` per decal, fading out on steep faces |

**Grass** is drawn by `GrassRenderer`, not terrain details. For each camera `Grass.compute` scatters every visible
tile's plants on a world-anchored lattice, one cell per plant at full coverage, so plants never swim as the camera
moves. Noise gathers them into clumps and tufts, and each plant takes a random model, size, yaw and lean, the ground's
height and tilt, and a tint from the ground picture round it. Plants thin out from 30 % of the draw distance to 30 %
at its end, the rest grow up to 1.5 times to cover, and thinned plants shrink before they go. Near plants, within the
grass shadow distance, cast shadows. Draws are `Graphics.RenderMeshIndirect`, one per plant model and band, on a copy
of the plant's material on `Hidden/HyperRTS/FoliageGrass`, the Foliage passes with an instanced path, so wind and
trampling work as on any Foliage plant. A camera rescatters only when it moves half a metre or the quality changes.
Grass and row crops share one scatter host, described under [row crops](modules/client.md#row-crops).
| Road ribbons | A mesh just above the ground, drawn by `HyperRTS/Road`, with faded free ends |
| Weather | A `WeatherSet` holding every climate weather, with the spec's weather active |

**Terrain material.** `HyperRTS/Terrain` blends up to 16 layers by texel height, gathered from the surfaces' own
layers, mixes and tramlines. A layer's `plants` thins scattered plants where it is painted. The climate's `heightBlendDepth`
sets how far one layer must stand above another to win; about 0.06 interlocks borders without speckle. Layers are
hex-tiled and randomly turned by their `antiTiling` to hide repeats. Layers with `followsRows` turn with the row
map, so furrows follow each field. Grass layers darken less in rain than bare ground.

Near the camera, a tileable soil grain from `TerrainMicroTexture` adds normal, occlusion and albedo detail at a 0.5 m
tile, fading out from 24 to 42 m. Rough ground takes it fully and low texels take most of its shadow. The layer of the
climate's `slopeSurface` is projected sideways on ground steeper than 30 degrees, fully from 42, so ravine walls don't
stretch. Within 38 m, parallax occlusion marches the most painted layer's height in 8 steps and shifts every layer by
the one offset, centred on mid height.

These strengths live on the climate's `terrain` base material. Each map's terrain material is a Material Variant of it
that `MapTerrainMaterial` writes, overriding only the layer arrays and the map's own textures. `TerrainDetail.Level`
takes a `GroundLevel` and sets the shader quality:

| Level | Keyword | Adds | Texture samples per pixel |
| --- | --- | --- | --- |
| Low | `_TERRAIN_SIMPLE` | One sample per layer texture | 8 + 3 per layer |
| Medium | `_TERRAIN_NO_FAR` | Hex tiling | 8 + 9 per layer |
| High | `_TERRAIN_NO_PARALLAX` | Far sample, micro detail, triplanar slope layer | 10 + 10 per layer, up to 6 more on steep slopes |
| Ultra | none | Parallax occlusion | High plus up to 9 within 38 m |

The fixed 8 are the four alphamaps, the row, ground tint and noise maps and the terrain normal. Edit mode sets no keyword,
so map captures show Ultra.

**Weather.** In play, `WeatherSet` swaps in the weather `MatchSetup.Weather` names. Empty keeps the default.

### Checks and measures

Checks run before anything is refined. `MapAssetCheck` covers the climate and palette, and `MapSpecCheck` covers
the spec. Every key must resolve, or the error names the key and the feature. Settings that only work in some
places, such as a prop's `besideRoad`, are rejected elsewhere. After the checks, lookups throw, so a missed key
fails loudly. Once props are placed, `MapPlacements.CheckEntities` makes every prop in the play area an entity: a
prefab without HyperRTS authoring is an error naming its key, since the simulation must see it to clear or crush it.
The backdrop draws `backdropModel`s instead.

The build then measures the map on the nav grid:

- **Roads.** Warnings for dead ends and bends tighter than half the class's curve radius.
- **Rush and paths.** Rush times between bases and the number of distinct attack paths, for a unit one cell
  wide that walks through scenery, as infantry do. Tanks crush trees, so they share those lanes.
- **Bases.** Reach, buildable ground and resources per base. Resources within 2.5 base radii count as the base's.
- **Renewable resources.** A renewable node among a base's resources is an error, since base fields must run
  dry. Across the map, stacks within 20 m of each other form one field. Fewer fields than the mode's
  `minRenewableFields` per player is an error.
- **Decals.** More than the rules' `maxDecals` is a warning.
- **Props.** The report lists props per hectare of each surface; a surface under its rules `propTargets` is a
  warning.

`MapHintWriter` turns the grid into bot hints: expansion order, attack lanes and defence points. The skirmish AI
needs them to play the map. `MapSpecRule` shows a built map's report errors on **HyperRTS ▸ Tools ▸ Validate**.

### Outputs

A preview writes `Logs/MapReports/<Map>.preview.png` and `.preview.json`. A build writes the report to
`Logs/MapReports/<Map>.json` and `.png`, and everything else to a `Generated/` folder beside the spec:

- the scene and its `_Entities` SubScene
- terrain, water and road assets
- the ground tint, row, ground colour, ground material, minimap and overview images
- row crop data, if the map has row crops
- baked lighting, and anything the game's `IMapBuildHook` adds

Generated objects sit under `[Generated]` roots and are replaced on every build. The SubScene's prefab instances
are also listed in `Generated/<Map>_BuildObjects.txt`, so a build still finds them if a prefab change drops their
name. Put hand-placed objects under `[Dressing]`, which the build never touches. Errors before the scene stage stop the build and leave the last good
scene alone.

Without a graphics device, a build skips lighting, minimap and views and keeps the last ones. A map's first build
needs graphics.

## Map library

The library holds the game's map content as JSON files, one per item, named `<Key>.<type>.json` anywhere under
`Assets/`. The key is the file name and is unique per type, so `Stubble` can be a layer, a plant and a surface. A
missing, duplicate or unreadable file throws, naming the file. So does any field the item doesn't read, at any
depth, and a file whose `version` is not 1. Formats stay at 1 until the first release: a change migrates every
file.

Each item holds placement data and links to its Unity assets. The assets are ordinary files edited in Unity,
never generated from the JSON. A link is `{ "path": "Assets/...", "guid": "..." }` and loads by GUID, so moving
or renaming an asset never breaks it. `MapLibraryRefSync` keeps the pair in step: saving a file fills in the GUIDs
of the paths it names, so to relink, edit the path, and moving an asset writes its new path into every file that
links it. An unused link is `{ "path": "", "guid": "" }`.

| Type | Holds |
| --- | --- |
| `layer` | A terrain layer, how the shader lays it, its map colour and grass cover |
| `plant` | A grass or small plant: `meshes` (FBX or mesh assets, picked per plant), a `HyperRTS/Foliage` `material`, `width` and `height` scale ranges, `healthy` and `dry` tints, `density` per square metre at full coverage and `clumping` 0 to 1 |
| `tree` | Terrain tree `prefabs`, one per variant, and optional entity `destructibles` in the same order |
| `crop` | A crop grown in 3D rows on fields whose surface names it |
| `decal` | A ground decal: material, size range, rotation, depth |
| `road` | A road class: width, surface, `impactClass`, verge, rank, levelling, curve radius |
| `surface` | A ground type: its layer, `impactClass`, and the trees, plants, props and decals on it |
| `farmland`, `belt`, `woodland` | Kits for fields, tree belts and woods |
| `settlement` | Plot layout along a street, street poles and pipe |
| `prop` | One placeable prefab: flattening, tilt, scale, ground pad, access road, ground decal |
| `climate` | The surfaces, road classes, kits, terrain and water materials and weathers one climate uses |
| `palette` | The sides, their start kits and build orders, base radius, match rig. One per project |
| `rules` | Thresholds per mode, `maxDecals`, match nav settings, view camera. One per project |

`impactClass` is a `SurfaceMaterial` name other than `None`. A palette side has a `side` id from 1 to 255, and
slot n uses `sides[(n - 1) % count]` by default.

A climate pulls in the layers, trees, plants and decals its surfaces and kits use, in a fixed order, so indices
change only when the data does. A climate holds at most 16 layers. Its terrain texture arrays are generated into a
`Generated` folder beside its file and rebuilt when a source texture or layer changes.

### Keys and asset fields

A string field that names another item carries `[MapKey(typeof(...))]`. A `MapAssetRef` field carries
`[MapAsset(typeof(...))]` with the asset type it links. `MapLibraryProblems.Find(root)` lists keys that don't resolve and assets that don't
load. Run it in a test on your library.

### Adding an item type

1. Add a class in `Data/` that implements `IMapLibraryItem`, plus `IMapKeyed` if other items name it.
2. Give it a `[MapLibraryItem("<type>", ...)]` attribute and add it to `MapLibrary`'s type list.
3. Hook it into builds through `MapClimateAssembly` or `MapLibrary.Palette`.
4. Check its keys in `MapAssetCheck` and list them in `MapKeyReference`.

## Batch and scripted entry points

| Entry | Does |
| --- | --- |
| `-executeMethod HyperRTS.Editor.Maps.Tools.MapCommandLine.Build -map <spec>` | Builds one map |
| `...MapCommandLine.Preview -map <spec>` | Previews one map |
| `MapPreview.Run`, `MapBuilder.Build`, `MapViewCapture.Capture` | The same from editor code |
| `MapBuildCommands.Build(spec)`, `.BuildAll()` | Builds as the editor does, saving and reopening scenes |
| `MapKeyReference.Write(path, MapLibrary.Load())` | Writes a Markdown list of every `kind` a spec may name |

Batch runs exit with 0 when the report passes, 1 on report errors, and 2 on a crash or a missing `-map`. Add
`-nographics` to build without lighting, minimap and views.

## What a game supplies

- **A map library**: the item files above and the Unity assets they name.
- **Specs**, each in its own folder.
- **An `IMapBuildHook`**, optionally. It runs after every build with the spec, report, minimap and overview, for
  example to list the map in the game's menus.

## Adding a kind of land use

1. Add a kit in `Data/`, like `MapFarmlandKit`, held by `MapClimate`. Give it a library type, check its keys in
   `MapAssetCheck` and list them in `MapKeyReference`.
2. Add a `MapLandKind` if none fits, and the surfaces it paints.
3. Add a `MapLandStep` in `Planning/` that claims land through `MapPlanContext`. Decide in the authored sector and
   place in every copy.
4. Slot the step into `MapLandPlanner.Plan` at its priority.
5. Add a spec class in `Spec/`, an array on `MapSpec`, key checks in `MapSpecCheck`, and a `MapFeatureKind` in
   `MapFeatureKinds` so the Map Editor shows it.
6. Write EditMode tests: the planner's output on a small layout, and a bad key failing loudly.
