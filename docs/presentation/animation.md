# Animation

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Animation

How models move on the client: baked bone and vertex animation, machines, the motion rig and squad figures. All of it
is cosmetic and reads replicated state. For artists and game developers setting up a unit or building.

## Bone animation

`HyperRTS/LitTeam` with `_BONES` skins a mesh from a bone animation texture, so crowds of animated infantry draw as
instanced static meshes. The source is a `BoneAnimationSet` asset (editor-only, so rig models and clips never reach a
build):

- `rig`: a model with a Generic or Humanoid rig; `skeleton` names the node holding the skinned mesh.
- `clips`: name, AnimationClip, loop, `inPlace` and speed. A Generic clip finds bones by path, so clip file and rig must
  name the same bones under the same parent. `inPlace` strips the root's ground travel and records it as `groundSpeed`.
- `rootBone` is the hips; `upperRoot` roots the upper body, which plays its own clip.
- `parts`: meshes to export. A node with a `socket` is a weapon or gear weighted to one bone, so weapons are swappable
  meshes modelled about their grip.

**Bake** with **Assets ▸ HyperRTS ▸ Bake Bone Animation Set** or the `bone bake [set]`
[command](../editor/README.md#commands). It writes `Generated/` beside the set: a bone texture, a `BoneClipTable` and a
plain mesh per part. A skinned part whose bind pose differs from the skeleton's fails the bake.

The material turns on `_BONES`. `BoneAnimatedAuthoring` on a renderer bakes the start clip and render bounds. Game code
plays a clip with `PlayingClip.Switch` using `SystemAPI.Time.ElapsedTime`, so pause and replay speed apply.

## Rigid vertex animation

`_VAT_RIGID` moves whole pieces instead of vertices (a sectioned building breaking). The structure tool writes
`VAT/<Model>.vat.json` with a positions and a rotations texture. A `VATClipTable` imports the clips through **Import
Bake Info**, and `VATAnimatedAuthoring` beside the renderer bakes the start clip and bounds. There is no fade between
clips.

## Machines

`MachineAuthoring` on a root and `MachinePartAuthoring` on each moving child animate pumpjacks, turbines, antennas,
cranes, launchers and doors. A part with no machine above it fails the bake. A part's motion is one of:

| Motion | Meaning |
| --- | --- |
| `Spin`, `Swing`, `Slide` | Rotate, rock or move to and fro along an axis |
| `Link` | A rod between two transforms |
| `Downwind` | Face the scene's `Wind` |
| `Raise`, `Extend`, `Stretch` | Turn, move or scale over a span of the machine's speed; packing up reverses it |
| `Point` | Keep an `aim` transform's bearing |

A machine's `runs` says when its clock turns: `Always`, `Powered`, `Producing`, `Firing`, `Deployed`, `Releasing` or
`Dropping`. The clock eases over `rampSeconds`.

A door is a machine of its own. `Releasing` runs after the producer above it releases a unit, so enemies see it too: set
`rampSeconds` to the open time and `keepRunningSeconds` to that plus the time it stays open. Use `Raise` for a hinged
door, `Extend` for a sliding one and `Stretch` for a roll-up curtain. `Dropping` is for a cargo ramp or bomb bay under
an aircraft with `FlightAuthoring`. A `Releasing` machine without `ProducerAuthoring` above it, or a `Dropping` one
without `FlightAuthoring`, fails the bake.

## Motion rig

`MotionRigAuthoring` on a unit or building root moves the model's pivots on the client. Its slots list turrets, barrels
and launchers that ease onto a `WeaponPose` index, wheels, rotors, and a tilt root (the hull) that follows the ground.
Traverse and elevation limits are weapon data; the rig only smooths the simulated pose.

**Fill from model** finds parts by art names: `Hull`, `Turret`, `Barrel`, `Launcher`, `Wheel_L01`, `Sprocket_L`,
`Idler_L`, `Rotor_Main`. Numbered `Turret_01` aims weapon 0, `_02` weapon 1. It keeps slots already listed.
`MotionRigRule` checks every slot names a child and a weapon the unit has.

Road wheels (`Wheel_` parts, `followsGround`) lift to the terrain under them. Tracks bend with them through a
`TrackMeshAuthoring` on each track mesh. Track meshes must sit unrotated and unscaled against the rig.

## Squad figures

`SquadFiguresAuthoring` on a squad unit draws it as hand-placed bone-animated figures: children of its `Model`, each
with `BoneAnimatedAuthoring` renderers sharing one `BoneClipTable`. Figure i shows `SquadMember` i, so counts must match
(`SquadFiguresRule`).

Each figure has a `FigureClipSet` that picks clips from the table by name: legs and fire per posture, a whole-body clip
per `MemberAction` and deaths per posture. Every entry is required: an unset one or a missing name fails the bake and
nothing falls back. `SquadFigureSystem` plays them; a clip with a `groundSpeed` plays at the figure's speed over that,
so feet don't slide.

A figure may name one other model: an `upgradedRoot`, swapped in when the member's `Weapon` differs from its
`BaseWeapon`, or an `emplacedRoot`, shown while the squad is `Emplaced` and the member takes a crew place. Naming both
is an error. `deployedModel` is a child (a gun on its tripod) shown while the squad deploys (see
[combat](../simulation/squads.md#squads-and-members)).
