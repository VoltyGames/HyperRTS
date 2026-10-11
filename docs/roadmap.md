# Roadmap

Status of the engine and the work still open. Update it when a feature lands or scope changes.

## Status

| Area | Status | Notes |
| --- | :---: | --- |
| Foundation: assemblies, phase groups, `TestWorld`, demo scene | ✅ | |
| Selection and input | ✅ | |
| Commands and orders | ✅ | Smart, queue, stop, hold, attack-move, patrol, escort, repair, capture, enter, unload |
| Pathfinding and movement | ✅ | Grid A*, formations, terrain height, nav layers, water, bridges |
| Economy | ✅ | |
| Bases and production | ✅ | Placement, construction, queues, rally points, prerequisites, repair, sell |
| Combat | ✅ | Stances, projectiles, splash, damage types, directional armor, ammo |
| Fog of war, stealth, detection | ✅ | Terrain line of sight |
| Players, teams, victory | ✅ | |
| Power, upgrades, veterancy, fields, abilities, capture, transports, aircraft | ✅ | |
| Skirmish AI | ✅ | Build orders, abilities, five difficulties |
| Audio | ✅ | Cues, network forwarding, pooled playback, voices |
| Replays | ✅ | State recording, playback, seek |
| Multiplayer | 🟡 | Works end to end; not measured at scale |
| Editor tooling | ✅ | Wizard, templates, validator, catalog, debug overlay, cheats |
| Hardening | 🟡 | No CI, no stress scene, no performance budgets |

## Decisions

| Decision | Why |
| --- | --- |
| Grid A* + smoothing + separation, no flow fields | Formation slots give every unit its own goal, so a shared field saves little |
| Server-authoritative Netcode, not lockstep | Burst doesn't promise cross-platform float determinism; Netcode is in the stack; the simulation is headless |
| Replays record state, not commands | The simulation isn't deterministic and entity indices differ between worlds |
| One damage queue (`DamageEvent`) | Armor, splash, facing and kill credit apply to every damage source |
| One stat-modifier buffer (`StatModifier`) | Upgrades, veterancy and fields share one formula and removable sources |
| Sound events name a prefab type and slot | One queue serves single player and clients |
| Spawns instantiate baked prefabs | Spawned entities render like placed ones |
| UI Toolkit for all UI (HUD, editor); uGUI only for Animation/Timeline UI; no IMGUI | Unity 6.6 recommendation |

## Open

### Gameplay

- Moving obstacles, per-area water levels, ramps onto decks.
- "Last seen" ghosts of enemy buildings.
- Micro AI (kiting, focus fire); AI scouting, defence, naval and air behaviour; fog-honest difficulties.
- Upgrades as prerequisites; ability targeting cursor and range preview; cargo slots in the selection panel.
- Aircraft: turn rates and attack runs, landed aircraft as surface targets, intercept stance, ammo readout.
- Audio: looping sounds, "under fire" and announcer alerts, batched sound messages.
- Replays: projectiles, resource nodes, passengers, player stats.

### Multiplayer

- 500-unit bandwidth test; ghost importance and send-rate tuning.
- Command feedback before the server confirms.
- Observer HUD (minimap, banner and overlays without a local player).
- Host migration, automatic reconnect.

### Architecture

- `CommandSystemGroup` and open order traits, so games add Smart resolutions and custom orders without edits.
- Shared `TargetedOrder` helper for the approach-then-act behaviours (build, repair, capture, gather, board).
- `Inoperative` state with reasons, replacing the repeated `WithNone<ConstructionProgress, Unpowered>` filters.
- Domain events (`Produced`, `Completed`, `OwnerChanged`) instead of inline sound and stats calls.
- One purchase rule (`ProducibleRules`) for placement, production, AI and HUD.
- Research as a generic producible in Production, removing the `Production → Upgrades` back-edge.
- `HyperRTS.Client` assembly for client state now in `Simulation/Interaction` and `LiveQuery`.
- Extensible command card (entries from registered sources, with grid slots and hotkeys).
- Explicit session phases and deferred `StatusChanged`.
- `TestWorld` that loads game assemblies.

### Hardening

- CI running EditMode tests and project validation, including closed map scenes.
- PlayMode tests, a stress scene, per-system budgets with profiler markers.
- Determinism test.

## Rules

- Generic and data-driven: authoring components and assets over hard-coded values.
- Burst and job safe: unmanaged components, ECB for structural changes, lookups for cross-entity access,
  `SystemAPI.Time`.
- No `UnityEngine.Random` or managed state in the simulation; ties broken by entity index.
- Check the 500-unit scenario when changing movement, combat or the spatial index.
