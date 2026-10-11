# Testing

EditMode tests in three assemblies. Run them from **Window ▸ General ▸ Test Runner ▸ EditMode**.

| Assembly | Folder | Covers |
| --- | --- | --- |
| `HyperRTS.Simulation.Tests` | `Simulation/Tests/` | Every simulation module end to end through `TestWorld`, plus `NetworkSession` |
| `HyperRTS.Presentation.Tests` | `Presentation/Tests/` | HUD context, `MatchView` colours, presentation math and systems, sound pool |
| `HyperRTS.Editor.Tests` | `Editor/Tests/` | Validation rules, quick fixes, templates, handles, module layout, project validation |

## TestWorld

An isolated world with every `HyperRTS.Simulation` system, at 30 fps fixed steps.

```csharp
using var world = new TestWorld();
world.CreateMatch(1, 2);                                  // two players, teams 1 and 2
var unit = world.SpawnUnit(1, float3.zero);
world.Command(1, new PlayerCommand { Type = CommandType.Move, Unit = unit, Position = new float3(10, 0, 0) });
world.Run(3f);
Assert.That(world.Get<LocalTransform>(unit).Position.x, Is.EqualTo(10f).Within(0.5f));
```

| Member | Does |
| --- | --- |
| `CreateMatch(params byte[] teams)` | Match singletons and one player per team entry |
| `CreateTerrain(height, spacing)` | Baked terrain from a height function |
| `Player(faction)` | The player entity |
| `SpawnUnit`, `SpawnBuilding` | Entities with the baked component set (`*Setup` helpers), no mesh |
| `MakePrefab(entity)` | Turns an entity into a prefab for producers and builders |
| `Command(faction, command)` | Appends a `PlayerCommand` |
| `Tick(dt, frames)`, `Run(seconds)` | Advances the world |
| `Get<T>`, `IsEnabled<T>`, `EntityManager` | Reads state |

Test kits add setup for one area: `CombatTestKit`, `EconomyTestKit`, `AircraftTestKit`.

Write tests through systems: issue commands, run time, assert on components. Don't call system internals.

## Validation in CI

`ValidationTests.ProjectHasNoValidationErrors` runs every validation rule over the project's prefabs and the open
scenes, and fails on errors. Closed scenes and SubScenes are not opened, so map content is only checked when
the map is open.

`ModuleLayoutTests` checks the module layer order, namespaces and authoring files (see
[architecture](architecture.md#modules)).

## Command line

```sh
Unity -batchmode -projectPath <project> -runTests -testPlatform EditMode -testResults results.xml
```

The project must not be open in another editor.

## Not covered yet

- PlayMode and rendering tests.
- Determinism (same commands, same state).
- Performance budgets and a stress scene.
- Input systems.
