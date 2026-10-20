# Roadmap

What the engine does today, why, and what is still open. Update it when a feature lands.

## Status

Done and tested: selection and orders, pathfinding over terrain, water and bridges, economy, bases and production,
combat, fog of war and stealth, power, upgrades, veterancy, fields, abilities, capture, transports, aircraft,
destruction, skirmish AI, audio, replays and the editor tooling with the Map Editor.

In progress:

- **Multiplayer** works end to end. 500 units cost a client about 0.3 Mbps at 20 Hz snapshots.
- **Hardening**: no CI, stress scene or performance budgets yet.

## Decisions

| Decision | Why |
| --- | --- |
| Grid A*, no flow fields | Formation slots give each unit its own goal |
| Server-authoritative Netcode, not lockstep | Burst float math is not deterministic across platforms |
| Replays record state, not commands | The simulation is not deterministic |
| One damage queue | Armor, splash and kill credit apply to every source |
| One stat-modifier buffer | Every bonus shares one formula and can be removed |
| Spawns instantiate baked prefabs | Spawned entities render like placed ones |
| UI Toolkit for all UI | Unity 6.6's recommendation. uGUI only for animated UI |

## Open

### Frozen until a game uses them

Abilities, aircraft, upgrades, veterancy, stealth, auras, build limits and deliveries work, but no shipped content
uses them yet. Keep them working and extend them only when a game needs it.

### Gameplay

- Moving obstacles, per-area water levels, ramps onto decks.
- Smarter AI: micro, scouting, defence, naval and air play.
- Ability targeting cursor, aircraft attack runs, announcer alerts.
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
