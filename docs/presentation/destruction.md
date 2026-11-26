# Destruction

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Destruction

How the client shows damage and death: damage stage models, falling trees, wrecks, debris and the sections of a
building breaking. For game developers giving units and buildings their remains. The rules behind them are in
[combat](../simulation/combat.md#destruction).

## Stages and falls

An entity shows the damage stage model for its health and bursts that stage's effect. Felled remains, such as trees, tip
over as a rod pivoting at the stump, bounce once and settle, or lie down at once if the fall was out of sight. The
tipping turns child models recorded in a `FallPart` buffer; the root keeps the simulation's final pose.

## Remains

The command buffer that spawns remains plays back after the transform systems, so a spawned prefab's parts would be
drawn at their baked places for a frame, leaving a blank frame between a building and its ruin. `Remains.Spawn` marks
fresh remains `Unplaced`, and `RemainsPlacementSystem` writes every part's `LocalToWorld` from its own and its
parents' transforms the frame the remains appear. It runs first in the presentation group, after `HullTiltSystem`.

The simulation keeps remains level. Every `Blocker` bakes a `HullTilt` and a
`PostTransformMatrix` (`HullTiltBakingSystem`), and `HullTiltSystem` writes the slope under its hull into that matrix
once when it appears, so the whole wreck tilts as the unit it came from did. A root the map already leaned turns only
by what is left, and a stretched root keeps its matrix and lies level.

## Model copies

Models a root shows only now and then (every damage stage but the first, every section model but the
intact one) bake as entity prefabs (`ModelCopyBakingSystem`), so a whole tree or building costs only the models on
screen. `ModelCopies` copies one in under the part it was authored under while it shows and destroys it when it hides.
A model saved at scale 0 bakes its world place at scale 1, since a copy is drawn its first frame from that place. A
copy takes the root's hide reasons and the values painters put on its meshes (team colour, fog fade, lights, build
progress, grime), and the root's `LinkedEntityGroup` lists it, so the root is destroyed whole.

## Debris

`DebrisAuthoring` on a ruin throws a `DebrisSet`'s pieces once when the ruin appears; on a building with
breakable parts it throws them from each part that breaks, and an `ImpactMark` throws its `debris` sets out of its
crater. `DebrisSystem` flies them on the client and lays them flat.
The set's material needs GPU instancing (checked at bake).

A piece at rest moves to `LastingDebris` and stays for the match, up to `LastingDebris.CapAt` per quality level; past the
cap the oldest buries into the ground. `LastingDebrisDraws` draws it through a `BatchRendererGroup`, which reads
matrices through the DOTS instancing path, so debris materials use a DOTS-instancing shader such as LitTeam. One
entity per piece would be simpler, but at 20,000 pieces it cost 20% more main-thread time and about 8 ms of render
thread.

## Sections

A building with `SectionsAuthoring` ([combat](../simulation/combat.md#sections)) gives each section models
(`SectionModel`): intact, rigid pieces, a stub and a heap, or a broken model, and holed variants (`SectionHole`).
Models other than the intact one are saved at scale 0, so editor views show the standing building, and bake as prefabs copied in while they
show (see [model copies](#model-copies)). `SectionViewSystem` shows each section by the simulation's broken mask:

- **Standing:** the intact model, or the holed variant nearest the latest impact the local player saw.
- **Breaking:** a break this client saw plays the pieces' clip once (`SectionClips`), blown in or out by the side the
  last impact came from (the hit that broke it counts), and the pieces stay on its last frame (`ShownSections.Settled`), so nothing jumps.
- **Broken:** stub and heap with no clip, for a break nobody saw or a building first seen broken.
- **Stub:** what stays standing shows only while a support of the section stands. A section that falls because its
  supports went shows none from its first frame, so nothing hangs in the air.
- **Resting debris:** pieces and heap hide while a section in `Section.Rests` is broken, and return on repair.
- **Repair:** the intact model returns.
- **Last section:** the simulation keeps a building whose every section is broken until its longest break clip has
  played (`SectionedBuilding.DiesAt`), so the ruin never replaces pieces still in the air.
