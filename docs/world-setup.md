# World setup — how entities enter play

How the ECS world is created, how entities get into it, and the order systems run in.

There is **no custom bootstrapper** — HyperRTS uses Unity's standard DOTS pipeline plus the
ordered system groups below.

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
`Baker` (see [`HealthComponentAuthoring.cs`](../Assets/Modules/Core/Health/HealthComponentAuthoring.cs)).
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
`DeathSystem` removes dead entities only after all damage is applied.

**New systems pick a phase group** with `[UpdateInGroup(typeof(<Phase>SystemGroup))]` — not
`SimulationSystemGroup` directly.

## Creating entities from code

To spawn entities in setup scripts or tests instead of baking, use an
[`IEntityFactory`](../Assets/Modules/Core/IEntityFactory.cs):

```csharp
var unit = new UnitEntityFactory().CreateEntity(entityManager);
entityManager.AddComponentData(unit, new MoveDestination { Value = new float3(100, 0, 0) });
```

[`UnitEntityFactory`](../Assets/Modules/Core/Units/UnitEntityFactory.cs) and
[`BuildingEntityFactory`](../Assets/Modules/Core/Buildings/BuildingEntityFactory.cs) build the
core entities. Factory entities have data but **no mesh** — they show in **Window ▸ Entities** but
not the Game view until rendering lands (roadmap phase 6).
[`SimulationSystemTests`](../Assets/Modules/Core/Tests/SimulationSystemTests.cs) uses them.

## Don't auto-spawn on startup

Avoid an auto-running `DemoBootstrapSystem` that spawns entities: because discovery is global, it
would also run in edit-mode test worlds and would duplicate baked SubScene entities. If you need a
code spawn point, make it opt-in — a static helper called explicitly, or a `[DisableAutoCreation]`
system you create yourself.

## Verify

- **Window ▸ Entities ▸ Systems** — `SimulationSystemGroup` nests Order → Movement → Combat →
  Production → Lifecycle, with `TransformSystemGroup` after Movement.
- **Window ▸ General ▸ Test Runner ▸ EditMode** — run `SimulationSystemTests`.
