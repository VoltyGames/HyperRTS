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
| Paths | Waypoints, manoeuvres and attack targets; its status totals the pathfinder's work |

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
