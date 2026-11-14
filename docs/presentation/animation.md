# Animation

[Docs](../README.md) ▸ [Presentation](README.md) ▸ Animation

How models move on the client: baked bone and vertex animation, machines, the motion rig and squad figures. All of it
is cosmetic and reads replicated state. For artists and game developers setting up a unit or building.

## Bone animation

`HyperRTS/LitTeam` with `_BONES` skins a mesh from a bone animation texture, so crowds of animated infantry draw as
instanced static meshes. The source is a `BoneAnimationSet` asset (editor-only, so rig models and clips never reach a
build):

- `rig`: a model with a Generic or Humanoid rig; `skeleton` names the node holding the skinned mesh.
- `clips`: name, AnimationClip, loop, `inPlace`, speed and `bothHands`. A Generic clip finds bones by path, so clip
  file and rig must name the same bones under the same parent. `inPlace` strips the root's ground travel and records
  it as `groundSpeed`.
- `rootBone` is the hips and must be the skeleton's first bone; `upperRoot` roots the upper body, which plays its own
  clip.
- `sockets`: each is a rig node where gear sits, such as a rifle's grip under the right hand, and bakes as a bone of
  its own after the skeleton's. A socket with a `support` (a node under it, where the other hand holds the gear) and
  a `supportHand` bone swings its gear about the socket in `bothHands` clips, so the support meets that hand.
- `parts`: meshes to export. A node with a `socket` is gear carried by that socket's bone, so weapons are swappable
  meshes modelled about their grip, in the rig's own axes.
- `meshLods`: Mesh LOD levels Unity generates below each part mesh, so one renderer per part draws near and far
  without a LOD Group. They are index ranges over the same vertices, which the bone texture still finds.

**Bake** with **Assets ▸ HyperRTS ▸ Bake Bone Animation Set** or the `bone bake [set]`
[command](../editor/README.md#commands). It writes `Generated/` beside the set: a bone texture, a `BoneClipTable` and a
plain mesh per part, and reloads the meshes and texture so open scenes draw the new bake. A skinned part whose bind
pose differs from the skeleton's fails the bake. Upper-body bones are written relative to the root bone of their
frame, so the shader sets the upper body's clip on the hips of the legs' clip, and firing over a walk keeps the torso
on the walking hips.

The material turns on `_BONES`. `BoneAnimatedAuthoring` on a renderer bakes the start clip, render bounds and a
`PoseSlot` of -1. Game code plays a clip with `PlayingClip.Switch` using `SystemAPI.Time.ElapsedTime`, so pause and
replay speed apply.

A renderer whose `PoseSlot` is 0 or above draws a live pose instead of its clips: every bone's skinning matrix,
absolute in object space, from that slot of the global `_PoseSlots` buffer (DOTS instanced draws only). The material
default is -1 too, since slot 0 is a real slot. `PoseSlotSystem` keeps the `PoseSlots` singleton, uploads the slots
written each frame and caps how many may be taken by the effect level (`EffectDetail.RagdollsAt`: none on Low, 24 on
Medium, 48 on High). A slot holds up to `PoseSlots.MostBones` bones.

## Rigid vertex animation

`_VAT_RIGID` moves whole pieces instead of vertices (a sectioned building breaking). Its textures hold a column per
piece and a row per frame: `_Pos` the piece's pivot and `_Rot` its rotation, row 0 at rest. A vertex moves as
`pos[row] + rotate(rot[row], p - pos[0])`, its column in UV1.

A `RigidAnimationSet` bakes them from two models: the model whose meshes carry the column, and its motion model
`VAT/<Model>_Motion.fbx`. The motion model has an empty per piece with an integer `column` FBX property, keyed on every
frame of one take, and an empty per clip with `clip`, `startRow`, `frameCount` and `fps`. `VATImportRules` imports it
unreduced and keeps its pieces and clips inside it (`RigidMotion`), and gives the model beside it full-float UV1.
**Assets ▸ HyperRTS ▸ Bake Rigid Animation Set** or `rigid bake` samples the take into half-float EXRs (the set's
`positions` and `rotations`, else `T_<Set>_Pos.exr` and `_Rot.exr` beside it) and writes the `VATClipTable`'s width,
clips and bounds. Sampling the curves needs no scene; a channel without a curve keeps its node's pose.

`VATAnimatedAuthoring` beside the renderer bakes the start clip and bounds. There is no fade between clips.

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

A machine's `runs` says when its clock turns: `Always`, `Powered`, `Producing`, `Firing`, `Deployed`, `Releasing`,
`Dropping`, `Building`, `Gathering`, `Unloading` or `Loaded`. The clock eases over `rampSeconds`. `Deployed` and
`Loaded` skip the ease and take the speed outright: the unit's deploy progress, or its harvester's cargo over capacity
(draining across the unload), so Raise and Extend parts appear one by one as a truck fills, each over its own span.
`Building` runs while the builder's `BuildWork` flag is on and for `keepRunningSeconds` after; `Gathering` runs while the
harvester fills at a node and `Unloading` while it stands at the drop-off. These read replicated state (`BuildWork`,
`HarvestState`, `Harvester.CargoAmount`), so clients show them. A `Building` machine needs `BuilderAuthoring` above it and
the harvester kinds need `HarvesterAuthoring`, or the bake fails.

A door is a machine of its own. `Releasing` runs after the producer above it releases a unit, so enemies see it too: set
`rampSeconds` to the open time and `keepRunningSeconds` to that plus the time it stays open. Use `Raise` for a hinged
door, `Extend` for a sliding one and `Stretch` for a roll-up curtain. `Dropping` is for a cargo ramp or bomb bay under
an aircraft with `FlightAuthoring`. A `Releasing` machine without `ProducerAuthoring` above it, or a `Dropping` one
without `FlightAuthoring`, fails the bake.

## Motion rig

`MotionRigAuthoring` on a unit or building root moves the model's parts on the client; each part is one mesh whose
origin is its pivot. Its slots list turrets, barrels
and launchers that ease onto a `WeaponPose` index, wheels, rotors, and a tilt root (the hull) that follows the ground.
An aircraft's tilt root leans instead: nose down at cruise speed and as it speeds up, nose up as it brakes, and
banked into its turns.
Traverse and elevation limits are weapon data; the rig only smooths the simulated pose.

A trailer slot swings a towed part, pivoted on its hitch, after the hull as a real trailer does: its angle to the
hull opens as the hull turns and closes as it rolls, held short of a jackknife. It is looks only; the unit still
moves and collides as one rigid hull.

**Fill from model** finds parts by art names: `Hull`, `Turret`, `Barrel`, `Launcher`, `Wheel_L01`, `Sprocket_L`,
`Idler_L`, `Rotor_Main`, `Trailer` (its axle length measured from the `Wheel_` parts under it). Numbered `Turret_01` aims weapon 0, `_02` weapon 1. It keeps slots already listed.
`MotionRigRule` checks every slot names a child and a weapon the unit has.

Road wheels (`Wheel_` parts, `followsGround`) lift to the terrain under them. Tracks bend with them through a
`TrackMeshAuthoring` on each track part, which must sit unrotated and unscaled against the rig.

The rig works out its pose on its own entity; each part, linked to its rig at bake (`MotionRigLink`), then poses
itself from it, so every job writes only the entities it runs on.

## Squad figures

`SquadFiguresAuthoring` on a squad unit draws it as hand-placed bone-animated figures: children of its `Model`, each
with `BoneAnimatedAuthoring` renderers sharing one `BoneClipTable`. Figure i shows `SquadMember` i, so counts must match
(`SquadFiguresRule`).

Each figure has a `FigureClipSet` that picks clips from the table by name: legs and fire per posture, a whole-body clip
per `MemberAction` and deaths per posture. Every entry is required: an unset one or a missing name fails the bake and
nothing falls back. `SquadFigureSystem` plays them; a clip with a `groundSpeed` plays at the figure's speed over that,
so feet don't slide. The squad records each figure's clips, and every figure part (`FigurePart`, added at bake) picks
up a change itself, so only chunks with a changed part are written. `SquadFigurePlacementSystem` turns a figure to its weapon's `WeaponPose` yaw while it fires (a
launcher or an extra weapon aims on its own; a hull-fixed rifle turns the squad), else to its cover heading, else to
the squad's facing.

