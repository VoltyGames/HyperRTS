# CLAUDE.md

## Project

**HyperRTS** - a reusable **RTS engine built on Unity DOTS** (Entities/ECS), to later
build games on top of. It's an engine-in-progress, not a game: prefer generic,
data-driven pieces over game-specific code.

`Assets/ThirdParty/RTS Engine/` is a commercial MonoBehaviour-based asset kept as a
**feature reference only** - don't edit it or copy its (non-DOTS) architecture.

## Environment

- **Unity 6000.5.0f1** (6.5), URP 17.5, new Input System (no legacy `UnityEngine.Input`).
- **DOTS:** Entities 6.5, Entities Graphics, Unity Physics, Netcode for Entities, Collections,
  Burst, Mathematics. (Entities `6.x` tracks the editor version; it's the successor to `1.x`,
  same APIs.)

## Layout

`Assets/Modules/` **is the engine** - code *and* its shipped assets - split into **layered assemblies**
(not per-module) so the simulation can run headless. The sample game lives outside it in `Assets/Demo/`.
Dependency direction: `Core ← Simulation ← {Presentation, Input}`, with Editor/Tests on top.
Details in [`docs/architecture.md`](docs/architecture.md).

```text
Assets/
├── Modules/           # the engine (code + shipped assets)
│   ├── Core/          # HyperRTS.Core - contracts: SystemGroups, IEntityFactory, HyperRTSMenu
│   ├── Simulation/    # HyperRTS.Simulation - components, systems, factories, authoring; modules:
│   │                  #   Attack/ Buildings/ Health/ Resources/ Units/ Selection/ Tests/
│   ├── Presentation/  # HyperRTS.Presentation - rendering (URP) + UI
│   ├── Input/         # HyperRTS.Input - CameraController, input→ECS bridge, RTSInputActions
│   ├── Editor/        # HyperRTS.Editor - editor-only tooling + GameObject ▸ HyperRTS menus
│   └── Prefabs/       # Unit/Building + RTSWorld (camera + UI rig) + UI/ (SelectionUI + PanelSettings)
└── Demo/              # sample game built on the engine: scene + EntitiesSubScene (no asmdef)
```

**Invariant:** `HyperRTS.Simulation` must never reference Graphics, InputSystem, or UIElements.
Cross-boundary data (e.g. `SelectionHighlightColors`, `SelectionDragState`) lives in Simulation;
only the render override / UI / input bridge sit above it.

## Conventions

- Namespaces follow the layer: `HyperRTS.<Layer>.<Module>` (contracts stay flat in `HyperRTS.Core`).
  A feature like Selection spans layers (`Simulation`/`Presentation`/`Input`).
- Authoring class + nested `Baker` + the `IComponentData` struct share one `*Authoring.cs`
  file (see `HealthComponentAuthoring.cs`), in `Simulation/` beside its systems. Bakers use
  `GetEntity(TransformUsageFlags.Dynamic)`.
- Component fields PascalCase; authoring MonoBehaviour fields camelCase.
- Authoring MonoBehaviours carry editor metadata: `[AddComponentMenu(HyperRTSMenu.<Module> + "Name")]`,
  `[Icon(HyperRTSIcons.<Module>)]`, `[HelpURL(...)]`, `[DisallowMultipleComponent]`, and `[Tooltip]` on each
  serialized field (group many fields with `[Header]`). Menu/icon/doc paths come from `HyperRTSMenu.cs` -
  the single source of truth. See [`docs/editor-ux.md`](docs/editor-ux.md).
- Systems: `[BurstCompile] partial struct …System : ISystem`, auto-discovered (don't register
  manually). Put each in a phase group from `SystemGroups.cs` via
  `[UpdateInGroup(typeof(<Phase>SystemGroup))]` - not `SimulationSystemGroup` directly.
- Keep components blittable (Burst-safe): unmanaged only, strings as `FixedStringNNBytes`.
- Structural changes go through an `EntityCommandBuffer`; cross-entity access via
  `ComponentLookup<T>`; timing via `SystemAPI.Time` (never `UnityEngine.Time`).

## Systems

- `MovementSystem` - moves entities with `MoveDestination` at `MovementSpeed`, clears order on arrival.
- `AttackSystem` - on `AttackCooldown`, subtracts `Melee`/`RangeAttackDamage` from `AttackTarget`'s health.
- `ConstructionSystem` - advances `ConstructionProgress` (0..1), removes it when complete.
- `DeathSystem` (`LifecycleSystemGroup`, runs last) - destroys entities at `CurrentHealth <= 0`.

Update order is explicit: `SimulationSystemGroup` → Order → Movement → Combat → Production →
Lifecycle (see `SystemGroups.cs` and [`docs/world-setup.md`](docs/world-setup.md)).
These are minimal scaffolds; expect to extend them.

## Run / test

Open in Unity 6000.5.0f1; play `Assets/Demo/Scenes/SampleScene.unity` (entities bake from
its EntitiesSubScene). Inspect via Window ▸ Entities. Edit-mode tests live in `HyperRTS.Simulation.Tests`
- run via Window ▸ General ▸ Test Runner. If the Unity MCP bridge is connected, see console errors via MCP.

## Gotchas

- C# edits need a Unity domain reload (editor open) before Play mode reflects them.
- Factory-created entities have data + `LocalTransform` but **no mesh** - bake a prefab via a
  SubScene to get rendered entities. The `Unit`/`Building` prefabs in `Assets/Modules/Prefabs/` carry a
  mesh, so they bake to rendered entities.
- `ResourceManager` is a static managed registry for editor/setup - not Burst/job-safe; runtime
  resource data lives in the `Resource` component. The Resource Editor's assets live in
  `Assets/ScriptableObjects/Resources`.

## Docs

- [`docs/world-setup.md`](docs/world-setup.md) - how entities enter play and the ordered system
pipeline. Editor/authoring conventions and helper tools are in
- [`docs/editor-ux.md`](docs/editor-ux.md). Add a doc per topic/phase as features land.

## Roadmap

Selection (Physics raycast) → commands/orders → pathfinding → economy → production →
rendering for factory entities → fog of war → factions → multiplayer (Netcode for Entities).

Full phased plan (milestones, deliverables, acceptance criteria) in
[`docs/roadmap.md`](docs/roadmap.md) - check phase status there before new work.
