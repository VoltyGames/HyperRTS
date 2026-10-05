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
├── Core/          contracts: SystemGroups, HyperRTSMenu/Icons/Docs
├── Simulation/    headless gameplay: AI, Buildings, Combat, Common, Interaction, Match, Navigation, Orders,
│                  Resources, Selection, Spatial, Units, Vision, Tests/
├── Presentation/  team colours, overlays, fog rendering, UI Toolkit HUD, Tests/
├── Input/         camera, input actions, input → PlayerCommand bridge
├── Editor/        validator, inspectors + handles, templates, catalog, debug/cheats, scene wizard, Tests/
└── Prefabs/       RTSWorld rig (camera + HUD), UI/HUD
```

`HyperRTS.Simulation` must never reference Graphics, InputSystem or UIElements. Data shared across layers
(e.g. `PlacementState`, `PointerState`, `SelectionDragState`) lives in Simulation. Module map and contracts:
[`docs/modules.md`](docs/modules.md).

## Gameplay contract

- All player intent (input or AI) is a `PlayerCommand` on the player entity, consumed in `OrderSystemGroup`.
- Orders go through `OrderWriter`; the system owning an order type disables `ActiveOrder` when done.
- Move a unit by setting and enabling `MoveDestination`. Combat owns movement while `AttackTarget` is enabled.
- Ownable entities carry `Faction` (0 = neutral); hostility comes from `FactionRelations`.
- Spawn by instantiating baked entity prefabs, then set `LocalTransform` and `Faction`.

## Conventions

- Namespaces: `HyperRTS.<Layer>.<Module>`; contracts stay in `HyperRTS.Core`.
- An authoring MonoBehaviour, its nested `Baker` and its component struct share one `*Authoring.cs` file.
  Bakers use `GetEntity(TransformUsageFlags.Dynamic)` and write component sets through the `*Setup` helpers
  (`IComponentSink`), so tests build the same entities.
- Authoring classes carry `[AddComponentMenu]`, `[Icon]`, `[HelpURL]`, `[DisallowMultipleComponent]` and a
  `[Tooltip]` per field. Paths come from `HyperRTSMenu.cs`. See [`docs/editor-ux.md`](docs/editor-ux.md).
- Component fields PascalCase; authoring fields camelCase.
- Acronyms are all-caps in identifiers, files and folders (`RTS`, `HUD`, `UI`, `AI`); USS class names stay
  lowercase kebab-case (`hud-root`).
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
EditMode tests are in `HyperRTS.Simulation.Tests`, `HyperRTS.Presentation.Tests` and `HyperRTS.Editor.Tests`
(Window ▸ General ▸ Test Runner). Simulation tests use `TestWorld` and go end-to-end through systems.
New authoring rules go in `AuthoringChecks` so the inspector, **HyperRTS ▸ Validate** and the tests all pick them up.
In Play mode, the Scene view's **HyperRTS Debug** overlay and **HyperRTS ▸ Cheats** inspect and drive the live world.

## Gotchas

- Enter Play Mode skips domain reload (CoreCLR-ready), so statics survive between sessions. Reset runtime
  statics with `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` or keep state in ECS. Treat `UAC*`
  analyzer diagnostics as errors.
- Entities built with the `*Setup` helpers have no mesh. Rendered spawns instantiate baked prefabs.
- SubScene entities stream in over the first frames: guard on singletons (`RequireForUpdate`) and never treat
  "nothing exists yet" as a game state (see `VictorySystem`).
- Keep `OverrideAutomaticNetcodeBootstrap` (on `RTSWorld.prefab`) in single-player scenes, or Netcode replaces
  the default world with client/server worlds.
- Entity type identity (`EntityInfo.TypeId`) is a hash of the display name: two prefab types must not share one.

## Docs

- [`getting-started`](docs/getting-started.md) - building a game on the engine
- [`modules`](docs/modules.md) - module reference and contracts
- [`world-setup`](docs/world-setup.md) - entity entry, system order, tests
- [`editor-ux`](docs/editor-ux.md) - authoring conventions and editor tools
- [`selection`](docs/selection.md) - selection pipeline
- [`roadmap`](docs/roadmap.md) - check phase status before new work
