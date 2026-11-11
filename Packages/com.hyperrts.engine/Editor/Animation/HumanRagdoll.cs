using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Animation.Bones;
using UnityEngine;

namespace HyperRTS.Editor.Animation
{
    /// <summary>A first ragdoll for a humanoid rig, read from its avatar's bone map, for hand tuning after.</summary>
    internal sealed class HumanRagdoll
    {
        private const float HeadHeight = 0.2f;
        private const float HeadRadius = 0.1f;

        private readonly BoneAnimationSet _set;

        public HumanRagdoll(BoneAnimationSet set) => _set = set;

        /// <summary>How a body is made: capsule radius as a share of its length, mass share and joint limits in degrees.</summary>
        private readonly struct BodySettings
        {
            public readonly float Radius;
            public readonly float Mass;
            public readonly float Cone;
            public readonly float Twist;

            public BodySettings(float radius, float mass, float cone, float twist)
            {
                (Radius, Mass, Cone, Twist) = (radius, mass, cone, twist);
            }
        }

        /// <summary>A body to fill: its bone, the parent body's bone, the capsule's ends in object space, and its settings.</summary>
        private readonly struct HumanBody
        {
            public readonly HumanBodyBones Bone;
            public readonly HumanBodyBones? Parent;
            public readonly Vector3 From;
            public readonly Vector3 To;
            public readonly BodySettings Settings;

            public HumanBody(HumanBodyBones bone, HumanBodyBones? parent, Vector3 from, Vector3 to, BodySettings settings)
            {
                (Bone, Parent, From, To, Settings) = (bone, parent, from, to, settings);
            }
        }

        /// <summary>Eleven bodies on the rig's pelvis, spine, head, arms and legs.</summary>
        public List<RagdollBone> Build()
        {
            var boneNames = HumanBoneNames();
            using var posed = new BoneRig(_set.rig, _set.skeleton, _set.upperRoot, _set.rootBone, _set.sockets);
            var binds = posed.BindPoses();
            string Bone(HumanBodyBones bone) => boneNames.TryGetValue(HumanTrait.BoneName[(int)bone], out var found)
                ? found
                : throw new InvalidOperationException($"Bone set '{_set.name}' maps no {bone} in its avatar or Human Bones.");
            Matrix4x4 Rest(HumanBodyBones bone) => binds[posed.BoneIndex(Bone(bone))].inverse;

            var bodies = new List<RagdollBone>();
            foreach (var body in HumanBodies(bone => Rest(bone).GetPosition()))
            {
                bodies.Add(Fill(body, Rest(body.Bone), Bone(body.Bone), body.Parent.HasValue ? Bone(body.Parent.Value) : null));
            }

            return bodies;
        }

        // By human trait name: from a humanoid avatar, else from _set.humanBones.
        private Dictionary<string, string> HumanBoneNames()
        {
            var names = new Dictionary<string, string>();
            var avatar = _set.rig != null ? _set.rig.GetComponentInChildren<Animator>()?.avatar : null;
            if (avatar != null && avatar.isHuman)
            {
                foreach (var human in avatar.humanDescription.human)
                {
                    names.Add(human.humanName, human.boneName);
                }

                return names;
            }

            if (_set.humanBones.Count == 0)
            {
                throw new InvalidOperationException($"Bone set '{_set.name}' needs a humanoid avatar or Human Bones to fill its ragdoll.");
            }

            foreach (var human in _set.humanBones)
            {
                names.Add(HumanTrait.BoneName[(int)human.part], human.bone);
            }

            return names;
        }

        // Mass shares follow adult segment masses; limits are a loose human range, tuned by hand after.
        private static IEnumerable<HumanBody> HumanBodies(Func<HumanBodyBones, Vector3> at)
        {
            var head = at(HumanBodyBones.Head);
            yield return new HumanBody(HumanBodyBones.Hips, null, at(HumanBodyBones.LeftUpperLeg), at(HumanBodyBones.RightUpperLeg), new BodySettings(0.5f, 0.15f, 0f, 0f));
            yield return Limb(at, HumanBodyBones.Spine, HumanBodyBones.Hips, HumanBodyBones.Neck, new BodySettings(0.28f, 0.35f, 30f, 30f));
            yield return new HumanBody(HumanBodyBones.Head, HumanBodyBones.Spine, head, head + Vector3.up * HeadHeight, new BodySettings(HeadRadius / HeadHeight, 0.08f, 45f, 45f));
            foreach (var (upperArm, lowerArm, hand, upperLeg, lowerLeg, foot) in new[]
                     {
                         (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                             HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
                         (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                             HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
                     })
            {
                yield return Limb(at, upperArm, HumanBodyBones.Spine, lowerArm, new BodySettings(0.18f, 0.03f, 80f, 60f));
                yield return Limb(at, lowerArm, upperArm, hand, new BodySettings(0.15f, 0.02f, 60f, 20f));
                yield return Limb(at, upperLeg, HumanBodyBones.Hips, lowerLeg, new BodySettings(0.2f, 0.1f, 60f, 20f));
                yield return Limb(at, lowerLeg, upperLeg, foot, new BodySettings(0.15f, 0.06f, 70f, 10f));
            }
        }

        private static HumanBody Limb(Func<HumanBodyBones, Vector3> at, HumanBodyBones bone, HumanBodyBones parent,
            HumanBodyBones toward, BodySettings settings) =>
            new(bone, parent, at(bone), at(toward), settings);

        // Radius is a share of the capsule's length; its core ends pull in by the radius so neighbours barely touch.
        private static RagdollBone Fill(HumanBody body, Matrix4x4 rest, string bone, string parentBone)
        {
            var place = rest.GetPosition();
            var toBone = Quaternion.Inverse(rest.rotation);
            var start = toBone * (body.From - place);
            var end = toBone * (body.To - place);
            var length = Vector3.Distance(start, end);
            var radius = body.Settings.Radius * length;
            var along = (end - start).normalized;
            var inset = along * Mathf.Min(radius, 0.4f * length);
            var across = Vector3.ProjectOnPlane(toBone * Vector3.right, along).normalized;
            var limit = Mathf.Min(body.Settings.Cone, 90f);
            return new RagdollBone
            {
                bone = bone,
                parentBone = parentBone,
                capsuleStart = start + inset,
                capsuleEnd = end - inset,
                radius = radius,
                massShare = body.Settings.Mass,
                joint = new RagdollJoint
                {
                    perpendicularAxis = across,
                    coneAngle = body.Settings.Cone,
                    perpendicularMin = -limit,
                    perpendicularMax = limit,
                    twistMin = -body.Settings.Twist,
                    twistMax = body.Settings.Twist,
                },
            };
        }
    }
}
