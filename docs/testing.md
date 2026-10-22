# Testing

The engine's tests are EditMode tests in `HyperRTS.Simulation.Tests`, `HyperRTS.Presentation.Tests` and
`HyperRTS.Editor.Tests`. Run them from **Window ▸ General ▸ Test Runner**.

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

`SpawnUnit` and `SpawnBuilding` build entities the way the bakers do, without a mesh. `MakePrefab` turns one into a
prefab for producers. `CreateMatch` also builds the nav grid and match rules, which systems require.

- Wait on a state with `RunUntil(condition, maxSeconds)` rather than a fixed `Run`; it returns whether the state came.
- `Own<T>` hands the world a Unity object to destroy on dispose.
- Give a unit an order with `Issue(unit, order, queue)`, which goes through `OrderWriter` like the game does.
- Kits add setup per area: `CombatTestKit` (`Kill`, `IsIdle`), `EconomyTestKit` (supplies, unit prefabs, producers),
  `ContainerTestKit`, `AircraftTestKit` and `CueTestKit` (queued cues and type ids).
- Presentation tests build their world through `PresentationWorldKit`, with players made by `PlayerSetup.Add`.
- `HostTestKit` builds a single-world host the way `NetworkSession` does and a client joined to it over IPC, each
  wrapped in a `TestWorld` for setup. It ticks both at 60 Hz with a fixed time step.

## Validation in CI

`ValidationTests.ProjectHasNoValidationErrors` runs every validation rule over the project's prefabs and open
scenes. A map is only checked while it is open. `RuleFiresTests` builds a broken case for every rule and checks
that it reports, so a new rule needs a row there. `ModuleLayoutTests` checks the
[module rules](architecture.md#modules).

## Command line

Close the project in the editor, then run:

```sh
Unity -batchmode -projectPath <project> -runTests -testPlatform EditMode -testResults results.xml
```

## Not covered yet

PlayMode and rendering tests, determinism, performance budgets and input.
