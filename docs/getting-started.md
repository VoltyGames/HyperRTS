# Getting started: build your first RTS on HyperRTS

This guide takes you from an empty scene to a playable skirmish: a base, workers that build, harvesters that
bring in money, factories that train an army, and an AI opponent. Everything is done in the Editor with authoring
components; no code is needed until you want new mechanics.

If you just want to see the engine running, open `Assets/Demo/Scenes/SampleScene.unity` and press Play. That
scene is the finished result of this guide (a small C&C Generals-style map), and its prefabs in
`Assets/Demo/Prefabs/` are good templates to copy.

## 1. How a HyperRTS game is put together

- **Authoring GameObjects bake into entities.** Units, buildings, resource nodes and the match rules live inside a
  **SubScene**. On Play, Unity bakes them into ECS entities and the engine's systems take over. Anything outside the
  SubScene (camera, HUD, light, ground) stays a normal GameObject.
- **Prefabs are the unit and building types.** A producer lists unit prefabs, a builder lists building prefabs, and
  the engine instantiates the baked prefab at runtime. There is no separate database: the prefab *is* the
  definition (name, icon, cost, build time, health, weapon...).
- **Players are defined by one `Match` component.** It holds the map size, the player slots (team, colour, human or
  AI, starting resources) and AI settings. A unit's `Owner` field is a player slot number (1 = first player,
  0 = neutral).
- **All player intent is a command.** Mouse and hotkeys (or the AI) append `PlayerCommand`s to the player entity;
  simulation systems turn them into unit orders. Your game code can issue the same commands.

## 2. Create the scene

Use **HyperRTS ▸ Create RTS Scene...** and pick a path. You get:

| Object | What it is |
| --- | --- |
| `RTSWorld` | Prefab rig: Main Camera with `CameraController`, the `HUD` (resource bar, selection panel, command card, minimap), and the Netcode override that keeps single-player in one local world |
| `Ground` | A 200 × 200 plane. Clicks on empty ground fall back to the y = 0 plane, so the ground needs no collider |
| `Directional Light` | |
| `SubScene` | Holds `<Scene>_Entities.unity` with a `Match` object. Tick the SubScene's checkbox to edit its contents |

Doing it by hand instead: drop `Packages/com.hyperrts.engine/Prefabs/RTSWorld.prefab` (or **GameObject ▸ HyperRTS ▸ RTS World**)
into a scene, add a SubScene (**GameObject ▸ New Sub Scene ▸ Empty Scene**) and put a **GameObject ▸ HyperRTS ▸
Match** inside it.

## 3. Define your resources

**Assets ▸ Create ▸ HyperRTS ▸ Resources ▸ Resource Type**: one asset per resource ("Supplies", "Gold", "Oil").
Set its display name, colour and optional icon; the HUD shows each one the local player holds.

Then select the `Match` object and give each player slot its **Starting Resources**.

## 4. Make a unit

1. In the SubScene: **GameObject ▸ HyperRTS ▸ Unit**. You get a root with a box collider and a capsule model
   child. Replace the model with your own mesh; keep the collider on the root (clicks and right-click targets need
   it).
2. On the `Unit` component set **Display Name**, **Max Health**, **Vision Range**, **Move Speed**, **Radius**,
   **Population**, **Cost** and **Build Time**.
3. Give it abilities by adding components (Add Component ▸ HyperRTS):

   | Component | Makes the unit... |
   |---|---|
   | `Combat ▸ Weapon` | attack: range, damage, cooldown, optional projectile prefab, stance |
   | `Combat ▸ Armor` | take less (or more) damage from given **Damage Type** assets |
   | `Resources ▸ Harvester` | gather from resource nodes and return cargo to a drop-off |
   | `Buildings ▸ Builder` | place and construct the buildings listed in **Build Options** |

4. Drag it into a project folder to make it a prefab. Remove the instance from the scene unless you want it on
   the map at start (then set its **Owner**).

