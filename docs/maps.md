# Maps

A map is a JSON spec (`<Map>.map.json`) that describes land use: farmland, settlements, woods, belts, rivers,
ravines, roads. The builder lays the land out by rule, then turns it into a scene, a SubScene and a report. The
look of the land (terrain layers, plants, trees, kits, props) comes from the game's map library. Source:
`Editor/Maps/`; rendering: `Presentation/Environment/`.

## Overview

```text
spec ─▶ Preview ─────────────▶ Build ─────────────────────────────────▶ Views
        seconds, no scene       minutes: scene, SubScene, report         match-camera screenshots
        .preview.png / .json    terrain, roads, water, minimap, hints    <Map>/*.png
```

| Stage | Entry | Writes |
| --- | --- | --- |
| Edit | **HyperRTS ▸ Map Editor**, or any editor or agent writing the JSON | The spec |
| Preview | Live in the Map Editor (memory only); `MapPreview.Run(spec)` or batch `.Preview` (to disk) | Layout image, issues, land figures. No rush times or paths |
| Build | **Build** in the Map Editor or on **HyperRTS ▸ Tools ▸ Maps**; `MapBuilder.Build(spec)`; batch `.Build` | Scene and SubScene, assets, report, minimap, views |
| Views | Every build with graphics; **Recapture views** in the Map Editor; `MapViewCapture.Capture(spec)` | `Logs/MapReports/<Map>/*.png` |

Code reference (folders, pipeline stages, land passes): [map tools](modules/map-tools.md).

## Map Editor

**HyperRTS ▸ Map Editor** edits one spec's authored sector on a top-down canvas. Double-clicking a `.map.json` in
the Project window, **Open** on **HyperRTS ▸ Tools ▸ Maps** and **New map...** open a spec in it.

| Part | Holds |
| --- | --- |
| Toolbar | Spec picker, the tools (Select, Point, Line, Area) and the feature kind the drawing tool adds, Save, Revert, **Ghosts**, **Auto preview**, **Preview**, **Build** |
| Feature list (left) | Every feature grouped by kind with counts; click one to select it |
| Canvas (centre) | The newest preview image under a metre grid, the playable bounds and backdrop edge, the sector edges, every feature, the symmetry ghosts and the selection's handles |
| Inspector (right) | The selected feature's fields, bound to the spec. Key fields are dropdowns of the library's keys |
| Report panel (under the inspector) | The newest preview's or build's issues, the land figures, the last build's metrics, **Open scene** and **Recapture views** |

The editor changes features only. The header (name, mode, size, symmetry, climate, seed, terrain) is edited in the
JSON or set by **New map...**.

### Tools and shortcuts

Shortcuts work while the canvas has focus. Ctrl, Cmd or Alt with a tool key does not switch tools.

| Input | Does |
| --- | --- |
| V | Select tool: click selects, drag moves the feature, drag a handle moves that vertex or a circle's radius |
| P | Point tool: each click adds the picked point or circle kind (spawn, resource, prop, hill, settlement) |
| L | Line tool: click adds vertices; Enter or double-click finishes (2+ points) |
| A | Area tool: click adds vertices; Enter or double-click finishes (3+ points) |
| Escape | Drops the line or area being drawn; with nothing in progress, back to Select |
| Alt+click on the selected line or area's edge | Adds a vertex there and drags it |
| Ctrl+click (Cmd+click) on a vertex | Removes it, as long as a line keeps 2 points and an area 3 |
| Delete, Backspace | Deletes the selected feature |
| Middle drag, Space+drag | Pan |
| Wheel | Zoom at the cursor |
| F | Fit the map and its margin |

A new feature copies the last one of its kind (kind, amount, width, `shared`...) or takes the class defaults when
the list is empty. Each gesture (a drag, a finished line, a deleted vertex) is one undo step.

### Editing rules

- **One sector is authored.** The symmetry copies it into the others. Points outside the authored sector get a
  ring in the warning colour: they would overlap their own copies. **Ghosts** shows the copies, faded.
- **Shared features sit on the axis.** A feature with `"shared": true` is placed once, not copied. The editor
  snaps a shared point or circle onto the mirror axis, or the centre for a rotation, so it stays symmetric.
