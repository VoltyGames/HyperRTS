# HyperRTS — Development Roadmap

A phased plan for building **HyperRTS**, a reusable RTS engine on Unity DOTS
(Entities/ECS). This is the engine track — games are built _on top_ of it later, so
every phase favors generic, data-driven, Burst-safe pieces over game-specific code.

> Keep this current. When a deliverable lands, check it off and note the
> commit/PR. When scope shifts, edit the phase rather than letting reality drift
> from the plan.

- **Engine:** Unity 6000.6.4f1 (6.6), URP 17.6, new Input System
- **DOTS stack:** Entities 6.6, Entities Graphics, Unity Physics, Netcode for Entities,
  Collections, Burst, Mathematics
- **Status legend:** ✅ done · 🟡 in progress · ⬜ not started

---

## Milestone overview

| Phase | Theme                          | Status | Exit criteria (one line)                                             |
| ----: | ------------------------------ | :----: | -------------------------------------------------------------------- |
|     0 | Foundation / scaffolds         |   ✅   | Core modules + Demo scene bake and run; systems are minimal but live |
|     1 | Selection & input              |   ✅   | Player can box/click-select rendered entities via Physics raycast    |
|     2 | Commands & orders              |   ⬜   | Selected units accept move/attack/stop orders; order queue exists    |
|     3 | Pathfinding & steering         |   ⬜   | Units route around obstacles and avoid stacking                      |
|     4 | Economy & resources            |   ⬜   | Harvest → deposit → stockpile loop runs end-to-end                   |
|     5 | Production & buildings         |   ⬜   | Buildings place, construct, and produce units from queues            |
|     6 | Rendering for factory entities |   ⬜   | Runtime-spawned entities render without a SubScene prefab            |
|     7 | Combat depth & AI              |   ⬜   | Targeting, auto-acquire, projectiles, basic combat AI                |
|     8 | Fog of war & vision            |   ⬜   | Per-faction vision reveals/hides entities and terrain                |
|     9 | Factions & players             |   ⬜   | Multiple factions with ownership, teams, resources, win/lose         |
|    10 | Multiplayer (Netcode)          |   ⬜   | Deterministic/replicated match across 2+ clients                     |
|    11 | Hardening & tooling            |   ⬜   | Tests, profiling budgets, docs, sample game                          |

Phases are roughly sequential but several overlap (e.g. rendering work in Phase 6
unblocks visual verification for everything after it). Dependencies are called out
per phase.

---

## Phase 0 — Foundation & scaffolds ✅

**Goal:** a compiling Core asmdef with the minimal ECS building blocks and a Demo
scene that bakes entities, so later phases have something to stand on.

**Done so far**

- ✅ Project/solution layout: layered asmdefs under `Assets/Modules/` — `HyperRTS.Core` (contracts),
  `HyperRTS.Simulation` (headless gameplay), `HyperRTS.Presentation`, `HyperRTS.Input`, `HyperRTS.Editor`
  (see [`architecture.md`](architecture.md))
- ✅ `Units` — `UnitTag`, `MovementSpeed`, `MoveDestination`, `MovementSystem`, `UnitEntityFactory`
- ✅ `Buildings` — `BuildingTag`, `ConstructionProgress`, `ConstructionSystem`, `BuildingEntityFactory`
- ✅ `Health` — `HealthComponent`, `DeathSystem` (OrderLast)
- ✅ `Attack` — `AttackTarget`, `MeleeAttackDamage`, `RangeAttackDamage`, `AttackCooldown`, `AttackSystem`
- ✅ `Resources` — `ResourceType`, `Resource`, `ResourceTag`, `ResourceManager` (static registry), editor tooling
- ✅ `Cameras` — `CameraController` (MonoBehaviour: pan/zoom/rotate/reset)
- ✅ Demo scene + EntitiesSubScene

**Remaining to close Phase 0**

- ✅ Establish an explicit system update order — ordered phase groups in `SystemGroups.cs`
  (`OrderSystemGroup` → `MovementSystemGroup` → `CombatSystemGroup` → `ProductionSystemGroup` →
  `LifecycleSystemGroup`); the four systems target these instead of `SimulationSystemGroup`
- ✅ First test assembly (`HyperRTS.Simulation.Tests`) with Burst-compiled system smoke tests
  (`SimulationSystemTests`: movement + death/lifecycle ordering)
- ✅ World-setup entry point documented in [`docs/world-setup.md`](../docs/world-setup.md) (how
  entities enter play: default world → SubScene baking → ordered groups → factory/test path)
- ✅ This roadmap referenced from `CLAUDE.md`

**Acceptance:** project opens in 6000.5.0f1, Demo scene plays, entities visible in
**Window ▸ Entities**, no compile errors in the Unity console.

---

## Phase 1 — Selection & input 🟡

**Goal:** turn rendered entities into selectable game objects.

**Deliverables**