A weapon with no projectile prefab hits instantly (infantry rifles, melee). With a projectile prefab (any small
mesh, no collider) the shot flies to the target and deals damage on arrival.

## 5. Make buildings

**GameObject ▸ HyperRTS ▸ Building**, then on `Building` set the name, health, **Footprint** (blocked ground
area, also used for placement), **Population Provided**, cost and build time.

| Add | For a... |
| --- | --- |
| `Buildings ▸ Producer` | barracks / factory: list unit prefabs in **Production Options**, set **Spawn Offset** |
| `Resources ▸ Resource Drop-Off` | HQ / supply center: harvesters deposit here |
| `Combat ▸ Weapon` | defensive tower (fires once construction is finished) |

**Prerequisites** (on any unit or building) lists buildings the owner must have finished first, such as a War
Factory needing a Barracks. The HUD greys out buttons whose prerequisites or cost aren't met.

Buildings placed in the SubScene start finished. Buildings placed by players start as construction sites, and only
progress while a builder works on them.

## 6. Lay out the map

Inside the SubScene:

- Size the playable area with **Map Size** on `Match` (the yellow gizmo). The pathfinding and fog grids cover it.
- Place starting buildings and units and set each one's **Owner**. Give every player a drop-off and a building with
  **Population Provided**, or nobody can train units.
- **GameObject ▸ HyperRTS ▸ Resource Node** for deposits: pick a **Resource Type** and amount (regrowth > 0 makes it
  renewable).
- **GameObject ▸ HyperRTS ▸ Nav Obstacle** for rocks, cliffs and water units must path around.

## 7. Players and AI

Each `Match` player slot has a **Control**: `LocalHuman` (you), `AI` (built-in skirmish AI) or `Remote` (idle in
single player). An AI slot also picks a **Difficulty** (tuned under the Match's **AI** header) and an optional
**Build Order** asset (**Create ▸ HyperRTS ▸ Match ▸ AI Build Order**): buildings, units and upgrades with counts,
worked through in order with its builders and producers. Afterwards it trains freely, keeps harvesters working,
uses abilities and attacks the nearest enemy base once its wave size of idle combat units is ready.

A player is defeated when it loses every entity flagged **Counts For Victory**. When only one team is left the
HUD shows Victory or Defeat.

## 8. Play

| Input | Action |
| --- | --- |
| Left click / drag | Select (drag prefers your units). Double-click selects all of that type on screen |
| Shift / Ctrl + select | Add to / remove from selection |
| Right click | Smart command: move, attack an enemy, harvest a node, build a site, or set a producer's rally point |
| Shift + right click | Queue the order |
| A, then left click | Attack-move |
| S / H | Stop / hold position |
| Ctrl+1-5, 1-5 | Assign / recall control group |
| Arrow keys, screen edge, scroll, middle drag, Home | Camera pan, zoom, rotate, reset |

Command-card buttons train units, start building placement (left click to place, Shift to keep placing,
right click or Esc to cancel) and set stances. Click a queued unit to cancel it and get a refund.

## 9. Adding your own mechanics

Create your game's own assembly (for example `Assets/MyGame/MyGame.asmdef`) referencing `HyperRTS.Core` and
`HyperRTS.Simulation`, plus `HyperRTS.Presentation` or `HyperRTS.Input` if it renders or reads input. Keep
gameplay in a headless assembly, like the engine does.

- **New behaviour**: add a component with an authoring component and baker, and a Burst `ISystem` in one of the
  phase groups (`OrderSystemGroup`, `MovementSystemGroup`, `CombatSystemGroup`, `ProductionSystemGroup`,
  `LifecycleSystemGroup`). Systems are discovered automatically. Derive the authoring component from
  `AuthoringBehaviour` instead of `MonoBehaviour`, so it gets the engine's inspector warnings, entity summaries and
  validation (add rules as `AuthoringRule<T>` classes in an Editor assembly; see [`editor-ux.md`](editor-ux.md)).
- **New order** (patrol, escort, lay mines...): pick a value from `OrderType.Custom` upward, issue it with
  `OrderWriter.Issue`, and write a system that runs units whose `ActiveOrder` has your type. Disable `ActiveOrder`
  when the order is done and the unit moves on to its next queued order. To move, set and enable `MoveDestination`.
- **New command** (toggle a mode, call reinforcements): pick a value from `CommandType.Custom` upward, append it to the
  player's `PlayerCommand` buffer from your UI, and consume it in `OrderSystemGroup` (commands are cleared at the
  end of that group).
