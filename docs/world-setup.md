# World setup — how entities enter play

How the ECS world is created, how entities get into it, and the order systems run in.

There is **no custom bootstrapper** — HyperRTS uses Unity's standard DOTS pipeline plus the
ordered system groups below.

Systems live in **layered assemblies** (`Simulation` for headless gameplay; `Presentation`/`Input`
for client-only rendering, UI, and input) — see [`architecture.md`](architecture.md). System
discovery is global across all of them, so the phase groups below nest systems regardless of which
assembly defines them. A headless server simply omits the `Presentation`/`Input` assemblies, so
those systems never load.

## The default world

On entering Play mode, Unity calls `DefaultWorldInitialization.Initialize`, which creates the
default `World`, discovers every system across all loaded assemblies, adds each to its
`[UpdateInGroup]` target, and hooks the root groups into the player loop. Our systems and phase
groups come alive this way — no manual registration.

> Discovery is global, not per-assembly. Any auto-running system anywhere in the project also
> loads into the default world (and into test worlds — see [below](#dont-auto-spawn-on-startup)).

## Entities from the Demo SubScene

[`SampleScene`](../Assets/Modules/Demo/Scenes/SampleScene.unity) holds an auto-loaded `SubScene`.
On load, Unity **bakes** its GameObjects into entities via each authoring MonoBehaviour's nested
`Baker` (see [`HealthComponentAuthoring.cs`](../Assets/Modules/Simulation/Health/HealthComponentAuthoring.cs)).
This is the way to get **rendered** entities into play, since a baked prefab carries a mesh.

The SubScene's `Unit` GameObject currently has no ECS authoring components, so it bakes to an
empty entity. Adding authoring components to it is the natural next step.

## Update order

Defined in [`SystemGroups.cs`](../Assets/Modules/Core/SystemGroups.cs) — five groups under Unity's
`SimulationSystemGroup`, mapped to roadmap phases:

```
SimulationSystemGroup
 ├─ OrderSystemGroup        (roadmap 1–2: selection, commands)   — empty for now
 ├─ MovementSystemGroup     (roadmap 3)   → MovementSystem        — before TransformSystemGroup
 ├─ CombatSystemGroup       (roadmap 7)   → AttackSystem
 ├─ ProductionSystemGroup   (roadmap 5)   → ConstructionSystem
 └─ LifecycleSystemGroup    (last)        → DeathSystem
```

Movement runs before `TransformSystemGroup` so a move shows the same frame. Lifecycle runs last so
`DeathSystem` removes dead entities only after all damage is applied; it records destruction from a
parallel job and `EndSimulationEntityCommandBufferSystem` plays it back at the end of the group.

Systems are `[BurstCompile]` `IJobEntity` jobs. Runtime state that toggles (`MoveDestination`,
`ConstructionProgress`, `Selected`) is enableable, so orders and completion never change an entity's
archetype. To trace when a component is added or removed while debugging, implement Entities 6.6's
`IDebugOnAdded`/`IDebugOnRemoved` on it (Editor and development builds only).

**New systems pick a phase group** with `[UpdateInGroup(typeof(<Phase>SystemGroup))]` — not
`SimulationSystemGroup` directly.

## Creating entities from code

To spawn entities in setup scripts, systems or tests instead of baking, use an
[`IEntityFactory`](../Assets/Modules/Core/IEntityFactory.cs). Factories record into an
`EntityCommandBuffer`, so systems and jobs can spawn without a sync point; since Entities 6.6 the
returned `Entity` is the real one (no placeholder), so you can store it or issue follow-up commands
right away. The `EntityManager` overload records and plays back immediately:

```csharp
var unit = new UnitEntityFactory().CreateEntity(entityManager); // or .CreateEntity(ecb)

// Units carry a disabled MoveDestination: an order is "set value + enable", cleared by disabling.
entityManager.SetComponentData(unit, new MoveDestination { Value = new float3(100, 0, 0) });
entityManager.SetComponentEnabled<MoveDestination>(unit, true);
```

[`UnitEntityFactory`](../Assets/Modules/Simulation/Units/UnitEntityFactory.cs) and
[`BuildingEntityFactory`](../Assets/Modules/Simulation/Buildings/BuildingEntityFactory.cs) build the
core entities. Factory entities have data but **no mesh** — they show in **Window ▸ Entities** but
not the Game view until rendering lands (roadmap phase 6).
[`SimulationSystemTests`](../Assets/Modules/Simulation/Tests/SimulationSystemTests.cs) uses them.

## Don't auto-spawn on startup

Avoid an auto-running `DemoBootstrapSystem` that spawns entities: because discovery is global, it
would also run in edit-mode test worlds and would duplicate baked SubScene entities. If you need a
code spawn point, make it opt-in — a static helper called explicitly, or a `[DisableAutoCreation]`
system you create yourself.

## Verify

- **Window ▸ Entities ▸ Systems** — `SimulationSystemGroup` nests Order → Movement → Combat →
  Production → Lifecycle, with `TransformSystemGroup` after Movement.
- **Window ▸ General ▸ Test Runner ▸ EditMode** — run `SimulationSystemTests`.
