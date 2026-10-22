using HyperRTS.Presentation.Destruction;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using NUnit.Framework;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Baking marks meshes and roots for hiding, and turns zero-scale broken pieces into hidden full-size ones.</summary>
    public class HiddenByBakingTests
    {
        private World _world;
        private EntityManager _em;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Hidden By Baking Tests");
            _em = _world.EntityManager;
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void MeshesAndRoots_GetHiddenBy_OtherEntitiesDont()
        {
            var mesh = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(Prefab));
            var root = _em.CreateEntity(typeof(EntityInfo), typeof(Prefab));
            var other = _em.CreateEntity(typeof(Prefab));

            _world.CreateSystem<HiddenByBakingSystem>().Update(_world.Unmanaged);

            Assert.IsTrue(_em.HasComponent<HiddenBy>(mesh));
            Assert.IsTrue(_em.HasComponent<HiddenBy>(root));
            Assert.IsFalse(_em.HasComponent<HiddenBy>(other));
        }

        [Test]
        public void BrokenPiece_BakesFullSizeAndHidden_WithItsChildMeshes()
        {
            var intact = Piece(1f);
            var broken = Piece(0f);
            var shard = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy), typeof(Prefab));
            _em.AddComponentData(shard, new Parent { Value = broken });
            var building = _em.CreateEntity(typeof(Prefab));
            _em.AddBuffer<BreakablePart>(building).Add(new BreakablePart { Intact = intact, Broken = broken });

            _world.CreateSystem<BreakablePartsBakingSystem>().Update(_world.Unmanaged);

            Assert.AreEqual(1f, _em.GetComponentData<LocalTransform>(broken).Scale);
            Assert.IsFalse(_em.IsComponentEnabled<MaterialMeshInfo>(broken));
            Assert.IsFalse(_em.IsComponentEnabled<MaterialMeshInfo>(shard));
            Assert.IsTrue(_em.GetComponentData<HiddenBy>(shard).Has(HideReason.Part));
            Assert.IsTrue(_em.IsComponentEnabled<MaterialMeshInfo>(intact));
        }

        private Entity Piece(float scale)
        {
            var piece = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy), typeof(Prefab));
            _em.AddComponentData(piece, LocalTransform.FromScale(scale));
            return piece;
        }
    }
}
