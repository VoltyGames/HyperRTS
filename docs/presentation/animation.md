# Animation

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Animation

How models move on the client: baked bone and vertex animation, machines, the motion rig and squad figures. All of it
is cosmetic and reads replicated state. For artists and game developers setting up a unit or building.

## Bone animation

`HyperRTS/LitTeam` with `_BONES` skins a mesh in the vertex stage from a bone animation texture, so crowds of animated
infantry draw as instanced static meshes. Every pass, shadows and silhouettes included, reads the same pose. The source
is a `BoneAnimationSet` asset (an editor-only ScriptableObject, so rig models and clips never reach a build):

- `rig`: a model with a Generic or Humanoid rig; `skeleton` names the node holding the skinned mesh whose bones and bind
  poses form the skeleton. Any bone count and names work.
- `clips`: name, AnimationClip (from the rig's file or another), loop, `inPlace` and speed, sampled at `fps`. A Generic
  clip finds bones by path from the parent of `rootBone`, so clip file and rig must name the same bones under the same
  parent. `inPlace` removes the root bone's ground travel and records it as the clip's `groundSpeed` (m/s).
- `rootBone` is the top bone (the hips); `upperRoot` roots the upper body, which plays its own clip with the bones below it.
- `parts`: meshes to export, each one or more `nodes` merged into one mesh, from the rig or from the part's own
  `model`, so one set and texture serves every kit and weapon file on the skeleton. A plain node with a `socket` is a
  weapon or gear weighted wholly to one bone, with its origin on the socket node (`sockets`), so weapons are swappable
  meshes modelled about their grip.

**Bake** with the asset's context menu (**Assets ▸ HyperRTS ▸ Bake Bone Animation Set**) or the `bone bake [set]`
[command](../editor/README.md#commands). It writes `Generated/` beside the set: `<Set>_Bones` (a half-float texture of skin
matrices, one row per frame of every clip), `<Set>_Clips` (a `BoneClipTable` with the clips, rows and bounds) and a plain
mesh per part with bone indices and weights in UV6 and UV7. A skinned part whose bind pose differs from the skeleton's
fails the bake.

The material turns on `_BONES` and points `_BoneTex` at the texture. Per instance, `LowerClip` and `UpperClip` hold a
`PlayingClip` and `LowerFade` and `UpperFade` a `ClipFade`, so a switch blends from the frozen pose over
`_BoneFadeTime`. `BoneAnimatedAuthoring` on a renderer bakes the start clip and widens the render bounds to every row
(`ClipBoundsBakingSystem`); `ClipSubmeshBakingSystem` copies the clip to a multi-material renderer's extra entities.
Game code plays a clip with `PlayingClip.Switch` using `SystemAPI.Time.ElapsedTime`; `ClipClockSystem` feeds the same
clock to the shader as `_ClipTime`, so pause and replay speed apply.

## Rigid vertex animation

`_VAT_RIGID` moves whole pieces instead of vertices (a sectioned building breaking). The bake info
(`VAT/<Model>.vat.json`, written by the structure tool) names a `positions` and a `rotations` texture, a column per
piece and a row per frame, with row 0 the rest frame. A vertex holds its piece's column in UV1's u. The material takes
the textures in `_VATPositions` and `_VATRotations`. A `VATClipTable` imports the clips through **Import Bake Info**, and
`VATAnimatedAuthoring` beside the renderer bakes the start clip and bounds. A model with a `.vat.json` beside it imports
uncompressed with UV1 as 32-bit floats; the textures import exact (linear, point, clamped, no mipmaps). There is no
fade between clips.

## Machines

`MachineAuthoring` on a root and `MachinePartAuthoring` on each moving child animate pumpjacks, turbines, antennas,
cranes, launchers and doors. A part with no machine above it fails the bake. A part can:

| Motion | Meaning |
| --- | --- |
| `Spin`, `Swing` | Rotate about its axis; Swing in step with the spin |
| `Slide` | Move to and fro along its axis |
| `Link` | A rod between two transforms |
| `Downwind` | Face the scene's `Wind`, plus `offset` degrees |
| `Raise`, `Extend`, `Stretch` | Turn, move or scale (to `openScale`) as the machine speeds up, over its own `from` to `to` span of the speed; packing up runs the stages in reverse |
| `Point` | Keep its `aim` transform's bearing, so a ram and rod track each other |

`MachinePartRule` and `MachineRule` flag bad spans, a `Point` with no aim and zero ramps. A part a machine moves is left
out of the Motion Rig's fill, and a weapon with no barrel slot fires from the `Barrel` or `Launcher` its machine raises.

A machine's `runs` says when its clock turns: `Always`, `Powered` (complete and not `Offline`), `Producing` (queue has
work), `Firing` (for `keepRunningSeconds` after a weapon fired, from `WeaponPose` shot counts), `Deployed` (no ramp; speed
from the replicated `Deployable.Progress`), `Releasing` or `Dropping`. The clock eases over `rampSeconds`, and a machine
first seen mid-match starts at the speed it should have.