- **React to deaths** (score, bounty): in `LifecycleSystemGroup` after `DeathSystem`, query entities with `Dead`
  enabled; `LastAttacker` says who did it. They are destroyed at the end of the frame.
- **Deal damage or heal**: append a `DamageEvent` through a `DamageWriter` before `DamageSystem`, so armor, splash and
  kill credit apply. Negative amounts heal.
- **Ability and field effects**: give an ability no built-in effects and read `AbilityActivation` after
  `AbilitySystem`; read an entity's `FieldPresence` for what its area fields mean in your game. Change stats with a
  `StatModifier` whose `StatSource` kind is `StatSourceKind.Custom` (128) or above, so removing yours never strips
  the engine's.
- **Custom stat** (fuel capacity, jamming strength): pick a value from `Stat.Custom` (128) upward and add modifiers
  for it as usual. The engine never applies custom stats: write a system, last in `LifecycleSystemGroup` with a
  change filter on `StatModifier`, that sets your component's value to `StatMath.Apply(modifiers, bases, stat,
  value)` (`bases` is the entity's `BaseStat` buffer, which keeps the authored value).
- **HUD panel**: implement `IHUDPanel` (or derive from `HUDPanel` for a boxed one), subclass `HUDController`,
  override `CreatePanels` to add yours with `Add(panel, parent)` alongside or instead of the built-in ones, and put
  the subclass on your HUD in place of `HUDController`.
- **Spawn from code**: `ecb.Instantiate(prefabEntity)`, then set `LocalTransform` and `Faction`. Prefab entities
  come from authoring references such as a producer's options. Tests and tools can build complete entities without
  baking through `GameEntitySetup` / `UnitSetup` / `BuildingSetup` with an `EntityManagerWriter`.

### Mapping a C&C Generals-style design

| Generals feature | In HyperRTS |
| --- | --- |
| Dozer / worker | Unit + `Builder` |
| Supply truck, supply dock | Unit + `Harvester`; `Resource Node`; HQ with `Resource Drop-Off` |
| Barracks, War Factory | Building + `Producer`, `Prerequisites` |
| Power plants | Building **Power**: positive generates, negative draws; consumers go `Unpowered` while power is low |
| Infantry vs tank damage, thin rear armor | `Damage Type` assets + `Armor` multipliers and its Directional fields |
| Artillery, grenades | `Weapon` splash radius and edge damage |
| Defensive structures | Building + `Weapon` |
| Upgrades | `Upgrade` prefabs in a producer's **Research Options** |
| Veterancy | `Veterancy` ranks with stat bonuses; kills pay `experienceValue` |
| Repair and sell | Builders repair damaged buildings (right-click); **Sell** on the command card |
| Oil derricks, tech buildings | Building + `Capturable`; units with `Capturer` |
| Garrisons, transports | `Container` on the building or vehicle, `Passenger` on the units |
| General's powers, unit abilities | `Abilities` on units and buildings; player-level powers are `Ability` buffers on the player entity |
| Radar jamming, healing zones | `Area Field` with bonuses, heal or damage, and `FieldPresence` for game rules |
| Aircraft | Unit + `Flight` (Air layer); `Airfield` pads on a building; `Ammo` reloads while docked |

## Next steps

- [`modules.md`](modules.md): every module's components and systems, and how they talk to each other.
- [`architecture.md`](architecture.md): the assembly layout and the headless rule.
- [`world-setup.md`](world-setup.md): system order and the frame pipeline.
- [`roadmap.md`](roadmap.md): what's done and what's next.
