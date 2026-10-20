# Editor

HyperRTS adds editor tooling for building an RTS inside Unity: a Tools window, generators, creation menus,
inspectors with live validation and Play-mode debug tools. Map tooling has its own page: [maps](maps.md).

All editor UI is UI Toolkit. Scene view `Handles` are the only IMGUI left, since they have no UI Toolkit
equivalent.

## HyperRTS menu

| Menu | Does |
| --- | --- |
| **HyperRTS ▸ Tools** | Opens the Tools window |
| **HyperRTS ▸ Map Editor** | Opens the [Map Editor](maps.md#map-editor). Double-clicking a `.map.json` opens it too |
| **HyperRTS ▸ Documentation** | Opens these docs in the browser |

## Tools window

**HyperRTS ▸ Tools** is one dockable window with a page per tool.

| Page | Does |
| --- | --- |
| **Maps** | Lists every map spec with its last build result, and builds them. **New blank scene...** starts a scene |
| **Catalog** | Every unit and building in one editable stats table, and the tech tree |
| **Validate** | Setup problems in prefabs and open scenes, with quick fixes |
| **Play** | Cheats, network tools, bot matches and replays. See [Play-mode debugging](#play-mode-debugging) |
| **Generate** | Every generator, each with **Run**, plus **Run all** |

**New blank scene...** asks for map size, player count and an optional starting base. It builds a scene with
light, ground, the `RTSWorld` rig and a SubScene holding the `Match`. See [scenes from code](#scenes-from-code).

To add a page, subclass `HubPage` in any editor assembly. Set `Title` and `Order`, build the content in `Build()`
and drop callbacks in `Release()`. An optional `Badge` shows short text such as an error count.
`ToolsWindow.Show<TPage>()` opens the window on a page.

## Generators

A `Generator` is one step that creates generated assets. Subclass it in the engine or a game and it appears on the
Generate page. It has a unique `Name`, a `Group` heading, an `Order`, a one-line `Description` and a `Run()`
method. A step that uses another's output must come after it.

**Run all** runs every generator in order and saves assets after each. In batch mode, call
`HyperRTS.Editor.Generators.GeneratorRunner.RunAll` with `-executeMethod`. To change generated output, change the
generator and run it again.

The engine has two: **Component icons** for the authoring icons, and **Environment textures** for the cloud
shadow cookie and road edge noise. They write into the package, so they only show when it is embedded or local.

## Creation menus

**GameObject ▸ HyperRTS** creates the `RTSWorld` rig, a Match, and templates for units, buildings, upgrades and
map objects. **Assets ▸ Create ▸ HyperRTS ▸ Prefabs** creates the same templates as prefab assets.
**Assets ▸ Create ▸ HyperRTS** also creates data assets: Resource Type, Damage Type, Impact Mark, Sound Cue and AI
Build Order.

A template is a root with a collider and authoring components, plus a primitive `Model` child for you to replace.
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
    [Tooltip("Firing range in world units, measured edge to edge.")]
    [Min(0.1f)]
    public float range = 6f;

    private class Baker : Baker<WeaponAuthoring> { ... }
}
```

- Derive from `AuthoringBehaviour`, in games too, so the inspector and validation pick the component up.
- Give every field a `[Tooltip]`. Guard values with `[Min]` or `[Range]`.
- Bakers write through `IEntityWriter` methods, so tests build the same entities.
- Paths come from `HyperRTSMenu`, `HyperRTSIcons` and `HyperRTSDocs`. A new module adds a line to the first two
  and an accent colour in the **Component icons** generator.
- `[Owner]` on an int field shows a dropdown of the Match's players.
- `[RequiresAuthoring(typeof(T), "why")]` declares a component needed alongside. Validation offers to add it.

## Inspectors

`AuthoringEditor` is the inspector for every `AuthoringBehaviour`. A header badge counts the selection's
validation issues; click it for the list and quick fixes. Units and buildings start with a summary line, such as
"Unit · Weapon, Builder · 12 DPS · 150 Supplies".

`[ShowIf(nameof(field))]` hides a field while a bool is false, a number is 0 or less, or a reference is empty.

Scene handles show vision, radius, footprints, weapon reach, producer spawn points and the map bounds, in the
owner's colour.

A component needs its own editor only for extra UI or handles. Subclass `AuthoringEditor` and override
`BuildHeader`, `BuildFooter` or `Refresh`. For handles, use `GroundHandles.EditBox` or `EditRadius` in
`OnSceneGUI`; each drag is one undo step. Editor styles live in `Editor/Common/HyperRTSEditor.uss` and are added
with `EditorAssets.AddStyles(root)`.

## Validation

| Rule type | Checks | Example |
| --- | --- | --- |
| `AuthoringRule<T>` | One authoring component | A Producer with no options |
| `ISceneRule` | The open scenes | No Match, or a missing Netcode bootstrap |
| `IPrefabRule` | All prefabs | Two prefabs sharing a display name |

Rules are found in every editor assembly, so a game adds its own the same way. Write one `AuthoringRule<T>` per
authoring type. A rule about components marked by an attribute derives from `AuthoringRule<Component>` and
overrides `AppliesTo`.

The same rules run in the inspector, on the **Validate** page, in the console on entering Play mode, and in the
`ValidationTests.ProjectHasNoValidationErrors` test. A quick fix is one undo step. Closed SubScenes are not
checked.

## Scenes from code

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

The **HyperRTS Debug** overlay in the Scene view toggles views of the nav grid, fog, spatial-index cells, unit
paths and attack targets. It also shows when each AI player thinks next and its attack wave size. To add a view,
subclass `DebugLayer` or `CellDebugLayer`. Layers read the authoritative world unless they override `Source`.

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
| `Prefabs/UI/HUD.prefab` | `PanelRenderer` with `HUDController`, overlays, fog and the drag box |

The HUD uses `PanelRenderer`, which has no `rootVisualElement`. Get the root through `RegisterUIReloadCallback`
and build idempotently, since the callback can fire more than once. `PanelContent` does this for you.
