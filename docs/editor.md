# Editor

The Tools window, generators, menus, authoring conventions, validation and debug tools. Map tooling (Map Editor,
map library, map builds) has its own doc: [maps](maps.md). All editor UI is UI Toolkit; the only IMGUI left is
Scene view `Handles`, which have no UI Toolkit equivalent.

## HyperRTS menu

| Menu | Does |
| --- | --- |
| **HyperRTS ▸ Tools** | Opens the Tools window (below) |
| **HyperRTS ▸ Map Editor** | Opens the Map Editor (a read-only viewer); double-clicking a `.map.json` opens it too ([maps](maps.md#map-editor)) |
| **HyperRTS ▸ Documentation ▸** | Getting Started, Module Reference, Editor, Networking, in the browser |

Paths and priorities live in `Editor/EditorMenu.cs`.

## Tools window

**HyperRTS ▸ Tools** is one dockable window with a sidebar of pages. It reopens on the page it last showed.
Each page is a `HubPage` subclass, found by type in every editor assembly (engine or game), so a game adds a page
by subclassing it: `Title`, `Order` (sidebar position), an optional `Badge` (short text such as an error count,
re-read every 2 s and on project or hierarchy changes), `Build()` for the content each time the page is shown and
`Release()` to drop callbacks when it is hidden. `ToolsWindow.Show<TPage>()` opens the window on a page.

| Page | Does |
| --- | --- |
| **Maps** | Every `.map.json` with its mode, size and last build (passed, errors and warnings, or not built); **Open** (Map Editor) and **Build** per map; **Build all** and **New blank scene...** ([maps](maps.md)) |
| **Catalog** | Stats tab: every unit and building prefab in one table, edit HP, build time, vision, speed, damage, cooldown, range; DPS and cost update live. Tech Tree tab: requires, made by, makes, unlocks |
| **Validate** | Setup problems in every authoring prefab and the open scenes, with quick fixes; click an object name to select it. Badge: error count |
| **Play** | Cheats (Play mode): add resources, instant build, fog toggle, spawn any prefab at the view, two hostile armies of it that attack each other's bases (a load test), control another player (single player), game speed. Acts on the authoritative world; instant build completes through the construction and production systems. Any cheat drops the running replay recording. Network: host, join localhost, dedicated server, stop, and the client's live traffic (receive rate, bytes per packet, how stale unit updates are). Bot matches: play the open map AI against AI at a set speed, match after match, moving the baked sides one slot on each match, and write winners, lengths and player stats to `Logs/BotMatches/`. Replays: save the Play-mode recording, or play a replay file |
| **Generate** | Every generator by group, each with **Run**, and **Run all** (below) |

**New blank scene...** asks for map size, player count (1 human, the rest AI) and an optional starting-base
prefab, then builds light, ground, `RTSWorld` rig, `MatchState` ghost and a SubScene with the `Match`
(`RTSSceneBuilder`, below).

## Generators

A `Generator` is one step that (re)creates generated assets. Subclass it (engine or game) and it appears on the
Generate page: `Name` (unique), `Group` (the heading: the engine's or the game's name), `Order` (run position;
steps that use another's output come after it), `Description` (one line on what it writes), `Available` (false
hides it) and `Run()`. **Run all** runs every available generator in order and saves assets after each;
`HyperRTS.Editor.Generators.GeneratorRunner.RunAll` is also the batch-mode entry (`-executeMethod`). Generated
output is never edited by hand: change the generator and rerun it.

| Engine generator | Writes |
| --- | --- |
| Component icons | The per-module icons in `Editor/Icons/` for the engine's authoring components |
| Environment textures | Cloud shadow cookie and road edge noise in `Presentation/Environment/` |

Both write into the engine package, so they are listed only when the package is embedded or local, not installed.

## Creation menus

| Menu | Does |
| --- | --- |
| **GameObject ▸ HyperRTS ▸ RTS World (Camera + HUD)**, **Match** | The rig prefab; a Match with its MatchState ghost |
| **GameObject ▸ HyperRTS ▸ Units ▸** | Unit, Combat Unit, Worker, Harvester, Transport, Aircraft |
| **GameObject ▸ HyperRTS ▸ Buildings ▸** | Building, Producer, Research Building, Resource Drop-Off, Defense Tower, Airfield, Capturable Building |
| **GameObject ▸ HyperRTS ▸ Research ▸** | Upgrade (a research prefab for a producer's research options) |
| **GameObject ▸ HyperRTS ▸ Map ▸** | Resource Node, Nav Obstacle, Nav Area |
| **Assets ▸ Create ▸ HyperRTS ▸ Prefabs ▸** | The same templates as prefab assets, named inline in the Project window |
| **Assets ▸ Create ▸ HyperRTS ▸** | Resources ▸ Resource Type, Combat ▸ Damage Type, Combat ▸ Impact Mark, Audio ▸ Sound Cue, Match ▸ AI Build Order |

Templates are a root with collider and authoring components plus a scaled primitive `Model` child to replace.
`Editor/Templates/TemplateRegistry.cs` lists every template once and both menus use its paths, so they offer the
same set under the same names; a created object is named after its menu label. New scene objects go into the
SubScene open for editing, when there is one.

## Add Component

| Category | Components |
| --- | --- |
| Units | Unit, Flight, Container, Passenger, Capturer, Crusher, Track Marks |
| Buildings | Building, Builder, Producer, Airfield, Capturable |
| Research | Upgrade |
| Combat | Weapon, Armor, Ammo, Ability, Area Field, Veterancy, Destructible, Damage Stages, Impact Marks, Spawn Effect |
| Resources | Resource Node, Resource Drop-Off, Resource Income, Harvester |
| Navigation | Nav Obstacle, Nav Area, Terrain Height |
| Vision | Stealth, Detector |
| Match | Match |
| Audio | Entity Sounds |
| Animation | Machine, Machine Part |
| Environment | Cloud Shadows |
| Rig | HUD, Overlay Renderer, Selection Drag Box UI, Fog of War Renderer, Camera Controller, Sound Listener |

Components that builders add (AI Map Hints, Match State, Ground Color, Minimap Image) are hidden from the menu.
Each component's help button opens its section of the module reference (`HyperRTSDocs`).

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
- Bakers write through `IEntityWriter` methods (a static `Add…` on the authoring class, or a shared `*Setup` when
  several authorings use it), so tests build the same entities.
- Paths come from `Core/HyperRTSMenu`, `HyperRTSIcons` and `HyperRTSDocs`. A new module adds a line to the first
  two and an accent colour in the icon generator (**Component icons** on **HyperRTS ▸ Tools ▸ Generate**).
- `[Owner]` on an int field shows a dropdown of the scene Match's players with their colour.
- `[RequiresAuthoring(typeof(T), "why")]` declares an authoring component needed alongside (a warning with an
  **Add** fix).

## Inspectors

`AuthoringEditor` is the inspector of every `AuthoringBehaviour`, engine or game:

- Default fields under a header badge counting the validation issues of every selected object (red errors, yellow
  warnings); click it to expand the list with its quick fixes. Refreshed on edits, undo and hierarchy changes.
- `[ShowIf(nameof(field))]` (`HyperRTS.Core` namespace) hides a field while a bool is false, a number is 0 or less or an
  object reference is empty, e.g. the Weapon's projectile and splash settings.
- Producer options are compact rows: the prefab plus its icon, cost and build time. Each Match AI difficulty is
  one collapsed line with its key numbers.
- Unit and building inspectors start with a summary ("Unit · Weapon, Builder · 12 DPS · 150 Supplies").
- The Match inspector counts entities per player in the open scenes, frames the map and toggles nav and fog grid
  previews.

A component needs its own editor only for extra UI or scene handles. Subclass `AuthoringEditor` and override
`BuildHeader`, `BuildFooter` and `Refresh`; add handles in `OnSceneGUI` through `GroundHandles.EditBox` /
`EditRadius`, which record one undo step.

Scene handles: vision and radius (Unit), footprint (Building, Nav Obstacle, Nav Area), weapon reach (range plus
the owner's radius), spawn point (Producer, dragged across the ground), map bounds (Match), coloured by owner. The
radius, vision and reach dots sit at different angles so they don't stack. Unselected buildings, nav areas and
obstacles show a faint footprint.

Styles for engine editor UI live in `Editor/Common/HyperRTSEditor.uss` (classes prefixed `hrts-`), added with
`EditorAssets.AddStyles(root)`.

## Validation

| Rule type | Checks | Example |
| --- | --- | --- |
| `AuthoringRule<T>` | One authoring component | Producer with empty options, unit without a collider, effect rig that is not a world-space `EffectRig` prefab (`EffectRigCheck`) |
| `ISceneRule` | The open scenes | No or several Matches, owners without a slot, missing Netcode bootstrap, a match scene without the Unit Silhouette renderer feature or the named Units rendering layer (with fixes), a map scene whose last build report has errors |
| `IPrefabRule` | The project's prefabs | Two prefabs sharing a display name (same `TypeId`) |

- Rules are discovered in every editor assembly, so a game adds its own the same way.
- One `AuthoringRule<T>` per authoring type. A rule about components marked some other way (an attribute) derives
  from `AuthoringRule<Component>` and overrides `AppliesTo`, like `RequiresGhostRule`.
- The same rules run in four places: the inspector, **HyperRTS ▸ Tools ▸ Validate**, the console on entering Play mode
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
position before saving it. It returns the `SubScene` component in the new scene. **New blank scene...** on
**HyperRTS ▸ Tools ▸ Maps** is a dialog over it.

## Maps from specs

A map is a JSON spec (`<Map>.map.json`) of land use that `MapBuilder` turns into a scene, a SubScene and a report,
with the look of the land from the game's JSON map library. Specs and library items are JSON written by hand or by
an agent; **HyperRTS ▸ Map Editor** shows a spec on a top-down canvas with its preview; **HyperRTS ▸ Tools ▸ Maps**
lists and builds them. The spec format, the pipeline, the library and the batch entry points are in
[maps](maps.md).

A library item names its Unity assets (materials, layers, prefabs) by path; they are ordinary assets edited in
Unity.

## Play-mode debugging

- **HyperRTS Debug** Scene view overlay: toggles for the nav grid (blocked, water, decks), fog (cells the local
  team sees), spatial-index cells, and unit paths and attack targets; each AI player's time to its next think and
  attack wave size; **Cheats...**, which opens **HyperRTS ▸ Tools ▸ Play**. Each toggle is a `DebugLayer`;
  subclass it (or `CellDebugLayer`) to add one. Layers read the authoritative world unless they override `Source`
  with `SimulationWorlds.Presented`. Toggles are remembered in EditorPrefs and greyed out in Edit mode.
- **HyperRTS ▸ Tools ▸ Play**: cheats, network and replays as above. Cheats write to the server when hosting.

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
