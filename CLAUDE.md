# CLAUDE.md

HyperRTS is a reusable RTS engine on Unity DOTS, not a game: prefer generic, data-driven code. The repo is public,
so no third-party assets go in, except editor icons under a permissive licence, each listed with its licence in
`Packages/com.hyperrts.engine/Editor/Icons/Credits.md`.

## Layout

The engine is the UPM package `Packages/com.hyperrts.engine/` and never depends on `Assets/`. `Assets/Demo/` is the
sample game.

Assemblies layer so the simulation runs headless: `Network` and `Presentation` build on `Simulation`, `Input` on
`Presentation`, Editor and tests on top. `HyperRTS.Simulation` never references Graphics, InputSystem or
UIElements, and a headless server never runs `Presentation/`. See [architecture](docs/architecture.md) and
[modules](docs/simulation/README.md).

## Gameplay contract

- All player intent, from input or AI, is a `PlayerCommand` on the player entity, consumed in `OrderSystemGroup`.
- Orders go through `OrderWriter`. The system owning an order type disables `ActiveOrder` when it is done.
- To move a unit, set and enable `MoveDestination`. Combat owns movement while `AttackTarget` is enabled.
- Ownable entities carry `Owner` (0 is neutral). Hostility comes from `OwnerRelations`.
- Spawn by instantiating a baked prefab, then `Place` it and set `Owner`. `Place` also writes `LocalToWorld`, since
  command buffers play back after the transform systems.

## Conventions

