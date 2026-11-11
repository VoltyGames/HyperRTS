# Testing

The engine's tests are EditMode tests in `HyperRTS.Simulation.Tests`, `HyperRTS.Network.Tests`,
`HyperRTS.Presentation.Tests`, `HyperRTS.Editor.Tests` and `HyperRTS.Editor.Maps.Tests`. Run them from **Window ▸
General ▸ Test Runner**, or from the command line with the project closed:

```sh
Unity -batchmode -projectPath <project> -runTests -testPlatform EditMode -testResults results.xml
```

## TestWorld

`TestWorld` is an isolated world with every simulation system, stepped at 30 fps. Test through systems: issue commands,
run time, then check components.

```csharp
using var world = new TestWorld();
world.CreateMatch(1, 2);                                  // two players, teams 1 and 2
var unit = world.SpawnUnit(1, float3.zero);
world.Command(1, new PlayerCommand { Type = CommandType.Move, Unit = unit, Position = new float3(10, 0, 0) });
world.Run(3f);
Assert.That(world.Get<LocalTransform>(unit).Position.x, Is.EqualTo(10f).Within(0.5f));
```

Most test classes derive from `SimulationFixture`, which creates the world and match before each test and disposes
them after. Reach the world through `World`; override `Teams` (default `{ 1, 2 }`) for another match shape. A class
whose setup differs keeps its own `[SetUp]` and `TestWorld` field.

- Write through the world's helpers, not its `EntityManager`: `Set<T>`, `Add<T>`, `Buffer<T>`, `SetEnabled<T>`,
  `Writer(entity)` and `Query(types)` (the caller disposes it).
- `SpawnUnit` and `SpawnBuilding` build entities as the bakers do, without a mesh; `MakePrefab` turns one into a
  prefab for producers. `CreateMatch` also builds the nav grid and match rules, which systems require.
- Wait on a state with `RunUntil(condition, maxSeconds)` rather than a fixed `Run`. Give orders with
  `Issue(unit, order, queue)`, which goes through `OrderWriter` like the game. `Own<T>` hands the world a Unity object
  to destroy on dispose.
- Kits add setup per area: `CombatTestKit`, `EconomyTestKit`, `ContainerTestKit`, `AircraftTestKit`, `CueTestKit`.
  Presentation tests build their world with `PresentationWorldKit` and `PlayerSetup.Add`. `HostTestKit` (in
  `HyperRTS.Network.Tests`) builds a single-world host as `NetworkSession` does and a client joined over IPC.
- `GridDump.Path` and `GridDump.Fog` render a window of a grid as ASCII for a failed assert: pass it in a lazy message,
  `Assert.That(x, Is.True, () => GridDump.Fog(...))`, or queue it with `DumpOnFailure(() => ...)`.

## Validation in CI

`ValidationTests.ProjectHasNoValidationErrors` runs every [validation rule](editor/README.md#validation) over the project's
prefabs and open scenes (a map is checked only while open). `RuleFiresTests` builds a broken case for every rule, so a
new rule needs a row there. `ModuleLayoutTests` checks the [module rules](architecture.md#modules).

## Benchmarks

Timing tests are `[Test, Performance]` cases in the `Benchmark` category (`NavBenchmarkTests`,
`CombatBenchmarkTests`), built on the Performance Testing package. `BenchmarkKit.Median` wraps `Measure.Method` with a
warm-up, 20 measured runs, untimed prepare and finish steps and the profiler markers to record. Each test keeps one
coarse assert at twice its budget, so a regression fails. Covered systems have markers named `<Type>.<Step>`; a
per-entity job opens its marker in `IJobEntityChunkBeginEnd`, heavier work uses `ProfilerMarker.Auto()`.

## Not covered yet

PlayMode and rendering tests, determinism and input.