- **Array order is the random stream.** Each feature draws from a stream seeded by the map seed, its channel and
  its index in its list (`MapRandom.Stream`). Deleting or reordering a feature renumbers the later ones of its
  kind and reshuffles their random detail; the inspector says so on every feature that has later ones.
- **Points snap to 0.5 m** (`MapSnap`), the grid specs are written in.

### Live preview and report

With **Auto preview** on (remembered per user), the editor previews the spec as it is in memory, unsaved edits
included: 0.75 s after an edit is committed (pointer up, field changed) it runs the checks on the main thread,
plans on a worker thread (`MapPreview.RunAsync`, `MapGeneration.PlanChecked`) and swaps the canvas image.
**Preview** runs one now. Nothing is written to `Logs/MapReports/`. The canvas also reloads
`<Map>.preview.png` when a preview run elsewhere (an agent, batch mode) rewrites it.

The report panel shows the newer of the last preview and the last build. An issue that names a feature (the
report's `errorFeatures` / `warningFeatures`, index for index with `errors` / `warnings`) has **Show**, which
selects and frames it. Land figures are the land-use shares and counts; metrics (rush, attack paths, path search,
buildable share and resources per base) come from the last build.

### Build

**Build** saves the spec, asks to save open scenes, builds the map (`MapBuildCommands.Build`), reopens the scene
that was open and shows the build's report.

### New map

**New map...** on **HyperRTS ▸ Tools ▸ Maps** asks for name (PascalCase, unique), mode (`1v1` to `4v4`, `ffa`),
size, symmetry, climate (from the library) and seed, then a folder under `Assets`. It writes
`<folder>/<Name>/<Name>.map.json` from `MapSpecTemplate`: status `blockout`, gentle relief with a margin of 30 % of
the size, and the spawns of one sector facing the centre. Team modes need a two-copy symmetry (`rotate-180`,
`mirror-x`, `mirror-z`: one sector per team); `ffa` needs `rotate-3` to `rotate-8` (one sector per player).

### Saving and files changed on disk

Edits go through a hidden `MapSpecDocument` and its `SerializedObject`, so Undo and Redo work. **Save** writes the
spec with `MapSpecJson` in the `MapJsonFormat` layout (Newtonsoft-parsed): indented by two spaces, points and short
scalar arrays on one line, floats in their shortest form (`0.35`, `20`), so saves diff cleanly. The document is
dirty while the spec writes differently from the file; the window asks to save on close or when another spec is
picked.

Once a second the editor checks the file's write time. When the file changed on disk (an agent or a text editor
wrote it) and the editor has no unsaved edits, it reloads the spec and drops the undo history. With unsaved edits
it keeps them; **Revert** reloads the file.

## Spec format

Unity's `JsonUtility` reads it, so it is flat: one array per feature, points as `{ "x", "z" }` objects. Metres,
origin at the map centre, X east, Z north, angles clockwise from north.

| Header field | Holds |
| --- | --- |
| `name`, `displayName` | The map name, which the spec's file name must match (`<name>.map.json`); the name the game shows |
| `mode`, `mapType`, `status`, `notes` | `1v1` to `4v4` or `ffa`; `land`; work state; dated notes |
| `climate` | A library climate key |
| `size` | Playable width (X) and depth (Z) |
| `symmetry` | `rotate-180`, `mirror-x`, `mirror-z`, `rotate-3` to `rotate-8` |
| `seed` | Seeds every random stream and noise field |
| `terrain` | `height` (open ground, lowered to world y = 0), `relief` and `reliefScale` (swell), `waterLevel`, `maxHeight`, `resolution`, `backdropWidth` (the backdrop: unplayable land past the playable edge) |

The spec authors **one symmetry sector**; the builder copies it (slots interleave so slots 1 and 2 always face
each other). `"shared": true` places a feature once, on the axis or centre. Random choices are made in the sector
and copied, so copies match. Each kind of choice draws from its own seeded stream (`MapRandomChannel`) and noise
field (`MapNoiseChannel`): a new feature takes a new channel, since changing a channel's value changes every map.