- ✅ `Selection` module: `Selectable`/`Selected` (enableable)/`SelectableType`/`SelectionHighlightColors`
- ✅ Unity Physics raycast for click-select; drag-box for multi-select
- ✅ Modifiers: Shift add, Ctrl subtract, double-click select-all-of-type
- ✅ Input via a dedicated `InputActions` asset (`RTSInputActions`) — no legacy `UnityEngine.Input`
- ✅ Placeholder highlight (`URPMaterialPropertyBaseColor` tint) + UI Toolkit marquee
- ✅ Scene wiring: attach `SelectableAuthoring` to the Demo Unit; add `SelectionDragBoxUI` to the scene
- ✅ Verify: no console errors, tests green, Play-mode picking works

**Dependencies:** Unity Physics colliders on entities (the Demo Unit's `CapsuleCollider` bakes into one).

**Acceptance:** click and box-select highlight the right entities; the selection set is ECS-queryable.
See [`docs/selection.md`](selection.md).

---

## Phase 2 — Commands & orders ⬜

**Goal:** issue intent to the current selection.

**Deliverables**

- `Orders` module: order components (`MoveOrder`, `AttackOrder`, `StopOrder`, `HoldPosition`)
- An **order queue** buffer (`DynamicBuffer<OrderElement>`) with shift-to-queue
- Right-click context resolution (ground → move, enemy → attack, resource → gather, building → enter/garrison)
- Order-issuing system translating raycast hits + selection into order components
- `StopSystem`/order-clear semantics; `MovementSystem` consumes `MoveOrder` instead of raw `MoveDestination`

**Dependencies:** Phase 1 (selection), Phase 4 partial overlap (gather order target).

**Acceptance:** selected units execute right-click move/attack; queued orders run in order.

---

## Phase 3 — Pathfinding & steering ⬜

**Goal:** units reach destinations around obstacles without stacking.

**Deliverables**

- Navigation data: choose and document approach (flow-field for group RTS movement, or
  grid A\* + funnel) — **decision required, record the trade-off here when made**
- Grid/navmesh bake from terrain + static building footprints
- Pathfinding system producing waypoint buffers; movement follows waypoints
- Local avoidance / separation steering (boids-style) so units don't overlap
- Dynamic obstacle updates when buildings are placed/destroyed
- Formation movement (group keeps shape, arrives together)

**Dependencies:** Phase 2 (move orders), building footprints from Phase 5.

**Acceptance:** a group ordered across a map with obstacles routes around them, spreads
on arrival, and re-paths when a building blocks the route. Pathfinding stays in Burst
jobs and holds frame budget for N units (set target N here, e.g. 500).

---

## Phase 4 — Economy & resources ⬜

**Goal:** a working harvest loop and per-faction stockpiles.

**Deliverables**

- Promote `ResourceManager` (static, editor/setup-only) data into runtime per-faction stockpile components
- Resource nodes with finite amounts + depletion/regrowth
- Harvester behavior: travel → gather (capacity/time) → return → deposit → repeat
- Drop-off buildings; nearest-depot selection
- Resource UI hook (event/component the UI layer can read) — UI itself out of scope here

**Dependencies:** Phase 2 (gather orders), Phase 3 (pathing to nodes/depots), Phase 9 (faction ownership).

**Acceptance:** a harvester ordered onto a node runs the full loop unattended and a
faction stockpile increases; node depletes.

---

## Phase 5 — Production & buildings ⬜

**Goal:** build placement, construction, and unit production.

**Deliverables**

- Placement mode: ghost preview, grid snap, valid/invalid footprint checks (terrain + overlap)
- Construction tie-in: placed building uses existing `ConstructionProgress`/`ConstructionSystem`; builders contribute progress
- Production queue (`DynamicBuffer`) on producer buildings; cost deduction from Phase 4 stockpile
- Rally points and spawn-on-complete
- Tech/prerequisite gating (data-driven prerequisites)

**Dependencies:** Phase 4 (costs), Phase 3 (footprints as obstacles, rally pathing).

**Acceptance:** place a building, watch it construct, queue a unit, see it spawn at the
rally point with resources spent.

---

## Phase 6 — Rendering for factory entities ⬜

**Goal:** runtime-spawned entities render without authoring a SubScene prefab per type.

> Per CLAUDE.md gotcha: factory-created entities currently have data + `LocalTransform`
> but **no mesh**. This phase removes that limitation.

**Deliverables**

- Prefab/entity-prefab registry the factories pull renderable archetypes from (RenderMeshArray + MaterialMeshInfo)
- Convert factories to instantiate baked entity prefabs rather than bare `CreateEntity`
- LOD / culling sanity pass for large counts
- Optional: GPU-friendly instancing path for unit-heavy scenes

**Dependencies:** none hard; unblocks visual verification of Phases 1–5, so consider
pulling it earlier if iteration is painful.

**Acceptance:** units/buildings spawned by factories at runtime appear rendered in Game view.

---

## Phase 7 — Combat depth & AI ⬜

**Goal:** combat beyond the current cooldown-subtracts-health scaffold.

**Deliverables**

- Target acquisition: range checks, auto-acquire nearest enemy, threat/aggro rules
- Projectiles for ranged attacks (travel time, hit resolution) vs. instant melee
- Attack-move order; stances (aggressive/defensive/hold/passive)
- Damage model: armor/damage types, multipliers, falloff
- Death → corpse/cleanup, kill credit, on-death effects hook
- Basic combat micro AI (kiting, focus fire) as reusable behavior components

**Dependencies:** Phases 2, 3, and partial 1.

**Acceptance:** two groups auto-engage on contact; ranged units fire projectiles;
stances change behavior; `DeathSystem` integrates cleanly.

---

## Phase 8 — Fog of war & vision ⬜

**Goal:** per-faction vision and concealment.

**Deliverables**

- Vision components (sight radius) producing a per-faction visibility grid (Burst job)
- Three-state fog: unseen / explored / visible
- Reveal/hide of entities based on the observing faction's vision
- Terrain fog rendering (shader/overlay)
- Detection rules (stealth/cloak hooks, detectors)

**Dependencies:** Phase 9 (factions define "who sees"), Phase 6 (hide/show rendered entities).

**Acceptance:** units outside a faction's vision are hidden; explored-but-not-visible
terrain dims; entering vision reveals enemies.

---

## Phase 9 — Factions & players ⬜

**Goal:** ownership, teams, and match state.

**Deliverables**

- `FactionId`/owner component on all ownable entities; team/alliance relations
- Per-faction state: resources (ties into Phase 4), population/supply caps, tech
- Diplomacy/relations matrix (ally/enemy/neutral) consumed by targeting + fog
- Player controller abstraction (local human vs. AI) so input maps to a faction
- Victory/defeat conditions (data-driven)

**Dependencies:** threads through Phases 4, 7, 8. Introduce `FactionId` early if
convenient — it touches many components.

**Acceptance:** two factions coexist with separate resources/vision; attacking only
affects enemies; a win condition can resolve a match.

---

## Phase 10 — Multiplayer (Netcode for Entities) ⬜

**Goal:** networked matches.

**Deliverables**

- Pick model and document trade-offs here: **deterministic lockstep** (command sync,
  fits RTS unit counts) vs. **server-authoritative replication** (Netcode ghosts).
  **Decision required.**
- Ghost/relevancy config or lockstep command-frame scheduling
- Client prediction/reconciliation or deterministic sim verification (checksum)
- Lobby/connection flow; client→faction binding
- Latency, desync detection, rejoin handling

**Dependencies:** stable simulation from all prior phases; determinism constraints may
retro-influence earlier systems (avoid non-deterministic float/order dependencies).

**Acceptance:** 2+ clients play the same match in sync; no desync over a representative game.

---

## Phase 11 — Hardening, tooling & sample game ⬜

**Goal:** make the engine dependable and adoptable.

**Deliverables**

- Test coverage: per-module test assemblies; system unit tests; play-mode integration tests
- Performance budgets per system with profiler markers; stress scene (target unit count)
- Authoring/designer tooling (editor windows, gizmos, data validation) extending `HyperRTS.Editor`
- API docs / module READMEs; "how to build a game on HyperRTS" guide
- A minimal **sample game** in a separate asmdef proving the engine end-to-end

**Acceptance:** CI-style green tests, documented perf budgets, a playable sample built
only from engine APIs.

---

## Cross-cutting tracks (continuous)

These run alongside every phase rather than as discrete milestones:

- **Determinism discipline** — keep simulation deterministic from the start; it is far
  cheaper than retrofitting for Phase 10.
- **Burst/job safety** — components stay blittable; structural changes via ECB;
  cross-entity reads via `ComponentLookup<T>`; timing via `SystemAPI.Time`.
- **Data-driven design** — prefer ScriptableObject/baked config + components over
  hard-coded values, so games tune the engine without forking it.
- **Performance** — profile against a target unit count each phase; protect frame budget.
- **Testing & docs** — grow `HyperRTS.Simulation.Tests` and module docs as features land.

---

## Open decisions to record

Capture the choice and its rationale here when made, so future work doesn't relitigate:

- [ ] **Pathfinding model** (Phase 3): flow-field vs. grid A\*+funnel
- [ ] **Networking model** (Phase 10): deterministic lockstep vs. Netcode replication
- [ ] **Rendering archetype source** (Phase 6): entity-prefab registry vs. baked-blob lookup
- [ ] **FactionId rollout timing** (Phase 9): introduce early vs. retrofit
- [x] **Selection visuals** (Phase 1): per-entity `URPMaterialPropertyBaseColor` swap; ring/decal deferred to Phase 6.
- [x] **Selection input** (Phase 1): dedicated `InputActions` asset (`RTSInputActions`) over direct device polling.
