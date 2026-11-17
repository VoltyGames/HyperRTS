# Play mode

[Docs](../README.md) ▸ [Editor](README.md) ▸ Play mode

The tools for starting and inspecting a match in the Editor: cheats, debug layers, the Play page launcher and virtual
players for network tests. For anyone playing or debugging a match in the Editor.

## Cheats and debug layers

The **HyperRTS ▸ Cheats & Debug** window (`CheatsDebugWindow`) holds the cheats, the debug layers and a table of
the AI players. Dock it beside the Game view so it stays in reach during Play. A line at the top sums up the running
match: who runs it (single player, host or server) and its players, units and buildings.

The cheats work in Play mode only, through the normal game systems, and drop the replay recording:

| Section | Does |
| --- | --- |
| Player | Pick a player; **Play as** it (its AI stops), add resources, finish its construction and production |
| Units | Spawn any prefab at the view centre (buildings follow placement rules), repair or destroy your selection, start a two-army battle as a load test |
| Match | Switch fog of war; change game speed or pause (single player only) |

Debug layers are `DebugLayer`s, found by type (a `Label`, `Detail`, `Group` and `Glyph` each). Switches are
remembered and work outside Play mode; **Hide all** clears them. While a layer is on, a status line under it shows
its `Describe` text, or that it has nothing to draw yet. Each refresh a layer adds lines and one grid of cells to a
`DebugShapes`: hidden mesh objects draw them in the Game and Scene views, the cells draped on the simulation's ground
heights, and both over everything so crops and units never hide them.

| Layer | Shows |
| --- | --- |
| Nav grid | Blocked, water and deck cells |
| Wall distance | Distance to the nearest wall, red at a wall to blue far away |
| Sectors | Connected regions of the first movement class |
| Flow field | Cost to the goal of the latest group field |
| Fog | Cells the local team sees |
| Sight area, Sight blockers, High ground | The selected unit's sight, what conceals cells, and rise above surrounding ground |
| Spatial cells | Spatial index cells holding an entity |
| Paths | Waypoints, rear axles and attack targets; its status totals the pathfinder's work |

To add a layer, subclass `DebugLayer` and implement `Draw`: `DebugShapes.Cells` takes a `CellGrid` and an
`ICellColors` struct that a Burst job reads for every cell, and `Line`, `DashedLine` and `Circle` draw the rest.
Layers read the authoritative world unless they override `Source`.

## Play page

The **Play** page is the launcher outside Play mode: map, mode (1v1 to 4v4 or free for all), start (into the map or
through the main menu), network (offline, host, dedicated server), the slot table and the game's switches. **Play**
(`GameLauncher.Play`) opens the game's menu scene and hands the choice to the game. The toolbar beside Play shows the choice
(`LaunchToolbar`). In Play mode the page shows:

| Section | Does |
| --- | --- |
| Network | Session status and traffic; host, join localhost, run a dedicated server of the scene |
| Soak test | AI against AI, match after match, with a results table and reports in `Logs/BotMatches/` |
| Replays | Save the recording to `Logs/Replays/`; play or reveal the newest |

A game joins the launcher with two subclasses in an editor assembly: `GameLauncher` lists maps and sides, names the
menu scene, starts a `LaunchChoice` and joins a hosted match; `LaunchSetting` adds an on or off switch.

## Virtual players

With network Host, **Virtual players** (0 to 3) test a networked match on one machine. Play starts the active
Multiplayer Play Mode scenario instead of plain Play: the main Editor hosts, and each other Editor instance tagged
`HyperRtsJoin` joins the match directly, with no menus.

Set it up once: add the `HyperRtsJoin` tag under **Project Settings ▸ Multiplayer ▸ Play Mode**, then make a scenario
with the main Editor and one to three more Editor instances carrying that tag, and make it active. Every human slot
past your own takes a virtual player, so pick a mode with enough of them.

Once the host listens, `GameLauncher.Invite` writes its address and the game's details (such as the match seed) to a
file in the temp folder, because the instances are separate Editor processes with their own project copies. Each
tagged instance waits up to 60 s in Play for that file, then calls `GameLauncher.Join`.

## Stagings

A staging is a JSON file that scripts a whole Play check, so an agent makes one call instead of many evals. `play
stage <file>` (`StagingJob`) opens the file's scene, enters Play, waits until the match is live (world, streamed
SubScene entities, a local player, the match camera), runs the steps at their game-time seconds, leaves Play and
reopens the scene that was open before. It returns at once; poll `play stage_status`. The run fails with a message
when the match is not live after 120 s, or with "Play ended early" when Play quits by itself.

