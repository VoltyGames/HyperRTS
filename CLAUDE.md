# CLAUDE.md

**HyperRTS** is a reusable RTS engine on Unity DOTS, not a game. Prefer generic, data-driven code over
game-specific code. `Assets/ThirdParty/RTS Engine/` is a MonoBehaviour asset kept as a feature reference only:
don't edit it or copy its architecture.

## Environment

- Unity 6000.6.4f1, URP 17.6, Input System only (no `UnityEngine.Input`).
- DOTS core packages (Entities, Entities Graphics, Physics, Netcode, Collections, Burst, Mathematics) track the
  editor version (6.6).

## Layout

`Packages/com.hyperrts.engine/` is the engine (code + shipped assets) as an embedded UPM package, split into layered
assemblies so the simulation can run headless: `Core ← Simulation ← {Network, Presentation, Input}`, with
Editor/Tests on top. The sample game is `Assets/Demo/`. Games consume the package (e.g. as a submodule + `file:` reference), so engine
code must never depend on anything under `Assets/`. See [`docs/architecture.md`](docs/architecture.md).

```text
Packages/com.hyperrts.engine/
├── Core/          contracts: SystemGroups, SimulationWorlds, HyperRTSMenu/Icons/Docs
├── Simulation/    headless gameplay, one folder per module: Common (base), AI, Abilities, Air, Audio, Buildings,
│                  Capture, Combat, Commands, Fields, GameEntities, Interaction, Match, Navigation, Orders, Power,
│                  Production, Replays, Resources, Selection, Spatial, Stats, Transport, Units, Upgrades,
│                  Veterancy, Vision, Tests/
├── Presentation/  team colours, overlays, rendering helpers, fog, UI Toolkit HUD, audio playback, Tests/
├── Input/         camera, shared input actions, input → PlayerCommand bridge
├── Network/       Session, Players (join), Commands (RPCs), Replication, Relevancy, Audio (docs/networking.md)
├── Editor/        Authoring (inspectors, handles), Validation, Templates, Catalog, PlayMode (debug draw, cheats),
│                  Common, scene wizard, Tests/
└── Prefabs/       RTSWorld rig (camera + HUD), MatchState ghost, UI/HUD
```

`HyperRTS.Simulation` must never reference Graphics, InputSystem or UIElements. Client UI state that input, HUD
and overlays share (`PlacementState`, `PointerState`, `SelectionDragState`, `CameraFocusRequest`) lives in
`Simulation/Interaction`. Module map and contracts: [`docs/modules.md`](docs/modules.md).

## Gameplay contract

- All player intent (input or AI) is a `PlayerCommand` on the player entity, consumed in `OrderSystemGroup`.
- Orders go through `OrderWriter`; the system owning an order type disables `ActiveOrder` when done.
- Move a unit by setting and enabling `MoveDestination`. Combat owns movement while `AttackTarget` is enabled.
- Ownable entities carry `Faction` (0 = neutral); hostility comes from `FactionRelations`.
- Spawn by instantiating baked entity prefabs, then set `LocalTransform` and `Faction`.

## Conventions

- Namespaces follow the folder path (`HyperRTS.<Layer>.<Module>`); contracts stay in `HyperRTS.Core`.
- Simulation modules are layered (order in `docs/modules.md`): a module uses only lower ones, and `Common` is the
  base. Put a component two modules share in `Common` or the lower module; authoring files may reference any
  module. `ModuleLayoutTests` enforces this; a new module needs a place in its `Layers`, and don't grow its
  back-edge allowlist casually.
- Authoring classes derive from `AuthoringBehaviour` (game ones too, for inspector warnings, summaries and
  validation). One `*Authoring.cs` file holds the authoring class and its nested `Baker`; runtime components live
  in their own files (`ModuleLayoutTests` enforces this).
  Bakers use `GetEntity(TransformUsageFlags.Dynamic)` and write component sets through the `*Setup` helpers
  (`IEntityWriter`), so tests build the same entities.
- Authoring classes carry `[AddComponentMenu]`, `[Icon]`, `[HelpURL]`, `[DisallowMultipleComponent]` and a
  `[Tooltip]` per field, with paths from `HyperRTSMenu`, `HyperRTSIcons` and `HyperRTSDocs`. See
  [`docs/editor.md`](docs/editor.md).
- Component fields PascalCase; authoring fields camelCase.
- Acronyms are all-caps in identifiers, files and folders (`RTS`, `HUD`, `UI`, `AI`); USS class names stay
  lowercase kebab-case (`hud-root`).
