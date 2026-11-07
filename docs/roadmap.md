# Roadmap

What the engine does today, the design decisions behind it, and what is still open. Update it when a feature lands.

## Status

Done and tested: selection and orders, pathfinding over terrain, water and bridges, economy, bases and production,
combat (multiple weapons, artillery arcs, rockets), fog of war and stealth, power, upgrades, veterancy, abilities,
status effects, capture, transports, aircraft, destruction, skirmish AI, audio, replays, bone-animated infantry,
motion rigs, multiplayer, and the editor tooling with the Map Editor.

In progress:

- **Vehicle driving**: wheeled vehicles turn about the rear axle and `DrivePlanner` plans forward and reverse legs
  where a chassis can't drive a path as drawn. The wall reflexes still run off planned legs; docks, the building gap
  rule and trailers follow. See [drive planner](simulation/driving.md#drive-planner).
- **Unit motion**: tracks don't scroll and suspension doesn't flex yet. See [motion rig](presentation/animation.md#motion-rig).

## Decisions

| Decision | Why |
| --- | --- |
| Grid with distance fields and a sector graph, not a navmesh | Buildings, crushable scenery, decks and water restamp cells cheaply |
| Corridor A* for single moves, a flow field per formation order | One search serves a whole group; one-off moves stay cheap |
| Sampled steering, not ORCA | Vehicles turn at their rate and radius, and walls count like neighbours |
| Hybrid A* only where the chassis can't drive the path | Easy paths stay cheap; yards, tight corners and wedges get real manoeuvres |
| Server-authoritative Netcode, not lockstep | Burst float math is not deterministic across platforms |
| Replays record state, not commands | The simulation is not deterministic |
| One damage queue, one stat-modifier buffer | Armor, splash, kill credit and every bonus share one path |
| UI Toolkit for all UI | Unity's recommendation |

## Open

- **Frozen until a game uses them:** abilities, aircraft, upgrades, veterancy, stealth, auras, build limits and
  deliveries work, but no shipped content uses them yet. Extend them only when a game needs it.
- **Gameplay:** moving obstacles, per-area water levels, ramps onto decks, smarter AI (micro, scouting, defence,
  naval and air play), an ability targeting cursor, aircraft attack runs, replays of projectiles, passengers and
  player stats.
- **Multiplayer:** ghost importance by distance, command feedback before the server confirms, an observer HUD, host
  migration, automatic reconnect.

### Hardening

No CI, stress scene or performance budgets yet. Wanted: CI running the EditMode tests and project validation
(including closed map scenes), PlayMode tests, a stress scene, per-system budgets and a determinism test.

## Rules

- Generic and data-driven: authoring and assets over hard-coded values.
- Burst safe: unmanaged components, command buffers for structural changes, `SystemAPI.Time`.
- No `UnityEngine.Random` in the simulation. Break ties by entity index.
- Check the 500-unit scenario when you change movement, combat or the spatial index.
