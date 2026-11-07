# Map tools

[Docs](../README.md) ▸ [Maps](README.md) ▸ Map code: the code behind the map builder, Map Editor and map library, in
`Editor/Maps/`, its own assembly `HyperRTS.Editor.Maps` (tests in `Tests/`), under `HyperRTS.Editor.Maps.<Folder>`.
For how to use them, see [maps](README.md), [Map Editor](editor.md) and [how a map is built](build.md).

## Pipeline

`MapGeneration` runs a map through three stages, and each tool goes only as far as it needs:

1. **Lay**: check the spec and library, refine the authored sector, make the symmetry copies.
2. **Plan**: shape heights, run the land passes, place props, trees and decals. A preview stops here.
3. **Run**: level roads and paint the ground. `MapBuilder` then writes the terrain, meshes, SubScene and report.

Plan touches no Unity object, so `MapPreview` runs it on a worker thread. Every random choice is made once in the
authored sector and copied. `MapRandom` and `MapNoise` draw from fixed channels, so give a new feature a new channel,
since changing one changes every map. `MapIssues` collects errors, which fail a build, and warnings.

## Folders

| Folder | Holds |
| --- | --- |
| `Spec/` | `MapSpec` and one class per feature, each a `MapFeatureSpec` with `[MapFeature]` and `IMapPoint` or `IMapLine`. `MapSpecJson` reads and writes it |
| `Climate/`, `Kits/`, `Palette/` | Library items: the climate with its surfaces, layers, plants and trees; the belt, farmland, woodland, settlement and war kits; the palette with props and sides |
| `Library/` | `MapLibrary`, the `MapLibraryItem` base, `[MapKey]` and its dropdown, `MapClimateAssembly`, `MapLibraryRule` |
| `Checks/` | The asset, spec and key checks, `MapRoadChecks`, `MapSpecRule` and `MapRules`, the thresholds the report measures against |
| `Common/` | `MapIssues`, `MapRandom` and `MapParallel`, which runs a painter's rows on worker threads with output identical to a serial run |
| `Refine/`, `Layout/` | `MapRefiner`; `MapSymmetry` and `MapSymmetryCopier`, which copy the sector into a `MapLayout` |
| `Geometry/` | Rivers, roads and ravines in map space; `MapGrid` samples on cell corners or centres |
| `Heights/`, `Land/` | Terrain heights, road levelling and drainage; the 1 m land-use grid and its regions |
| `Placement/`, `Planning/` | Prefab placements and props; `MapLandPlanner` with its land passes, and `Farmland/`, `Settlements/`, `War/` for the larger ones |
| `Ground/`, `Vegetation/` | The ground painters (texture, tint, colour, materials) and the terrain material; grass, row crops and rows |
| `Meshes/`, `Scenes/` | Road ribbons and river meshes; `MapScene` and the writers for terrain, environment, placements, decals and lighting |
| `Nav/`, `Hints/` | `MapNavGrid` builds the runtime nav grid with the runtime's stamps, `MapPathfinder` runs `GridAStar` without its budget so rush times match in-game pathing; `MapHintWriter` writes bot hints |
| `Report/`, `Views/`, `Look/` | `MapMeasurer`, `MapReport`; match-camera screenshots, minimap and overview; `map look` captures |
| `Build/`, `Tools/` | `MapBuilder.Build`, `MapPreview.Run`, `MapPaths`, `IMapBuildHook`; `MapBuildRunner`, `MapCommandLine`, `MapCommands`, `MapKeyReference`, `MapSpecSchema` |
| `Designer/` | The Map Editor. `MapSpecDocument` holds the open spec for Undo, saves each edit through `JsonChanges` and reloads on import; the canvas tools (`IMapTool`) edit through `MapFeatureEdits` |

Each planted `MapTree` carries a seed hashed from its sector point, so every copy picks the same model variant.
Inside the playable area, trees with entity versions become placements; the rest are terrain trees.

## Extending

- **A new kind of land use**: a kit, a land pass and a spec class with `[MapFeature]`. Steps:
  [adding a kind of land use](build.md#adding-a-kind-of-land-use).
- **A new library item type**: [adding an item type](README.md#adding-an-item-type).
- **Game code after every build**: implement `IMapBuildHook`.
