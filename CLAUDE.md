# CLAUDE.md

## Project

**HyperRTS** — a reusable **RTS engine built on Unity DOTS** (Entities/ECS), to later
build games on top of. It's an engine-in-progress, not a game: prefer generic,
data-driven pieces over game-specific code.

`Assets/ThirdParty/RTS Engine/` is a commercial MonoBehaviour-based asset kept as a
**feature reference only** — don't edit it or copy its (non-DOTS) architecture.

## Environment

- **Unity 6000.5.0f1** (6.5), URP 17.5, new Input System (no legacy `UnityEngine.Input`).
- **DOTS:** Entities 6.5, Entities Graphics, Unity Physics, Netcode for Entities, Collections,
  Burst, Mathematics. (Entities `6.x` tracks the editor version; it's the successor to `1.x`,
  same APIs.)

## Layout

First-party code lives in `Assets/Modules/`. New modules go in sibling folders under
`Modules/` with their own asmdef referencing `HyperRTS.Core`.

```
Assets/Modules/
├── Core/                 # the engine — asmdef: HyperRTS.Core
│   ├── Attack/           # AttackTarget, Melee/RangeAttackDamage, AttackCooldown, AttackSystem
│   ├── Buildings/        # BuildingTag, ConstructionProgress, ConstructionSystem, factory
│   ├── Cameras/          # CameraController (MonoBehaviour: pan/zoom/rotate)
│   ├── Health/           # HealthComponent, DeathSystem
│   ├── Resources/        # ResourceType, Resource, ResourceManager (static registry)
│   ├── Units/            # UnitTag, MovementSpeed, MoveDestination, MovementSystem, factory
│   └── Editor/           # editor-only tooling — asmdef: HyperRTS.Core.Editor
└── Demo/                 # sample scene + EntitiesSubScene
```

## Conventions

- Namespaces mirror folders: `HyperRTS.Core.<Module>`.
- Authoring class + nested `Baker` + the `IComponentData` struct share one `*Authoring.cs`
  file (see `HealthComponentAuthoring.cs`). Bakers use `GetEntity(TransformUsageFlags.Dynamic)`.
- Component fields PascalCase; authoring MonoBehaviour fields camelCase.
- Systems: `[BurstCompile] partial struct …System : ISystem`, auto-discovered (don't register
  manually), `[UpdateInGroup(typeof(SimulationSystemGroup))]` + order attributes.
- Keep components blittable (Burst-safe): unmanaged only, strings as `FixedStringNNBytes`.
- Structural changes go through an `EntityCommandBuffer`; cross-entity access via
  `ComponentLookup<T>`; timing via `SystemAPI.Time` (never `UnityEngine.Time`).

## Systems

- `MovementSystem` — moves entities with `MoveDestination` at `MovementSpeed`, clears order on arrival.
- `AttackSystem` — on `AttackCooldown`, subtracts `Melee`/`RangeAttackDamage` from `AttackTarget`'s health.
- `ConstructionSystem` — advances `ConstructionProgress` (0..1), removes it when complete.
- `DeathSystem` (OrderLast) — destroys entities at `CurrentHealth <= 0`.

These are minimal scaffolds; expect to extend them.

## Run / test

Open in Unity 6000.5.0f1; play `Assets/Modules/Demo/Scenes/SampleScene.unity` (entities bake from
its EntitiesSubScene). Inspect via Window ▸ Entities. No test assemblies yet. If the Unity MCP
bridge is connected, `mcp__unity-mcp__Unity_GetConsoleLogs` surfaces compile errors.

## Gotchas

- C# edits need a Unity domain reload (editor open) before Play mode reflects them.
- Factory-created entities have data + `LocalTransform` but **no mesh** — bake a prefab via a
  SubScene to get rendered entities.
- `ResourceManager` is a static managed registry for editor/setup — not Burst/job-safe; runtime
  resource data lives in the `Resource` component.

## Roadmap

Selection (Physics raycast) → commands/orders → pathfinding → economy → production →
rendering for factory entities → fog of war → factions → multiplayer (Netcode for Entities).