- Namespaces follow folders: `HyperRTS.<Layer>.<Module>`.
- Simulation modules sit in [tiers](docs/simulation/README.md#tiers): Base, Foundation, Gameplay, Orchestration. A module uses
  its own tier or lower ones, a tier never forms a cycle, and a shared component goes in `Common` or the lower
  module. Authoring files may reference any module. `ModuleLayoutTests` enforces this; add a new module to `Tiers`.
- Authoring classes, game ones too, derive from `AuthoringBehaviour`. One `*Authoring.cs` holds the class and its
  nested `Baker`; runtime components get their own files, which `ModuleLayoutTests` also checks. Each authoring
  class carries `[AddComponentMenu]`, `[Icon]`, `[HelpURL]`,
  `[DisallowMultipleComponent]` and a `[Tooltip]` per field, with paths from `HyperRTSMenu`, `HyperRTSIcons` and
  `HyperRTSDocs` ([editor](docs/editor/README.md)).
- Bakers write through `this.Writer()` (an `EntityWriter` on the baked entity), with a static `Add…` on the
  authoring class or a shared `*Setup` taking `EntityWriter`, so tests build the same entities.
- Component fields are PascalCase, authoring fields camelCase. Acronyms are all caps (`RTS`, `HUD`, `UI`, `AI`); USS
  classes are lowercase kebab-case (`hud-root`).
- Simulation systems are `[BurstCompile] partial struct : ISystem` in a phase group from `SystemGroups.cs`, never in
  `SimulationSystemGroup`, which holds only network plumbing. Presentation, input, lobby, one-shot setup and network
  plumbing may be `SystemBase` or a non-Burst `ISystem`.
- Per-entity work is a Burst `IJobEntity`: `ScheduleParallel`, or `Schedule` when it writes other entities through a
  `ComponentLookup`. Never turn job safety checks off (`ModuleLayoutTests` fails on it): a parallel job writes only
  what it iterates, and parts pull their state from their root.
- Components are unmanaged: `FixedStringNNBytes` for strings, `UnityObjectRef<T>` for Unity objects.
- Runtime toggles (orders, construction, selection) are `IEnableableComponent`s flipped with `EnabledRefRW<T>`,
  paired with `ref T`, never `in T`. `WithPresent<T>` also visits disabled entities.
- Structural changes go through an `EntityCommandBuffer`, from jobs through `EndSimulationEntityCommandBufferSystem`.
  `ecb.CreateEntity` returns the real entity at record time.
- Systems take time from `SystemAPI.Time`, never `UnityEngine.Time`; UI and MonoBehaviours may.
- Gameplay stays in ECS; GameObjects only show it. A MonoBehaviour reads the match through `MatchView` or
  `LiveSingleton<T>` and hands data to systems through `SceneSingleton<T>`.
- Input System only, never `UnityEngine.Input`.
- UI is UI Toolkit everywhere, in UXML beside its script (`MapGallery.uxml` beside `MapGallery.cs`). Code only looks
  elements up, wires them and builds rows from templates or Painter2D. No IMGUI; uGUI only where Animation Clips or
  Timeline need it; Scene view `Handles` and the Project window row decorations (`Editor/Rows/ProjectRows`), which Unity offers
  only in IMGUI, are the two exceptions. Per-unit markers such as health bars are instanced meshes.
- Logic over one component or buffer is a method on that type, or on a small struct wrapping the buffer
  (`Stockpile`, `StatModifiers`). A static class only holds a shared pure function with no owner, constants or
  editor commands.
- Statics hold no match state. Match state lives in its worlds: an ECS singleton (`GameSpeed`), or a
  `SceneSingleton<T>` a MonoBehaviour writes (`GroundWetness`). A session carries its `MatchSetup` in through
  `SessionScene`. Statics may front what Unity keeps per process (`NetworkSession` and the world stack,
  `PresentationQuality`, input devices) and editor caches.
- Shared simulation code in `Simulation/Common` sits in `Geometry`, `Systems` (phase groups, frame, worlds),
  `Combat`, `Buildings`, `Authoring`, `Cells`, `Events`, `Infantry`, `Players` and `Prefabs`; entity helpers stay at
  its root. Avoid folder names that shadow common names in their namespace (`Math`, `Entities`).
- Name a method after what it returns or does (`TierIndex`), never a bare `Of` or `XOf`.
- No long `&&` or `||` chains: a block body with an early `return` per rule and named locals. One- or two-term `=>`
  bodies are fine.
- No fallback code: no defaults for missing data, readers for old formats or compatibility overloads. Require what
  the design guarantees and fail loudly, naming the bad key. Formats stay at version 1 until the first release: a
  change migrates the data in place. Tests set up what the real pipeline provides.

## Comments

- A one-line `<summary>` on public types and non-obvious members; names explain the rest.
- Comment why in one or two short lines, never what. No restated code, history, walkthroughs or `<param>` and
  `<returns>` boilerplate; file headers are a line or two. Delete stale comments, and do a comment pass over every
  touched file before committing.

## Size

- Files under 300 lines, methods under 40, at most 6 parameters (job `Execute` and `[TestCase]` tests aside).
- A type never spans files. Split a big type into types that own their data: a nested job becomes a top-level job
  (`PathEvaluateJob`), methods over some of the fields become a struct holding them (`DriveWindow`), a concern
  becomes its own planner (`AttackPlanner`). Never dotted partial files (`X.Y.cs`); `partial` is only for codegen.
- Group parameters that travel together: a lookup context built once per job (`TargetLookup`), a chunk view and an
  index for per-unit work (`MoverChunk`), or a settings type with an object initializer instead of optional
  parameters (`CommandSpecSettings`).
- One type per file, except an authoring class with its baker, or a feature's small components and tags sharing a
  file named after it (`HarvesterComponents.cs`). Systems, MonoBehaviours and ScriptableObjects are one per file.
- Prefer deleting code. No dead code, unused usings or commented-out code.

## Run and test

- Play `Assets/Demo/Scenes/SampleScene.unity`; its entities bake from a SubScene. The Hierarchy lists entity worlds
  beside GameObjects: filter with `t:` and a component name.
- EditMode tests live in `HyperRTS.Simulation.Tests` (end to end through `TestWorld`), `HyperRTS.Network.Tests`
  (sessions, RPCs), `HyperRTS.Presentation.Tests`, `HyperRTS.Editor.Tests` and `HyperRTS.Editor.Maps.Tests`. Each `Tests/` folder mirrors its
  module folders, with the shared kits at its root.
- Test behaviour that matters and hard edge cases, not every method or path. Extend an existing test before adding a
  near-copy, and skip a case another test already covers.
- No test depends on timing: an Editor import landing, a fixed number of frames, or files staying unchanged during
  the run. Delete one that does; a check over files or assets is a validation rule.
- A new authoring rule is an `AuthoringRule<T>` in its domain folder under `Editor/Validation/Rules/` (`Units`,
  `Buildings`, `Effects`, `Match`, `Fields`). The inspector, **HyperRTS ▸ Tools ▸ Validate** and the tests pick it up.
- In Play, the Scene view's **HyperRTS Debug** and **HyperRTS Cheats** overlays and the Tools window's Play page
  (the launcher, network, soak tests and replays) inspect and drive the world.

## Gotchas

- Domain reload is off, so statics survive between Play sessions. Mark the statics allowed above
  `[AutoStaticsCleanup]` or, for immutable tables, `[NoAutoStaticsCleanup]`, and put side effects such as shader
  globals in an `[OnEnteringPlayMode]` method (its type `partial`). `UAC*` diagnostics and `UAL*` findings are errors.
- UXML names custom elements by full type name, and moving a folder changes the namespace: rewrite those tags with
  the move.
- A serialized `Dictionary<K, V>` needs `[SerializeField]`, even on a public field.
- Entities built through `EntityWriter` in tests have no mesh; rendered spawns instantiate baked prefabs.
- SubScene entities stream in over the first frames: guard on singletons with `RequireForUpdate`, and never treat
  "nothing exists yet" as a game state (`VictorySystem` shows how).
- Keep `OverrideAutomaticNetcodeBootstrap`, on `EngineRig.prefab`, in scenes. `NetworkSession` makes the worlds: one
  local world for single player, one Netcode single-world host, a server world for a dedicated server and a client
  world for a joining player.
- A client system that mirrors a remote server, such as sending commands or receiving cues, carries
  `[RemoteClientOnly]`. Senders skip connections without `NetworkStreamConnection`, which is the host's own player.
  Receive RPCs through `RpcInbox<T>` and send them with `Rpc.Send`.
- Systems default to the server and local worlds. Client systems opt in with
  `[WorldSystemFilter(SimulationWorlds.Presented)]`; those under `PresentationSystemGroup` already default to the
  presented worlds. A component the HUD reads needs `[GhostField]`s.
- `EntityInfo.TypeId` hashes the display name, so two prefab types never share a name.

## Docs

The [docs index](docs/README.md) lists every page: guides, then `simulation/`, `presentation/`, `editor/` and `maps/`,
matching the layers. Check the [roadmap](docs/roadmap.md) before starting new work.

A doc says what the code can't: purpose, how the parts fit, how to extend, gotchas. Field meanings go in tooltips
and tuned values stay in the data. When a system changes, rewrite its section for the system as it is now and delete
what it replaced; never append a note. Pages stay under 2500 words, with no em or en dashes or arrow glyphs.
