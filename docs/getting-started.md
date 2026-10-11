# Getting started

From an empty project to a playable skirmish against the AI, in the editor, without code. The finished result is
`Assets/Demo/Scenes/SampleScene.unity`; its prefabs in `Assets/Demo/Prefabs/` are good templates.

## How a game is put together

| Concept | In HyperRTS |
| --- | --- |
| Gameplay objects | Authoring GameObjects inside a **SubScene** bake into entities. Camera, HUD, light and ground stay GameObjects |
| Unit and building types | Prefabs. The prefab is the definition: name, icon, cost, build time, health, weapon |
| Players | Slots on the one `Match` component: team, colour, human or AI, starting resources. A unit's **Owner** is a slot number (1 = first, 0 = neutral) |
| Player actions | `PlayerCommand`s on the player entity, from input or the AI. Game code can issue the same commands |

## 1. Create the scene

**HyperRTS ▸ Create RTS Scene...**: pick map size, player count and an optional starting-base prefab. You get:

| Object | What it is |
| --- | --- |
| `RTSWorld` | Rig prefab: camera with `CameraController`, the HUD, and the Netcode override that keeps single player in one world |
| `Ground` | 200 × 200 plane. Clicks on empty ground fall back to y = 0, so it needs no collider |
| `Directional Light` | |
| `MatchState` | Ghost that replicates the match state in multiplayer |
| `SubScene` | `<Scene>_Entities.unity` with the `Match`. Tick the SubScene's checkbox to edit it |

## 2. Define resources

**Assets ▸ Create ▸ HyperRTS ▸ Resources ▸ Resource Type**, one per resource ("Supplies", "Oil"). Set the name,
colour and icon. Then give each `Match` player slot its **Starting Resources**.

## 3. Make a unit

1. In the SubScene: **GameObject ▸ HyperRTS ▸ Units ▸ Unit** (or Combat Unit, Worker, Harvester). Replace the
   `Model` child with your mesh; keep the collider on the root.
2. On `Unit` set **Display Name**, **Max Health**, **Vision Range**, **Move Speed**, **Radius**, **Population**,
   **Cost**, **Build Time**.
3. Add behaviour with components (**Add Component ▸ HyperRTS**):

   | Component | Gives the unit |
   | --- | --- |
   | `Combat ▸ Weapon` | Attack: range, damage, cooldown, optional projectile prefab |
   | `Combat ▸ Armor` | Damage multipliers per **Damage Type** asset |
   | `Resources ▸ Harvester` | Gathering and returning to a drop-off |
   | `Buildings ▸ Builder` | Placing and constructing the buildings in **Build Options** |

4. Drag it into a project folder to make a prefab. Delete the scene instance unless it should start on the map
   (then set its **Owner**).

A weapon without a projectile prefab hits instantly. With one, the shot flies and deals damage on arrival.

## 4. Make buildings

**GameObject ▸ HyperRTS ▸ Buildings ▸ Building**, then set name, health, **Footprint**, **Population Provided**,
cost and build time.

| Add | For |
| --- | --- |
| `Buildings ▸ Producer` | Barracks, factory: unit prefabs in **Production Options**, **Spawn Offset** |
| `Resources ▸ Resource Drop-Off` | HQ, supply center |
| `Combat ▸ Weapon` | Defensive tower (fires once built) |

**Prerequisites** on any unit or building lists buildings the owner must have finished first. The HUD greys out
buttons whose prerequisites or cost aren't met.

Buildings placed in the SubScene start finished. Buildings placed by players start as sites and progress only
while a builder works on them.

## 5. Lay out the map

Inside the SubScene:

- **Map Size** on `Match` (yellow gizmo) sizes the playable area and the pathfinding and fog grids.
- Place starting buildings and units and set each **Owner**. Every player needs a drop-off and a building with
  **Population Provided**, or nobody can train units.
- **GameObject ▸ HyperRTS ▸ Map ▸ Resource Node**: pick a **Resource Type** and amount (regrowth > 0 makes it
  renewable).
- **GameObject ▸ HyperRTS ▸ Map ▸ Nav Obstacle** for rocks and cliffs.

Run **HyperRTS ▸ Validate** to list setup problems; most have a one-click fix.

## 6. Players and AI

Each player slot has a **Control**: `LocalHuman`, `AI` or `Remote` (idle in single player). An AI slot picks a
**Difficulty** (tuned under the Match's **AI** header) and an optional **Build Order**
(**Assets ▸ Create ▸ HyperRTS ▸ Match ▸ AI Build Order**).

A player is defeated after losing every entity flagged **Counts For Victory**. When one team is left, the HUD
shows the result.

## Play

| Input | Action |
| --- | --- |
| Left click / drag | Select. A drag prefers your units. Double-click selects all of that type on screen |
| Shift / Ctrl + select | Add to / remove from selection |
| Right click | Smart command: move, attack, gather, build, repair, enter, capture, or set a rally point |
| Shift + right click | Queue the order |
| A / P / E, then left click | Attack-move / patrol / escort |
| S / H | Stop / hold position |
| Esc | Cancel placement or a targeted command |
| Ctrl+1–5, 1–5, Shift+1–5 | Assign, recall, add control group |
| Arrows, screen edge, scroll, middle drag, Home | Pan, zoom, rotate, reset camera |

Command-card buttons train units, research, start placement (left click places, Shift keeps placing, right click
or Esc cancels), use abilities and set stances. Click a queued item to cancel it with a refund.

## Next

- Writing game code on top of the engine: [extending](extending.md).
- Every component and system: [modules](modules.md).
- Multiplayer: [networking](networking.md).
