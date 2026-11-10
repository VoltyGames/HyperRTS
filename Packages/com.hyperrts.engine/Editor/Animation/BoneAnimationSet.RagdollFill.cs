using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Animation.Bones;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Animation
{
    // A first ragdoll for a humanoid rig, read from its avatar's bone map, for hand tuning after.
    public partial class BoneAnimationSet
    {
        private const float HeadHeight = 0.2f;
        private const float HeadRadius = 0.1f;

        /// <summary>A body to fill: its bone, the parent body's bone, the capsule's ends in object space, and its make-up.</summary>
        private readonly struct HumanBody
        {
            public readonly HumanBodyBones Bone;
            public readonly HumanBodyBones? Parent;
            public readonly Vector3 From;
            public readonly Vector3 To;
            public readonly float Radius;
            public readonly float Mass;
            public readonly float Cone;
            public readonly float Twist;

            public HumanBody(HumanBodyBones bone, HumanBodyBones? parent, Vector3 from, Vector3 to, float radius,
                float mass, float cone, float twist)
            {
                (Bone, Parent, From, To, Radius, Mass, Cone, Twist) = (bone, parent, from, to, radius, mass, cone, twist);
            }
        }

        /// <summary>Replaces the ragdoll with eleven bodies on a humanoid rig's pelvis, spine, head, arms and legs.</summary>
        [ContextMenu("Fill ragdoll from rig")]
        public void FillRagdoll()
        {
            var avatar = rig != null ? rig.GetComponentInChildren<Animator>()?.avatar : null;
            if (avatar == null || !avatar.isHuman)
            {
                throw new InvalidOperationException($"Bone set '{name}' needs a rig with a humanoid avatar to fill its ragdoll.");
            }

            var boneNames = new Dictionary<string, string>();
            foreach (var human in avatar.humanDescription.human)
            {
                boneNames.Add(human.humanName, human.boneName);
            }

            using var posed = new BoneRig(rig, skeleton, upperRoot, rootBone, sockets);
            var binds = posed.BindPoses();
            string Bone(HumanBodyBones bone) => boneNames.TryGetValue(HumanTrait.BoneName[(int)bone], out var found)
                ? found
                : throw new InvalidOperationException($"The avatar of bone set '{name}' maps no {bone}.");
            Matrix4x4 Rest(HumanBodyBones bone) => binds[posed.BoneIndex(Bone(bone))].inverse;

            Undo.RecordObject(this, "Fill ragdoll from rig");
            ragdoll = new List<RagdollBone>();
            foreach (var body in HumanBodies(bone => Rest(bone).GetPosition()))
            {
                ragdoll.Add(Fill(body, Rest(body.Bone), Bone(body.Bone), body.Parent.HasValue ? Bone(body.Parent.Value) : null));
            }

            EditorUtility.SetDirty(this);
        }

        // Mass shares follow adult segment masses; limits are a loose human range, tuned by hand after.
        private static IEnumerable<HumanBody> HumanBodies(Func<HumanBodyBones, Vector3> at)
        {
            var head = at(HumanBodyBones.Head);
            yield return new HumanBody(HumanBodyBones.Hips, null, at(HumanBodyBones.LeftUpperLeg), at(HumanBodyBones.RightUpperLeg), 0.5f, 0.15f, 0f, 0f);
            yield return Limb(at, HumanBodyBones.Spine, HumanBodyBones.Hips, HumanBodyBones.Neck, 0.28f, 0.35f, 30f, 30f);
            yield return new HumanBody(HumanBodyBones.Head, HumanBodyBones.Spine, head, head + Vector3.up * HeadHeight, HeadRadius / HeadHeight, 0.08f, 45f, 45f);
            foreach (var (upperArm, lowerArm, hand, upperLeg, lowerLeg, foot) in new[]
                     {
                         (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                             HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
                         (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                             HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
                     })
            {
                yield return Limb(at, upperArm, HumanBodyBones.Spine, lowerArm, 0.18f, 0.03f, 80f, 60f);
                yield return Limb(at, lowerArm, upperArm, hand, 0.15f, 0.02f, 60f, 20f);
                yield return Limb(at, upperLeg, HumanBodyBones.Hips, lowerLeg, 0.2f, 0.1f, 60f, 20f);
                yield return Limb(at, lowerLeg, upperLeg, foot, 0.15f, 0.06f, 70f, 10f);
            }
        }

        private static HumanBody Limb(Func<HumanBodyBones, Vector3> at, HumanBodyBones bone, HumanBodyBones parent,
            HumanBodyBones toward, float radius, float mass, float cone, float twist) =>
            new(bone, parent, at(bone), at(toward), radius, mass, cone, twist);

        // Radius is a share of the capsule's length; its core ends pull in by the radius so neighbours barely touch.
        private static RagdollBone Fill(HumanBody body, Matrix4x4 rest, string bone, string parentBone)
        {
            var place = rest.GetPosition();
            var toBone = Quaternion.Inverse(rest.rotation);
            var start = toBone * (body.From - place);
            var end = toBone * (body.To - place);
            var length = Vector3.Distance(start, end);
            var radius = body.Radius * length;
            var inset = (end - start).normalized * Mathf.Min(radius, 0.4f * length);
            var along = (end - start).normalized;
            var across = Vector3.ProjectOnPlane(toBone * Vector3.right, along).normalized;
            var limit = Mathf.Min(body.Cone, 90f);
            return new RagdollBone
            {
                bone = bone,
                parentBone = parentBone,
                capsuleStart = start + inset,
                capsuleEnd = end - inset,
                radius = radius,
                massShare = body.Mass,
                joint = new RagdollJoint
                {
                    perpendicularAxis = across,
                    coneAngle = body.Cone,
                    perpendicularMin = -limit,
                    perpendicularMax = limit,
                    twistMin = -body.Twist,
                    twistMax = body.Twist,
                },
            };
        }
    }
}
