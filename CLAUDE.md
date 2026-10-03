# CLAUDE.md

**HyperRTS** is a reusable RTS engine on Unity DOTS, not a game. Prefer generic, data-driven code over
game-specific code. `Assets/ThirdParty/RTS Engine/` is a MonoBehaviour asset kept as a feature reference only:
don't edit it or copy its architecture.

## Environment

- Unity 6000.6.4f1, URP 17.6, Input System only (no `UnityEngine.Input`).
- DOTS core packages (Entities, Entities Graphics, Physics, Netcode, Collections, Burst, Mathematics) track the
  editor version (6.6).

## Layout

`Assets/Modules/` is the engine (code + shipped assets), split into layered assemblies so the simulation can
run headless: `Core ← Simulation ← {Presentation, Input}`, with Editor/Tests on top. The sample game is
`Assets/Demo/`. See [`docs/architecture.md`](docs/architecture.md).

```text
Assets/Modules/
├── Core/          contracts: SystemGroups, IEntityFactory, HyperRTSMenu
├── Simulation/    components, systems, authoring, factories, Tests/ (Attack, Buildings, Health, Resources, Selection, Units)
├── Presentation/  rendering (URP) + UI
├── Input/         camera, input → ECS bridge
├── Editor/        editor tools and menus
└── Prefabs/       Unit, Building, RTSWorld rig, SelectionUI
```

`HyperRTS.Simulation` must never reference Graphics, InputSystem or UIElements. Data shared across layers
(e.g. `SelectionDragState`) lives in Simulation.

## Conventions

- Namespaces: `HyperRTS.<Layer>.<Module>`; contracts stay in `HyperRTS.Core`.
- An authoring MonoBehaviour, its nested `Baker` and its component struct share one `*Authoring.cs` file.
  Bakers use `GetEntity(TransformUsageFlags.Dynamic)`.
- Authoring classes carry `[AddComponentMenu]`, `[Icon]`, `[HelpURL]`, `[DisallowMultipleComponent]` and a
  `[Tooltip]` per field. Paths come from `HyperRTSMenu.cs`. See [`docs/editor-ux.md`](docs/editor-ux.md).
- Component fields PascalCase; authoring fields camelCase.
- Systems are `[BurstCompile] partial struct : ISystem`, placed in a phase group from `SystemGroups.cs`
  (never `SimulationSystemGroup` directly). Per-entity work goes in Burst `IJobEntity` jobs: `ScheduleParallel`,
  or `Schedule` when writing other entities through a `ComponentLookup`.
- Components are unmanaged: `FixedStringNNBytes` for strings, `UnityObjectRef<T>` for Unity objects. Managed
  components are deprecated in Entities 6.6.
- Runtime toggles (orders, construction, selection) are `IEnableableComponent`s flipped with `EnabledRefRW<T>`.
  Pair it with `ref T`, never `in T`. Use `WithPresent<T>` to also visit disabled entities.
- Structural changes go through an `EntityCommandBuffer` (`EndSimulationEntityCommandBufferSystem` from jobs).
  `ecb.CreateEntity` returns the real entity at record time.
- Time comes from `SystemAPI.Time`, never `UnityEngine.Time`.

## Comments

- One-line `<summary>` on public types and non-obvious members. Let names explain the rest.
- Comment *why*, never *what*. No comments that restate the code, narrate changes, or explain C#/Unity basics.
- No `<param>`/`<returns>` boilerplate, multi-paragraph remarks, or per-field docs on self-explanatory fields.
- Keep each comment to one or two short lines. Delete stale comments when the code changes.

## LOC

- Files ≤ 300 lines, methods ≤ 40 lines. Split by responsibility when a file grows past that.
- One type per file, except the authoring + baker + component trio.
- Prefer deleting code to adding it. No dead code, unused usings or commented-out code.

## Run / test

Play `Assets/Demo/Scenes/SampleScene.unity` (entities bake from its SubScene); inspect via Window ▸ Entities.
EditMode tests are in `HyperRTS.Simulation.Tests` (Window ▸ General ▸ Test Runner).

## Gotchas

- Enter Play Mode skips domain reload (CoreCLR-ready), so statics survive between sessions. Reset runtime
  statics with `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` or keep state in ECS. Treat `UAC*`
  analyzer diagnostics as errors.
- Factory entities have no mesh. Bake a prefab from a SubScene to render.
- `ResourceManager` is a static editor-time registry, not Burst-safe. Runtime data lives in `Resource`.

## Docs

- [`world-setup`](docs/world-setup.md) - entity entry and system order
- [`editor-ux`](docs/editor-ux.md) - authoring conventions and editor tools
- [`selection`](docs/selection.md) - selection pipeline
- [`roadmap`](docs/roadmap.md) - check phase status before new work
