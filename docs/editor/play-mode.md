# Play mode

[Docs](../README.md) ▸ [Editor](README.md) ▸ Play mode

The tools for starting and inspecting a match in the Editor: cheats, debug layers, the Play page launcher and virtual
players for network tests. For anyone playing or debugging a match in the Editor.

## Cheats and debug layers

Two windows share the `PlayModeWindow` base, which remembers each group's fold state and opens each as a floating
window that stays above the Editor during Play. **HyperRTS ▸ Cheats** (`CheatsWindow`) holds the cheats;
**HyperRTS ▸ Debug** (`DebugWindow`) holds the debug layers and a table of the AI players. A line at the top of Cheats
sums up the running match: who runs it (single player, host or server) and its players, units and buildings.

The cheats work in Play mode only, through the normal game systems, and drop the replay recording:

| Group | Does |
| --- | --- |
| Player and resources | Pick the player every cheat acts for; **Play as** it (its AI stops), add resources, finish its construction and production |
| Spawning | Spawn any prefab at the view centre for the player (buildings follow placement rules) |
| Combat and battles | Repair, destroy or ready the abilities of your selection; start a two-army battle as a load test |
| Strikes | One row per strike the game has, found from its prefabs under the spawn grouping's headings: each strike ability of a structure and each unit with a strategic weapon, tactical or strategic. A player owning none gets **Build at view**; otherwise the row shows Ready, the cooldown, a launcher's rounds or why it can't fire, with **Ready** and **Fire at view** |
| World | Switch fog of war; change game speed or pause (single player only) |

**Ready** powers the strike and ends its cooldown or fills a launcher's strategic rounds, so a launch door opens over a
charged strike without firing. **Fire at view** readies it first, then sends the player's command at the ground point
the Game view looks at: `UseAbility` for a strike, a fire mission for a launcher. Powering sets a consumer's `PowerSupply` draw to zero and clears its `Unpowered` reason, since the power system only sets it and
`AbilityCommandSystem` drops commands from an offline caster. Other reasons (under construction, a game's own) and an
aimed ability out of range are shown in the row instead.

The spawn and strike lists are grouped as heading, then Units, Buildings or Other, then the prefab. A game gives it headings by
implementing `ISpawnGrouping` in an editor assembly: `Headings` in display order and `FindHeading` for a prefab's
entity name, with null listing the prefab under Other. It runs as the list opens, so it reads only the name and loads
no assets. The picker finds the implementation by type, and search covers every
heading.

Debug layers (the Debug window) are `DebugLayer`s, found by type (a `Label`, `Tooltip`, `Group` and `Glyph` each). Switches are
remembered and work outside Play mode; **Hide all** clears them. While a layer is on, a status line under it shows
its `Status` text, or that it has nothing to draw yet. Each refresh a layer adds lines and one grid of cells to a
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

| Part | Does |
| --- | --- |
| Network | Session status and traffic; host, join localhost, run a dedicated server of the scene |
| Bot matches | AI against AI, match after match, with a results table and reports in `Logs/BotMatches/` |
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

A staging is a JSON file that scripts a whole Play check or a filmed scene, so an agent makes one call instead of many
evals. `play
stage <file>` (`StagingJob`) opens the file's scene, enters Play, waits until the match is live (world, streamed
SubScene entities, a local player, the match camera), runs the steps at their game-time seconds, leaves Play and
reopens the scene that was open before. It returns at once; poll `play stage_status`. The run fails with a message
when the match is not live after 120 s, when the run still goes after an hour, or with "Play ended early" when Play
quits by itself. A contact sheet that fails is noted in the report's `ended` line and the report is still written.

`Logs/Stagings/<name>/` is cleared at the start of a run and then holds `report.json`, the shots, one folder per
frames burst, the sample TSVs and `sheet.png`, a contact sheet of all shots and up to six evenly spaced frames of each
burst, three or four tiles across and unlabelled; the report lists its tiles in order. The report gives each step's
outcome, how many units of each group are alive, every console error (message and first stack line, with counts), the
camera's largest move and turn from its first sample, and how the run ended. A step that fails is recorded and the
run goes on, except a `spawn`, whose failure ends the run with its message (a wrong prefab name lists the near matches).

