# Getting started

Build a skirmish against the AI in the editor, without code. The finished result is
`Assets/Demo/Scenes/SampleScene.unity`, and its prefabs in `Assets/Demo/Prefabs/` make good templates.

Gameplay objects are GameObjects in a SubScene that bake into entities. A unit or building type is a prefab. Players
are slots on the `Match` component, and an entity's **Owner** is a slot number: 1 is the first, 0 is neutral.

## 1. Create the scene

Run **HyperRTS ▸ Tools ▸ Maps ▸ New blank scene...** and pick the map size and player count. You get the `RTSWorld`
camera and HUD rig, a ground plane, a light, a `MatchState` for multiplayer and a SubScene holding the `Match`.
Tick the SubScene's checkbox to edit it.

## 2. Define resources

Create each resource with **Assets ▸ Create ▸ HyperRTS ▸ Resources ▸ Resource Type** and set its name, colour and
icon. Then give each player slot on `Match` its **Starting Resources**.

## 3. Make a unit

1. In the SubScene, add **GameObject ▸ HyperRTS ▸ Units ▸ Unit** or one of its variants. Swap the `Model` child
   for your mesh.
2. On `Unit`, set name, health, vision, speed, radius, population, cost and build time.
3. Add behaviour from **Add Component ▸ HyperRTS**:

   | Component | Gives the unit |
   | --- | --- |
   | `Combat ▸ Weapon` | An attack. Without a projectile prefab it hits instantly |
   | `Combat ▸ Armor` | Damage multipliers per **Damage Type** asset |
   | `Resources ▸ Harvester` | Gathering |
   | `Buildings ▸ Builder` | Constructing the buildings in **Build Options** |

4. Drag it into a project folder to make a prefab. Keep the scene copy only if it should start on the map.

## 4. Make buildings

Add **GameObject ▸ HyperRTS ▸ Buildings ▸ Building** and set its **Footprint** and **Population Provided**. Add
`Producer` for a barracks, `Resource Drop-Off` for a delivery point or `Weapon` for a tower.

**Prerequisites** lists buildings the owner must finish first. Buildings placed in the SubScene start finished.
Buildings placed by players are sites that grow only while a builder works on them.

## 5. Lay out the map

In the SubScene, set **Map Size** on `Match` and place each player's starting units and buildings. Every player
needs a drop-off and a building with **Population Provided**, or nobody can train units. Add
**GameObject ▸ HyperRTS ▸ Map ▸ Resource Node** for resources and **Nav Obstacle** for rocks and cliffs.

Run **HyperRTS ▸ Tools ▸ Validate** to list setup problems. Most have a one-click fix.

## 6. Players and AI

Set each slot's **Control** to `LocalHuman`, `AI` or `Remote`. An AI slot picks a **Difficulty** and can follow an
**AI Build Order** asset. AI players need the map's bot hints, which the [map builder](maps.md) writes. Without them
they stay idle.

A player is defeated after losing every entity flagged **Counts For Victory**.

## Play

| Input | Action |
| --- | --- |
| Left click or drag | Select. Double-click selects that type on screen |
| Shift / Ctrl + select | Add to / remove from the selection |
| Right click | Smart command: move, attack, gather, build, repair, enter, capture or rally |
| Shift + right click | Queue the order |
| A / P / E, then left click | Attack-move / patrol / escort |
| S / H | Stop / hold position |
| Esc | Cancel placement or a targeted command |
| Ctrl+1 to Ctrl+5 | Assign a control group |
| 1 to 5 | Recall a control group |
| Shift+1 to Shift+5 | Add to a control group |
| Arrows, screen edge, scroll, middle drag, Home | Pan, zoom, rotate, reset the camera |

The command card trains, researches, places buildings, uses abilities and sets stances. Shift keeps placing. Click
a queued item to cancel it for a refund.

## Next

- Game code on top of the engine: [extending](extending.md).
- Every component and system: [modules](modules.md).
- Multiplayer: [networking](networking.md).
