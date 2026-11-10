using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace HyperRTS.Presentation.Animation.Bones
{
    /// <summary>
    /// A bone clip table's bones and ragdoll in a blob, for the handover from a fall clip to physics bodies and the live
    /// pose after it. No bodies means the figure keeps its fall clip. Its methods aren't <c>readonly</c>: a copy of a
    /// blob array points nowhere.
    /// </summary>
    public struct Ragdoll
    {
        /// <summary>Per bone, from object space into the bone's space.</summary>
        public BlobArray<float4x4> BindPoses;

        /// <summary>Per bone, whether its texture rows are relative to the root bone of the same row.</summary>
        public BlobArray<bool> UpperBones;

        /// <summary>Per bone, the body it rides on; empty without a ragdoll.</summary>
        public BlobArray<int> BoneBodies;

        public BlobArray<RagdollBody> Bodies;

        public RagdollTuning Tuning;

        public static void Build(BlobBuilder builder, ref Ragdoll ragdoll, BoneClipTable table)
        {
            if (table.ragdoll.Count > 0 && table.bindPoses.Count > PoseSlots.MostBones)
            {
                throw new InvalidOperationException(
                    $"Bone clip table '{table.name}' has {table.bindPoses.Count} bones, but a ragdoll's pose slot holds {PoseSlots.MostBones}.");
            }

            var binds = builder.Allocate(ref ragdoll.BindPoses, table.bindPoses.Count);
            for (var i = 0; i < binds.Length; i++)
            {
                binds[i] = table.bindPoses[i];
            }

            var upper = builder.Allocate(ref ragdoll.UpperBones, table.upperBones.Count);
            for (var i = 0; i < upper.Length; i++)
            {
                upper[i] = table.upperBones[i];
            }

            var ridden = builder.Allocate(ref ragdoll.BoneBodies, table.boneBodies.Count);
            for (var i = 0; i < ridden.Length; i++)
            {
                ridden[i] = table.boneBodies[i];
            }

            var bodies = builder.Allocate(ref ragdoll.Bodies, table.ragdoll.Count);
            for (var i = 0; i < bodies.Length; i++)
            {
                bodies[i] = table.ragdoll[i];
            }

            ragdoll.Tuning = table.ragdollTuning;
        }

        public bool HasBodies => Bodies.Length > 0;

        public int BoneCount => BindPoses.Length;

        /// <summary>
        /// Every bone's skinning matrix as LitTeamBones.hlsl draws it: upper-body bones play <paramref name="upper"/> on
        /// the hips of <paramref name="lower"/>, which the rest play.
        /// </summary>
        public void Skins(in BoneTexture texture, in BoneSide lower, in BoneSide upper, NativeArray<float4x4> skins)
        {
            var hips = texture.Pose(0, lower);
            for (var bone = 0; bone < BoneCount; bone++)
            {
                skins[bone] = UpperBones[bone] ? math.mul(hips, texture.Pose(bone, upper)) : texture.Pose(bone, lower);
            }
        }

        /// <summary>Where body <paramref name="body"/> is in the world for <paramref name="skins"/> drawn at <paramref name="world"/>.</summary>
        public RigidTransform BodyWorld(int body, NativeArray<float4x4> skins, in float4x4 world)
        {
            var bone = Bodies[body].bone;
            var pose = math.mul(skins[bone], math.inverse(BindPoses[bone]));
            return Rigid(math.mul(world, pose));
        }

        public CapsuleGeometry Capsule(int body) => new()
        {
            Vertex0 = Bodies[body].capsuleStart,
            Vertex1 = Bodies[body].capsuleEnd,
            Radius = Bodies[body].radius,
        };

        /// <summary>The joint of <paramref name="body"/> to its parent at the body's bone origin, in both bodies' frames at rest.</summary>
        public void JointFrames(int body, out BodyFrame child, out BodyFrame parent)
        {
            ref var entry = ref Bodies[body];
            var twist = math.normalize((float3)entry.capsuleEnd - (float3)entry.capsuleStart);
            var across = (float3)entry.joint.perpendicularAxis;
            var perpendicular = math.normalize(across - math.dot(across, twist) * twist);
            child = new BodyFrame { Position = float3.zero, Axis = twist, PerpendicularAxis = perpendicular };
            var fromChild = math.mul(math.inverse(Rest(entry.parent)), Rest(body));
            parent = new BodyFrame
            {
                Position = fromChild.pos,
                Axis = math.rotate(fromChild.rot, twist),
                PerpendicularAxis = math.rotate(fromChild.rot, perpendicular),
            };
        }

        // A body's frame is its bone's rest position and rotation in object space, without scale.
        private RigidTransform Rest(int body) => Rigid(math.inverse(BindPoses[Bodies[body].bone]));

        private static RigidTransform Rigid(in float4x4 matrix)
        {
            var rotation = quaternion.LookRotationSafe(math.normalize(matrix.c2.xyz), math.normalize(matrix.c1.xyz));
            return new RigidTransform(rotation, matrix.c3.xyz);
        }
    }
}
