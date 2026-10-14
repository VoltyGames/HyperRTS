using HyperRTS.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Stretch parts: the caps move out keeping their size, and the span scales along the axis to meet them.</summary>
    public class StretchLayoutTests
    {
        private const float Length = 4.6f;
        private const float CapHalf = 0.3f;

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

        [TestCase(1f)]
        [TestCase(1.5f)]
        [TestCase(3f)]
        public void CapsMoveOut_SoTheArchSpansItsStretchedLength(float stretch)
        {
            var layout = Arch();
            var extension = StretchLayout.Extension(Length, stretch);

            Assert.AreEqual(-Length * stretch * 0.5f, layout.StartCapAt(extension).z - CapHalf, 1e-4f);
            Assert.AreEqual(Length * stretch * 0.5f, layout.EndCapAt(extension).z + CapHalf, 1e-4f);
        }

        [TestCase(1f, 1f)]
        [TestCase(1.5f, 5.7f / 3.4f)]
        [TestCase(3f, 12.6f / 3.4f)]
        public void TheSpanScalesAlongTheAxisOnly_ToMeetBothCaps(float stretch, float factor)
        {
            var layout = Arch();
            var extension = StretchLayout.Extension(Length, stretch);

            var scale = layout.SpanScaleAt(extension);

            Assert.AreEqual(factor, scale.z, 1e-4f);
            Assert.AreEqual(1f, scale.x, 1e-6f);
            Assert.AreEqual(1f, scale.y, 1e-6f);
            Assert.AreEqual(0f, layout.SpanPositionAt(extension).z, 1e-5f);
        }

        [Test]
        public void ASpanPivotedAtItsStart_ShiftsSoBothEndsMeetTheCaps()
        {
            var layout = Arch(-1.7f);

            var pivot = layout.SpanPositionAt(1f).z;
            var length = 3.4f * layout.SpanFactor(1f);

            Assert.AreEqual(-2.7f, pivot, 1e-4f);
            Assert.AreEqual(2.7f, pivot + length, 1e-4f);
        }

        [Test]
        public void PartsUnderAScaledModel_MoveByTheExtensionInRootMetres()
        {
            var layout = Arch();
            layout.RootUnits = 2f;

            Assert.AreEqual(-3f, layout.StartCapAt(2f).z, 1e-5f);
            Assert.AreEqual(5.4f / 3.4f, layout.SpanFactor(2f), 1e-4f);
        }

        [Test]
        public void Measure_ReadsATurnedSpanAlongItsOwnAxis()
        {
            var root = new GameObject("Arch").transform;
            try
            {
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
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void Measure_FindsTheAxisFromCapMeshes_WhenEveryPivotIsAtTheOrigin()
        {
            var root = new GameObject("Arch").transform;
            var meshes = new[] { CapMesh(-2f), CapMesh(2f) };
            try
            {
                var start = Child(root, Vector3.zero, Quaternion.identity);
                var end = Child(root, Vector3.zero, Quaternion.identity);
                start.gameObject.AddComponent<MeshFilter>().sharedMesh = meshes[0];
                end.gameObject.AddComponent<MeshFilter>().sharedMesh = meshes[1];
                var span = Child(root, Vector3.zero, Quaternion.identity);
                var bounds = new Bounds(Vector3.zero, new Vector3(0.1f, 0.1f, 3.4f));

                var layout = StretchLayout.Measure(root, start, end, span, bounds);

                Assert.AreEqual(new float3(0f, 0f, 1f), layout.Axis);
                Assert.AreEqual(1f, layout.EndCapAt(1f).z, 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
                System.Array.ForEach(meshes, Object.DestroyImmediate);
            }
        }

        private static Mesh CapMesh(float z) => new()
        {
            vertices = new[] { new Vector3(0f, 0f, z - 0.3f), new Vector3(0f, 1f, z), new Vector3(0f, 0f, z + 0.3f) },
            triangles = new[] { 0, 1, 2 },
        };

        private static Transform Child(Transform root, Vector3 position, Quaternion rotation)
        {
            var child = new GameObject("Part").transform;
            child.SetParent(root, false);
            child.SetLocalPositionAndRotation(position, rotation);
            return child;
        }
    }
}
