# Testing

The engine's tests are EditMode tests in `HyperRTS.Simulation.Tests`, `HyperRTS.Network.Tests`,
`HyperRTS.Presentation.Tests`, `HyperRTS.Editor.Tests` and
`HyperRTS.Editor.Maps.Tests`. Run them from **Window ▸ General ▸ Test Runner**.

## TestWorld

`TestWorld` is an isolated world with every simulation system, stepped at 30 fps. Test through systems: issue
commands, run time, then check components.

```csharp
using var world = new TestWorld();
world.CreateMatch(1, 2);                                  // two players, teams 1 and 2
var unit = world.SpawnUnit(1, float3.zero);
world.Command(1, new PlayerCommand { Type = CommandType.Move, Unit = unit, Position = new float3(10, 0, 0) });
world.Run(3f);
Assert.That(world.Get<LocalTransform>(unit).Position.x, Is.EqualTo(10f).Within(0.5f));
```

Most test classes derive from `SimulationFixture`, which creates the `TestWorld` and the match before each test and
disposes it after. Reach the world through `World`, and override `Teams` (default `{ 1, 2 }`, one team per player) for
another match shape.

```csharp
public class AimTests : SimulationFixture
{
    [Test]
    public void Shot_HitsTheEnemy()
    {
        var gun = World.SpawnUnit(1, float3.zero);
        World.Set(gun, new Health { Current = 5f, Max = 10f });
        World.Run(1f);
    }
}
```

Write through the world's helpers rather than its `EntityManager`: `Set<T>`, `Add<T>`, `Buffer<T>`,
`SetEnabled<T>`, `Writer(entity)` (an `EntityWriter`) and `Query(types)`, which the caller disposes. A class whose
setup differs keeps its own `[SetUp]` and `TestWorld` field.

`SpawnUnit` and `SpawnBuilding` build entities the way the bakers do, without a mesh. `MakePrefab` turns one into a
prefab for producers. `CreateMatch` also builds the nav grid and match rules, which systems require.

- Wait on a state with `RunUntil(condition, maxSeconds)` rather than a fixed `Run`; it returns whether the state came.
- `Own<T>` hands the world a Unity object to destroy on dispose.
- Give a unit an order with `Issue(unit, order, queue)`, which goes through `OrderWriter` like the game does.
- Kits add setup per area: `CombatTestKit` (`Kill`, `IsIdle`), `EconomyTestKit` (supplies, unit prefabs, producers),
  `ContainerTestKit`, `AircraftTestKit` and `CueTestKit` (queued cues and type ids).
- Presentation tests build their world through `PresentationWorldKit`, with players made by `PlayerSetup.Add`.
- `HostTestKit` (in `HyperRTS.Network.Tests`, which references the Simulation tests for `TestWorld`) builds a single-world host the way `NetworkSession` does and a client joined to it over IPC, each
  wrapped in a `TestWorld` for setup. It ticks both at 60 Hz with a fixed time step.

## Validation in CI

`ValidationTests.ProjectHasNoValidationErrors` runs every validation rule over the project's prefabs and open
scenes. A map is only checked while it is open. `RuleFiresTests` builds a broken case for every rule and checks
that it reports, so a new rule needs a row there. `ModuleLayoutTests` checks the
[module rules](architecture.md#modules).

## Benchmarks

Timing tests are `[Test, Performance]` cases in the `Benchmark` category (`NavBenchmarkTests`, `VisionBenchmarkTests`),
built on the Performance Testing package that Entities already pulls in. `BenchmarkKit.Median` wraps
`Measure.Method` with a warm-up, 20 measured runs, an untimed prepare and finish step, and the profiler markers to
record (`GridAStar.Search`, `FogMergeJob.Merge` and so on). It returns the median, and each test keeps one coarse
assert on it at twice its budget, so a regression fails. Systems the benchmarks cover have markers named
`<Type>.<Step>`. A per-entity job opens and closes its marker in
`IJobEntityChunkBeginEnd`, once a chunk; heavier per-item work uses `ProfilerMarker.Auto()`.

## Command line

Close the project in the editor, then run:

```sh
Unity -batchmode -projectPath <project> -runTests -testPlatform EditMode -testResults results.xml
```

## Not covered yet

PlayMode and rendering tests, determinism and input.
