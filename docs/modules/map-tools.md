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

The folders below follow the pipeline, from the data a build reads to what it writes.

## Data in

| Folder | Holds |
| --- | --- |
| `Spec/` | `MapSpec` and one class per feature. `MapSpecJson` reads and writes it in `JsonFormat` (`Editor/Common/`), the layout specs and effect recipes are saved in |
| `Climate/` | Climate library items: the climate, its surfaces, layers, trees, plants, row crops, road classes, decals and its belt, farmland, woodland and war kits |
| `Palette/` | The palette: props, sides and their start kits, settlement kits |
| `Rules/` | The rules: thresholds per mode and the props a surface should carry |
| `Library/` | `MapLibrary`, the `MapLibraryItem` base, `MapKeys`, `MapClimateAssembly`, the `[MapKey]` attribute and its dropdown, `MapLibraryRule` |

## Lay

| Folder | Holds |
| --- | --- |
| `Checks/` | `MapAssetCheck`, `MapSpecCheck`, `MapKeyCheck`, and `MapRoadChecks` after a build |
| `Refine/` | `MapRefiner`: river meanders, road curves and joins, ravine gullies |
| `Layout/` | `MapSymmetry`, the sector's copies and how they number slots; `MapExpander` copies the sector into a `MapLayout` |
| `Geometry/` | `MapShapes` for rivers, roads, ravines and crossings in map space, plus curves and noise |

## Plan

| Folder | Holds |
| --- | --- |
| `Heights/` | `MapHeights`, `MapHeightShaper` for terrain heights, `MapRoadLeveller` and `MapFlow` for drainage |
| `Land/` | `MapLand`, the 1 m land-use grid, its regions and `MapLandDistance` |
| `Placement/` | `MapPlacements` for prefabs to instantiate, `MapPropPlacer` for spawns, resources and props |
| `Planning/` | `MapLandPlanner` and the land passes (`MapLandPass`), `MapTreePlanting`; `Farmland/`, `Settlements/` and `War/` for the larger passes |

Settlements use `MapPlotFinder` and `MapPlotBuilder` for plots and `MapStreetFurniture` for streets. Farmland uses
`MapFieldGrid` for fields and `MapFieldTrackLinks` to join tracks to roads.

Each planted `MapTree` carries a seed hashed from its sector point, so every copy picks the same model variant.
Inside the playable area, trees with entity versions become placements. The rest are terrain trees.

`MapCropPainter` bakes row crops at the end of Plan, so previews report them too.

## Run and build

| Folder | Holds |
| --- | --- |
| `Ground/` | The ground painters: texture, tint, colour, grass, row crops and rows. `MapCropWriter` and `MapGrassWriter` save them |
| `Texturing/` | The `HyperRTS/Terrain` material and its layer texture arrays |
| `Meshes/` | Road ribbons and river meshes |
| `Scenes/` | `MapScene` and the writers for terrain, environment, placements, decals and lighting |
| `Nav/` | `MapNavGrid` builds the runtime nav grid with the runtime's stamps (`NavGrid.StampArea`). `MapPathfinder` runs the runtime `GridAStar` without its budget, so rush times and attack paths match in-game pathing |
| `Hints/` | `MapHintWriter`: bot hints from the nav grid |
| `Report/` | `MapMeasurer`, `MapReport` and its image (`MapReportImage`) |
| `Views/` | `MapViews`: match-camera screenshots and their contrast, the minimap and the overview picture |
| `Build/` | `MapProject`, `MapPaths`, `MapGeneration`, `MapBuilder.Build`, `MapPreview.Run` and `IMapBuildHook` |

## Editor UI

| Folder | Holds |
| --- | --- |
| `Designer/` | **HyperRTS ▸ Map Editor**. `MapSpecDocument` (a `JsonDocument`) holds the open spec in a hidden ScriptableObject for Undo and property fields, saves each edit through `JsonChanges` and reloads on import; the canvas tools (`IMapTool`) edit through `MapFeatureEdits` |
| `Tools/` | The **Tools ▸ Maps** page, `MapBuildRunner`, `MapCommandLine`, `MapKeyReference`, `MapSpecRule` |

## Extending

- **A new kind of land use**: a kit in `Climate/`, a land pass in `Planning/`, a spec class and a feature kind for
  the Map Editor. Steps: [maps](../maps.md#adding-a-kind-of-land-use).
- **Game code after every build**: implement `IMapBuildHook`.