`Logs/Stagings/<name>/` is cleared at the start of a run and then holds `report.json`, the shots, one folder per
frames burst, the sample TSVs and `sheet.png`, a contact sheet of all shots and up to six evenly spaced frames of each
burst, three or four tiles across and unlabelled; the report lists its tiles in order. The report gives each step's
outcome, how many units of each group are alive, every console error (message and first stack line, with counts), the
camera's largest move and turn from its first sample, and how the run ended. A step that fails is recorded and the
run goes on, except a `spawn`, whose failure ends the run with its message (a wrong prefab name lists the near matches).

```json
{
  "name": "muzzle_cannon", "scene": "Assets/Maps/X.unity", "fog": false,
  "steps": [
    { "at": 0, "spawn": { "as": "blue", "prefab": "Tank", "owner": 1, "count": 2, "at": [120, -40], "spacing": 8, "yaw": 90 } },
    { "at": 0, "spawn": { "as": "red", "prefab": "Truck", "owner": 2, "count": 1, "at": [160, -40], "yaw": 270 } },
    { "at": 0.5, "camera": { "on": "blue", "height": 15, "yaw": 0 } },
    { "at": 1, "order": { "units": "blue", "attack": "red" } },
    { "at": 1, "frames": { "name": "muzzle", "seconds": 3, "fps": 10 } },
    { "at": 5, "shot": "after" },
    { "at": 5, "sample": { "what": "camera", "seconds": 1 } }
  ]
}
```

`name`, `scene` and `steps` are required; `fog` switches fog when the match is live. Positions are `[x, z]` on the
map. An unknown key, a wrong type, a step with two kinds or two order actions, or a group no earlier step spawns fails
the file, naming its path. Steps run by `at`, and in file order at the same time.

| Step key | Keys inside | Does |
| --- | --- | --- |
| `spawn` | `as`, `prefab`, `owner`, `count`, `at`, `yaw`; `spacing` for more than one; `ground` with `radius` | Spawns a row across the facing (`yaw` degrees clockwise from +Z) under the group label `as`. `prefab` is the picker's display name. `ground` is a `SurfaceMaterial` name; the row moves to the nearest such cell within `radius` metres |
| `order` | `units`, and one of `move` `[x, z]`, `attack` (group), `attackGround` `[x, z]`, `stop` true, `ability` (its name), `command` | Sends a `PlayerCommand` for the group's player. `attack` gives each unit a target of the other group in turn; `attackGround` is a fire mission. `ability` uses the group's ability of that name and `command` is a `CommandType` name or a game command number with an optional `argument`; both take an optional `at` or `target` (group) |
| `camera` | `on` (group) or `at`, `height`; `yaw`, `follow` | Frames the group's centre or a point; the height is clamped to the camera's zoom range. `follow` keeps the group centred each frame until the next camera step |
| `shot` | a name | One Game view PNG, `<name>.png`, through the Recorder |
| `frames` | `name`, `seconds`, `fps` | A burst through `FrameCapture` into `<name>/`; the Recorder fixes the time step, so game time runs at `fps` during it |
| `sample` | `what` (`camera` or a group), `seconds` | Writes `<what>.tsv`, a row per rendered frame: camera pose, or each unit's position and health |
| `destroy` | a group | Kills it through the damage system |
| `damage` | `units`, `amount` | Damages it through the damage system |
| `speed` | a number | Game speed of a single-player match |
| `fog` | true or false | Fog of war |

To add a step kind, subclass `StagingStep` with a static `Read` that takes the step's `StagingObject`, run it in `Run`
(long work becomes a `StagingActivity` handed to `run.Start`), and list its key in `StagingStep.Readers`. A group
label it makes is `Defines`, labels it needs are `Uses`, and a file it writes is `Output`.

- One frame capture runs at a time: space `shot` and `frames` steps apart.
- `destroy` and `damage` go through unit health, so a sectioned building stands; shoot it with a group instead.
- A staging that never reaches Play is ended as failed by the next `play stage` or `play stage_status` after 3 minutes.
- Orders and spawns write the authoritative world, so a staging needs a map with a local player.
- Entering Play replaces the open scene: the staging fails to start while another scene has unsaved changes.
