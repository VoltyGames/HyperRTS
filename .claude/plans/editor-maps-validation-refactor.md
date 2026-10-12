# Editor/Maps and Editor/Validation refactor

Review of `Editor/Maps` (~200 files, 11.8k lines) and `Editor/Validation` (36 files, 1k lines), and the plan that
follows from it. The refactor must keep generated maps byte-identical: every map preview (JSON minus the timestamp,
and PNG) and a full build's report, terrain, macro, ground and road assets are compared against a baseline after each
phase.

## Findings

### Statics

80 of 208 files are static classes. Static is right for stateless pure functions (`MapGeometry`, `MapCurves`,
`MapFillet`, `MapLandDistance`, the path searches, `QuickFixes`). It is wrong where a static carries state through
parameter lists:

- `MapSceneWriter` returns a `(Main, Entities, Root)` tuple that every writer takes apart again
  (`MapEnvironmentWriter.Write` has 8 parameters, `MapHintWriter.Write` 8).
- 17 land planners are `static Plan(MapPlanContext)` whose private helpers each take `context` plus the same kit,
  noise and random again (`Claim(context, kit, line, gaps, seed)`, `PlantAt(context, kit, path, along, offset, random)`).
- The settlement helpers (`MapPlotFinder`, `MapPlotClaims`, `MapPlotFences`, `MapStreetFurniture`, `MapStreets`)
  pass `(context, kit, entries, streets, center, radius, index, rejections)` between each other.
- `MapGeneration` is a data bag filled by two static factories with private setters.
- `AuthoringChecks` and `ProjectValidator` are two statics each holding discovered rule arrays.

### Validation scattered through the pipeline

Keys are checked in `MapAssetCheck`, `MapKitCheck`, then again where they are used: `MapRoadRefiner` and `MapShapes`
(road class, twice), `MapShapes` (crossing kind, bridge prop), `MapPlacer.Entry` (every palette lookup),
`MapPlanContext.Region` and `.Tree`, `MapBeltPlanner.Kit`, `MapFarmlandPlanner` (kit, track class),
`MapWoodPlanner`, `MapAccessPlanner` (with a dedupe set), `MapSettlementPlanner`, `MapSettlementEntries.CheckValues`
(repeats `MapKitCheck.Settlement`), `MapSplatPainter.Layer`. Some skip silently (`MapDetailPainter`) or fall back
(`MapOverviewImage` paints magenta). Each lookup site carries an `if (x == null) { error; continue; }` branch.

### Folders

- `Build/` holds five concerns in 24 files: pipeline, key checks, terrain textures and material, scene writers, meshes.
- `Layout/` mixes spec refinement (rivers, ravines, roads) with symmetry expansion; `MapIssues`, used everywhere, lives
  there.
- `Metrics/` mixes the nav grid and its searches with the measurer and the report's metric records.
- `Report/` holds screenshot capture (`MapViews`, `MapView`, `MapSubScenes`) and a plan-time check (`MapRoadChecks`).
- `Validation/Rules/` holds the rule inputs `PrefabSet` and `SceneSet`; `Validation/Rules/MapSpecRule` makes the generic
  validator depend on Maps.

### Duplication and smells

- `MapViewCapture` re-runs refine, expand and shapes by hand instead of the generation pipeline.
- Seeds: `MapPlacer.Random` is called as `Placement.MapPlacer.Random` from eight planners; noise and random channels are
  bare integers (0 to 33) spread over 20 files; `(float)random.NextDouble() * 2f - 1f` and scale lerps are repeated.
- `MapRavineWall` and the height shaper share a noise channel constant so their wobble matches; ravines are
  `(MapLine, MapRavineSpec)` tuples while rivers have a class.
- `MapPlacer` is both the placement store planners add to and the pass that places the spec's props.
- `MapRoadRibbon.Add(mesh, ...)` is a static that only fills a `MapRoadMesh`.
- `MapMenu.CurrentSpec` and `MapSpecRule` both derive a scene's spec path by hand.
- `RequiresGhostRule.WarnIfNotGhost` is a static other rules reach into; `RequiredAuthoringRule` and `RequiresGhostRule`
  each keep their own reflection cache; `WeaponRule` and `ProjectileGhostRule` both check the projectile;
  `GameEntityRule` and `DeathSpawnGhostRule` both check the death spawn.
- `MapDetailPainter` has two stacked `<summary>` blocks (the reeds doc sits on `Sown`).

## Plan

1. **Validation.** One `ProjectValidator` (component, scene, prefab rules). Every component rule derives from
   `AuthoringRule<T>` with a virtual `AppliesTo`; ghost check and attribute cache live in the base. One rule per
   authoring type. `PrefabSet`/`SceneSet` move to `Validation/`. `MapSpecRule` moves to `Maps/Tools/`.
2. **Folders.** `Refine/` (from Layout), `Checks/`, `Nav/` (from Metrics), `Terrain/`, `Scene/`, `Meshes/` (from Build),
   `Views/` (from Report and Tools), `Planning/Farmland/`, `Planning/Settlements/`; `MapIssues` to the Maps root.
3. **Check once, then trust.** `MapContentCheck` checks assets, kits, palette entries and every spec key before
   refining. Data lookups throw on unknown keys; the per-site error branches go.
4. **Seeds and shapes.** `MapRandom` names every channel and adds `System.Random` helpers (same formulas, same draw
   order). `MapRiver` and `MapRavine` shape classes; the ravine wobble lives in the shape.
5. **Planning.** `MapLandPass` base; planners become instances; `MapTreePlanting` takes the tree state out of the
   context; one `MapSettlement` per settlement replaces the helper parameter lists; `MapPlacements` (store) splits from
   `MapPropPlacer` (spec pass).
6. **Build.** `MapGeneration` is a staged pipeline that preview, build and view capture share; `MapScene` replaces the
   static scene writer and its tuple; the road ribbon becomes a `MapRoadMesh` method.
7. **Docs.** `docs/editor.md`; the game's map skill references.
