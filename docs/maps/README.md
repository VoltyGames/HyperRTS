# Maps

A map starts as a JSON spec, `<Map>.map.json`, that describes land use: farmland, settlements, woods, rivers,
ravines and roads. The builder lays the land out by rule, keeps it exactly symmetric and turns it into a scene, a
SubScene and a report. You say what goes where; the builder makes it look right.

This page covers the workflow, the spec format and the map library. The [Map Editor](editor.md) edits specs,
[how a map is built](build.md) covers the passes, ground rendering, checks and outputs, and
[map tools](code.md) tours the code (`Editor/Maps/`, with runtime rendering in
`Presentation/Environment/`).

## Overview

Edit the spec, preview it in seconds, then build it. A build takes minutes and also captures screenshots from the
match camera.

| Stage | How to run it | Writes |
| --- | --- | --- |
| Edit | **HyperRTS ▸ Map Editor** or any text editor | The spec |
| Preview | The Map Editor, the `map preview` [command](../editor/README.md#commands) or batch `.Preview` | Layout image, issues and land figures |
| Build | **Build** in the Map Editor, `map build` or batch `.Build` | Scene, SubScene, assets, report, minimap, views |
| Views | Every build with graphics, **Recapture views**, or `map capture` | Screenshots in `Logs/MapReports/<Map>/`, and their contrast in the report |
| Looks | `map look`, in Play mode | The spec's `looks` at three zooms in every weather with GI settled, in `Logs/MapReports/<Map>/look/<Weather>/` |

A preview needs no scene. It does not measure rush times or attack paths; only a build does. The views' contrast is
`viewLuminanceSpread` and `viewLocalContrast` in the report, averaged over the ground views; low figures mean flat,
front-lit ground.

## Spec format

The spec is flat: one array per feature type, points as `{ "x", "z" }` objects. Units are metres. The origin is the
map centre, X points east, Z points north, and angles run clockwise from north. Files are saved in one fixed layout
(`JsonFormat`) so they diff cleanly, and a field the spec doesn't read fails the load with its path, such as
`hills[1].raduis`. `MapSpecSchema` writes a JSON Schema for editors.

| Header field | Holds |
| --- | --- |
| `name` | The map name. The file must be named `<name>.map.json` |
| `displayName` | The name the game shows |
| `mode` | `1v1` to `4v4`, or `ffa`. The spawns times the symmetry's copies must fit |
| `mapType`, `status`, `notes` | `land` (`coastal` and `island` are named but not built); a free work-state label; dated notes |
| `climate`, `weather` | A library climate key, and one of its weathers. The scene is built and lit with that weather |
| `warAge` | `None` (default), `Skirmished`, `Contested` or `Frontline`: how scarred the land is before the battle |
| `size` | Playable width (X) and depth (Z) |
| `symmetry` | See [symmetry](#symmetry) |
| `seed` | Seeds every random stream and noise field |
| `terrain` | `height`, `relief` and `reliefScale` (swell amplitude and wavelength), `waterLevel`, `maxHeight`, `resolution` (2^n + 1, larger for bigger maps), `backdropWidth`, `horizonWidth` |

Base `height` is lowered to world y = 0. The backdrop is unplayable land past the playable edge; gameplay points
(spawns, resources, props, hills, settlements) stay inside half the `size`, while lines and polygons may reach into
the backdrop. `horizonWidth` is the farmland past the terrain edge that a tilted camera's horizon sees; a `Horizon`
builds it in memory when the scene loads, and 0 turns it off.

### Symmetry

You author **one sector**; the builder copies it.

| `symmetry` | Author | Copies |
| --- | --- | --- |
| `rotate-180` | The half with z at or below 0 | Rotated 180 degrees about the centre |
| `mirror-x` | The half with x at or below 0 | Mirrored across x = 0 |
| `mirror-z` | The half with z at or below 0 | Mirrored across z = 0 |
| `rotate-N` (3 to 8) | The wedge from +X counter-clockwise by 360 / N degrees | N - 1 rotations |

- Team modes need a two-copy symmetry (`rotate-180`, `mirror-x`, `mirror-z`). `ffa` needs `rotate-3` to `rotate-8`,
  one sector per player.
- `"shared": true` places a feature once; it must map onto itself (on the centre or the axis).
- Everything else stays inside the sector. A road or river crossing the axis is authored as a piece that ends on it,
  and its copy continues it.
- Random choices (crops, trees, plots) are made in the sector and copied, so copies match exactly. Farmland fields
  never straddle the seam: it becomes a field edge.
- Slots interleave: sector spawn i, copy k is slot `i * copies + k + 1`, so slots 1 and 2 face each other. List
  sector spawns best first. Team maps: copy k is team k + 1. FFA: one team per slot.
- Each feature's random stream is seeded by the map seed and its index in its list, so deleting or reordering a
  feature reshuffles the detail of later features of its kind.
- Points sit on a 0.5 m grid.

### Features

All arrays are optional and authored for the sector only. Land is claimed in the order of
[land use](build.md#land-use). Every feature except spawns also takes `shared`.

| Feature | Fields | Builds |
| --- | --- | --- |
| `spawns` | `pos`, `yaw` | A slot's start position: the base radius is levelled and kept clear, with a worn yard painted as base ground |
| `resources` | `pos`, `kind`, `amount` (above 0) | A resource node from the palette. A renewable kind regrows once tapped |
| `props` | `pos`, `yaw`, `kind` | A palette prefab, with its ground pad and access track |
| `rivers` | `points`, `width`, `depth`, `valleyWidth`, `meander`, `widthVariation`, `crossings` | A meandering channel with banks, floodplain meadow, reeds and a flowing water mesh |
| `ponds` | `points` (polygon), `depth`, `bankWidth`, `shore`, `shoreWidth`, `dam`, `damWidth`, `damHeight`, `damSurface` | Still water in a basin with a wet margin and reeds. `dam` is the index of the point whose edge to the next is an earth dam (at most 6 m high); -1 for none. Fails when it reaches a river, a road or a base |
| `ravines` | `points`, `width`, `depth`, `rim`, `branches`, `wooded` | A steep cut with side gullies: walls that block vehicles, a grassy rim and woods on `wooded` of the floor |
| `hills` | `pos`, `radius`, `height`, `flatTop` | A smooth rise. Keep its slopes under the rules' max slope |
| `roads` | `points`, `kind` | A road of a library road class, rounded into curves, joined to the roads it meets |
| `settlements` | `center`, `radius`, `kind`, `count` | Up to `count` plots along the roads within `radius` |
| `woods` | `points` (polygon), `kind` | Canopy, glades, shrubby edge, floor |
| `belts` | `points` (line), `kind` | Rows of trees and shrubs with gaps |
| `farmland` | `points` (polygon), `kind`, `angle` | Fields on a grid along `angle`, the way the rows run. Empty `points` takes all free land |
| `areas` | `points` (polygon), `kind` | One surface painted by hand |
| `rows` | `points`, `kind`, `spacing` | A row every `spacing` metres of a climate tree or a palette prop |
| `scatters` | `points` (polygon), `kind`, `count` | Palette props at random in the polygon. `besideRoad` entries move onto the nearest road's verge, so the polygon must reach a road |
| `looks` | `name`, `pos`, `yaw`, `fog` | Not a feature: places `map look` frames. Not mirrored. `fog` keeps the fog of war on |

A river crossing has `along` (0 to 1 down the authored river), `kind` (`bridge` or `ford`), `width` and `prop`: a
bridge's prefab carries the walkable deck, a ford's bed sits just under the water.

Surfaces grow their own lone trees and props by rule, so use `scatters` and `rows` only for what no rule makes.

## Map library

The library holds the game's map content as ScriptableObject assets, one per item, named `<Key>.<type>.asset`
anywhere under `Assets/`, such as `Stubble.surface.asset`. The key is unique per type, so `Stubble` can be a layer, a
plant and a surface. **Assets ▸ Create ▸ HyperRTS ▸ Map Library** makes each type; rename the new asset to its key.

Items link their Unity assets as ordinary object fields, so Unity tracks moves and renames. A string field that names
another item shows a dropdown of that type's keys. `MapLibrary.Load` indexes the project's items, and each item a
build asks for loads as a copy the library owns, so a build fills derived fields without touching the asset. A
misnamed asset or a duplicate key throws, naming both assets.

Edit items in the Inspector, or from a script or command through `SerializedObject`. Never edit their YAML by hand:
Unity drops a field it doesn't know without a word.

| Type | Holds |
| --- | --- |
| `layer` | A terrain layer, how the shader lays it, its map colour and grass cover |
| `plant` | A grass or small plant: meshes, a `HyperRTS/Foliage` material, size ranges, tints, `density`, `clumping` and `reach`, the metres it draws to |
| `tree` | Terrain tree `prefabs`, one per variant, and optional entity `destructibles` in the same order |
| `crop` | A crop grown in 3D rows on fields whose surface names it |
| `decal` | A ground decal: material, size range, rotation, depth |
| `road` | A road class: width, surface, `impactClass`, verge, rank, levelling, curve radius |
| `surface` | A ground type: its layer, `impactClass`, and the trees, plants, props and decals on it |
| `farmland`, `belt`, `woodland` | Kits for fields, tree belts and woods |
| `war` | Old scars per war age: crater, burn, wreck and trench densities, weeds, shredded trees, props |
| `settlement` | Plot layout along a street, street poles and pipe |
| `prop` | One placeable prefab: flattening, tilt, scale, ground pad, access road, ground decal |
| `climate` | The surfaces, road classes, kits, terrain and water materials and weathers one climate uses |
| `palette` | The sides, their start kits and build orders, base radius, match rig. One per project |
| `rules` | Thresholds per mode, `maxDecals`, match nav settings, view camera. One per project |

`impactClass` is a `SurfaceMaterial` name other than `None`. A palette side has a `side` id from 1 to 255, and slot n
uses `sides[(n - 1) % count]` by default. A climate pulls in the layers, trees, plants and decals its surfaces and
kits use, and holds at most 16 layers; its terrain texture arrays are generated into a `Generated` folder beside it
and rebuilt when a source texture or layer changes.

### Keys and checks

A string field that names another item carries `[MapKey(typeof(...))]`. `MapLibraryRule`, a
[validation rule](../editor/README.md#validation), reports a misnamed asset, a duplicate key, an item whose load check fails, a
key that names no item and a link to a deleted asset, on the Validate page and from `project validate`.
`MapKeyReference.Write` lists every `kind` a spec may name.

### Adding an item type

1. Add a class in `Climate/`, `Palette/` or `Checks/` that derives from `MapLibraryItem`, with
   `[MapLibraryItem("<type>")]` and a `[CreateAssetMenu]` under `HyperRTSMenu.MapLibrary`, and add it to
   `MapLibrary`'s type list. Override `Load` to fill derived fields.
2. Hook it into builds through `MapClimateAssembly` or `MapLibrary.Palette`.
3. Check its keys in `MapAssetCheck` and list them in `MapKeyReference`.

## Batch and scripted entry points

| Entry | Does |
| --- | --- |
| `-executeMethod HyperRTS.Editor.Maps.Tools.MapCommandLine.Build -map <spec>` | Builds one map |
| `...MapCommandLine.Preview -map <spec>` | Previews one map |
| `map preview`, `map build` (every map when none is named), `map capture`, `map look` | The same with the Editor open, as [commands](../editor/README.md#commands) |
| `MapPreview.Run`, `MapBuilder.Build`, `MapViewCapture.Capture` | The same from editor code |
| `MapBuildRunner.Build(spec)`, `.BuildAll()` | Builds as the editor does, saving and reopening scenes; with `ask: false` unsaved scenes throw instead |
| `MapKeyReference.Write(path, MapLibrary.Load())` | A Markdown list of every `kind` a spec may name |
| `MapSpecSchema.Write(path, MapLibrary.Load())` | The JSON Schema of `.map.json`; `[MapKey]` fields list the library's keys |
| `MapPaths.Find(map)` | The spec for a map name or spec path, as the commands take it |

Batch runs exit with 0 when the report passes, 1 on report errors, and 2 on a crash or a missing `-map`. Add
`-nographics` to build without lighting, minimap and views.

## What a game supplies

- **A map library**: the item assets above and the Unity assets they link.
- **Specs**, each in its own folder.
- **An `IMapBuildHook`**, optionally. It runs after every build with the spec, report, minimap and overview, for
  example to list the map in the game's menus.
