using System;
using System.Collections.Generic;
using HyperRTS.Editor.Animation;
using HyperRTS.Presentation.Animation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Tests.Animation
{
    /// <summary>
    /// A bone set's parts come from other models on the same skeleton: a skinned part whose node turns differently
    /// moves into the skeleton mesh's space, one at another rest pose fails, and a weapon sits with its origin on its
    /// socket node and follows the bone above it.
    /// </summary>
    public class BoneRigTests
    {
        private static readonly Vector3 HandAt = new(0.5f, 1.4f, 0f);
        private readonly List<Object> _made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var made in _made)
            {
                Object.DestroyImmediate(made);
            }

            _made.Clear();
        }

        // Root > Armature > Hips > Hand (with a Grip empty), and a skinned node holding one triangle at the hand.
        private GameObject Model(string name, Quaternion meshTurn, Vector3 handNudge = default)
        {
            var root = Track(new GameObject(name));
            var armature = Child(root.transform, "Armature", Vector3.zero, Quaternion.identity);
            var hips = Child(armature, "Hips", new Vector3(0f, 1f, 0f), Quaternion.identity);
            var hand = Child(hips, "Hand", HandAt - hips.position + handNudge, Quaternion.identity);
            Child(hand, "Grip", new Vector3(0.1f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            var body = Child(root.transform, "Body", Vector3.zero, meshTurn);
            var mesh = Track(new Mesh { name = name });
            mesh.vertices = Array.ConvertAll(new[] { HandAt, HandAt + Vector3.up * 0.1f, HandAt + Vector3.right * 0.1f }, body.InverseTransformPoint);
            mesh.normals = Array.ConvertAll(new[] { Vector3.back, Vector3.back, Vector3.back }, body.InverseTransformDirection);
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.boneWeights = new[] { Weighted(), Weighted(), Weighted() };
            mesh.bindposes = new[] { hips.worldToLocalMatrix * body.localToWorldMatrix, hand.worldToLocalMatrix * body.localToWorldMatrix };
            var skin = body.gameObject.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = new[] { hips, hand };
            return root;
        }

        private static BoneWeight Weighted() => new() { boneIndex0 = 1, weight0 = 1f };

        private static Transform Child(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.SetLocalPositionAndRotation(position, rotation);
            return child;
        }

        private T Track<T>(T made) where T : Object
        {
            _made.Add(made);
            return made;
        }

        private BoneRig Rig() => new(Model("Rig", Quaternion.identity), "Body", "Hand", "Hips");

        [Test]
        public void SkinnedPartFromAnotherModel_MovesIntoTheSkeletonMeshSpace()
        {
            using var rig = Rig();
            var kit = Model("Kit", Quaternion.Euler(-90f, 0f, 0f));

            var mesh = Track(rig.SkinnedMesh(kit, "Body"));

            Assert.AreEqual(0f, Vector3.Distance(HandAt, mesh.vertices[0]), 1e-4f);
            Assert.AreEqual(0f, Vector3.Distance(HandAt + Vector3.up * 0.1f, mesh.vertices[1]), 1e-4f);
            Assert.AreEqual(0f, Vector3.Distance(Vector3.back, mesh.normals[0]), 1e-4f);
            var indices = new List<Vector4>();
            mesh.GetUVs(BoneAnimatedAuthoring.IndexChannel, indices);
            Assert.AreEqual(1f, indices[0].x, "the hand, by its index in the skeleton");
        }

        [Test]
        public void MergedNodes_MakeOneMeshKeepingTheirBones()
        {
            using var rig = Rig();
            var kit = Model("Kit", Quaternion.Euler(-90f, 0f, 0f));

            var merged = Track(BoneRig.Merge(new List<Mesh> { rig.SkinnedMesh(kit, "Body"), rig.SkinnedMesh(kit, "Body") }));

            Assert.AreEqual(6, merged.vertexCount);
            Assert.AreEqual(1, merged.subMeshCount);
            Assert.AreEqual(6, merged.triangles.Length);
            Assert.AreEqual(0f, Vector3.Distance(HandAt, merged.vertices[3]), 1e-4f);
            var weights = new List<Vector4>();
            merged.GetUVs(BoneAnimatedAuthoring.WeightChannel, weights);
            Assert.AreEqual(6, weights.Count);
            Assert.AreEqual(-1f, weights[5].x, "the hand is upper body, so its weight is negative");
        }

        [Test]
        public void SkinnedPartAtAnotherRestPose_Fails()
        {
            using var rig = Rig();
            var bent = Model("Bent", Quaternion.identity, handNudge: new Vector3(0f, 0.2f, 0f));

            Assert.Throws<InvalidOperationException>(() => rig.SkinnedMesh(bent, "Body"));
        }

        [Test]
        public void Weapon_SitsWithItsOriginOnTheSocketNode()
        {
            using var rig = Rig();
            var weapon = Track(new GameObject("Weapon"));
            var mesh = Track(new Mesh
            {
                vertices = new[] { Vector3.zero, new Vector3(0f, 0f, 1f), new Vector3(0f, 0.1f, 0f) },
                normals = new[] { Vector3.up, Vector3.up, Vector3.up },
                triangles = new[] { 0, 1, 2 },
            });
            weapon.AddComponent<MeshFilter>().sharedMesh = mesh;

            var placed = Track(rig.AttachedMesh(weapon, "Weapon", "Grip"));

            var grip = HandAt + new Vector3(0.1f, 0f, 0f);
            Assert.AreEqual(0f, Vector3.Distance(grip, placed.vertices[0]), 1e-4f, "the origin on the grip");
            Assert.AreEqual(0f, Vector3.Distance(grip + Vector3.right, placed.vertices[1]), 1e-4f, "turned with the grip");
            var indices = new List<Vector4>();
            placed.GetUVs(BoneAnimatedAuthoring.IndexChannel, indices);
            Assert.AreEqual(1f, indices[0].x, "carried by the hand above the grip");
        }
    }
}
