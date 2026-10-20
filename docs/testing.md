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
prefab for producers. `CombatTestKit`, `EconomyTestKit` and `AircraftTestKit` add setup for their area.

## Validation in CI

`ValidationTests.ProjectHasNoValidationErrors` runs every validation rule over the project's prefabs and open
scenes. A map is only checked while it is open. `ModuleLayoutTests` checks the [module rules](architecture.md#modules).

## Command line

Close the project in the editor, then run:

```sh
Unity -batchmode -projectPath <project> -runTests -testPlatform EditMode -testResults results.xml
```

## Not covered yet

PlayMode and rendering tests, determinism, performance budgets and input.