- Systems are `[BurstCompile] partial struct : ISystem`, placed in a phase group from `SystemGroups.cs`
  (never `SimulationSystemGroup` directly; only network plumbing such as join and relevancy sits there).
  Per-entity work goes in Burst `IJobEntity` jobs: `ScheduleParallel`, or `Schedule` when writing other entities
  through a `ComponentLookup`.
- Components are unmanaged: `FixedStringNNBytes` for strings, `UnityObjectRef<T>` for Unity objects. Managed
  components are deprecated in Entities 6.6.
- Runtime toggles (orders, construction, selection) are `IEnableableComponent`s flipped with `EnabledRefRW<T>`.
  Pair it with `ref T`, never `in T`. Use `WithPresent<T>` to also visit disabled entities.
- Structural changes go through an `EntityCommandBuffer` (`EndSimulationEntityCommandBufferSystem` from jobs).
  `ecb.CreateEntity` returns the real entity at record time.
- Time comes from `SystemAPI.Time`, never `UnityEngine.Time`.
- UI is UI Toolkit everywhere: HUD, menus, world-space UI, inspectors, property drawers, editor windows and
  overlays. uGUI only for UI that needs Animation Clips or Timeline. No IMGUI; Scene view `Handles` are the one
  exception (no UI Toolkit equivalent). Mass per-unit markers (health bars, rings) are instanced meshes, not UI.

## Comments

- One-line `<summary>` on public types and non-obvious members. Let names explain the rest.
- Comment *why*, never *what*. No comments that restate the code, narrate changes, or explain C#/Unity basics.
- No `<param>`/`<returns>` boilerplate, multi-paragraph remarks, or per-field docs on self-explanatory fields.
- Keep each comment to one or two short lines. Delete stale comments when the code changes.

## LOC

- Files ≤ 300 lines, methods ≤ 40 lines. Split by responsibility when a file grows past that.
- One type per file, except authoring + baker; runtime components live in their own files.
- Prefer deleting code to adding it. No dead code, unused usings or commented-out code.

## Run / test

Play `Assets/Demo/Scenes/SampleScene.unity` (entities bake from its SubScene); inspect via Window ▸ Entities.
EditMode tests are in `HyperRTS.Simulation.Tests`, `HyperRTS.Presentation.Tests` and `HyperRTS.Editor.Tests`
(Window ▸ General ▸ Test Runner). Simulation tests use `TestWorld` and go end-to-end through systems.
New authoring rules are `AuthoringRule<T>` classes in `Editor/Validation/Rules/` so the inspector, **HyperRTS ▸ Validate** and the tests all pick them up.
In Play mode, the Scene view's **HyperRTS Debug** overlay and **HyperRTS ▸ Cheats** inspect and drive the live world.

## Gotchas

- Enter Play Mode skips domain reload (CoreCLR-ready), so statics survive between sessions. Reset runtime
  statics with `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` or keep state in ECS. Treat `UAC*`
  analyzer diagnostics as errors.
- Entities built with the `*Setup` helpers have no mesh. Rendered spawns instantiate baked prefabs.
- SubScene entities stream in over the first frames: guard on singletons (`RequireForUpdate`) and never treat
  "nothing exists yet" as a game state (see `VictorySystem`).
- Keep `OverrideAutomaticNetcodeBootstrap` (on `RTSWorld.prefab`) in scenes: single player is one local world and
  `NetworkSession` creates the server/client worlds. Systems default to server/local worlds; client-side ones opt in
  with `[WorldSystemFilter(SimulationWorlds.Presented)]`. A component the HUD reads needs `[GhostField]`s.
- Entity type identity (`EntityInfo.TypeId`) is a hash of the display name: two prefab types must not share one.

## Docs

- [`getting-started`](docs/getting-started.md) - first skirmish in the editor, controls
- [`architecture`](docs/architecture.md) - assemblies, module layers, contract, worlds, frame order
- [`modules`](docs/modules.md) - components and systems per module
- [`extending`](docs/extending.md) - game code, HUD, AI, front-end integration
- [`networking`](docs/networking.md) - sessions, join, commands, replication, fog relevancy
- [`editor`](docs/editor.md) - menus, authoring conventions, validation, debug tools
- [`testing`](docs/testing.md) - `TestWorld`, test kits, CI validation
- [`roadmap`](docs/roadmap.md) - status and open work; check before new work
