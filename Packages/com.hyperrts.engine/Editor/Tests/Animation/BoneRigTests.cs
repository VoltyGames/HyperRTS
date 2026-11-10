using HyperRTS.Presentation.Animation.Bones;
using System;
using System.Collections.Generic;
using HyperRTS.Editor.Animation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Tests.Animation
{
    /// <summary>
    /// Parts from other models on the same skeleton: a differently turned skinned part is moved into the skeleton's
    /// space, one at another rest pose fails, a weapon follows its socket's bone and swings to the other hand, and the
    /// upper body is written on the root bone, and the ragdoll bake writes the rest pose and refuses a missing bone.
    /// </summary>
    public partial class BoneRigTests
    {
        private static readonly Vector3 HandAt = new(0.5f, 1.4f, 0f);
        private static readonly Vector3 OffHandAt = new(0.6f, 1.6f, 0.4f);
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

        // Root > Armature > Hips > Hand (with a Grip empty holding a Support empty) and OffHand, and a skinned node
        // holding one triangle at the hand.
        private GameObject Model(string name, Quaternion meshTurn, Vector3 handNudge = default)
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

        private readonly BoneAnimationSet.Socket _grip = new()
        {
            name = "Rifle", node = "Grip", support = "Support", supportHand = "OffHand",
        };

        private BoneRig Rig() => new(Model("Rig", Quaternion.identity), "Body", "Hand", "Hips", new[] { _grip });

        // Bone texture order: Hips, Hand, OffHand, then the Grip socket.
        private const int GripBone = 3;

        private AnimationClip Still()
        {
            var clip = Track(new AnimationClip());
            clip.SetCurve("Hips", typeof(Transform), "localPosition.y", AnimationCurve.Constant(0f, 1f, 1f));
            return clip;
        }

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

            var merged = Track(BoneMesh.Merge(new List<Mesh> { rig.SkinnedMesh(kit, "Body"), rig.SkinnedMesh(kit, "Body") }));

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
        public void ClipWithRootTravel_ReportsItsGroundSpeed()
        {
            using var rig = Rig();
            var clip = Track(new AnimationClip());
            clip.SetCurve("Hips", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 2f, 3f));
            var still = Track(new AnimationClip());
            still.SetCurve("Hips", typeof(Transform), "localPosition.y", AnimationCurve.Linear(0f, 1f, 2f, 1.5f));

            Assert.AreEqual(1.5f, rig.GroundSpeed(clip, 1f), 1e-3f);
            Assert.AreEqual(3f, rig.GroundSpeed(clip, 2f), 1e-3f, "played twice as fast covers twice the ground");
            Assert.AreEqual(0f, rig.GroundSpeed(still, 1f), 1e-3f, "height is not ground");
        }

        [Test]
        public void InPlaceClip_FirstFrameShowsTheClipStart()
        {
            using var rig = Rig();
            var clip = Track(new AnimationClip());
            clip.SetCurve("Hips", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 2f, 3f));
            clip.SetCurve("Hips", typeof(Transform), "localPosition.y", AnimationCurve.Linear(0f, 1f, 2f, 1.5f));

            var first = rig.Pose(clip, 0f, true, false)[1].MultiplyPoint3x4(HandAt);
            var middle = rig.Pose(clip, 1f, true, false)[1].MultiplyPoint3x4(HandAt);

            Assert.AreEqual(0f, Vector3.Distance(HandAt, first), 1e-4f, "the start pose, not the end pose a stride ahead");
            Assert.AreEqual(0f, Vector3.Distance(HandAt + new Vector3(0f, 0.25f, 0f), middle), 1e-4f, "ground travel removed, height kept");
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
            Assert.AreEqual(GripBone, indices[0].x, "carried by the grip's own bone, after the skeleton's");
            var held = rig.Pose(Still(), 0f, false, false)[GripBone].MultiplyPoint3x4(placed.vertices[1]);
            Assert.AreEqual(0f, Vector3.Distance(grip + Vector3.right, held), 1e-4f, "one hand: the grip's own pose");
        }

        [Test]
        public void BothHands_SwingTheWeaponAboutItsGripToTheOtherHand()
        {
            using var rig = Rig();
            var grip = HandAt + new Vector3(0.1f, 0f, 0f);
            var support = grip + Vector3.right * 0.3f;

            var swung = rig.Pose(Still(), 0f, false, true)[GripBone];

            Assert.AreEqual(0f, Vector3.Distance(grip, swung.MultiplyPoint3x4(grip)), 1e-4f, "the grip stays in the hand");
            var toSupport = (swung.MultiplyPoint3x4(support) - grip).normalized;
            Assert.AreEqual(1f, Vector3.Dot(toSupport, (OffHandAt - grip).normalized), 1e-4f, "the support points at the other hand");
        }

        [Test]
        public void UpperBones_AreWrittenOnTheRootBone()
        {
            using var rig = Rig();
            var clip = Track(new AnimationClip());
            clip.SetCurve("Hips", typeof(Transform), "localPosition.x", AnimationCurve.Constant(0f, 1f, 2f));
            clip.SetCurve("Hips", typeof(Transform), "localPosition.y", AnimationCurve.Constant(0f, 1f, 1f));
            var plan = new ClipFrames(new BoneAnimationSet.Clip { name = "Shift", source = clip, loop = true }, 1f);

            var texture = Track(BoneFrames.Sample(rig, new List<ClipFrames> { plan }).Texture(rig.BoneNames().Count));

            Assert.AreEqual(2f, texture.GetPixel(0 * 3, 0).a, 1e-3f, "the hips keep their own place");
            Assert.AreEqual(0f, texture.GetPixel(1 * 3, 0).a, 1e-3f, "the hand rides on the hips, so it carries no shift of its own");
            Assert.AreEqual(2f, texture.GetPixel(2 * 3, 0).a, 1e-3f, "the off hand is lower body here, so it keeps the shift");
        }
    }
}
