# Effects

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Effects

Visual effects: the rig prefab, VFX Graph conventions, the generated graphs, pools and loop lights. For artists and
developers making an effect. Building a graph step by step is in [editor](../editor/README.md#effect-graphs).

## Effects

An `EffectRig` is a prefab whose root holds child `VisualEffect` graphs, authored +Y up and +Z forward. Impacts turn +Y
to the ground normal. A rig may list variant rigs, played at random. The validator names a broken rig: a graph without
an asset, one that plays on its own (an Initial Event Name) or one without `Density`.

A graph must expose `Density`, which takes the effect detail share, and handle a `Burst` event. `EffectGraph` sends it
with the pose as attributes: `position`, `direction`, `axisZ` and `scale`. A graph drops events until it has run once,
so bursts in the frame the pool spawned it wait. `GraphBurstPose.hlsl` (Place At Burst) serves graph authors as a Custom
HLSL block.

A graph that exposes `Speed` streams, for loops and trails. `GraphStreamPool` keeps copies of the rig's stream graphs,
and each frame the stream sets `From`, `To`, `Up`, `Forward`, `Scale`, `Speed` and `Density`, and sends `Play`. When a
stream is dropped, the copy gets `Stop` and returns to the pool once its particles are gone, so stopping fades rather
than cuts.

### Authoring

| Authoring | Plays |
| --- | --- |
| `EntityEffectsAuthoring` | A rig per cue slot. Fire plays at a `muzzle` socket |
| `ImpactEffectsAuthoring` | A rig per surface material where a weapon's shots land |
| `HitMaterialAuthoring` | Sets what an entity is made of, for hits on it |
| `LoopEffectsAuthoring` | Rigs that stream while shown, such as exhaust or smoke |
| `EffectTrailAuthoring` | A rig along a projectile's path |
| `MotionDustAuthoring`, `RotorWashAuthoring` | Dust by ground material and speed, or under a low rotorcraft |

Rigs play through [cues](../simulation/support.md#cues), on the client, only for cues it can see.

## Generated graphs

`EffectGraphBuilder` (Editor/Effects) generates a graph from the `<Graph>.effect.asset` recipe beside it. A recipe is a
list of layers, each one system of a kind (`glow`, `puff`, `sixway`, `ring`, `debris`, `spark`, `strip`, `distort`,
`source`) with plain fields for bursts or rates, ranges, shape, forces, curves over life, textures and meshes. Every
layer gets placement, `Density` and fog of war. Edit the recipe, never the graph.

`distort` layers refract the opaque texture through `HyperRTS/EffectDistort`, which needs the camera's opaque and depth
textures. `sixway` and `ring` layers are lit through `HyperRTS/EffectSixWay`.

All reflection into VFX Graph's editor model sits in `GraphModel`. `EffectGraphBuilder.Problem` names a missing or stale
graph, and `EffectGraphBuilder.ShaderErrors` compiles a built graph's shaders so tests catch errors before first draw.

## Pools and playback

`EffectPool` keeps one instance per prefab and replays it for every event, never instantiating per shot.

| Effect detail | Particles | Extra |
| --- | --- | --- |
| Low | 35 % | Loop range halved, no ambient life |
| Medium | 70 % | Smoke shadows |
| High | 100 % | Smoke shadows, light flashes and fire lights |

Two parts on a rig's root join loops to the scene's light: `LoopLight` (a flickering point light, given to the nearest
loops on High) and `SmokeShadow` (a smoke column, drawn into the sun's cookie from Medium up, see
[atmosphere](environment.md#atmosphere)).
