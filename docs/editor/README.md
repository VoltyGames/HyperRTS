# Editor

HyperRTS adds editor tooling for building an RTS inside Unity: a Tools window, generators, creation menus, inspectors
with live validation and Play-mode debug tools. Read it to use the tools or to add one. Authoring components and inspectors are in
[editor authoring](authoring.md), map tooling in [maps](../maps/README.md). Editor UI follows the [UI rule](../../CLAUDE.md#conventions).

## HyperRTS menu

| Menu | Does |
| --- | --- |
| **HyperRTS ▸ Tools** (Ctrl+Shift+T) | Opens the Tools window |
| **HyperRTS ▸ Map Editor** (Ctrl+Shift+M) | Opens the [Map Editor](../maps/editor.md); so does double-clicking a `.map.json` |
| **HyperRTS ▸ Documentation** | Opens these docs; each component's help button opens its own section |

Rebind the hotkeys under **Main Menu** in **Edit ▸ Shortcuts**. Commands on one kind of asset sit in its context menu
under **Assets ▸ HyperRTS** (Build Effect Graph, Bake Bone Animation Set).

## Tools window

**HyperRTS ▸ Tools** is one dockable window with a page per tool. A sidebar entry has a glyph, a title and a count
pill, red when any counted item is an error, amber otherwise.

| Page | Does |
| --- | --- |
| **Catalog** | Every unit and building in one editable stats table, and the tech tree |
| **Effects** | Every effect rig prefab by kind, played at the Scene view pivot, with its recipes. See [Effects page](#effects-page) |
| **Validate** | Every rule's issues in prefabs, assets and open scenes, with quick fixes. See [Validation](#validation) |
| **Play** | The launcher outside Play mode; network, soak tests and replays in it. See [Play-mode debugging](#play-mode-debugging) |
| **Build** | Tabs for the generators, a game's art commands and player builds. See [Build page](#build-page) |

**New blank scene...** in the Map Editor gallery asks for map size, player count and an optional starting base, then builds a scene with a
light, ground, the `EngineRig` rig and a SubScene holding the `Match` (see [scenes from code](#scenes-from-code)).

To add a page, subclass `HubPage` in any editor assembly. Set `Title`, `Glyph` (a name from `Icons/Glyphs.uss`) and
`Order`, and return the content from `Build()`: `Layout().Instantiate()` clones `<Page>.uxml` from beside the script,
and `Require<T>(name)` wires its elements. The layout root is a `PageShell`: a `uie:Toolbar` written first goes in the
toolbar slot, the rest in a scrolling body (`scroll="false"` when the page scrolls itself). `empty-text`, `ShowEmpty`
and `ShowBusy` show notes. The page leaves the panel when hidden, so subscribe to editor events with
`root.WhileAttached(on, off)`. An optional `Badge` returns a `PageBadge` (a count and whether any is an error).
`ToolsWindow.Show<TPage>()` opens a page.

### Build page

One tab per `BuildTab`, found like pages: subclass it, set `Title` and `Order`, return the content from `Build()`. A tab
builds when shown and drops its content when hidden; the last tab is remembered. The engine adds **Generators**, and a
game adds its own.

## Commands

Each Tools window action an agent needs is also a command of the `com.unity.pipeline` package, run with
`unity command <group> <name> [args]` while the Editor is open, or through the game's wrapper
(`uv run hardpower editor run <group> <command>`). A command is a thin `[CliCommand]` class beside the feature it
drives; the work lives on the type that owns it, and the button and the command both call it. Long commands run as
detached jobs. `unity command --tag hyperrts` lists them with their arguments.

| Command | Does |
| --- | --- |
| `map preview <map>` | Previews a map spec: issues, land shares, counts, report and image paths |
| `map build [map]` | Builds one map, or every map when none is named; returns rush times, issues and report paths |
| `map capture <map>` | Captures the map's view images |
| `map look <map> [weathers] [quality] [graded] [cost]` | Enters Play, captures the spec's `looks` per weather once GI settles, writes `summary.json`, leaves Play. `--graded false` turns Color Lookup off (for fitting lookups); `--cost true` takes no images and writes a per-feature GPU and main-thread cost to `cost.json` |
| `map look_status` | Whether a look capture runs, and how it ended |
| `effect build [recipe]` | Builds effect graphs, reporting each `Problem` and `ShaderErrors` |
| `effect capture <rig> [times] [distance]` | Renders a rig to `Logs/EffectCaptures/<Rig>/` at seconds after its burst; fails after 45 s without an Editor frame, so keep the Editor in front |
| `generator run [name] [--list true] [--stale true]` | Runs one, all, or the out of date [generators](#generators) |
| `bone bake [set]` | Bakes a `BoneAnimationSet` (none named bakes all) |
| `rigid bake [set]` | Bakes a `RigidAnimationSet` (none named bakes all) |
| `project validate` | Error and warning counts, then each issue's rule, message, asset and fix |

`<map>` is a map name or its spec path, `[recipe]` a graph name or `.effect.asset` path; an unknown one fails, naming
it. `map build` fails while an open scene has unsaved changes, since a build opens its own scene.

To add a command, write a static method in a `<Group>Commands` class in any editor assembly that references
`Unity.Pipeline.Attributes`. Tag it `[CliCommand("<group>", "<name>", "what it does", Tags = new[] { "<area>" })]`, its
parameters `[CliArg]`. Return a small result (counts, issues, paths) and name a report file rather than returning the
report. Throw to fail: the message reaches the caller. An `async Task<T>` method runs off the main thread while it
awaits.

## Generators

A `Generator` is one step that creates generated assets. Subclass it in the engine or a game and it appears on the
Build page's Generators tab. It has a unique `Name`, a `Group`, an `Order`, a `Description`, its `Sources` and
`Outputs`, its `Inputs` (asset paths of files or folders it reads) and a `Run()` method. A step that uses another's
output must come after it. Each run's time and result go to `Library/HyperRTS/GeneratorRuns.json`.

A generator that overrides `RunOnImport` to true also runs on its own: `GeneratorImport` runs it once per import batch
(after the batch, through `delayCall`) when an imported, deleted or moved path is one it `Reads`. By default that is an
input or anything below an input folder; override `Reads` when the inputs are a list that lags a delete or when an
output sits inside an input folder, so the step's own writes never start it again.

A generator is **Out of date** when it never ran here, failed last time, or an input was written after its last run.
**Run out of date** runs only those, in order. In batch mode call `GeneratorRunner.RunAll` with `-executeMethod`. To
change generated output, change the generator and run it again.

The engine has four: **Component icons**, **Environment textures** (cloud shadow cookie, road edge noise, terrain
noise, water ripple normals), **Terrain micro detail** and **Effect graphs**. The first three write into the package, so
they show only when it is embedded or local.

## Effect graphs

An effect's VFX graph is generated from its `<Graph>.effect.asset` recipe, an `EffectRecipe` made with **Assets ▸
Create ▸ HyperRTS ▸ Effects ▸ Effect Recipe**. Build it from **Assets ▸ HyperRTS ▸ Build Effect Graph**, **Build All
Effect Graphs**, the **Effect graphs** [generator](#generators), the recipe inspector's **Build graph** or the
`effect build` [command](#commands). A build is in place: the graph keeps its GUID, so prefabs keep their reference.
`EffectRecipeRule` reports a recipe without layers, a broken texture or mesh link, a debris layer without 1 to 4 meshes
and a graph that is missing or older than its recipe. The recipe format and builder are in
[client](../presentation/effects.md#effects).

## Effects page

**HyperRTS ▸ Tools ▸ Effects** lists every prefab whose root has an `EffectRig` under its kind (the second field of an
`FX_<Kind>_...` name, else the folder), with a search box. Picking a row plays the rig on a loop at the Scene view
pivot, as the pool plays it. The right pane has Replay, Pause, Loop, Scale and wind controls, then the rig's recipes,
each with its state (**Built**, **Out of date**, **Missing**), **Select** and **Build graph**. **Capture frames** does
what `effect capture` does. The rig's validation problems list under it.

**Follow selection** selects the row of a rig or recipe picked in the Project window. The recipe inspector's
**Preview** opens the page on the first rig that uses it. The effect plays and holds the fog clear only while the page
is shown; leaving it restores the wind and fog globals. Saving a recipe rebuilds its graph and plays again.

## Creation menus

**GameObject ▸ HyperRTS** places the `EngineRig`, a Match, or a template for a unit, building, upgrade or map object.
New scene objects go into the SubScene open for editing. Authoring components sit under **Add Component ▸ HyperRTS**,
one submenu per module, each with a help button to its module section.

**Assets ▸ Create ▸ HyperRTS ▸ Entity...** opens the New Entity wizard: search, pick an entry, type the prefab name,
**Create**. It lists every group of every `EntityBases` asset (**Create ▸ HyperRTS ▸ Entity Bases**, where a project
names its base prefabs) and then the engine templates in `TemplateRegistry`. A base becomes a prefab variant of it. The
name sets the HUD display name, underscores read as spaces. **Assets ▸ Create ▸ HyperRTS** also creates data assets
(Resource Type, Damage Type, Impact Mark, Sound Cue, AI Build Order).

## Validation

| Rule type | Checks | Example |
| --- | --- | --- |
| `AuthoringRule<T>` | One authoring component | A Producer with no options |
| `ISceneRule` | The open scenes | No Match, or a missing Netcode bootstrap |
| `IPrefabRule` | All prefabs | Two prefabs sharing a display name |
| `IAssetRule` | Any assets | A sound cue with no clips |

Rules are found in every editor assembly, so a game adds its own the same way. Write one `AuthoringRule<T>` per
authoring type; a rule about components marked by an attribute derives from `AuthoringRule<Component>` and overrides
`AppliesTo`. An asset rule asks its `AssetSet` for `All<T>(folders)`, which loads each type and folder once; tests build
a set with `AssetSet.From`.

The same rules run in the inspector, on the **Validate** page, in the console on entering Play mode, from the
`project validate` [command](#commands) and in `ValidationTests.ProjectHasNoValidationErrors`. Prefab and asset rules
run only in the full pass. Closed SubScenes are not checked. A new rule needs a case in `RuleFiresTests`.

There are two severities, error (what must hold) and warning; a hint belongs in the field's tooltip. An error or
warning can carry a quick fix, one undo step. The Validate page groups issues by rule, errors first, then by asset, and
shows an issue shared by a base prefab and its variants once, on the base. The toolbar has error and warning filters, a
search field, **Fix all** (one undo step) and **Selection only**.

## Scenes from code

`SceneBuilder` (in `Editor/Scenes/`) builds a playable scene and its `<Scene>_Entities.unity` SubScene:

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
builder throws on a bad path, player count or size. Maps built from JSON specs: see [maps](../maps/README.md).

## Play-mode debugging

The debug layers and cheats show in two places: the **HyperRTS Debug** and **HyperRTS Cheats** Scene view overlays,
and the **HyperRTS ▸ Cheats & Debug** window, which docks beside the Game view so they stay in reach during Play.
Both build on `DebugSection` and `CheatsSection`.

The debug section toggles `DebugLayer`s, found by type (a `Label`, `Detail`, `Group` and `Glyph` each). Toggles are
remembered and work outside Play mode; **Hide all** clears them.

| Layer | Shows |
| --- | --- |
| Nav grid | Blocked, water and deck cells |
| Wall distance | Distance to the nearest wall, red at a wall to blue at 8 m |
| Sectors | Connected regions of the first movement class |
| Flow field | Cost to the goal of the latest group field |
| Fog | Cells the local team sees |
| Sight area, Sight blockers, High ground | The selected unit's sight, what conceals cells, and rise above surrounding ground |
| Spatial cells | Spatial index cells holding an entity |
| Paths and targets | Waypoints, manoeuvres, attack targets and dock bays, with the pathfinder's totals (all units) |

In Play, an AI table shows each player's think timer and attack wave size. To add a grid view, subclass
`CellTextureLayer` and implement `Paint`, giving `CellTexture.Paint` a `CellGrid` and an `ICellColors` struct; a Burst
job fills one texture drawn as a single quad. For anything else subclass `DebugLayer`. Layers read the authoritative
world unless they override `Source`.

The cheats work in Play mode only:

| Control | Does |
| --- | --- |
| Player, Resources, Instant build, Control this player | Act on the chosen player |
| Fog of war, Game speed | Switch fog and change speed |
| Spawn, Armies per side | Any prefab at the view, or two hostile armies of it as a load test |

Cheats act through the normal game systems and drop the replay recording.

The **Play** page is the launcher outside Play mode: map, mode (1v1 to 4v4 or free for all), start (into the map or
through the main menu), network (offline, host, dedicated server), the slot table and the game's switches. **Play**
opens the game's menu scene and hands the choice to the game. The toolbar beside Play shows the choice
(`LaunchToolbar`). In Play mode the page shows:

| Section | Does |
| --- | --- |
| Network | Session status and traffic; host, join localhost, run a dedicated server of the scene |
| Soak test | AI against AI, match after match, with a results table and reports in `Logs/BotMatches/` |
| Replays | Save the recording to `Logs/Replays/`; play or reveal the newest |

A game joins the launcher with two subclasses in an editor assembly: `LaunchGame` lists maps and sides, names the menu
scene and starts a `LaunchChoice`; `LaunchSetting` adds an on or off switch.

## Prefabs

| Prefab | Holds |
| --- | --- |
| `Prefabs/EngineRig.prefab` | Camera with `CameraController`, the HUD, `OverrideAutomaticNetcodeBootstrap` |
| `Prefabs/MatchState.prefab` | The ghost replicating `MatchState`; one per map SubScene |
| `Prefabs/UI/HUD.prefab` | `PanelRenderer` with `HUDController`, overlays, fog and the drag box |

`PanelRenderer` has no `rootVisualElement`: get the root through `RegisterUIReloadCallback` and build idempotently, as
the callback can fire more than once. `PanelContent` does this for you.
