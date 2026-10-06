# Editor UX and authoring conventions

How HyperRTS presents itself in the Unity Editor, and the tools that remove manual wiring. Goal: anyone building a
game on the engine gets a clean Add Component menu, helpful inspectors and one-click setup, and new components
inherit the same polish.

## Menus

| Menu | What it does |
| --- | --- |
| **HyperRTS ▸ Create RTS Scene...** | Wizard: map size, player count (1 human, the rest AI) and an optional starting-base prefab placed for each player. Builds light, ground, `RTSWorld` rig and a SubScene with the `Match` ([`RTSSceneWizard.cs`](../Packages/com.hyperrts.engine/Editor/RTSSceneWizard.cs)) |
| **HyperRTS ▸ Generate Component Icons** | Regenerates the per-module icons in `Packages/com.hyperrts.engine/Editor/Icons/` |
| **HyperRTS ▸ Validate** | Lists setup problems in every HyperRTS prefab and the open scenes; click one to select the object ([`Validation/`](../Packages/com.hyperrts.engine/Editor/Validation/)) |
| **HyperRTS ▸ Catalog** | Every unit and building prefab in one table: edit HP, build time, vision, speed, damage, cooldown and range, see DPS and cost. The Tech Tree tab shows what each one requires, is made by, makes and unlocks ([`Catalog/`](../Packages/com.hyperrts.engine/Editor/Catalog/)) |
| **HyperRTS ▸ Cheats** | Play mode: add resources, instant build, toggle fog, spawn any entity prefab at the camera's view point, control another player (single player only), game speed. Cheats write to the authoritative world, so when hosting they change the server, not the client copy ([`PlayMode/`](../Packages/com.hyperrts.engine/Editor/PlayMode/)) |
| **HyperRTS ▸ Network** | Play mode: host, join localhost, run a dedicated server, or stop the session |
| **HyperRTS ▸ Replays** | Save the Play-mode recording, or open a replay's scene and play it back |
| **HyperRTS ▸ Documentation** | Opens the getting-started guide or the module reference |
| **GameObject ▸ HyperRTS ▸ RTS World (Camera + HUD)**, **Match** | Drops the rig prefab or a Match |
| **GameObject ▸ HyperRTS ▸ Units / Buildings / Map ▸ …** | Role templates in the scene: Unit, Combat Unit, Worker, Harvester, Building, Producer, Resource Drop-Off, Defense Tower, Resource Node, Nav Obstacle. A root with collider and authoring, and a scaled primitive `Model` child to replace with your mesh ([`Templates/`](../Packages/com.hyperrts.engine/Editor/Templates/)) |
| **Assets ▸ Create ▸ HyperRTS ▸ Prefabs ▸ …** | The same role templates saved as prefab assets in the selected folder, ready for producer and builder option lists |
| **Assets ▸ Create ▸ HyperRTS ▸ Resources ▸ Resource Type**, **Combat ▸ Damage Type** | Data assets |

Paths and priorities of the **HyperRTS** menu live in one place,
[`EditorMenu.cs`](../Packages/com.hyperrts.engine/Editor/EditorMenu.cs), in the order the menu shows them.

## Add Component menu

First-party authoring components sit under one **`HyperRTS`** root, grouped by module:

```text
Add Component ▸ HyperRTS ▸ Units ▸ Unit
                         ▸ Buildings ▸ Building / Builder / Producer
                         ▸ Combat ▸ Weapon / Armor
                         ▸ Resources ▸ Resource Node / Resource Drop-Off / Harvester
                         ▸ Navigation ▸ Nav Obstacle
                         ▸ Match ▸ Match
```

## The convention (apply to every authoring component)

```csharp
[AddComponentMenu(HyperRTSMenu.Combat + "Weapon")]   // category + friendly name
[Icon(HyperRTSIcons.Combat)]                         // per-module icon
[HelpURL(HyperRTSDocs.Modules)]                      // "?" opens the docs
[DisallowMultipleComponent]
public class WeaponAuthoring : AuthoringBehaviour
{
    [Tooltip("Firing range in world units, measured edge to edge.")]
    [Min(0.1f)]
    public float range = 6f;
}
```

- **`AuthoringBehaviour`** is the base of every authoring component, engine or game, so the inspector, entity
  summaries and validation pick game components up too.
- **`[Tooltip]`** on every serialized field, **`[Header]`** to group long components, `[Min]`/`[Range]` to stop
  invalid values.
