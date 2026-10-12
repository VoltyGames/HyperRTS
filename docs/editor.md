# Editor

Menus, authoring conventions, validation and debug tools. All editor UI is UI Toolkit; the only IMGUI left is
Scene view `Handles`, which have no UI Toolkit equivalent.

## Menus

| Menu | Does |
| --- | --- |
| **HyperRTS ▸ Create RTS Scene...** | Window: map size, player count (1 human, the rest AI), optional starting-base prefab. Builds light, ground, `RTSWorld` rig, `MatchState` ghost and a SubScene with the `Match` |
| **HyperRTS ▸ Validate** | Setup problems in every authoring prefab and the open scenes, with quick fixes; click an object name to select it |
| **HyperRTS ▸ Catalog** | Stats tab: every unit and building prefab in one table, edit HP, build time, vision, speed, damage, cooldown, range; DPS and cost update live. Tech Tree tab: requires, made by, makes, unlocks |
| **HyperRTS ▸ Cheats** | Play mode: add resources, instant build, fog toggle, spawn any prefab at the view, control another player (single player), game speed. Acts on the authoritative world; instant build completes through the construction and production systems. Any cheat drops the running replay recording |
| **HyperRTS ▸ Network** | Play mode: host, join localhost, dedicated server, stop |
| **HyperRTS ▸ Replays** | Save the Play-mode recording, or play a replay file |
| **HyperRTS ▸ Maps** | Build or Preview the selected `.map.json` (or the open map's), Capture Views, Build All, Auto-rebuild on spec change, Open Report |
| **HyperRTS ▸ Generate Component Icons** | Regenerates the per-module icons in `Editor/Icons/` |
| **HyperRTS ▸ Documentation** | Opens getting started or the module reference |
| **GameObject ▸ HyperRTS ▸ RTS World**, **Match** | The rig prefab; a Match |
| **GameObject ▸ HyperRTS ▸ Units ▸** | Unit, Combat Unit, Worker, Harvester |
| **GameObject ▸ HyperRTS ▸ Buildings ▸** | Building, Producer, Resource Drop-Off, Defense Tower |
| **GameObject ▸ HyperRTS ▸ Map ▸** | Resource Node, Nav Obstacle |
| **Assets ▸ Create ▸ HyperRTS ▸ Prefabs ▸** | Combat Unit, Worker, Harvester, Producer Building, Resource Drop-Off, Defense Tower, Resource Node as prefab assets |
| **Assets ▸ Create ▸ HyperRTS ▸** | Resources ▸ Resource Type, Combat ▸ Damage Type, Audio ▸ Sound Cue, Match ▸ AI Build Order |

Templates are a root with collider and authoring components plus a scaled primitive `Model` child to replace.
Menu paths and priorities live in `Editor/EditorMenu.cs`.

## Add Component

| Category | Components |
| --- | --- |
| Units | Unit, Flight, Container, Passenger, Capturer |
| Buildings | Building, Builder, Producer, Airfield, Capturable, Upgrade |
| Combat | Weapon, Armor, Ammo, Abilities, Area Field, Veterancy |
| Resources | Resource Node, Resource Drop-Off, Harvester |
| Navigation | Nav Obstacle, Nav Area, Terrain Height |
| Vision | Stealth, Detector, Fog Of War Renderer |
| Match | Match, Match State |
| Audio | Entity Sounds, Sound Listener |
| Cameras | Camera Controller |
| Selection | Selection Drag Box UI |
| UI | HUD, Overlay Renderer |

## Authoring conventions

```csharp
[AddComponentMenu(HyperRTSMenu.Combat + "Weapon")]
[Icon(HyperRTSIcons.Combat)]
[HelpURL(HyperRTSDocs.Modules)]
[DisallowMultipleComponent]
public class WeaponAuthoring : AuthoringBehaviour
{
    [Tooltip("Firing range in world units, measured edge to edge.")]
    [Min(0.1f)]
    public float range = 6f;

    private class Baker : Baker<WeaponAuthoring> { ... }   // writes components through a *Setup helper
}
```

- Derive from `AuthoringBehaviour` (engine and game), so the inspector and validation pick the component up.
- `[Tooltip]` on every field; `[Header]` to group; `[Min]` / `[Range]` to stop bad values.
- Bakers write through `*Setup` helpers and an `IEntityWriter`, so tests build the same entities.
- Paths come from `Core/HyperRTSMenu`, `HyperRTSIcons` and `HyperRTSDocs`. A new module adds a line to the first
  two and an accent colour in the icon generator.
- `[Owner]` on an int field shows a dropdown of the scene Match's players with their colour.
- `[RequiresAuthoring(typeof(T), "why")]` declares an authoring component needed alongside (a warning with an
  **Add** fix).

## Inspectors

`AuthoringEditor` is the inspector of every `AuthoringBehaviour`, engine or game:

- Default fields plus the validation issues of every selected object, refreshed on edits, undo and hierarchy
  changes.
- Unit and building inspectors start with a summary ("Unit · Weapon, Builder · 12 DPS · 150 Supplies").
- The Match inspector counts entities per player in the open scenes, frames the map and toggles nav and fog grid
  previews.

A component needs its own editor only for extra UI or scene handles. Subclass `AuthoringEditor` and override
`BuildHeader`, `BuildFooter` and `Refresh`; add handles in `OnSceneGUI` through `GroundHandles.EditBox` /
`EditRadius`, which record one undo step.

Scene handles: vision and radius (Unit), footprint (Building, Nav Obstacle, Nav Area), weapon reach (range plus
the owner's radius), spawn point (Producer), map bounds (Match), coloured by owner.

Styles for engine editor UI live in `Editor/Common/HyperRTSEditor.uss` (classes prefixed `hrts-`), added with
`EditorAssets.AddStyles(root)`.

## Validation

| Rule type | Checks | Example |
| --- | --- | --- |
| `AuthoringRule<T>` | One authoring component | Producer with empty options, unit without a collider |
| `ISceneRule` | The open scenes | No or several Matches, owners without a slot, missing Netcode bootstrap |
| `IPrefabRule` | The project's prefabs | Two prefabs sharing a display name (same `TypeId`) |

- Rules are discovered in every editor assembly, so a game adds its own the same way.
- One `AuthoringRule<T>` per authoring type. A rule about components marked some other way (an attribute) derives
  from `AuthoringRule<Component>` and overrides `AppliesTo`, like `RequiresGhostRule`.
- The same rules run in four places: the inspector, **HyperRTS ▸ Validate**, the console on entering Play mode
  (scene rules), and `ValidationTests.ProjectHasNoValidationErrors`.
- Issues with an obvious fix carry a button (Fit Collider, Use Prefab, Remove Empty, Move Outside, Make Ghost,
  Add ...). Each fix is one undo step.
- Closed SubScenes aren't checked.

## Scenes from code

```csharp
RTSSceneBuilder.Build(new RTSSceneSpec
{
    ScenePath = "Assets/Maps/Duel.unity",
    MapSize = new Vector2(240f, 240f),
    Players = 2,
    StartingBase = commandCentre,
    Rig = myRigVariant,                       // null = engine RTSWorld
    Furnish = subScene => PlaceResources(subScene.Scene, subScene.Bases),
});
```

The builder validates the spec (throws on a bad path, player count or size), saves the scene and its
`<Scene>_Entities.unity` SubScene, and calls `Furnish` with the open SubScene, its `Match` and each player's base
position before saving it. It returns the `SubScene` component in the new scene.

## Maps from specs

A map is a JSON spec (`<Map>.map.json`) that describes land use: farmland, settlements, woods, belts, rivers, ravines,
roads. `MapBuilder.Build(specPath)` lays the land out by rule, then turns it into a scene, a SubScene and a report.
Source: `Editor/Maps/`; rendering: `Presentation/Environment/`.

```text
spec ─▶ Preview (seconds, no scene) ─▶ Build (minutes) ─▶ Capture Views
         .preview.png / .json          scene, SubScene, report, minimap     views/*.png
```

The code is layered, each row using only the rows above it:

| Folders | Hold |
| --- | --- |
| `Spec/`, `Data/`, `Symmetry/` | The spec as authored; the game's palette, rules and climates; sector copies |
| `Checks/` | Asset and spec checks, run before anything is generated |
| `Refine/`, `Layout/` | Sector lines smoothed, then copied into the whole-map layout |
| `Geometry/`, `Land/`, `Placement/`, `Ground/`, `Planning/` | Shapes and noise, the land grid, placements, heights and painters, land passes |
| `Generation/` | `MapProject` (spec and assets), `MapPaths`, `MapGeneration` in stages: `Lay`, `Plan`, `Run` |
| `Scenes/`, `Meshes/`, `Texturing/`, `Nav/`, `Hints/`, `Report/`, `Views/` | What a build writes, measures and captures |
| `Build/`, `Tools/` | `MapBuilder`, `MapPreview`, `IMapBuildHook`; menu, command line, spec watcher |

### Spec format

Unity's `JsonUtility` reads it, so it is flat: one array per feature, points as `{ "x", "z" }` objects. Metres,
origin at the map centre, X east, Z north, angles clockwise from north. The spec authors **one symmetry sector**;
the builder copies it (`rotate-180`, `mirror-x`, `mirror-z`, `rotate-3` to `rotate-8`; slots interleave so slots 1
and 2 always face each other). `"shared": true` places a feature once, on the axis or centre. Random choices are made
in the sector and copied, so copies match. Each kind of choice draws from its own seeded stream
(`MapRandomChannel`) and noise field (`MapNoiseChannel`): a new feature takes a new channel, since changing a
channel's value changes every map.

| Feature | Fields | Builds |
| --- | --- | --- |
| `spawns`, `resources`, `props` | `pos`, `yaw`, `kind` | The palette's start kit (base levelled and kept clear), resource nodes, palette prefabs; a prop with a pad gets ground round it, one with access gets a track to the nearest road |
| `rivers` | `points`, `width`, `depth`, `valleyWidth`, `meander`, `widthVariation`, `crossings` | Meandering channel of varying width, banks, floodplain, reeds, water mesh; each crossing is a bridge or ford at `t` along the river |
| `ravines` | `points`, `width`, `depth`, `rim`, `branches`, `wooded` | Steep-walled cut with a gentle rim, side gullies and woods in patches |
| `hills` | `pos`, `radius`, `height`, `flatTop` | Smooth rise |
| `roads` | `points`, `kind` | A road class: width, surface, verge, rank, levelled or not |
| `settlements` | `center`, `radius`, `kind`, `count` | Up to `count` plots along the roads within `radius`, from a settlement kit |
| `woods` | `points` (polygon), `kind` | Woodland kit: canopy, glades, shrubby edge, floor |
| `belts` | `points` (polyline), `kind` | Belt kit: rows of trees and shrubs with gaps |
| `farmland` | `points` (polygon; empty = all free land), `kind`, `angle` | Fields on a grid along `angle`, a crop each, margins, belts and tracks across the field ends |
| `areas`, `lines`, `scatters` | `points`, `kind`, `spacing` / `count` | Exceptions: one surface by hand, a row of trees or props, props at random in a polygon |

`terrain`: `height` (open ground, lowered to world y = 0), `relief` and `reliefScale` (swell), `waterLevel`,
`maxHeight`, `resolution`, `margin` (scenery ground past the playable edge).

### Land use and planners

`MapLand` is a 1 m grid. Each cell is claimed once by a `MapLandRegion` (a `MapLandKind`, a climate surface, a seed
and a row angle). `MapLandPlanner` runs the land passes (`MapLandPass`) in priority order, each taking only what
earlier ones left:

1. water, roads, river banks and floodplain
2. bases and prop pads, access tracks
3. ravines, settlements and spec tree rows, spec areas
4. woods, belts, farmland
5. meadow on everything left, then `MapSurfacePlanter`: each surface's own lone trees and props

Passes choose at authored-sector points (`Symmetry.InSector`), place in every copy (`MapTreePlanting.Plant`,
`MapPlacements.AddEverywhere`) and claim land for every copy; raster decisions key on the sector point (`ToSector`).

Farmland splits the free land on a grid along `angle` into fields (slivers become margin), picks each crop by the
kit's weights and lays belts and tracks along the field ends. A settlement (`MapSettlement`, one per spec settlement) fits plots along the streets: house on the
street, yard, outbuildings, garden, orchard, fences, gate, street furniture. The report says why a plot didn't fit.

### Refiners

`MapRefiner` reshapes the authored sector in memory before the copies are made (the file keeps the coarse lines):
rivers meander (held straight at their ends and crossings), roads become curves, ravines smooth and fork. A road end
that stops short of another road is joined onto it by `MapRoadJoiner` at 60° or more, highest rank first.

### Ground and rendering

- **Heights**: relief, hills, river valleys, ravines. Roads of a levelling class are cut onto embankments; ramps lead
  to bridge decks.
- **Splat** (`MapSplatPainter`): each texel averages the surfaces at a few noise-jittered points, so borders come out
  ragged and the shader's height blend sharpens them; steep ground takes the slope surface.
- **Details** (`MapDetailPainter`): each surface's plants cover their share, clumped by noise at two scales (crops
  grow evenly); reeds in the shallows. Nothing grows on roads or placed objects.
- **Macro map** (`MapMacroPainter`): a colour shift per field and per wood, broad noise, wetness near water. RGB 0.5
  is neutral, A is wetness.
- **Terrain material** (`MapTerrainMaterial`): `HyperRTS/Terrain`, one pass, up to 16 height-blended layers from the
  climate's texture arrays, the macro map, and per-layer rotation to the crop rows.
- **Ground colour** (`MapGroundColor`): a top-down picture of the layers' mean albedo by splat weight, tinted by the
  macro map. Foliage blends into it at its roots.
- **Road ribbons** (`MapRoadMesh`, `MapRoadMeshWriter`): a mesh lifted a few centimetres over the ground, UV Y in
  metres along the road. Vertex alpha fades free ends and the lower-ranked end of a junction (`MapRoadFades`); rows
  over water or under a bridge are left out. Drawn by `HyperRTS/Road`.

### Checks and measures

- `MapAssetCheck` (climate and palette) and `MapSpecCheck` (every kind the spec names, its size and terrain) run
  before anything is refined: every layer, tree, plant, surface, road class, kit and palette key resolves, and
  generated prefabs have display names. A bad key is an error that names it. After them, lookups throw
  (`MapKeys`): a key the checks missed fails loudly instead of being skipped.
- Road checks warn about dead ends and bends tighter than half the class's curve radius.
- The nav grid (classified like `NavGridSystem`) gives rush times, attack paths (close a corridor along the shortest
  path, search again; the paths found before none remains are the count), reach, buildable ground and resources per
  base.
- `MapHintWriter` computes bot hints from that grid and the layout (expansion order per slot, attack lanes between
  enemy slots, defence points) and bakes them as an `AIMapHintsAuthoring` object in the SubScene. The skirmish AI
  needs it to play the map.

### Outputs

| Tool | Writes |
| --- | --- |
| Preview (`MapPreview.Run`) | `Logs/MapReports/<Map>.preview.png` (land, trees, props, crossings, bases) and `.preview.json` (issues, land shares, counts). No rush times or paths |
| Build (`MapBuilder.Build`) | Scene and `_Entities` SubScene (gameplay prefabs under `[Generated] …` roots, scenery under `[Generated]`; `[Dressing]` roots are never touched), terrain and its material, water, road meshes, macro and ground-colour pictures, baked lighting, minimap, `Logs/MapReports/<Map>.json` and `.png` |
| Capture Views (`MapViewCapture`) | `Logs/MapReports/<Map>/*.png` from the scene's own match camera: `centre`, `base-N`, `crossing-N`, `settlement-N` (wide and `-close`), `overview` |
| Key reference (`MapKeyReference`) | A Markdown list of every `kind` a spec may name, for the game to write beside its map tooling |

Menu: **HyperRTS ▸ Maps** (Build, Preview, Capture Views, Build All, Auto-rebuild, Open Report). Batch mode:
`-executeMethod HyperRTS.Editor.Maps.Tools.MapCommandLine.Build` (or `.Preview`) `-map <spec>` exits 0, 1 on report
errors, 2 on a crash; `-nographics` skips the screenshots. `MapSpecRule` shows a map's report errors in
**HyperRTS ▸ Validate**.

### What the game supplies

One `MapPalette` (kinds to prefabs, start kit, settlement kits, rig), one `MapRules` (thresholds per mode, the view
camera) and a `MapClimate` per climate key: terrain layers and their texture arrays, surfaces (layer, plants, lone
trees and props), trees, plants, road classes, farmland, belt and woodland kits, water, atmosphere. It can implement
`IMapBuildHook` to list built maps in its menus.

### Adding a kind of land use

1. A kit in `Data/` (like `MapFarmlandKit`, keyed through `IMapKeyed`) held by `MapClimate`, its keys checked in
   `MapAssetCheck` and listed in `MapKeyReference`.
2. A `MapLandKind` if no existing one fits, and the surfaces it paints.
3. A `MapLandPass` in `Planning/` that claims land and places things through `MapPlanContext`, symmetric as above.
4. Its slot in `MapLandPlanner.Plan`, where its priority puts it.
5. A spec class in `Spec/`, an array on `MapSpec`, and its keys checked in `MapSpecCheck`.
6. EditMode tests: the planner's output on a small layout, and a bad key failing loudly.

## Play-mode debugging

- **HyperRTS Debug** Scene view overlay: blocked nav cells, cells the local team sees, spatial-index cells, unit
  paths and attack targets, each AI player's next think. Each toggle is a `DebugLayer`; subclass it (or
  `CellDebugLayer`) to add one. Layers read the authoritative world unless they override `Source` with
  `SimulationWorlds.Presented`.
- **HyperRTS ▸ Cheats** and **Network** as above. Cheats write to the server when hosting.

## Prefabs

| Prefab | Holds |
| --- | --- |
| `Prefabs/RTSWorld.prefab` | Camera with `CameraController`, the HUD, `OverrideAutomaticNetcodeBootstrap` |
| `Prefabs/MatchState.prefab` | Ghost replicating `MatchState`; one per map SubScene |
| `Prefabs/UI/HUD.prefab` | `PanelRenderer` + `HUDController`, `OverlayRenderer`, `FogOfWarRenderer`, drag box |
| `Prefabs/UI/HUDTheme.tss`, `HUDPanelSettings.asset` | HUD theme and panel settings |

The HUD uses `PanelRenderer` (Unity 6.5+). It has no `rootVisualElement`: get the root through
`RegisterUIReloadCallback` and rebuild idempotently, since the callback can fire more than once
(`PanelContent` does this for you).
