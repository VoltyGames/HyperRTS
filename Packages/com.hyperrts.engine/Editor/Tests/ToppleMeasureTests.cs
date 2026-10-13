using System;
using System.Collections.Generic;
using HyperRTS.Editor.Authoring;
using HyperRTS.Simulation.Combat;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Topple's trunk and lie measured from the standing and remains models.</summary>
    public class ToppleMeasureTests
    {
        private readonly List<Object> _made = new();
        private Material _bark;
        private Material _leaves;

        [SetUp]
        public void SetUp()
        {
            _bark = Track(new Material(Shader.Find("Hidden/InternalErrorShader")) { name = "Bark" });
            _leaves = Track(new Material(Shader.Find("Hidden/InternalErrorShader")) { name = "Leaves" });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var made in _made)
            {
                Object.DestroyImmediate(made);
            }

            _made.Clear();
        }

        [Test]
        public void MeasuresTrunkTopFromTrunkFacesAndLieTowardTheRemainsMass()
        {
            var topple = Tree(Model(Vector3.right * 4f));

            ToppleMeasure.Measure(topple, _bark);

            Assert.That(topple.trunk.y, Is.EqualTo(10f).Within(1e-4f), "leaf faces above the trunk are ignored");
            Assert.That(topple.lie, Is.EqualTo(90f).Within(1e-3f));
        }

        [Test]
        public void RemainsWithoutMeshesThrow()
        {
            var topple = Tree(Track(new GameObject("Remains")));

            Assert.Throws<InvalidOperationException>(() => ToppleMeasure.Measure(topple, _bark));
        }

        [Test]
        public void RemainsAroundTheOriginThrow()
        {
            var topple = Tree(Model(Vector3.zero));

            Assert.Throws<InvalidOperationException>(() => ToppleMeasure.Measure(topple, _bark));
        }

        private ToppleAuthoring Tree(GameObject remains)
        {
            var root = Track(new GameObject("Tree"));
            root.AddComponent<DestructibleAuthoring>().remains = remains;
            var model = Model(Vector3.zero);
            model.transform.SetParent(root.transform, false);
            var group = model.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(0.1f, new Renderer[] { model.GetComponent<MeshRenderer>() }) });
            return root.AddComponent<ToppleAuthoring>();
        }

        // A trunk from the origin to 10 m with a leaf card above it, shifted by offset.
        private GameObject Model(Vector3 offset)
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
            model.AddComponent<MeshRenderer>().sharedMaterials = new[] { _bark, _leaves };
            return model;
        }

        private T Track<T>(T made) where T : Object
        {
            _made.Add(made);
            return made;
        }
    }
}
