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
        private FogOfWar _grid;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Presentation Tests");
            _em = _world.EntityManager;
            _commands = _world.GetOrCreateSystemManaged<BeginPresentationEntityCommandBufferSystem>();
            _teamColor = _world.CreateSystem<TeamColorSystem>();
            _fog = _world.CreateSystem<FogVisibilitySystem>();

            var relations = new FactionRelations();
            relations.Teams.Add(0);
            relations.Teams.Add(1);
            relations.Teams.Add(2);
            var match = _em.CreateEntity();
            _em.AddComponentData(match, relations);
            _em.AddComponentData(match, new MapSettings { Min = 0f, Size = 8f, FogCellSize = 1f, FogOfWar = true });

            var local = _em.CreateEntity(typeof(LocalPlayer));
            _em.AddComponentData(local, new Player { Faction = 1, Team = 1, Color = Blue });
            _em.AddComponentData(_em.CreateEntity(), new Player { Faction = 2, Team = 2, Color = new float4(1f, 0f, 0f, 1f) });
        }

        [TearDown]
        public void TearDown()
        {
            if (_grid.IsCreated)
            {
                _grid.Visible.Dispose();
                _grid.Explored.Dispose();
            }

            _world.Dispose();
        }

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
        public void Fog_HidesUnseenEnemies_UntilTheyAreVisible()
        {
            CreateGrid();
            var enemy = Spawn(2, new float3(5.5f, 0f, 5.5f));
            var own = Spawn(1, new float3(5.5f, 0f, 5.5f));

            Run(_fog);
            Assert.IsTrue(_em.HasComponent<DisableRendering>(enemy));
            Assert.IsTrue(_em.HasComponent<FogHidden>(enemy));
            Assert.IsFalse(_em.HasComponent<DisableRendering>(own));

            _grid.Visible[_grid.Index(new int2(5, 5))] = 1 << 1;
            Run(_fog);
            Assert.IsFalse(_em.HasComponent<DisableRendering>(enemy));
            Assert.IsFalse(_em.HasComponent<FogHidden>(enemy));
        }

        [Test]
        public void Fog_RevealsEverythingWhenDisabled()
        {
            CreateGrid();
            var enemy = Spawn(2, new float3(5.5f, 0f, 5.5f));
            Run(_fog);

            var map = _em.CreateEntityQuery(typeof(MapSettings)).GetSingletonEntity();
            _em.SetComponentData(map, new MapSettings { Size = 8f, FogOfWar = false });
            Run(_fog);

            Assert.IsFalse(_em.HasComponent<DisableRendering>(enemy));
        }

        private Entity Spawn(byte faction, float3 position)
        {
            var entity = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(EntityInfo));
            _em.AddComponentData(entity, new Faction { Value = faction });
            _em.AddComponentData(entity, new LocalToWorld { Value = float4x4.Translate(position) });
            return entity;
        }

        private void CreateGrid()
        {
            _grid = new FogOfWar
            {
                Visible = new NativeArray<byte>(64, Allocator.Persistent),
                Explored = new NativeArray<byte>(64, Allocator.Persistent),
                Size = new int2(8, 8),
                CellSize = 1f,
            };
            _em.AddComponentData(_em.CreateEntity(), _grid);
        }

        private void Run(SystemHandle system)
        {
            system.Update(_world.Unmanaged);
            _commands.Update();
            _em.CompleteAllTrackedJobs();
        }
    }
}
