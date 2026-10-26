using System;
using System.Linq;
using HyperRTS.Editor.Authoring;
using HyperRTS.Editor.Templates;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Upgrades;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Tools
{
    /// <summary>Inspector helpers, scene handles, and the topple and stretch parts measured from models.</summary>
    public class AuthoringToolTests : TemplateFixture
    {
        private const float ArchLength = 4.6f;
        private const float CapHalf = 0.3f;

        [TestCase(0, "")]
        [TestCase(1, "1 error")]
        [TestCase(3, "3 errors")]
        public void BadgeCountsArePlural(int count, string expected) =>
            Assert.AreEqual(expected, IssueBadge.CountText(count, "error"));

        [Test]
        public void ShowIfFollowsNumbersAndObjects_AndRejectsOtherConditions()
        {
            var weapon = Track(EntityTemplates.CombatUnit()).GetComponent<WeaponAuthoring>();
            var serialized = new SerializedObject(weapon);
            bool IsMet(string field) => ConditionalFields.IsMet(serialized.FindProperty(Stat(field)));
            Assert.IsFalse(IsMet(nameof(WeaponStats.splashRadius)));
            Assert.IsFalse(IsMet(nameof(WeaponStats.projectilePrefab)));

            weapon.stats.splashRadius = 2f;
            weapon.stats.projectilePrefab = weapon.gameObject;
            serialized.Update();
            Assert.IsTrue(IsMet(nameof(WeaponStats.splashRadius)));
            Assert.IsTrue(IsMet(nameof(WeaponStats.projectilePrefab)));
            Assert.Throws<ArgumentException>(() => IsMet(nameof(WeaponStats.targets)));
        }

        [Test]
        public void ShowIfReadsSiblingsInsideNestedStats() =>
            CollectionAssert.Contains(ConditionalFields.Conditions(typeof(WeaponAuthoring)).ToList(),
                (Stat(nameof(WeaponStats.splashEdgeDamage)), Stat(nameof(WeaponStats.splashRadius))));

        [Test]
        public void SummariesListRoleModulesDamageCostAndTime()
        {
            var soldier = Track(EntityTemplates.CombatUnit()).GetComponent<GameEntityAuthoring>();
            var upgrade = Track(EntityTemplates.Upgrade()).GetComponent<UpgradeAuthoring>();
            upgrade.buildTime = 30f;

            Assert.AreEqual("Unit · Weapon · 10 DPS · free", EntitySummary.Line(soldier));
            Assert.AreEqual("free · 30s", EntitySummary.ProducibleText(upgrade));
            Assert.AreEqual("", EntitySummary.ProducibleText(null));
        }

        // The opposite face stays put, so moving the max (+1) or min (-1) face by d shifts the centre by d / 2.
        [TestCase(1f, 1f, 6f, TestName = "MaxFaceOutwardGrows")]
        [TestCase(1f, -1f, 2f, TestName = "MaxFaceInwardShrinks")]
        [TestCase(-1f, 1f, 6f, TestName = "MinFaceOutwardGrows")]
        [TestCase(-1f, -1f, 2f, TestName = "MinFaceInwardShrinks")]
        [TestCase(1f, 0f, 4f, TestName = "UntouchedBoxKeepsItsSize")]
        public void DraggedGroundFaceMirrors(float side, float drag, float expected) =>
            Assert.AreEqual(expected, GroundHandles.MirroredSize(4f, side * drag * 0.5f, 4f + drag), 1e-4f);

        [Test]
        public void GroundHandleDirectionLiesFlatAtTheGivenAngle() =>
            Assert.That(Vector3.Distance(Vector3.forward, GroundHandles.Direction(90f)), Is.LessThan(1e-4f));

        [Test]
        public void ToppleMeasuresTrunkTopFromTrunkFacesAndLieTowardTheRemainsMass()
        {
            var (topple, bark) = Tree(Vector3.right * 4f);

            ToppleMeasure.Measure(topple, bark);

            Assert.That(topple.trunk.y, Is.EqualTo(10f).Within(1e-4f), "leaf faces above the trunk are ignored");
            Assert.That(topple.lie, Is.EqualTo(90f).Within(1e-3f));
        }

        [TestCase(true, TestName = "ToppleRemainsWithoutMeshesThrow")]
        [TestCase(false, TestName = "ToppleRemainsAroundTheOriginThrow")]
        public void ToppleRejectsRemainsItCannotMeasure(bool meshless)
        {
            var (topple, bark) = Tree(Vector3.zero);
            if (meshless)
            {
                topple.GetComponent<DestructibleAuthoring>().remains = Track(new GameObject("Remains"));
            }

            Assert.Throws<InvalidOperationException>(() => ToppleMeasure.Measure(topple, bark));
        }

        [TestCase(1f, 1f)]
        [TestCase(1.5f, 5.7f / 3.4f)]
        [TestCase(3f, 12.6f / 3.4f)]
        public void StretchCapsMoveOut_AndTheSpanScalesAlongTheAxisToMeetThem(float stretch, float factor)
        {
            var layout = Arch();
            var extension = StretchLayout.Extension(ArchLength, stretch);

            var scale = layout.SpanScaleAt(extension);

            Assert.AreEqual(-ArchLength * stretch * 0.5f, layout.StartCapAt(extension).z - CapHalf, 1e-4f);
            Assert.AreEqual(ArchLength * stretch * 0.5f, layout.EndCapAt(extension).z + CapHalf, 1e-4f);
            Assert.AreEqual(factor, scale.z, 1e-4f);
            Assert.AreEqual(1f, scale.x, 1e-6f);
            Assert.AreEqual(1f, scale.y, 1e-6f);
            Assert.AreEqual(0f, layout.SpanPositionAt(extension).z, 1e-5f);
        }

        [Test]
        public void StretchSpanPivotedAtItsStart_ShiftsSoBothEndsMeetTheCaps()
        {
            var layout = Arch(-1.7f);

            var pivot = layout.SpanPositionAt(1f).z;
            var length = 3.4f * layout.SpanFactor(1f);

            Assert.AreEqual(-2.7f, pivot, 1e-4f);
            Assert.AreEqual(2.7f, pivot + length, 1e-4f);
        }

        [Test]
        public void StretchPartsUnderAScaledModel_MoveByTheExtensionInRootMetres()
        {
            var layout = Arch();
            layout.RootUnits = 2f;

            Assert.AreEqual(-3f, layout.StartCapAt(2f).z, 1e-5f);
            Assert.AreEqual(5.4f / 3.4f, layout.SpanFactor(2f), 1e-4f);
        }

        [Test]
        public void StretchMeasure_ReadsATurnedSpanAlongItsOwnAxis()
        {
            var root = Track(new GameObject("Arch")).transform;
            var start = Child(root, new Vector3(0f, 0f, -2f), Quaternion.identity);
            var end = Child(root, new Vector3(0f, 0f, 2f), Quaternion.identity);
            var span = Child(root, Vector3.zero, Quaternion.Euler(-90f, 0f, 0f));
            var bounds = new Bounds(new Vector3(0f, 0.1f, 0f), new Vector3(0.1f, 3.4f, 0.1f));

            var layout = StretchLayout.Measure(root, start, end, span, bounds);

            Assert.AreEqual(new float3(0f, 0f, 1f), layout.Axis);
            Assert.AreEqual(new float3(0f, 1f, 0f), layout.SpanAxis);
            Assert.AreEqual(-1.8f, layout.SpanMin, 1e-4f);
            Assert.AreEqual(1.6f, layout.SpanMax, 1e-4f);
        }

        [Test]
        public void StretchMeasure_FindsTheAxisFromCapMeshes_WhenEveryPivotIsAtTheOrigin()
        {
            var root = Track(new GameObject("Arch")).transform;
            var start = Child(root, Vector3.zero, Quaternion.identity);
            var end = Child(root, Vector3.zero, Quaternion.identity);
            start.gameObject.AddComponent<MeshFilter>().sharedMesh = CapMesh(-2f);
            end.gameObject.AddComponent<MeshFilter>().sharedMesh = CapMesh(2f);
            var span = Child(root, Vector3.zero, Quaternion.identity);
            var bounds = new Bounds(Vector3.zero, new Vector3(0.1f, 0.1f, 3.4f));

            var layout = StretchLayout.Measure(root, start, end, span, bounds);

            Assert.AreEqual(new float3(0f, 0f, 1f), layout.Axis);
            Assert.AreEqual(1f, layout.EndCapAt(1f).z, 1e-5f);
        }

        private static string Stat(string field) => $"{nameof(WeaponAuthoring.stats)}.{field}";

        /// <summary>A tree whose remains model is the standing one shifted by <paramref name="remainsOffset"/>.</summary>
        private (ToppleAuthoring Topple, Material Bark) Tree(Vector3 remainsOffset)
        {
            var bark = Track(new Material(Shader.Find("Hidden/InternalErrorShader")) { name = "Bark" });
            var leaves = Track(new Material(Shader.Find("Hidden/InternalErrorShader")) { name = "Leaves" });
            var root = Track(new GameObject("Tree"));
            root.AddComponent<DestructibleAuthoring>().remains = TreeModel(remainsOffset, bark, leaves);
            var model = TreeModel(Vector3.zero, bark, leaves);
            model.transform.SetParent(root.transform, false);
            var group = model.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(0.1f, new Renderer[] { model.GetComponent<MeshRenderer>() }) });
            return (root.AddComponent<ToppleAuthoring>(), bark);
        }

        // A trunk from the origin to 10 m with a leaf card above it, shifted by offset.
        private GameObject TreeModel(Vector3 offset, Material bark, Material leaves)
        {
            var mesh = Track(new Mesh());
            mesh.vertices = new[]
            {
                offset, offset + new Vector3(0.1f, 0f, 0f), offset + new Vector3(0f, 10f, 0f),
                offset + new Vector3(-1f, 12f, 0f), offset + new Vector3(1f, 12f, 0f), offset + new Vector3(0f, 14f, 0f),
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 3, 4, 5 }, 1);
            var model = Track(new GameObject("Model"));
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.AddComponent<MeshRenderer>().sharedMaterials = new[] { bark, leaves };
            return model;
        }

        /// <summary>A 4.6 m arch along Z: 0.6 m caps at either end and a 3.4 m span between, pivots at their centres.</summary>
        private static StretchLayout Arch(float spanPivot = 0f) => new()
        {
            Axis = new float3(0f, 0f, 1f),
            StartCap = new float3(0f, 0f, -2f),
            EndCap = new float3(0f, 0f, 2f),
            SpanPosition = new float3(0f, 0f, spanPivot),
            SpanScale = new float3(1f),
            SpanAxis = new float3(0f, 0f, 1f),
            SpanMin = -1.7f,
            SpanMax = 1.7f,
            RootUnits = 1f,
        };

        private Mesh CapMesh(float z) => Track(new Mesh
        {
            vertices = new[] { new Vector3(0f, 0f, z - 0.3f), new Vector3(0f, 1f, z), new Vector3(0f, 0f, z + 0.3f) },
            triangles = new[] { 0, 1, 2 },
        });

        private static Transform Child(Transform root, Vector3 position, Quaternion rotation)
        {
            var child = new GameObject("Part").transform;
            child.SetParent(root, false);
            child.SetLocalPositionAndRotation(position, rotation);
            return child;
        }
    }
}