A figure may name one other model: an `upgradedRoot`, swapped in when the member's `Weapon` differs from its
`BaseWeapon`, or an `emplacedRoot`, shown while the squad is `Emplaced` and the member takes a crew place. Naming both
is an error. `deployedModel` is a child (a gun on its tripod) shown while the squad deploys (see
[combat](../simulation/squads.md#squads-and-members)).

### Deaths and ragdolls

A dead member's figure plays its fall (the shot fall, or the blast fall for the side the blast came from) and hides
after `dieHold`. When the bone set has a ragdoll, `CorpseSystem` decides at the handover, `handoverTime` into the fall:
a figure within `cameraDistance` of the camera, with a pose slot free, becomes a `Corpse`; the rest keep their fall.
A frame makes and hands over at most `EffectDetail.CorpsesPerFrameAt` corpses, nearest the camera first, so a blast
that kills hundreds spreads its cost over frames.

- A corpse draws through its own copies of the figure's renderers (`VisualPrefab.Copy`), listed in its
  `LinkedEntityGroup` at the figure's world place, so it outlives the squad ghost. The squad's figure hides.
- The handover reads the pose the fall draws now from the readable bone texture (`BoneTexture`, `Ragdoll.Skins`, the
  shader's sums) into the slot, so nothing pops. One capsule body per ragdoll body starts there in
  [physics world 1](README.md#physics), where it lands on the terrain, buildings and vehicles, moving as the clip did
  over its last row, joined by `PhysicsJoint.CreateRagdoll` with the baked limits. A blast death adds `blastSpeed`
  away from the blast and `blastLift` up.
- `CorpsePoseSystem` writes each bone into the slot as its body moved since the handover (bones without a body ride
  on `boneBodies`) and keeps the copies' render bounds round the bodies. Once every body is under `restSpeed` for
  `restTime`, or after `settleTime`, the bodies go and the slot keeps the last pose. The corpse and its slot go at the
  figure's hide time.
- The client never sees the last member die: the ghost goes the tick it does, often with every member in one blast.
  Each squad keeps a cleanup `SquadEnd` and a `ShownFigure` per figure (place, posture, fall). Every squad's death plays
  a death cue, sound or not (`CueWriter`), so when the ghost goes and a seen death cue of its type lands on it within
  `CueWait`, its figures still on show wait to fall as corpses copied from the prefab: falling ones carry on, living
  ones fall as if shot, then hand over as above. One the frame's budget leaves waits, and past `FallWait` it hides
  with its squad. A ghost that goes into fog has no death cue and leaves nothing.

The tuning (`RagdollTuning`) sits on the `BoneAnimationSet` beside its ragdoll bodies and bakes through the clip table
into `FigureClips.Ragdoll`. Headless and server worlds run none of this, and a `-nographics` client keeps no slots.
