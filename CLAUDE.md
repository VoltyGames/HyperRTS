# CLAUDE.md

**HyperRTS** is a reusable RTS engine on Unity DOTS, not a game. Prefer generic, data-driven code over
game-specific code. This repo is public: no third-party assets go into it.

## Environment

- Unity 7000.0.0a11, the CoreCLR editor, and URP 17.7.
- Input System only. Never `UnityEngine.Input`.
- The DOTS packages track the editor version, 6.7: Entities, Entities Graphics, Physics, Collections, Burst and
  Mathematics. Netcode for Entities is its own package again, 7.1, in the `Unity.Netcode` namespace.

## Layout

The engine is an embedded UPM package in `Packages/com.hyperrts.engine/`. Games consume it as a package, so engine
code never depends on anything under `Assets/`. `Assets/Demo/` is the sample game.

| Folder | Holds |
| --- | --- |
| `Core/` | Contracts: `SystemGroups`, `SimulationWorlds`, `HyperRTSMenu`, `HyperRTSIcons`, `HyperRTSDocs`. Part of `HyperRTS.Simulation` |
| `Simulation/` | Headless gameplay, one folder per module, with `Common` as the base |
| `Client/` | Client state that input, HUD and overlays share, under `Interaction/`. Part of `HyperRTS.Presentation` |
| `Presentation/` | Team colours, overlays, rendering, fog, the UI Toolkit HUD, audio playback |
| `Input/` | Camera, shared input actions, the bridge from input to `PlayerCommand` |
| `Network/` | Sessions, join, command RPCs, replication, relevancy, cues. See [networking](docs/networking.md) |
| `Editor/` | Tools window, generators, inspectors, validation, templates, catalog, Play-mode tools, Map Editor |
| `Prefabs/` | The `RTSWorld` rig with camera and HUD, the `MatchState` ghost, HUD UI |

Assemblies are layered so the simulation runs headless. `Network` and `Presentation` build on `Simulation`,
`Input` builds on `Presentation`, and Editor and Tests sit on top. `HyperRTS.Simulation` never references
Graphics, InputSystem or UIElements. A headless server never runs `Client/`. See
[architecture](docs/architecture.md) and the [module map](docs/modules.md).

## Gameplay contract

- All player intent, from input or AI, is a `PlayerCommand` on the player entity, consumed in `OrderSystemGroup`.
- Orders go through `OrderWriter`. The system that owns an order type disables `ActiveOrder` when it is done.
- To move a unit, set and enable `MoveDestination`. Combat owns movement while `AttackTarget` is enabled.
- Ownable entities carry `Faction`, where 0 is neutral. Hostility comes from `FactionRelations`.
- Spawn by instantiating a baked entity prefab, then `Place` it and set `Faction`. `Place` also writes
  `LocalToWorld`, since command buffers play back after the transform systems.

## Conventions

