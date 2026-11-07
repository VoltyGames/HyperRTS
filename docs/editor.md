# Editor

HyperRTS adds editor tooling for building an RTS inside Unity: a Tools window, generators, creation menus,
inspectors with live validation and Play-mode debug tools. Map tooling has its own page: [maps](maps.md). Editor UI
follows the [UI rule](../CLAUDE.md#conventions).

## HyperRTS menu

| Menu | Does |
| --- | --- |
| **HyperRTS ▸ Tools** (Ctrl+Shift+T) | Opens the Tools window |
| **HyperRTS ▸ Map Editor** (Ctrl+Shift+M) | Opens the [Map Editor](maps.md#map-editor). Double-clicking a `.map.json` opens it too |
| **HyperRTS ▸ Effect Preview** | Opens the [Effect Preview](#effect-preview) |
| **HyperRTS ▸ Documentation** | Opens these docs in the browser. Each component's help button opens its own section |

Rebind the hotkeys under **Main Menu** in **Edit ▸ Shortcuts**. Commands on one kind of asset sit in that asset's
context menu under **Assets ▸ HyperRTS** (Build Effect Graph, Bake Bone Animation Set).

## Tools window

**HyperRTS ▸ Tools** is one dockable window titled HyperRTS, with a page per tool. Each sidebar entry has a glyph, the
title and a count pill that is red when any of its count is an error and amber otherwise (Validate shows its errors
and warnings).

| Page | Does |
| --- | --- |
| **Catalog** | Every unit and building in one editable stats table, and the tech tree |
| **Validate** | Every rule's issues in prefabs, assets and open scenes, grouped by rule and asset, with quick fixes |
| **Play** | The launcher outside Play mode; network, soak tests and replays in it. See [Play-mode debugging](#play-mode-debugging) |
| **Build** | Tabs for the generators, the game's art commands and player builds. See [Build page](#build-page) |

**New blank scene...** asks for map size, player count and an optional starting base. It builds a scene with
light, ground, the `EngineRig` rig and a SubScene holding the `Match`. See [scenes from code](#scenes-from-code).

To add a page, subclass `HubPage` in any editor assembly. Set `Title`, `Glyph` (a name from `Icons/Glyphs.uss`, such as `shield-check`) and `Order` and return the content from
`Build()`: clone the page's layout with `Layout().Instantiate()`, which finds `<Page>.uxml` beside the script in its
package or in `Assets` (`Layout("Row")` finds `<Page>Row.uxml`), and wire its elements by name with
`Require<T>(name)`. The layout's root is a `PageShell`: a `uie:Toolbar` written as its first child goes in the toolbar
slot and the rest in a scrolling body (`scroll="false"` for a page whose list or tree scrolls itself). `empty-text`
and `ShowEmpty` show the empty-state note, and `ShowBusy` a busy note while a long task runs. The page leaves the panel
when it hides, so subscribe to editor events with `root.WhileAttached(on, off)`; scheduled polls stop on their own. An
optional `Badge` returns a `PageBadge` (a count and whether any is an error), as Validate's does.
`ToolsWindow.Show<TPage>()` opens the window on a page.

## Commands

Each Tools window action an agent needs is also a command of the `com.unity.pipeline` package, run from any shell
with `unity command <group> <name> [args]` while the Editor is open. A command is a thin `[CliCommand]` wrapper class beside
the feature it drives (`Animation/BoneCommands`, `Validation/ValidationCommands`, the map commands in `Maps/Tools/`):
the work lives in a method on the type that owns it, and the Tools window button and the command both call that
method. Long commands run as detached jobs (`unity command --detach`).

| Command | Calls | Returns |
| --- | --- | --- |
| `map preview <map>` | `MapPreview.Run` | Issues, land shares, counts, the preview report and image paths |
| `map build [map]` | `MapBuildRunner.Build` or `BuildAll` | One summary per map built: rush times, paths and issues, the report, image and views paths |
| `map capture <map>` | `MapViewCapture.Capture` | The view image paths |
| `map look <map> [weathers] [quality] [graded] [cost]` | `MapLookCapture.Start` | The folder at once; it enters Play, captures the spec's `looks` at three zooms per weather once GI settles, writes `summary.json` and leaves Play. `--graded false` turns the weathers' Color Lookup off and writes to `look-ungraded/`, for fitting lookups. `--cost true` takes no images: in the first weather (or the one named) it measures the median GPU and main-thread time over 120 frames at every look point and zoom, with everything on and then with each of grass, row crops, trees, contact shadows, fog, water, anti-aliasing and the horizon off in turn, and writes `cost.json` (read it with `hardpower textures look-cost <folder>`). A feature the scene lacks is listed under `skipped` |
| `map look_status` | `MapLookCapture.Status` | Whether a look capture runs, and its progress or how it ended |
| `effect build [recipe]` | `EffectGraphBuilder.Build` | Each graph built, with its `Problem` and `ShaderErrors` |
| `effect capture <rig> [times] [distance]` | `EffectCapture.Capture` | The PNG paths, one for each time |
| `generator run [name] [--list true]` | `GeneratorRunner.Run` or `RunAll` | Each generator run (or, with `list`, every one) with its sources, outputs and last run |
| `bone bake [set]` | `BoneAnimationSet.Bake` | Each set baked (none named bakes every set) with the paths of the bone texture, clip table and meshes it wrote |
| `project validate` | `ProjectValidator.Run` | Error and warning counts, then each issue's rule, severity, message, asset path and fix label |

`<map>` is a map name or its spec path (`map build` with none builds every map), and `[recipe]` a graph name or its `.effect.asset` path; an unknown one fails,
naming it. `effect capture` renders a rig in `Logs/EffectCaptures/<Rig>/` at seconds after its burst (default
`0.1,0.3,0.6,1.2,2.5`), from 24 m unless `distance` says otherwise; graphs only run on the Editor's frames, which
stop while it is in the background, so it fails after 45 s without one. The commands carry the tags `hyperrts/maps`,
`hyperrts/effects`, `hyperrts/generate`, `hyperrts/animation` and `hyperrts/validate`, so `unity command --tag hyperrts` lists them with
their arguments. `map build` fails while an open scene has unsaved changes, since a build opens its
own scene and a command can't answer the save dialog.

To add a command, write a static method in a `<Group>Commands` class beside its feature, in any editor assembly that references
`Unity.Pipeline.Attributes`, tag it `[CliCommand("<group>", "<name>", "what it does", Tags = new[] { "<area>" })]` and its
parameters `[CliArg]`. Return a small result (counts, issues, file paths) and name the full report file rather than
returning it. Throw to fail: the message reaches the caller. An `async Task<T>` method runs off the main thread
while it awaits.

### Build page

The **Build** page holds one tab per `BuildTab`, found like pages. Subclass it in any editor assembly, set `Title` and
`Order` and return the content from `Build()`, with `Layout()` finding `<Tab>.uxml` as a page does. A tab builds when
it is shown and drops its content when it hides. The last tab is remembered. The engine has **Generators** (10); the
game adds its own, such as art commands and player builds.

## Generators

A `Generator` is one step that creates generated assets. Subclass it in the engine or a game and it appears on the
Build page's Generators tab. It has a unique `Name`, a `Group` heading, an `Order`, a one-line `Description`, its `Sources` and
`Outputs` (paths, or a short description when they are many), its `Inputs` (asset paths of the files or folders it
reads, where a folder counts every file below it) and a `Run()` method. A step whose settings are constants in its
script lists that script with `ScriptPath()`. A step that uses another's output
must come after it. An import hook that keeps an output current calls its generator through `GeneratorRunner.Run`,
so an import and a manual run do the same work. Each run's time and result go to `Library/HyperRTS/GeneratorRuns.json`,
which the page and `generator run --list true` show.

A generator is **Out of date** when it never ran here, failed last time, or an input file was written after its last
run. **Run out of date** runs only those, in order, and saves assets after each. The `generator run` [command](#commands)
runs one, all, or with `--stale true` only the out of date ones. In batch mode, call `HyperRTS.Editor.Generators.GeneratorRunner.RunAll` with `-executeMethod`. To
change generated output, change the generator and run it again.

The engine has four: **Component icons** (each authoring component's glyph on its module tile), **Environment textures** (the cloud shadow cookie, road edge noise,
terrain noise and water ripple normals), **Terrain micro detail** and **Effect graphs**. The first three write into
the package, so they only show when it is embedded or local.

## Effect graphs

An effect's VFX graph is generated from its `<Graph>.effect.asset` recipe, an `EffectRecipe` ScriptableObject made
with **Assets ▸ Create ▸ HyperRTS ▸ Effects ▸ Effect Recipe**. Build one from **Assets ▸ HyperRTS ▸ Build
Effect Graph** on the selected recipe, every graph from **Build All Effect Graphs**, the **Effect graphs**
[generator](#generators), the recipe inspector's **Build graph** or the `effect build` [command](#commands). A build
is in place: the graph keeps its GUID, so prefabs keep their reference. The `EffectRecipeRule` [rule](#validation)
reports a recipe without layers, a link to a deleted texture or mesh (`BrokenLinks`, naming the field), a debris
layer without 1 to 4 meshes, and a graph that is missing or older than its recipe. Textures and meshes are object
references, so a moved asset stays linked. The recipe format and the builder are in
[client](modules/client.md#effects).

## Creation menus

**GameObject ▸ HyperRTS** places the `EngineRig` rig, a Match, or a template for a unit, building, upgrade or map
object in the scene. New scene objects go into the SubScene open for editing.

**Assets ▸ Create ▸ HyperRTS ▸ Entity...** opens the New Entity wizard: search the list, pick an entry, type the
prefab's name and **Create** saves it in the selected folder. It lists every group of every `EntityBases` asset
(**Assets ▸ Create ▸ HyperRTS ▸ Entity Bases**), where a project names its base prefabs by heading, then the engine
templates. A base becomes a prefab variant of it. The name sets the HUD display name too, with underscores read as
spaces (`USA_M1A2` shows as "USA M1A2").

A template is a root with a pick box and authoring components, plus a primitive `Model` child for you to replace.
`Editor/Templates/TemplateRegistry.cs` lists them all. **Assets ▸ Create ▸ HyperRTS** also creates data assets:
Resource Type, Damage Type, Impact Mark, Sound Cue and AI Build Order.

Authoring components sit under **Add Component ▸ HyperRTS**, one submenu per module. Each one's help button opens
its section of the module reference.

## Authoring conventions

```csharp
[AddComponentMenu(HyperRTSMenu.Entity + "Combat/Weapon")]
[Icon(HyperRTSIcons.WeaponAuthoring)]
[HelpURL(HyperRTSDocs.Combat)]
[DisallowMultipleComponent]
public class WeaponAuthoring : AuthoringBehaviour
{
    [Tooltip("Distance at which idle units spot enemies; 0 uses the vision range.")]
    [Min(0f)]
    public float acquireRange;

    private class Baker : Baker<WeaponAuthoring> { ... }
}
```

- Derive from `AuthoringBehaviour`, in games too, so the inspector and validation pick the component up.
- Give every field a `[Tooltip]`. Guard values with `[Min]` or `[Range]`. Put a number's unit in
  `[Unit("m")]` (`"s"`, `"m/s"`, `"°"`, `"/s"`), not in the tooltip, which stays a sentence. `[Header]` starts a
  foldout group in the inspector, so give each group of fields one.
- Bakers write through `EntityWriter` methods, so tests build the same entities.
- Paths come from `HyperRTSMenu`, `HyperRTSIcons` and `HyperRTSDocs`. The Add Component path is
  `HyperRTSMenu.Entity`, `Look` or `Scene` (what the component is placed on) plus `"<Module>/<Name>"`. A component
  that only lives under a parent takes `[AddComponentMenu("")]`, which hides it from Add Component, and its parent
  gets an `[InspectorButton]` that adds it as a child (`PartButtons`).
- Each component has its own icon: a white glyph from [game-icons.net](../Packages/com.hyperrts.engine/Editor/Icons/Credits.md)
  in `Editor/Icons/Components/Source/<ComponentClass>.svg` (with the black background path removed and a row in
  `Credits.md`), a `HyperRTSIcons` constant named after the class, and its name in a module's list in
  `ComponentIconGenerator`, which sets the tile colour. Run **Component icons** to draw the PNG. A game does the same
  with its own generator, which calls `ComponentIconGenerator.WriteIcon`.
- `[Owner]` on an int field shows a dropdown of the Match's players.
- `[RequiresAuthoring(typeof(T), "why")]` declares a component needed alongside. Validation offers to add it.
- Field checks need no rule of their own: `[PrefabAsset("Production option")]` wants a prefab asset, not a scene
  object; `[GhostPrefab("Hull")]` wants a ghost prefab, as the server spawns it; `[NoEmptyEntries]` (with
  `Key = nameof(...)` for entries that are structs) flags list entries the baker skips. `FieldAttributeRule` reads
  them in nested blocks and lists too, with Use Prefab, Make Ghost and Remove Empty fixes. A rule that finds the
  problem in one field passes its property path to `ValidationIssues.Warn` or `Error` (`path:`), and the inspector
  marks that field.

## Inspectors

`AuthoringEditor` is the inspector for every `AuthoringBehaviour`. A slim strip on top counts the selection's
validation issues; click it for the list and quick fixes. Below it the fields are laid out by the editor, not
`FillDefaultInspector`: each `[Header]` starts a foldout (its open state is remembered per component type and header in
`EditorState.closedGroups`) and fields before the first header stay outside. A `[Unit]` field shows its unit dim inside
the input (`UnitSuffixes`). An issue that names a property path (`ValidationIssue.Path`) puts a red or yellow edge on that
field with the message under it (`FieldIssueMarks`); the nearest drawn field holds an issue inside a closed list. Issues
without a path stay in the strip's list.

Units and buildings (`GameEntityAuthoringEditor`, single selection) start with an `EntityCard`: icon, name, role such as
"Unit · Weapon, Builder", the owner's colour and a grid of the stats the object has (cost, build time, health, armor,
speed, vision, DPS, range), read by `EntitySummary.Stats`. Under it the `ModuleStrip` has a chip per authoring component
with its icon and a dot for its worst issue. A click shows only that component's inspector and folds the others, a second
click shows them all; the last chip, **+**, opens `AddComponentPopup`, a searchable list of the components the object
lacks grouped by their Add Component folder (hidden part components are skipped).

`[InspectorButton("Fill from model", Glyph = "wand-sparkles")]` on a parameterless method of the component draws a button
row under its fields, with the glyph (a name from `Editor/Icons/Glyphs.uss`) left of the label,
or on a static editor method whose one parameter is the component, for work that needs editor code (`SectionsFill`,
`ToppleMeasure`). A click runs it on every selected object as one undo step over the object's hierarchy, and an
`InvalidOperationException` it throws shows as an error under the buttons.

`[ShowIf(nameof(field))]` hides a field while a bool is false, a number is 0 or less, or a reference is empty. It names a sibling, so it also works inside a nested block such as
`WeaponStats`.

Scene handles show vision, radius, footprints, weapon reach, producer spawn points and the map bounds, in the
owner's colour.

A component needs its own editor only for UI a button can't give, or handles. Subclass `AuthoringEditor` and override
`BuildHeader`, `BuildFooter` or `Refresh`. For handles, use `GroundHandles.EditBox` or `EditRadius` in
`OnSceneGUI`; each drag is one undo step. Editor layouts are `.uxml` files beside their scripts. Only root windows, inspectors and drawers link the shared
`Editor/Common/HyperRTSEditor.uss` with a `<Style>` tag; pages, parts and row templates take it from the window they
show in, and link only their feature's own sheet (`MapEditor.uss`, `Catalog.uss`). A `[UxmlElement]` control with a
sheet of its own adds it in its constructor with `EditorAssets.Sheet(name)`, as `IssueList` adds `Issues.uss`. The
shared sheet holds the layout classes and the colour tokens the feature sheets use: `--hrts-border` and
`--hrts-dim-text` alias Unity's editor theme variables, so the light skin follows, and `--hrts-error` and
`--hrts-warning` read on both skins. Lists and trees use Unity's own selection colour. The shared
controls are `Section` (a titled block, `title="Bot matches"`), `ValueList` (name and value rows, filled with
`Set`) and `PageShell`. `EditorAssets.Layout(type, part)` finds
`<Type><part>.uxml` in the type's package, caches it and throws when it is missing. Rows built from data clone a row
template such as `MapGalleryCard.uxml` with `CloneRoot()`. Toggle visibility with `SetHidden` and `SetInvisible` from
`HUDElements`, which set the `hrts-hidden` and `hrts-invisible` classes, not inline styles.

Glyphs are [Lucide](https://lucide.dev) SVGs in `Editor/Icons/Glyphs/`, imported as vector images by
`GlyphImportRules`: give an element the classes `hrts-glyph hrts-glyph--<name>` (from `Icons/Glyphs.uss`) and it takes
the editor text colour. To add one, copy the icon from the `lucide-static` package's `icons/` folder to
`Glyphs/<PascalName>.svg`, delete its licence comment and `class` attribute, set `stroke="#ffffff"` and add its class
to `Glyphs.uss`.

An effect recipe's `EffectRecipeInspector` adds **Build graph** and the graph's state above the layers.

To edit a JSON file in an inspector or window, derive from `JsonDocument`: a hidden ScriptableObject that holds the
parsed value for Undo and property fields. Override `Read`, `Write`, `WriteOver` (the edit written over the file's
text, changing only what differs, as `JsonChanges` does) and `Canonical`. Edit through `Serialized` and `Apply`,
or `Record` for code; each finished edit saves, an undo saves the file back, and an outside write reloads it.
`MapSpecDocument` is the one.

## Editor state

`EditorState`, a `ScriptableSingleton` saved in `UserSettings/HyperRTS.asset`, keeps every remembered
editor setting, per user and project: the Tools window's last page, the Build page's last tab, Validate's **Selection only**, the Map Editor's
auto preview, the Hierarchy and Project row switches, the debug layers switched on, the inspector header groups folded shut, the cheat amounts, the launcher choice, the soak test settings and the Effect Preview
controls. A layout binds its fields with `binding-path` and `EditorState.Bind(root)`, which saves each change;
defaults, `[Min]` and `[Range]` live on the fields, never in the UXML. Code that sets a value calls `Save()`. Values
that must reset each session stay in `SessionState`: the last validation's counts behind the Validate badge, the
replay waiting for Play mode and the local package import stamp.

## Hierarchy and Project rows

`Editor/Rows/` decorates two built-in windows. The Hierarchy uses the Hierarchy window's UI Toolkit item events
(`HierarchyWindow.BindViewItem` and `UnbindViewItem`, styled by `Rows.uss`): a row of an object with a
`ProducibleAuthoring` shows that component's icon at the right edge, a 2 px edge in the owner's match colour (none for
owner 0) and a red or amber dot when a component rule finds an error or warning on any of its authoring components. Row
data is built on first bind and cleared on hierarchy change, undo and any object change, which decorates the bound rows
again. The Project window offers only `projectWindowItemOnGUI`, so its decoration is the second exception to the
no-IMGUI rule, after Scene view `Handles`: in the Project grid view, a prefab whose root `ProducibleAuthoring` has an
`icon` shows it over the thumbnail with a small prefab badge; the list view is untouched. Preferences > HyperRTS has the
two switches, **Hierarchy row icons** and **Project unit icons**.

## Effect preview

**HyperRTS ▸ Effect Preview** plays an effect rig at the Scene view pivot, the way the pool plays it, on a loop. It
follows the rig selected in the Project window, with replay, loop interval, pause, scale and wind controls, and
holds the fog of war clear while open; closing it puts the wind and fog globals back. Saving a recipe of the rig, by
hand or in its inspector, rebuilds that graph and plays again. `EffectPreviewPlayer` plays the rig and
`EffectCapture` renders it for the `effect capture` command, above the open scene, so its light and fog still tint
the picture.

## Validation

| Rule type | Checks | Example |
| --- | --- | --- |
| `AuthoringRule<T>` | One authoring component | A Producer with no options |
| `ISceneRule` | The open scenes | No Match, or a missing Netcode bootstrap |
| `IPrefabRule` | All prefabs | Two prefabs sharing a display name |
| `IAssetRule` | Any assets | A sound cue with no clips |

Rules are found in every editor assembly, so a game adds its own the same way. Write one `AuthoringRule<T>` per
authoring type. A rule about components marked by an attribute derives from `AuthoringRule<Component>` and
overrides `AppliesTo`. An asset rule asks its `AssetSet` for `All<T>(folders)`, which loads each type and folder
once however many rules ask for it; tests build a set from assets made in memory with `AssetSet.From`.

The same rules run in the inspector, on the **Validate** page, in the console on entering Play mode, from the
`project validate` [command](#commands), and in the `ValidationTests.ProjectHasNoValidationErrors` test. Prefab and asset
rules run only in the full pass, after the component rules. The prefab and asset sets are kept between passes until
the next import, delete or move. An error or a warning can carry a quick fix, which is one undo step. Closed SubScenes
are not checked.

There are two severities, error (what must hold) and warning; there are no notes, so a hint belongs in the field's
tooltip. The Validate page groups the issues by rule, errors first, then by asset. An issue that a base prefab and its
variants share (same rule and message) shows once, on the topmost prefab that has it, as "and 2 variants" with the
variants listed under it (`ValidationIssues.GroupVariants`). The toolbar has an errors chip and a warnings chip, each
with its count and toggling that severity (remembered in `EditorState`), a search field over message, asset and rule,
**Fix all**, which applies every listed issue's fix as one undo step, and **Selection only**, which shows just the
issues on the selected objects, their children and their prefab assets. Clicking a row selects and pings its asset or
scene object, and its tooltip names the rule. The sidebar badge counts errors and warnings, such as `3E 12W`.

## Scenes from code

`Editor/Scenes/` holds the scene builder, spec and wizard.
`SceneBuilder` builds a playable scene and its `<Scene>_Entities.unity` SubScene.

```csharp
SceneBuilder.Build(new SceneSpec
{
    ScenePath = "Assets/Maps/Duel.unity",
    MapSize = new Vector2(240f, 240f),
    Players = 2,
    StartingBase = commandCentre,
    Rig = myRigVariant,                       // null = engine EngineRig
    Furnish = subScene => PlaceResources(subScene.Scene, subScene.Bases),
});
```

`Furnish` runs before the SubScene is saved, with the SubScene, its `Match` and each player's base position. The
builder throws on a bad path, player count or size. For maps built from JSON specs, see [maps](maps.md).

## Play-mode debugging

The **HyperRTS Debug** overlay in the Scene view toggles these layers; each is a `DebugLayer` discovered by type. A layer
has a short `Label`, a `Detail` for the row's tooltip, a `Group` heading (Movement or Vision) and a `Glyph`. The
toggles stay switchable outside Play mode (they are remembered and draw once Play starts), **Hide all** hides them, and
the AI table of think timer and attack wave size per player shows only in Play:

| Layer | Shows |
| --- | --- |
| Nav grid | Blocked, water and deck cells |
| Wall distance | Distance to the nearest wall for the first field key, red at a wall to blue at 8 m |
| Sectors | Connected regions of the first movement class, white where a region ends |
| Flow field | Cost to the goal of the latest group field, as repeating bands |
| Fog | Cells the local team sees |
| Sight area | The selected unit's sight: ground, tall-only and close cells |
| Sight blockers | Solid, porous or crop, and other concealing cells |
| High ground | Rise above the surrounding ground, scaled by the sight bonus |
| Spatial cells | Spatial index cells holding an entity |
| Paths and targets | Waypoints, manoeuvres, attack targets and dock bays, with the pathfinder's totals in a corner label |

The pathfinder totals (`PathStats`) cover all units, not the selected one. AI layers show when each AI player
thinks next and its attack wave size.

To add a grid view, subclass `CellTextureLayer` and implement `Paint`: give `CellTexture.Paint` the grid's
`CellGrid` and a struct implementing `ICellColors`, which maps a cell index to a `Color32` from the world's native
arrays. A Burst job fills one texture, which is drawn as a single quad, so a layer costs one upload per repaint
whatever the grid size. For anything else subclass `DebugLayer`. Layers read the authoritative world unless they
override `Source`.

The second Scene view overlay, **HyperRTS Cheats**, works in Play mode (outside it shows a one-line hint):

| Control | Does |
| --- | --- |
| Player, Resources, Instant build, Control this player | Act on the chosen player |
| Fog of war, Game speed | Switch fog and slow or speed the game |
| Spawn, Armies per side | Any prefab at the view, or two hostile armies of it as a load test |

Cheats act on the authoritative world, through the normal game systems. Using one drops the replay recording.

The **Play** page changes with the mode. Outside Play it is the launcher: map, mode (1v1 to 4v4 or free for all, which
deals the slot table), start (straight into the map or through the main menu), network (offline, host or dedicated
server), the slot table (player, side, team, bot difficulty) and the game's own switches. **Play** opens the game's
menu scene and enters Play mode, then hands the choice to the game. In Play mode it shows:

| Section | Does |
| --- | --- |
| Network | The session status and traffic; host, join localhost, run a dedicated server of the Play-mode scene |
| Soak test | AI against AI on the open map, match after match, with a table of this editor session's results (winner, length, sides) and reports in `Logs/BotMatches/` |
| Replays | Save the current recording into `Logs/Replays/`; the newest recordings with **Play** (outside Play mode) and **Reveal** |

The main toolbar beside Play shows the choice as `Chornozem 2v2 vs Normal`; its menu starts the match or opens the
launcher. The launch element is `LaunchToolbar`.

A game joins the launcher with two subclasses in an editor assembly, found like a `HubPage`. `LaunchGame` lists the
maps and sides, names the menu scene and starts the `LaunchChoice` in Play mode, through whatever its own match
launcher needs. `LaunchSetting` adds an on or off switch with a label and tooltip, such as a debug view of text.

## Prefabs

| Prefab | Holds |
| --- | --- |
| `Prefabs/EngineRig.prefab` | Camera with `CameraController`, the HUD, `OverrideAutomaticNetcodeBootstrap` |
| `Prefabs/MatchState.prefab` | The ghost replicating `MatchState`. One per map SubScene |
| `Prefabs/UI/HUD.prefab` | `PanelRenderer` with `HUDController` (`HUDPanelLayouts`), overlays, fog and the drag box |

The HUD uses `PanelRenderer`, which has no `rootVisualElement`. Get the root through `RegisterUIReloadCallback`
and build idempotently, since the callback can fire more than once. `PanelContent` does this for you.