```json
{
  "name": "muzzle_cannon", "scene": "Assets/Maps/X.unity",
  "steps": [
    { "at": 0, "fog": false },
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

`name`, `scene` and `steps` are required; `resolution` (`[width, height]`) sets the Game view size every shot, burst
and clip records at. Positions are `[x, z]` on the map. An unknown key, a wrong type, a step with two kinds or two order actions, or a group no earlier step spawns fails
the file, naming its path. Steps run by `at`, and in file order at the same time.

| Step key | Keys inside | Does |
| --- | --- | --- |
| `spawn` | `as`, `prefab`, `owner`, `count`, `at`, `yaw`; `spacing` for more than one; `ground`, `radius` | Spawns a row across the facing (`yaw` degrees clockwise from +Z) under the group label `as`. `prefab` is the picker's display name. `ground` is a `SurfaceMaterial` name; the row moves to the nearest such cell within `radius` metres. With a `radius`, a building whose ground is blocked takes the nearest free spot within it |
| `order` | `units`, and one of `move` `[x, z]`, `attack` (group), `attackGround` `[x, z]`, `stop` true, `ability` (its name), `command` | Sends a `PlayerCommand` for the group's player. `attack` gives each unit a target of the other group in turn; `attackGround` is a fire mission. `ability` uses the group's ability of that name and `command` is a `CommandType` name or a game command number with an optional `argument`; both take an optional `at` or `target` (group) |
| `camera` | `on` (group) or `at`, `height`; `yaw`, `follow` | Frames the group's centre or a point; the height is clamped to the camera's zoom range. `follow` keeps the group centred each frame until the next camera step. It also ends a cinematic `view` |
| `view` | `on` (group) or `at`, `lift`, `from` and optional `to` (each `distance`, `pitch`, `yaw`, and in `to` also `at` and `lift`), `seconds`; `fov`, `sun` | A cinematic move through `CinematicCamera`, free of the zoom range: aims `lift` metres above the ground at the point (or the group's centre, followed as it moves) from `distance` metres, `pitch` degrees down and `yaw` degrees clockwise from +Z, and eases to the `to` aim over `seconds`. What `to` leaves out stays as in `from`; yaw turns the way its numbers run, so 0 to 270 turns three quarters. With `sun` true, yaws count from the way the brightest directional light shines, so yaw 0 keeps the sun behind the camera on any map and 180 looks into it. The view holds at the end until the next `view` or `camera` step |
| `shot` | a name | One Game view PNG, `<name>.png`, through the Recorder |
| `frames` | `name`, `seconds`, `fps` | A burst through `FrameCapture` into `<name>/`; the Recorder fixes the time step, so game time runs at `fps` during it |
| `clip` | `name`, `seconds`, `fps` | A ProRes 422 HQ `<name>.mov` with the game's sound through `FrameCapture`, timed like a burst: a master to cut and encode for sharing, since the Recorder's own H.264 is soft. The Recorder renders the sound beside each frame, so it stays in step however slowly frames come |
| `sample` | `what` (`camera` or a group), `seconds` | Writes `<what>.tsv`, a row per rendered frame: camera pose, or each unit's position and health |
| `destroy` | a group | Kills it through the damage system |
| `damage` | `units`, `amount` | Damages it through the damage system |
| `ready` | a group | Ends its ability cooldowns, so a power that charges for minutes, such as a strategic asset, fires at once |
| `speed` | a number | Game speed of a single-player match |
| `fog` | true or false | Fog of war |
| `detail` | `shadows`, `grass`, `crops` (metres), `lod` (a factor); one or more | Raises render detail past the play camera's needs for footage shot from far off: the pipeline's shadow distance, which plants follow out to their drawn reach, full vegetation drawn out to the given distances (capped by each plant's own) and finer model levels. The run puts each setting back when it ends, so nothing is saved |
| `mixer` | parameter names and values | Sets exposed parameters on the loaded audio mixers, such as `{ "Music": -80 }` to record without the match music under footage that gets its own; a name no mixer exposes fails |
| `clean` | true or false | Hides (or shows again) every UI panel, the `OverlayRenderer`'s markers and bars and the silhouettes of hidden units, for footage |

To add a step kind, subclass `StagingStep` in `Editor/PlayMode/Staging/Steps/` with a static `Read` that takes the step's `StagingJson`, run it in `Run`
(long work becomes a `StagingActivity` handed to `run.Start`), and list its key in `StagingStep.Readers`. A group
label it makes is `Defines`, labels it needs are `Uses`, and a file it writes is `Output`.

- One recording runs at a time: space `shot`, `frames` and `clip` steps apart, and cut between `view` steps inside
  one clip.
- `destroy` and `damage` go through unit health, so a sectioned building stands; shoot it with a group instead.
- A staging that never reaches Play is ended as failed by the next `play stage` or `play stage_status` after 3 minutes.
- Orders and spawns write the authoritative world, so a staging needs a map with a local player.
- Entering Play replaces the open scene: the staging fails to start while another scene has unsaved changes.