- **Scene handles** on selection show what a number means and let you drag it: vision and radius (`Unit`), footprint
  (`Building`, `Nav Obstacle`), weapon reach (range plus the owner's radius, as combat measures it), spawn point
  (`Producer`) and map bounds (`Match`). Colours follow the owner's player colour.
- **Paths are centralised** in `Core/`: [`HyperRTSMenu`](../Packages/com.hyperrts.engine/Core/HyperRTSMenu.cs),
  [`HyperRTSIcons`](../Packages/com.hyperrts.engine/Core/HyperRTSIcons.cs) and
  [`HyperRTSDocs`](../Packages/com.hyperrts.engine/Core/HyperRTSDocs.cs). A new module adds one line to `HyperRTSMenu` and `HyperRTSIcons`, plus an accent
  colour in the icon generator.
- **Shared setup.** Bakers write components through the `*Setup` helpers and an `IComponentSink`, so tests and
  tools build the same entities as baking.

## Validation

Per-component rules live in [`Validation/Rules/`](../Packages/com.hyperrts.engine/Editor/Validation/Rules/), one
`AuthoringRule<T>` per authoring type (no collider, empty or scene-object production options, unit prerequisites,
spawn point inside the footprint, node without a resource type, ...). `[RequiresAuthoring]` on an authoring class
declares a component it needs alongside it. `AuthoringRule<T>` has shared checks for prefab references and option
lists (`CheckPrefabReference`, `CheckPrefabOptions`) and for entries the baker skips (`CheckEmptyEntries`). `[RequiresGhost]`
marks a component whose object must be a ghost.
[`AuthoringChecks`](../Packages/com.hyperrts.engine/Editor/Validation/AuthoringChecks.cs) runs every rule that applies. Cross-object rules sit beside them: `ISceneRule`s check the open scenes (zero or several Matches, owners without a
player slot, entities outside the map, a scene missing `OverrideAutomaticNetcodeBootstrap`) and `IPrefabRule`s
check the project's prefabs (two sharing a display name would merge into one `TypeId`).
[`ProjectValidator`](../Packages/com.hyperrts.engine/Editor/Validation/ProjectValidator.cs) runs them all; Play mode runs only
the scene rules. The same rules show up in four places:

- under each authoring component in the inspector ([`AuthoringEditor`](../Packages/com.hyperrts.engine/Editor/Authoring/AuthoringEditor.cs));
- in **HyperRTS ▸ Validate**;
- in the console, linked to the object, when entering Play mode (open scenes only);
- in `ValidationTests.ProjectHasNoValidationErrors`, so CI fails on errors.

Rules are discovered, so a game adds its own (`AuthoringRule<T>`, `ISceneRule` or `IPrefabRule`) in any editor
assembly, and every one of these picks it up. Closed SubScenes aren't checked.

## Inspectors

Every authoring component (anything deriving from `AuthoringBehaviour`, engine or game) draws its default inspector
plus its validation warnings: `AuthoringEditor` is the inspector for all of them, and a component needs its own
editor only for extra GUI or scene handles. Warnings with an obvious fix carry a button (**Fit Collider**, **Use
Prefab**, **Remove Empty**, **Move Outside**, **Make Ghost**, **Add ...** for a missing required component), also
shown in **HyperRTS ▸ Validate**; each fix is one undo step
([`QuickFixes`](../Packages/com.hyperrts.engine/Editor/Validation/QuickFixes.cs)).

Unit and building inspectors open with a one-line summary (for example "Unit · Weapon, Builder · 12 DPS · 150
Supplies") and draw radius or footprint handles (`UnitAuthoringEditor`, `BuildingAuthoringEditor`). Fields marked
`[Owner]` show as a dropdown of the scene Match's players with their colour. The **Match** inspector
counts entities per player in the open scenes, frames the map in the Scene view, and previews the nav and fog grids.

Scene handles go through `GroundHandles.EditBox` and `GroundHandles.EditRadius`, which apply a drag as one undo step
with `EditorUndo.Record` (in [`Common/`](../Packages/com.hyperrts.engine/Editor/Common/), next to `EditorAssets`,
`TypeDiscovery` and the shared `NavColors`).

To customise one field, give it a `PropertyAttribute` and a `PropertyDrawer`, as `[Owner]` does.

## Play-mode debugging

The **HyperRTS Debug** Scene view overlay (toggle it from the Scene view's Overlays menu) draws live simulation
state: blocked nav cells, cells the local team can see, occupied spatial-index cells, unit paths and attack targets.
It also shows each AI player's next think time. Each toggle is a `DebugLayer`; subclass it (or `CellDebugLayer`
for grid cells) to add one ([`PlayMode/DebugDraw/`](../Packages/com.hyperrts.engine/Editor/PlayMode/DebugDraw/)).
Layers draw the authoritative world (the server when hosting) unless they override `Source` with
`SimulationWorlds.Presented` for client-only state, as the fog layer does for the local fog view. The tools read the
running worlds directly and live in the Editor assembly, so nothing ships with the game.

## Prefabs

- **`Packages/com.hyperrts.engine/Prefabs/RTSWorld.prefab`**: the playable rig. A Main Camera with `CameraController`, the nested
  `HUD`, and `OverrideAutomaticNetcodeBootstrap` so single-player uses one local world.
- **`Packages/com.hyperrts.engine/Prefabs/UI/HUD.prefab`**: `PanelRenderer` + `HUDController` (UI Toolkit HUD, styled by
  `HUD.uss`), `OverlayRenderer`, `FogOfWarRenderer` and the drag-box marquee.
- **`Assets/Demo/Prefabs/`**: complete unit, building and map prefabs from the sample game, to copy as templates.

## UI Toolkit: PanelRenderer

The HUD uses **`PanelRenderer`** (Unity 6.5+), the successor to `UIDocument`. It doesn't expose
`rootVisualElement`; get the root through `RegisterUIReloadCallback((panelRenderer, root, version) => …)` and
rebuild content idempotently in the callback, skipping a `version` already handled (it can fire more than once).
See [`HUDController.cs`](../Packages/com.hyperrts.engine/Presentation/HUD/HUDController.cs).
