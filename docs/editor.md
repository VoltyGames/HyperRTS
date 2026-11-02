# Editor

HyperRTS adds editor tooling for building an RTS inside Unity: a Tools window, generators, creation menus,
inspectors with live validation and Play-mode debug tools. Map tooling has its own page: [maps](maps.md). Editor UI
follows the [UI rule](../CLAUDE.md#conventions).

## HyperRTS menu

| Menu | Does |
| --- | --- |
| **HyperRTS ▸ Tools** | Opens the Tools window |
| **HyperRTS ▸ Map Editor** | Opens the [Map Editor](maps.md#map-editor). Double-clicking a `.map.json` opens it too |
| **HyperRTS ▸ Effect Preview** | Opens the [Effect Preview](#effect-preview) |
| **HyperRTS ▸ Documentation** | Opens these docs in the browser |

## Tools window

**HyperRTS ▸ Tools** is one dockable window with a page per tool.

| Page | Does |
| --- | --- |
| **Maps** | Lists every map spec with its last build result, and builds them. **New blank scene...** starts a scene |
| **Catalog** | Every unit and building in one editable stats table, and the tech tree |
| **Validate** | Every rule's issues in prefabs, assets and open scenes, grouped by rule and asset, with quick fixes |
| **Play** | Cheats, network tools, bot matches and replays. See [Play-mode debugging](#play-mode-debugging) |
| **Generate** | Every generator with what it reads and writes and its last run, each with **Run**, plus **Run all** |

**New blank scene...** asks for map size, player count and an optional starting base. It builds a scene with
light, ground, the `RTSWorld` rig and a SubScene holding the `Match`. See [scenes from code](#scenes-from-code).

To add a page, subclass `HubPage` in any editor assembly. Set `Title` and `Order` and return the content from
`Build()`: clone the page's layout with `Layout().Instantiate()`, which finds `<Page>.uxml` beside the script in its
package or in `Assets` (`Layout("Row")` finds `<Page>Row.uxml`), and wire its elements by name with
`Require<T>(name)`. The layout's root is a `PageShell`: a `uie:Toolbar` written as its first child goes in the toolbar
slot and the rest in a scrolling body (`scroll="false"` for a page whose list or tree scrolls itself). `empty-text`
and `ShowEmpty` show the empty-state note, and `ShowBusy` a busy note while a long task runs. The page leaves the panel
when it hides, so subscribe to editor events with `root.WhileAttached(on, off)`; scheduled polls stop on their own. An
optional `Badge` shows short text such as the Validate page's `3E 12W` (errors and warnings).
`ToolsWindow.Show<TPage>()` opens the window on a page.

## Commands

Each Tools window action an agent needs is also a command of the `com.unity.pipeline` package, run from any shell
with `unity command <name> [args]` while the Editor is open. A command is a thin `[CliCommand]` wrapper in an
`Editor/Commands/` folder (the map commands sit in `Editor/Maps/Tools/`): the work lives in a method on the type that owns it, and the Tools window button and the
command both call that method. Long commands run as detached jobs (`unity command --detach`).

| Command | Calls | Returns |
| --- | --- | --- |
| `map_preview <map>` | `MapPreview.Run` | Issues, land shares, counts, the preview report and image paths |
| `map_build [map]` | `MapBuildRunner.Build` or `BuildAll` | One summary per map built: rush times, paths and issues, the report, image and views paths |
| `map_capture <map>` | `MapViewCapture.Capture` | The view image paths |
| `effect_build [recipe]` | `EffectGraphBuilder.Build` | Each graph built, with its `Problem` and `ShaderErrors` |
| `effect_capture <rig> [times] [distance]` | `EffectCapture.Capture` | The PNG paths, one for each time |
| `generate [name] [--list true]` | `GeneratorRunner.Run` or `RunAll` | Each generator run (or, with `list`, every one) with its sources, outputs and last run |
| `validate` | `ProjectValidator.Run` | Error and warning counts, then each issue's rule, severity, message, asset path and fix label |

`<map>` is a map name or its spec path (`map_build` with none builds every map), and `[recipe]` a graph name or its `.effect.asset` path; an unknown one fails,
naming it. `effect_capture` renders a rig in `Logs/EffectCaptures/<Rig>/` at seconds after its burst (default
`0.1,0.3,0.6,1.2,2.5`), from 24 m unless `distance` says otherwise; graphs only run on the Editor's frames, which
stop while it is in the background, so it fails after 45 s without one. The commands carry the tags `hyperrts/maps`,
`hyperrts/effects`, `hyperrts/generate` and `hyperrts/validate`, so `unity command --tag hyperrts` lists them with
their arguments. `map_build` fails while an open scene has unsaved changes, since a build opens its
own scene and a command can't answer the save dialog.

To add a command, write a static method in an `Editor/Commands/` class of any editor assembly that references
`Unity.Pipeline.Attributes`, tag it `[CliCommand("name", "what it does", Tags = new[] { "<area>" })]` and its
parameters `[CliArg]`. Return a small result (counts, issues, file paths) and name the full report file rather than
returning it. Throw to fail: the message reaches the caller. An `async Task<T>` method runs off the main thread
while it awaits.

## Generators

A `Generator` is one step that creates generated assets. Subclass it in the engine or a game and it appears on the
Generate page. It has a unique `Name`, a `Group` heading, an `Order`, a one-line `Description`, its `Sources` and
`Outputs` (paths, or a short description when they are many) and a `Run()` method. A step that uses another's output
must come after it. An import hook that keeps an output current calls its generator through `GeneratorRunner.Run`,
so an import and a manual run do the same work. Each run's time and result go to `Library/HyperRTS/GeneratorRuns.json`,
which the page and `generate --list true` show.

**Run all** runs every generator in order and saves assets after each. The `generate` [command](#commands) runs one
or all of them. In batch mode, call `HyperRTS.Editor.Generators.GeneratorRunner.RunAll` with `-executeMethod`. To
change generated output, change the generator and run it again.

The engine has four: **Component icons**, **Environment textures** (the cloud shadow cookie, road edge noise,
terrain noise and water ripple normals), **Terrain micro detail** and **Effect graphs**. The first three write into
the package, so they only show when it is embedded or local.

## Effect graphs

An effect's VFX graph is generated from its `<Graph>.effect.asset` recipe, an `EffectRecipe` ScriptableObject made
with **Assets ▸ Create ▸ HyperRTS ▸ Effects ▸ Effect Recipe**. Build one from **Assets ▸ HyperRTS ▸ Build
Effect Graph** on the selected recipe, every graph from **Build All Effect Graphs**, the **Effect graphs**
[generator](#generators), the recipe inspector's **Build graph** or the `effect_build` [command](#commands). A build
is in place: the graph keeps its GUID, so prefabs keep their reference. The `EffectRecipeRule` [rule](#validation)
reports a recipe without layers, a link to a deleted texture or mesh (`BrokenLinks`, naming the field), a debris
layer without 1 to 4 meshes, and a graph that is missing or older than its recipe. Textures and meshes are object
references, so a moved asset stays linked. The recipe format and the builder are in
[client](modules/client.md#effects).

## Creation menus

**GameObject ▸ HyperRTS** creates the `RTSWorld` rig, a Match, and templates for units, buildings, upgrades and
map objects. **Assets ▸ Create ▸ HyperRTS ▸ Prefabs** creates the same templates as prefab assets.
**Assets ▸ Create ▸ HyperRTS** also creates data assets: Resource Type, Damage Type, Impact Mark, Sound Cue and AI
Build Order.

A template is a root with a pick box and authoring components, plus a primitive `Model` child for you to replace.
`Editor/Templates/TemplateRegistry.cs` lists them all. New scene objects go into the SubScene open for editing.

Authoring components sit under **Add Component ▸ HyperRTS**, one submenu per module. Each one's help button opens
its section of the module reference.

## Authoring conventions

```csharp
[AddComponentMenu(HyperRTSMenu.Combat + "Weapon")]
[Icon(HyperRTSIcons.Combat)]
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
- Give every field a `[Tooltip]`. Guard values with `[Min]` or `[Range]`.
- Bakers write through `EntityWriter` methods, so tests build the same entities.
- Paths come from `HyperRTSMenu`, `HyperRTSIcons` and `HyperRTSDocs`. A new module adds a line to the first two
  and an accent colour in the **Component icons** generator.
- `[Owner]` on an int field shows a dropdown of the Match's players.
- `[RequiresAuthoring(typeof(T), "why")]` declares a component needed alongside. Validation offers to add it.
- Field checks need no rule of their own: `[PrefabAsset("Production option")]` wants a prefab asset, not a scene
  object; `[GhostPrefab("Hull")]` wants a ghost prefab, as the server spawns it; `[NoEmptyEntries]` (with
  `Key = nameof(...)` for entries that are structs) flags list entries the baker skips. `FieldAttributeRule` reads
  them in nested blocks and lists too, with Use Prefab, Make Ghost and Remove Empty fixes.

## Inspectors

`AuthoringEditor` is the inspector for every `AuthoringBehaviour`. A header badge counts the selection's
validation issues; click it for the list and quick fixes. Units and buildings start with a summary line, such as
"Unit · Weapon, Builder · 12 DPS · 150 Supplies".

`[InspectorButton("Fill from model")]` on a parameterless method of the component draws a button under its fields,
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
show in, and link only their feature's own sheet (`MapsPage.uss`, `Catalog.uss`). A `[UxmlElement]` control with a
sheet of its own adds it in its constructor with `EditorAssets.Sheet(name)`, as `IssueList` adds `Issues.uss`. The
shared sheet holds the layout classes and the colour tokens the feature sheets use: `--hrts-border` and
`--hrts-dim-text` alias Unity's editor theme variables, so the light skin follows, and `--hrts-error`,
`--hrts-warning` and `--hrts-note` read on both skins. Lists and trees use Unity's own selection colour. The shared
controls are `Section` (a titled block, `title="Bot matches"`), `ValueList` (name and value rows, filled with
`Set`) and `PageShell`. `EditorAssets.Layout(type, part)` finds
`<Type><part>.uxml` in the type's package, caches it and throws when it is missing. Rows built from data clone a row
template such as `MapsPageRow.uxml` with `CloneRoot()`. Toggle visibility with `SetHidden` and `SetInvisible` from
`HUDElements`, which set the `hrts-hidden` and `hrts-invisible` classes, not inline styles.

An effect recipe's `EffectRecipeInspector` adds **Build graph** and the graph's state above the layers.

To edit a JSON file in an inspector or window, derive from `JsonDocument`: a hidden ScriptableObject that holds the
parsed value for Undo and property fields. Override `Read`, `Write`, `WriteOver` (the edit written over the file's
text, changing only what differs, as `JsonChanges` does) and `Canonical`. Edit through `Serialized` and `Apply`,
or `Record` for code; each finished edit saves, an undo saves the file back, and an outside write reloads it.
`MapSpecDocument` is the one.

## Editor state

`HyperRTSEditorState`, a `ScriptableSingleton` saved in `UserSettings/HyperRTS.asset`, keeps every remembered
editor setting, per user and project: the Tools window's last page, Validate's **Selection only**, the Map Editor's
auto preview, the debug layers switched on, the cheat amounts, the bot match settings and the Effect Preview
controls. A layout binds its fields with `binding-path` and `HyperRTSEditorState.Bind(root)`, which saves each change;
defaults, `[Min]` and `[Range]` live on the fields, never in the UXML. Code that sets a value calls `Save()`. Values
that must reset each session stay in `SessionState`: the last validation's counts behind the Validate badge, the
replay waiting for Play mode and the local package import stamp.

## Effect preview

**HyperRTS ▸ Effect Preview** plays an effect rig at the Scene view pivot, the way the pool plays it, on a loop. It
follows the rig selected in the Project window, with replay, loop interval, pause, scale and wind controls, and
holds the fog of war clear while open; closing it puts the wind and fog globals back. Saving a recipe of the rig, by
hand or in its inspector, rebuilds that graph and plays again. `EffectPreviewPlayer` plays the rig and
`EffectCapture` renders it for the `effect_capture` command, above the open scene, so its light and fog still tint
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
`validate` [command](#commands), and in the `ValidationTests.ProjectHasNoValidationErrors` test. Prefab and asset
rules run only in the full pass, after the component rules. The prefab and asset sets are kept between passes until
the next import, delete or move. A quick fix is one undo step. Closed SubScenes are not checked.

The Validate page groups the issues by rule, errors first, then by asset. **Selection only** shows just the issues
on the selected objects, their children and their prefab assets. Its sidebar badge counts errors and warnings, such
as `3E 12W`.

## Scenes from code

`Editor/Scenes/` holds the scene builder, spec and wizard.
`RTSSceneBuilder` builds a playable scene and its `<Scene>_Entities.unity` SubScene.

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

`Furnish` runs before the SubScene is saved, with the SubScene, its `Match` and each player's base position. The
builder throws on a bad path, player count or size. For maps built from JSON specs, see [maps](maps.md).

## Play-mode debugging

The **HyperRTS Debug** overlay in the Scene view toggles these layers; each is a `DebugLayer` discovered by type:

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

The **Play** page drives a running match:

| Section | Does |
| --- | --- |
| Cheats | Resources, instant build, fog toggle, game speed, control another player |
| Spawn | Any prefab at the view, or two hostile armies of it as a load test |
| Network | Host, join localhost, run a dedicated server, and watch the client's traffic |
| Bot matches | AI against AI on the open map, match after match, with results in `Logs/BotMatches/` |
| Replays | Save the current recording, or play a replay file |

Cheats act on the authoritative world, through the normal game systems. Using one drops the replay recording.

## Prefabs

| Prefab | Holds |
| --- | --- |
| `Prefabs/RTSWorld.prefab` | Camera with `CameraController`, the HUD, `OverrideAutomaticNetcodeBootstrap` |
| `Prefabs/MatchState.prefab` | The ghost replicating `MatchState`. One per map SubScene |
| `Prefabs/UI/HUD.prefab` | `PanelRenderer` with `HUDController` (`HUDPanelLayouts`), overlays, fog and the drag box |

The HUD uses `PanelRenderer`, which has no `rootVisualElement`. Get the root through `RegisterUIReloadCallback`
and build idempotently, since the callback can fire more than once. `PanelContent` does this for you.
