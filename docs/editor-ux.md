# Editor UX and authoring conventions

How HyperRTS presents itself in the Unity Editor, and the tools that remove manual wiring. Goal: anyone building a
game on the engine gets a clean Add Component menu, helpful inspectors and one-click setup, and new components
inherit the same polish.

## Menus

| Menu | What it does |
| --- | --- |
| **HyperRTS ▸ Create RTS Scene...** | New scene with light, ground, `RTSWorld` rig and a SubScene containing a `Match` ([`RTSSceneWizard.cs`](../Assets/Modules/Editor/RTSSceneWizard.cs)) |
| **HyperRTS ▸ Documentation** | Opens the getting-started guide or the module reference |
| **HyperRTS ▸ Tools ▸ Generate Component Icons** | Regenerates the per-module icons in `Assets/Modules/Editor/Icons/` |
| **GameObject ▸ HyperRTS ▸ RTS World (Camera + HUD)** | Drops the rig prefab |
| **GameObject ▸ HyperRTS ▸ Match / Unit / Building / Resource Node / Nav Obstacle** | Ready-to-bake templates: a root with collider and authoring, and a scaled primitive `Model` child to replace with your mesh ([`HyperRTSObjectMenu.cs`](../Assets/Modules/Editor/HyperRTSObjectMenu.cs)) |
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
- **Gizmos** on selection show what a number means: vision and radius (`Unit`), footprint (`Building`,
  `Nav Obstacle`), weapon range, spawn point (`Producer`), map bounds (`Match`).
- **Paths are centralised** in [`HyperRTSMenu.cs`](../Assets/Modules/Core/HyperRTSMenu.cs) (`HyperRTSMenu`,
  `HyperRTSIcons`, `HyperRTSDocs`). A new module adds one line to `HyperRTSMenu` and `HyperRTSIcons`, plus an accent
  colour in the icon generator.
- **Shared setup.** Bakers write components through the `*Setup` helpers and an `IComponentSink`, so tests and
  tools build the same entities as baking.

## Inspectors

[`GameEntityAuthoringEditor`](../Assets/Modules/Editor/Authoring/GameEntityAuthoringEditor.cs) (units and
buildings) warns when there is no collider (can't be clicked), no renderer (invisible), a cost entry without a
resource type, or the object sits outside a SubScene (won't bake).

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
