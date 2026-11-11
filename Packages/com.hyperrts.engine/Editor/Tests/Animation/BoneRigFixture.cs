using System;
using HyperRTS.Editor.Animation;
using HyperRTS.Presentation.Animation.Bones;
using NUnit.Framework;
using UnityEngine;


namespace HyperRTS.Editor.Tests.Animation
{
    /// <summary>A small rig model, rig and clip for the bone rig tests.</summary>
    public abstract class BoneRigFixture : TemplateFixture
    {
        protected static readonly Vector3 HandAt = new(0.5f, 1.4f, 0f);
        protected static readonly Vector3 OffHandAt = new(0.6f, 1.6f, 0.4f);

        // Root > Armature > Hips > Hand (with a Grip empty holding a Support empty) and OffHand, and a skinned node
        // holding one triangle at the hand.
        protected GameObject Model(string name, Quaternion meshTurn, Vector3 handNudge = default)
        {
            var root = Track(new GameObject(name));
            var armature = Child(root.transform, "Armature", Vector3.zero, Quaternion.identity);
            var hips = Child(armature, "Hips", new Vector3(0f, 1f, 0f), Quaternion.identity);
            var hand = Child(hips, "Hand", HandAt - hips.position + handNudge, Quaternion.identity);
            var grip = Child(hand, "Grip", new Vector3(0.1f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            Child(grip, "Support", new Vector3(0f, 0f, 0.3f), Quaternion.identity);
            var offHand = Child(hips, "OffHand", OffHandAt - hips.position, Quaternion.identity);
            var body = Child(root.transform, "Body", Vector3.zero, meshTurn);
            var mesh = Track(new Mesh { name = name });
            mesh.vertices = Array.ConvertAll(new[] { HandAt, HandAt + Vector3.up * 0.1f, HandAt + Vector3.right * 0.1f }, body.InverseTransformPoint);
            mesh.normals = Array.ConvertAll(new[] { Vector3.back, Vector3.back, Vector3.back }, body.InverseTransformDirection);
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.boneWeights = new[] { Weighted(), Weighted(), Weighted() };
            mesh.bindposes = new[]
            {
                hips.worldToLocalMatrix * body.localToWorldMatrix, hand.worldToLocalMatrix * body.localToWorldMatrix,
                offHand.worldToLocalMatrix * body.localToWorldMatrix,
            };
            var skin = body.gameObject.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = new[] { hips, hand, offHand };
            return root;
        }

        private static BoneWeight Weighted() => new() { boneIndex0 = 1, weight0 = 1f };

        protected static Transform Child(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.SetLocalPositionAndRotation(position, rotation);
            return child;
        }

        protected readonly BoneAnimationSet.Socket _grip = new()
        {
            name = "Rifle", node = "Grip", support = "Support", supportHand = "OffHand",
        };

        internal BoneRig Rig() => new(Model("Rig", Quaternion.identity), "Body", "Hand", "Hips", new[] { _grip });

        // Bone texture order: Hips, Hand, OffHand, then the Grip socket.
        protected const int GripBone = 3;

        protected AnimationClip Still()
        {
            var clip = Track(new AnimationClip());
            clip.SetCurve("Hips", typeof(Transform), "localPosition.y", AnimationCurve.Constant(0f, 1f, 1f));
            return clip;
        }
    }
}
