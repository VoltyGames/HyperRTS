using HyperRTS.Presentation.Flags;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Capture;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests
{
    public class FlagSystemTests
    {
        private const byte LocalSide = 3;
        private const byte EnemySide = 5;
        private static readonly float4 Blue = new(0f, 0f, 1f, 1f);
        private static readonly float4 Red = new(1f, 0f, 0f, 1f);

        private PresentationWorldKit _kit;
        private World _world;
        private EntityManager _em;
        private SystemHandle _flags;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Flag Tests");
            _world = _kit.World;
            _em = _kit.EntityManager;
            _flags = _world.CreateSystem<FlagSystem>();
            _kit.AddPlayer(1, Blue, local: true, side: LocalSide);
            _kit.AddPlayer(2, Red, side: EnemySide);
        }

        [TearDown]
        public void TearDown()
        {
            using (var grids = _em.CreateEntityQuery(typeof(FogOfWar)))
            {
                if (grids.TryGetSingleton(out FogOfWar fog))
                {
                    fog.Visible.Dispose();
                }
            }

            _kit.Dispose();
        }

        [Test]
        public void OwnedBuilding_FliesItsOwnersSideAtTheTop_WithTheOwnersBand()
        {
            var flag = Building(1, out _);

            Run();

            Assert.AreEqual(new float4(LocalSide, 1f, 1f, 3f), _em.GetComponentData<FlagState>(flag).Value);
            Assert.AreEqual(Blue, _em.GetComponentData<FlagColor>(flag).Value);
        }

        [Test]
        public void CaptureInSecondHalf_RaisesTheCapturersFlag()
        {
            var flag = Building(1, out var building);
            _em.SetComponentData(building, new CaptureProgress { Faction = 2, Value = 0.75f });

            Run();

            Assert.AreEqual(new float4(EnemySide, 0.5f, 1f, 3f), _em.GetComponentData<FlagState>(flag).Value);
            Assert.AreEqual(Red, _em.GetComponentData<FlagColor>(flag).Value);
        }

        [Test]
        public void HostileBuildingUnderFog_HidesItsFlag_OwnStaysShown()
        {
            AddFog();
            var enemy = Building(2, out _);
            var own = Building(1, out _);

            Run();

            Assert.AreEqual(0f, _em.GetComponentData<FlagState>(enemy).Value.z);
            Assert.AreEqual(1f, _em.GetComponentData<FlagState>(own).Value.z);
        }

        [Test]
        public void BuildingUnderConstruction_HidesItsFlag_UntilComplete()
        {
            var flag = Building(1, out var building);
            _em.AddComponentData(building, new ConstructionProgress { Value = 0.5f });

            Run();

            Assert.AreEqual(0f, _em.GetComponentData<FlagState>(flag).Value.z);

            _em.SetComponentEnabled<ConstructionProgress>(building, false);
            Run();

            Assert.AreEqual(1f, _em.GetComponentData<FlagState>(flag).Value.z);
        }

        private Entity Building(byte owner, out Entity building)
        {
            var flag = _em.CreateEntity(typeof(FlagState), typeof(FlagColor));
            building = _em.CreateEntity();
            _em.AddComponentData(building, new Faction { Value = owner });
            _em.AddComponentData(building, new LocalToWorld { Value = float4x4.Translate(new float3(0.5f, 0f, 0.5f)) });
            _em.AddComponentData(building, new CaptureProgress());
            _em.AddComponentData(building, new FlagMount { Flag = flag, Drop = 3f });
            return flag;
        }

        // One fog cell nobody sees, with factions 1 and 2 on opposing teams.
        private void AddFog()
        {
            var relations = new FactionRelations();
            relations.SetTeam(1, 1);
            relations.SetTeam(2, 2);
            _em.CreateSingleton(relations);
            _em.CreateSingleton(new LocalFogView { Viewer = 1, Team = 1, Active = true });
            var visible = new NativeArray<ushort>(1, Allocator.Persistent);
            _em.CreateSingleton(new FogOfWar { Visible = visible, Size = new int2(1, 1), CellSize = 1f });
        }

        private void Run() => _flags.Update(_world.Unmanaged);
    }
}