| Feature | Fields | Builds |
| --- | --- | --- |
| `spawns` | `pos`, `yaw` | The palette's start kit; the base is levelled and kept clear |
| `resources` | `pos`, `kind`, `amount`, `shared` | The palette prefab `kind` (a resource node) holding `amount`; random facing |
| `props` | `pos`, `yaw`, `kind`, `shared` | A palette prefab; one with a pad gets ground round it, one with access a track to the nearest road |
| `rivers` | `points`, `width`, `depth`, `valleyWidth`, `meander`, `widthVariation`, `crossings`, `shared` | Meandering channel of varying width, banks, floodplain, reeds, water mesh; each crossing (`t`, `kind` bridge or ford, `width`, `prop`) sits at `t` along the river; fords thin the water and streak it with foam (vertex A carries how shallow the bed is, the water material's `_FordAlpha` is the alpha mid-ford; the climate's `fordShallowsWidth` and `fordRapidsLength` set how far the shallows and the foam reach) |
| `ravines` | `points`, `width`, `depth`, `rim`, `branches`, `wooded`, `shared` | Steep-walled cut with a gentle rim, side gullies and woods in patches |
| `hills` | `pos`, `radius`, `height`, `flatTop`, `shared` | Smooth rise |
| `roads` | `points`, `kind`, `shared` | A road class: width, surface, verge, rank, levelled or not |
| `settlements` | `center`, `radius`, `kind`, `count` | Up to `count` plots along the roads within `radius`, from a settlement kit |
| `woods` | `points` (polygon), `kind`, `shared` | Woodland kit: canopy, glades, shrubby edge, floor |
| `belts` | `points` (polyline), `kind`, `shared` | Belt kit: rows of trees and shrubs with gaps |
| `farmland` | `points` (polygon; empty = all free land), `kind`, `angle`, `shared` | Fields on a grid along `angle`, a crop each, margins, belts and tracks across the field ends |
| `areas`, `lines`, `scatters` | `points`, `kind`, `spacing` / `count`, `shared` | Exceptions: one surface by hand, a row of trees or props, props at random in a polygon |

### Land use and planners

`MapLand` is a 1 m grid. Each cell is claimed once by a `MapLandRegion` (a `MapLandKind`, a climate surface, a seed
and a row angle). `MapLandPlanner` runs the land passes (`MapLandPass`) in priority order on a `MapPlanContext`,
each taking only what earlier ones left:

1. water, roads, river banks and floodplain
2. bases and prop pads, access tracks
3. ravines, settlements and spec tree rows, spec areas
4. woods, belts, farmland
5. meadow on everything left, then `MapSurfacePlanter`: each surface's own lone trees and props
6. `MapDecalPlanner`: each surface's ground decals per hectare of the playable area (kept off props), then one under
   every placement whose prop names a `groundDecal`, turned with it. They go in `MapGeneration.Decals`

Passes choose at authored-sector points (`Symmetry.InSector`), place in every copy (`MapTreePlanting.Plant`,
`MapPlacements.AddEverywhere`) and claim land for every copy; raster decisions key on the sector point (`ToSector`).

Farmland splits the free land on a grid along `angle` into fields (slivers become margin), picks each crop by the
kit's weights and lays belts and tracks along the field ends. A track end within the kit's `trackJoinDistance` of a road's edge is
carried onto the road (`MapFieldTrackLinks`), and a track whose ends still lead nowhere (no road, map edge, base or
settlement, the same test as the dead-end check in `MapRoadEnds`) is dropped. A settlement (`MapSettlement`, one per
spec settlement) fits plots along the streets: house on the street, yard, outbuildings, garden, orchard, fences, gate,
street furniture. The street pipe decides every section first, then keeps only runs of at least `streetPipeMinSections`
sections, with a `streetPipeArch` over each gap of up to `streetPipeMaxGap` metres (a driveway, a crossing road) inside a
run (`MapPipeRuns`), stretched along its long side so its ends meet the runs (a placement's `Stretch`, written
as a non-uniform local scale; remains of a stretched piece keep the stretch). Access tracks: props asking for the same road class within the smaller `sharedAccessDistance` of the two
share one track, from the one nearest a road (`MapAccessGroups`). The report says why a plot didn't fit.

### Refiners

`MapRefiner` reshapes the authored sector in memory before the copies are made (the file keeps the coarse lines):
rivers meander (held straight at their ends and crossings), roads become curves, ravines smooth and fork. A road end
that stops short of another road is joined onto it by `MapRoadJoiner` at 60° or more, highest rank first.

### Ground and rendering

