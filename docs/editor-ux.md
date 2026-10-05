# Editor UX and authoring conventions

How HyperRTS presents itself in the Unity Editor, and the tools that remove manual wiring. Goal: anyone building a
game on the engine gets a clean Add Component menu, helpful inspectors and one-click setup, and new components
inherit the same polish.

## Menus

| Menu | What it does |
| --- | --- |
| **HyperRTS ▸ Create RTS Scene...** | Wizard: map size, player count (1 human, the rest AI) and an optional starting-base prefab placed for each player. Builds light, ground, `RTSWorld` rig and a SubScene with the `Match` ([`RTSSceneWizard.cs`](../Assets/Modules/Editor/RTSSceneWizard.cs)) |
| **HyperRTS ▸ Validate** | Lists setup problems in every HyperRTS prefab and the open scenes; click one to select the object ([`Validation/`](../Assets/Modules/Editor/Validation/)) |
| **HyperRTS ▸ Catalog** | Every unit and building prefab in one table: edit HP, build time, vision, speed, damage, cooldown and range, see DPS and cost. The Tech Tree tab shows what each one requires, is made by, makes and unlocks ([`Catalog/`](../Assets/Modules/Editor/Catalog/)) |
| **HyperRTS ▸ Cheats** | Play mode: add resources, instant build, toggle fog, spawn any entity prefab at the camera's view point, control another player, game speed ([`Debugging/`](../Assets/Modules/Editor/Debugging/)) |
| **HyperRTS ▸ Documentation** | Opens the getting-started guide or the module reference |
| **HyperRTS ▸ Tools ▸ Generate Component Icons** | Regenerates the per-module icons in `Assets/Modules/Editor/Icons/` |
| **GameObject ▸ HyperRTS ▸ RTS World (Camera + HUD)**, **Match** | Drops the rig prefab or a Match |
| **GameObject ▸ HyperRTS ▸ Units / Buildings / Map ▸ …** | Role templates in the scene: Unit, Combat Unit, Worker, Harvester, Building, Producer, Resource Drop-Off, Defense Tower, Resource Node, Nav Obstacle. A root with collider and authoring, and a scaled primitive `Model` child to replace with your mesh ([`Templates/`](../Assets/Modules/Editor/Templates/)) |
| **Assets ▸ Create ▸ HyperRTS ▸ Prefabs ▸ …** | The same role templates saved as prefab assets in the selected folder, ready for producer and builder option lists |
| **Assets ▸ Create ▸ HyperRTS ▸ Resources ▸ Resource Type**, **Combat ▸ Damage Type** | Data assets |

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

## The convention (apply to every authoring MonoBehaviour)

```csharp
[AddComponentMenu(HyperRTSMenu.Combat + "Weapon")]   // category + friendly name
[Icon(HyperRTSIcons.Combat)]                         // per-module icon
[HelpURL(HyperRTSDocs.Modules)]                      // "?" opens the docs
[DisallowMultipleComponent]
public class WeaponAuthoring : MonoBehaviour
{
    [Tooltip("Firing range in world units, measured edge to edge.")]
    [Min(0.1f)]
    public float range = 6f;
}
```

- **`[Tooltip]`** on every serialized field, **`[Header]`** to group long components, `[Min]`/`[Range]` to stop
  invalid values.
- **Scene handles** on selection show what a number means and let you drag it: vision and radius (`Unit`), footprint
  (`Building`, `Nav Obstacle`), weapon reach (range plus the owner's radius, as combat measures it), spawn point
  (`Producer`) and map bounds (`Match`). Colours follow the owner's player colour.
- **Paths are centralised** in [`HyperRTSMenu.cs`](../Assets/Modules/Core/HyperRTSMenu.cs) (`HyperRTSMenu`,
  `HyperRTSIcons`, `HyperRTSDocs`). A new module adds one line to `HyperRTSMenu` and `HyperRTSIcons`, plus an accent
  colour in the icon generator.
- **Shared setup.** Bakers write components through the `*Setup` helpers and an `IComponentSink`, so tests and
  tools build the same entities as baking.

## Validation

[`AuthoringChecks`](../Assets/Modules/Editor/Validation/AuthoringChecks.cs) holds the per-component rules (no
collider, empty or scene-object production options, unit prerequisites, spawn point inside the footprint, node
without a resource type, ...). [`ProjectValidator`](../Assets/Modules/Editor/Validation/ProjectValidator.cs) adds
the cross-object rules: two prefabs sharing a display name (they would merge into one `TypeId`), zero or several
Matches, owners without a player slot, entities outside the map, and a scene missing
`OverrideAutomaticNetcodeBootstrap`. The same rules show up in four places:

- under each HyperRTS component in the inspector ([`AuthoringEditor`](../Assets/Modules/Editor/Authoring/AuthoringEditor.cs));
- in **HyperRTS ▸ Validate**;
- in the console, linked to the object, when entering Play mode (open scenes only);
- in `ValidationTests.ProjectHasNoValidationErrors`, so CI fails on errors.

Add a rule to `AuthoringChecks.For` and every one of these picks it up. Closed SubScenes aren't checked.

## Inspectors

Every HyperRTS authoring component draws its default inspector plus its validation warnings. Warnings with an
obvious fix carry a button (**Fit Collider**, **Use Prefab**, **Remove Empty**, **Move Outside**), also shown in
**HyperRTS ▸ Validate**; each fix is one undo step ([`QuickFixes`](../Assets/Modules/Editor/Validation/QuickFixes.cs)).

Unit and building inspectors open with a one-line summary (for example "Unit · Weapon, Builder · 12 DPS · 150
Supplies") and show **Owner** as a dropdown of the scene Match's players with their colour. The **Match** inspector
counts entities per player in the open scenes, frames the map in the Scene view, and previews the nav and fog grids.

To customise one field, override `AuthoringEditor.DrawProperty` and leave the rest to the default drawing.

## Play-mode debugging

The **HyperRTS Debug** Scene view overlay (toggle it from the Scene view's Overlays menu) draws live simulation
state: blocked nav cells, cells the local team can see, occupied spatial-index cells, unit paths and attack targets.
It also shows each AI player's next think time. The tools read the running world directly and live in the Editor
assembly, so nothing ships with the game.

## Prefabs

- **`Assets/Modules/Prefabs/RTSWorld.prefab`**: the playable rig. A Main Camera with `CameraController`, the nested
  `HUD`, and `OverrideAutomaticNetcodeBootstrap` so single-player uses one local world.
- **`Assets/Modules/Prefabs/UI/HUD.prefab`**: `PanelRenderer` + `HUDController` (UI Toolkit HUD, styled by
  `HUD.uss`), `OverlayRenderer`, `FogOfWarRenderer` and the drag-box marquee.
- **`Assets/Demo/Prefabs/`**: complete unit, building and map prefabs from the sample game, to copy as templates.

## UI Toolkit: PanelRenderer

The HUD uses **`PanelRenderer`** (Unity 6.5+), the successor to `UIDocument`. It doesn't expose
`rootVisualElement`; get the root through `RegisterUIReloadCallback((panelRenderer, root, version) => …)` and
rebuild content idempotently in the callback, skipping a `version` already handled (it can fire more than once).
See [`HUDController.cs`](../Assets/Modules/Presentation/HUD/HUDController.cs).
