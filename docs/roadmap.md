# Roadmap

What the engine does today, why, and what is still open. Update it when a feature lands.

## Status

Done and tested: selection and orders, pathfinding over terrain, water and bridges, economy, bases and production,
combat with multiple weapons per unit, artillery arcs, rockets and minimum range, fog of war and stealth, power, upgrades, veterancy, fields, abilities, status effects, capture, transports,
aircraft that reload every limited weapon and wrecks that fall, destruction, skirmish AI, audio with entity loops and announcer alerts, replays and the editor tooling with the Map Editor.

In progress:

- **Multiplayer** works end to end. 500 units cost a client about 0.3 Mbps at 20 Hz snapshots.
- **Hardening**: no CI, stress scene or performance budgets yet.
- **Unit motion**: `MotionRigSystem` turns turrets, elevates barrels, plays recoil, rolls wheels, spins rotors up and down and
  tilts hulls on the client. Tracks don't scroll and suspension doesn't flex yet. See
  [motion rig](modules/client.md#motion-rig).
- **Vertex animation**: `HyperRTS/LitTeam` plays VAT clips for instanced infantry, and squads show one figure per
  share of health that idles, walks, runs, fires and dies with the unit. No clip blending, per-instance speed or
  figure spacing in formation yet. See [vertex animation](modules/client.md#vertex-animation).

## Decisions

| Decision | Why |
| --- | --- |
| Grid with distance fields and a sector graph, not a navmesh | Buildings, crushable scenery, decks and water restamp cells cheaply |
| Corridor A* for single moves, a flow field per formation order | One search serves a whole group; chases and one-off moves stay cheap |
| Sampled steering, not ORCA | Vehicles turn at their rate and radius, and walls count like neighbours |
| Server-authoritative Netcode, not lockstep | Burst float math is not deterministic across platforms |
| Replays record state, not commands | The simulation is not deterministic |
| One damage queue | Armor, splash and kill credit apply to every source |
| One stat-modifier buffer | Every bonus shares one formula and can be removed |
| Spawns instantiate baked prefabs | Spawned entities render like placed ones |
| UI Toolkit for all UI | Unity's recommendation. The rule is in [conventions](../CLAUDE.md#conventions) |

## Open

### Frozen until a game uses them

Abilities, aircraft, upgrades, veterancy, stealth, auras, build limits and deliveries work, but no shipped content
uses them yet. Keep them working and extend them only when a game needs it.

### Gameplay

- Moving obstacles, per-area water levels, ramps onto decks.
- Smarter AI: micro, scouting, defence, naval and air play.
- Ability targeting cursor, aircraft attack runs.
- Replays of projectiles, passengers and player stats.

### Multiplayer

- Ghost importance by distance, command feedback before the server confirms, an observer HUD.
- Host migration and automatic reconnect.

### Hardening

- CI running the EditMode tests and project validation, including closed map scenes.
- PlayMode tests, a stress scene, per-system budgets and a determinism test.

## Rules

- Generic and data-driven: authoring and assets over hard-coded values.
- Burst safe: unmanaged components, command buffers for structural changes, `SystemAPI.Time`.
- No `UnityEngine.Random` in the simulation. Break ties by entity index.
- Check the 500-unit scenario when you change movement, combat or the spatial index.
