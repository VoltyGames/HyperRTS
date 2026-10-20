# Map tools

[Modules](../modules.md) ▸ Map tools: the code behind the map builder, Map Editor and map library, in
`Editor/Maps/` under `HyperRTS.Editor.Maps.<Folder>`. For how to use them, see [maps](../maps.md).

## Pipeline

`MapGeneration` runs a map through three stages, and each tool goes only as far as it needs:

1. **Lay**: check the spec and library, refine the authored sector, make the symmetry copies.
2. **Plan**: shape heights, run the land passes, place props, trees and decals. A preview stops here.
3. **Run**: level roads and paint the ground. `MapBuilder` then writes the terrain, meshes, SubScene and report.

Plan touches no Unity object, so `MapPreview` runs it on a worker thread.

Every random choice is made once in the authored sector and copied, so maps are exactly symmetric. `MapRandom`
and `MapNoise` draw from fixed channels. Give a new feature a new channel, since changing one changes every map.
`MapIssues` collects errors, which fail a build, and warnings.

The folders below are in dependency order: a folder uses only those listed before it.

## Data in

| Folder | Holds |
| --- | --- |
| `Spec/` | `MapSpec` and one class per feature. `MapSpecJson` reads and writes it |
| `Json/` | `MapJsonFormat`, the layout specs and library files are saved in |
| `Data/` | Library item classes: climate, surfaces, trees, plants, row crops, kits, palette, rules |
| `Library/` | `MapLibrary`, `MapClimateAssembly`, the `[MapKey]` and `[MapAsset]` attributes, `MapLibraryProblems` |
| `Symmetry/` | `MapSymmetry`: the sector's copies and how they number slots |
| `Generation/` | `MapProject`, `MapPaths` and `MapGeneration` |

## Lay

| Folder | Holds |
| --- | --- |
| `Checks/` | `MapAssetCheck`, `MapSpecCheck`, `MapKeyCheck`, and `MapRoadChecks` after a build |
| `Refine/` | `MapRefiner`: river meanders, road curves and joins, ravine gullies |
| `Layout/` | `MapExpander` copies the sector into a `MapLayout` |
| `Geometry/` | `MapShapes` for rivers, roads, ravines and crossings in map space, plus curves and noise |

## Plan

| Folder | Holds |
| --- | --- |
| `Land/` | `MapLand`, the 1 m land-use grid, and its regions |
| `Placement/` | `MapPlacements` for prefabs to instantiate, `MapPropPlacer` for spawns, resources and props |
| `Ground/` | `MapHeightShaper` for terrain heights |
| `Planning/` | `MapLandPlanner` and the land passes |

Settlements use `MapPlotFinder` and `MapPlotBuilder` for plots and `MapStreetFurniture` for streets. Farmland uses
`MapFieldGrid` for fields and `MapFieldTrackLinks` to join tracks to roads.

Each planted `MapTree` carries a seed hashed from its sector point, so every copy picks the same model variant.
Inside the playable area, trees with entity versions become placements. The rest are terrain trees.

`MapCropPainter` bakes row crops at the end of Plan, so previews report them too.

## Run and build

| Folder | Holds |
| --- | --- |
| `Ground/` | `MapRoadLeveller` and the ground painters. `MapCropWriter` saves row crops |
| `Texturing/` | The `HyperRTS/Terrain` material and its layer texture arrays |
| `Meshes/` | Road ribbons and river meshes |
| `Scenes/` | `MapScene` and the writers for terrain, environment, placements, decals, lighting and minimap |
| `Nav/` | `MapNavGrid`, the runtime nav grid built in the editor, and path search |
| `Hints/` | `MapHintWriter`: bot hints from the nav grid |
| `Report/` | `MapMeasurer`, `MapReport` and the overview image |
| `Views/` | `MapViews`: match-camera screenshots |
| `Build/` | `MapBuilder.Build`, `MapPreview.Run` and `IMapBuildHook` |

## Editor UI

| Folder | Holds |
| --- | --- |
| `Designer/` | The read-only **HyperRTS ▸ Map Editor**. `MapSpecDocument` reloads the spec when its file changes |
| `Tools/` | The **Tools ▸ Maps** page, `MapBuildCommands`, `MapCommandLine`, `MapKeyReference`, `MapSpecRule` |

## Extending

- **A new kind of land use**: a kit in `Data/`, a land pass in `Planning/`, a spec class and a feature kind for
  the Map Editor. Steps: [maps](../maps.md#adding-a-kind-of-land-use).
- **Game code after every build**: implement `IMapBuildHook`.