- Namespaces follow the folder path: `HyperRTS.<Layer>.<Module>`. Contracts in `Core/` use `HyperRTS.Core`.
- Simulation modules sit in [tiers](docs/modules.md#tiers): Base, Foundation, Gameplay, Orchestration. A module
  uses its own tier or lower ones, and modules in one tier never form a cycle. A component two modules share goes
  in `Common` or the lower module. Authoring files may reference any module. `ModuleLayoutTests` enforces this,
  and a new module needs an entry in its `Tiers`.
- Authoring classes, game ones included, derive from `AuthoringBehaviour` for inspector warnings, summaries and
  validation. One `*Authoring.cs` holds the class and its nested `Baker`. Runtime components live in their own
  files. `ModuleLayoutTests` enforces this too.
- Bakers use `GetEntity(TransformUsageFlags.Dynamic)` and write components through `IEntityWriter`, with a static
  `Add…` on the authoring class or a shared `*Setup` when several authorings need it. Tests then build the same
  entities.
- Authoring classes carry `[AddComponentMenu]`, `[Icon]`, `[HelpURL]`, `[DisallowMultipleComponent]` and a
  `[Tooltip]` per field, with paths from `HyperRTSMenu`, `HyperRTSIcons` and `HyperRTSDocs`. See
  [editor](docs/editor.md).
- Component fields are PascalCase, authoring fields camelCase.
- Acronyms are all caps in identifiers, files and folders: `RTS`, `HUD`, `UI`, `AI`. USS class names stay
  lowercase kebab-case, such as `hud-root`.
- Simulation systems are `[BurstCompile] partial struct : ISystem` in a phase group from `SystemGroups.cs`. Never
  put one in `SimulationSystemGroup` directly. Only network plumbing such as join, relevancy and lobby sits there.
- Systems that drive managed objects, such as presentation, input, lobby, one-shot setup and network plumbing, may
  be `SystemBase` or an `ISystem` without Burst.
- Per-entity work goes in Burst `IJobEntity` jobs. Use `ScheduleParallel`, or `Schedule` when the job writes other
  entities through a `ComponentLookup`.
- Components are unmanaged. Use `FixedStringNNBytes` for strings and `UnityObjectRef<T>` for Unity objects. Managed
  components are deprecated in Entities 6.6.
- Runtime toggles such as orders, construction and selection are `IEnableableComponent`s flipped with
  `EnabledRefRW<T>`. Pair it with `ref T`, never `in T`. Use `WithPresent<T>` to also visit disabled entities.
- Structural changes go through an `EntityCommandBuffer`, from jobs through
  `EndSimulationEntityCommandBufferSystem`. `ecb.CreateEntity` returns the real entity at record time.
- Systems take time from `SystemAPI.Time`, never `UnityEngine.Time`. UI and MonoBehaviours may use
  `UnityEngine.Time`.
- UI is UI Toolkit everywhere: HUD, menus, world-space UI, inspectors, property drawers, editor windows and
  overlays. Use uGUI only for UI that needs Animation Clips or Timeline. No IMGUI. Scene view `Handles` are the one
  exception, since UI Toolkit has no equivalent. Per-unit markers such as health bars and rings are instanced
  meshes, not UI.
- Layout lives in UXML beside its script, same name (`MapsPage.uxml` beside `MapsPage.cs`). Code only looks
  elements up and wires them, and builds just rows from data (from row templates) and Painter2D drawing.
- Name a method after what it returns or does, like `TierIndex` or `PlaybackControl`. Never a bare `Of` or `XOf`.
- Readable predicates: no long multi-line `&&` or `||` chains. Write a block body with an early `return` per rule
  and name intermediate facts with locals. Short one- or two-term `=>` bodies are fine.
- No fallback code: no defaults for missing data, no readers for old formats, no compatibility overloads. Require
  the data the design guarantees and fail loudly with a message naming the bad key. A format change bumps its
  version and rejects older ones. Tests set up what the real pipeline provides.

## Comments

- A one-line `<summary>` on public types and non-obvious members. Names explain the rest.
- Comment why, never what. No comments that restate the code, narrate changes or explain C# and Unity basics.
- No `<param>` or `<returns>` boilerplate, no multi-paragraph remarks, no docs on self-explanatory fields.
- One or two short lines per comment. Delete stale comments when the code changes.
- No narrative: no history, no walkthroughs of behaviour, no clauses chained with semicolons. File headers are a
  line or two.
- Do a comment pass over every touched file before committing.

## Size

- Files stay under 300 lines and methods under 40. Split by responsibility when one grows past that.
- One type per file. Exceptions: an authoring class with its baker, and a feature's small unmanaged components and
  tags, which may share one file named after the feature, such as `HarvesterComponents.cs`. Systems,
  MonoBehaviours and ScriptableObjects are always one per file.
- Prefer deleting code to adding it. No dead code, unused usings or commented-out code.

## Run and test

- Play `Assets/Demo/Scenes/SampleScene.unity`. Its entities bake from a SubScene. Inspect them in
  **Window ▸ Entities**.
- EditMode tests live in `HyperRTS.Simulation.Tests`, `HyperRTS.Presentation.Tests` and `HyperRTS.Editor.Tests`.
  Simulation tests use `TestWorld` and run end to end through the systems.
- A new authoring rule is an `AuthoringRule<T>` in `Editor/Validation/Rules/`. The inspector,
  **HyperRTS ▸ Tools ▸ Validate** and the tests all pick it up.
- In Play mode, the Scene view's **HyperRTS Debug** overlay and **HyperRTS ▸ Tools ▸ Play** inspect and drive the
  live world.

## Gotchas

- Enter Play Mode skips domain reload, so statics survive between sessions. Mark session state
  `[AutoStaticsCleanup]` and immutable tables `[NoAutoStaticsCleanup]`. Put side effects such as shader globals or
  disposal in an `[OnEnteringPlayMode]` method. Types that use them are `partial`. Or keep the state in ECS. Treat
  `UAC*` diagnostics and Project Auditor's Domain Reload (`UAL*`) findings as errors.
- Entities built through `IEntityWriter` in tests have no mesh. Rendered spawns instantiate baked prefabs.
- SubScene entities stream in over the first frames. Guard on singletons with `RequireForUpdate`, and never treat
  "nothing exists yet" as a game state. `VictorySystem` shows how.
- Keep `OverrideAutomaticNetcodeBootstrap`, on `RTSWorld.prefab`, in scenes. `NetworkSession` makes the worlds:
  - single player is one local world;
  - a host is one Netcode single-world host, server and client at once;
  - a dedicated server is a server world, and a joining player a client world.
- A client system that mirrors a remote server's state, such as sending commands or receiving cues, carries
  `[RemoteClientOnly]`, so a host world leaves it out. Senders skip connections without `NetworkStreamConnection`,
  which is the host's own player.
- Systems default to the server and local worlds. Client-side systems opt in with
  `[WorldSystemFilter(SimulationWorlds.Presented)]`. A component the HUD reads needs `[GhostField]`s.
- Systems under `PresentationSystemGroup` already default to the presented worlds, local and client, so they need
  no `[WorldSystemFilter]`.
- `EntityInfo.TypeId` is a hash of the display name, so two prefab types must never share a name.

## Docs

| Doc | Covers |
| --- | --- |
| [Getting started](docs/getting-started.md) | First skirmish in the editor, controls |
| [Architecture](docs/architecture.md) | Assemblies, module rules, the gameplay contract, worlds, frame order |
| [Modules](docs/modules.md) | Module tiers, components and systems per module |
| [Extending](docs/extending.md) | Game code, HUD, AI, front-end integration |
| [Networking](docs/networking.md) | Sessions, join, commands, replication, fog relevancy |
| [Editor](docs/editor.md) | Tools window, generators, menus, authoring conventions, validation, debug tools |
| [Maps](docs/maps.md) | Map Editor, spec format, build pipeline, map library. Code in [map tools](docs/modules/map-tools.md) |
| [Testing](docs/testing.md) | `TestWorld`, test kits, validation in CI |
| [Roadmap](docs/roadmap.md) | Status and open work. Check it before starting new work |