- **Heights**: relief, hills, river valleys, ravines. Roads of a levelling class are cut onto embankments; ramps lead
  to bridge decks.
- **Edges** (`MapEdgeField`): every surface states its edge style: `edgeWidth` (the blend band, m), `edgeJitter` (how
  far the line wanders, m; larger jitter also waves more slowly, 6 m to 22 m) and an optional `edgeSurface` strip
  `edgeSurfaceWidth` m wide just inside its border (a weedy margin inside a field). A border takes the crisper side's
  width and jitter, so a ploughed edge stays sharp against a meadow while two meadows blend wide; the style is
  smoothed over 3 m so it changes without a step where three regions meet. Each land cell knows its distance to the
  nearest other region and which region that is (`MapEdgeNeighbours`); the jitter is a noise vector that turns with
  the symmetry, so mirrored borders wander alike. Splat, details and macro all read the border through it.
- **Splat** (`MapSplatPainter`): each texel blends its border's two surfaces across the band, with each side's edge
  strip next to the border; the shader's height blend sharpens them; steep ground takes the slope surface.
- **Details** (`MapDetailPainter`): each surface's plants cover their share, clumped by noise at two scales (crops
  grow evenly), switching to the other region at the band's middle and to the edge strip's plants inside it; reeds in
  the shallows. Nothing grows on roads or placed objects.
- **Macro map** (`MapMacro`, written by `MapMacroPainter`): a light and warmth shift per field and per wood plus a
  slow gradient across each (averaged over the symmetry copies), blended over the same border band as the splat;
  noise at 14, 24, 36 and 90 m; hollows (height curvature over 8 m) a little darker and damper; wetness near water
  and on ravine floors. Shifts stay a few percent. RGB 0.5 is neutral, A is wetness.
- **Row map** (`MapRowPainter`, `_Rows.png`): one texel per land cell, RG8 linear, bilinear, no mips. RG is the
  direction of the cell's crop or garden rows (x east, y north) remapped to 0..1, moved through the cell's symmetry
  copy so mirrored and rotated fields turn too; (0.5, 0.5) where nothing grows in rows. Regions carry rows when the
  planner gives them an angle (fields, gardens: `MapLandRegion.HasRows`).
- **Terrain material** (`MapTerrainMaterial`): `HyperRTS/Terrain`, one pass, up to 16 height-blended layers from the
  climate's texture arrays, the macro and row maps, and per-layer rotation. The climate's `heightBlendDepth` (texel
  height, 0..1; about 0.06 interlocks borders without speckle) sets how far one layer's texels must stand above
  another's to win where they mix. Each layer is hex-tiled (Mikkelsen 2022): three cells per pixel, each randomly offset
  and turned by the layer's `antiTiling` (the fraction of a full turn), blended by texel height. `followsRows` layers
  (param row 3 w = 0) add the row map's turn to their own `rotation` per pixel, so each field's furrows follow its rows;
  their hex cells never turn and slide only along the texture's lines, so furrows stay continuous. Normals turn back by
  the combined rotation. A far albedo sample at `_FarTiling` × the layer tiling fades in between `_FarStart` and
  `_FarEnd` (camera distance) up to `_FarBlend`. `TerrainDetail.Level` (Presentation) sets the global keywords
  `_TERRAIN_SIMPLE` (Low: one sample) or `_TERRAIN_NO_FAR` (Medium: hex only); with neither (High, edit mode, map views)
  the shader does both.
- **Ground colour** (`MapGroundColor`): a top-down picture of the layers' mean albedo by splat weight, tinted by the
  macro map. Foliage blends into it at its roots.
- **Ground decals** (`MapDecalWriter`, after `MapPlacementWriter`): a URP `DecalProjector` per decal under the main
  scene's `Decals` root, centred on the finished ground and projecting along its normal, faces steeper than 50–75°
  fading out (as the impact marks do). Decals laid by a mirroring copy flip their UVs. The rules' `maxDecals` caps
  the count: above it the preview and build warn; the land figures and metrics report it.
- **Road ribbons** (`MapRoadMesh`, `MapRoadMeshWriter`): a mesh lifted a few centimetres over the ground, UV Y in
  metres along the road. Vertex alpha fades free ends and the lower-ranked end of a junction (`MapRoadFades`); rows
  over water or under a bridge are left out. Drawn by `HyperRTS/Road`.

