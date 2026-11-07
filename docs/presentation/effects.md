# Effects

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Effects

Visual effects: the rig prefab, VFX Graph conventions, the generated graphs, pools and loop lights. For artists and
developers making an effect. Building a graph step by step is in [editor](../editor/README.md#effect-graphs).

## Effects

An `EffectRig` is a prefab whose root holds child `VisualEffect` graphs, authored +Y up and +Z forward. Impacts turn +Y to
the ground normal. The rig sets a max distance, a scale range, scale per splash radius and ground alignment. The
validator reports a broken rig, naming the prefab: a graph without an asset, one that plays on its own (an Initial Event
Name) or one without `Density`.

No two plays need look the same:

- **Variants**: a rig may list alternative rigs, each its own pooled rig with none of its own. A burst plays the rig or a
  variant at random; a loop or trail keeps one per entity.
- **Random spin** turns each burst about +Y. It is off by default so muzzles keep their aim; impacts always get a random yaw.
- **Loops** play at a scale inside the rig's range, and with random spin at a yaw, both stable per entity.

`EffectGraph` sends each graph its `Burst` event with the pose as event attributes: `position`, `direction` (the rig's
+Y), `axisZ` and a uniform `scale` (per axis as `scaleX` to `scaleZ`). A graph drops events until it has run once, so
bursts in the frame the pool spawned it wait. The exposed float `Density` takes the effect detail share. Include files
serve graph authors as Custom HLSL blocks, among them `GraphBurstPose.hlsl` (Place At Burst, the end of every
Initialize) and `GraphFogOfWar.hlsl` (fog of war per particle in an output).

A graph that exposes `Speed` streams, for loops and trails. `GraphStreamPool` on the rig keeps copies of its stream
graphs, one `GraphStream` per playing stream. Each frame the stream sets `From`, `To`, `Up`, `Forward`, `Scale`, `Speed`
and `Density`, and sends `Play` after its first run. When `EffectStreams` drops a stream, or its variant changes, the
copy gets `Stop` and returns to the pool once its particles are gone, so stopping fades rather than cuts.

### Authoring

| Authoring | Plays |
| --- | --- |
| `EntityEffectsAuthoring` | A rig per cue slot. Fire plays at a `muzzle` socket |
| `ImpactEffectsAuthoring` | A rig per surface material where a weapon's shots land |
| `HitMaterialAuthoring` | Sets what an entity is made of, for hits on it |
| `LoopEffectsAuthoring` | Rigs that stream while shown, such as exhaust or smoke |
| `EffectTrailAuthoring` | A rig along a projectile's path |
| `MotionDustAuthoring` | Dust by ground material and speed |
| `RotorWashAuthoring` | Dust under a low rotorcraft by ground material |

The last four and their components live in `Presentation/Effects/Streams`, since only the client reads them. Rigs play
through [cues](../simulation/support.md#cues), on the client.

## Generated graphs

Graphs are generated from a `<Graph>.effect.asset` recipe beside them by `EffectGraphBuilder` (Editor/Effects). A recipe
is a list of layers, each one system of a kind (`glow`, `puff`, `sixway`, `ring`, `debris`, `spark`, `strip`, `distort`,
`source`) with plain fields for bursts or rates, start ranges, shape, forces, wind, curves over life, textures and
meshes. `EffectGraphInputs` holds the events and properties every system reads; `EffectLayerEmission` writes what spawns
a layer: bursts at Place At Burst, a constant rate between `Play` and `Stop` at Place Along Path
(`GraphStreamPose.hlsl`), or a GPU event from a parent layer's particles. Every layer gets Emit From Shape
(`GraphEmitShape.hlsl`), `Density`, World-space slots and the fog of war. Optional behaviours come from layer fields:
`ground` (bounce, `GraphGroundBounce.hlsl`), spark stretch, `drag` and `dragBySpeed`, wind drift (`GraphWind.hlsl`, from
`Wind`'s globals), `noise`, `orbit` and flipbook looping for `randomStart`.

Two shaders serve layers:

- `distort` draws through `HyperRTS/EffectDistort`, a Shader Graph with VFX support that refracts the opaque texture
  along a normal map at queue offset -50, before other transparents. It needs the camera's opaque and depth textures and
  fades near the ground and inside the fog of war.
- `sixway` and `ring` draw through `HyperRTS/EffectSixWay`, a premultiplied Shader Graph that shades with
  `SixWayLighting.hlsl`: sun with shadows and cookie, clustered lights, ambient, URP fog and the fog of war per pixel. The
  motion map (`FlipbookMotion.hlsl`) blends flipbook frames so slow sheets flow. The Unlit target declares the lighting
  and fog keywords itself.

All reflection into VFX Graph's internal editor model sits in `GraphModel`, so an Editor upgrade breaks that file alone.
The builder stamps the importer's `userData` with a hash of the recipe; `EffectGraphBuilder.Problem` names a missing or
stale graph and `EffectGraphBuilder.ShaderErrors` compiles a built graph's shaders so tests catch errors before first
draw. Recipes use `JsonFormat`'s layout.

## Pools and playback

`EffectPool` keeps one instance per prefab and replays it for every event, never instantiating per shot.
`EffectPoolSystem` reads the camera position once a frame for every range check and exposes it as `Eye`. Each streaming
system holds an `EffectStreams` built on the pool, whose `Step` finds the rig, checks range and moves the stream. Effects
play only for cues the client can see:

- **Bursts**: fire, death, ability and impact cues; impacts pick a rig by the surface material hit.
- **Loops**: effects at sockets while health is in range, such as fires on a damaged building. A building under
  construction streams none.
- **Trails** along a projectile's path.
- **Motion dust** by ground material and speed, measured from the drawn motion along the facing.

| Effect detail | Particles | Extra |
| --- | --- | --- |
| Low | 35 % | Loop range halved, no ambient life |
| Medium | 70 % | Smoke shadows |
| High | 100 % | Smoke shadows, light flashes from 8 shared lights, fire lights from 8 more |

Loops take part in the scene's light through two parts on a rig's root, both fed by `LoopEffectSystem`:

- **`LoopLight`**: a flickering point light (colour, range, intensity, height, flicker). On High, `LoopLights` gives its
  shadowless lights to the nearest such loops each frame, scaled by loop strength, with a flicker phase per stream.
- **`SmokeShadow`**: a smoke column's radius, height, opacity and grow time. From Medium up, `SmokeShadows` writes the 32
  nearest columns as shader globals (`_HyperRTSSmokeStarts`, `_HyperRTSSmokeEnds`, `_HyperRTSSmokeCount`) and
  `CloudShadows` draws them into the sun's cookie (see [atmosphere](environment.md#atmosphere)).
