using HyperRTS.Presentation.Fog;
using HyperRTS.Presentation.TeamColors;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests
{
    public class PresentationSystemTests
    {
        private static readonly float4 Blue = new(0f, 0f, 1f, 1f);

        private World _world;
        private EntityManager _em;
        private SystemHandle _teamColor;
        private SystemHandle _fog;
        private EntityCommandBufferSystem _commands;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Presentation Tests");
            _em = _world.EntityManager;
            _commands = _world.GetOrCreateSystemManaged<BeginPresentationEntityCommandBufferSystem>();
            _teamColor = _world.CreateSystem<TeamColorSystem>();
            _fog = _world.CreateSystem<FogVisibilitySystem>();

            var local = _em.CreateEntity(typeof(LocalPlayer));
            _em.AddComponentData(local, new Player { Faction = 1, Color = Blue });
            _em.AddComponentData(_em.CreateEntity(), new Player { Faction = 2, Color = new float4(1f, 0f, 0f, 1f) });
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void TeamColor_TintsOwnedMeshesAndChildren_LeavesNeutralAlone()
        {
            var unit = Spawn(1, float3.zero);
            var child = _em.CreateEntity(typeof(MaterialMeshInfo));
            _em.AddBuffer<LinkedEntityGroup>(unit).AddRange(new NativeArray<LinkedEntityGroup>(
                new LinkedEntityGroup[] { unit, child }, Allocator.Temp));
            var neutral = Spawn(Faction.Neutral, float3.zero);

            Run(_teamColor);

            Assert.AreEqual(Blue, _em.GetComponentData<URPMaterialPropertyBaseColor>(unit).Value);
            Assert.AreEqual(Blue, _em.GetComponentData<URPMaterialPropertyBaseColor>(child).Value);
            Assert.IsFalse(_em.HasComponent<URPMaterialPropertyBaseColor>(neutral));
        }

        [Test]
        public void TeamColor_FollowsOwnerChange()
        {
            var unit = Spawn(2, float3.zero);
            Run(_teamColor);

            _em.SetComponentData(unit, new Faction { Value = 1 });
            Run(_teamColor);

            Assert.AreEqual(Blue, _em.GetComponentData<URPMaterialPropertyBaseColor>(unit).Value);
        }

        [Test]
        public void Fog_StopsRenderingHiddenRoots_UntilTheyAreRevealed()
        {
            var enemy = Spawn(2, float3.zero);
            var child = _em.CreateEntity(typeof(MaterialMeshInfo));
            _em.AddBuffer<LinkedEntityGroup>(enemy).AddRange(new NativeArray<LinkedEntityGroup>(
                new LinkedEntityGroup[] { enemy, child }, Allocator.Temp));
            var own = Spawn(1, float3.zero);
            _em.AddComponent<FogHidden>(enemy);

            Run(_fog);
            Assert.IsTrue(_em.HasComponent<DisableRendering>(enemy));
            Assert.IsTrue(_em.HasComponent<DisableRendering>(child));
            Assert.IsFalse(_em.HasComponent<DisableRendering>(own));

            _em.RemoveComponent<FogHidden>(enemy);
            Run(_fog);
            Assert.IsFalse(_em.HasComponent<DisableRendering>(enemy));
            Assert.IsFalse(_em.HasComponent<DisableRendering>(child));
        }

        private Entity Spawn(byte faction, float3 position)
        {
            var entity = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(EntityInfo));
            _em.AddComponentData(entity, new Faction { Value = faction });
            _em.AddComponentData(entity, new LocalToWorld { Value = float4x4.Translate(position) });
            return entity;
        }

        private void Run(SystemHandle system)
        {
            system.Update(_world.Unmanaged);
            _commands.Update();
            _em.CompleteAllTrackedJobs();
        }
    }
}