### Checks and measures

- `MapAssetCheck` (climate and palette) and `MapSpecCheck` (size, terrain, amounts and every kind the spec names)
  run before anything is refined: every layer, tree, plant, decal, surface, road class, kit and palette key resolves, and
  generated prefabs have display names. Settings honoured only in some places are rejected elsewhere: `maxEdgeDistance`
  on a kit's or the ravine surface's trees, `besideRoad` on a prop a surface, settlement kit or spec prop places. A bad key is an error that names it and the feature (`MapKeyCheck`). After
  them, lookups throw (`MapKeys`): a key the checks missed fails loudly instead of being skipped.
- Road checks warn about dead ends and bends tighter than half the class's curve radius.
- The nav grid (classified like `NavGridSystem`) gives rush times, attack paths (close a corridor along the shortest
  path, search again; the paths found before none remains are the count), reach, buildable ground and resources per
  base.
- Resources within 2.5 base radii of a base count as its own, finite and renewable apart. A renewable node (its
  `ResourceNodeAuthoring.regrowthPerSecond` is above 0; `MapPaletteEntry.IsRenewable`) there is an error: main and
  natural fields run dry. Renewable stacks across the whole map are grouped into fields (`MapFieldClusters`: stacks
  within 20 m of each other, directly or through a chain, are one field); fewer than the mode's
  `minRenewableFields` × players is an error. The report stores the fields per player.
- `MapHintWriter` computes bot hints from that grid and the layout (expansion order per slot, attack lanes between
  enemy slots, defence points) and bakes them as an `AIMapHintsAuthoring` object in the SubScene. The skirmish AI
  needs it to play the map.
- `MapSpecRule` (a scene rule) shows a built map's report errors in **HyperRTS ▸ Tools ▸ Validate**, and flags a
  map scene whose spec has no report.

### Outputs

