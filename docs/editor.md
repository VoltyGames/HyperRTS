# Editor

Menus, authoring conventions, validation and debug tools. All editor UI is UI Toolkit; the only IMGUI left is
Scene view `Handles`, which have no UI Toolkit equivalent.

## Menus

| Menu | Does |
| --- | --- |
| **HyperRTS ▸ Create RTS Scene...** | Window: map size, player count (1 human, the rest AI), optional starting-base prefab. Builds light, ground, `RTSWorld` rig, `MatchState` ghost and a SubScene with the `Match` |
| **HyperRTS ▸ Validate** | Setup problems in every authoring prefab and the open scenes, with quick fixes; click an object name to select it |
| **HyperRTS ▸ Catalog** | Stats tab: every unit and building prefab in one table, edit HP, build time, vision, speed, damage, cooldown, range; DPS and cost update live. Tech Tree tab: requires, made by, makes, unlocks |
| **HyperRTS ▸ Cheats** | Play mode: add resources, instant build, fog toggle, spawn any prefab at the view, control another player (single player), game speed. Acts on the authoritative world |
| **HyperRTS ▸ Network** | Play mode: host, join localhost, dedicated server, stop |
| **HyperRTS ▸ Replays** | Save the Play-mode recording, or play a replay file |
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
position before saving it.

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
