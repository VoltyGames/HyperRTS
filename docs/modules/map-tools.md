# Map tools

[Modules](../modules.md) ▸ Map tools: the map builder, Map Editor and map library in `Editor/Maps/`
(`HyperRTS.Editor.Maps.<Folder>`). How to use them: [maps](../maps.md).

## Pipeline

```text
MapProject (spec + library) ─▶ Lay ─────────────▶ Plan ─────────────────────▶ Run ──────────────▶ MapBuilder
                                checks, refine,     heights, land passes,       roads levelled,      terrain, meshes,
                                symmetry copies     placements, trees, decals   ground painted       SubScene, nav,
                                                    (MapPreview stops here)                          report, views
```

`MapGeneration` runs the stages; each tool goes as far as it needs. `Plan` touches no Unity object, so
`MapPreview` runs it on a worker thread. Every random choice is made once in the authored sector and
copied by the symmetry, so maps are exactly symmetric.

A folder uses only the folders listed before it.

## Data in

| Folder | Holds |
| --- | --- |
| `Spec/` | The map as authored (`MapSpec`, one sector) and its feature classes: terrain, hills, rivers and crossings, roads, ravines, woods, belts, farmland, settlements, areas, lines, props, scatters, resources, spawns. `MapSpecJson` reads and writes it |
| `Json/` | `MapJsonFormat`: the one layout specs and library files are saved in |
| `Data/` | Library item classes: `MapClimate` (layers, surfaces, trees, plants, decals, road classes, farmland / belt / woodland kits), `MapPalette` (what each spec `kind` places, start kit, settlement kits), `MapRules` (thresholds per mode, match settings), `MapMaterial`. `MapKeys` looks keys up after the checks |
| `Library/` | `MapLibrary`: one `<Key>.<type>.json` per item anywhere under the root; `MapClimateAssembly` builds a climate from its file; load problems, usages, `[MapKey]` / `[MapAsset]` fields and their drawers, and the **Tools ▸ Library** page |
| `Symmetry/` | `MapSymmetry`: the copies of the sector (rotations or mirrors) and how they number slots |
| `Factories/` | `IMapAssetFactory`: makes a library item's Unity assets from its JSON (`photoMaterial`, `colorMaterial`, `terrainLayer`; games add their own). `MapAssetPostprocessor` reruns them when an item file imports |
| `Generation/` | `MapProject`, `MapPaths` (generated files beside the spec), `MapGeneration` |

`MapIssues` collects errors (they fail a build) and warnings, each optionally naming its spec feature;
`MapRandom` and `MapNoise` draw from fixed channels, so changing a channel changes every map.

## Lay

| Folder | Holds |
| --- | --- |
| `Checks/` | `MapAssetCheck` (every asset and key the climate and palette name exists), `MapSpecCheck` (sizes, amounts, kinds), `MapKeyCheck`; `MapRoadChecks` warns about dead ends and tight bends after a build |
| `Refine/` | `MapRefiner` shapes the sector's coarse lines before copying: meandering rivers, filleted roads that join each other (`MapRoadJoiner`), ravines with side gullies |
| `Layout/` | `MapExpander` copies the sector into `MapLayout` (map space, spawns with slots and teams) and rejects features outside it |
| `Geometry/` | `MapShapes` (rivers, roads, ravines, crossings in map space, shared by every pass), curves and fillets, `MapPathWalker`, noise |

## Plan

| Folder | Holds |
| --- | --- |
| `Land/` | `MapLand`: land use on a 1 m grid, each cell owned by the first region that claimed it (`MapLandRegion`, `MapLandKind`); `MapLandDistance` for edge bands |
| `Placement/` | `MapPlacements` (palette prefabs to instantiate, each reserving its footprint), `MapClearance`, `MapPropPlacer` (start kits, resources, props, rows, scatters), `MapDecal` |
| `Ground/` (shape) | `MapHeightShaper` (relief, hills, valleys, fords, ravines), `MapHeights`, `MapTreeSpacing` |
| `Planning/` | `MapLandPlanner` runs the `MapLandPass`es in priority order, each claiming only what earlier ones left |

Land passes, in order: water, roads (with verges), river banks, bases and pads, access tracks, ravines,
settlements, tree and prop rows, hand-drawn areas, woods, belts, farmland, meadow, then surface planting and
decals. Settlements find plots along their streets (`MapPlotFinder`), fill them (`MapPlotBuilder`: house, yard,
fences, garden, orchard) and line the streets (`MapStreetFurniture`). Farmland splits leftover land into fields on
a grid (`MapFieldGrid`), gives each a crop and links field tracks into the road net (`MapFieldTrackLinks`).

## Run and build

| Folder | Holds |
| --- | --- |
| `Ground/` (paint) | `MapRoadLeveller`, `MapSplatPainter` (terrain layers with soft borders), `MapDetailPainter` (grass and crops), `MapMacroPainter` (field tint, wetness) |
| `Texturing/` | The `HyperRTS/Terrain` material: layer texture arrays (`MapTerrainTextures`), per-layer params, ground colour for foliage |
| `Meshes/` | Road ribbons (`MapRoadMeshWriter`, faded ends) and river ribbons (`MapWaterMesh`, ford colours from `MapWaterFords`) |
| `Scenes/` | `MapScene` (scene and SubScene, cleared of the last build), terrain, environment, placements (`MapPlacementWriter`, posed on the ground by `MapPlacementPose`), decals, lighting, minimap, overview picture |
| `Nav/` | `MapNavGrid` (the runtime's grid, built in the editor), `MapPathfinder`, `MapWalkField`, `MapPathCounter` (distinct attack paths) |
| `Hints/` | `MapHintWriter`: bot hints from the nav grid (expansion order, attack lanes, defence points) |
| `Report/` | `MapMeasurer` (rush times, paths, reachability, buildable ground, resources), `MapReport`, `MapOverviewImage` |
| `Views/` | `MapViews`: standard match-camera screenshots (bases, crossings, settlements, overview) |
| `Build/` | `MapBuilder.Build`, `MapPreview.Run`, `IMapBuildHook` (game code after every build) |

## Editor UI

| Folder | Holds |
| --- | --- |
| `Designer/` | **HyperRTS ▸ Map Editor**: `MapSpecDocument` (undoable edits), canvas with pan, zoom and symmetry ghosts, Select / Point / Line / Area tools, feature list and inspector, live preview, `NewMapDialog` |
| `Tools/` | **Tools ▸ Maps** page, `MapBuildCommands`, `MapCommandLine` (batch `.Build` / `.Preview`), `MapKeyReference`, the `MapSpecRule` validation rule |

## Extending

- **A kind of land use**: a kit in `Data/` held by `MapClimate`, a `MapLandPass` in `Planning/` added to
  `MapLandPlanner`, a spec class and a `MapFeatureKind` for the Map Editor. Steps: [maps](../maps.md#adding-a-kind-of-land-use).
- **An asset factory**: implement `IMapAssetFactory` (`Kind` is the item's `factory` value) in game editor code;
  `MapLibraryAssets.Factories` finds it by type discovery.
- **After a build**: implement `IMapBuildHook`.