| Tool | Writes |
| --- | --- |
| Preview (`MapPreview.Run`) | `Logs/MapReports/<Map>.preview.png` (land, trees, props, crossings, bases) and `.preview.json` (issues, land figures). The Map Editor's live preview writes nothing |
| Build (`MapBuilder.Build`) | Beside the spec: scene and `_Entities` SubScene (gameplay prefabs at `[Generated] …` roots, scenery under `[Generated]`; the build also clears every library prefab instance at the SubScene root, since a prefab whose internal IDs changed drops the instance's name; hand-placed objects go under `[Dressing]`, which is never touched), `_Terrain.asset` and `.mat`, `_Water.asset`, `_Roads.asset`, `_Macro.png`, `_Rows.png`, `_Ground.png`, `_Minimap.png`, `_Overview.png`, baked lighting. In `Logs/MapReports/`: `<Map>.json` and `.png`. Errors before the scene stage stop the build with a report and leave the last good scene alone |
| Views (`MapViewCapture`, every build) | `Logs/MapReports/<Map>/*.png` from the scene's own match camera: `centre`, `base-N`, `crossing-N`, `settlement-N` (wide and `-close`), `overview`. None without a graphics device |
| Key reference (`MapKeyReference.Write`) | A Markdown list of every `kind` a spec may name, for the game to write beside its map tooling |

## Map library

The game's map content is a set of JSON files, one per item, named `<Key>.<type>.json` and kept anywhere under
`Assets/`. The key is the file name, never a field, and is unique per type (`Stubble` can be a layer, a plant and a
surface). Unity assets are named by project path. `MapLibrary.Load()` indexes every such file; items are read when
first asked for. A missing, duplicate or unreadable file throws naming the file.

| Type | Class | Holds | Names |
| --- | --- | --- | --- |
| `material` | `MapMaterial` | A shared material props are built with; model slots named after it are remapped onto it | `material` |
| `layer` | `MapClimateLayer` | A terrain texture layer, how the shader lays it, its map colour, the grass on it | `terrainLayer` |
| `plant` | `MapClimateDetail` | A terrain detail: instanced mesh prefab or billboard grass texture | `mesh`, `texture` |
| `tree` | `MapClimateTree` | A terrain tree, optionally an entity version for the playable area | `prefab`, `destructible` |
| `decal` | `MapClimateDecal` | A ground decal: size range (metres), `randomRotation`, `depth` | `material` |
| `road` | `MapRoadClass` | Width, surface, verge, rank, levelling and curve radius of a road class | `material`; surface keys |
| `surface` | `MapClimateSurface` | A ground type: its layer, the trees, plants and props on it and its `decals` per hectare (a tree with `maxEdgeDistance` grows only that close to the surface's edge, per hectare of that band; not on the ravine surface) | layer, tree, plant, prop, decal keys |
| `farmland` | `MapFarmlandKit` | Field sizes, crop mix, margins, belts and tracks (`trackJoinDistance`: metres from a road's edge within which a track end joins it) | surface, belt and road keys |
| `belt` | `MapBeltKit` | Rows of trees with shrubs in the gaps | tree and surface keys |
| `woodland` | `MapWoodlandKit` | Canopy, shrubby edge, clearings, floor | tree and surface keys |
| `settlement` | `MapSettlementKit` | Plot layout along a street, street poles and pipe (`streetPipeMinSections` sections, arches over gaps up to `streetPipeMaxGap` m) | prop, surface and tree keys |
| `prop` | `MapPaletteEntry` | One placeable prefab: flattening, yaw jitter, scale, `besideRoad` (scatters only: puts it on the nearest road's verge, facing along it), `maxTilt` (max tilt with the slope, 0 upright), `buriedDepth` (metres below the ground), the pad surface and access road it gets, `sharedAccessDistance` (metres within which props with the same access share its track; 0 for its own, required 0 without access), a `groundDecal` (with its required `groundDecalSize`) under it | `prefab`; surface, road and decal keys |
| `climate` | `MapClimate` | Ground, slope, shore, floodplain and ravine surfaces, reeds, ford shallows (`fordShallowsWidth`) and fast water (`fordRapidsLength`), fog and draw distances | `surfaces`, `roadClasses`, `farmlandKits`, `beltKits`, `woodlandKits`, `water`, `atmosphere`, `skybox`; surface and plant keys |
| `palette` | `MapPalette` | Start kit, base radius (exactly one per project) | `rig`, `startingResources`; prop keys |
| `rules` | `MapRules` | Rush thresholds, attack paths and renewable fields per player per mode, `maxDecals`, nav cell size and max slope for the built match, corridor width, view camera (exactly one per project) | |

"Names" lists the asset paths and key lists a file holds beside the item's own fields (the ref fields its
`[MapLibraryItem]` attribute lists, written first in the file), then the kinds of key the item's own fields name.

What a build gets:

- **Climate** (`MapLibrary.Climate`, `MapClimateAssembly`): the surfaces, road classes and kits its file lists, then
  the terrain layers (at most 16), trees, plants and decals those use, each added the first time it is named. The walk
  order is fixed (surfaces, belt kits, woodland kits, reeds, settlement orchards, every prop's ground decal), so layer, tree and plant indices
  change only when the data does.
- **Palette** (`MapLibrary.Palette`): the palette file plus every `prop` and every `settlement` in the library.
- **Rules** (`MapLibrary.Rules`): the one rules file.

### Keys and asset fields

String fields that name another item carry `[MapKey(typeof(...))]` (several types when either fits); spec key
fields (`climate`, each feature's `kind`) carry it too. `MapKeyDrawer` shows them as a dropdown of the library's
keys; a key the library doesn't hold stays selected and is marked missing, so a typo shows. String fields that hold
an asset path carry `[MapAsset(typeof(...))]`, drawn as an object field that writes the picked asset's path.

### Library page

**HyperRTS ▸ Tools ▸ Library** (`LibraryPage`) edits the library:

| Part | Does |
| --- | --- |
| Toolbar | **New**, **Duplicate**, **Rename** (move or rename the file; first lists what names the old key, which must change by hand), **Reveal in Project**, **Reload** |
| Types | Every type with its item count |
| Items | Search field; each item with its thumbnail (a layer's texture and map colour, a plant's texture or mesh, a tree's or prop's prefab, a road's, material item's or decal's material) |
| Form | The item loaded into a hidden `MapLibraryItemWrapper`, so property fields, drawers and Undo work; Save, Revert and an unsaved marker |
| Used by | Items and specs whose `[MapKey]` fields name the open item (`MapLibraryUsages`); click an item to open it, a spec to select its file |
| Problems | Files that don't index or parse, keys that don't resolve, asset paths that don't load (`MapLibraryProblems`); the count is the page's badge |

Saves go through `MapLibraryItemFile` in the same `MapJsonFormat` layout as specs: the ref fields first, the
`factory` and its `params` last, no key field. New, Duplicate and Rename ask for a `<Key>.<type>.json`
path in any folder and refuse a taken key. Nothing caches `MapLibrary`; the page, the key dropdowns and the problems reload when `MapLibraryChanges` sees the
project change.

### Library assets

An item's JSON holds placement data and the paths of its Unity assets (materials, layers, prefabs); the assets are
ordinary files edited in Unity, never made from the JSON. A climate's terrain texture arrays (albedo with height in alpha, normals, occlusion and smoothness) are generated
into a `Generated` folder beside the climate's library file, named after it, and rebuilt only when their source
textures or the layers change (`MapTerrainTextures`). The source images are read from file and resampled to the
slice size; mask heights are stretched so every layer blends alike (`MapTerrainSource`).

### Adding an item type

1. A class in `Data/` implementing `IMapLibraryItem` (and `IMapKeyed` if things name it), with `[MapKey]` on its
   key fields.
2. A `[MapLibraryItem("<type>", "<ref field>"...)]` attribute naming its type and ref fields (new asset paths or key
   lists go on `MapLibraryRefs` with `[MapAsset]` or `[MapKey]`), its place in `MapLibrary`'s ordered type list, and
   the refs that picture it in lists as the attribute's `Preview`, if it has one.
3. Where builds get it: `MapClimateAssembly` for climate content, `MapLibrary.Palette` for palette content.
4. Its keys checked in `MapAssetCheck` and listed in `MapKeyReference`.

## Batch and scripted entry points

| Entry | Does |
| --- | --- |
| `-executeMethod HyperRTS.Editor.Maps.Tools.MapCommandLine.Build -map <spec>` | Builds one map in batch mode. Exits 0 when the report passes, 1 on report errors, 2 on a crash or a missing `-map` |
| `...MapCommandLine.Preview -map <spec>` | Previews one map to `Logs/MapReports/`, same exit codes |
| `-nographics` | No graphics device: the build skips views, so no overview picture |
| `MapPreview.Run(spec)`, `MapBuilder.Build(spec)`, `MapViewCapture.Capture(spec)` | The same from editor code or an `eval` against a running editor |
| `MapBuildCommands.Build(spec)`, `.BuildAll()` | What the Map Editor and the Maps page call: save open scenes first, reopen the active scene after |
| `MapKeyReference.Write(path, MapLibrary.Load())` | Writes the key reference |

An agent can edit a spec's JSON while the Map Editor has it open: the editor reloads the file when it has no unsaved
edits, and redraws the preview image when a preview run elsewhere rewrites it.

## What a game supplies

- A map library: the item files above, with exactly one palette and one rules file, and the Unity assets they name
  (terrain layers, plant and tree prefabs, road materials, water, atmosphere, sky, prop prefabs, the match rig).
  A game makes those assets through library factories or a `Generator` (**HyperRTS ▸ Tools ▸ Generate**), and
  writes the key reference there.
- Specs, each in its own folder; the build writes beside it.
- Optionally an `IMapBuildHook`: called after every build with the spec, paths, report, player count, minimap and
  overview, e.g. to list the map in the game's menus and build settings.

## Adding a kind of land use

1. A kit in `Data/` (like `MapFarmlandKit`, keyed through `IMapKeyed`, loaded through `IMapLibraryItem`) held by
   `MapClimate`, its library type as above, `[MapKey]` on its key fields, its keys checked in `MapAssetCheck` and
   listed in `MapKeyReference`.
2. A `MapLandKind` if no existing one fits, and the surfaces it paints.
3. A `MapLandPass` in `Planning/` that claims land and places things through `MapPlanContext`, symmetric as above.
4. Its slot in `MapLandPlanner.Plan`, where its priority puts it.
5. A spec class in `Spec/`, an array on `MapSpec`, its keys checked in `MapSpecCheck`, and a `MapFeatureKind` in
   `MapFeatureKinds` so the Map Editor lists and draws it.
6. EditMode tests: the planner's output on a small layout, and a bad key failing loudly.
