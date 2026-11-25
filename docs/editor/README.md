# Editor

HyperRTS adds editor tooling for building an RTS inside Unity: a Tools window, generators, creation menus, inspectors
with live validation and Play-mode debug tools ([Play mode](play-mode.md)). Read it to use the tools or to add one. Authoring components and inspectors are in
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
| **Effects** | Every effect rig prefab by kind, played at the Scene view pivot, with its recipes and stages. See [Effects page](#effects-page) |
| **Validate** | Every rule's issues in prefabs, assets and open scenes, with quick fixes. See [Validation](#validation) |
| **Play** | The launcher outside Play mode; network, bot matches and replays in it. See [Play mode](play-mode.md#play-page) |
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

Each Tools window action an agent needs is also a `com.unity.pipeline` command: `unity command <group> <name>
[args]`, or a game's wrapper, which may refresh first and so can't run in Play. A command is a thin `[CliCommand]`
class beside its feature; the type that owns the work serves the button and the command. Long commands run as
detached jobs. `--tag hyperrts` lists them.

| Command | Does |
| --- | --- |
| `map preview <map>` | Previews a map spec: issues, land shares, counts, report and image paths |
| `map build [map]` | Builds one map, or every map when none is named; returns rush times, issues and report paths |
| `map build_missing` | Builds every listed map whose scene is missing, as after a fresh clone ([stable GUIDs](../maps/build.md#stable-guids)) |
| `map capture <map>` | Captures the map's view images |
| `map look <map> [weathers] [quality] [graded] [cost]` | Enters Play, captures the spec's `looks` per weather once GI settles, writes `summary.json`, leaves Play. `--graded false` turns Color Lookup off (for fitting lookups); `--cost true` takes no images and writes a per-feature GPU and main-thread cost to `cost.json` |
| `map look_status` | Whether a look capture runs, and how it ended |
| `effect build [recipe]` | Builds effect graphs, reporting each `Problem` and `ShaderErrors` |
| `effect show <recipe> [layer]` | Prints a recipe's layers, a block each, as `field=value` text in the form `effect set` takes |
| `effect set <recipe> <layer> <changes> [build]` | Sets layer fields, saves the recipe and rebuilds its graph unless the positional `build` is `false` (`effect set VFX_X Wall "size*=1.3" false`); returns each change as `layer.field: before -> after`. `changes` are `field=value` joined by `;` (a value may hold commas), or `field*=factor` to scale a number or range. A field is a serialized path (`size.min`, `flipbook.columns`, `sixWay.opacity`); values are numbers, `true`/`false`, enum names, colours `r,g,b[,a]`, vectors `x,y,z`, ranges `min..max`, curves `0:0.9,1:1.1`, bursts `0:1-1,0.7:1-1` (delay:min-max) and textures by asset name or path. An unknown layer or field fails, naming it |
| `effect capture <rig> [times] [distance] [height] [label] [pitch]` | Warms the rig up on a throwaway copy, then renders it to `Logs/EffectCaptures/<Rig>/[label/]` at seconds after its burst, from the front and turned 90 degrees (`_front`, `_side`), aimed `height` metres up (raise it for a tall loop) and looking `pitch` degrees down (the default camera's closest-zoom pitch when left out); a label keeps the files of an earlier capture; fails after 45 s without an Editor frame, so keep the Editor in front |
| `play capture <name> [seconds] [fps]` | In Play, records the Game view as PNGs at a fixed rate into `Logs/FrameCaptures/<name>/` |
| `play stage <file>` | Runs a JSON [staging](play-mode.md#stagings) as one scripted Play check: opens its scene, enters Play, runs the timed steps, leaves Play and writes `Logs/Stagings/<name>/report.json` and `sheet.png`; returns at once |
| `play stage_status` | Whether a staging runs, and how the last one ended, with its report path |
| `generator run [name] [--stale true]` | Runs one, all, or the out of date [generators](#generators) |
| `generator list` | Lists the generators with what each reads and writes and its last run |
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
output must come after it. Each run's time and result go to `Library/HyperRTS/GeneratorRuns.json`, one entry per
generator or per item.

A generator that makes many independent things, such as one prefab per model, lists them as `Items` instead of
overriding `Run()`. A `GeneratorItem` has a name unique in its generator, its own inputs and outputs, a
`ProjectPath` (the asset or folder whose Project window entry shows its state) and a run delegate. Each item runs,
fails and goes out of date alone; a failed item never stops the others.

A generator that overrides `RunOnImport` to true also runs on its own: `GeneratorImport` runs it once per import batch
(after the batch, through `delayCall`) when an imported, deleted or moved path is one it `Reads`, and of a generator
with items only the items whose inputs the batch touched. By default `Reads` is an input or anything below an input
folder; override it when the inputs are a list that lags a delete or when an output sits inside an input folder.
Imports made while any generator or item runs start nothing, so a step that reimports what it reads never starts
itself again, and a generator that throws holds back none of the others in the batch.

A generator or item is **Out of date** when it never ran here, failed last time, or an input was written after its
last run; a generator with items is out of date when any item is. **Run out of date** runs only those, in order, and
of a generator with items only its out of date items. The tab shows such a generator's item count and how many are
out of date or failed. The `generator run` command takes a generator `name`, an `item` and `stale`; `generator list`
returns each generator with its items and their last runs. In batch mode call `GeneratorRunner.RunAll` with
`-executeMethod`. To change generated output, change the generator and run it again.

An item that is out of date shows an amber dot at the right of its `ProjectPath` entry in the Project window, a failed
one a red dot ([rows](authoring.md#hierarchy-and-project-rows)).

The engine has four: **Component icons**, **Environment textures** (cloud shadow cookie, road edge noise, terrain
noise, water ripple normals), **Terrain micro detail** and **Effect graphs**. The first three write into the package, so
they show only when it is embedded or local.

## Effect graphs

An effect's VFX graph is generated from its `<Graph>.effect.asset` recipe, an `EffectRecipe` made with **Assets ▸
Create ▸ HyperRTS ▸ Effects ▸ Effect Recipe**. Build it from **Assets ▸ HyperRTS ▸ Build Effect Graph**, **Build All
Effect Graphs**, the **Effect graphs** [generator](#generators), the recipe inspector's **Build graph** or the
`effect build` [command](#commands); `effect show` and `effect set` read and edit a recipe's layers from the command
line. A build is in place: the graph keeps its GUID, so prefabs keep their reference.
`EffectRecipeRule` reports a recipe without layers, a broken texture or mesh link, a debris layer without 1 to 4 meshes
and a graph that is missing or older than its recipe. The recipe format and builder are in
[client](../presentation/effects.md#effects).

## Effects page

**HyperRTS ▸ Tools ▸ Effects** lists every prefab whose root has an `EffectRig` under its kind (the second field of an
`FX_<Kind>_...` name, else the folder), with a search box. Picking a row plays the rig on a loop at the Scene view
pivot, as the pool plays it. The right pane has Replay, Pause, Loop, Scale and wind controls, the rig's recipes, its
validation problems and its stages: **Graph** builds its recipes' graphs and **Capture** does what `effect capture` does
without a label, its images shown below. Each stage has a state (**Done**, **Out of date**, **Missing**,
**Failed**), **Log** and **Run**; **Run out of date** queues the first stage not Done and every one after it, one at a
time. Logs go to `Logs/Effects/<Rig>/<stage>.log`, ending with an `exit code N` line.

A game adds a stage by subclassing `EffectRigStage` (found by itself): a constructor title, an `Order` before `GraphOrder` for
a stage that makes what the graph or capture reads, `Applies`, `State` (`StageFiles` judges it by input and output
times and the log), and `Run`, which writes the log and throws on failure so the rig's later stages drop.

Other tool pages reuse `Stage<TSource>`, `GeneratorStage<TSource>`, `StageQueue` and `StagesView` from `Editor/Stages/`.

**Follow selection** selects the row of a rig or recipe picked in the Project window. The recipe inspector's
**Preview** opens the page on the first rig that uses it. The effect plays and holds the fog clear only while the page
is shown; leaving it restores the wind and fog globals. Saving a recipe rebuilds its graph and plays again.

## Creation menus

Creating objects and assets has its own page: [creation menus](creation.md).

## Validation

| Rule type | Checks | Example |
| --- | --- | --- |
| `AuthoringRule<T>` | One authoring component | A Producer with no options |
| `ISceneRule` | The open scenes | No Match, or a missing Netcode bootstrap |
| `IPrefabRule` | All prefabs | Two prefabs sharing a display name |
| `IAssetRule` | Any assets | A sound cue with no clips; `UIFilesRule` reads every UXML and USS file |

Rules are found in every editor assembly, so a game adds its own the same way. Write one `AuthoringRule<T>` per
authoring type; a rule about components marked by an attribute derives from `AuthoringRule<Component>` and overrides
`AppliesTo`. An asset rule asks its `AssetSet` for `All<T>(folders)`, which loads each type and folder once; tests build
a set with `AssetSet.From`.

The same rules run in the inspector, on the **Validate** page, in the console on entering Play mode, from the
`project validate` [command](#commands) and in `ValidationTests.ProjectHasNoValidationErrors`. Prefab and asset rules
run only in the full pass. Closed SubScenes are not checked. A new rule needs a case in `RuleFiresTests`.

`UIFilesRule` checks UXML and USS under `Assets/` and the engine package, each issue naming file and line: a `Style`,
`Template`, `@import` or `url()` link naming no asset (error), a custom element tag whose `VisualElement` type is gone
(error) and a UXML class no USS selector names (warning). Classes set only from code show as that warning; `unity-`
classes are skipped.

There are two severities, error (what must hold) and warning; a hint belongs in the field's tooltip. An error or
warning can carry a quick fix, one undo step. The Validate page groups issues by rule, errors first, then by asset, and
shows an issue shared by a base prefab and its variants once, on the base. The toolbar has error and warning filters, a
search field, **Fix all** (one undo step) and **Selection only**. A run checks every prefab and takes seconds, so the
page runs only when **Validate** is pressed and keeps the last results while it closes and opens again.

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
});
```

The builder throws on a bad path, player count or size. Maps built from JSON specs: see [maps](../maps/README.md).

## Prefabs

| Prefab | Holds |
| --- | --- |
| `Prefabs/EngineRig.prefab` | Camera with `CameraController`, the HUD, `OverrideAutomaticNetcodeBootstrap` |
| `Prefabs/MatchState.prefab` | The ghost replicating `MatchState`; one per map SubScene |
| `Prefabs/UI/HUD.prefab` | `PanelRenderer` with `HUDController`, overlays, fog and the drag box |

`PanelRenderer` has no `rootVisualElement`: get the root through `RegisterUIReloadCallback` and build idempotently, as
the callback can fire more than once. `PanelContent` does this for you.