A door is a machine of its own: `Releasing` runs for `keepRunningSeconds` after the replicated `Producer.Released`
count of the producer above it rises, so enemies see it too. Set `rampSeconds` to the open time and `keepRunningSeconds`
to that plus the time it stays open. Use `Raise` for a hinged door, `Extend` for a sliding one and `Stretch` for a
roll-up curtain (it bakes a `PostTransformMatrix`). `Dropping` is for a cargo ramp or bomb bay under an aircraft with
`FlightAuthoring`, open while `DropRun.Active` is set. A `Releasing` machine without `ProducerAuthoring` above it, or a
`Dropping` one without `FlightAuthoring`, fails the bake.

## Motion rig

`MotionRigAuthoring` on a unit or building root moves the model's pivots on the client. Its slots list turrets, barrels
and launchers that ease onto a `WeaponPose` index, barrels that recoil, wheels, sprockets and idlers that roll, rotors
that spin up in flight and down while `Docked`, and a tilt root (the hull) that pitches and rolls with the ground under
its footprint (a flying unit leans with acceleration instead; a building never tilts). Ground vehicles also sway on a
`Suspension` spring: squat, dive, roll and recoil rock. Traverse and elevation limits are weapon data; the rig only
smooths the simulated pose.

**Fill from model** finds parts by art names: `Hull`, `Turret`, `Barrel`, `Launcher`, `Wheel_L01`, `Sprocket_L`,
`Idler_L`, `Rotor_Main`, `Rotor_Tail`, `Prop_01` on. Numbered `Turret_01` and `Launcher_01` aim weapon 0, `_02` weapon 1.
It keeps slots already listed, measures wheel radii and the footprint, fills the weapons' pivots, and lets same-named
parts of every model under the rig (a damaged model beside the intact one) follow. `MotionRigRule` checks every slot
names a child and a weapon the unit has.

Road wheels (`Wheel_` parts, `followsGround`) lift to the `TerrainHeight` under them within their `travel`. Tracks
(`Track_L` paired with the `Wheel_L` wheels) bend with them: a `TrackMeshAuthoring` on each track mesh bakes its span, and
`LitTeamTrack.hlsl` lifts the lower run by stations written to `_TrackLift0` and `_TrackLift1`. Track meshes must sit
unrotated and unscaled against the rig.

The baker stores a `MotionRig` and a `MotionRigPart` buffer on the root. `MotionRigSystem` runs in `PoseSystemGroup` on
the interpolated unit and writes only child `LocalTransform`s. A rig with rotors also gets a `RotorSpin` that
`LoopSoundSystem` reads. `PoseSystemGroup` runs first in `TransformSystemGroup` on presented worlds, so rig, machine and
fall poses render the same frame as the unit.

## Squad figures

`SquadFiguresAuthoring` on a squad unit draws it as hand-placed bone-animated figures: children of its `Model`, each with
`BoneAnimatedAuthoring` renderers (body levels of detail in a LODGroup, a weapon) sharing one `BoneClipTable`. Figure i
shows `SquadMember` i, so counts must match (`SquadFiguresRule`).

Each figure has a `FigureClipSet` that picks clips from the table by name: legs per posture, fire per posture, a
whole-body clip per `MemberAction` and a death per posture for a shot and for a blast from each side. Every entry is
required: an unset one or a missing name fails the bake and nothing falls back. `FigureClipSet.Build` resolves a set
into a shared `FigureClips` blob.

`SquadFigurePlacementSystem` (in `PoseSystemGroup`) eases each living figure toward its member's `Offset` and turns it to
its cover. `SquadFigureSystem` plays the clips in the presentation group:

- Legs play the clip for the member's `Posture` and horizontal speed. The upper body plays the posture's fire clip once
  after each change of the carried weapon's `WeaponPose.Shots`, else it follows the legs.
- Death plays the posture's fall, or for a `Blast` the fall for the side the replicated `BlastHeading` gives, then the
  figure hides. A squad spawned with dead members hides their figures without the fall.
- Clips are written only when state, posture or firing changes; looping clips start at a per-figure phase; a clip with a
  `groundSpeed` plays at the figure's speed over that, from 0.5 to 1.6 times, so feet don't slide.

A figure may name one other model, kept in `SquadFigure.Other`: an `upgradedRoot` (with `upgradedClips` and
`upgradedWeaponName`), swapped in when the member's `Weapon` differs from its `BaseWeapon`; or an `emplacedRoot` (with
`emplacedClips`), shown while the squad's `Deployable` is `Emplaced` and the member takes a crew place. Naming both is an
error. `deployedModel` is a child (a gun on its tripod) hidden by `HideReason.Figure` and shown from the start of
deploying until packing up begins; a member with `takesCrewPlace` walks to its place in the simulation (see
[combat](../simulation/squads.md#squads-and-members)).
