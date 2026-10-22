using HyperRTS.Presentation.Construction;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The build reveal: a building's progress reaches every mesh under it until it completes.</summary>
    public class BuildRevealSystemTests
    {
        private PresentationWorldKit _kit;
        private EntityManager _em;
        private SystemHandle _system;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Build Reveal Tests");
            _em = _kit.EntityManager;
            _system = _kit.World.CreateSystem<BuildRevealSystem>();
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [Test]
        public void UnderConstruction_EveryMeshGetsTheProgress_AndOtherPartsNone()
        {
            var building = Building(0.3f, true, out var meshes, out var bare);

            Update();

            foreach (var mesh in meshes)
            {
                Assert.AreEqual(0.3f, _em.GetComponentData<BuildProgress>(mesh).Value);
            }

            Assert.IsFalse(_em.HasComponent<BuildProgress>(bare));
            Assert.IsTrue(_em.HasComponent<BuildRevealing>(building));
        }

        [Test]
        public void RisingProgress_IsWrittenEachFrame()
        {
            var building = Building(0.3f, true, out var meshes, out _);
            Update();

            _em.SetComponentData(building, new ConstructionProgress { Value = 0.6f });
            Update();

            Assert.AreEqual(0.6f, _em.GetComponentData<BuildProgress>(meshes[1]).Value);
        }

        [Test]
        public void Completion_TakesTheProgressOff_SoTheModelDrawsAsAuthored()
        {
            var building = Building(0.9f, true, out var meshes, out _);
            Update();

            _em.SetComponentEnabled<ConstructionProgress>(building, false);
            Update();

            Assert.IsFalse(_em.HasComponent<BuildProgress>(meshes[0]));
            Assert.IsFalse(_em.HasComponent<BuildProgress>(meshes[1]));
            Assert.IsFalse(_em.HasComponent<BuildRevealing>(building));
        }

        [Test]
        public void FinishedBuilding_NeverGetsTheProgress()
        {
            var building = Building(1f, false, out var meshes, out _);

            Update();

            Assert.IsFalse(_em.HasComponent<BuildProgress>(meshes[0]));
            Assert.IsFalse(_em.HasComponent<BuildRevealing>(building));
        }

        private void Update() => _system.Update(_kit.World.Unmanaged);

        /// <summary>A building with two meshes and a mesh-less part, linked as an instantiated prefab is.</summary>
        private Entity Building(float progress, bool building, out Entity[] meshes, out Entity bare)
        {
            var root = _em.CreateEntity();
            _em.AddComponentData(root, new ConstructionProgress { Value = progress });
            _em.SetComponentEnabled<ConstructionProgress>(root, building);
            meshes = new[] { _em.CreateEntity(typeof(MaterialMeshInfo)), _em.CreateEntity(typeof(MaterialMeshInfo)) };
            bare = _em.CreateEntity();
            var group = new NativeList<LinkedEntityGroup>(Allocator.Temp) { root, meshes[0], meshes[1], bare };
            _em.AddBuffer<LinkedEntityGroup>(root).AddRange(group.AsArray());
            return root;
        }
    }
}
