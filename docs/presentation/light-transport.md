# Light transport

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Light transport

What takes part in Surface Cache GI's bounce light, and why moving and changing things stay out. For developers
adding rendered content or per-entity shader values.

## The rule

Only what never changes during a match bounces light: the map's own scenery and a fixed copy of the ground.
Units, everything spawned from a prefab (player buildings, wrecks, corpses, debris) and the Unity Terrain still
receive bounce light, but cast none.

Surface Cache GI keeps its own ray-traced copy of the scene and updates it on the main thread. Three changes are
costly there, and a battle makes all of them every frame:

| Change | Cost |
| --- | --- |
| A `[MaterialProperty]` component takes a new value on an entity in the copy | A new GI material for that entity: a Meta pass render and a managed allocation |
| The `TerrainData` of a terrain in the copy changes, as on a crater | The whole terrain is taken out and added again |
| An entity in the copy moves | Its transform is refitted |

## How it is set up

- `LightTransportLayer` names rendering layer 9. A game sets its Surface Cache volume's **Light Transport ▸ Rendering
  Layer Mask** to that layer alone (`LightTransportSettings.Restrict`), and only renderers on the layer bounce light.
  A volume left at Everything turns the rule off: then every rendered thing takes part.
- `LightTransportBakingSystem` puts every rendered entity a scene places on the layer, except the parts of units.
  Prefab entities never get it, so nothing spawned during a match joins.
- The map build adds a hidden `GroundBounce` mesh on the layer (`MapGroundBounceMesh`): the terrain's shape at about
  2 m a vertex, drawn with the terrain material, whose Meta pass gives the ground's colour from above. The Unity
  Terrain keeps the default layer, so craters and building levelling cost the GI nothing.
- `LightTransportSettings` holds the checks a game's validator runs: the layer's name and the volume's mask.

## Adding to it

- A new kind of map scenery needs nothing: placed by the scene, it bounces light. Its lit shader needs a
  [Meta pass](rendering.md#shaders).
- A GameObject renderer that should bounce light sets `renderingLayerMask` to include `LightTransportLayer.Mask`.
- Scenery that animates a `[MaterialProperty]` value, or a scene-placed thing that moves all match, should not be on
  the layer: clear the bit where it is baked.

## Gotchas

- Unity doesn't save `forceRenderingOff` with a scene, and a mesh that is never drawn is never uploaded, so it
  bounces no light. `GroundBounce` sets the first and uploads the mesh each time it is enabled.
- Surface Cache takes tens of seconds to settle after its copy of the scene changes. Compare captures only after it has.
- A map built before the ground mesh existed has no ground in the GI and looks darker: rebuild it.
